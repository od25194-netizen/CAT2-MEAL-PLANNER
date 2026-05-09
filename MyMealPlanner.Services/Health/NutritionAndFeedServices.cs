using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.DTOs;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Services.Health;

/// <summary>
/// Calculates nutritional summaries for recipes and returns
/// daily targets per age bracket, sourced from WHO / USDA data.
/// </summary>
public class NutritionService : INutritionService
{
    private readonly ApplicationDbContext _db;

    public NutritionService(ApplicationDbContext db) => _db = db;

    public async Task<List<NutrientFood>> GetFoodsByNutrientAsync(
        NutrientCategory category, int count = 50)
        => await _db.NutrientFoods
            .Where(f => f.NutrientCategory == category)
            .OrderBy(f => f.SortRank)
            .Take(count)
            .ToListAsync();

    public async Task<NutritionSummary> GetRecipeNutritionAsync(int recipeId, int servings = 1)
    {
        var info = await _db.RecipeNutritions
            .Where(n => n.RecipeId == recipeId)
            .ToListAsync();

        if (!info.Any())
        {
            // Return estimated values based on recipe category
            var recipe = await _db.Recipes.FindAsync(recipeId);
            return EstimateNutrition(recipe, servings);
        }

        decimal calories = info.FirstOrDefault(n => n.NutrientCategory == NutrientCategory.Carbohydrates)?.AmountPer100g * 4 ?? 350;
        decimal protein  = info.FirstOrDefault(n => n.NutrientCategory == NutrientCategory.Protein)?.AmountPer100g ?? 20;
        decimal carbs    = info.FirstOrDefault(n => n.NutrientCategory == NutrientCategory.Carbohydrates)?.AmountPer100g ?? 45;
        decimal fat      = info.FirstOrDefault(n => n.NutrientCategory == NutrientCategory.HealthyFats)?.AmountPer100g ?? 12;
        decimal fibre    = info.FirstOrDefault(n => n.NutrientCategory == NutrientCategory.Fibre)?.AmountPer100g ?? 4;

        var highlights = info
            .Where(n => n.DailyValuePercent > 15)
            .OrderByDescending(n => n.DailyValuePercent)
            .Take(5)
            .Select(n => new NutrientHighlight(
                Category:         n.NutrientCategory,
                DailyValuePercent: (decimal)n.DailyValuePercent,
                Level:            n.DailyValuePercent >= 50 ? "Excellent"
                                : n.DailyValuePercent >= 25 ? "High"
                                : n.DailyValuePercent >= 15 ? "Medium" : "Low"))
            .ToList();

        return new NutritionSummary(
            Calories:   (int)(calories * servings / 4),
            ProteinG:   protein,
            CarbsG:     carbs,
            FatG:       fat,
            FibreG:     fibre,
            SugarG:     carbs * 0.3m,
            SodiumMg:   400m,
            Highlights: highlights);
    }

    public async Task<DailyTargets> GetTargetsByAgeBracketAsync(AgeBracket bracket)
    {
        // WHO / USDA daily reference values
        return bracket switch
        {
            AgeBracket.Infants    => new DailyTargets(bracket, 600,  900,  13,  200, 11,  25,  10,  "Breast milk / formula first. Iron-fortified foods from 6 months."),
            AgeBracket.Children   => new DailyTargets(bracket, 1200, 1600, 19,  800, 7,   25,  10,  "Focus on calcium for bone growth. Avoid added sugars and salt."),
            AgeBracket.Teenagers  => new DailyTargets(bracket, 1800, 2600, 52,  1300,15,  65,  15,  "Iron critical for girls. Calcium builds peak bone density. High energy needs."),
            AgeBracket.YoungAdults=> new DailyTargets(bracket, 1800, 2400, 50,  1000,8,   75,  15,  "Balanced approach. Gut health matters. Folate essential for women."),
            AgeBracket.Adults     => new DailyTargets(bracket, 1600, 2200, 50,  1000,8,   75,  15,  "Heart health focus. Fibre and antioxidants reduce chronic disease risk."),
            AgeBracket.Seniors    => new DailyTargets(bracket, 1600, 2000, 50,  1200,8,   75,  20,  "Calcium + Vitamin D critical. B12 often deficient. Lower sodium needed."),
            AgeBracket.Elderly    => new DailyTargets(bracket, 1400, 1800, 46,  1200,8,   75,  20,  "Smaller portions, nutrient-dense. Hydration especially important."),
            _                     => new DailyTargets(bracket, 1800, 2400, 50,  1000,8,   75,  15,  "Consult a healthcare provider for personalised targets.")
        };
    }

    private static NutritionSummary EstimateNutrition(Recipe? recipe, int servings)
    {
        // Simple heuristic estimation when no detailed data exists
        decimal baseCalories = recipe?.DifficultyLevel switch
        {
            DifficultyLevel.Beginner => 300,
            DifficultyLevel.Easy     => 380,
            _                        => 450
        };

        return new NutritionSummary(
            Calories:   (int)(baseCalories * servings / (recipe?.Servings ?? 4)),
            ProteinG:   22,
            CarbsG:     45,
            FatG:       14,
            FibreG:     5,
            SugarG:     8,
            SodiumMg:   520,
            Highlights: new List<NutrientHighlight>());
    }
}



