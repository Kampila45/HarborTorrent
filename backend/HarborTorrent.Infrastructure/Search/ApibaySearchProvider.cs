using System.Net.Http.Json;
using HarborTorrent.Application.Abstractions.Search;
using HarborTorrent.Application.Contracts.Search;
using Microsoft.Extensions.Logging;

namespace HarborTorrent.Infrastructure.Search;

internal sealed class ApibaySearchProvider : ITorrentSearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApibaySearchProvider> _logger;

    public string Name => "The Pirate Bay";

    public ApibaySearchProvider(HttpClient httpClient, ILogger<ApibaySearchProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<SearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://apibay.org/q.php?q={Uri.EscapeDataString(query)}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            response.EnsureSuccessStatusCode();

            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var apiResults = await response.Content.ReadFromJsonAsync<ApibayResult[]>(options, cancellationToken: cancellationToken);

            if (apiResults == null)
            {
                return Enumerable.Empty<SearchResultDto>();
            }

            // Apibay returns a dummy entry if nothing is found (id = "0")
            return apiResults
                .Where(r => r.Id != "0" && !string.IsNullOrWhiteSpace(r.InfoHash))
                .Select(r => 
                {
                    var encodedTrackers = "&tr=udp%3A%2F%2Fopen.demonii.com%3A1337%2Fannounce&tr=udp%3A%2F%2Ftracker.openbittorrent.com%3A80&tr=udp%3A%2F%2Ftracker.coppersurfer.tk%3A6969&tr=udp%3A%2F%2Fglotorrents.pw%3A6969%2Fannounce&tr=udp%3A%2F%2Ftracker.opentrackr.org%3A1337%2Fannounce";
                    return new SearchResultDto(
                        Title: r.Name ?? "Unknown",
                        InfoHash: r.InfoHash!,
                        SizeBytes: long.TryParse(r.Size, out var size) ? size : 0,
                        Seeders: int.TryParse(r.Seeders, out var seeders) ? seeders : 0,
                        Leechers: int.TryParse(r.Leechers, out var leechers) ? leechers : 0,
                        ProviderName: Name,
                        MagnetUri: $"magnet:?xt=urn:btih:{r.InfoHash}&dn={Uri.EscapeDataString(r.Name ?? "Unknown")}{encodedTrackers}"
                    );
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Apibay for query: {Query}", query);
            return Enumerable.Empty<SearchResultDto>();
        }
    }

    private sealed class ApibayResult
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("info_hash")]
        public string? InfoHash { get; set; }
        
        public string? Leechers { get; set; }
        public string? Seeders { get; set; }
        public string? Size { get; set; }
    }
}
