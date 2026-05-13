using Microsoft.AspNetCore.Identity;
using MyMealPlanner.Core.Enums;

namespace MyMealPlanner.Core.Models;

// ═══════════════════════════════════════════════════════════════
// USER
// ═══════════════════════════════════════════════════════════════
public class ApplicationUser : IdentityUser
{
    // Basic
    public string FullName          { get; set; } = string.Empty;
    public string? ProfilePhotoUrl  { get; set; }
    public string? CoverPhotoUrl    { get; set; }
    public string? Bio              { get; set; }
    public DateTime? DateOfBirth    { get; set; }
    public AgeBracket AgeBracket    { get; set; }

    // Location
    public string? CountryCode      { get; set; }
    public string? CountryName      { get; set; }
    public string? ContinentCode    { get; set; }
    public string? CityName         { get; set; }
    public string? Address          { get; set; }   // encrypted

    // Preferences
    public string PreferredLanguage  { get; set; } = "en";
    public string PreferredCurrency  { get; set; } = "USD";
    public string PreferredUnits     { get; set; } = "metric";
    public string TimeZone           { get; set; } = "UTC";
    public HealthGoal HealthGoal     { get; set; }
    public BloodType BloodType       { get; set; }  // encrypted, optional
    public int NumberOfPeopleICookFor { get; set; } = 1;

    // Food identity
    public string? FavouriteDish            { get; set; }
    public string? Hobbies                  { get; set; }
    public string? FavouriteCuisinesJson    { get; set; }
    public string? DietaryRestrictionsJson  { get; set; }
    public string? AllergiesJson            { get; set; }
    public string? DislikedFoodsJson        { get; set; }

    // Gamification
    public ChefLevel ChefLevel                  { get; set; } = ChefLevel.Level1_KitchenNewcomer;
    public VerificationTick VerificationTick    { get; set; } = VerificationTick.None;
    public int TotalPoints                      { get; set; }
    public int CookStreak                       { get; set; }
    public DateTime? LastCookDate               { get; set; }
    public bool IsVerifiedChef                  { get; set; }
    public bool IsExpert                        { get; set; }

    // Social
    public string? YoutubeChannelUrl    { get; set; }
    public string? InstagramHandle      { get; set; }
    public int FollowerCount            { get; set; }
    public int FollowingCount           { get; set; }

    // Settings
    public bool DarkMode                        { get; set; }
    public string AccentColor                   { get; set; } = "#E8630A";
    public bool ShowOnlineStatus                { get; set; } = true;
    public string ProfileVisibility             { get; set; } = "Public";
    public bool EmailNotifications              { get; set; } = true;
    public bool PushNotifications               { get; set; } = true;
    public bool DailyJokeNotification           { get; set; } = true;
    public bool MealTimeReminders               { get; set; } = true;
    public string NotificationFrequency         { get; set; } = "Daily";

    // Timestamps
    public DateTime CreatedAt           { get; set; } = DateTime.UtcNow;
    public DateTime? LastActiveAt       { get; set; }
    public bool IsDeleted               { get; set; }
    public DateTime? DeleteRequestedAt  { get; set; }
    public DateTime? DeleteScheduledAt  { get; set; }

    // Navigation
    public ICollection<Recipe>          Recipes     { get; set; } = [];
    public ICollection<PetProfile>      Pets        { get; set; } = [];
    public ICollection<SavedCollection> Collections { get; set; } = [];
    public ICollection<CookLog>         CookLogs    { get; set; } = [];
    public ICollection<UserBadge>       Badges      { get; set; } = [];
    public ICollection<UserFollow>      Followers   { get; set; } = [];
    public ICollection<UserFollow>      Following   { get; set; } = [];
    public ICollection<MealPlan>        MealPlans   { get; set; } = [];
    public ICollection<Notification>    Notifications { get; set; } = [];
    public ICollection<QuizAttempt>     QuizAttempts  { get; set; } = [];
    public ICollection<ChatMessage>     ChatMessages  { get; set; } = [];
    public ICollection<UserBlock>       BlockedUsers  { get; set; } = [];
}

