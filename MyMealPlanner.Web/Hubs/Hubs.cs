using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;

namespace MyMealPlanner.Web.Hubs;

// ═══════════════════════════════════════════════════════════════
// RECIPE HUB — real-time comments & reactions on recipe pages
// ═══════════════════════════════════════════════════════════════
[Authorize]
public class RecipeHub : Hub
{
    private readonly ApplicationDbContext _db;

    public RecipeHub(ApplicationDbContext db) => _db = db;

    public async Task JoinRecipeRoom(int recipeId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"recipe:{recipeId}");

    public async Task LeaveRecipeRoom(int recipeId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"recipe:{recipeId}");

    public async Task SendComment(int recipeId, string body, string? imageUrl)
    {
        var userId = Context.UserIdentifier!;

        var comment = new Comment
        {
            RecipeId  = recipeId,
            UserId    = userId,
            Body      = body[..Math.Min(body.Length, 2000)],
            ImagesJson = imageUrl is null ? null : $"[\"{imageUrl}\"]",
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        // Increment comment counter
        await _db.Recipes.Where(r => r.Id == recipeId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.CommentCount, r => r.CommentCount + 1));

        var user = await _db.Users.FindAsync(userId);

        // Broadcast to everyone in this recipe's room
        await Clients.Group($"recipe:{recipeId}").SendAsync("NewComment", new
        {
            id            = comment.Id,
            recipeId,
            userId,
            userName      = user?.FullName ?? "Anonymous",
            userAvatar    = user?.ProfilePhotoUrl,
            body          = comment.Body,
            imageUrl,
            createdAt     = comment.CreatedAt.ToString("o"),
            likeCount     = 0
        });
    }

    public async Task LikeComment(int commentId)
    {
        var userId  = Context.UserIdentifier!;
        var comment = await _db.Comments.FindAsync(commentId);
        if (comment is null) return;

        comment.LikeCount++;
        await _db.SaveChangesAsync();

        await Clients.Group($"recipe:{comment.RecipeId}").SendAsync("CommentLiked", new
        {
            commentId,
            newLikeCount = comment.LikeCount
        });
    }

    public async Task SendTypingIndicator(int recipeId, bool isTyping)
    {
        var userId = Context.UserIdentifier!;
        var user   = await _db.Users.FindAsync(userId);
        await Clients.OthersInGroup($"recipe:{recipeId}").SendAsync("UserTyping", new
        {
            userId,
            userName  = user?.FullName ?? "Someone",
            isTyping
        });
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is not null)
            {
                user.LastActiveAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
        await base.OnConnectedAsync();
    }
}

// ═══════════════════════════════════════════════════════════════
// CHAT HUB — DMs, community rooms, live chef sessions
// ═══════════════════════════════════════════════════════════════
[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _db;

    public ChatHub(ApplicationDbContext db) => _db = db;

    public async Task JoinRoom(int roomId)
    {
        var room = await _db.ChatRooms.FindAsync(roomId);
        if (room is null) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");
        await Clients.Group($"room:{roomId}").SendAsync("UserJoined", new
        {
            userId   = Context.UserIdentifier,
            roomId,
            memberCount = ++room.MemberCount
        });
        await _db.SaveChangesAsync();
    }

    public async Task LeaveRoom(int roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room:{roomId}");
        var room = await _db.ChatRooms.FindAsync(roomId);
        if (room is not null) { room.MemberCount = Math.Max(0, room.MemberCount - 1); }
        await _db.SaveChangesAsync();
    }

    public async Task SendMessage(int roomId, string body, string? imageUrl, int? sharedRecipeId)
    {
        var userId  = Context.UserIdentifier!;
        var message = new ChatMessage
        {
            RoomId          = roomId,
            SenderId        = userId,
            Body            = body[..Math.Min(body.Length, 4000)],
            ImageUrl        = imageUrl,
            SharedRecipeId  = sharedRecipeId,
            SentAt          = DateTime.UtcNow
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);

        await Clients.Group($"room:{roomId}").SendAsync("NewMessage", new
        {
            id              = message.Id,
            roomId,
            senderId        = userId,
            senderName      = user?.FullName ?? "Unknown",
            senderAvatar    = user?.ProfilePhotoUrl,
            body            = message.Body,
            imageUrl,
            sharedRecipeId,
            sentAt          = message.SentAt.ToString("o")
        });
    }

    public async Task SendTyping(int roomId, bool isTyping)
    {
        var user = await _db.Users.FindAsync(Context.UserIdentifier);
        await Clients.OthersInGroup($"room:{roomId}").SendAsync("Typing", new
        {
            userId   = Context.UserIdentifier,
            userName = user?.FullName ?? "Someone",
            isTyping
        });
    }

    public async Task MarkRead(int roomId)
    {
        var userId = Context.UserIdentifier!;
        await _db.ChatMessages
            .Where(m => m.RoomId == roomId && !m.IsRead && m.SenderId != userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
    }
}

// ═══════════════════════════════════════════════════════════════
// NOTIFICATION HUB — push notifications to connected users
// ═══════════════════════════════════════════════════════════════
[Authorize]
public class NotificationHub : Hub
{
    private readonly ApplicationDbContext _db;

    public NotificationHub(ApplicationDbContext db) => _db = db;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        await base.OnConnectedAsync();
    }

    public async Task MarkNotificationRead(int notificationId)
    {
        var userId = Context.UserIdentifier!;
        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (n is null) return;
        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var unreadCount = await _db.Notifications
            .CountAsync(x => x.UserId == userId && !x.IsRead);

        await Clients.User(userId).SendAsync("UnreadCountUpdated", unreadCount);
    }

    public async Task MarkAllRead()
    {
        var userId = Context.UserIdentifier!;
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));

        await Clients.User(userId).SendAsync("UnreadCountUpdated", 0);
    }
}
