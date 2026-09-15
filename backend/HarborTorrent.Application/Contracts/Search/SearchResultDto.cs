namespace HarborTorrent.Application.Contracts.Search;

public record SearchResultDto(
    string Title,
    string InfoHash,
    long SizeBytes,
    int Seeders,
    int Leechers,
    string ProviderName,
    string MagnetUri
);
