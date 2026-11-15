using System.Security.Cryptography;
using System.Text;

namespace HarborTorrent.Api.Security;

/// <summary>
/// Issues and validates short-lived, file-scoped HMAC-SHA256 media tokens for
/// browser &lt;video&gt; streaming requests where custom auth headers cannot be attached.
/// </summary>
internal sealed class StreamTokenService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(4);

    private readonly byte[] _keyBytes;
    private readonly TimeProvider _timeProvider;

    public StreamTokenService(string launchToken, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(launchToken))
        {
            throw new ArgumentException("A non-empty launch token is required.", nameof(launchToken));
        }

        _keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(launchToken));
        _timeProvider = timeProvider;
    }

    public (string Token, DateTimeOffset ExpiresAtUtc) IssueToken(Guid torrentId, int fileIndex)
    {
        var expiresAtUtc = _timeProvider.GetUtcNow().Add(DefaultLifetime);
        var expiresUnix = expiresAtUtc.ToUnixTimeSeconds();
        var signature = ComputeSignature(torrentId, fileIndex, expiresUnix);
        var token = $"{expiresUnix}.{Base64UrlEncode(signature)}";
        return (token, expiresAtUtc);
    }

    public bool ValidateToken(string? token, Guid torrentId, int fileIndex)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var separatorIndex = token.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        if (!long.TryParse(token.AsSpan(0, separatorIndex), out var expiresUnix))
        {
            return false;
        }

        var nowUnix = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (expiresUnix < nowUnix)
        {
            return false;
        }

        if (!TryBase64UrlDecode(token[(separatorIndex + 1)..], out var providedSignature))
        {
            return false;
        }

        var expectedSignature = ComputeSignature(torrentId, fileIndex, expiresUnix);
        return CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature);
    }

    private byte[] ComputeSignature(Guid torrentId, int fileIndex, long expiresUnix)
    {
        var payload = $"{torrentId:N}:{fileIndex}:{expiresUnix}";
        return HMACSHA256.HashData(_keyBytes, Encoding.UTF8.GetBytes(payload));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryBase64UrlDecode(string value, out byte[] bytes)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }
}

