using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Services;

public interface IRecipePromotionService
{
    Task<Recipe> PromoteAsync(RecipeSuggestion suggestion);
}

public class RecipePromotionService : IRecipePromotionService
{
    private readonly ApplicationDbContext _db;

    public RecipePromotionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Recipe> PromoteAsync(RecipeSuggestion suggestion)
    {
        var recipe = new Recipe
        {
            Title = suggestion.Title,
            Slug = suggestion.Title.ToLower().Replace(" ", "-"), // Basic slugification
            Description = suggestion.Description,
            CulturalStory = suggestion.CulturalStory,
            OriginCountry = suggestion.OriginCountry,
            OriginCountryCode = suggestion.OriginCountryCode,
            OriginContinent = suggestion.OriginContinent,
            MealType = suggestion.MealType,
            DifficultyLevel = suggestion.DifficultyLevel,
            PrepTimeMinutes = suggestion.PrepTimeMinutes,
            CookTimeMinutes = suggestion.CookTimeMinutes,
            IsApproved = true,
            IsPublished = true,
            SubmittedByUserId = suggestion.SubmittedByUserId,
            CreatedAt = DateTime.UtcNow,
            Source = RecipeSource.CommunitySuggestion
        };

        _db.Recipes.Add(recipe);
        suggestion.PublishedRecipeId = recipe.Id;
        
        await _db.SaveChangesAsync();
        return recipe;
    }
}
