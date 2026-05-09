using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.DTOs;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Services.AI;

/// <summary>
/// "Mia" — My Meal Planner's AI cooking assistant.
/// Routes to the cheapest available free/low-cost API:
///   1. Ollama (local LLM, fully free if self-hosted)
///   2. Groq API (free tier, very fast — Llama 3)
///   3. OpenRouter (pay-per-token fallback)
///   4. Claude API (most capable, pay-per-token)
///
/// The system prompt is food-aware and pulls in the user's profile,
/// allergens, dietary restrictions, and country for hyper-personalised responses.
/// </summary>
public class AIChatAssistantService : IAIChatAssistantService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly ILogger<AIChatAssistantService> _logger;

    public AIChatAssistantService(
        ApplicationDbContext db,
        IConfiguration config,
        IHttpClientFactory httpFactory,
        ILogger<AIChatAssistantService> logger)
    {
        _db     = db;
        _config = config;
        _http   = httpFactory.CreateClient("AIClient");
        _logger = logger;
    }

    public async Task<string> ChatAsync(
        string userId,
        string message,
        List<ChatTurn> history)
    {
        var user = await _db.Users.FindAsync(userId);
        var systemPrompt = BuildSystemPrompt(user);

        // Try providers in cost order
        return await TryGroqAsync(systemPrompt, message, history)
            ?? await TryOllamaAsync(systemPrompt, message, history)
            ?? await TryOpenRouterAsync(systemPrompt, message, history)
            ?? await TryClaudeAsync(systemPrompt, message, history)
            ?? FallbackResponse(message);
    }

    // ── System Prompt Builder ─────────────────────────────────
    private string BuildSystemPrompt(ApplicationUser? user)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are Mia, the friendly AI cooking assistant for My Meal Planner.");
        sb.AppendLine("You specialise in global cuisines, nutrition, recipes, and meal planning.");
        sb.AppendLine("Keep responses concise, warm, and actionable. Use food emojis sparingly.");
        sb.AppendLine("Always suggest recipes from the platform when possible.");
        sb.AppendLine("If you detect a food allergy question, be extra careful and advise consulting a doctor.");
        sb.AppendLine();

        if (user != null)
        {
            sb.AppendLine($"User profile:");
            sb.AppendLine($"- Name: {user.FullName}");
            sb.AppendLine($"- Country: {user.CountryName ?? user.CountryCode}");
            sb.AppendLine($"- Cooks for: {user.NumberOfPeopleICookFor} people");

            if (!string.IsNullOrEmpty(user.AllergiesJson))
                sb.AppendLine($"- KNOWN ALLERGIES (critical): {user.AllergiesJson}");

            if (!string.IsNullOrEmpty(user.DietaryRestrictionsJson))
                sb.AppendLine($"- Dietary restrictions: {user.DietaryRestrictionsJson}");

            sb.AppendLine($"- Health goal: {user.HealthGoal}");
            sb.AppendLine($"- Skill level: {user.ChefLevel}");
        }

        sb.AppendLine();
        sb.AppendLine("Platform context: My Meal Planner has recipes from 195+ countries, cultural food stories,");
        sb.AppendLine("nutritional information, allergy guides, meal planning tools, and a chef levelling system.");

        return sb.ToString();
    }

    // ── Groq API (Free tier — Llama 3, very fast) ────────────
    private async Task<string?> TryGroqAsync(
        string systemPrompt, string message, List<ChatTurn> history)
    {
        var apiKey = _config["AI:GroqApiKey"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var messages = BuildMessages(systemPrompt, message, history);
            var body = JsonSerializer.Serialize(new
            {
                model    = "llama3-8b-8192",
                messages,
                max_tokens      = 600,
                temperature     = 0.7
            });

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var resp = await _http.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                       .GetProperty("choices")[0]
                       .GetProperty("message")
                       .GetProperty("content")
                       .GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Mia] Groq failed");
            return null;
        }
    }

    // ── Ollama (Local LLM — completely free, self-hosted) ─────
    private async Task<string?> TryOllamaAsync(
        string systemPrompt, string message, List<ChatTurn> history)
    {
        var baseUrl = _config["AI:OllamaBaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return null;

        try
        {
            var prompt = $"{systemPrompt}\n\nUser: {message}\nMia:";
            var body   = JsonSerializer.Serialize(new
            {
                model  = _config["AI:OllamaModel"] ?? "llama3",
                prompt,
                stream = false
            });

            var resp = await _http.PostAsync(
                $"{baseUrl.TrimEnd('/')}/api/generate",
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("response").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Mia] Ollama failed");
            return null;
        }
    }

    // ── OpenRouter (cheap multi-model gateway) ────────────────
    private async Task<string?> TryOpenRouterAsync(
        string systemPrompt, string message, List<ChatTurn> history)
    {
        var apiKey = _config["AI:OpenRouterApiKey"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var messages = BuildMessages(systemPrompt, message, history);
            var body     = JsonSerializer.Serialize(new
            {
                model    = "mistralai/mistral-7b-instruct:free",
                messages,
                max_tokens = 600
            });

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _http.DefaultRequestHeaders.Add("HTTP-Referer", "https://mymealplanner.app");

            var resp = await _http.PostAsync(
                "https://openrouter.ai/api/v1/chat/completions",
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                       .GetProperty("choices")[0]
                       .GetProperty("message")
                       .GetProperty("content")
                       .GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Mia] OpenRouter failed");
            return null;
        }
    }

    // ── Claude API (Anthropic — most capable) ────────────────
    private async Task<string?> TryClaudeAsync(
        string systemPrompt, string message, List<ChatTurn> history)
    {
        var apiKey = _config["AI:ClaudeApiKey"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var messages = history.Select(h => new { role = h.Role, content = h.Content })
                                  .Append(new { role = "user", content = message })
                                  .ToList();

            var body = JsonSerializer.Serialize(new
            {
                model      = "claude-haiku-4-5-20251001",
                max_tokens = 600,
                system     = systemPrompt,
                messages
            });

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var resp = await _http.PostAsync(
                "https://api.anthropic.com/v1/messages",
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                       .GetProperty("content")[0]
                       .GetProperty("text")
                       .GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Mia] Claude API failed");
            return null;
        }
    }

    // ── Fallback ──────────────────────────────────────────────
    private static string FallbackResponse(string message)
    {
        var lower = message.ToLowerInvariant();

        if (lower.Contains("ingredient") || lower.Contains("substitute"))
            return "Great question! Try searching in our Recipe section with those ingredients — our search understands what you have and suggests dishes. 🍽️";

        if (lower.Contains("allergy") || lower.Contains("allergic"))
            return "For allergy safety, always check our Allergy Guide under the Health section. When in doubt, consult your doctor. 🛡️";

        if (lower.Contains("calorie") || lower.Contains("diet") || lower.Contains("healthy"))
            return "Head to our Health Hub → Nutrient Navigator to explore foods by nutrient category and find recipes that match your health goals! 💚";

        return "I'm Mia, your cooking assistant! Ask me about recipes, ingredients, nutrition, or how to cook a specific dish. I'm here to help! 🍳";
    }

    // ── Helper ────────────────────────────────────────────────
    private static List<object> BuildMessages(
        string systemPrompt, string message, List<ChatTurn> history)
    {
        var msgs = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };
        msgs.AddRange(history.Select(h => (object)new { role = h.Role, content = h.Content }));
        msgs.Add(new { role = "user", content = message });
        return msgs;
    }
}

