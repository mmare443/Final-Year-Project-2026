using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace LCC_CMS_Api.Hubs;

/// <summary>
/// M8 Phase 3 — notification hub only. Chat body is never accepted here.
/// Group membership is bound in <see cref="OnConnectedAsync"/> from
/// ICurrentUser (lab: X-User-Id header or hub query). Subscribe cannot
/// choose another user's group. REST remains the source of truth.
/// </summary>
public class MessageHub : Hub
{
    private readonly ICurrentUser _currentUser;

    public MessageHub(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public override async Task OnConnectedAsync()
    {
        if (!await _currentUser.ResolveAsync(Context.ConnectionAborted)
            || _currentUser.UserId is not int userId)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Deprecated. Connection already joined the current user's group.
    /// Matching userId is a no-op; any other userId is rejected.
    /// </summary>
    public async Task Subscribe(int userId)
    {
        if (!await _currentUser.ResolveAsync(Context.ConnectionAborted)
            || _currentUser.UserId is not int currentUserId)
        {
            throw new HubException("Unauthorized.");
        }

        if (userId != currentUserId)
        {
            throw new HubException("Subscribe userId does not match the current user.");
        }
    }

    /// <summary>Deprecated. Group membership lasts for the connection.</summary>
    public Task Unsubscribe(int userId)
    {
        _ = userId;
        return Task.CompletedTask;
    }

    public static string GroupName(int userId) => $"user-{userId}";
}

public class MessageNotification
{
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public int RecipientId { get; set; }
}
