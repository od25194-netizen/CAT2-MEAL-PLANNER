using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Web.Controllers;

// ═══════════════════════════════════════════════════════════════
// HOME CONTROLLER
// ═══════════════════════════════════════════════════════════════
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRankingService _ranking;

    public HomeController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IRankingService ranking)
    {
        _db          = db;
        _userManager = userManager;
        _ranking     = ranking;
    }

    public async Task<IActionResult> Index()
    {
        var user          = await _userManager.GetUserAsync(User);
        var countryCode   = user?.CountryCode;
        var continentCode = user?.ContinentCode;

        // ── Live platform stats ───────────────────────────────
        var totalRecipes  = await _db.Recipes.CountAsync(r => r.IsPublished);
        var totalUsers    = await _db.Users.CountAsync(u => !u.IsDeleted);
        var countriesCovered = await _db.Recipes
            .Where(r => r.IsPublished && r.OriginCountryCode != null)
            .Select(r => r.OriginCountryCode)
            .Distinct()
            .CountAsync();
        var continentsCovered = await _db.Recipes
            .Where(r => r.IsPublished && r.OriginContinent != null)
            .Select(r => r.OriginContinent)
            .Distinct()
            .CountAsync();

        // ── Trending ──────────────────────────────────────────
        var globalTrending = await _ranking.GetTrendingThisWeekAsync(null, 8);
        var localTrending  = countryCode is not null
            ? await _ranking.GetTrendingThisWeekAsync(countryCode, 6)
            : new List<Core.Models.DishRanking>();

        // ── Featured recipes ──────────────────────────────────
        var featured = await _db.Recipes
            .Where(r => r.IsPublished && r.IsFeatured)
            .OrderByDescending(r => r.CreatedAt)
            .Take(6)
            .ToListAsync();

        // ── If no featured, fall back to top-liked ────────────
        if (!featured.Any())
        {
            featured = await _db.Recipes
                .Where(r => r.IsPublished)
                .OrderByDescending(r => r.LikeCount)
                .Take(6)
                .ToListAsync();
        }

        // ── New today (or recent if none today) ───────────────
        var newToday = await _db.Recipes
            .Where(r => r.IsPublished && r.CreatedAt >= DateTime.UtcNow.AddHours(-24))
            .OrderByDescending(r => r.CreatedAt)
            .Take(12)
            .ToListAsync();

        if (!newToday.Any())
        {
            newToday = await _db.Recipes
                .Where(r => r.IsPublished)
                .OrderByDescending(r => r.CreatedAt)
                .Take(8)
                .ToListAsync();
        }

        // ── Random joke ───────────────────────────────────────
        var joke = await _db.CookingJokes
            .Where(j => j.IsApproved)
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync();

        ViewBag.GlobalTrending    = globalTrending;
        ViewBag.LocalTrending     = localTrending;
        ViewBag.Featured          = featured;
        ViewBag.NewToday          = newToday;
        ViewBag.DailyJoke         = joke;
        ViewBag.CountryCode       = countryCode;
        ViewBag.ContinentCode     = continentCode;
        ViewBag.TotalRecipes      = totalRecipes;
        ViewBag.TotalUsers        = totalUsers;
        ViewBag.CountriesCovered  = countriesCovered;
        ViewBag.ContinentsCovered = continentsCovered;

        return View();
    }

    public IActionResult Privacy()   => View();
    public IActionResult About()     => View();
    public IActionResult Error()     => View();
}

