using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;
using MyMealPlanner.Services;

namespace MyMealPlanner.Web.Controllers;

// ═══════════════════════════════════════════════════════════════
// HEALTH CONTROLLER
// ═══════════════════════════════════════════════════════════════
public class HealthController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public HealthController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    // Eat Healthy hub home
    [AllowAnonymous]
    public IActionResult Index() => View();

    [AllowAnonymous]
    public async Task<IActionResult> DietPlans()
    {
        var plans = await _db.DietPlans.OrderBy(p => p.PlanName).ToListAsync();
        return View(plans);
    }


    // ── Nutrient Navigator — click a nutrient, see all foods ──
    [AllowAnonymous]
    public async Task<IActionResult> Nutrients(NutrientCategory? category)
    {
        var allCategories = Enum.GetValues<NutrientCategory>().ToList();

        if (category.HasValue)
        {
            var foods = await _db.NutrientFoods
                .Where(f => f.NutrientCategory == category.Value)
                .OrderBy(f => f.SortRank)
                .ToListAsync();

            var recipes = await _db.Recipes
                .Where(r => r.IsPublished && r.NutrientTagsJson!.Contains(category.Value.ToString()))
                .OrderByDescending(r => r.LikeCount)
                .Take(12)
                .ToListAsync();

            ViewBag.SelectedCategory = category.Value;
            ViewBag.Foods   = foods;
            ViewBag.Recipes = recipes;
        }

        ViewBag.AllCategories = allCategories;
        return View();
    }

    // ── Allergy Guide ─────────────────────────────────────────
    [AllowAnonymous]
    public async Task<IActionResult> Allergies(AllergenType? type)
    {
        var guides = await _db.AllergyGuides.ToListAsync();
        ViewBag.SelectedGuide = type.HasValue
            ? guides.FirstOrDefault(g => g.AllergenType == type.Value)
            : null;
        ViewBag.AllGuides = guides;
        return View();
    }

    // ── Food as Medicine ─────────────────────────────────────
    [AllowAnonymous]
    public async Task<IActionResult> FoodAsMedicine()
    {
        var benefits = await _db.FoodHealthBenefits.ToListAsync();
        return View(benefits);
    }

    // ── When To Eat ───────────────────────────────────────────
    [AllowAnonymous]
    public async Task<IActionResult> WhenToEat()
    {
        var guides = await _db.FoodTimingGuides
            .OrderBy(g => g.TimingType)
            .ToListAsync();
        return View(guides);
    }

    // ── Age Bracket Plans ─────────────────────────────────────
    [AllowAnonymous]
    public async Task<IActionResult> AgePlan(AgeBracket? bracket)
    {
        var user = await _users.GetUserAsync(User);
        var target = bracket ?? user?.AgeBracket ?? AgeBracket.Adults;

        var recipes = await _db.Recipes
            .Where(r => r.IsPublished && r.DietaryTagsJson!.Contains(target.ToString()))
            .OrderByDescending(r => r.LikeCount)
            .Take(16)
            .ToListAsync();

        ViewBag.Bracket  = target;
        ViewBag.Recipes  = recipes;
        return View();
    }

    // ── Obesity Fighter ───────────────────────────────────────
    [AllowAnonymous]
    public async Task<IActionResult> ObesityFighter()
    {
        var recipes = await _db.Recipes
            .Where(r => r.IsPublished && r.DietaryTagsJson!.Contains("LowCalorie"))
            .OrderByDescending(r => r.LikeCount)
            .Take(20)
            .ToListAsync();
        return View(recipes);
    }

    // ── Workout Nutrition ────────────────────────────────────
    [AllowAnonymous]
    public IActionResult Workout() => View();

    // ── Pet Health ────────────────────────────────────────────
    [AllowAnonymous]
    public IActionResult Pets() => View();
}