// ═══════════════════════════════════════════════════════════════
// PET
// ═══════════════════════════════════════════════════════════════
public class PetProfile
{
    public int Id                       { get; set; }
    public string OwnerId               { get; set; } = string.Empty;
    public ApplicationUser Owner        { get; set; } = null!;
    public string PetName               { get; set; } = string.Empty;
    public string? ProfilePhotoUrl      { get; set; }
    public PetType Type                 { get; set; }
    public string? Breed                { get; set; }
    public int AgeYears                 { get; set; }
    public int AgeMonths                { get; set; }
    public decimal? WeightKg            { get; set; }
    public string? FoodAllergiesJson    { get; set; }
    public string? HealthConditionsJson { get; set; }
    public bool IsNeutered              { get; set; }
    public DateTime CreatedAt           { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════
// RECIPE
// ═══════════════════════════════════════════════════════════════
public class Recipe
{
    public int Id                       { get; set; }
    public string Title                 { get; set; } = string.Empty;
    public string Slug                  { get; set; } = string.Empty;
    public string Description           { get; set; } = string.Empty;
    public string? CulturalStory        { get; set; }

    // Origin
    public string? OriginCountry        { get; set; }
    public string? OriginCountryCode    { get; set; }
    public string? OriginContinent      { get; set; }
    public string? CultureTag           { get; set; }
    public string? Region               { get; set; }
    public Season Season                { get; set; } = Season.AllYear;

    // Classification
    public CookingEnvironment CookingEnvironment { get; set; }
    public MealType MealType                     { get; set; }
    public DifficultyLevel DifficultyLevel       { get; set; }
    public string? DietaryTagsJson               { get; set; }
    public string? AllergenTagsJson              { get; set; }
    public string? NutrientTagsJson              { get; set; }

    // Timing & cost
    public int PrepTimeMinutes          { get; set; }
    public int CookTimeMinutes          { get; set; }
    public int Servings                 { get; set; } = 4;
    public decimal? EstimatedCostUSD    { get; set; }

    // Media
    public string? CoverImageUrl        { get; set; }
    public string? YouTubeVideoId       { get; set; }
    public string? YouTubeChannelId     { get; set; }
    public string? YouTubeChannelName   { get; set; }

    // Source
    public RecipeSource Source          { get; set; }
    public string? SourceUrl            { get; set; }
    public DateTime? ScrapedAt          { get; set; }
    public bool IsApproved              { get; set; }
    public bool IsPublished             { get; set; }
    public bool IsFeatured              { get; set; }
    public string Language              { get; set; } = "en";

    // Engagement
    public int LikeCount                { get; set; }
    public int SaveCount                { get; set; }
    public int ViewCount                { get; set; }
    public int CommentCount             { get; set; }
    public int ShareCount               { get; set; }
    public int CookLogCount             { get; set; }
    public double RatingAverage         { get; set; }

    // Ownership
    public string? SubmittedByUserId    { get; set; }
    public ApplicationUser? SubmittedByUser { get; set; }

    public DateTime CreatedAt           { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt           { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Ingredient>              Ingredients             { get; set; } = [];
    public ICollection<RecipeStep>              Steps                   { get; set; } = [];
    public ICollection<AlternativeIngredient>   AlternativeIngredients  { get; set; } = [];
    public ICollection<Comment>                 Comments                { get; set; } = [];
    public ICollection<RecipeTranslation>       Translations            { get; set; } = [];
    public ICollection<RecipeNutrition>         NutritionInfo           { get; set; } = [];
    public ICollection<DishRanking>             Rankings                { get; set; } = [];
    public ICollection<IngredientCost>          IngredientCosts         { get; set; } = [];
    public ICollection<RecipeLike>              Likes                   { get; set; } = [];
}

public class Ingredient
{
    public int Id                   { get; set; }
    public int RecipeId             { get; set; }
    public Recipe Recipe            { get; set; } = null!;
    public string Name              { get; set; } = string.Empty;
    public string? LocalName        { get; set; }
    public decimal Quantity         { get; set; }
    public string Unit              { get; set; } = string.Empty;
    public string? Notes            { get; set; }
    public int SortOrder            { get; set; }
    public bool IsOptional          { get; set; }
    public string? AllergenTagsJson { get; set; }
}

public class AlternativeIngredient
{
    public int Id                           { get; set; }
    public int RecipeId                     { get; set; }
    public Recipe Recipe                    { get; set; } = null!;
    public string OriginalIngredientName    { get; set; } = string.Empty;
    public string SubstituteName            { get; set; } = string.Empty;
    public string? Reason                   { get; set; }
    public string? RegionAvailability       { get; set; }
    public decimal CostSavingPercent        { get; set; }
    public bool IsVegan                     { get; set; }
    public bool IsGlutenFree                { get; set; }
}

public class RecipeStep
{
    public int Id                       { get; set; }
    public int RecipeId                 { get; set; }
    public Recipe Recipe                { get; set; } = null!;
    public int StepOrder                { get; set; }
    public string Instruction           { get; set; } = string.Empty;
    public string? MediaUrl             { get; set; }
    public int? DurationMinutes         { get; set; }
    public string? ChefTip              { get; set; }
    public string? EnvironmentVariant   { get; set; }
}

public class RecipeTranslation
{
    public int Id                   { get; set; }
    public int RecipeId             { get; set; }
    public Recipe Recipe            { get; set; } = null!;
    public string LanguageCode      { get; set; } = string.Empty;
    public string Title             { get; set; } = string.Empty;
    public string Description       { get; set; } = string.Empty;
    public string? CulturalStory    { get; set; }
    public DateTime TranslatedAt    { get; set; } = DateTime.UtcNow;
    public bool IsAutoTranslated    { get; set; } = true;
}

public class RecipeNutrition
{
    public int Id                           { get; set; }
    public int RecipeId                     { get; set; }
    public Recipe Recipe                    { get; set; } = null!;
    public NutrientCategory NutrientCategory { get; set; }
    public decimal AmountPer100g            { get; set; }
    public string Unit                      { get; set; } = "g";
    public decimal DailyValuePercent        { get; set; }
}

// ═══════════════════════════════════════════════════════════════
// COSTS & RANKING
// ═══════════════════════════════════════════════════════════════
public class IngredientCost
{
    public int Id                           { get; set; }
    public int RecipeId                     { get; set; }
    public Recipe Recipe                    { get; set; } = null!;
    public string CountryCode               { get; set; } = string.Empty;
    public string CurrencyCode              { get; set; } = string.Empty;
    public string CurrencySymbol            { get; set; } = string.Empty;
    public decimal TotalCost                { get; set; }
    public decimal CostPerServing           { get; set; }
    public string BudgetTier               { get; set; } = "Moderate";
    public string? SupermarketSource        { get; set; }
    public string? IngredientsBreakdownJson { get; set; }
    public DateTime LastUpdated             { get; set; } = DateTime.UtcNow;
}

public class DishRanking
{
    public int Id               { get; set; }
    public int RecipeId         { get; set; }
    public Recipe Recipe        { get; set; } = null!;
    public RankingScope Scope   { get; set; }
    public string? ScopeValue   { get; set; }
    public int RankPosition     { get; set; }
    public double Score         { get; set; }
    public int WeeklyTrend      { get; set; }
    public int MonthlyTrend     { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════
// SOCIAL
// ═══════════════════════════════════════════════════════════════
public class Comment
{
    public int Id                       { get; set; }
    public int? RecipeId                { get; set; }
    public Recipe? Recipe               { get; set; }
    public string UserId                { get; set; } = string.Empty;
    public ApplicationUser User         { get; set; } = null!;
    public string Body                  { get; set; } = string.Empty;
    public string? ImagesJson           { get; set; }
    public string? GifUrl               { get; set; }
    public int LikeCount                { get; set; }
    public int? ParentCommentId         { get; set; }
    public Comment? ParentComment       { get; set; }
    public bool IsEdited                { get; set; }
    public DateTime? EditedAt           { get; set; }
    public bool IsFlagged               { get; set; }
    public bool IsDeleted               { get; set; }
    public DateTime CreatedAt           { get; set; } = DateTime.UtcNow;
    public ICollection<Comment>         Replies     { get; set; } = [];
    public ICollection<CommentReaction> Reactions   { get; set; } = [];
}

public class CommentReaction
{
    public int Id                   { get; set; }
    public int CommentId            { get; set; }
    public Comment Comment          { get; set; } = null!;
    public string UserId            { get; set; } = string.Empty;
    public ReactionType ReactionType { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
}

public class RecipeLike
{
    public int Id                   { get; set; }
    public int RecipeId             { get; set; }
    public Recipe Recipe            { get; set; } = null!;
    public string UserId            { get; set; } = string.Empty;
    public ApplicationUser User     { get; set; } = null!;
    public ReactionType ReactionType { get; set; } = ReactionType.Love;
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
}

public class UserFollow
{
    public int Id                   { get; set; }
    public string FollowerId        { get; set; } = string.Empty;
    public ApplicationUser Follower { get; set; } = null!;
    public string FolloweeId        { get; set; } = string.Empty;
    public ApplicationUser Followee { get; set; } = null!;
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
}

public class UserBlock
{
    public int Id                   { get; set; }
    public string BlockerId         { get; set; } = string.Empty;
    public ApplicationUser Blocker  { get; set; } = null!;
    public string BlockedId         { get; set; } = string.Empty;
    public ApplicationUser Blocked  { get; set; } = null!;
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
}

public class SavedCollection
{
    public int Id                   { get; set; }
    public string UserId            { get; set; } = string.Empty;
    public ApplicationUser User     { get; set; } = null!;
    public string Name              { get; set; } = string.Empty;
    public string? Description      { get; set; }
    public string? CoverImageUrl    { get; set; }
    public bool IsPublic            { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public ICollection<CollectionItem> Items { get; set; } = [];
}

public class CollectionItem
{
    public int Id                   { get; set; }
    public int CollectionId         { get; set; }
    public SavedCollection Collection { get; set; } = null!;
    public int RecipeId             { get; set; }
    public Recipe Recipe            { get; set; } = null!;
    public string? Note             { get; set; }
    public DateTime AddedAt         { get; set; } = DateTime.UtcNow;
}

public class CookLog
{
    public int Id                   { get; set; }
    public string UserId            { get; set; } = string.Empty;
    public ApplicationUser User     { get; set; } = null!;
    public int RecipeId             { get; set; }
    public Recipe Recipe            { get; set; } = null!;
    public string? Note             { get; set; }
    public string? PhotoUrl         { get; set; }
    public int Rating               { get; set; }
    public int ServingsCooked       { get; set; } = 4;
    public bool IsPublic            { get; set; } = true;
    public double? AIPhotoScore     { get; set; }
    public string? AIPhotoFeedback  { get; set; }
    public DateTime CookedAt        { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════
// CHAT
// ═══════════════════════════════════════════════════════════════
public class ChatRoom
{
    public int Id                   { get; set; }
    public string Name              { get; set; } = string.Empty;
    public string? Description      { get; set; }
    public string? CoverImageUrl    { get; set; }
    public string? CultureTag       { get; set; }
    public bool IsDirectMessage     { get; set; }
    public bool IsLiveSession       { get; set; }
    public string? HostUserId       { get; set; }
    public int MemberCount          { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public ICollection<ChatMessage> Messages { get; set; } = [];
}

public class ChatMessage
{
    public int Id                   { get; set; }
    public int RoomId               { get; set; }
    public ChatRoom Room            { get; set; } = null!;
    public string SenderId          { get; set; } = string.Empty;
    public ApplicationUser Sender   { get; set; } = null!;
    public string Body              { get; set; } = string.Empty;
    public string? ImageUrl         { get; set; }
    public string? VoiceNoteUrl     { get; set; }
    public int? SharedRecipeId      { get; set; }
    public bool IsEdited            { get; set; }
    public bool IsDeleted           { get; set; }
    public bool IsRead              { get; set; }
    public DateTime SentAt          { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════
// MEAL PLANNER
// ═══════════════════════════════════════════════════════════════
public class MealPlan
{
    public int Id                   { get; set; }
    public string UserId            { get; set; } = string.Empty;
    public ApplicationUser User     { get; set; } = null!;
    public string Name              { get; set; } = "My Meal Plan";
    public DateTime WeekStartDate   { get; set; }
    public bool IsAIGenerated       { get; set; }
    public string? CuisineTheme     { get; set; }
    public HealthGoal? HealthGoal   { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public ICollection<MealPlanItem> Items { get; set; } = [];
    public ShoppingList? ShoppingList     { get; set; }
}

public class MealPlanItem
{
    public int Id                   { get; set; }
    public int MealPlanId           { get; set; }
    public MealPlan MealPlan        { get; set; } = null!;
    public int RecipeId             { get; set; }
    public Recipe Recipe            { get; set; } = null!;
    public int DayOfWeek            { get; set; }
    public MealType MealType        { get; set; }
    public int Servings             { get; set; } = 4;
    public string? Note             { get; set; }
}

public class ShoppingList
{
    public int Id                   { get; set; }
    public int MealPlanId           { get; set; }
    public MealPlan MealPlan        { get; set; } = null!;
    public DateTime GeneratedAt     { get; set; } = DateTime.UtcNow;
    public ICollection<ShoppingListItem> Items { get; set; } = [];
}

public class ShoppingListItem
{
    public int Id                   { get; set; }
    public int ShoppingListId       { get; set; }
    public ShoppingList ShoppingList { get; set; } = null!;
    public string IngredientName    { get; set; } = string.Empty;
    public decimal Quantity         { get; set; }
    public string Unit              { get; set; } = string.Empty;
    public string? StoreSection     { get; set; }
    public bool IsPurchased         { get; set; }
    public decimal? EstimatedCost   { get; set; }
}

// ═══════════════════════════════════════════════════════════════
// SUGGESTIONS & QUIZ
// ═══════════════════════════════════════════════════════════════
public class RecipeSuggestion
{
    public int Id                           { get; set; }
    public string SubmittedByUserId         { get; set; } = string.Empty;
    public ApplicationUser SubmittedByUser  { get; set; } = null!;
    public string Title                     { get; set; } = string.Empty;
    public string Description               { get; set; } = string.Empty;
    public string? CountryOfOrigin          { get; set; }
    public string? CultureTag               { get; set; }
    public string IngredientsJson           { get; set; } = "[]";
    public string StepsJson                 { get; set; } = "[]";
    public string? CoverImageUrl            { get; set; }
    public string? YouTubeVideoId           { get; set; }
    public SuggestionStatus Status          { get; set; } = SuggestionStatus.Pending;
    public string? ReviewNote               { get; set; }
    public string? ReviewedByAdminId        { get; set; }
    public double? AIQualityScore           { get; set; }
    public DateTime SubmittedAt             { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt             { get; set; }
    public int? PublishedRecipeId           { get; set; }
}

public class QuizQuestion
{
    public int Id                           { get; set; }
    public QuizQuestionType QuestionType    { get; set; }
    public string QuestionText              { get; set; } = string.Empty;
    public string? ImageUrl                 { get; set; }
    public string? VideoUrl                 { get; set; }
    public string AnswersJson               { get; set; } = "[]";
    public string CorrectAnswer             { get; set; } = string.Empty;
    public string? Explanation              { get; set; }
    public DifficultyLevel Difficulty       { get; set; }
    public ChefLevel MinLevel               { get; set; }
    public string? CultureTag               { get; set; }
    public string? CountryCode              { get; set; }
    public NutrientCategory? NutrientCategory { get; set; }
    public bool IsActive                    { get; set; } = true;
    public DateTime CreatedAt               { get; set; } = DateTime.UtcNow;
}

public class QuizAttempt
{
    public int Id                           { get; set; }
    public string UserId                    { get; set; } = string.Empty;
    public ApplicationUser User             { get; set; } = null!;
    public ChefLevel TargetLevel            { get; set; }
    public int TotalQuestions               { get; set; }
    public int CorrectAnswers               { get; set; }
    public double ScorePercent              { get; set; }
    public bool IsPassed                    { get; set; }
    public bool LeveledUp                   { get; set; }
    public DateTime AttemptedAt             { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptAllowedAt   { get; set; }
    public string? QuestionsJson            { get; set; }
    public string? AnswersJson              { get; set; }
}

// ═══════════════════════════════════════════════════════════════
// BADGES & NOTIFICATIONS
// ═══════════════════════════════════════════════════════════════
public class Badge
{
    public int Id                   { get; set; }
    public string Name              { get; set; } = string.Empty;
    public string Description       { get; set; } = string.Empty;
    public string IconUrl           { get; set; } = string.Empty;
    public string Category          { get; set; } = string.Empty;
    public string TriggerCondition  { get; set; } = string.Empty;
    public ICollection<UserBadge> UserBadges { get; set; } = [];
}

public class UserBadge
{
    public int Id                   { get; set; }
    public string UserId            { get; set; } = string.Empty;
    public ApplicationUser User     { get; set; } = null!;
    public int BadgeId              { get; set; }
    public Badge Badge              { get; set; } = null!;
    public DateTime EarnedAt        { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int Id                   { get; set; }
    public string UserId            { get; set; } = string.Empty;
    public ApplicationUser User     { get; set; } = null!;
    public NotificationType Type    { get; set; }
    public string Title             { get; set; } = string.Empty;
    public string Body              { get; set; } = string.Empty;
    public string? ActionUrl        { get; set; }
    public string? ImageUrl         { get; set; }
    public bool IsRead              { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt         { get; set; }
}

// ═══════════════════════════════════════════════════════════════
// SCRAPER
// ═══════════════════════════════════════════════════════════════
public class ScrapeJob
{
    public int Id                   { get; set; }
    public string Source            { get; set; } = string.Empty;
    public string Url               { get; set; } = string.Empty;
    public string Platform          { get; set; } = string.Empty;
    public ScrapeStatus Status      { get; set; } = ScrapeStatus.Pending;
    public int RecipesFound         { get; set; }
    public int RecipesAdded         { get; set; }
    public string? ErrorMessage     { get; set; }
    public DateTime LastRunAt       { get; set; } = DateTime.UtcNow;
    public DateTime NextRunAt       { get; set; } = DateTime.UtcNow;
    public bool IsActive            { get; set; } = true;
}

public class ScrapedRaw
{
    public int Id                   { get; set; }
    public string RawJson           { get; set; } = string.Empty;
    public string SourceUrl         { get; set; } = string.Empty;
    public string Platform          { get; set; } = string.Empty;
    public string ContentHash       { get; set; } = string.Empty;
    public bool IsDuplicate         { get; set; }
    public DateTime ParsedAt        { get; set; } = DateTime.UtcNow;
    public int? MappedToRecipeId    { get; set; }
}

// ═══════════════════════════════════════════════════════════════
// JOKES
// ═══════════════════════════════════════════════════════════════
public class CookingJoke
{
    public int Id                   { get; set; }
    public string Body              { get; set; } = string.Empty;
    public string? Category         { get; set; }
    public string? FoodTag          { get; set; }
    public string Source            { get; set; } = "AI";
    public string? SourceUrl        { get; set; }
    public int LikeCount            { get; set; }
    public bool IsApproved          { get; set; } = true;
    public bool IsUserSubmitted     { get; set; }
    public string? SubmittedByUserId { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════
// HEALTH
// ═══════════════════════════════════════════════════════════════
public class NutrientFood
{
    public int Id                           { get; set; }
    public string FoodName                  { get; set; } = string.Empty;
    public NutrientCategory NutrientCategory { get; set; }
    public decimal AmountPer100g            { get; set; }
    public string Unit                      { get; set; } = "mg";
    public string? CulturalVariant          { get; set; }
    public string? RegionAvailability       { get; set; }
    public string? ImageUrl                 { get; set; }
    public int SortRank                     { get; set; }
}

public class AllergyGuide
{
    public int Id                       { get; set; }
    public AllergenType AllergenType    { get; set; }
    public string MildSymptoms          { get; set; } = string.Empty;
    public string SevereSymptoms        { get; set; } = string.Empty;
    public string FirstResponse         { get; set; } = string.Empty;
    public string HiddenSourcesJson     { get; set; } = "[]";
    public string SafeSubstitutesJson   { get; set; } = "[]";
    public AllergenRisk DefaultRisk     { get; set; }
}

public class FoodHealthBenefit
{
    public int Id                   { get; set; }
    public string Condition         { get; set; } = string.Empty;
    public string FoodRemedy        { get; set; } = string.Empty;
    public string HowToUse          { get; set; } = string.Empty;
    public string? FoodsToAvoid     { get; set; }
    public string? IconEmoji        { get; set; }
    public string? SourceReference  { get; set; }
}

public class FoodTimingGuide
{
    public int Id                           { get; set; }
    public MealTimingType TimingType        { get; set; }
    public string TimeRange                 { get; set; } = string.Empty;
    public string BestFoodsJson             { get; set; } = "[]";
    public string FoodsToAvoidJson          { get; set; } = "[]";
    public string Reason                    { get; set; } = string.Empty;
    public string? WorkoutType              { get; set; }
}

// ═══════════════════════════════════════════════════════════════
// EQUIPMENT & INVENTORY
// ═══════════════════════════════════════════════════════════════
public class CookingEquipment
{
    public int Id                   { get; set; }
    public string Name              { get; set; } = string.Empty;
    public string Category          { get; set; } = string.Empty; // e.g., Cutlery, Cookware, Appliance
    public string? Description      { get; set; }
    public string? ImageUrl         { get; set; }
    public string? CleaningSteps    { get; set; }
    public string? MaintenanceTips  { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════
// SPECIALIZED DIET PLANS
// ═══════════════════════════════════════════════════════════════
public class DietPlan
{
    public int Id                   { get; set; }
    public string PlanName          { get; set; } = string.Empty; // e.g., Vegan, Keto, Diabetic
    public string Description       { get; set; } = string.Empty;
    public string MealsJson         { get; set; } = "[]";
    public int TargetCalories       { get; set; }
    public string? MacrosJson       { get; set; }
    public string? HealthBenefits   { get; set; }
    public bool IsGlobal            { get; set; } = true;
    public string? Region           { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
}
