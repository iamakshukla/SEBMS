namespace SEBMS.Domain.Entities;

/// <summary>
/// One row per change an admin made to someone else's data. Append-only: nothing in the application edits or deletes
/// these. Emails are copied in (not joined) so an entry stays readable after the user it concerns has been deleted.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public string AdminUserId { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>"Update" or "Delete".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>"Transaction", "Budget", "Category" or "User".</summary>
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;

    /// <summary>The user whose data was changed.</summary>
    public string TargetUserId { get; set; } = string.Empty;
    public string TargetEmail { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    /// <summary>For updates the changed fields ("Amount: 800.00 → 950.50"); for deletes a snapshot of what was removed.</summary>
    public string? Details { get; set; }
}
