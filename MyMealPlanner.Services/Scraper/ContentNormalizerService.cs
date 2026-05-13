using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;

namespace MyMealPlanner.Services.Scraper;

/// <summary>
/// Converts a ScrapedRaw (Schema.org JSON-LD, Reddit post, or raw HTML blob)
/// into a normalised Recipe entity ready for the database.
/// Handles deduplication, slug generation, auto-tagging stubs and language detection.
/// </summary>
public class ContentNormalizerService : IContentNormalizerService
{
    private readonly ILogger<ContentNormalizerService> _logger;

    // Country → Continent quick lookup (abbreviated — full list in production)
    private static readonly Dictionary<string, string> CountryToContinent = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nigeria"] = "Africa",  ["ghana"] = "Africa",     ["kenya"] = "Africa",
        ["ethiopia"] = "Africa", ["egypt"] = "Africa",     ["morocco"] = "Africa",
        ["japan"] = "Asia",      ["china"] = "Asia",       ["india"] = "Asia",
        ["thailand"] = "Asia",   ["korea"] = "Asia",       ["vietnam"] = "Asia",
        ["france"] = "Europe",   ["italy"] = "Europe",     ["spain"] = "Europe",
        ["germany"] = "Europe",  ["uk"] = "Europe",        ["greece"] = "Europe",
        ["usa"] = "Americas",    ["mexico"] = "Americas",  ["brazil"] = "Americas",
        ["peru"] = "Americas",   ["colombia"] = "Americas",
        ["australia"] = "Oceania", ["new zealand"] = "Oceania",
    };

    public ContentNormalizerService(ILogger<ContentNormalizerService> logger)
        => _logger = logger;

    public async Task<Recipe?> NormalizeAsync(ScrapedRaw raw)
    {
        try
        {
            return raw.Platform switch
            {
                "Reddit"  => NormalizeRedditPost(raw),
                "Blog"    => NormalizeBlogHtml(raw),
                "Health"  => NormalizeHealthArticle(raw),
                _         => NormalizeSchemaOrg(raw)   // Website / YouTube description
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Normalizer] Failed to normalize {Url}", raw.SourceUrl);
            return null;
        }
    }

    private Recipe? NormalizeHealthArticle(ScrapedRaw raw)
    {
        // This is a placeholder for a more complex extraction logic.
        // In a real scenario, we would use AI to extract recipes from health articles
        // or create FoodHealthBenefit records.
        // For now, we return null to avoid cluttering the Recipe table with non-recipes.
        return null;
    }

    // ── Schema.org JSON-LD ────────────────────────────────────
    private Recipe? NormalizeSchemaOrg(ScrapedRaw raw)
    {
        using var doc  = JsonDocument.Parse(raw.RawJson);
        var root        = doc.RootElement;

        var title = GetString(root, "name");
        if (string.IsNullOrWhiteSpace(title)) return null;

        var description   = GetString(root, "description") ?? string.Empty;
        var imageUrl      = ExtractImageUrl(root);
        var prepTime      = ParseIsoDuration(GetString(root, "prepTime"));
        var cookTime      = ParseIsoDuration(GetString(root, "cookTime"));
        var servings      = ParseServings(GetString(root, "recipeYield"));
        var ingredients   = ExtractIngredients(root);
        var steps         = ExtractSteps(root);
        var country       = DetectCountry(title + " " + description);
        var continent     = country is not null && CountryToContinent.TryGetValue(country, out var c) ? c : null;

        if (ingredients.Count == 0 && steps.Count == 0) return null; // skip empty

        var recipe = new Recipe
        {
            Title            = CleanText(title),
            Slug             = GenerateSlug(title),
            Description      = CleanText(description),
            CoverImageUrl    = imageUrl,
            PrepTimeMinutes  = prepTime,
            CookTimeMinutes  = cookTime,
            Servings         = servings,
            OriginCountry    = country,
            OriginContinent  = continent,
            Source           = RecipeSource.Scraped,
            SourceUrl        = raw.SourceUrl,
            ScrapedAt        = DateTime.UtcNow,
            Language         = "en",
            IsApproved       = false,
            IsPublished      = false,
            Ingredients      = ingredients,
            Steps            = steps
        };

        return recipe;
    }

    // ── Reddit Post ───────────────────────────────────────────
    private Recipe? NormalizeRedditPost(ScrapedRaw raw)
    {
        using var doc = JsonDocument.Parse(raw.RawJson);
        var data       = doc.RootElement;

        var title    = GetString(data, "title") ?? "";
        var selftext = GetString(data, "selftext") ?? "";

        if (string.IsNullOrWhiteSpace(title) || selftext.Length < 100) return null;

        // Attempt to parse ingredients from text blocks
        var ingredients = ParseIngredientsFromText(selftext);
        var steps       = ParseStepsFromText(selftext);

        if (ingredients.Count == 0) return null;

        return new Recipe
        {
            Title           = CleanText(title),
            Slug            = GenerateSlug(title),
            Description     = selftext[..Math.Min(500, selftext.Length)],
            Source          = RecipeSource.Scraped,
            SourceUrl       = $"https://reddit.com{GetString(data, "permalink")}",
            ScrapedAt       = DateTime.UtcNow,
            Language        = "en",
            IsApproved      = false,
            IsPublished     = false,
            Ingredients     = ingredients,
            Steps           = steps
        };
    }

    // ── Blog HTML blob ────────────────────────────────────────
    private Recipe? NormalizeBlogHtml(ScrapedRaw raw)
    {
        // Minimal blog parser — extracts title and ingredient list from HTML text
        using var doc = JsonDocument.Parse(raw.RawJson);
        var html       = doc.RootElement.GetProperty("html").GetString() ?? "";

        var titleMatch = Regex.Match(html, @"<h[12][^>]*>(.*?)</h[12]>",
                                     RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!titleMatch.Success) return null;

        var title = Regex.Replace(titleMatch.Groups[1].Value, "<[^>]+>", "").Trim();
        if (string.IsNullOrWhiteSpace(title)) return null;

        return new Recipe
        {
            Title       = CleanText(title),
            Slug        = GenerateSlug(title),
            Description = "Recipe from blog — details pending AI enrichment.",
            Source      = RecipeSource.Scraped,
            SourceUrl   = raw.SourceUrl,
            ScrapedAt   = DateTime.UtcNow,
            Language    = "en",
            IsApproved  = false,
            IsPublished = false
        };
    }

    // ── Deduplication ─────────────────────────────────────────
    public string GenerateContentHash(string title, List<string> firstThreeIngredients)
    {
        var key   = (title + string.Join("|", firstThreeIngredients.Take(3)))
                    .ToLowerInvariant()
                    .Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes)[..32];
    }

    // ── Slug ──────────────────────────────────────────────────
    public string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = slug.Trim('-');
        if (slug.Length > 200) slug = slug[..200].TrimEnd('-');
        return slug;
    }

    // ── Private helpers ───────────────────────────────────────
    private static string? GetString(JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val))
            return val.ValueKind == JsonValueKind.String ? val.GetString() : val.GetRawText();
        return null;
    }

    private static string? ExtractImageUrl(JsonElement root)
    {
        if (!root.TryGetProperty("image", out var img)) return null;
        if (img.ValueKind == JsonValueKind.String) return img.GetString();
        if (img.ValueKind == JsonValueKind.Object && img.TryGetProperty("url", out var u))
            return u.GetString();
        if (img.ValueKind == JsonValueKind.Array && img.GetArrayLength() > 0)
            return img[0].ValueKind == JsonValueKind.String
                ? img[0].GetString()
                : img[0].TryGetProperty("url", out var au) ? au.GetString() : null;
        return null;
    }

    private static List<Ingredient> ExtractIngredients(JsonElement root)
    {
        if (!root.TryGetProperty("recipeIngredient", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
            return [];

        return arr.EnumerateArray()
            .Select((item, i) => ParseIngredientLine(item.GetString() ?? "", i))
            .Where(x => x is not null)
            .Cast<Ingredient>()
            .ToList();
    }

    private static Ingredient? ParseIngredientLine(string line, int order)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        // Simple regex: [amount] [unit] [name]
        var match = Regex.Match(line.Trim(),
            @"^(?<qty>[\d\.\s/½¼¾⅓⅔⅛]+)?\s*(?<unit>cup|tbsp|tsp|g|kg|ml|l|oz|lb|pound|tablespoon|teaspoon|clove|bunch|pinch|handful|slice|piece|medium|large|small)s?\b\.?\s*(?<name>.+)$",
            RegexOptions.IgnoreCase);

        if (match.Success)
        {
            decimal.TryParse(match.Groups["qty"].Value.Trim(), out var qty);
            return new Ingredient
            {
                Name      = CleanText(match.Groups["name"].Value),
                Quantity  = qty == 0 ? 1 : qty,
                Unit      = match.Groups["unit"].Value.ToLowerInvariant(),
                SortOrder = order
            };
        }

        return new Ingredient { Name = CleanText(line), Quantity = 1, Unit = "item", SortOrder = order };
    }

    private static List<RecipeStep> ExtractSteps(JsonElement root)
    {
        if (!root.TryGetProperty("recipeInstructions", out var arr)) return [];

        var steps = new List<RecipeStep>();
        int order = 1;

        if (arr.ValueKind == JsonValueKind.String)
        {
            steps.Add(new RecipeStep { Instruction = arr.GetString() ?? "", StepOrder = 1 });
            return steps;
        }

        if (arr.ValueKind != JsonValueKind.Array) return [];

        foreach (var item in arr.EnumerateArray())
        {
            string? text = null;
            if (item.ValueKind == JsonValueKind.String)
                text = item.GetString();
            else if (item.TryGetProperty("text", out var t))
                text = t.GetString();

            if (!string.IsNullOrWhiteSpace(text))
                steps.Add(new RecipeStep { Instruction = CleanText(text), StepOrder = order++ });
        }

        return steps;
    }

    private static List<Ingredient> ParseIngredientsFromText(string text)
    {
        // Looks for lines that resemble ingredients in Reddit posts
        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 3 && l.Length < 200)
            .Where(l => Regex.IsMatch(l, @"\d|cup|tbsp|tsp|g\b|kg|ml|oz|lb|clove", RegexOptions.IgnoreCase))
            .Take(30)
            .Select((l, i) => ParseIngredientLine(l, i))
            .Where(x => x is not null)
            .Cast<Ingredient>()
            .ToList();
        return lines;
    }

    private static List<RecipeStep> ParseStepsFromText(string text)
    {
        // Looks for numbered/bulleted steps
        var matches = Regex.Matches(text,
            @"(?:^|\n)\s*(?:\d+[\.\)]|[-*•])\s*(.+?)(?=\n|$)",
            RegexOptions.Multiline);

        return matches
            .Select((m, i) => new RecipeStep
            {
                Instruction = CleanText(m.Groups[1].Value),
                StepOrder   = i + 1
            })
            .Where(s => s.Instruction.Length > 10)
            .Take(20)
            .ToList();
    }

    private static int ParseIsoDuration(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return 0;
        var match = Regex.Match(iso, @"PT(?:(\d+)H)?(?:(\d+)M)?");
        int hours   = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        int minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        return hours * 60 + minutes;
    }

    private static int ParseServings(string? yield)
    {
        if (string.IsNullOrEmpty(yield)) return 4;
        var match = Regex.Match(yield, @"\d+");
        return match.Success ? Math.Clamp(int.Parse(match.Value), 1, 200) : 4;
    }

    private static string? DetectCountry(string text)
    {
        text = text.ToLowerInvariant();
        foreach (var country in CountryToContinent.Keys)
            if (text.Contains(country)) return char.ToUpper(country[0]) + country[1..];
        return null;
    }

    private static string CleanText(string text)
        => Regex.Replace(text.Trim(), @"\s{2,}", " ");
}
