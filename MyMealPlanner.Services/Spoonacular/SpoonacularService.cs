using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Services.Spoonacular;

public class SpoonacularService : ISpoonacularService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly IContentNormalizerService _normalizer;
    private readonly ILogger<SpoonacularService> _logger;

    public SpoonacularService(
        ApplicationDbContext db,
        IConfiguration config,
        IHttpClientFactory httpFactory,
        IContentNormalizerService normalizer,
        ILogger<SpoonacularService> logger)
    {
        _db = db;
        _config = config;
        _http = httpFactory.CreateClient("ScraperClient");
        _normalizer = normalizer;
        _logger = logger;
    }

    public async Task<Recipe?> SearchAndImportRecipeAsync(string query)
    {
        var apiKey = _config["Spoonacular:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("[Spoonacular] API Key is missing in configurations.");
            return null;
        }

        try
        {
            _logger.LogInformation("[Spoonacular] Searching for: {Query}", query);

            // Step 1: Search recipes
            var searchUrl = $"https://api.spoonacular.com/recipes/complexSearch?query={Uri.EscapeDataString(query)}&number=1&apiKey={apiKey}";
            var searchResponse = await _http.GetAsync(searchUrl);
            if (!searchResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Spoonacular] Search request failed with status: {Status}", searchResponse.StatusCode);
                return null;
            }

            var searchJson = await searchResponse.Content.ReadAsStringAsync();
            using var searchDoc = JsonDocument.Parse(searchJson);
            var results = searchDoc.RootElement.GetProperty("results");

            if (results.GetArrayLength() == 0)
            {
                _logger.LogInformation("[Spoonacular] No recipes found for query: {Query}", query);
                return null;
            }

            var recipeId = results[0].GetProperty("id").GetInt32();

            // Step 2: Get detailed recipe info
            _logger.LogInformation("[Spoonacular] Fetching details for recipe ID: {Id}", recipeId);
            var infoUrl = $"https://api.spoonacular.com/recipes/{recipeId}/information?includeNutrition=true&apiKey={apiKey}";
            var infoResponse = await _http.GetAsync(infoUrl);
            if (!infoResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Spoonacular] Details request failed with status: {Status}", infoResponse.StatusCode);
                return null;
            }

            var infoJson = await infoResponse.Content.ReadAsStringAsync();
            using var infoDoc = JsonDocument.Parse(infoJson);
            var root = infoDoc.RootElement;

            // Map standard properties
            var title = root.GetProperty("title").GetString() ?? "";
            var slug = _normalizer.GenerateSlug(title);

            // Check if duplicate slug exists
            var existing = await _db.Recipes.FirstOrDefaultAsync(r => r.Slug == slug);
            if (existing != null)
            {
                _logger.LogInformation("[Spoonacular] Recipe already exists: {Title}", title);
                return existing;
            }

            var summary = root.GetProperty("summary").GetString() ?? "";
            // Clean up Spoonacular HTML summary text
            summary = System.Text.RegularExpressions.Regex.Replace(summary, "<.*?>", string.Empty).Trim();
            if (summary.Length > 500) summary = summary[..497] + "...";

            var readyInMinutes = root.TryGetProperty("readyInMinutes", out var rim) ? rim.GetInt32() : 30;
            var servings = root.TryGetProperty("servings", out var sv) ? sv.GetInt32() : 4;
            var coverImage = root.TryGetProperty("image", out var img) ? img.GetString() : null;
            var sourceUrl = root.TryGetProperty("sourceUrl", out var su) ? su.GetString() : null;

            var recipe = new Recipe
            {
                Title = title,
                Slug = slug,
                Description = summary,
                PrepTimeMinutes = Math.Max(5, readyInMinutes / 3),
                CookTimeMinutes = Math.Max(5, readyInMinutes * 2 / 3),
                Servings = servings,
                CoverImageUrl = coverImage,
                SourceUrl = sourceUrl,
                Source = RecipeSource.PlatformCurated,
                IsApproved = true,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Language = "en",
                DifficultyLevel = readyInMinutes < 20 ? DifficultyLevel.Easy : (readyInMinutes < 45 ? DifficultyLevel.Intermediate : DifficultyLevel.Advanced),
                CookingEnvironment = CookingEnvironment.Home,
                MealType = MealType.Dinner
            };

            // Parse cuisines for cultural tag & origin
            if (root.TryGetProperty("cuisines", out var cuisines) && cuisines.GetArrayLength() > 0)
            {
                var mainCuisine = cuisines[0].GetString();
                recipe.CultureTag = mainCuisine;
                recipe.OriginCountry = mainCuisine;

                // Guess origin continent
                recipe.OriginContinent = mainCuisine switch
                {
                    "Italian" or "French" or "Spanish" or "German" or "Greek" or "European" => "Europe",
                    "Chinese" or "Japanese" or "Indian" or "Thai" or "Vietnamese" or "Korean" or "Asian" => "Asia",
                    "Mexican" or "American" or "Canadian" => "Americas",
                    "African" or "Ethiopian" or "Moroccan" or "Egyptian" => "Africa",
                    "Australian" or "Oceanic" => "Oceania",
                    _ => "Global"
                };
            }

            // Parse dishTypes for meal classification
            if (root.TryGetProperty("dishTypes", out var dishTypes) && dishTypes.GetArrayLength() > 0)
            {
                foreach (var dt in dishTypes.EnumerateArray())
                {
                    var dtStr = dt.GetString()?.ToLowerInvariant();
                    if (dtStr == "breakfast" || dtStr == "morning") { recipe.MealType = MealType.Breakfast; break; }
                    if (dtStr == "brunch") { recipe.MealType = MealType.Brunch; break; }
                    if (dtStr == "snack" || dtStr == "appetizer") { recipe.MealType = MealType.Snack; break; }
                    if (dtStr == "dessert" || dtStr == "sweet") { recipe.MealType = MealType.Dessert; break; }
                    if (dtStr == "drink" || dtStr == "beverage") { recipe.MealType = MealType.Drink; break; }
                }
            }

            // Parse Ingredients
            if (root.TryGetProperty("extendedIngredients", out var extIngs))
            {
                int order = 1;
                foreach (var ing in extIngs.EnumerateArray())
                {
                    var ingName = ing.GetProperty("name").GetString() ?? "";
                    if (string.IsNullOrEmpty(ingName)) continue;

                    var amount = ing.TryGetProperty("amount", out var amt) ? amt.GetDecimal() : 1m;
                    var unit = ing.TryGetProperty("unit", out var un) ? un.GetString() : "item";
                    if (string.IsNullOrEmpty(unit)) unit = "item";

                    recipe.Ingredients.Add(new Ingredient
                    {
                        Name = ingName,
                        Quantity = amount,
                        Unit = unit,
                        SortOrder = order++
                    });
                }
            }

            // Parse Steps
            if (root.TryGetProperty("analyzedInstructions", out var instructions) && instructions.GetArrayLength() > 0)
            {
                var firstInstruction = instructions[0];
                if (firstInstruction.TryGetProperty("steps", out var steps))
                {
                    int order = 1;
                    foreach (var step in steps.EnumerateArray())
                    {
                        var stepText = step.GetProperty("step").GetString() ?? "";
                        if (string.IsNullOrEmpty(stepText)) continue;

                        var duration = step.TryGetProperty("length", out var len) && len.TryGetProperty("number", out var num) ? num.GetInt32() : (int?)null;

                        recipe.Steps.Add(new RecipeStep
                        {
                            StepOrder = order++,
                            Instruction = stepText,
                            DurationMinutes = duration
                        });
                    }
                }
            }

            // Fallback steps if analyzedInstructions is empty but instructions string exists
            if (recipe.Steps.Count == 0 && root.TryGetProperty("instructions", out var rawInst))
            {
                var rawInstText = rawInst.GetString();
                if (!string.IsNullOrEmpty(rawInstText))
                {
                    var lines = rawInstText.Split(new[] { "\n", "\r", ". " }, StringSplitOptions.RemoveEmptyEntries);
                    int order = 1;
                    foreach (var line in lines)
                    {
                        var cleanLine = System.Text.RegularExpressions.Regex.Replace(line, "<.*?>", string.Empty).Trim();
                        if (string.IsNullOrEmpty(cleanLine)) continue;
                        recipe.Steps.Add(new RecipeStep
                        {
                            StepOrder = order++,
                            Instruction = cleanLine
                        });
                    }
                }
            }

            // Parse Nutrition details
            if (root.TryGetProperty("nutrition", out var nutrition) && nutrition.TryGetProperty("nutrients", out var nutrients))
            {
                foreach (var nut in nutrients.EnumerateArray())
                {
                    var nutName = nut.GetProperty("name").GetString();
                    var amt = nut.GetProperty("amount").GetDecimal();
                    var unit = nut.GetProperty("unit").GetString() ?? "g";
                    var dv = nut.TryGetProperty("percentOfDailyNeeds", out var pdn) ? pdn.GetDecimal() : 0m;

                    NutrientCategory? category = nutName switch
                    {
                        "Protein" => NutrientCategory.Protein,
                        "Carbohydrates" => NutrientCategory.Carbohydrates,
                        "Fat" => NutrientCategory.HealthyFats,
                        "Fiber" => NutrientCategory.Fibre,
                        "Calcium" => NutrientCategory.Calcium,
                        "Iron" => NutrientCategory.Iron,
                        "Vitamin C" => NutrientCategory.VitaminC,
                        "Vitamin A" => NutrientCategory.VitaminA,
                        "Vitamin D" => NutrientCategory.VitaminD,
                        "Sodium" => NutrientCategory.Sodium,
                        "Potassium" => NutrientCategory.Potassium,
                        _ => null
                    };

                    if (category.HasValue)
                    {
                        recipe.NutritionInfo.Add(new RecipeNutrition
                        {
                            NutrientCategory = category.Value,
                            AmountPer100g = amt, // Spoonacular gives per serving; we store per serving/100g based on design
                            Unit = unit,
                            DailyValuePercent = dv
                        });
                    }
                }
            }

            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync();

            _logger.LogInformation("[Spoonacular] Successfully imported recipe: {Title}", title);
            return recipe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Spoonacular] Error importing recipe for query: {Query}", query);
            return null;
        }
    }
}
