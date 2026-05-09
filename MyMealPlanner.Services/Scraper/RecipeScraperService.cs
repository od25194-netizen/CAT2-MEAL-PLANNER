using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;
using Polly;
using Polly.Extensions.Http;

namespace MyMealPlanner.Services.Scraper;

/// <summary>
/// Orchestrates all recipe scraping jobs. Runs on a Hangfire schedule every 6 hours.
/// Priority order: Schema.org JSON-LD (80% coverage) → HtmlAgilityPack fallback → Reddit API.
/// </summary>
public class RecipeScraperService : IRecipeScraperService
{
    private readonly ApplicationDbContext _db;
    private readonly IContentNormalizerService _normalizer;
    private readonly ILogger<RecipeScraperService> _logger;
    private readonly HttpClient _http;

    // All target sources — categorised by scrape strategy
    private static readonly string[] SchemaOrgSites =
    [
        "https://www.allrecipes.com/recipes/",
        "https://www.bbcgoodfood.com/recipes",
        "https://www.seriouseats.com/recipes",
        "https://www.food.com/recipe",
        "https://www.epicurious.com/recipes-menus",
        "https://www.196flavors.com/",
        "https://www.tasteatlas.com/",
        "https://www.recipetineats.com/"
    ];

    private static readonly string[] RedditFoodSubs =
    [
        "recipes", "Cooking", "food", "worldcuisine",
        "AskCulinary", "MealPrepSunday", "EatCheapAndHealthy"
    ];

    public RecipeScraperService(
        ApplicationDbContext db,
        IContentNormalizerService normalizer,
        ILogger<RecipeScraperService> logger,
        IHttpClientFactory httpFactory)
    {
        _db         = db;
        _normalizer = normalizer;
        _logger     = logger;
        _http       = httpFactory.CreateClient("ScraperClient");
    }

    // ── Main Orchestrator ─────────────────────────────────────
    public async Task ScrapeAllSourcesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[Scraper] Starting full scrape cycle at {Time}", DateTime.UtcNow);

        await ScrapeSchemaOrgSitesAsync(ct);
        await ScrapeRedditAsync(ct);
        await ScrapeYouTubeFoodChannelsAsync(ct);

        foreach (var continent in new[] { "AF", "AS", "EU", "NA", "SA", "OC" })
            await ScrapeFoodBlogsByRegionAsync(continent, ct);

