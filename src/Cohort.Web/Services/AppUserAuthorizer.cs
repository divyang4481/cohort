using System.Security.Claims;
using Cohort.Shared.Auth;
using Cohort.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Cohort.Web.Services;

public sealed class AppUserAuthorizer
{
    private readonly CohortWebDbContext _db;

    public AppUserAuthorizer(CohortWebDbContext db)
    {
        _db = db;
    }

    public async Task<string?> ResolveAppRoleForOidcUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        // In Entra ID, the stable user key is typically "oid" (object id). Prefer it.
        // Fall back to standard OIDC claims for compatibility with other providers.
        var subject = principal.FindFirstValue("oid")
                      ?? principal.FindFirstValue("sub")
                      ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var user = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Subject == subject && x.IsActive, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // Only admin/host are governed here. Participants are handled via Participant area / flow.
        if (string.Equals(user.AppRole, AuthConstants.AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return AuthConstants.AppRoles.Admin;
        }

        if (string.Equals(user.AppRole, AuthConstants.AppRoles.Host, StringComparison.OrdinalIgnoreCase))
        {
            return AuthConstants.AppRoles.Host;
        }

        return null;
    }
}
