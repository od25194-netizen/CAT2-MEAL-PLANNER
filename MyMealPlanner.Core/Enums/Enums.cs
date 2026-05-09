namespace MyMealPlanner.Core.Enums;

public enum CookingEnvironment  { Home, Restaurant, Hotel, StreetFood, Outdoor, Camping }
public enum MealType            { Breakfast, Brunch, Lunch, Dinner, Snack, Dessert, Drink, Starter }
public enum DifficultyLevel     { Beginner, Easy, Intermediate, Advanced, Professional }
public enum RecipeSource        { Scraped, UserSubmitted, AIGenerated, PlatformCurated, ChefContributed }
public enum SuggestionStatus    { Pending, UnderReview, Approved, Rejected, Published }
public enum DiscoveryScope      { Local, Country, Continent, Global }
public enum RankingScope        { Global, Continent, Country, City, Weekly, Monthly }
public enum HealthGoal          { LoseWeight, BuildMuscle, EatHealthier, ManageDiabetes, HeartHealth, ImmunityBoost, EnergyBoost, Explore }
public enum AgeBracket          { Infants, Children, Teenagers, YoungAdults, Adults, Seniors, Elderly }
public enum PetType             { Dog, Cat, Bird, Rabbit, Fish, Hamster, GuineaPig, Reptile, Other }
public enum ReactionType        { Love, Wow, Funny, Impressive, Fire, Tasty, WantThis }
public enum BloodType           { APositive, ANegative, BPositive, BNegative, ABPositive, ABNegative, OPositive, ONegative, Unknown }
public enum ScrapeStatus        { Pending, Running, Completed, Failed, Skipped }
public enum Season              { Spring, Summer, Autumn, Winter, AllYear }
public enum WorkoutType         { Cardio, WeightTraining, Yoga, HIIT, Swimming, Cycling, Walking }
public enum MealTimingType      { PreWorkout, PostWorkout, WakeUp, Breakfast, MidMorning, Lunch, AfternoonSnack, Dinner, BeforeBed }
public enum AllergenRisk        { Low, Moderate, High, Critical }
public enum QuizQuestionType    { MultipleChoice, ImageIdentification, IngredientMatch, TrueOrFalse, FillInBlank, OrderTheSteps, NutrientIdentifier, VideoClip }
public enum ContentType         { Recipe, Comment, Post, CookLog, JokeSubmission }

public enum NutrientCategory
{
    VitaminA, VitaminB, VitaminC, VitaminD, VitaminE, VitaminK,
    Protein, Carbohydrates, HealthyFats, Fibre,
    Calcium, Iron, Zinc, Magnesium, Potassium, Sodium,
    Omega3, Antioxidants, Probiotics, Folate
}

public enum AllergenType
{
    Peanuts, TreeNuts, Milk, Eggs, Wheat, Gluten, Soy,
    Fish, Shellfish, Sesame, Mustard, Celery, Lupin, Molluscs, Sulphites
}

public enum ChefLevel
{
    Level1_KitchenNewcomer  = 1,
    Level2_CuriousCook      = 2,
    Level3_HomeExplorer     = 3,
    Level4_ConfidentCook    = 4,
    Level5_SkilledChef      = 5,
    Level6_ExpertCulinarian = 6,
    Level7_MasterChef       = 7,
    Level8_GrandChef        = 8
}

public enum VerificationTick   { None, White, Green, Gold }

public enum NotificationType
{
    DailyMealSuggestion, WeeklyMealPlanReady, TrendingInYourCountry,
    NewRecipeFromFollowed, UnseenContentAlert, QuizReadyToTake,
    MealTimeReminder, HealthTipOfTheDay, SeasonalFoodAlert, HydrationReminder,
    SomeoneCommentedYours, RecipeSuggestionApproved, LevelUpAlert,
    BadgeEarned, FollowerMilestone, BasedOnYourHistory,
    IngredientDealNearby, ExpertAnsweredYou, CookingClassReminder,
    NewJokeOfTheDay, ChefGoesLive, ChallengeStarting
}
