using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Web.Controllers;

// ═══════════════════════════════════════════════════════════════
// MEAL PLAN CONTROLLER
// ═══════════════════════════════════════════════════════════════
[Authorize]
public class MealPlanController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MealPlanController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    public async Task<IActionResult> Index()
    {
        var userId = _users.GetUserId(User)!;

        // Get or create current week's plan
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1);
        var plan = await _db.MealPlans
            .Include(p => p.ShoppingList)
                .ThenInclude(s => s!.Items)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.WeekStartDate == weekStart);

        var items = plan != null
            ? await _db.MealPlanItems
                .Include(i => i.Recipe)
                .Where(i => i.MealPlanId == plan.Id)
                .ToListAsync()
            : new List<MealPlanItem>();

        ViewBag.CurrentPlan  = plan;
        ViewBag.PlanItems    = items;
        ViewBag.ShoppingList = plan?.ShoppingList;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate()
    {
        var userId    = _users.GetUserId(User)!;
        var user      = await _db.Users.FindAsync(userId);
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1);

        // Remove existing
        var existing = await _db.MealPlans.FirstOrDefaultAsync(
            p => p.UserId == userId && p.WeekStartDate == weekStart);
        if (existing != null) _db.MealPlans.Remove(existing);

        // Create new plan
        var plan = new MealPlan
        {
            UserId        = userId,
            WeekStartDate = weekStart,
            IsAIGenerated = true,
            Name          = $"Week of {weekStart:MMMM d}"
        };
        _db.MealPlans.Add(plan);
        await _db.SaveChangesAsync();

        // Pull recipes matching user's preferences
        var recipesPool = await _db.Recipes
            .Where(r => r.IsPublished)
            .OrderBy(_ => Guid.NewGuid())
            .Take(50)
            .ToListAsync();

        var mealTypes = new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner };
        var servings  = user?.NumberOfPeopleICookFor ?? 4;
        int slot      = 0;

        for (int day = 1; day <= 7; day++)
        {
            foreach (var mealType in mealTypes)
            {
                var recipe = recipesPool.ElementAtOrDefault(slot++ % recipesPool.Count);
                if (recipe == null) continue;

                _db.MealPlanItems.Add(new MealPlanItem
                {
                    MealPlanId = plan.Id,
                    RecipeId   = recipe.Id,
                    DayOfWeek  = day,
                    MealType   = mealType,
                    Servings   = servings
                });
            }
        }

        await _db.SaveChangesAsync();
        await GenerateShoppingListAsync(plan.Id);

        TempData["Success"] = "Your meal plan has been generated! 🍽️";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] AddMealItemRequest req)
    {
        var userId    = _users.GetUserId(User)!;
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1);
        var user      = await _db.Users.FindAsync(userId);

        var plan = await _db.MealPlans
            .FirstOrDefaultAsync(p => p.UserId == userId && p.WeekStartDate == weekStart);

        if (plan == null)
        {
            plan = new MealPlan { UserId = userId, WeekStartDate = weekStart };
            _db.MealPlans.Add(plan);
            await _db.SaveChangesAsync();
        }

        // Remove existing slot
        var existing = await _db.MealPlanItems.FirstOrDefaultAsync(
            i => i.MealPlanId == plan.Id && i.DayOfWeek == req.DayOfWeek &&
                 i.MealType == Enum.Parse<MealType>(req.MealType));
        if (existing != null) _db.MealPlanItems.Remove(existing);

        _db.MealPlanItems.Add(new MealPlanItem
        {
            MealPlanId = plan.Id,
            RecipeId   = req.RecipeId,
            DayOfWeek  = req.DayOfWeek,
            MealType   = Enum.Parse<MealType>(req.MealType),
            Servings   = user?.NumberOfPeopleICookFor ?? 4
        });

        await _db.SaveChangesAsync();
        await GenerateShoppingListAsync(plan.Id);

        return Json(new { success = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePlanItem(int itemId)
    {
        var item = await _db.MealPlanItems.FindAsync(itemId);
        if (item != null) _db.MealPlanItems.Remove(item);
        await _db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleShoppingItem(int id)
    {
        var item = await _db.ShoppingListItems.FindAsync(id);
        if (item != null)
        {
            item.IsPurchased = !item.IsPurchased;
            await _db.SaveChangesAsync();
        }
        return Ok();
    }

    public async Task<IActionResult> ShoppingList(int id)
    {
        var userId = _users.GetUserId(User)!;
        var list = await _db.ShoppingLists
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.MealPlanId == id && s.MealPlan.UserId == userId);
        if (list == null) return NotFound();
        return View(list);
    }

    public async Task<IActionResult> ExportPdf(int id)
    {
        var userId = _users.GetUserId(User)!;
        var plan = await _db.MealPlans
            .Include(p => p.Items).ThenInclude(i => i.Recipe)
            .Include(p => p.ShoppingList).ThenInclude(s => s!.Items)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        if (plan == null) return NotFound();

        // QuestPDF generation — returns PDF bytes
        // var pdf = MealPlanPdfBuilder.Build(plan);
        // return File(pdf, "application/pdf", $"MealPlan-{plan.WeekStartDate:yyyyMMdd}.pdf");
        TempData["Info"] = "PDF export is being generated. This feature completes in Phase 4.";
        return RedirectToAction("Index");
    }

    private async Task GenerateShoppingListAsync(int planId)
    {
        var existing = await _db.ShoppingLists.FirstOrDefaultAsync(s => s.MealPlanId == planId);
        if (existing != null)
        {
            _db.ShoppingListItems.RemoveRange(existing.Items);
            _db.ShoppingLists.Remove(existing);
        }

        var items = await _db.MealPlanItems
            .Include(i => i.Recipe).ThenInclude(r => r.Ingredients)
            .Where(i => i.MealPlanId == planId)
            .ToListAsync();

        var shoppingList = new ShoppingList { MealPlanId = planId, GeneratedAt = DateTime.UtcNow };
        _db.ShoppingLists.Add(shoppingList);
        await _db.SaveChangesAsync();

        // Aggregate ingredients
        var aggregated = items
            .SelectMany(i => i.Recipe.Ingredients.Select(ing => new
            {
                ing.Name, ing.Unit,
                Quantity = ing.Quantity * (decimal)((double)i.Servings / i.Recipe.Servings)
            }))
            .GroupBy(x => (x.Name, x.Unit))
            .Select(g => new ShoppingListItem
            {
                ShoppingListId  = shoppingList.Id,
                IngredientName  = g.Key.Name,
                Quantity        = (decimal)g.Sum(x => x.Quantity),
                Unit            = g.Key.Unit,
                StoreSection    = GuessStoreSection(g.Key.Name)
            });

        _db.ShoppingListItems.AddRange(aggregated);
        await _db.SaveChangesAsync();
    }

    private static string GuessStoreSection(string name) =>
        name.ToLowerInvariant() switch
        {
            var n when new[] { "apple","banana","tomato","onion","garlic","carrot","pepper","lettuce","spinach" }.Any(n.Contains) => "Produce",
            var n when new[] { "chicken","beef","pork","lamb","fish","prawn","shrimp","salmon","tuna" }.Any(n.Contains) => "Meat & Fish",
            var n when new[] { "milk","yoghurt","cream","cheese","butter","egg" }.Any(n.Contains) => "Dairy & Eggs",
            var n when new[] { "rice","pasta","flour","oat","bread","noodle" }.Any(n.Contains) => "Grains & Staples",
            var n when new[] { "oil","salt","pepper","spice","herb","cumin","turmeric","paprika","ginger" }.Any(n.Contains) => "Spices & Oils",
            var n when new[] { "can","tin","sauce","paste","stock","broth" }.Any(n.Contains) => "Canned & Sauces",
            _ => "Other"
        };
}

public class AddMealItemRequest
{
    public int RecipeId  { get; set; }
    public int DayOfWeek { get; set; }
    public string MealType { get; set; } = "Lunch";
}

// ═══════════════════════════════════════════════════════════════
// SEARCH API CONTROLLER
// ═══════════════════════════════════════════════════════════════
[Route("api/search")]
public class SearchApiController : Controller
{
    private readonly ApplicationDbContext _db;

    public SearchApiController(ApplicationDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(q)) return Json(Array.Empty<object>());

        var results = await _db.Recipes
            .Where(r => r.IsPublished && (
                r.Title.Contains(q) ||
                r.Description.Contains(q) ||
                (r.OriginCountry != null && r.OriginCountry.Contains(q)) ||
                (r.CultureTag   != null && r.CultureTag.Contains(q)) ||
                (r.CulturalStory != null && r.CulturalStory.Contains(q))
            ))
            .OrderByDescending(r => r.LikeCount)
            .Take(limit)
            .Select(r => new {
                r.Id, r.Title, r.Slug,
                r.OriginCountry, r.CoverImageUrl,
                MealType = r.MealType.ToString(),
                r.PrepTimeMinutes, r.CookTimeMinutes,
                r.LikeCount
            })
            .ToListAsync();

        return Json(results);
    }

    [HttpPost("image")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SearchByImage(IFormFile image)
    {
        if (image == null || image.Length == 0)
            return Json(new { error = "No image provided" });

        // In production: pipe to IImageSearchService.IdentifyFoodFromImageAsync
        // For now return a stub that triggers a text search
        return Json(new
        {
            identifiedDish  = "Recipe",
            confidence      = 0.0,
            matchingRecipes = Array.Empty<object>()
        });
    }
}

// ═══════════════════════════════════════════════════════════════
// NOTIFICATIONS API
// ═══════════════════════════════════════════════════════════════
[Route("api/notifications")]
[Authorize]
public class NotificationsApiController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public NotificationsApiController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    [HttpGet("")]
    public async Task<IActionResult> GetUnread()
    {
        var userId = _users.GetUserId(User)!;
        var notes  = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .Select(n => new {
                n.Id, n.Type, n.Title, n.Body,
                n.ActionUrl, n.IsRead, n.CreatedAt
            })
            .ToListAsync();
        return Json(notes);
    }
}
