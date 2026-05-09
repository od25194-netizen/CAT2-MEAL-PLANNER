using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Models;

namespace MyMealPlanner.Core.DTOs;

public record YouTubeVideoDto(
    string VideoId,
    string Title,
    string ChannelName,
    string ChannelId,
    string ThumbnailUrl,
    string EmbedUrl,
    long ViewCount,
    long LikeCount,
    string Duration,
    DateTime PublishedAt);

public record RecipePromptRequest(
    string? CountryCode,
    string? CultureTag,
    CookingEnvironment Environment,
    DifficultyLevel ChefLevel,
    List<string> DietaryRestrictions,
    List<string> AvailableIngredients,
    MealType MealType,
    int Servings,
    string? TargetLanguage);

public record RecipeTags(
    string? DetectedCountry,
    string? DetectedContinent,
    string? CultureTag,
    List<string> DietaryFlags,
    List<string> AllergenFlags,
    List<string> NutrientFlags,
    DifficultyLevel Difficulty,
    Season Season,
    double QualityScore);

public record AllergyCheckResult(
    bool HasAllergen,
    List<AllergenFound> AllergenDetails,
    string? SafeVersionAdvice);

public record AllergenFound(
    AllergenType Type,
    string IngredientName,
    AllergenRisk Risk,
    string? HiddenSource,
    string? SafeSubstitute,
    string Symptoms,
    string FirstResponse);

public record NutritionSummary(
    int Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    decimal FibreG,
    decimal SugarG,
    decimal SodiumMg,
    List<NutrientHighlight> Highlights);

public record NutrientHighlight(
    NutrientCategory Category,
    decimal DailyValuePercent,
    string Level); // Low / Medium / High / Excellent

public record DailyTargets(
    AgeBracket Bracket,
    int CaloriesMin,
    int CaloriesMax,
    decimal ProteinG,
    decimal CalciumMg,
    decimal IronMg,
    decimal VitaminCMg,
    decimal VitaminDMcg,
    string Notes);

public record FeedItem(
    string ItemType,         // Recipe | CookLog | Following
    int? RecipeId,
    string? RecipeTitle,
    string? RecipeCoverUrl,
    string? UserId,
    string? UserName,
    string? UserAvatarUrl,
    string? ActionText,
    DateTime Timestamp);

public record QuizResult(
    bool IsPassed,
    double ScorePercent,
    int CorrectAnswers,
    int TotalQuestions,
    bool LeveledUp,
    ChefLevel? NewLevel,
    DateTime? NextAttemptAllowedAt,
    List<QuizAnswerFeedback> Feedback);

public record QuizAnswerFeedback(
    int QuestionId,
    bool IsCorrect,
    string CorrectAnswer,
    string? Explanation);

public record FoodIdentificationResult(
    string? IdentifiedDish,
    string? Country,
    double Confidence,
    List<string> Labels,
    List<Recipe> MatchingRecipes);

public record ChatTurn(string Role, string Content);

public record CostBreakdown(
    string CountryCode,
    string CurrencyCode,
    string CurrencySymbol,
    decimal TotalCost,
    decimal CostPerServing,
    string BudgetTier,
    List<IngredientCostLine> Lines);

public record IngredientCostLine(
    string Name,
    decimal Quantity,
    string Unit,
    decimal Cost,
    string? CheaperAlternative);
