using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Web.Controllers;

// ═══════════════════════════════════════════════════════════════
// SOCIAL CONTROLLER
// ═══════════════════════════════════════════════════════════════
[Authorize]
public class SocialController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public SocialController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    public async Task<IActionResult> Chat(int? roomId)
    {
        var userId = _users.GetUserId(User)!;

        var rooms = await _db.ChatRooms
            .OrderByDescending(r => r.MemberCount)
            .Take(20)
            .ToListAsync();

        ChatRoom? active = null;
        List<ChatMessage> history = new();

        if (roomId.HasValue)
        {
            active = await _db.ChatRooms.FindAsync(roomId.Value);
            history = await _db.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.RoomId == roomId.Value && !m.IsDeleted)
                .OrderBy(m => m.SentAt)
                .Take(50)
                .ToListAsync();

            // Mark messages as read
            await _db.ChatMessages
                .Where(m => m.RoomId == roomId.Value && !m.IsRead && m.SenderId != userId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        }

        ViewBag.Rooms         = rooms;
        ViewBag.Active        = active;
        ViewBag.History       = history;
        ViewBag.CurrentUserId = userId;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(string name, string? description, string? cultureTag)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest();

        var room = new ChatRoom
        {
            Name        = name[..Math.Min(name.Length, 80)],
            Description = description,
            CultureTag  = cultureTag,
            HostUserId  = _users.GetUserId(User),
            MemberCount = 1,
            CreatedAt   = DateTime.UtcNow
        };

        _db.ChatRooms.Add(room);
        await _db.SaveChangesAsync();

        return RedirectToAction("Chat", new { roomId = room.Id });
    }
}

// ═══════════════════════════════════════════════════════════════
// THEME + UTILITY CONTROLLER
// ═══════════════════════════════════════════════════════════════
[Authorize]
public class PreferencesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public PreferencesController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    [HttpPost, ValidateAntiForgeryToken]
    [Route("Account/SetTheme")]
    public async Task<IActionResult> SetTheme([FromBody] SetThemeRequest req)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();

        user.DarkMode = req.DarkMode;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}

public class SetThemeRequest { public bool DarkMode { get; set; } }
