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
            OriginCountry = suggestion.CountryOfOrigin ?? "Unknown",
            OriginContinent = "Unknown",
            MealType = MealType.Dinner,
            DifficultyLevel = DifficultyLevel.Intermediate,
            PrepTimeMinutes = 15,
            CookTimeMinutes = 30,
            IsApproved = true,
            IsPublished = true,
            SubmittedByUserId = suggestion.SubmittedByUserId,
            CreatedAt = DateTime.UtcNow,
            Source = RecipeSource.UserSubmitted
        };

        _db.Recipes.Add(recipe);
        suggestion.PublishedRecipeId = recipe.Id;
        
        await _db.SaveChangesAsync();
        return recipe;
    }
}
