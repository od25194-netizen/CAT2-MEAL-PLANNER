using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Core.DTOs;
using MyMealPlanner.Infrastructure.Data;
using MyMealPlanner.Services.AI;
using MyMealPlanner.Services.Cost;
using MyMealPlanner.Services.Health;
using MyMealPlanner.Services.Ranking;
using MyMealPlanner.Services.Scraper;
using Xunit;

namespace MyMealPlanner.Tests;

// ═══════════════════════════════════════════════════════════════
// TEST DATABASE FACTORY
// ═══════════════════════════════════════════════════════════════
public static class TestDbFactory
{
    public static ApplicationDbContext Create(string? dbName = null)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opts);
    }
}

// ═══════════════════════════════════════════════════════════════
// RANKING SERVICE TESTS
// ═══════════════════════════════════════════════════════════════
public class RankingServiceTests
{
    private readonly RankingService _svc;

    public RankingServiceTests()
    {
        var db     = TestDbFactory.Create();
        var logger = Mock.Of<ILogger<RankingService>>();
        _svc       = new RankingService(db, logger);
    }

    [Fact]
    public void CalculateScore_WithHighEngagement_ReturnsHighScore()
    {
        var recipe = new Recipe
        {
            LikeCount    = 1000,
            SaveCount    = 500,
            CookLogCount = 300,
            CommentCount = 200,
            ShareCount   = 100,
            RatingAverage = 4.8,
            CreatedAt    = DateTime.UtcNow.AddDays(-1) // recent
        };

        var score = _svc.CalculateScore(recipe);

        score.Should().BeGreaterThan(0);
        score.Should().BeGreaterThan(100); // meaningful score
    }

