using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using MyMealPlanner.Core.DTOs;
using MyMealPlanner.Core.Interfaces;

namespace MyMealPlanner.Services.YouTube;

/// <summary>
/// Integrates YouTube Data API v3 (free tier: 10,000 units/day).
/// Video metadata is cached in Redis for 10 minutes to save quota.
/// The embed URL uses the YouTube IFrame API — views register on the
/// creator's channel while the user stays within My Meal Planner.
/// </summary>
public class YouTubeService : IYouTubeService
{
    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly ILogger<YouTubeService> _logger;
    private readonly string _apiKey;

    private const string BaseUrl   = "https://www.googleapis.com/youtube/v3";
    private const int CacheMinutes = 10;

    public YouTubeService(
        IHttpClientFactory httpFactory,
        IDistributedCache cache,
        IConfiguration config,
        ILogger<YouTubeService> logger)
    {
        _http   = httpFactory.CreateClient("YouTubeClient");
        _cache  = cache;
        _logger = logger;
        _apiKey = config["YouTube:ApiKey"] ?? throw new InvalidOperationException("YouTube:ApiKey not configured");
    }

    // ── Embed URL ─────────────────────────────────────────────
    /// <summary>
    /// Returns an embed URL that keeps the user inside the app.
    /// The iframe counts the view on the creator's channel automatically.
    /// </summary>
    public string GetEmbedUrl(string videoId)
        => $"https://www.youtube.com/embed/{videoId}" +
           $"?rel=0&modestbranding=1&enablejsapi=1&origin=https://mymealplanner.app";

    // ── Single video metadata ─────────────────────────────────
    public async Task<YouTubeVideoDto?> GetVideoMetaAsync(string videoId)
    {
        var cacheKey = $"yt:video:{videoId}";
        var cached   = await _cache.GetStringAsync(cacheKey);

        if (cached is not null)
            return JsonSerializer.Deserialize<YouTubeVideoDto>(cached);

        try
        {
            var url = $"{BaseUrl}/videos?part=snippet,statistics,contentDetails" +
                      $"&id={videoId}&key={_apiKey}";

            var response = await _http.GetFromJsonAsync<JsonElement>(url);
            var items    = response.GetProperty("items");

            if (items.GetArrayLength() == 0) return null;

            var item        = items[0];
            var snippet     = item.GetProperty("snippet");
            var stats       = item.GetProperty("statistics");
            var contentDet  = item.GetProperty("contentDetails");

            var dto = new YouTubeVideoDto(
                VideoId:      videoId,
                Title:        snippet.GetProperty("title").GetString() ?? "",
                ChannelName:  snippet.GetProperty("channelTitle").GetString() ?? "",
                ChannelId:    snippet.GetProperty("channelId").GetString() ?? "",
                ThumbnailUrl: snippet.GetProperty("thumbnails")
                                     .GetProperty("high")
                                     .GetProperty("url").GetString() ?? "",
                EmbedUrl:     GetEmbedUrl(videoId),
                ViewCount:    long.TryParse(
                                  stats.TryGetProperty("viewCount", out var vc)
                                       ? vc.GetString() : "0", out var v) ? v : 0,
                LikeCount:    long.TryParse(
                                  stats.TryGetProperty("likeCount", out var lc)
                                       ? lc.GetString() : "0", out var l) ? l : 0,
                Duration:     FormatDuration(
                                  contentDet.GetProperty("duration").GetString() ?? ""),
                PublishedAt:  DateTime.TryParse(
                                  snippet.GetProperty("publishedAt").GetString(), out var pub)
                                  ? pub : DateTime.UtcNow);

            var opts = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CacheMinutes));

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dto), opts);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YouTube] Failed to fetch meta for video {Id}", videoId);
            return null;
        }
    }

    // ── Search ────────────────────────────────────────────────
    public async Task<List<YouTubeVideoDto>> SearchFoodVideosAsync(
        string query, int maxResults = 10)
    {
        var cacheKey = $"yt:search:{query.ToLowerInvariant()}:{maxResults}";
        var cached   = await _cache.GetStringAsync(cacheKey);

        if (cached is not null)
            return JsonSerializer.Deserialize<List<YouTubeVideoDto>>(cached) ?? [];

        try
        {
            var encoded = Uri.EscapeDataString(query + " recipe cooking");
            var url = $"{BaseUrl}/search?part=snippet&q={encoded}" +
                      $"&type=video&maxResults={maxResults}" +
                      $"&videoCategoryId=26&key={_apiKey}"; // 26 = How-to & Style

            var response = await _http.GetFromJsonAsync<JsonElement>(url);
            var ids = response.GetProperty("items")
                              .EnumerateArray()
                              .Select(i => i.GetProperty("id").GetProperty("videoId").GetString())
                              .Where(id => id is not null)
                              .Cast<string>()
                              .ToList();

            var dtos = new List<YouTubeVideoDto>();
            foreach (var id in ids)
            {
                var dto = await GetVideoMetaAsync(id);
                if (dto is not null) dtos.Add(dto);
            }

            var opts = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dtos), opts);

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YouTube] Search failed for query: {Query}", query);
            return [];
        }
    }

    // ── Channel videos ────────────────────────────────────────
    public async Task<List<YouTubeVideoDto>> GetChannelVideosAsync(
        string channelId, int maxResults = 20)
    {
        try
        {
            var url = $"{BaseUrl}/search?part=snippet&channelId={channelId}" +
                      $"&type=video&order=date&maxResults={maxResults}&key={_apiKey}";

            var response = await _http.GetFromJsonAsync<JsonElement>(url);
            var ids = response.GetProperty("items")
                              .EnumerateArray()
                              .Select(i => i.GetProperty("id").GetProperty("videoId").GetString())
                              .Where(id => id is not null)
                              .Cast<string>()
                              .ToList();

            var dtos = new List<YouTubeVideoDto>();
            foreach (var id in ids)
            {
                var dto = await GetVideoMetaAsync(id);
                if (dto is not null) dtos.Add(dto);
            }

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YouTube] Channel {Id} videos failed", channelId);
            return [];
        }
    }

    // ── Helper ────────────────────────────────────────────────
    private static string FormatDuration(string isoDuration)
    {
        // PT4M30S → 4:30  |  PT1H2M3S → 1:02:03
        var h = System.Text.RegularExpressions.Regex.Match(isoDuration, @"(\d+)H");
        var m = System.Text.RegularExpressions.Regex.Match(isoDuration, @"(\d+)M");
        var s = System.Text.RegularExpressions.Regex.Match(isoDuration, @"(\d+)S");

        int hours   = h.Success ? int.Parse(h.Groups[1].Value) : 0;
        int minutes = m.Success ? int.Parse(m.Groups[1].Value) : 0;
        int seconds = s.Success ? int.Parse(s.Groups[1].Value) : 0;

        return hours > 0
            ? $"{hours}:{minutes:D2}:{seconds:D2}"
            : $"{minutes}:{seconds:D2}";
    }
}