// ═══════════════════════════════════════════════════════════════
// RECIPE CONTROLLER
// ═══════════════════════════════════════════════════════════════
public class RecipeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IYouTubeService _youtube;
    private readonly ISpoonacularService _spoonacular;

    public RecipeController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IYouTubeService youtube,
        ISpoonacularService spoonacular)
    {
        _db          = db;
        _userManager = userManager;
        _youtube     = youtube;
        _spoonacular = spoonacular;
    }

    // ── Browse / Search ──────────────────────────────────────
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        string? q,
        string? country,
        string? continent,
        string? culture,
        string? mealType,
        string? diet,
        string? environment,
        string? difficulty,
        int? maxTime,
        DiscoveryScope scope = DiscoveryScope.Global,
        int page = 1)
    {
        var user = await _userManager.GetUserAsync(User);
        var pageSize = 24;

        var query = _db.Recipes
            .Where(r => r.IsPublished)
            .AsQueryable();

        // Scope filter
        if (scope == DiscoveryScope.Local && user?.CityName is not null)
            query = query.Where(r => r.Region == user.CityName ||
                                     r.OriginCountryCode == user.CountryCode);
        else if (scope == DiscoveryScope.Country && user?.CountryCode is not null)
            query = query.Where(r => r.OriginCountryCode == user.CountryCode);
        else if (scope == DiscoveryScope.Continent && continent is not null)
            query = query.Where(r => r.OriginContinent == continent);

        // Filters
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(r =>
                r.Title.Contains(q) ||
                r.Description.Contains(q) ||
                r.CultureTag!.Contains(q) ||
                r.OriginCountry!.Contains(q));

        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(r => r.OriginCountry == country);

        if (!string.IsNullOrWhiteSpace(continent))
            query = query.Where(r => r.OriginContinent == continent);

        if (!string.IsNullOrWhiteSpace(culture))
            query = query.Where(r => r.CultureTag == culture);

        if (Enum.TryParse<MealType>(mealType, true, out var mt))
            query = query.Where(r => r.MealType == mt);

        if (Enum.TryParse<CookingEnvironment>(environment, true, out var env))
            query = query.Where(r => r.CookingEnvironment == env);

        if (Enum.TryParse<DifficultyLevel>(difficulty, true, out var diff))
            query = query.Where(r => r.DifficultyLevel == diff);

        if (maxTime.HasValue)
            query = query.Where(r => r.PrepTimeMinutes + r.CookTimeMinutes <= maxTime.Value);

        if (!string.IsNullOrWhiteSpace(diet))
            query = query.Where(r => r.DietaryTagsJson!.Contains(diet));

        var totalCount  = await query.CountAsync();
        var recipes     = await query
            .OrderByDescending(r => r.LikeCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Auto-scale servings if user has household size set
        if (user?.NumberOfPeopleICookFor > 1)
            ViewBag.ServingsMultiplier = (double)user.NumberOfPeopleICookFor / 4.0;

        ViewBag.TotalCount  = totalCount;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages  = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.Query       = q;
        ViewBag.Scope       = scope;

        return View(recipes);
    }

    // ── Recipe Detail ─────────────────────────────────────────
    public async Task<IActionResult> Details(int id, string? slug)
    {
        var user = await _userManager.GetUserAsync(User);

        var recipe = await _db.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Steps.OrderBy(s => s.StepOrder))
            .Include(r => r.AlternativeIngredients)
            .Include(r => r.NutritionInfo)
            .Include(r => r.Comments.Where(c => !c.IsDeleted && c.ParentCommentId == null)
                                    .OrderByDescending(c => c.LikeCount)
                                    .Take(20))
                .ThenInclude(c => c.User)
            .Include(r => r.SubmittedByUser)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsPublished);

        if (recipe is null) return NotFound();

        // SEO slug redirect
        if (!string.IsNullOrEmpty(slug) && slug != recipe.Slug)
            return RedirectToActionPermanent("Details", new { id, slug = recipe.Slug });

        // Increment view count
        await _db.Recipes.Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ViewCount, r => r.ViewCount + 1));

        // YouTube metadata
        if (!string.IsNullOrEmpty(recipe.YouTubeVideoId))
        {
            ViewBag.YouTubeMeta  = await _youtube.GetVideoMetaAsync(recipe.YouTubeVideoId);
            ViewBag.YouTubeEmbed = _youtube.GetEmbedUrl(recipe.YouTubeVideoId);
        }

        // Related YouTube videos
        ViewBag.RelatedVideos = await _youtube.SearchFoodVideosAsync(recipe.Title, 4);

        // Auto-scale servings
        int targetServings = user?.NumberOfPeopleICookFor ?? recipe.Servings;
        ViewBag.ServingsMultiplier = (double)targetServings / recipe.Servings;
        ViewBag.TargetServings     = targetServings;

        // Cost breakdown
        var userCountry = user?.CountryCode ?? "US";
        ViewBag.CostInfo = await _db.IngredientCosts
            .Where(c => c.RecipeId == id && c.CountryCode == userCountry)
            .FirstOrDefaultAsync();

        // User has liked?
        ViewBag.UserHasLiked = user is not null &&
            await _db.RecipeLikes.AnyAsync(l => l.RecipeId == id && l.UserId == user.Id);

        // User has saved?
        ViewBag.UserHasSaved = user is not null &&
            await _db.CollectionItems.AnyAsync(ci =>
                ci.RecipeId == id && ci.Collection.UserId == user.Id);

        // Rankings
        ViewBag.Rankings = await _db.DishRankings
            .Where(r => r.RecipeId == id)
            .OrderBy(r => r.Scope)
            .ToListAsync();

        return View(recipe);
    }

    // ── Like / Unlike (AJAX) ──────────────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Like(int id, string reactionType = "Love")
    {
        var userId  = _userManager.GetUserId(User)!;
        var existing = await _db.RecipeLikes
            .FirstOrDefaultAsync(l => l.RecipeId == id && l.UserId == userId);

        int delta;
        if (existing is not null)
        {
            _db.RecipeLikes.Remove(existing);
            delta = -1;
        }
        else
        {
            _db.RecipeLikes.Add(new RecipeLike
            {
                RecipeId     = id,
                UserId       = userId,
                ReactionType = Enum.TryParse<ReactionType>(reactionType, true, out var rt) ? rt : ReactionType.Love
            });
            delta = 1;
        }

        await _db.Recipes.Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.LikeCount, r => r.LikeCount + delta));

        await _db.SaveChangesAsync();

        var newCount = await _db.Recipes.Where(r => r.Id == id).Select(r => r.LikeCount).FirstAsync();
        return Json(new { liked = delta > 0, likeCount = newCount });
    }

    // ── Save to Collection (AJAX) ─────────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, int? collectionId)
    {
        var userId = _userManager.GetUserId(User)!;

        // Default collection — "My Saved Recipes"
        if (collectionId is null)
        {
            var defaultCollection = await _db.SavedCollections
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "My Saved Recipes");

            if (defaultCollection is null)
            {
                defaultCollection = new SavedCollection { UserId = userId, Name = "My Saved Recipes" };
                _db.SavedCollections.Add(defaultCollection);
                await _db.SaveChangesAsync();
            }

            collectionId = defaultCollection.Id;
        }

        var alreadySaved = await _db.CollectionItems
            .AnyAsync(ci => ci.CollectionId == collectionId && ci.RecipeId == id);

        if (!alreadySaved)
        {
            _db.CollectionItems.Add(new CollectionItem
            {
                CollectionId = collectionId.Value,
                RecipeId     = id
            });

            await _db.Recipes.Where(r => r.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.SaveCount, r => r.SaveCount + 1));

            await _db.SaveChangesAsync();
        }

        return Json(new { saved = !alreadySaved });
    }

    // ── Suggest a Recipe ─────────────────────────────────────
    [Authorize]
    public IActionResult Suggest() => View();

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suggest(RecipeSuggestion model)
    {
        if (!ModelState.IsValid) return View(model);

        model.SubmittedByUserId = _userManager.GetUserId(User)!;
        model.Status            = SuggestionStatus.Pending;
        model.SubmittedAt       = DateTime.UtcNow;

        _db.RecipeSuggestions.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Thank you! Your recipe is under review. 🍽️";
        return RedirectToAction("Index");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportSpoonacular(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Json(new { success = false, message = "Please enter a valid recipe name or keyword." });

        var recipe = await _spoonacular.SearchAndImportRecipeAsync(keyword);
        if (recipe == null)
            return Json(new { success = false, message = "Could not find or import any matching recipe from Spoonacular. Try another keyword!" });

        return Json(new { 
            success = true, 
            recipeId = recipe.Id, 
            slug = recipe.Slug,
            title = recipe.Title,
            url = Url.Action("Details", new { id = recipe.Id, slug = recipe.Slug })
        });
    }

    // ── Cook Log ──────────────────────────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogCook(int recipeId, string? note, string? photoUrl, int rating)
    {
        var userId = _userManager.GetUserId(User)!;

        _db.CookLogs.Add(new CookLog
        {
            UserId   = userId,
            RecipeId = recipeId,
            Note     = note,
            PhotoUrl = photoUrl,
            Rating   = Math.Clamp(rating, 1, 5),
            CookedAt = DateTime.UtcNow
        });

        await _db.Recipes.Where(r => r.Id == recipeId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.CookLogCount, r => r.CookLogCount + 1));

        // Update user streak
        var user = await _db.Users.FindAsync(userId);
        if (user is not null)
        {
            var yesterday  = DateTime.UtcNow.Date.AddDays(-1);
            var lastCook   = user.LastCookDate?.Date;
            user.CookStreak = lastCook == yesterday ? user.CookStreak + 1 : 1;
            user.LastCookDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }
}