// ═══════════════════════════════════════════════════════════════
// EXPLORE / RANKINGS CONTROLLER
// ═══════════════════════════════════════════════════════════════
public class ExploreController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IRankingService _ranking;
    private readonly UserManager<ApplicationUser> _users;

    public ExploreController(ApplicationDbContext db, IRankingService ranking,
        UserManager<ApplicationUser> users)
    { _db = db; _ranking = ranking; _users = users; }

    [AllowAnonymous]
    public async Task<IActionResult> Index(DiscoveryScope scope = DiscoveryScope.Global,
        string? continent = null, string? country = null)
    {
        var user = await _users.GetUserAsync(User);

        var topGlobal    = await _ranking.GetTopByScope(RankingScope.Global,    null, 10);
        var topWeekly    = await _ranking.GetTrendingThisWeekAsync(null, 10);
        var topContinent = continent is not null
            ? await _ranking.GetTopByScope(RankingScope.Continent, continent, 10)
            : new List<DishRanking>();
        var topCountry   = (country ?? user?.CountryCode) is { } c
            ? await _ranking.GetTopByScope(RankingScope.Country, c, 10)
            : new List<DishRanking>();

        // Cultural categories
        var cultures = await _db.Recipes
            .Where(r => r.IsPublished && r.CultureTag != null)
            .GroupBy(r => r.CultureTag)
            .Select(g => new { Culture = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(20)
            .ToListAsync();

        ViewBag.TopGlobal    = topGlobal;
        ViewBag.TopWeekly    = topWeekly;
        ViewBag.TopContinent = topContinent;
        ViewBag.TopCountry   = topCountry;
        ViewBag.Cultures     = cultures;
        ViewBag.Scope        = scope;
        ViewBag.Continent    = continent;
        ViewBag.Country      = country ?? user?.CountryCode;
        return View();
    }

    [AllowAnonymous]
    public async Task<IActionResult> Rankings(RankingScope scope = RankingScope.Global,
        string? scopeValue = null, int page = 1)
    {
        int pageSize = 50;
        var all = await _ranking.GetTopByScope(scope, scopeValue, 200);
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.Scope      = scope;
        ViewBag.ScopeValue = scopeValue;
        ViewBag.Page       = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)all.Count / pageSize);
        return View(paged);
    }

    [AllowAnonymous]
    public async Task<IActionResult> CulturalMap()
    {
        var continentData = await _db.Recipes
            .Where(r => r.IsPublished && r.OriginContinent != null)
            .GroupBy(r => r.OriginContinent)
            .Select(g => new { Continent = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.ContinentData = continentData;
        return View();
    }
}

// ═══════════════════════════════════════════════════════════════
// PROFILE CONTROLLER
// ═══════════════════════════════════════════════════════════════
[Authorize]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    public async Task<IActionResult> Index(string? userId)
    {
        var targetId = userId ?? _users.GetUserId(User)!;
        var profile  = await _db.Users
            .Include(u => u.Pets)
            .Include(u => u.Badges).ThenInclude(b => b.Badge)
            .FirstOrDefaultAsync(u => u.Id == targetId && !u.IsDeleted);

        if (profile is null) return NotFound();

        var isOwnProfile = profile.Id == _users.GetUserId(User);
        if (!isOwnProfile && profile.ProfileVisibility == "Private") return Forbid();

        // Recipes
        var recipes = await _db.Recipes
            .Where(r => r.SubmittedByUserId == targetId && r.IsPublished)
            .OrderByDescending(r => r.CreatedAt).Take(12).ToListAsync();

        // Cook logs
        var cookLogs = await _db.CookLogs
            .Include(c => c.Recipe)
            .Where(c => c.UserId == targetId && c.IsPublic)
            .OrderByDescending(c => c.CookedAt).Take(12).ToListAsync();

        // Collections
        var collections = await _db.SavedCollections
            .Where(c => c.UserId == targetId && (c.IsPublic || isOwnProfile))
            .Take(6).ToListAsync();

        // Cuisine passport
        var cuisinePassport = await _db.CookLogs
            .Include(c => c.Recipe)
            .Where(c => c.UserId == targetId && c.Recipe.OriginCountry != null)
            .GroupBy(c => c.Recipe.OriginCountry)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        // Quiz history
        var quizHistory = await _db.QuizAttempts
            .Where(q => q.UserId == targetId)
            .OrderByDescending(q => q.AttemptedAt)
            .Take(5).ToListAsync();

        // Is following?
        var currentUserId = _users.GetUserId(User);
        ViewBag.IsFollowing = !isOwnProfile && currentUserId is not null &&
            await _db.UserFollows.AnyAsync(f =>
                f.FollowerId == currentUserId && f.FolloweeId == targetId);

        ViewBag.IsOwnProfile   = isOwnProfile;
        ViewBag.Recipes        = recipes;
        ViewBag.CookLogs       = cookLogs;
        ViewBag.Collections    = collections;
        ViewBag.CuisinePassport = cuisinePassport;
        ViewBag.QuizHistory    = quizHistory;
        return View(profile);
    }

    // ── Follow / Unfollow ─────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Follow(string targetUserId)
    {
        var currentId = _users.GetUserId(User)!;
        var existing  = await _db.UserFollows.FirstOrDefaultAsync(
            f => f.FollowerId == currentId && f.FolloweeId == targetUserId);

        if (existing is not null)
        {
            _db.UserFollows.Remove(existing);
            await _db.Users.Where(u => u.Id == currentId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowingCount, u => u.FollowingCount - 1));
            await _db.Users.Where(u => u.Id == targetUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowerCount, u => u.FollowerCount - 1));
        }
        else
        {
            _db.UserFollows.Add(new UserFollow { FollowerId = currentId, FolloweeId = targetUserId });
            await _db.Users.Where(u => u.Id == currentId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowingCount, u => u.FollowingCount + 1));
            await _db.Users.Where(u => u.Id == targetUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FollowerCount, u => u.FollowerCount + 1));
        }

        await _db.SaveChangesAsync();
        return Json(new { following = existing is null });
    }
}

