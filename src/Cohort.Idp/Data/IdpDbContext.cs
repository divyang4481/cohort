using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cohort.Idp.Data;

public class IdpDbContext(DbContextOptions<IdpDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Registers the OpenIddict entity sets.
        builder.UseOpenIddict();
    }
}