/// <summary>
/// Builds the personalised social feed — showing activity
/// from followed users, trending recipes, and AI-recommended content.
/// </summary>
public class SocialFeedService : ISocialFeedService
{
    private readonly ApplicationDbContext _db;
    private readonly IPersonalisationService _personalisation;

    public SocialFeedService(
        ApplicationDbContext db,
        IPersonalisationService personalisation)
    {
        _db              = db;
        _personalisation = personalisation;
    }

    public async Task<List<FeedItem>> GetPersonalFeedAsync(
        string userId, int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;

        // Get IDs of users being followed
        var followingIds = await _db.UserFollows
            .Where(f => f.FollowerId == userId)
            .Select(f => f.FolloweeId)
            .ToListAsync();

        var items = new List<FeedItem>();

        // 1. Recent recipes from followed users
        var followedRecipes = await _db.Recipes
            .Include(r => r.SubmittedByUser)
            .Where(r => r.IsPublished &&
                        r.SubmittedByUserId != null &&
                        followingIds.Contains(r.SubmittedByUserId))
            .OrderByDescending(r => r.CreatedAt)
            .Take(pageSize * 2)
            .ToListAsync();

        items.AddRange(followedRecipes.Select(r => new FeedItem(
            ItemType:       "Recipe",
            RecipeId:       r.Id,
            RecipeTitle:    r.Title,
            RecipeCoverUrl: r.CoverImageUrl,
            UserId:         r.SubmittedByUserId,
            UserName:       r.SubmittedByUser?.FullName,
            UserAvatarUrl:  r.SubmittedByUser?.ProfilePhotoUrl,
            ActionText:     $"posted a new recipe from {r.OriginCountry ?? "the world"}",
            Timestamp:      r.CreatedAt)));

        // 2. Recent cook logs from followed users
        var cookLogs = await _db.CookLogs
            .Include(c => c.Recipe)
            .Include(c => c.User)
            .Where(c => c.IsPublic && followingIds.Contains(c.UserId))
            .OrderByDescending(c => c.CookedAt)
            .Take(pageSize)
            .ToListAsync();

        items.AddRange(cookLogs.Select(c => new FeedItem(
            ItemType:       "CookLog",
            RecipeId:       c.RecipeId,
            RecipeTitle:    c.Recipe?.Title,
            RecipeCoverUrl: c.PhotoUrl ?? c.Recipe?.CoverImageUrl,
            UserId:         c.UserId,
            UserName:       c.User?.FullName,
            UserAvatarUrl:  c.User?.ProfilePhotoUrl,
            ActionText:     $"cooked {c.Recipe?.Title} and rated it {new string('⭐', c.Rating)}",
            Timestamp:      c.CookedAt)));

        // 3. If feed is sparse, add recommendations
        if (items.Count < pageSize / 2)
        {
            var recommended = await _personalisation.GetRecommendationsAsync(userId, pageSize / 2);
            items.AddRange(recommended.Select(r => new FeedItem(
                ItemType:       "Recipe",
                RecipeId:       r.Id,
                RecipeTitle:    r.Title,
                RecipeCoverUrl: r.CoverImageUrl,
                UserId:         null,
                UserName:       null,
                UserAvatarUrl:  null,
                ActionText:     $"Recommended for you · {r.OriginCountry}",
                Timestamp:      r.CreatedAt)));
        }

        return items
            .OrderByDescending(i => i.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToList();
    }

    public async Task<List<FeedItem>> GetDiscoverFeedAsync(
        string userId, DiscoveryScope scope, string? scopeValue,
        int page = 1, int pageSize = 20)
    {
        var skip    = (page - 1) * pageSize;
        var query   = _db.Recipes
            .Where(r => r.IsPublished)
            .AsQueryable();

        query = scope switch
        {
            DiscoveryScope.Country    => query.Where(r => r.OriginCountryCode == scopeValue),
            DiscoveryScope.Continent  => query.Where(r => r.OriginContinent == scopeValue),
            DiscoveryScope.Local      => query.Where(r => r.Region == scopeValue || r.OriginCountryCode == scopeValue),
            _                         => query
        };

        var recipes = await query
            .OrderByDescending(r => r.LikeCount)
            .ThenByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return recipes.Select(r => new FeedItem(
            ItemType:       "Recipe",
            RecipeId:       r.Id,
            RecipeTitle:    r.Title,
            RecipeCoverUrl: r.CoverImageUrl,
            UserId:         r.SubmittedByUserId,
            UserName:       null,
            UserAvatarUrl:  null,
            ActionText:     $"{r.OriginCountry} · {r.MealType}",
            Timestamp:      r.CreatedAt))
            .ToList();
    }
}