// ═══════════════════════════════════════════════════════════════
// JOKES CONTROLLER
// ═══════════════════════════════════════════════════════════════
public class JokesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public JokesController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? category, int page = 1)
    {
        int pageSize = 10;
        var query = _db.CookingJokes.Where(j => j.IsApproved);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(j => j.Category == category);

        var total = await query.CountAsync();
        var jokes = await query
            .OrderByDescending(j => j.LikeCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var categories = await _db.CookingJokes
            .Where(j => j.IsApproved && j.Category != null)
            .Select(j => j.Category)
            .Distinct().ToListAsync();

        ViewBag.Categories   = categories;
        ViewBag.SelectedCat  = category;
        ViewBag.TotalPages   = (int)Math.Ceiling((double)total / pageSize);
        ViewBag.CurrentPage  = page;
        return View(jokes);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> LikeJoke(int id)
    {
        await _db.CookingJokes.Where(j => j.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.LikeCount, j => j.LikeCount + 1));
        var count = await _db.CookingJokes.Where(j => j.Id == id).Select(j => j.LikeCount).FirstAsync();
        return Json(new { likeCount = count });
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Random()
    {
        var joke = await _db.CookingJokes
            .Where(j => j.IsApproved)
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync();
        return Json(joke);
    }
}

// ═══════════════════════════════════════════════════════════════
// ADMIN CONTROLLER
// ═══════════════════════════════════════════════════════════════
[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IRecipePromotionService _promotion;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users, IRecipePromotionService promotion)
    { 
        _db = db; 
        _users = users;
        _promotion = promotion;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.TotalUsers       = await _db.Users.CountAsync(u => !u.IsDeleted);
        ViewBag.TotalRecipes     = await _db.Recipes.CountAsync(r => r.IsPublished);
        ViewBag.PendingSuggest   = await _db.RecipeSuggestions.CountAsync(s => s.Status == SuggestionStatus.Pending);
        ViewBag.TodayScrapes     = await _db.ScrapedRaws.CountAsync(s => s.ParsedAt >= DateTime.UtcNow.AddHours(-24));
        ViewBag.UnpublishedCount = await _db.Recipes.CountAsync(r => !r.IsApproved);
        ViewBag.ActiveJobs       = await _db.ScrapeJobs.CountAsync(j => j.IsActive);
        ViewBag.NewUsersToday    = await _db.Users.CountAsync(u => u.CreatedAt >= DateTime.UtcNow.AddHours(-24));
        ViewBag.TotalCookLogs    = await _db.CookLogs.CountAsync();

        // Continent breakdown
        ViewBag.ContinentStats = await _db.Recipes
            .Where(r => r.IsPublished && r.OriginContinent != null)
            .GroupBy(r => r.OriginContinent)
            .Select(g => new { Continent = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        return View();
    }

    // ── Unified Moderation Hub ───────────────────────────────
    public async Task<IActionResult> Moderation(string tab = "suggestions", int page = 1)
    {
        int pageSize = 20;
        ViewBag.ActiveTab = tab.ToLower();

        if (tab.ToLower() == "scraped")
        {
            var total = await _db.Recipes.CountAsync(r => !r.IsApproved);
            var items = await _db.Recipes
                .Where(r => !r.IsApproved)
                .Include(r => r.SubmittedByUser)
                .OrderBy(r => r.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            return View(items);
        }
        else
        {
            var total = await _db.RecipeSuggestions.CountAsync(s => s.Status == SuggestionStatus.Pending);
            var items = await _db.RecipeSuggestions
                .Include(s => s.SubmittedByUser)
                .Where(s => s.Status == SuggestionStatus.Pending)
                .OrderBy(s => s.SubmittedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            return View(items);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ModerateContent(int id, string type, bool approve, string? note)
    {
        string userId = _users.GetUserId(User)!;

        if (type == "suggestion")
        {
            var suggestion = await _db.RecipeSuggestions.FindAsync(id);
            if (suggestion == null) return NotFound();

            if (approve)
            {
                suggestion.Status = SuggestionStatus.Approved;
                await _promotion.PromoteAsync(suggestion);
            }
            else
            {
                suggestion.Status = SuggestionStatus.Rejected;
            }
            suggestion.ReviewNote = note;
            suggestion.ReviewedByAdminId = userId;
            suggestion.ReviewedAt = DateTime.UtcNow;
        }
        else if (type == "recipe")
        {
            var recipe = await _db.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();

            if (approve)
            {
                recipe.IsApproved = true;
                recipe.IsPublished = true;
            }
            else
            {
                _db.Recipes.Remove(recipe); // Rejecting an unpublished recipe deletes it
            }
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ── User Management ───────────────────────────────────────
    public async Task<IActionResult> Users(string? q, int page = 1)
    {
        int pageSize = 30;
        var query = _db.Users.Where(u => !u.IsDeleted).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => u.FullName.Contains(q) || u.Email!.Contains(q));

        var total = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.TotalPages   = (int)Math.Ceiling((double)total / pageSize);
        ViewBag.CurrentPage  = page;
        ViewBag.Query        = q;
        return View(users);
    }

    // ── Bulk Import ───────────────────────────────────────────
    public IActionResult BulkImport() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkImport(string dataType, string jsonData)
    {
        if (string.IsNullOrWhiteSpace(jsonData)) return Json(new { success = false, message = "JSON data is empty" });

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            switch (dataType.ToLower())
            {
                case "recipes":
                    var recipes = System.Text.Json.JsonSerializer.Deserialize<List<Recipe>>(jsonData, options);
                    if (recipes != null) { _db.Recipes.AddRange(recipes); await _db.SaveChangesAsync(); }
                    break;
                case "quizzes":
                    var quizzes = System.Text.Json.JsonSerializer.Deserialize<List<QuizQuestion>>(jsonData, options);
                    if (quizzes != null) { _db.QuizQuestions.AddRange(quizzes); await _db.SaveChangesAsync(); }
                    break;
                case "allergies":
                    var allergies = System.Text.Json.JsonSerializer.Deserialize<List<AllergyGuide>>(jsonData, options);
                    if (allergies != null) { _db.AllergyGuides.AddRange(allergies); await _db.SaveChangesAsync(); }
                    break;
                case "diets":
                    var diets = System.Text.Json.JsonSerializer.Deserialize<List<DietPlan>>(jsonData, options);
                    if (diets != null) { _db.DietPlans.AddRange(diets); await _db.SaveChangesAsync(); }
                    break;
                case "equipment":
                    var equipment = System.Text.Json.JsonSerializer.Deserialize<List<CookingEquipment>>(jsonData, options);
                    if (equipment != null) { _db.CookingEquipment.AddRange(equipment); await _db.SaveChangesAsync(); }
                    break;
                default:
                    return Json(new { success = false, message = "Invalid data type" });
            }

            return Json(new { success = true, message = "Import successful" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    // ── Scraper Monitor ───────────────────────────────────────
    public async Task<IActionResult> ScraperMonitor()
    {
        var jobs = await _db.ScrapeJobs
            .OrderByDescending(j => j.LastRunAt)
            .ToListAsync();

        var recentRaw = await _db.ScrapedRaws
            .Where(r => r.ParsedAt >= DateTime.UtcNow.AddHours(-24))
            .CountAsync();

        ViewBag.RecentRawCount = recentRaw;
        return View(jobs);
    }
}

// ═══════════════════════════════════════════════════════════════
// QUIZ CONTROLLER
// ═══════════════════════════════════════════════════════════════
[Authorize]
public class QuizController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public QuizController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();

        var lastAttempt = await _db.QuizAttempts
            .Where(q => q.UserId == user.Id)
            .OrderByDescending(q => q.AttemptedAt)
            .FirstOrDefaultAsync();

        ViewBag.CurrentLevel = user.ChefLevel;
        ViewBag.NextLevel    = user.ChefLevel < ChefLevel.Level8_GrandChef
            ? (ChefLevel?)(user.ChefLevel + 1) : null;
        ViewBag.LastAttempt  = lastAttempt;
        ViewBag.CanAttempt   = lastAttempt?.NextAttemptAllowedAt is null ||
                                lastAttempt.NextAttemptAllowedAt <= DateTime.UtcNow;
        return View();
    }

    public async Task<IActionResult> Take()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();

        // Check 48h cooldown
        var lastAttempt = await _db.QuizAttempts
            .Where(q => q.UserId == user.Id)
            .OrderByDescending(q => q.AttemptedAt)
            .FirstOrDefaultAsync();

        if (lastAttempt?.NextAttemptAllowedAt > DateTime.UtcNow)
        {
            TempData["Error"] = $"Next quiz available: {lastAttempt.NextAttemptAllowedAt:f}";
            return RedirectToAction("Index");
        }

        // Personalised questions based on food history
        var targetLevel = user.ChefLevel + 1;
        var questions   = await _db.QuizQuestions
            .Where(q => q.IsActive && q.MinLevel <= targetLevel)
            .OrderBy(_ => Guid.NewGuid())
            .Take(10)
            .ToListAsync();

        ViewBag.TargetLevel = targetLevel;
        return View(questions);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Dictionary<int, string> answers)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();

        var questionIds = answers.Keys.ToList();
        var questions   = await _db.QuizQuestions
            .Where(q => questionIds.Contains(q.Id))
            .ToListAsync();

        int correct = questions.Count(q =>
            answers.TryGetValue(q.Id, out var ans) &&
            string.Equals(ans.Trim(), q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase));

        double score     = questions.Count > 0 ? (double)correct / questions.Count * 100 : 0;
        var targetLevel  = user.ChefLevel + 1;
        double threshold = (int)targetLevel >= 6 ? 90.0 : 80.0;
        bool passed      = score >= threshold;
        bool leveledUp   = false;

        if (passed && targetLevel <= ChefLevel.Level8_GrandChef)
        {
            user.ChefLevel       = targetLevel;
            user.VerificationTick = targetLevel switch
            {
                ChefLevel.Level4_ConfidentCook or ChefLevel.Level5_SkilledChef => VerificationTick.White,
                ChefLevel.Level6_ExpertCulinarian or ChefLevel.Level7_MasterChef => VerificationTick.Green,
                ChefLevel.Level8_GrandChef => VerificationTick.Gold,
                _ => VerificationTick.None
            };
            leveledUp = true;
        }

        var attempt = new QuizAttempt
        {
            UserId                 = user.Id,
            TargetLevel            = targetLevel,
            TotalQuestions         = questions.Count,
            CorrectAnswers         = correct,
            ScorePercent           = score,
            IsPassed               = passed,
            LeveledUp              = leveledUp,
            AttemptedAt            = DateTime.UtcNow,
            NextAttemptAllowedAt   = DateTime.UtcNow.AddHours(48)
        };

        _db.QuizAttempts.Add(attempt);
        await _db.SaveChangesAsync();

        return View("QuizResult", attempt);
    }
}
