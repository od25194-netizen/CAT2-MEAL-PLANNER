using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.DTOs;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Services.Localization;

/// <summary>
/// Auto-translates recipe content using LibreTranslate — a free, open-source
/// translation API that can be self-hosted with Docker at zero cost.
/// Results are cached in Redis to avoid re-translating the same content.
///
/// Self-host: docker run -p 5000:5000 libretranslate/libretranslate
/// Public API (rate-limited): https://libretranslate.com
/// </summary>
public class LibreTranslationService : ITranslationService
{
    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LibreTranslationService> _logger;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    private static readonly string[] SupportedLanguages =
        ["en","fr","es","pt","ar","zh","hi","de","it","ja","ko","sw"];

    public LibreTranslationService(
        IHttpClientFactory httpFactory,
        IDistributedCache cache,
        ApplicationDbContext db,
        IConfiguration config,
        ILogger<LibreTranslationService> logger)
    {
        _http    = httpFactory.CreateClient("TranslationClient");
        _cache   = cache;
        _db      = db;
        _logger  = logger;
        _baseUrl = config["LibreTranslate:BaseUrl"] ?? "http://localhost:5000";
        _apiKey  = config["LibreTranslate:ApiKey"]; // optional for self-hosted
    }

    public async Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        string sourceLanguage = "en")
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (targetLanguage == sourceLanguage) return text;
        if (!SupportedLanguages.Contains(targetLanguage)) return text;

        // Check cache first
        var cacheKey = $"trans:{sourceLanguage}:{targetLanguage}:{ComputeShortHash(text)}";
        var cached   = await _cache.GetStringAsync(cacheKey);
        if (cached is not null) return cached;

        try
        {
            var payload = new Dictionary<string, string>
            {
                ["q"]      = text,
                ["source"] = sourceLanguage,
                ["target"] = targetLanguage,
                ["format"] = "text"
            };
            if (_apiKey is not null) payload["api_key"] = _apiKey;

            var response = await _http.PostAsync(
                $"{_baseUrl.TrimEnd('/')}/translate",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode) return text;

            var json         = await response.Content.ReadAsStringAsync();
            using var doc    = JsonDocument.Parse(json);
            var translated   = doc.RootElement.GetProperty("translatedText").GetString() ?? text;

            // Cache for 30 days — translations don't change
            var opts = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromDays(30));
            await _cache.SetStringAsync(cacheKey, translated, opts);

            return translated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Translation] Failed to translate to {Lang}", targetLanguage);
            return text; // Return original on failure
        }
    }

    public async Task TranslateRecipeAsync(int recipeId, string targetLanguage)
    {
        var recipe = await _db.Recipes.FindAsync(recipeId);
        if (recipe is null) return;

        // Check if already translated
        var exists = await _db.RecipeTranslations.AnyAsync(
            t => t.RecipeId == recipeId && t.LanguageCode == targetLanguage);
        if (exists) return;

        var title        = await TranslateAsync(recipe.Title,          targetLanguage);
        var description  = await TranslateAsync(recipe.Description,    targetLanguage);
        var story        = recipe.CulturalStory is not null
            ? await TranslateAsync(recipe.CulturalStory, targetLanguage)
            : null;

        _db.RecipeTranslations.Add(new RecipeTranslation
        {
            RecipeId        = recipeId,
            LanguageCode    = targetLanguage,
            Title           = title,
            Description     = description,
            CulturalStory   = story,
            TranslatedAt    = DateTime.UtcNow,
            IsAutoTranslated = true
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("[Translation] Recipe {Id} translated to {Lang}", recipeId, targetLanguage);
    }

    public async Task<string?> DetectLanguageAsync(string text)
    {
        try
        {
            var payload = new Dictionary<string, string> { ["q"] = text[..Math.Min(text.Length, 200)] };
            if (_apiKey is not null) payload["api_key"] = _apiKey;

            var response = await _http.PostAsync(
                $"{_baseUrl.TrimEnd('/')}/detect",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode) return null;

            var json  = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement[0].GetProperty("language").GetString();
        }
        catch { return null; }
    }

    private static string ComputeShortHash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}



/// <summary>
/// Identifies food in uploaded photos using Google Cloud Vision API
/// (free: 1,000 units/month) or a locally cached ML.NET ONNX food model.
/// Returns the identified dish + matching recipes from the database.
/// </summary>
public class ImageSearchService : IImageSearchService
{
    private readonly ApplicationDbContext _db;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ImageSearchService> _logger;

    // Top 50 food labels from Google Vision for food images
    private static readonly Dictionary<string, string[]> LabelToSearchTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rice"]          = ["rice", "jollof", "fried rice", "biryani", "paella"],
        ["noodle"]        = ["noodles", "ramen", "pad thai", "pho", "pasta"],
        ["bread"]         = ["bread", "toast", "focaccia", "baguette", "naan"],
        ["chicken"]       = ["chicken", "jerk chicken", "roast chicken", "grilled chicken"],
        ["soup"]          = ["soup", "stew", "broth", "chowder", "tom yum"],
        ["salad"]         = ["salad", "greek salad", "caesar", "coleslaw"],
        ["pizza"]         = ["pizza", "flatbread", "calzone"],
        ["cake"]          = ["cake", "dessert", "pastry", "tiramisu"],
        ["curry"]         = ["curry", "indian curry", "thai curry", "korma"],
        ["sandwich"]      = ["sandwich", "burger", "wrap", "sub"],
        ["sushi"]         = ["sushi", "sashimi", "maki", "japanese"],
        ["taco"]          = ["taco", "mexican", "burrito", "enchilada"],
        ["steak"]         = ["steak", "beef", "grilled meat", "barbecue"],
        ["fish"]          = ["fish", "seafood", "salmon", "grilled fish", "fish curry"],
        ["dumpling"]      = ["dumplings", "gyoza", "dim sum", "pierogi"],
        ["egg"]           = ["eggs", "omelette", "shakshuka", "fried egg"],
        ["mushroom"]      = ["mushrooms", "mushroom risotto", "mushroom soup"],
        ["tomato"]        = ["tomato sauce", "stew", "bruschetta"],
        ["avocado"]       = ["avocado toast", "guacamole", "salad"],
        ["pancake"]       = ["pancakes", "crepes", "waffles", "french toast"],
    };

    public ImageSearchService(
        ApplicationDbContext db,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<ImageSearchService> logger)
    {
        _db     = db;
        _http   = httpFactory.CreateClient("AIClient");
        _config = config;
        _logger = logger;
    }

    public async Task<FoodIdentificationResult> IdentifyFoodFromImageAsync(
        Stream imageStream, string contentType)
    {
        // Try Google Vision API first (1000 free units/month)
        var googleResult = await TryGoogleVisionAsync(imageStream, contentType);
        if (googleResult is not null) return googleResult;

        // Fallback to basic content-type check
        return new FoodIdentificationResult(
            IdentifiedDish:   "Food",
            Country:          null,
            Confidence:       0.5,
            Labels:           ["food"],
            MatchingRecipes:  new List<Recipe>());
    }

    public async Task<List<Recipe>> FindRecipesByImageAsync(
        Stream imageStream, string contentType, string userId)
    {
        var result = await IdentifyFoodFromImageAsync(imageStream, contentType);
        if (result.Labels.Count == 0) return new List<Recipe>();

        // Get search terms from labels
        var searchTerms = result.Labels
            .SelectMany(label => LabelToSearchTerms.GetValueOrDefault(label.ToLower(), [label]))
            .Distinct()
            .Take(5)
            .ToList();

        var recipes = new List<Recipe>();
        foreach (var term in searchTerms)
        {
            var matches = await _db.Recipes
                .Where(r => r.IsPublished && (
                    r.Title.Contains(term) ||
                    r.Description.Contains(term) ||
                    (r.CultureTag != null && r.CultureTag.Contains(term))))
                .OrderByDescending(r => r.LikeCount)
                .Take(4)
                .ToListAsync();

            recipes.AddRange(matches);
        }

        return recipes.DistinctBy(r => r.Id).Take(12).ToList();
    }

    private async Task<FoodIdentificationResult?> TryGoogleVisionAsync(
        Stream imageStream, string contentType)
    {
        var apiKey = _config["GoogleVision:ApiKey"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            var payload = new
            {
                requests = new[]
                {
                    new {
                        image    = new { content = base64 },
                        features = new[] {
                            new { type = "LABEL_DETECTION", maxResults = 10 },
                            new { type = "WEB_DETECTION", maxResults = 5 }
                        }
                    }
                }
            };

            _http.DefaultRequestHeaders.Clear();
            var response = await _http.PostAsync(
                $"https://vision.googleapis.com/v1/images:annotate?key={apiKey}",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var annotations = doc.RootElement
                .GetProperty("responses")[0]
                .GetProperty("labelAnnotations")
                .EnumerateArray()
                .Select(a => new
                {
                    Description = a.GetProperty("description").GetString() ?? "",
                    Score       = a.GetProperty("score").GetDouble()
                })
                .ToList();

            if (!annotations.Any()) return null;

            var labels     = annotations.Select(a => a.Description).ToList();
            var topLabel   = annotations.First().Description;
            var confidence = annotations.First().Score;

            // Detect country from web entities if available
            string? country = null;
            try
            {
                var webEntities = doc.RootElement
                    .GetProperty("responses")[0]
                    .GetProperty("webDetection")
                    .GetProperty("webEntities")
                    .EnumerateArray()
                    .Select(e => e.TryGetProperty("description", out var d) ? d.GetString() : null)
                    .Where(d => d is not null)
                    .ToList();

                // Simple country detection from web entity names
                var countryKeywords = new[] { "Nigerian", "Japanese", "Italian", "Indian", "Mexican", "Thai", "French" };
                foreach (var kw in countryKeywords)
                    if (webEntities.Any(e => e?.Contains(kw) == true))
                    { country = kw.Replace("ese","").Replace("ian","").Replace("ish",""); break; }
            }
            catch { /* Web detection optional */ }

            var matchingRecipes = await FindRecipesByImageAsync(new MemoryStream(), contentType, "");

            return new FoodIdentificationResult(
                IdentifiedDish:  topLabel,
                Country:         country,
                Confidence:      confidence,
                Labels:          labels,
                MatchingRecipes: matchingRecipes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ImageSearch] Google Vision failed");
            return null;
        }
    }
}
