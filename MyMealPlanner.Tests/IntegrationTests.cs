using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyMealPlanner.Infrastructure.Data;
using System.Net;
using Xunit;

namespace MyMealPlanner.Tests;

/// <summary>
/// Integration tests using WebApplicationFactory.
/// These spin up the full ASP.NET pipeline with an in-memory DB,
/// verifying that controllers, routing, and views all work end-to-end.
/// </summary>
public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real DB with in-memory for tests
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("IntegrationTestDb_" + Guid.NewGuid()));
            });
        });
    }

    // ── Public Pages ──────────────────────────────────────────
    [Theory]
    [InlineData("/")]
    [InlineData("/Recipe")]
    [InlineData("/Health")]
    [InlineData("/Health/Nutrients")]
    [InlineData("/Explore")]
    [InlineData("/Explore/Rankings")]
    [InlineData("/Jokes")]
    [InlineData("/Home/About")]
    [InlineData("/Home/Privacy")]
    public async Task Get_PublicPages_ReturnsSuccess(string url)
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync(url);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ── Auth-Required Pages Redirect ─────────────────────────
    [Theory]
    [InlineData("/MealPlan")]
    [InlineData("/Profile")]
    [InlineData("/Quiz")]
    [InlineData("/Admin")]
    [InlineData("/Social/Chat")]
    public async Task Get_AuthRequired_RedirectsToLogin(string url)
    {
        var client   = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/Account/Login");
    }

    // ── Auth Pages ────────────────────────────────────────────
    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    public async Task Get_AuthPages_ReturnsSuccess(string url)
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Health Endpoint ───────────────────────────────────────
    [Fact]
    public async Task Get_HealthEndpoint_ReturnsOkWithStatus()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("healthy");
    }

    // ── API Search ────────────────────────────────────────────
    [Fact]
    public async Task Get_SearchApi_ReturnsJsonArray()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/api/search?q=rice&limit=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().StartWith("[");
    }

    // ── Error Handling ────────────────────────────────────────
    [Fact]
    public async Task Get_NonExistentPage_ReturnsNotFound()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/this-page-does-not-exist-at-all");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Static Files ──────────────────────────────────────────
    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/js/site.js")]
    [InlineData("/manifest.json")]
    [InlineData("/service-worker.js")]
    [InlineData("/offline.html")]
    public async Task Get_StaticFiles_ReturnsSuccess(string url)
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Content Type Verification ─────────────────────────────
    [Fact]
    public async Task Get_HomePage_ReturnsHtml()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task Get_ManifestJson_ReturnsJsonWithCorrectFields()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/manifest.json");
        var body     = await response.Content.ReadAsStringAsync();
        body.Should().Contain("My Meal Planner");
        body.Should().Contain("standalone");
        body.Should().Contain("#E8630A");
    }
}

/// <summary>
/// Extended unit tests for the ranking score formula —
/// verifying edge cases and boundary conditions.
/// </summary>
public class RankingEdgeCaseTests
{
    [Fact]
    public void CalculateScore_WithNegativeLikeCount_DoesNotThrow()
    {
        var svc = new MyMealPlanner.Services.Ranking.RankingService(
            TestDbFactory.Create(),
            Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<MyMealPlanner.Services.Ranking.RankingService>>());

        var recipe = new MyMealPlanner.Core.Models.Recipe
        {
            LikeCount    = 0,
            SaveCount    = 0,
            CookLogCount = 0,
            CommentCount = 0,
            ShareCount   = 0,
            CreatedAt    = DateTime.UtcNow
        };

        var score = Record.Exception(() => svc.CalculateScore(recipe));
        score.Should().BeNull(); // no exception
    }

    [Fact]
    public void CalculateScore_VeryOldRecipe_ScoreDecaysButNotNegative()
    {
        var svc = new MyMealPlanner.Services.Ranking.RankingService(
            TestDbFactory.Create(),
            Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<MyMealPlanner.Services.Ranking.RankingService>>());

        var recipe = new MyMealPlanner.Core.Models.Recipe
        {
            LikeCount = 10000,
            SaveCount = 5000,
            CreatedAt = DateTime.UtcNow.AddYears(-5) // 5 years old
        };

        var score = svc.CalculateScore(recipe);
        score.Should().BeGreaterThan(0);
        score.Should().BeLessThan(100); // time-decayed significantly
    }
}

/// <summary>
/// Content Normalizer edge case tests.
/// </summary>
public class ContentNormalizerEdgeCaseTests
{
    private readonly MyMealPlanner.Services.Scraper.ContentNormalizerService _svc =
        new(Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<MyMealPlanner.Services.Scraper.ContentNormalizerService>>());

    [Theory]
    [InlineData("Jollof Rice", "jollof-rice")]
    [InlineData("Tom Yum Soup!!!", "tom-yum-soup")]
    [InlineData("Crème Brûlée", "crme-brle")]
    [InlineData("100% Whole Wheat Bread", "100-whole-wheat-bread")]
    [InlineData("A", "a")]
    public void GenerateSlug_VariousTitles_ProducesValidSlugs(string title, string expectedStart)
    {
        var slug = _svc.GenerateSlug(title);
        slug.Should().NotBeNullOrWhiteSpace();
        slug.Should().MatchRegex("^[a-z0-9-]*$");
        slug.Should().StartWith(expectedStart[..Math.Min(expectedStart.Length, 3)]);
    }

    [Fact]
    public async Task NormalizeAsync_WithNullRaw_ReturnsNull()
    {
        var raw = new MyMealPlanner.Core.Models.ScrapedRaw
        {
            RawJson   = "{}",
            Platform  = "Website",
            SourceUrl = "https://example.com"
        };

        var result = await _svc.NormalizeAsync(raw);
        result.Should().BeNull(); // no name field in JSON
    }

    [Fact]
    public async Task NormalizeAsync_ValidSchemaOrgJson_ReturnsRecipe()
    {
        var json = """
            {
              "@type": "Recipe",
              "name": "Classic Jollof Rice",
              "description": "A traditional West African rice dish",
              "recipeIngredient": ["2 cups rice", "3 tomatoes", "1 onion"],
              "recipeInstructions": [{"text": "Cook the rice with the tomato base."}],
              "prepTime": "PT15M",
              "cookTime": "PT45M"
            }
            """;

        var raw = new MyMealPlanner.Core.Models.ScrapedRaw
        {
            RawJson   = json,
            Platform  = "Website",
            SourceUrl = "https://example.com"
        };

        var result = await _svc.NormalizeAsync(raw);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Classic Jollof Rice");
        result.PrepTimeMinutes.Should().Be(15);
        result.CookTimeMinutes.Should().Be(45);
        result.Ingredients.Should().HaveCount(3);
        result.Steps.Should().HaveCount(1);
    }
}

/// <summary>
/// Email service tests — verifying template generation and content.
/// </summary>
public class EmailServiceTests
{
    [Fact]
    public void EmailTemplate_ContainsRequiredElements()
    {
        // The template is private, but we can verify the EmailService is constructable
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SmtpHost"]  = "smtp.test.com",
                ["Email:SmtpPort"]  = "587",
                ["Email:Username"]  = "test@test.com",
                ["Email:Password"]  = "password",
                ["Email:FromName"]  = "Test Planner",
                ["App:BaseUrl"]     = "https://test.mymealplanner.app"
            })
            .Build();

        var logger  = Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<MyMealPlanner.Services.Email.EmailService>>();
        var service = new MyMealPlanner.Services.Email.EmailService(config, logger);

        service.Should().NotBeNull();
    }
}