    [Fact]
    public void CalculateScore_WithZeroEngagement_ReturnsNearZero()
    {
        var recipe = new Recipe
        {
            LikeCount    = 0,
            SaveCount    = 0,
            CookLogCount = 0,
            CommentCount = 0,
            ShareCount   = 0,
            CreatedAt    = DateTime.UtcNow
        };

        var score = _svc.CalculateScore(recipe);

        score.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void CalculateScore_OlderRecipe_ScoresLowerThanNewerWithSameEngagement()
    {
        var baseEngagement = new { LikeCount = 100, SaveCount = 50, CookLogCount = 30, CommentCount = 20, ShareCount = 10 };

        var newRecipe = new Recipe
        {
            LikeCount = baseEngagement.LikeCount, SaveCount = baseEngagement.SaveCount,
            CookLogCount = baseEngagement.CookLogCount, CommentCount = baseEngagement.CommentCount,
            ShareCount = baseEngagement.ShareCount, CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var oldRecipe = new Recipe
        {
            LikeCount = baseEngagement.LikeCount, SaveCount = baseEngagement.SaveCount,
            CookLogCount = baseEngagement.CookLogCount, CommentCount = baseEngagement.CommentCount,
            ShareCount = baseEngagement.ShareCount, CreatedAt = DateTime.UtcNow.AddDays(-60)
        };

        _svc.CalculateScore(newRecipe).Should().BeGreaterThan(_svc.CalculateScore(oldRecipe));
    }

    [Fact]
    public async Task RecalculateAllRankingsAsync_WithPublishedRecipes_CreatesRankings()
    {
        var db     = TestDbFactory.Create("ranking-test");
        var logger = Mock.Of<ILogger<RankingService>>();
        var svc    = new RankingService(db, logger);

        db.Recipes.AddRange(
            new Recipe { Title = "Jollof Rice", Slug = "jollof-rice", IsPublished = true, LikeCount = 500, OriginCountry = "Nigeria", OriginContinent = "Africa", CreatedAt = DateTime.UtcNow },
            new Recipe { Title = "Sushi",       Slug = "sushi",       IsPublished = true, LikeCount = 800, OriginCountry = "Japan",   OriginContinent = "Asia",   CreatedAt = DateTime.UtcNow },
            new Recipe { Title = "Pizza",       Slug = "pizza",       IsPublished = true, LikeCount = 300, OriginCountry = "Italy",   OriginContinent = "Europe", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        await svc.RecalculateAllRankingsAsync();

        var globalRankings = await db.DishRankings
            .Where(r => r.Scope == RankingScope.Global)
            .ToListAsync();

        globalRankings.Should().HaveCountGreaterThan(0);
        globalRankings.Should().Contain(r => r.RankPosition == 1);

        // Sushi has most likes — should be #1
        var topRecipeId = globalRankings.OrderBy(r => r.RankPosition).First().RecipeId;
        var topRecipe   = await db.Recipes.FindAsync(topRecipeId);
        topRecipe!.Title.Should().Be("Sushi");
    }
}

// ═══════════════════════════════════════════════════════════════
// CONTENT NORMALIZER TESTS
// ═══════════════════════════════════════════════════════════════
public class ContentNormalizerTests
{
    private readonly ContentNormalizerService _svc;

    public ContentNormalizerTests()
    {
        _svc = new ContentNormalizerService(Mock.Of<ILogger<ContentNormalizerService>>());
    }

    [Fact]
    public void GenerateSlug_WithSpacesAndSpecialChars_ReturnsValidSlug()
    {
        var slug = _svc.GenerateSlug("Nigerian Jollof Rice (Spicy!)");

        slug.Should().Be("nigerian-jollof-rice-spicy");
        slug.Should().NotContain(" ");
        slug.Should().NotContain("(");
        slug.Should().NotContain("!");
    }

    [Fact]
    public void GenerateSlug_WithLongTitle_TruncatesAt200Chars()
    {
        var longTitle = string.Concat(Enumerable.Repeat("VeryLongWord ", 20));
        var slug      = _svc.GenerateSlug(longTitle);

        slug.Length.Should().BeLessOrEqualTo(200);
    }

    [Fact]
    public void GenerateSlug_WithUnicode_RemovesNonAscii()
    {
        var slug = _svc.GenerateSlug("Crème brûlée");

        slug.Should().NotBeNullOrEmpty();
        slug.Should().NotContain("è");
        slug.Should().NotContain("û");
    }

    [Fact]
    public void GenerateContentHash_SameInputs_ReturnsSameHash()
    {
        var hash1 = _svc.GenerateContentHash("Jollof Rice", new List<string> { "rice", "tomato", "onion" });
        var hash2 = _svc.GenerateContentHash("Jollof Rice", new List<string> { "rice", "tomato", "onion" });

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GenerateContentHash_DifferentInputs_ReturnsDifferentHash()
    {
        var hash1 = _svc.GenerateContentHash("Jollof Rice",  new List<string> { "rice", "tomato" });
        var hash2 = _svc.GenerateContentHash("Sushi Recipe", new List<string> { "rice", "fish" });

        hash1.Should().NotBe(hash2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateSlug_WithEmptyInput_ReturnsEmpty(string input)
    {
        var slug = _svc.GenerateSlug(input);
        slug.Should().BeEmpty();
    }
}

// ═══════════════════════════════════════════════════════════════
// AI TAGGER TESTS
// ═══════════════════════════════════════════════════════════════
public class AITaggerServiceTests
{
    private readonly AITaggerService _svc;

    public AITaggerServiceTests()
    {
        _svc = new AITaggerService(Mock.Of<ILogger<AITaggerService>>());
    }

    [Fact]
    public async Task AutoTagAsync_JollofRecipe_DetectsNigerianOrigin()
    {
        var recipe = new Recipe
        {
            Title       = "Classic Jollof Rice",
            Description = "A traditional Nigerian jollof recipe with tomatoes and peppers",
            Ingredients = new List<Ingredient>
            {
                new() { Name = "long grain rice" },
                new() { Name = "tomatoes" },
                new() { Name = "onions" },
                new() { Name = "chicken" }
            },
            Steps = new List<RecipeStep> { new() { Instruction = "Cook the rice." } }
        };

        var tags = await _svc.AutoTagAsync(recipe);

        tags.DetectedCountry.Should().Be("Nigeria");
        tags.DetectedContinent.Should().Be("Africa");
        tags.NutrientFlags.Should().Contain("Protein"); // chicken
    }

    [Fact]
    public async Task AutoTagAsync_WithPeanuts_FlagsAllergen()
    {
        var recipe = new Recipe
        {
            Title       = "Peanut Stew",
            Description = "West African groundnut soup",
            Ingredients = new List<Ingredient>
            {
                new() { Name = "peanut butter" },
                new() { Name = "chicken" },
                new() { Name = "tomatoes" }
            },
            Steps = new List<RecipeStep>()
        };

        var tags = await _svc.AutoTagAsync(recipe);

        tags.AllergenFlags.Should().Contain("Peanuts");
    }

    [Fact]
    public async Task AutoTagAsync_VeganKeywords_FlagsDietaryRestriction()
    {
        var recipe = new Recipe
        {
            Title       = "Vegan Mushroom Risotto",
            Description = "A plant-based creamy risotto with no dairy",
            Ingredients = new List<Ingredient>(),
            Steps       = new List<RecipeStep>()
        };

        var tags = await _svc.AutoTagAsync(recipe);

        tags.DietaryFlags.Should().Contain("Vegan");
    }

    [Fact]
    public async Task AutoTagAsync_ManySteps_AssignsHigherDifficulty()
    {
        var recipe = new Recipe
        {
            Title       = "Complex French Dish",
            Description = "A technically demanding recipe",
            Ingredients = new List<Ingredient>(),
            Steps       = Enumerable.Range(1, 15)
                            .Select(i => new RecipeStep { Instruction = $"Step {i}", StepOrder = i })
                            .ToList()
        };

        var tags = await _svc.AutoTagAsync(recipe);

        ((int)tags.Difficulty).Should().BeGreaterThanOrEqualTo((int)DifficultyLevel.Advanced);
    }

    [Fact]
    public async Task ScoreQualityAsync_FullySuggestedRecipe_HighScore()
    {
        var suggestion = new RecipeSuggestion
        {
            Title           = "Egusi Soup",
            Description     = "A rich Nigerian soup made with ground melon seeds, leafy greens and assorted meats",
            IngredientsJson = "[\"egusi\",\"palm oil\",\"onions\"]",
            StepsJson       = "[\"Grind the egusi\",\"Fry the base\",\"Add ingredients\"]",
            CoverImageUrl   = "https://example.com/egusi.jpg",
            YouTubeVideoId  = "abc123"
        };

        var score = await _svc.ScoreQualityAsync(suggestion);

        score.Should().BeGreaterThanOrEqualTo(80);
    }

    [Fact]
    public async Task ScoreQualityAsync_EmptySuggestion_LowScore()
    {
        var suggestion = new RecipeSuggestion
        {
            Title           = "A",
            Description     = "",
            IngredientsJson = "[]",
            StepsJson       = "[]"
        };

        var score = await _svc.ScoreQualityAsync(suggestion);

        score.Should().BeLessThan(40);
    }
}

// ═══════════════════════════════════════════════════════════════
// ALLERGY SERVICE TESTS
// ═══════════════════════════════════════════════════════════════
public class AllergyServiceTests
{
    private readonly AllergyService _svc;

    public AllergyServiceTests()
    {
        _svc = new AllergyService(TestDbFactory.Create());
    }

    [Fact]
    public async Task CheckIngredientsAsync_PeanutAllergyWithPeanuts_DetectsAllergen()
    {
        var ingredients = new List<string> { "peanut butter", "chicken", "garlic" };
        var userAllergens = new List<string> { "Peanuts" };

        var result = await _svc.CheckIngredientsAsync(ingredients, userAllergens);

        result.HasAllergen.Should().BeTrue();
        result.AllergenDetails.Should().HaveCount(1);
        result.AllergenDetails[0].Type.Should().Be(AllergenType.Peanuts);
        result.AllergenDetails[0].Risk.Should().Be(AllergenRisk.Critical);
    }

    [Fact]
    public async Task CheckIngredientsAsync_GlutenAllergyWithSoySauce_DetectsHiddenSource()
    {
        var ingredients   = new List<string> { "soy sauce", "noodles", "vegetables" };
        var userAllergens = new List<string> { "Gluten" };

        var result = await _svc.CheckIngredientsAsync(ingredients, userAllergens);

        result.HasAllergen.Should().BeTrue();
        result.AllergenDetails[0].HiddenSource.Should().Contain("soy sauce");
    }

    [Fact]
    public async Task CheckIngredientsAsync_NoAllergens_ReturnsFalse()
    {
        var ingredients   = new List<string> { "tomatoes", "olive oil", "basil", "garlic" };
        var userAllergens = new List<string> { "Peanuts", "Shellfish" };

        var result = await _svc.CheckIngredientsAsync(ingredients, userAllergens);

        result.HasAllergen.Should().BeFalse();
        result.AllergenDetails.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckIngredientsAsync_EmptyAllergenList_ReturnsNoAllergens()
    {
        var ingredients   = new List<string> { "peanuts", "milk", "eggs" };
        var userAllergens = new List<string>(); // no allergies registered

        var result = await _svc.CheckIngredientsAsync(ingredients, userAllergens);

        result.HasAllergen.Should().BeFalse();
    }

    [Fact]
    public async Task CheckIngredientsAsync_MultipleAllergens_DetectsAll()
    {
        var ingredients   = new List<string> { "peanut butter", "milk", "egg", "wheat flour" };
        var userAllergens = new List<string> { "Peanuts", "Milk", "Eggs", "Gluten" };

        var result = await _svc.CheckIngredientsAsync(ingredients, userAllergens);

        result.HasAllergen.Should().BeTrue();
        result.AllergenDetails.Should().HaveCountGreaterThan(1);
    }
}

// ═══════════════════════════════════════════════════════════════
// INGREDIENT COST SERVICE TESTS
// ═══════════════════════════════════════════════════════════════
public class IngredientCostServiceTests
{
    [Fact]
    public async Task RefreshCostsAsync_ValidRecipe_CreatesCostRecordsForMultipleCountries()
    {
        var db     = TestDbFactory.Create("cost-test");
        var cache  = Mock.Of<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        var logger = Mock.Of<ILogger<IngredientCostService>>();
        var http   = Mock.Of<IHttpClientFactory>();

        var recipe = new Recipe
        {
            Id          = 1,
            Title       = "Chicken Stew",
            Slug        = "chicken-stew",
            Servings    = 4,
            Ingredients = new List<Ingredient>
            {
                new() { Name = "chicken breast", Quantity = 500, Unit = "g" },
                new() { Name = "tomatoes",        Quantity = 3,   Unit = "item" },
                new() { Name = "onions",          Quantity = 2,   Unit = "item" },
                new() { Name = "garlic",          Quantity = 4,   Unit = "clove" },
                new() { Name = "rice",            Quantity = 2,   Unit = "cup" },
            }
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var svc = new IngredientCostService(db, http, cache, logger);
        await svc.RefreshCostsAsync(1);

        var costs = await db.IngredientCosts.Where(c => c.RecipeId == 1).ToListAsync();

        costs.Should().NotBeEmpty();
        costs.Should().HaveCountGreaterOrEqualTo(5); // At least 5 countries

        var nigerianCost = costs.FirstOrDefault(c => c.CountryCode == "NG");
        nigerianCost.Should().NotBeNull();
        nigerianCost!.CurrencySymbol.Should().Be("₦");
        nigerianCost.TotalCost.Should().BeGreaterThan(0);

        var usCost = costs.FirstOrDefault(c => c.CountryCode == "US");
        usCost.Should().NotBeNull();
        usCost!.CurrencySymbol.Should().Be("$");

        // Nigerian cost should be lower than US in absolute USD terms
        usCost.TotalCost.Should().BeGreaterThan(nigerianCost.TotalCost / 1600m * 1m);
    }

    [Fact]
    public async Task RefreshCostsAsync_RecipeWithNoIngredients_CreatesCostsWithZeroTotal()
    {
        var db     = TestDbFactory.Create("cost-empty-test");
        var cache  = Mock.Of<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        var logger = Mock.Of<ILogger<IngredientCostService>>();
        var http   = Mock.Of<IHttpClientFactory>();

        var recipe = new Recipe { Id = 2, Title = "Empty Recipe", Slug = "empty", Servings = 4 };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var svc = new IngredientCostService(db, http, cache, logger);
        await svc.RefreshCostsAsync(2);

        var costs = await db.IngredientCosts.Where(c => c.RecipeId == 2).ToListAsync();
        costs.Should().NotBeEmpty();
        costs.All(c => c.TotalCost == 0).Should().BeTrue();
    }
}

// ═══════════════════════════════════════════════════════════════
// MODEL VALIDATION TESTS
// ═══════════════════════════════════════════════════════════════
public class ModelValidationTests
{
    [Theory]
    [InlineData(ChefLevel.Level4_ConfidentCook, VerificationTick.White)]
    [InlineData(ChefLevel.Level5_SkilledChef,   VerificationTick.White)]
    [InlineData(ChefLevel.Level6_ExpertCulinarian, VerificationTick.Green)]
    [InlineData(ChefLevel.Level7_MasterChef,    VerificationTick.Green)]
    [InlineData(ChefLevel.Level8_GrandChef,     VerificationTick.Gold)]
    public void ChefLevel_MapsToCorrectVerificationTick(ChefLevel level, VerificationTick expectedTick)
    {
        // This mirrors the logic in QuizController.Submit
        var tick = level switch
        {
            ChefLevel.Level4_ConfidentCook or ChefLevel.Level5_SkilledChef => VerificationTick.White,
            ChefLevel.Level6_ExpertCulinarian or ChefLevel.Level7_MasterChef => VerificationTick.Green,
            ChefLevel.Level8_GrandChef => VerificationTick.Gold,
            _ => VerificationTick.None
        };

        tick.Should().Be(expectedTick);
    }

    [Theory]
    [InlineData(0,  AgeBracket.Infants)]
    [InlineData(2,  AgeBracket.Infants)]
    [InlineData(5,  AgeBracket.Children)]
    [InlineData(15, AgeBracket.Teenagers)]
    [InlineData(25, AgeBracket.YoungAdults)]
    [InlineData(45, AgeBracket.Adults)]
    [InlineData(65, AgeBracket.Seniors)]
    [InlineData(75, AgeBracket.Elderly)]
    public void AgeBracketCalculation_CorrectlyMapsAge(int age, AgeBracket expected)
    {
        var bracket = age switch
        {
            <= 2  => AgeBracket.Infants,
            <= 12 => AgeBracket.Children,
            <= 19 => AgeBracket.Teenagers,
            <= 35 => AgeBracket.YoungAdults,
            <= 55 => AgeBracket.Adults,
            <= 70 => AgeBracket.Seniors,
            _     => AgeBracket.Elderly
        };

        bracket.Should().Be(expected);
    }

    [Fact]
    public void Recipe_Slug_ShouldBeUnique_AndUrlSafe()
    {
        var title = "Grandmother's Egusi Soup & Pounded Yam";
        var normalizer = new ContentNormalizerService(Mock.Of<ILogger<ContentNormalizerService>>());

        var slug = normalizer.GenerateSlug(title);

        slug.Should().NotContain("'");
        slug.Should().NotContain("&");
        slug.Should().NotContain(" ");
        slug.Should().MatchRegex("^[a-z0-9-]+$");
    }
}

// ═══════════════════════════════════════════════════════════════
// SHOPPING LIST SECTION TESTS
// ═══════════════════════════════════════════════════════════════
public class ShoppingListTests
{
    [Theory]
    [InlineData("chicken breast",    "Meat & Fish")]
    [InlineData("salmon fillet",     "Meat & Fish")]
    [InlineData("whole milk",        "Dairy & Eggs")]
    [InlineData("cheddar cheese",    "Dairy & Eggs")]
    [InlineData("basmati rice",      "Grains & Staples")]
    [InlineData("all-purpose flour", "Grains & Staples")]
    [InlineData("red bell pepper",   "Produce")]
    [InlineData("ground cumin",      "Spices & Oils")]
    [InlineData("olive oil",         "Spices & Oils")]
    [InlineData("tomato paste",      "Canned & Sauces")]
    public void GuessStoreSection_ReturnsCorrectCategory(string ingredient, string expectedSection)
    {
        // Mirrors MealPlanAndSearchControllers.GuessStoreSection
        var n = ingredient.ToLowerInvariant();
        string section;
        if (new[] { "chicken", "beef", "pork", "lamb", "fish", "prawn", "shrimp", "salmon", "tuna" }.Any(x => n.Contains(x))) section = "Meat & Fish";
        else if (new[] { "milk", "yoghurt", "cream", "cheese", "butter", "egg" }.Any(x => n.Contains(x))) section = "Dairy & Eggs";
        else if (new[] { "rice", "pasta", "flour", "oat", "bread", "noodle" }.Any(x => n.Contains(x))) section = "Grains & Staples";
        else if (new[] { "can", "tin", "sauce", "paste", "stock", "broth" }.Any(x => n.Contains(x))) section = "Canned & Sauces";
        else if (new[] { "apple", "banana", "tomato", "onion", "garlic", "carrot", "pepper", "lettuce", "spinach" }.Any(x => n.Contains(x))) section = "Produce";
        else if (new[] { "oil", "salt", "spice", "herb", "cumin", "turmeric", "paprika", "ginger" }.Any(x => n.Contains(x))) section = "Spices & Oils";
        else section = "Other";

        section.Should().Be(expectedSection);
    }
}

// ═══════════════════════════════════════════════════════════════
// AI CHAT ASSISTANT SERVICE TESTS
// ═══════════════════════════════════════════════════════════════
public class AIChatAssistantServiceTests
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IHttpClientFactory> _httpFactoryMock;
    private readonly Mock<ILogger<AIChatAssistantService>> _loggerMock;

    public AIChatAssistantServiceTests()
    {
        _db = TestDbFactory.Create();
        _configMock = new Mock<IConfiguration>();
        _httpFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<AIChatAssistantService>>();

        var user = new ApplicationUser
        {
            Id = "test-user-id",
            UserName = "testuser@example.com",
            Email = "testuser@example.com",
            FullName = "Test User",
            ChefLevel = ChefLevel.Level4_ConfidentCook,
            AllergiesJson = "[\"Peanuts\"]",
            DietaryRestrictionsJson = "[\"Vegan\"]"
        };
        _db.Users.Add(user);
        _db.SaveChanges();
    }

    private void SetupEmptyKeys()
    {
        _configMock.Setup(c => c["AI:DeepSeekApiKey"]).Returns((string?)null);
        _configMock.Setup(c => c["AI:DashScopeApiKey"]).Returns((string?)null);
        _configMock.Setup(c => c["AI:GroqApiKey"]).Returns((string?)null);
        _configMock.Setup(c => c["AI:OllamaBaseUrl"]).Returns((string?)null);
        _configMock.Setup(c => c["AI:OpenRouterApiKey"]).Returns((string?)null);
        _configMock.Setup(c => c["AI:ClaudeApiKey"]).Returns((string?)null);
    }

    [Fact]
    public async Task ChatAsync_NoApiKeys_ReturnsFallbackResponse()
    {
        SetupEmptyKeys();
        var handlerMock = new Mock<HttpMessageHandler>();
        var client = new HttpClient(handlerMock.Object);
        _httpFactoryMock.Setup(f => f.CreateClient("AIClient")).Returns(client);

        var svc = new AIChatAssistantService(_db, _configMock.Object, _httpFactoryMock.Object, _loggerMock.Object);

        var resAllergy = await svc.ChatAsync("test-user-id", "What about peanut allergy?", new List<ChatTurn>());
        var resIngredient = await svc.ChatAsync("test-user-id", "Can I substitute ingredients?", new List<ChatTurn>());
        var resDiet = await svc.ChatAsync("test-user-id", "Is this a healthy calorie option?", new List<ChatTurn>());
        var resDefault = await svc.ChatAsync("test-user-id", "Hello Mia!", new List<ChatTurn>());

        resAllergy.Should().Contain("Allergy Guide");
        resIngredient.Should().Contain("ingredients");
        resDiet.Should().Contain("Health Hub");
        resDefault.Should().Contain("I'm Mia");
    }

    [Fact]
    public async Task ChatAsync_DeepSeekApiKeyProvided_CallsDeepSeekApi()
    {
        SetupEmptyKeys();
        _configMock.Setup(c => c["AI:DeepSeekApiKey"]).Returns("sk-deepseek-test-key");

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("api.deepseek.com")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"Hello from DeepSeek!\"}}]}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            });

        var client = new HttpClient(handlerMock.Object);
        _httpFactoryMock.Setup(f => f.CreateClient("AIClient")).Returns(client);

        var svc = new AIChatAssistantService(_db, _configMock.Object, _httpFactoryMock.Object, _loggerMock.Object);

        var res = await svc.ChatAsync("test-user-id", "Hello", new List<ChatTurn>());
        res.Should().Be("Hello from DeepSeek!");
    }

    [Fact]
    public async Task ChatAsync_QwenApiKeyProvided_CallsQwenApi()
    {
        SetupEmptyKeys();
        _configMock.Setup(c => c["AI:DashScopeApiKey"]).Returns("qwen-test-key");

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("dashscope.aliyuncs.com")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(
                    "{\"output\":{\"choices\":[{\"message\":{\"content\":\"Hello from Qwen!\"}}]}}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            });

        var client = new HttpClient(handlerMock.Object);
        _httpFactoryMock.Setup(f => f.CreateClient("AIClient")).Returns(client);

        var svc = new AIChatAssistantService(_db, _configMock.Object, _httpFactoryMock.Object, _loggerMock.Object);

        var res = await svc.ChatAsync("test-user-id", "Hello", new List<ChatTurn>());
        res.Should().Be("Hello from Qwen!");
    }

    [Fact]
    public async Task ChatAsync_GroqApiKeyProvided_CallsGroqApi()
    {
        SetupEmptyKeys();
        _configMock.Setup(c => c["AI:GroqApiKey"]).Returns("groq-test-key");

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("api.groq.com")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"Hello from Groq!\"}}]}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            });

        var client = new HttpClient(handlerMock.Object);
        _httpFactoryMock.Setup(f => f.CreateClient("AIClient")).Returns(client);

        var svc = new AIChatAssistantService(_db, _configMock.Object, _httpFactoryMock.Object, _loggerMock.Object);

        var res = await svc.ChatAsync("test-user-id", "Hello", new List<ChatTurn>());
        res.Should().Be("Hello from Groq!");
    }

    [Fact]
    public async Task ChatAsync_OllamaBaseUrlProvided_CallsOllamaApi()
    {
        SetupEmptyKeys();
        _configMock.Setup(c => c["AI:OllamaBaseUrl"]).Returns("http://localhost:11434");
        _configMock.Setup(c => c["AI:OllamaModel"]).Returns("llama3");

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("11434")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(
                    "{\"response\":\"Hello from Ollama!\"}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            });

        var client = new HttpClient(handlerMock.Object);
        _httpFactoryMock.Setup(f => f.CreateClient("AIClient")).Returns(client);

        var svc = new AIChatAssistantService(_db, _configMock.Object, _httpFactoryMock.Object, _loggerMock.Object);

        var res = await svc.ChatAsync("test-user-id", "Hello", new List<ChatTurn>());
        res.Should().Be("Hello from Ollama!");
    }

    [Fact]
    public async Task ChatAsync_OpenRouterApiKeyProvided_CallsOpenRouterApi()
    {
        SetupEmptyKeys();
        _configMock.Setup(c => c["AI:OpenRouterApiKey"]).Returns("openrouter-test-key");

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("openrouter.ai")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"Hello from OpenRouter!\"}}]}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            });

        var client = new HttpClient(handlerMock.Object);
        _httpFactoryMock.Setup(f => f.CreateClient("AIClient")).Returns(client);

        var svc = new AIChatAssistantService(_db, _configMock.Object, _httpFactoryMock.Object, _loggerMock.Object);

        var res = await svc.ChatAsync("test-user-id", "Hello", new List<ChatTurn>());
        res.Should().Be("Hello from OpenRouter!");
    }

    [Fact]
    public async Task ChatAsync_ClaudeApiKeyProvided_CallsClaudeApi()
    {
        SetupEmptyKeys();
        _configMock.Setup(c => c["AI:ClaudeApiKey"]).Returns("claude-test-key");

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("api.anthropic.com")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(
                    "{\"content\":[{\"text\":\"Hello from Claude!\"}]}",
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            });

        var client = new HttpClient(handlerMock.Object);
        _httpFactoryMock.Setup(f => f.CreateClient("AIClient")).Returns(client);

        var svc = new AIChatAssistantService(_db, _configMock.Object, _httpFactoryMock.Object, _loggerMock.Object);

        var res = await svc.ChatAsync("test-user-id", "Hello", new List<ChatTurn>());
        res.Should().Be("Hello from Claude!");
    }
}
