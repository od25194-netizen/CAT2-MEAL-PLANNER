using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMealPlanner.Core.Enums;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;
using MyMealPlanner.Infrastructure.Data;
using MyMealPlanner.Web.Hubs;

namespace MyMealPlanner.Services.Notifications;

/// <summary>
/// Sends notifications via SignalR (real-time) and persists to DB.
/// Supports bulk sends, scheduled daily/weekly jobs, and re-engagement campaigns.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationService> _logger;

    private static readonly Dictionary<NotificationType, string> TypeIcons = new()
    {
        [NotificationType.DailyMealSuggestion]      = "🍽️",
        [NotificationType.WeeklyMealPlanReady]       = "📅",
        [NotificationType.TrendingInYourCountry]     = "🔥",
        [NotificationType.NewRecipeFromFollowed]     = "👨‍🍳",
        [NotificationType.UnseenContentAlert]        = "👀",
        [NotificationType.QuizReadyToTake]           = "📝",
        [NotificationType.MealTimeReminder]          = "⏰",
        [NotificationType.HealthTipOfTheDay]         = "💚",
        [NotificationType.SeasonalFoodAlert]         = "🌿",
        [NotificationType.HydrationReminder]         = "💧",
        [NotificationType.SomeoneCommentedYours]     = "💬",
        [NotificationType.RecipeSuggestionApproved]  = "🎉",
        [NotificationType.LevelUpAlert]              = "🌟",
        [NotificationType.BadgeEarned]               = "🏅",
        [NotificationType.FollowerMilestone]         = "👥",
        [NotificationType.BasedOnYourHistory]        = "✨",
        [NotificationType.IngredientDealNearby]      = "🛒",
        [NotificationType.ExpertAnsweredYou]         = "🎓",
        [NotificationType.CookingClassReminder]      = "📚",
        [NotificationType.NewJokeOfTheDay]           = "😂",
        [NotificationType.ChefGoesLive]              = "📺",
        [NotificationType.ChallengeStarting]         = "🏆",
    };

    public NotificationService(
        ApplicationDbContext db,
        IHubContext<NotificationHub> hub,
        ILogger<NotificationService> logger)
    {
        _db     = db;
        _hub    = hub;
        _logger = logger;
    }

    public async Task SendAsync(
        string userId, NotificationType type, string title, string body,
        string? actionUrl = null)
    {
        // Check user notification preferences
        var user = await _db.Users.FindAsync(userId);
        if (user is null || !user.PushNotifications) return;

        var notification = new Notification
        {
            UserId    = userId,
            Type      = type,
            Title     = title,
            Body      = body,
            ActionUrl = actionUrl,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        // Push via SignalR
        try
        {
            await _hub.Clients.User(userId).SendAsync("NewNotification", new
            {
                notification.Id,
                notification.Type,
                icon      = TypeIcons.GetValueOrDefault(type, "🔔"),
                notification.Title,
                notification.Body,
                notification.ActionUrl,
                notification.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Notifications] SignalR push failed for user {Id}", userId);
        }
    }

    public async Task SendBulkAsync(
        List<string> userIds, NotificationType type, string title, string body)
    {
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id) && u.PushNotifications && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync();

        var notifications = users.Select(uid => new Notification
        {
            UserId    = uid,
            Type      = type,
            Title     = title,
            Body      = body,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[Notifications] Bulk sent {Type} to {Count} users", type, users.Count);
    }

    public async Task MarkReadAsync(int notificationId, string userId)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == userId);
        if (n is null) return;
        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(string userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    public async Task<List<Notification>> GetUnreadAsync(string userId, int count = 20)
        => await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToListAsync();

    // ── Scheduled Jobs (called from Hangfire) ─────────────────
    public async Task ScheduleDailyMealSuggestionsAsync()
    {
        var users = await _db.Users
            .Where(u => !u.IsDeleted && u.EmailNotifications)
            .Select(u => new { u.Id, u.CountryCode, u.FullName })
            .ToListAsync();

        foreach (var user in users)
        {
            // Get top recipe for their country
            var recipe = await _db.DishRankings
                .Where(r => r.Scope == RankingScope.Country && r.ScopeValue == user.CountryCode && r.RankPosition == 1)
                .Include(r => r.Recipe)
                .FirstOrDefaultAsync();

            var title = recipe?.Recipe != null
                ? $"Today's top dish: {recipe.Recipe.Title} 🍽️"
                : "Discover today's trending recipes";

            await SendAsync(user.Id, NotificationType.DailyMealSuggestion, title,
                "Tap to explore and start cooking!", "/Recipe?scope=Country");
        }
    }

    public async Task ScheduleWeeklyMealPlansAsync()
    {
        var users = await _db.Users
            .Where(u => !u.IsDeleted && u.PushNotifications)
            .Select(u => u.Id)
            .ToListAsync();

        await SendBulkAsync(users, NotificationType.WeeklyMealPlanReady,
            "Your personalised meal plan is ready! 📅",
            "Your week of delicious meals has been prepared. Tap to view.");
    }

    public async Task SendReEngagementAsync()
    {
        // Users inactive for 3+ days
        var cutoff = DateTime.UtcNow.AddDays(-3);
        var inactive = await _db.Users
            .Where(u => !u.IsDeleted && u.PushNotifications &&
                       (u.LastActiveAt == null || u.LastActiveAt < cutoff))
            .Select(u => u.Id)
            .Take(1000)
            .ToListAsync();

        await SendBulkAsync(inactive, NotificationType.UnseenContentAlert,
            "You've been away! Here's what you missed 👀",
            "New recipes, trending dishes, and community activity are waiting for you.");
    }
}
