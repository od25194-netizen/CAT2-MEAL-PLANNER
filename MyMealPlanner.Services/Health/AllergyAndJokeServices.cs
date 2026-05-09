using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.DTOs;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Services.Health;

/// <summary>
/// Checks recipes and ingredient lists against user's known allergens.
/// Detects hidden allergen sources (e.g. soy sauce contains gluten).
/// </summary>
public class AllergyService : IAllergyService
{
    private readonly ApplicationDbContext _db;

    // Hidden sources — allergen hidden in common ingredients
    private static readonly Dictionary<AllergenType, string[]> HiddenSources = new()
    {
        [AllergenType.Gluten]    = ["soy sauce", "malt", "beer", "barley", "seitan", "spelt", "wheat starch", "rye"],
        [AllergenType.Milk]      = ["butter", "cream", "ghee", "casein", "whey", "lactose", "curd", "paneer"],
        [AllergenType.Eggs]      = ["mayonnaise", "meringue", "albumin", "lecithin (egg)", "caesar dressing"],
        [AllergenType.Peanuts]   = ["peanut oil", "groundnut oil", "mixed nuts", "satay sauce", "some curries"],
        [AllergenType.Soy]       = ["edamame", "miso", "tofu", "tempeh", "soy lecithin", "some margarines"],
        [AllergenType.Shellfish] = ["worcestershire sauce", "some fish sauces", "caesar dressing"],
        [AllergenType.Sesame]    = ["tahini", "hummus", "sesame oil", "some salad dressings"],
        [AllergenType.Fish]      = ["worcestershire sauce", "caesar dressing", "some fish sauces", "anchovies"],
    };

    private static readonly Dictionary<AllergenType, (string Mild, string Severe, string First, AllergenRisk Risk)> AllergenInfo = new()
    {
        [AllergenType.Peanuts]   = ("Hives, itchy mouth", "Throat swelling, difficulty breathing", "EpiPen + call emergency services", AllergenRisk.Critical),
        [AllergenType.TreeNuts]  = ("Hives, nausea", "Anaphylaxis possible", "EpiPen + call emergency services", AllergenRisk.Critical),
        [AllergenType.Shellfish] = ("Stomach cramps, rash", "Anaphylaxis", "EpiPen + call emergency services", AllergenRisk.Critical),
        [AllergenType.Fish]      = ("Hives, nausea, vomiting", "Anaphylaxis", "EpiPen if severe + ER", AllergenRisk.High),
        [AllergenType.Milk]      = ("Bloating, diarrhea, cramps", "Rare anaphylaxis", "Antihistamine; avoid dairy", AllergenRisk.Moderate),
        [AllergenType.Eggs]      = ("Skin rash, runny nose, stomach pain", "Rare anaphylaxis", "Antihistamine if mild", AllergenRisk.Moderate),
        [AllergenType.Wheat]     = ("Bloating, diarrhea, fatigue", "Severe celiac reaction", "Remove gluten immediately", AllergenRisk.Moderate),
        [AllergenType.Gluten]    = ("Cramps, fatigue, brain fog", "Celiac flare", "Strict gluten-free diet", AllergenRisk.Moderate),
        [AllergenType.Soy]       = ("Hives, digestive upset", "Rare anaphylaxis", "Antihistamine; read labels", AllergenRisk.Low),
        [AllergenType.Sesame]    = ("Rash, stomach pain", "Rare anaphylaxis", "Antihistamine; check labels", AllergenRisk.Moderate),
        [AllergenType.Mustard]   = ("Skin rash, runny nose", "Rare anaphylaxis", "Antihistamine", AllergenRisk.Low),
    };

    public AllergyService(ApplicationDbContext db) => _db = db;

    public async Task<AllergyCheckResult> CheckRecipeAsync(int recipeId, string userId)
    {
        var user = await _db.Users.FindAsync(userId);
        var allergenList = user?.AllergiesJson?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new();

        var recipe = await _db.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (recipe is null || !allergenList.Any())
            return new AllergyCheckResult(false, new(), null);

        var ingredientNames = recipe.Ingredients.Select(i => i.Name.ToLowerInvariant()).ToList();
        return await CheckIngredientsAsync(ingredientNames, allergenList);
    }

    public async Task<AllergyCheckResult> CheckIngredientsAsync(
        List<string> ingredients, List<string> userAllergens)
    {
        var found = new List<AllergenFound>();

        foreach (var allergenStr in userAllergens)
        {
            if (!Enum.TryParse<AllergenType>(allergenStr.Replace(" ", ""), true, out var allergenType))
                continue;

            var info = AllergenInfo.GetValueOrDefault(allergenType,
                ("Reaction possible", "Consult doctor", "Seek medical advice", AllergenRisk.Low));

            // Direct match
            string? directMatch = ingredients.FirstOrDefault(ing =>
                ing.Contains(allergenStr.ToLowerInvariant()) ||
                ing.Contains(allergenType.ToString().ToLowerInvariant()));

            // Hidden source match
            string? hiddenSource = null;
            if (HiddenSources.TryGetValue(allergenType, out var sources))
                hiddenSource = ingredients.FirstOrDefault(ing =>
                    sources.Any(s => ing.Contains(s.ToLowerInvariant())));

            if (directMatch != null || hiddenSource != null)
            {
                found.Add(new AllergenFound(
                    Type:            allergenType,
                    IngredientName:  directMatch ?? hiddenSource ?? allergenStr,
                    Risk:            info.Risk,
                    HiddenSource:    hiddenSource,
                    SafeSubstitute:  GetSafeSubstitute(allergenType),
                    Symptoms:        $"Mild: {info.Mild} | Severe: {info.Severe}",
                    FirstResponse:   info.First));
            }
        }

        var advice = found.Any()
            ? $"This recipe contains {found.Count} allergen(s). Consider using safe substitutes or choosing an alternative recipe."
            : null;

        return new AllergyCheckResult(found.Any(), found, advice);
    }

