using MyMealPlanner.Core.Models;

namespace MyMealPlanner.Core.Interfaces;

public interface ISpoonacularService
{
    Task<Recipe?> SearchAndImportRecipeAsync(string query);
}