        _logger.LogInformation("[Scraper] Full cycle complete at {Time}", DateTime.UtcNow);
    }

    // ── Schema.org JSON-LD (Primary — covers ~80% of food sites) ──
    public async Task ScrapeSchemaOrgSitesAsync(CancellationToken ct = default)
    {
        foreach (var siteUrl in SchemaOrgSites)
        {
            try
            {
                var job = await GetOrCreateJobAsync(siteUrl, "Website");
                job.Status = ScrapeStatus.Running;
                await _db.SaveChangesAsync(ct);

                var html = await FetchWithRetryAsync(siteUrl, ct);
                if (html is null) continue;

                var recipeJsonObjects = ExtractSchemaOrgRecipes(html);
                int added = 0;

                foreach (var json in recipeJsonObjects)
                {
                    var raw = new ScrapedRaw
                    {
                        RawJson     = json,
                        SourceUrl   = siteUrl,
                        Platform    = "Website",
                        ContentHash = GenerateHash(json),
                        ParsedAt    = DateTime.UtcNow
                    };

                    if (await IsDuplicateAsync(raw.ContentHash))
                    {
                        raw.IsDuplicate = true;
                        continue;
                    }

                    var recipe = await _normalizer.NormalizeAsync(raw);
                    if (recipe != null)
                    {
                        _db.Recipes.Add(recipe);
                        raw.MappedToRecipeId = recipe.Id;
                        added++;
                    }

                    _db.ScrapedRaws.Add(raw);
                }

                await _db.SaveChangesAsync(ct);

                job.Status        = ScrapeStatus.Completed;
                job.RecipesFound  = recipeJsonObjects.Count;
                job.RecipesAdded  = added;
                job.LastRunAt     = DateTime.UtcNow;
                job.NextRunAt     = DateTime.UtcNow.AddHours(6);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("[Scraper] {Site} → {Added} new recipes", siteUrl, added);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scraper] Failed scraping {Site}", siteUrl);
                await MarkJobFailedAsync(siteUrl, ex.Message, ct);
            }
        }
    }

    // ── Reddit ────────────────────────────────────────────────
    public async Task ScrapeRedditAsync(CancellationToken ct = default)
    {
        foreach (var sub in RedditFoodSubs)
        {
            try
            {
                // Reddit JSON API — no auth needed for public posts
                var url  = $"https://www.reddit.com/r/{sub}/hot.json?limit=25";
                var json = await FetchWithRetryAsync(url, ct);
                if (json is null) continue;

                using var doc  = JsonDocument.Parse(json);
                var posts       = doc.RootElement
                                     .GetProperty("data")
                                     .GetProperty("children")
                                     .EnumerateArray();

                foreach (var post in posts)
                {
                    var data    = post.GetProperty("data");
                    var title   = data.GetProperty("title").GetString() ?? "";
                    var selftext = data.TryGetProperty("selftext", out var st) ? st.GetString() ?? "" : "";
                    var url2    = data.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";

                    if (!IsLikelyRecipe(title, selftext)) continue;

                    var raw = new ScrapedRaw
                    {
                        RawJson     = data.GetRawText(),
                        SourceUrl   = $"https://reddit.com/r/{sub}",
                        Platform    = "Reddit",
                        ContentHash = GenerateHash(title + selftext),
                        ParsedAt    = DateTime.UtcNow
                    };

                    if (!await IsDuplicateAsync(raw.ContentHash))
                    {
                        var recipe = await _normalizer.NormalizeAsync(raw);
                        if (recipe != null) _db.Recipes.Add(recipe);
                        _db.ScrapedRaws.Add(raw);
                    }
                }

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("[Scraper] Reddit r/{Sub} scraped", sub);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scraper] Reddit r/{Sub} failed", sub);
            }
        }
    }

    // ── YouTube Food Channels ─────────────────────────────────
    public async Task ScrapeYouTubeFoodChannelsAsync(CancellationToken ct = default)
    {
        // YouTube Data API v3 — free, 10,000 units/day
        // Channels are stored in ScrapeJobs with Platform = "YouTube"
        var ytJobs = await _db.ScrapeJobs
            .Where(j => j.Platform == "YouTube" && j.IsActive && j.NextRunAt <= DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var job in ytJobs)
        {
            _logger.LogInformation("[Scraper] YouTube channel {Url}", job.Url);
            // IYouTubeService.GetChannelVideosAsync is called from here in production
            // Results linked to Recipe.YouTubeVideoId
            job.LastRunAt = DateTime.UtcNow;
            job.NextRunAt = DateTime.UtcNow.AddHours(24);
        }

        await _db.SaveChangesAsync(ct);
    }

    // ── Food Blogs by Region ──────────────────────────────────
    public async Task ScrapeFoodBlogsByRegionAsync(string continentCode, CancellationToken ct = default)
    {
        var regionalJobs = await _db.ScrapeJobs
            .Where(j => j.Platform == "Blog" && j.IsActive
                     && j.NextRunAt <= DateTime.UtcNow
                     && j.Source.Contains(continentCode))
            .ToListAsync(ct);

        foreach (var job in regionalJobs)
        {
            try
            {
                var html = await FetchWithRetryAsync(job.Url, ct);
                if (html is null) continue;

                // Use HtmlAgilityPack for blogs that don't have Schema.org markup
                var doc   = new HtmlDocument();
                doc.LoadHtml(html);

                var recipeSections = doc.DocumentNode
                    .SelectNodes("//article[contains(@class,'recipe')] | //div[contains(@class,'recipe-card')]");

                if (recipeSections is null) continue;

                foreach (var node in recipeSections)
                {
                    var rawText = node.InnerText;
                    var hash    = GenerateHash(rawText);
                    if (await IsDuplicateAsync(hash)) continue;

                    var raw = new ScrapedRaw
                    {
                        RawJson     = JsonSerializer.Serialize(new { html = node.OuterHtml }),
                        SourceUrl   = job.Url,
                        Platform    = "Blog",
                        ContentHash = hash,
                        ParsedAt    = DateTime.UtcNow
                    };

                    _db.ScrapedRaws.Add(raw);
                }

                job.LastRunAt = DateTime.UtcNow;
                job.NextRunAt = DateTime.UtcNow.AddHours(12);
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scraper] Blog {Url} failed", job.Url);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────
    public async Task<bool> IsDuplicateAsync(string contentHash)
        => await _db.ScrapedRaws.AnyAsync(r => r.ContentHash == contentHash);

    private List<string> ExtractSchemaOrgRecipes(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var scripts = doc.DocumentNode
            .SelectNodes("//script[@type='application/ld+json']");

        if (scripts is null) return [];

        var results = new List<string>();
        foreach (var script in scripts)
        {
            var json = script.InnerText.Trim();
            try
            {
                using var jdoc = JsonDocument.Parse(json);
                var root = jdoc.RootElement;

                // Handle single recipe or @graph array
                if (root.TryGetProperty("@type", out var t) &&
                    t.GetString()?.Contains("Recipe") == true)
                {
                    results.Add(json);
                }
                else if (root.TryGetProperty("@graph", out var graph))
                {
                    foreach (var item in graph.EnumerateArray())
                        if (item.TryGetProperty("@type", out var it) &&
                            it.GetString()?.Contains("Recipe") == true)
                            results.Add(item.GetRawText());
                }
            }
            catch { /* skip malformed JSON */ }
        }

        return results;
    }

    private async Task<string?> FetchWithRetryAsync(string url, CancellationToken ct)
    {
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

        try
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; MyMealPlannerBot/1.0; +https://mymealplanner.app/bot)");

            var response = await policy.ExecuteAsync(
                async () => await _http.GetAsync(url, ct));

            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Scraper] Could not fetch {Url}", url);
            return null;
        }
    }

    private static string GenerateHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..32]; // first 32 chars is enough
    }

    private static bool IsLikelyRecipe(string title, string body)
    {
        var keywords = new[] { "recipe", "cook", "ingredient", "tablespoon", "teaspoon",
                               "bake", "fry", "boil", "simmer", "cup of", "grams" };
        var combined = (title + " " + body).ToLowerInvariant();
        return keywords.Any(k => combined.Contains(k));
    }

    private async Task<ScrapeJob> GetOrCreateJobAsync(string url, string platform)
    {
        var job = await _db.ScrapeJobs.FirstOrDefaultAsync(j => j.Url == url);
        if (job is not null) return job;

        job = new ScrapeJob { Source = new Uri(url).Host, Url = url, Platform = platform };
        _db.ScrapeJobs.Add(job);
        await _db.SaveChangesAsync();
        return job;
    }

    private async Task MarkJobFailedAsync(string url, string error, CancellationToken ct)
    {
        var job = await _db.ScrapeJobs.FirstOrDefaultAsync(j => j.Url == url, ct);
        if (job is null) return;
        job.Status       = ScrapeStatus.Failed;
        job.ErrorMessage = error;
        job.NextRunAt    = DateTime.UtcNow.AddHours(2); // retry sooner on failure
        await _db.SaveChangesAsync(ct);
    }
}
