using MyMealPlanner.Core.Models;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.DTOs;

namespace MyMealPlanner.Core.Interfaces;

// ── Scraper ──────────────────────────────────────────────────
public interface IRecipeScraperService
{
    Task ScrapeAllSourcesAsync(CancellationToken ct = default);
    Task ScrapeSchemaOrgSitesAsync(CancellationToken ct = default);
    Task ScrapeRedditAsync(CancellationToken ct = default);
    Task ScrapeYouTubeFoodChannelsAsync(CancellationToken ct = default);
    Task ScrapeFoodBlogsByRegionAsync(string continentCode, CancellationToken ct = default);
    Task<bool> IsDuplicateAsync(string contentHash);
}

public interface IContentNormalizerService
{
    Task<Recipe?> NormalizeAsync(ScrapedRaw raw);
    string GenerateContentHash(string title, List<string> firstThreeIngredients);
    string GenerateSlug(string title);
}

// ── YouTube ──────────────────────────────────────────────────
public interface IYouTubeService
{
    Task<YouTubeVideoDto?> GetVideoMetaAsync(string videoId);
    string GetEmbedUrl(string videoId);
    Task<List<YouTubeVideoDto>> SearchFoodVideosAsync(string query, int maxResults = 10);
    Task<List<YouTubeVideoDto>> GetChannelVideosAsync(string channelId, int maxResults = 20);
}

// ── AI / Recipe Generation ───────────────────────────────────
public interface IRecipeGeneratorService
{
    Task<Recipe?> GenerateAsync(RecipePromptRequest request);
    Task<string> GenerateCulturalStoryAsync(string dishName, string countryOfOrigin);
    Task<List<string>> SuggestAlternativeIngredientsAsync(string ingredientName, string targetCountry);
    Task<string> GradePhotoAsync(string photoUrl);
}

public interface IAITaggerService
{
    Task<RecipeTags> AutoTagAsync(Recipe recipe);
    Task<string?> DetectLanguageAsync(string text);
    Task<double> ScoreQualityAsync(RecipeSuggestion suggestion);
}

public interface IAIChatAssistantService
{
    Task<string> ChatAsync(string userId, string message, List<ChatTurn> history);
}

// ── Ranking ──────────────────────────────────────────────────
public interface IRankingService
{
    double CalculateScore(Recipe recipe);
    Task RecalculateAllRankingsAsync(CancellationToken ct = default);
    Task<List<DishRanking>> GetTopByScope(RankingScope scope, string? scopeValue, int count = 50);
    Task<List<DishRanking>> GetTrendingThisWeekAsync(string? countryCode, int count = 20);
}

// ── Localization / Translation ───────────────────────────────
public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string targetLanguage, string sourceLanguage = "en");
    Task TranslateRecipeAsync(int recipeId, string targetLanguage);
    Task<string?> DetectLanguageAsync(string text);
}

// ── Notifications ────────────────────────────────────────────
public interface INotificationService
{
    Task SendAsync(string userId, NotificationType type, string title, string body, string? actionUrl = null);
    Task SendBulkAsync(List<string> userIds, NotificationType type, string title, string body);
    Task MarkReadAsync(int notificationId, string userId);
    Task MarkAllReadAsync(string userId);
    Task<List<Notification>> GetUnreadAsync(string userId, int count = 20);
    Task ScheduleDailyMealSuggestionsAsync();
    Task ScheduleWeeklyMealPlansAsync();
    Task SendReEngagementAsync();
}

// ── Health ───────────────────────────────────────────────────
public interface IAllergyService
{
    Task<AllergyCheckResult> CheckRecipeAsync(int recipeId, string userId);
    Task<AllergyCheckResult> CheckIngredientsAsync(List<string> ingredients, List<string> userAllergens);
    Task<AllergyGuide?> GetGuideAsync(AllergenType type);
}

public interface INutritionService
{
    Task<List<NutrientFood>> GetFoodsByNutrientAsync(NutrientCategory category, int count = 50);
    Task<NutritionSummary> GetRecipeNutritionAsync(int recipeId, int servings = 1);
    Task<DailyTargets> GetTargetsByAgeBracketAsync(AgeBracket bracket);
}

// ── Cost ─────────────────────────────────────────────────────
public interface IIngredientCostService
{
    Task<IngredientCost?> GetCostAsync(int recipeId, string countryCode);
    Task RefreshCostsAsync(int recipeId);
    Task<List<IngredientCost>> GetCostsByRecipeAsync(int recipeId);
}

// ── Social ───────────────────────────────────────────────────
public interface IFollowService
{
    Task FollowAsync(string followerId, string followeeId);
    Task UnfollowAsync(string followerId, string followeeId);
    Task<bool> IsFollowingAsync(string followerId, string followeeId);
    Task<List<ApplicationUser>> GetSuggestedUsersAsync(string userId, int count = 10);
}

public interface ISocialFeedService
{
    Task<List<FeedItem>> GetPersonalFeedAsync(string userId, int page = 1, int pageSize = 20);
    Task<List<FeedItem>> GetDiscoverFeedAsync(string userId, DiscoveryScope scope, string? scopeValue, int page = 1, int pageSize = 20);
}

// ── Quiz ─────────────────────────────────────────────────────
public interface IQuizService
{
    Task<List<QuizQuestion>> GenerateQuizAsync(string userId, ChefLevel targetLevel, int count = 10);
    Task<QuizResult> SubmitQuizAsync(string userId, QuizAttempt attempt);
    Task<bool> CanAttemptAsync(string userId, ChefLevel targetLevel);
}

// ── Personalisation ──────────────────────────────────────────
public interface IPersonalisationService
{
    Task<List<Recipe>> GetRecommendationsAsync(string userId, int count = 12);
    Task<List<string>> GetRelatedSearchesAsync(string query, string userId);
    Task CheckAndAwardBadgesAsync(string userId);
    Task UpdateChefLevelAsync(string userId);
}

// ── Jokes ────────────────────────────────────────────────────
public interface IJokeService
{
    Task<CookingJoke?> GetDailyJokeAsync();
    Task<CookingJoke?> GetRandomJokeAsync();
    Task<List<CookingJoke>> GetByCategoryAsync(string category, int count = 10);
    Task ScrapeNewJokesAsync();
    Task GenerateAIJokesAsync(int count = 5);
}

// ── Image Search ─────────────────────────────────────────────
public interface IImageSearchService
{
    Task<FoodIdentificationResult> IdentifyFoodFromImageAsync(Stream imageStream, string contentType);
    Task<List<Recipe>> FindRecipesByImageAsync(Stream imageStream, string contentType, string userId);
}
