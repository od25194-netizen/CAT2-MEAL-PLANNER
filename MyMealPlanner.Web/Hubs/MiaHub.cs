using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using MyMealPlanner.Core.DTOs;
using MyMealPlanner.Core.Interfaces;
using MyMealPlanner.Core.Models;

namespace MyMealPlanner.Web.Hubs;

/// <summary>
/// "Mia" — AI cooking assistant hub.
/// Maintains per-connection conversation history so Mia remembers context
/// within a session. History is reset on reconnect (no cross-session memory).
/// </summary>
[Authorize]
public class MiaHub : Hub
{
    private readonly IAIChatAssistantService _mia;
    private readonly UserManager<ApplicationUser> _users;

    // Per-connection conversation history (in-memory, session only)
    private static readonly Dictionary<string, List<ChatTurn>> ConversationHistory = new();

    public MiaHub(IAIChatAssistantService mia, UserManager<ApplicationUser> users)
    {
        _mia   = mia;
        _users = users;
    }

    public async Task SendMessage(string message)
    {
        var userId = Context.UserIdentifier!;
        var connId = Context.ConnectionId;

        // Init history for this connection
        if (!ConversationHistory.ContainsKey(connId))
            ConversationHistory[connId] = new List<ChatTurn>();

        var history = ConversationHistory[connId];

        // Show typing indicator
        await Clients.Caller.SendAsync("MiaTyping", true);

        try
        {
            var response = await _mia.ChatAsync(userId, message, history);

            // Update history (keep last 10 turns to stay within token limits)
            history.Add(new ChatTurn("user",      message));
            history.Add(new ChatTurn("assistant", response ?? ""));
            if (history.Count > 20)
                history.RemoveRange(0, 2);

            await Clients.Caller.SendAsync("MiaTyping",  false);
            await Clients.Caller.SendAsync("MiaResponse", new
            {
                message  = response,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }
        catch
        {
            await Clients.Caller.SendAsync("MiaTyping",   false);
            await Clients.Caller.SendAsync("MiaResponse", new
            {
                message   = "Sorry, I'm having a little trouble right now. Please try again! 🍳",
                timestamp = DateTime.UtcNow.ToString("o"),
                isError   = true
            });
        }
    }

    public async Task ClearHistory()
    {
        ConversationHistory.Remove(Context.ConnectionId);
        await Clients.Caller.SendAsync("HistoryCleared");
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        ConversationHistory.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
