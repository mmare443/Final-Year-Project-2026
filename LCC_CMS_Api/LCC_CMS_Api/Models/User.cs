using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class User
{
    public int UserId { get; set; }

    public string EntraId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<Message> MessageRecipients { get; set; } = new List<Message>();

    public virtual ICollection<Message> MessageSenders { get; set; } = new List<Message>();

    public virtual Staff? Staff { get; set; }

    public virtual Student? Student { get; set; }
}
