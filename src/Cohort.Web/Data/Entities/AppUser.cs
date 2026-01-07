namespace Cohort.Web.Data.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Stable external identity key for Admin/Host authorization.
    // Prefer Entra ID style: "oid" (object id). For other providers this may be "sub".
    public string Subject { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string EmpId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // Our app-side role/permission gate.
    // Values: "admin" | "host" (participants are handled separately)
    public string AppRole { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
