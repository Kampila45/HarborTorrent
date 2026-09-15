using System.Net.Http.Json;
using HarborTorrent.Application.Abstractions.Search;
using HarborTorrent.Application.Contracts.Search;
using Microsoft.Extensions.Logging;

namespace HarborTorrent.Infrastructure.Search;

internal sealed class YtsSearchProvider : ITorrentSearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YtsSearchProvider> _logger;

    public string Name => "YTS";

    public YtsSearchProvider(HttpClient httpClient, ILogger<YtsSearchProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<SearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://yts.mx/api/v2/list_movies.json?query_term={Uri.EscapeDataString(query)}&limit=20";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            response.EnsureSuccessStatusCode();

            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

            var options = new System.Text.Json.JsonSerializerOptions { 
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString 
            };
            var apiResult = System.Text.Json.JsonSerializer.Deserialize<YtsApiResponse>(rawContent, options);

            if (apiResult?.Data?.Movies == null)
            {
                return Enumerable.Empty<SearchResultDto>();
            }

            var results = new List<SearchResultDto>();

            foreach (var movie in apiResult.Data.Movies)
            {
                if (movie.Torrents == null) continue;

                var movieTitle = $"{movie.Title} ({movie.Year})";

                foreach (var torrent in movie.Torrents)
                {
                    if (string.IsNullOrWhiteSpace(torrent.Hash)) continue;

                    var qualityTitle = $"{movieTitle} [{torrent.Quality} {torrent.Type}]";
                    var encodedTrackers = "&tr=udp%3A%2F%2Fopen.demonii.com%3A1337%2Fannounce&tr=udp%3A%2F%2Ftracker.openbittorrent.com%3A80&tr=udp%3A%2F%2Ftracker.coppersurfer.tk%3A6969&tr=udp%3A%2F%2Fglotorrents.pw%3A6969%2Fannounce&tr=udp%3A%2F%2Ftracker.opentrackr.org%3A1337%2Fannounce";
                    var magnetUri = $"magnet:?xt=urn:btih:{torrent.Hash}&dn={Uri.EscapeDataString(qualityTitle)}{encodedTrackers}";

                    results.Add(new SearchResultDto(
                        Title: qualityTitle,
                        InfoHash: torrent.Hash,
                        SizeBytes: torrent.SizeBytes,
                        Seeders: torrent.Seeds,
                        Leechers: torrent.Peers,
                        ProviderName: Name,
                        MagnetUri: magnetUri
                    ));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search YTS for query: {Query}", query);
            return Enumerable.Empty<SearchResultDto>();
        }
    }

    private sealed class YtsApiResponse
    {
        public string? Status { get; set; }
        public YtsData? Data { get; set; }
    }

    private sealed class YtsData
    {
        public List<YtsMovie>? Movies { get; set; }
    }

    private sealed class YtsMovie
    {
        public string? Title { get; set; }
        public int Year { get; set; }
        public List<YtsTorrent>? Torrents { get; set; }
    }

    private sealed class YtsTorrent
    {
        public string? Hash { get; set; }
        public string? Quality { get; set; }
        public string? Type { get; set; }
        public int Seeds { get; set; }
        public int Peers { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("size_bytes")]
        public long SizeBytes { get; set; }
    }
}
