using LCC_CMS_Api.Hubs;
using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M8 Phase 2–3 — persisted staff–student messages. SignalR notifies after
/// save; this API remains the source of truth. Inbox/sent/send use
/// ICurrentUser (lab: X-User-Id). Query userId and body SenderId are ignored.
/// Soft-delete sets IsDeleted.
/// </summary>
[ApiController]
[Authorize]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;
    private readonly IHubContext<MessageHub> _messageHub;
    private readonly ICurrentUser _currentUser;

    public MessagesController(
        LccCmsDbContext dbContext,
        IHubContext<MessageHub> messageHub,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _messageHub = messageHub;
        _currentUser = currentUser;
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<IEnumerable<MessageRecord>>> GetInbox(
        [FromQuery] int? userId,
        CancellationToken cancellationToken)
    {
        _ = userId;
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is not int currentUserId)
        {
            return Unauthorized();
        }

        var messages = await MessageGraph()
            .AsNoTracking()
            .Where(m => m.RecipientId == currentUserId && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.MessageId)
            .ToListAsync(cancellationToken);

        return Ok(messages.Select(ToRecord));
    }

    [HttpGet("sent")]
    public async Task<ActionResult<IEnumerable<MessageRecord>>> GetSent(
        [FromQuery] int? userId,
        CancellationToken cancellationToken)
    {
        _ = userId;
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is not int currentUserId)
        {
            return Unauthorized();
        }

        var messages = await MessageGraph()
            .AsNoTracking()
            .Where(m => m.SenderId == currentUserId && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.MessageId)
            .ToListAsync(cancellationToken);

        return Ok(messages.Select(ToRecord));
    }

    [HttpPost]
    public async Task<ActionResult<MessageRecord>> SendMessage(
        [FromBody] MessageWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is not int senderId)
        {
            return Unauthorized();
        }

        var content = request.Content?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest("Content is required.");
        }
        if (content.Length > 2000)
        {
            return BadRequest("Content cannot exceed 2000 characters.");
        }
        if (request.RecipientId <= 0) return BadRequest("Recipient is required.");
        if (senderId == request.RecipientId)
        {
            return BadRequest("Sender and recipient must be different.");
        }

        var sender = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == senderId, cancellationToken);
        if (sender is null) return Unauthorized();

        var recipient = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == request.RecipientId, cancellationToken);
        if (recipient is null) return BadRequest("Recipient was not found.");

        if (!IsStaffStudentPair(sender.Role, recipient.Role))
        {
            return BadRequest("Messages are only allowed between a staff member and a student.");
        }

        var message = new Message
        {
            SenderId = sender.UserId,
            RecipientId = recipient.UserId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsDeleted = false,
        };
        _dbContext.Messages.Add(message);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var messageText))
        {
            return StatusCode(status, messageText);
        }

        message.Sender = sender;
        message.Recipient = recipient;

        try
        {
            await _messageHub.Clients
                .Group(MessageHub.GroupName(recipient.UserId))
                .SendAsync("InboxUpdated", new MessageNotification
                {
                    MessageId = message.MessageId,
                    SenderId = message.SenderId,
                    RecipientId = message.RecipientId,
                });
        }
        catch
        {
            // Persist succeeded; notification is best-effort.
        }

        return Ok(ToRecord(message));
    }

    [HttpPut("{id}/delete")]
    public async Task<ActionResult<MessageRecord>> SoftDelete(
        int id,
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken)
            || _currentUser.UserId is not int currentUserId)
        {
            return Unauthorized();
        }

        var message = await MessageGraph()
            .FirstOrDefaultAsync(m => m.MessageId == id, cancellationToken);
        if (message is null) return NotFound();
        if (message.IsDeleted) return NotFound();
        if (message.SenderId != currentUserId && message.RecipientId != currentUserId)
        {
            return Forbid();
        }

        message.IsDeleted = true;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (TryDescribePersistenceFailure(ex, out var status, out var messageText))
        {
            return StatusCode(status, messageText);
        }

        return Ok(ToRecord(message));
    }

    private IQueryable<Message> MessageGraph()
    {
        return _dbContext.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipient);
    }

    private static bool IsStaffStudentPair(string senderRole, string recipientRole)
    {
        var senderStudent = IsStudentRole(senderRole);
        var recipientStudent = IsStudentRole(recipientRole);
        var senderStaff = IsStaffRole(senderRole);
        var recipientStaff = IsStaffRole(recipientRole);
        return (senderStudent && recipientStaff) || (senderStaff && recipientStudent);
    }

    private static bool IsStudentRole(string? role) =>
        string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase);

    private static bool IsStaffRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase)
            || role.Equals("HoD", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Registrar/Admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("RegistrarAdmin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Management/Principal", StringComparison.OrdinalIgnoreCase)
            || role.Equals("ManagementPrincipal", StringComparison.OrdinalIgnoreCase);
    }

    private static MessageRecord ToRecord(Message message)
    {
        return new MessageRecord
        {
            Id = message.MessageId,
            SenderId = message.SenderId,
            SenderEmail = message.Sender?.Email ?? "",
            RecipientId = message.RecipientId,
            RecipientEmail = message.Recipient?.Email ?? "",
            Content = message.Content,
            SentAt = message.SentAt,
            IsDeleted = message.IsDeleted,
        };
    }

    private static bool TryDescribePersistenceFailure(DbUpdateException ex, out int status, out string message)
    {
        status = StatusCodes.Status400BadRequest;
        message = "Could not save the message.";

        if (ex.InnerException is not SqlException sql)
        {
            return false;
        }

        if (sql.Number == 547)
        {
            message = "A related record was not found, or the message violates a database rule.";
            return true;
        }

        return false;
    }
}

public class MessageRecord
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public string SenderEmail { get; set; } = "";
    public int RecipientId { get; set; }
    public string RecipientEmail { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime SentAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class MessageWriteRequest
{
    /// <summary>Ignored. Sender is always the current user.</summary>
    public int SenderId { get; set; }
    public int RecipientId { get; set; }
    public string Content { get; set; } = "";
}
