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

        // Try providers in cost/capability order
        return await TryDeepSeekAsync(systemPrompt, message, history)
            ?? await TryQwenAsync(systemPrompt, message, history)
            ?? await TryGroqAsync(systemPrompt, message, history)
            ?? await TryOllamaAsync(systemPrompt, message, history)
            ?? await TryOpenRouterAsync(systemPrompt, message, history)
            ?? await TryClaudeAsync(systemPrompt, message, history)
            ?? FallbackResponse(message);
    }

    // ── DeepSeek-V3 (High-performance Chinese AI) ─────────────
    private async Task<string?> TryDeepSeekAsync(
        string systemPrompt, string message, List<ChatTurn> history)
    {
        var apiKey = _config["AI:DeepSeekApiKey"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var messages = BuildMessages(systemPrompt, message, history);
            var body = JsonSerializer.Serialize(new
            {
                model    = "deepseek-chat",
                messages,
                max_tokens = 800,
                temperature = 0.7
            });

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var resp = await _http.PostAsync(
                "https://api.deepseek.com/v1/chat/completions",
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Mia] DeepSeek failed");
            return null;
        }
    }

    // ── Alibaba Qwen (Multi-lingual & Culinary knowledge) ─────
    private async Task<string?> TryQwenAsync(
        string systemPrompt, string message, List<ChatTurn> history)
    {
        var apiKey = _config["AI:DashScopeApiKey"]; // Alibaba's API gateway
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var messages = BuildMessages(systemPrompt, message, history);
            var body = JsonSerializer.Serialize(new
            {
                model    = "qwen-max",
                input = new { messages },
                parameters = new { result_format = "message" }
            });

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var resp = await _http.PostAsync(
                "https://dashscope.aliyuncs.com/api/v1/services/aigc/text-generation/generation",
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("output").GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Mia] Qwen failed");
            return null;
        }
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

    // ── Intelligent Rule-Based Fallback ───────────────────────
    private static string FallbackResponse(string message)
    {
        var lower = message.ToLowerInvariant();

        // ── Greetings ────────────────────────────────────────────
        if (lower.Contains("hello") || lower.Contains("hi mia") || lower.Contains("hey") || lower == "hi")
            return "👋 Hello! I'm Mia, your personal cooking assistant. I can help you with:\n\n🍳 **Recipes** from 195+ countries\n🥗 **Nutrition** & healthy eating advice\n🧾 **Ingredient substitutions** when you're missing something\n⏱️ **Cooking techniques** and tips\n🌍 **World cuisines** explained\n\nWhat can I help you cook today?";

        // ── Recipe requests ──────────────────────────────────────
        if (lower.Contains("jollof") || lower.Contains("nigerian rice"))
            return "🍚 **Nigerian Jollof Rice** is a West African classic!\n\n**Key ingredients:** Long-grain rice, tomatoes, red bell pepper, scotch bonnet, onion, chicken stock, bay leaves, seasoning cube.\n\n**Quick method:**\n1. Blend tomatoes, peppers & onion → fry the paste in oil for 15 mins until darkened.\n2. Add stock, seasoning, bay leaves → pour in washed rice.\n3. Cover tightly, cook on low heat 30–40 mins until rice absorbs all the liquid.\n\n💡 *The secret to smoky Jollof? A few minutes of high heat at the end — the 'party rice' effect!*\n\nFind the full recipe in our Recipes section! 🌍";

        if (lower.Contains("pasta") || lower.Contains("spaghetti") || lower.Contains("carbonara") || lower.Contains("bolognese"))
            return "🍝 **Classic Italian Pasta Tips:**\n\n**Carbonara (authentic):** Eggs + Pecorino Romano + guanciale + black pepper. **No cream!** The heat of the pasta cooks the eggs.\n\n**Bolognese:** Slow-cook minced beef + pork with soffritto (onion, celery, carrot), wine, and a tiny bit of milk for 2+ hours.\n\n**Golden rule:** Always salt your pasta water until it 'tastes like the sea' 🌊 and reserve a cup before draining — the starchy water is liquid gold for the sauce.\n\nSearch 'pasta' in our recipe library for 50+ variations! 🇮🇹";

        if (lower.Contains("sushi") || lower.Contains("ramen") || lower.Contains("japanese") || lower.Contains("miso"))
            return "🍣 **Japanese Cuisine Guide:**\n\n**Sushi rice:** Short-grain rice + rice vinegar + sugar + salt. Fan while mixing for a glossy finish.\n\n**Ramen broth types:**\n- *Shoyu* (soy) — clear, light\n- *Miso* — rich, slightly sweet\n- *Tonkotsu* — milky, pork-bone based (simmer 12+ hrs)\n- *Shio* (salt) — delicate, seafood-based\n\n**Miso soup:** Dashi (kombu + bonito flakes) + miso paste. Never boil the miso — it kills the probiotics!\n\nExplore our Japanese recipe collection in the Recipes section! 🇯🇵";

        if (lower.Contains("curry") || lower.Contains("indian") || lower.Contains("biryani") || lower.Contains("masala"))
            return "🍛 **Indian Cooking Essentials:**\n\n**Curry base (makhani / tikka):** Fry onions golden → add ginger-garlic paste → tomatoes → spices (cumin, coriander, turmeric, garam masala, chilli). Cook until oil separates.\n\n**Biryani tip:** Par-cook rice to 70% → layer with marinated meat → seal and dum (steam) cook for 20–25 mins. The 'dum' is what makes biryani magical!\n\n**Spice toasting:** Always bloom whole spices in oil first (mustard seeds, cumin, curry leaves) before adding other ingredients.\n\nBrowse our Indian cuisine collection — 40+ recipes! 🇮🇳";

        if (lower.Contains("tacos") || lower.Contains("mexican") || lower.Contains("enchilada") || lower.Contains("guacamole"))
            return "🌮 **Mexican Kitchen Secrets:**\n\n**Street Tacos:** Corn tortillas (always!), grilled meat, diced white onion, cilantro, lime, and salsa. Simple = authentic.\n\n**Perfect Guacamole:** 3 ripe avocados + lime juice (prevents browning) + red onion + cilantro + jalapeño + salt. Mash chunky, not smooth!\n\n**Enchilada sauce from scratch:** Toast dried chiles (ancho, guajillo) → rehydrate → blend with garlic, cumin, chicken stock.\n\n💡 *Keep the avocado pit in your guac to keep it green longer!*\n\nFind 30+ Mexican recipes in our collection! 🇲🇽";

        if (lower.Contains("thai") || lower.Contains("pad thai") || lower.Contains("tom yum") || lower.Contains("green curry"))
            return "🍜 **Thai Cooking Basics:**\n\n**Pad Thai:** Rice noodles + tofu/shrimp + eggs + bean sprouts. Sauce = tamarind paste + fish sauce + palm sugar. High heat, quick toss!\n\n**Tom Yum:** Lemongrass + galangal + kaffir lime leaves in broth → add mushrooms + protein → fish sauce + lime + chilli. Fresh herbs at the end only.\n\n**Green Curry paste ingredients:** Green chillies, lemongrass, galangal, kaffir lime zest, coriander root, shrimp paste. Homemade is 10x better than jarred!\n\n💡 *The secret to Thai food: balance of sour, sweet, salty, and spicy in every dish.* 🇹🇭";

        if (lower.Contains("moroccan") || lower.Contains("tagine") || lower.Contains("couscous"))
            return "🥘 **Moroccan Cuisine:**\n\n**Tagine:** A slow-cooked stew in a conical clay pot. Key spices: ras el hanout, cumin, cinnamon, ginger, saffron, turmeric.\n\n**Chicken Tagine method:** Brown chicken → add onion, garlic, spices → preserved lemon + olives → low heat 1–1.5 hrs until tender.\n\n**Couscous:** Pour boiling salted stock over couscous, cover 5 mins, fluff with a fork + drizzle olive oil.\n\n💡 *Preserved lemons are the soul of Moroccan cooking — they add a unique fermented citrus depth!* 🇲🇦";

        if (lower.Contains("korean") || lower.Contains("kimchi") || lower.Contains("bibimbap") || lower.Contains("bulgogi"))
            return "🇰🇷 **Korean Cooking Guide:**\n\n**Kimchi:** Napa cabbage + gochugaru (Korean chilli flakes) + garlic + ginger + fish sauce + salted shrimp. Ferment 1–5 days at room temp, then refrigerate.\n\n**Bibimbap:** Rice topped with assorted seasoned vegetables + meat + fried egg + gochujang (chilli paste). Mix everything before eating!\n\n**Bulgogi marinade:** Thinly sliced beef + soy sauce + pear juice (tenderiser) + sesame oil + garlic + sugar. Marinate 30 mins minimum.\n\n💡 *Gochujang is the backbone of Korean cooking — spicy, sweet, and fermented!*";

        // ── Specific techniques ──────────────────────────────────
        if (lower.Contains("how to boil") || lower.Contains("boil egg") || lower.Contains("boiled egg"))
            return "🥚 **Perfect Boiled Eggs Guide:**\n\n| Result | Cold water method | Boiling water method |\n|--------|------------------|---------------------|\n| Soft (runny yolk) | 6 mins | 5½ mins |\n| Medium (jammy yolk) | 8 mins | 7 mins |\n| Hard (fully set) | 11 mins | 10 mins |\n\n**Steps:** Place eggs in cold water (cold water method) or gently lower into boiling water. Set timer. Ice bath immediately after for easy peeling!\n\n💡 *Fresh eggs are harder to peel. Use eggs that are 1–2 weeks old for best results.*";

        if (lower.Contains("how to fry") || lower.Contains("deep fry") || lower.Contains("frying"))
            return "🍳 **Frying Masterclass:**\n\n**Shallow frying:** Oil depth ½–1cm. Best for: fish, chicken cutlets, vegetables.\n\n**Deep frying:** Oil temp = 175–180°C (350°F). Use a thermometer! Too cool = soggy; too hot = burnt outside, raw inside.\n\n**Best oils for frying:** Sunflower, vegetable, peanut (high smoke points).\n\n**Crispiness secrets:**\n- Pat food completely dry before frying\n- Don't overcrowd the pan\n- Drain on a wire rack, not paper towel\n\n💡 *Test oil readiness: drop a small bread cube in — it should sizzle and turn golden in 60 seconds.*";

        if (lower.Contains("how to bake") || lower.Contains("baking") || lower.Contains("bread") || lower.Contains("cake"))
            return "🎂 **Baking Fundamentals:**\n\n**The golden rules:**\n- Measure everything precisely (baking is chemistry!)\n- All ingredients at room temperature\n- Don't overmix — develops gluten = tough baked goods\n- Preheat oven fully before baking\n\n**Why did my cake sink?** Underbaked, oven too hot, or opened oven door too early.\n\n**Bread basics:** Yeast needs warmth (37°C) and sugar to activate. Knead until smooth and elastic. First rise until doubled (1–2 hrs). Second rise after shaping (30–45 mins). Bake at high heat for crust.\n\n💡 *Test cake doneness: insert a toothpick — it should come out clean or with a few dry crumbs.*";

        // ── Substitutions ────────────────────────────────────────
        if (lower.Contains("substitute") || lower.Contains("replacement") || lower.Contains("instead of") || lower.Contains("don't have"))
            return "🔄 **Common Ingredient Substitutions:**\n\n| Missing | Substitute |\n|---------|-----------|\n| Buttermilk | Milk + 1 tbsp lemon juice (rest 5 mins) |\n| Eggs (baking) | Flax egg (1 tbsp flax + 3 tbsp water) or applesauce |\n| Butter | Same amount of coconut oil or 80% amount of vegetable oil |\n| Sour cream | Greek yoghurt 1:1 |\n| Cream | Coconut cream (dairy-free) |\n| Fresh herbs | ⅓ the amount of dried herbs |\n| Wine (cooking) | Stock + 1 tsp vinegar |\n| Breadcrumbs | Crushed crackers, oats, or ground almonds |\n\nTell me which ingredient you're missing and I'll give a specific recommendation! 🍽️";

        // ── Nutrition ────────────────────────────────────────────
        if (lower.Contains("protein") || lower.Contains("high protein"))
            return "💪 **Best High-Protein Foods:**\n\n🥇 **Animal sources:** Chicken breast (31g/100g), Tuna (29g/100g), Eggs (13g), Greek yoghurt (10g)\n\n🌱 **Plant sources:** Spirulina (57g!), Edamame (11g), Lentils (9g), Black beans (8.9g), Tofu (8g), Quinoa (4g — complete protein!)\n\n💡 *Aim for 0.8g of protein per kg of bodyweight per day (1.2–2g if active or building muscle).*\n\nVisit our **Nutrient Navigator** in the Health section to see protein-rich recipe recommendations! 🏃";

        if (lower.Contains("vitamin c") || lower.Contains("immune") || lower.Contains("immunity"))
            return "🍊 **Vitamin C Powerhouses:**\n\n1. Kakadu Plum (Australia) — 5,300mg/100g!\n2. Acerola Cherry — 1,677mg/100g\n3. Baobab Fruit (Africa) — 280mg/100g\n4. Guava — 228mg/100g\n5. Bell Pepper — 183mg/100g\n6. Kiwi — 93mg/100g\n7. Orange — 53mg/100g\n\n💡 *An orange has just 53mg of Vitamin C — a bell pepper has 3x more!*\n\n**Immune system boosters beyond Vitamin C:** Zinc (pumpkin seeds), Vitamin D (salmon, eggs), Garlic (allicin), Ginger (gingerol).";

        if (lower.Contains("calorie") || lower.Contains("weight loss") || lower.Contains("low calorie"))
            return "⚖️ **Smart Calorie Management:**\n\n**Low-calorie but filling foods:**\n- Eggs (155 kcal/100g, very filling)\n- Oats (68 kcal cooked, high fibre)\n- Greek yoghurt (59 kcal/100g)\n- Broccoli (34 kcal/100g!)\n- Chicken breast (165 kcal/100g, high protein)\n\n**The 80/20 rule:** Eat whole, unprocessed foods 80% of the time and don't stress the rest.\n\n💡 *Protein and fibre keep you full longest. Always build your meals around them.*\n\nCheck our **Health Hub → Obesity Fighter** for low-calorie recipe ideas! 💚";

        if (lower.Contains("vegan") || lower.Contains("plant based") || lower.Contains("plant-based"))
            return "🌱 **Vegan Cooking Essentials:**\n\n**Protein sources:** Lentils, chickpeas, tofu, tempeh, edamame, seitan, quinoa.\n\n**Replacing dairy:**\n- Milk → Oat milk (best for coffee/baking), almond, soy\n- Butter → Coconut oil, vegan butter\n- Cheese → Nutritional yeast for cheesy flavour, cashew cream cheese\n\n**Replacing eggs:**\n- Binding: Flax egg, chia egg, mashed banana\n- Scrambled: Crumbled tofu + turmeric + black salt (kala namak — tastes eggy!)\n\n💡 *Missing umami? Nutritional yeast, soy sauce, miso paste, and mushrooms are your best friends!*";

        // ── Allergy ──────────────────────────────────────────────
        if (lower.Contains("allergy") || lower.Contains("allergic") || lower.Contains("intolerance"))
            return "🛡️ **Food Allergy Safety:**\n\n**The Big 14 allergens:** Milk, Eggs, Fish, Shellfish, Tree Nuts, Peanuts, Wheat/Gluten, Soy, Sesame, Celery, Mustard, Lupin, Molluscs, Sulphites.\n\n**Mia can help with:**\n- Finding allergen-free recipes\n- Suggesting safe ingredient substitutions\n- Identifying hidden allergens in dishes\n\n**For serious allergies:** Always check our **Allergy Guide** in the Health section for detailed hidden sources, symptoms, and emergency responses.\n\n⚠️ *For severe or anaphylactic allergies, always consult your doctor and carry an EpiPen.*";

        // ── Meal planning ────────────────────────────────────────
        if (lower.Contains("meal plan") || lower.Contains("meal prep") || lower.Contains("weekly meals"))
            return "📅 **Meal Planning Made Easy:**\n\n**My 5-step method:**\n1. Pick 3–4 proteins for the week\n2. Choose 2–3 grains/carbs\n3. Buy 5+ vegetables (roast a big batch Sunday)\n4. Make one big sauce/dressing to mix meals\n5. Prep in 2 hours, eat well all week\n\n**Time-saving tips:**\n- Cook grains in bulk (rice, quinoa keep 5 days in fridge)\n- Marinate proteins overnight for flavour + tenderness\n- Roasted vegetables work in salads, wraps, pasta, and stir-fries\n\nUse our **Meal Planner** feature to schedule meals, auto-generate shopping lists, and get AI suggestions! 🗓️";

        // ── Storage ──────────────────────────────────────────────
        if (lower.Contains("store") || lower.Contains("storage") || lower.Contains("how long") || lower.Contains("fridge") || lower.Contains("freeze"))
            return "🧊 **Food Storage Guide:**\n\n| Food | Fridge | Freezer |\n|------|--------|---------|\n| Raw chicken | 1–2 days | 9–12 months |\n| Cooked meals | 3–4 days | 2–3 months |\n| Fish | 1–2 days | 6 months |\n| Leftover rice | 3–4 days | 1 month |\n| Bread | 5–7 days | 3 months |\n| Fresh herbs | 1 week (in water) | 6 months (chopped, frozen) |\n\n💡 *Label everything with the date you cooked it!*\n\n⚠️ **Never refreeze raw meat that has been thawed.**";

        // ── Spices & flavour ─────────────────────────────────────
        if (lower.Contains("spice") || lower.Contains("seasoning") || lower.Contains("flavour") || lower.Contains("flavor") || lower.Contains("bland"))
            return "🌶️ **Flavour Building 101:**\n\n**5 flavour layers:**\n1. **Fat** — olive oil, butter, ghee (carries flavour)\n2. **Aromatics** — onion, garlic, ginger, leek\n3. **Dry spices** — toast in fat first to bloom\n4. **Acid** — lemon, vinegar, tomato (brightens everything)\n5. **Salt** — adjust at every stage, not just the end\n\n**Dish tastes flat?** Try:\n- A pinch of salt\n- A squeeze of lemon/lime\n- A dash of fish sauce or soy sauce (umami)\n- Fresh herbs at the very end\n\n💡 *Umami is the secret 5th taste: mushrooms, parmesan, anchovies, miso, and tomato paste are all packed with it!*";

        // ── Catch-all helpful response ───────────────────────────
        var topics = new[]
        {
            ("recipe", "🍽️ Search our **Recipes** section — we have dishes from 195+ countries with full ingredients, steps, and videos!"),
            ("cook", "🍳 Ask me 'how to cook [dish]' and I'll walk you through it step by step!"),
            ("vegetarian", "🥦 Use our **Recipe Search** and filter by 'Vegetarian' to find plant-based dishes from around the world!"),
            ("gluten", "🌾 Filter by 'Gluten-Free' in our Recipe Search, and check the **Allergy Guide** under Health for safe alternatives!"),
            ("breakfast", "🍳 Some great quick breakfasts: Shakshuka (Middle Eastern eggs in tomato), Congee (Chinese rice porridge), Ful Medames (Egyptian fava beans), or classic Ugali with eggs!"),
            ("lunch", "🥗 Quick and healthy lunch ideas: a Thai glass noodle salad, Lebanese fattoush, Japanese bento bowl, or a Ugandan groundnut stew with rice!"),
            ("dinner", "🌙 For dinner inspiration, check our Explore page — filter by continent to discover what the world is eating tonight!"),
            ("africa", "🌍 African cuisine is incredibly diverse! Try Jollof Rice (Nigeria), Injera with Doro Wat (Ethiopia), Bobotie (South Africa), or Ugali with Sukuma Wiki (Kenya)!"),
            ("europe", "🇪🇺 European highlights: Italian Carbonara, French Bouillabaisse, Spanish Paella, Polish Pierogi, Hungarian Goulash — all in our recipe library!"),
            ("asia", "🌏 Asian food heaven: Japanese Ramen, Indian Biryani, Thai Pad Thai, Korean Bibimbap, Vietnamese Pho, Malaysian Nasi Lemak — explore them all!"),
            ("snack", "🥨 Healthy snack ideas: hummus + veggies, edamame, a handful of mixed nuts, apple slices + almond butter, or a boiled egg with a sprinkle of za'atar!"),
        };

        foreach (var (keyword, response) in topics)
            if (lower.Contains(keyword)) return response;

        return "👩‍🍳 I'm Mia, your global cooking assistant! I can help with:\n\n• **Recipes** from any country — just ask *'How do I make [dish]?'*\n• **Ingredient substitutions** — *'What can I use instead of butter?'*\n• **Nutrition advice** — *'What foods are high in protein?'*\n• **Cooking techniques** — *'How do I fry chicken perfectly?'*\n• **Allergy guidance** — *'I'm allergic to gluten, what can I eat?'*\n\nWhat would you like to cook today? 🍳";
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