    public async Task<AllergyGuide?> GetGuideAsync(AllergenType type)
        => await _db.AllergyGuides.FirstOrDefaultAsync(g => g.AllergenType == type);

    private static string? GetSafeSubstitute(AllergenType type) => type switch
    {
        AllergenType.Milk     => "Use oat milk, almond milk, or coconut cream",
        AllergenType.Eggs     => "Use flax egg (1 tbsp flax + 3 tbsp water) or chia egg",
        AllergenType.Gluten   => "Use gluten-free flour blend, tamari instead of soy sauce",
        AllergenType.Peanuts  => "Use sunflower seed butter or toasted pumpkin seeds",
        AllergenType.Soy      => "Use coconut aminos instead of soy sauce",
        AllergenType.Sesame   => "Use hemp seeds or sunflower seeds",
        AllergenType.Fish     => "Use capers or seaweed for umami flavour",
        _ => null
    };
}

namespace MyMealPlanner.Services.Social;

/// <summary>
/// Manages cooking jokes — scraped daily from multiple sources
/// and augmented with AI-generated content.
/// </summary>
public class JokeService : IJokeService
{
    private readonly ApplicationDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<JokeService> _logger;

    private static readonly string[] ScrapeSources =
    [
        "https://www.punpedia.com/food-puns/",
        "https://jokes4us.com/foodjokes/",
        "https://bestlifeonline.com/food-jokes/",
    ];

    public JokeService(ApplicationDbContext db, IHttpClientFactory httpFactory,
        ILogger<JokeService> logger)
    {
        _db     = db;
        _http   = httpFactory.CreateClient("ScraperClient");
        _logger = logger;
    }

    public async Task<CookingJoke?> GetDailyJokeAsync()
    {
        // Return today's featured joke (highest liked, added today or recently)
        return await _db.CookingJokes
            .Where(j => j.IsApproved)
            .OrderByDescending(j => j.LikeCount)
            .ThenByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<CookingJoke?> GetRandomJokeAsync()
        => await _db.CookingJokes
            .Where(j => j.IsApproved)
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync();

    public async Task<List<CookingJoke>> GetByCategoryAsync(string category, int count = 10)
        => await _db.CookingJokes
            .Where(j => j.IsApproved && j.Category == category)
            .OrderByDescending(j => j.LikeCount)
            .Take(count)
            .ToListAsync();

    public async Task ScrapeNewJokesAsync()
    {
        foreach (var url in ScrapeSources)
        {
            try
            {
                var html = await _http.GetStringAsync(url);
                var doc  = new HtmlDocument();
                doc.LoadHtml(html);

                // Generic: look for paragraphs containing "Q:" or joke patterns
                var nodes = doc.DocumentNode.SelectNodes(
                    "//p[contains(.,'Q:') or contains(.,'Why') or contains(.,'What')]");

                if (nodes is null) continue;

                int added = 0;
                foreach (var node in nodes.Take(20))
                {
                    var text = System.Text.RegularExpressions.Regex.Replace(
                        node.InnerText.Trim(), @"\s+", " ");

                    if (text.Length < 20 || text.Length > 500) continue;

                    var exists = await _db.CookingJokes.AnyAsync(j => j.Body == text);
                    if (exists) continue;

                    _db.CookingJokes.Add(new CookingJoke
                    {
                        Body       = text,
                        Category   = "Food Puns",
                        Source     = "Web",
                        SourceUrl  = url,
                        IsApproved = true,
                        CreatedAt  = DateTime.UtcNow
                    });
                    added++;
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("[Jokes] {Url} → {Count} new jokes", url, added);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Jokes] Scrape failed: {Url}", url);
            }
        }
    }

    public async Task GenerateAIJokesAsync(int count = 5)
    {
        // In production: call Ollama/Groq API to generate jokes
        // Using seeded defaults for now
        var defaults = new[]
        {
            ("Why did the chef get fired? Because he kept making the customer cry — he only cooked onions. 😂", "Chef Jokes"),
            ("What do you call a fake noodle? An impasta! 🍝", "Food Puns"),
            ("Why did the banana go to the doctor? Because it wasn't peeling well. 🍌", "Fruit Jokes"),
            ("What's a chef's favourite type of music? Heavy metal — because they love beating eggs! 🥚", "Chef Jokes"),
            ("Why don't scientists trust atoms? Because they make up everything — just like my recipe claims. ⚗️", "Kitchen Jokes"),
        };

        foreach (var (body, cat) in defaults)
        {
            var exists = await _db.CookingJokes.AnyAsync(j => j.Body == body);
            if (!exists)
            {
                _db.CookingJokes.Add(new CookingJoke
                {
                    Body       = body,
                    Category   = cat,
                    Source     = "AI",
                    IsApproved = true,
                    CreatedAt  = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("[Jokes] AI jokes seeded");
    }
}
