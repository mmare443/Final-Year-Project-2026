using Microsoft.AspNetCore.SignalR;

namespace LCC_CMS_Api.Hubs;

/// <summary>
/// M8 Phase 3 — notification hub only. Chat body is never accepted here.
/// Clients Subscribe(userId) to a per-user group; MessagesController notifies
/// after a row is saved. No JWT group mapping yet (AuthEnabled=false).
/// </summary>
public class MessageHub : Hub
{
    public Task Subscribe(int userId)
    {
        if (userId <= 0) return Task.CompletedTask;
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));
    }

    public Task Unsubscribe(int userId)
    {
        if (userId <= 0) return Task.CompletedTask;
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(userId));
    }

    public static string GroupName(int userId) => $"user-{userId}";
}

public class MessageNotification
{
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public int RecipientId { get; set; }
}
