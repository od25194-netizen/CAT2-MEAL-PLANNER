using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Web.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _db;

    public InventoryController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var equipment = await _db.CookingEquipment
            .OrderBy(e => e.Category)
            .ThenBy(e => e.Name)
            .ToListAsync();
        return View(equipment);
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await _db.CookingEquipment.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }
}