/// <summary>
/// Auto-tags scraped recipes with country, continent, dietary flags, allergens,
/// difficulty, and a quality score — all from the recipe text alone.
/// Uses rule-based NLP (zero API cost) with optional AI enrichment.
/// </summary>
public class AITaggerService : IAITaggerService
{
    private readonly ILogger<AITaggerService> _logger;

    private static readonly Dictionary<string, string> CountryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jollof"] = "Nigeria", ["suya"] = "Nigeria", ["egusi"] = "Nigeria", ["fufu"] = "Nigeria",
        ["jerk"] = "Jamaica", ["akara"] = "Nigeria",
        ["sushi"] = "Japan", ["ramen"] = "Japan", ["miso"] = "Japan", ["teriyaki"] = "Japan",
        ["pasta"] = "Italy", ["pizza"] = "Italy", ["risotto"] = "Italy", ["tiramisu"] = "Italy",
        ["curry"] = "India", ["biryani"] = "India", ["dhal"] = "India", ["samosa"] = "India",
        ["tacos"] = "Mexico", ["enchilada"] = "Mexico", ["guacamole"] = "Mexico",
        ["couscous"] = "Morocco", ["tagine"] = "Morocco", ["harissa"] = "Morocco",
        ["kimchi"] = "Korea", ["bibimbap"] = "Korea", ["bulgogi"] = "Korea",
        ["pad thai"] = "Thailand", ["tom yum"] = "Thailand", ["green curry"] = "Thailand",
        ["bouillabaisse"] = "France", ["ratatouille"] = "France", ["crepe"] = "France",
        ["shakshuka"] = "Israel", ["falafel"] = "Lebanon", ["hummus"] = "Lebanon",
        ["injera"] = "Ethiopia", ["doro wat"] = "Ethiopia",
        ["bobotie"] = "South Africa", ["bunny chow"] = "South Africa",
        ["ceviche"] = "Peru", ["empanada"] = "Argentina", ["feijoada"] = "Brazil",
        ["pierogi"] = "Poland", ["goulash"] = "Hungary", ["stroganoff"] = "Russia",
        ["pho"] = "Vietnam", ["banh mi"] = "Vietnam", ["rendang"] = "Indonesia",
        ["laksa"] = "Malaysia", ["nasi lemak"] = "Malaysia",
    };

    private static readonly Dictionary<string, string> CountryToContinent = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Nigeria"] = "Africa", ["Ghana"] = "Africa", ["Kenya"] = "Africa",
        ["Ethiopia"] = "Africa", ["Morocco"] = "Africa", ["South Africa"] = "Africa",
        ["Japan"] = "Asia", ["China"] = "Asia", ["India"] = "Asia",
        ["Thailand"] = "Asia", ["Vietnam"] = "Asia", ["Indonesia"] = "Asia", ["Malaysia"] = "Asia",
        ["Korea"] = "Asia",
        ["Italy"] = "Europe", ["France"] = "Europe", ["Spain"] = "Europe",
        ["Germany"] = "Europe", ["Poland"] = "Europe", ["Hungary"] = "Europe",
        ["Mexico"] = "Americas", ["Brazil"] = "Americas", ["Peru"] = "Americas",
        ["Argentina"] = "Americas", ["Jamaica"] = "Americas",
        ["Lebanon"] = "Middle East", ["Israel"] = "Middle East",
        ["Australia"] = "Oceania", ["New Zealand"] = "Oceania",
    };

    private static readonly string[] VeganKeywords   = ["vegan", "plant-based", "no meat", "no dairy", "no animal"];
    private static readonly string[] VegetarianKw    = ["vegetarian", "no meat", "meatless", "veggie"];
    private static readonly string[] HalalKw         = ["halal", "no pork", "no alcohol", "islamically"];
    private static readonly string[] KosherKw        = ["kosher", "no pork", "jewish"];
    private static readonly string[] GlutenFreeKw    = ["gluten-free", "gluten free", "no gluten", "rice flour", "almond flour"];
    private static readonly string[] DairyFreeKw     = ["dairy-free", "dairy free", "no dairy", "lactose-free"];

    private static readonly string[] PeanutKw        = ["peanut", "groundnut"];
    private static readonly string[] GlutenAllergen  = ["wheat", "flour", "bread", "pasta", "soy sauce", "barley"];
    private static readonly string[] MilkKw          = ["milk", "cream", "butter", "cheese", "yoghurt", "yogurt"];
    private static readonly string[] EggsKw          = ["egg", "eggs"];
    private static readonly string[] ShellfishKw     = ["shrimp", "prawn", "lobster", "crab", "clam", "oyster", "mussel"];
    private static readonly string[] NutsKw          = ["almond", "walnut", "cashew", "hazelnut", "pecan", "pistachio"];
    private static readonly string[] SoyKw           = ["soy", "tofu", "edamame", "miso", "tempeh"];
    private static readonly string[] SesameKw        = ["sesame", "tahini"];

    public AITaggerService(ILogger<AITaggerService> logger) => _logger = logger;

    public async Task<RecipeTags> AutoTagAsync(Recipe recipe)
    {
        var text = $"{recipe.Title} {recipe.Description} {recipe.CulturalStory}"
                   .ToLowerInvariant();

        var ingredientText = string.Join(" ",
            recipe.Ingredients.Select(i => i.Name)).ToLowerInvariant();

        var combined = $"{text} {ingredientText}";

        // Country detection
        string? country = null;
        foreach (var (keyword, c) in CountryKeywords)
            if (combined.Contains(keyword)) { country = c; break; }

        string? continent = country != null &&
            CountryToContinent.TryGetValue(country, out var cont) ? cont : null;

        // Cultural tag
        string? cultureTag = country != null ? $"{country} Cuisine" : null;

        // Dietary flags
        var dietFlags = new List<string>();
        if (VeganKeywords.Any(k => combined.Contains(k)))       dietFlags.Add("Vegan");
        if (VegetarianKw.Any(k => combined.Contains(k)))        dietFlags.Add("Vegetarian");
        if (HalalKw.Any(k => combined.Contains(k)))             dietFlags.Add("Halal");
        if (KosherKw.Any(k => combined.Contains(k)))            dietFlags.Add("Kosher");
        if (GlutenFreeKw.Any(k => combined.Contains(k)))        dietFlags.Add("Gluten-Free");
        if (DairyFreeKw.Any(k => combined.Contains(k)))         dietFlags.Add("Dairy-Free");

        // Allergen flags
        var allergenFlags = new List<string>();
        if (PeanutKw.Any(k => ingredientText.Contains(k)))       allergenFlags.Add("Peanuts");
        if (GlutenAllergen.Any(k => ingredientText.Contains(k))) allergenFlags.Add("Gluten");
        if (MilkKw.Any(k => ingredientText.Contains(k)))         allergenFlags.Add("Milk");
        if (EggsKw.Any(k => ingredientText.Contains(k)))         allergenFlags.Add("Eggs");
        if (ShellfishKw.Any(k => ingredientText.Contains(k)))    allergenFlags.Add("Shellfish");
        if (NutsKw.Any(k => ingredientText.Contains(k)))         allergenFlags.Add("TreeNuts");
        if (SoyKw.Any(k => ingredientText.Contains(k)))          allergenFlags.Add("Soy");
        if (SesameKw.Any(k => ingredientText.Contains(k)))       allergenFlags.Add("Sesame");

        // Nutrient flags (simple)
        var nutrientFlags = new List<string>();
        if (new[] { "spinach","kale","broccoli","carrot" }.Any(k => ingredientText.Contains(k)))
            nutrientFlags.Add("VitaminA");
        if (new[] { "lemon","orange","pepper","tomato" }.Any(k => ingredientText.Contains(k)))
            nutrientFlags.Add("VitaminC");
        if (new[] { "salmon","sardine","mackerel","tuna","walnut","flaxseed" }.Any(k => ingredientText.Contains(k)))
            nutrientFlags.Add("Omega3");
        if (new[] { "chicken","beef","lentil","bean","egg","tofu" }.Any(k => ingredientText.Contains(k)))
            nutrientFlags.Add("Protein");
        if (new[] { "oat","broccoli","apple","bean","lentil" }.Any(k => ingredientText.Contains(k)))
            nutrientFlags.Add("Fibre");

        // Difficulty
        var diff = recipe.Steps.Count switch
        {
            <= 3  => Core.Enums.DifficultyLevel.Beginner,
            <= 5  => Core.Enums.DifficultyLevel.Easy,
            <= 8  => Core.Enums.DifficultyLevel.Intermediate,
            <= 12 => Core.Enums.DifficultyLevel.Advanced,
            _     => Core.Enums.DifficultyLevel.Professional
        };

        // Season
        var season = combined.Contains("summer") ? Core.Enums.Season.Summer
                   : combined.Contains("winter") ? Core.Enums.Season.Winter
                   : combined.Contains("spring") ? Core.Enums.Season.Spring
                   : combined.Contains("autumn") || combined.Contains("fall") ? Core.Enums.Season.Autumn
                   : Core.Enums.Season.AllYear;

        // Quality score — simple heuristic
        double quality = 0;
        if (!string.IsNullOrEmpty(recipe.Description) && recipe.Description.Length > 50)  quality += 20;
        if (recipe.Ingredients.Count >= 3)   quality += 20;
        if (recipe.Steps.Count >= 2)         quality += 20;
        if (!string.IsNullOrEmpty(recipe.CoverImageUrl))  quality += 20;
        if (country != null)                 quality += 10;
        if (!string.IsNullOrEmpty(recipe.CulturalStory)) quality += 10;

        return new RecipeTags(
            DetectedCountry:  country,
            DetectedContinent: continent,
            CultureTag:       cultureTag,
            DietaryFlags:     dietFlags,
            AllergenFlags:    allergenFlags,
            NutrientFlags:    nutrientFlags,
            Difficulty:       diff,
            Season:           season,
            QualityScore:     quality);
    }

    public async Task<string?> DetectLanguageAsync(string text)
    {
        // Simple heuristic — check character sets
        if (text.Any(c => c >= '\u4e00' && c <= '\u9fff')) return "zh";
        if (text.Any(c => c >= '\u0600' && c <= '\u06ff')) return "ar";
        if (text.Any(c => c >= '\u0900' && c <= '\u097f')) return "hi";
        if (text.Any(c => c >= '\u3040' && c <= '\u309f')) return "ja";
        if (text.Any(c => c >= '\uac00' && c <= '\ud7a3')) return "ko";

        // Latin script language detection (simplified)
        var lower = text.ToLowerInvariant();
        if (lower.Contains("le ") || lower.Contains("les ") || lower.Contains("une ")) return "fr";
        if (lower.Contains(" el ") || lower.Contains(" los ") || lower.Contains(" una ")) return "es";
        if (lower.Contains(" die ") || lower.Contains(" der ") || lower.Contains(" das ")) return "de";
        if (lower.Contains(" il ") || lower.Contains(" gli ") || lower.Contains(" una ")) return "it";

        return "en";
    }

    public async Task<double> ScoreQualityAsync(Core.Models.RecipeSuggestion suggestion)
    {
        double score = 0;
        if (!string.IsNullOrEmpty(suggestion.Title) && suggestion.Title.Length > 5)       score += 20;
        if (!string.IsNullOrEmpty(suggestion.Description) && suggestion.Description.Length > 30) score += 20;
        if (!string.IsNullOrEmpty(suggestion.IngredientsJson) && suggestion.IngredientsJson != "[]") score += 25;
        if (!string.IsNullOrEmpty(suggestion.StepsJson) && suggestion.StepsJson != "[]")  score += 25;
        if (!string.IsNullOrEmpty(suggestion.CoverImageUrl))   score += 5;
        if (!string.IsNullOrEmpty(suggestion.YouTubeVideoId))  score += 5;
        return score;
    }
}
