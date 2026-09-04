namespace LCC_CMS_Api.Services;

/// <summary>
/// Request-scoped signed-in CMS user. Call <see cref="ResolveAsync"/>
/// before reading properties.
/// </summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? Role { get; }
    string? Email { get; }
    int? StudentId { get; }
    string? StudentNumber { get; }
    int? StaffId { get; }
    string? JobTitle { get; }

    /// <summary>
    /// Loads identity for this request. Safe to call more than once.
    /// Returns false when no lab header, Entra oid, or matching active user exists.
    /// </summary>
    Task<bool> ResolveAsync(CancellationToken cancellationToken = default);
}
