using Cohort.Shared.Auth;
using Cohort.Web.Data;
using Cohort.Web.Data.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Cohort.Web.Services;

public static class DevAppUserSeeder
{
    // Convenience for local dev so you can log in immediately with the seeded IdP accounts.
    // In real usage, you would manage this table via your own admin UI/process.
    public static async Task SeedAsync(CohortWebDbContext db, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var adminEmail = configuration["Seed:Admin:Email"] ?? "admin@example.com";
        var adminFirst = configuration["Seed:Admin:FirstName"] ?? "Admin";
        var adminLast = configuration["Seed:Admin:LastName"] ?? "User";
        var adminEmpId = configuration["Seed:Admin:EmpId"] ?? "A001";
        var adminOid = configuration["Seed:Admin:Oid"];

        var hostEmail = configuration["Seed:Host:Email"] ?? "host@example.com";
        var hostFirst = configuration["Seed:Host:FirstName"] ?? "Host";
        var hostLast = configuration["Seed:Host:LastName"] ?? "User";
        var hostEmpId = configuration["Seed:Host:EmpId"] ?? "H001";
        var hostOid = configuration["Seed:Host:Oid"];

        static string ChooseSubject(string? oid, string email)
            => !string.IsNullOrWhiteSpace(oid) ? oid : email;

        var adminSubject = ChooseSubject(adminOid, adminEmail);
        var hostSubject = ChooseSubject(hostOid, hostEmail);

        // If the DB already has rows (older dev runs), repair Subject=email -> Subject=oid when possible.
        // This avoids breaking DB-gated authorization when we switch to Entra-style "oid".
        if (await db.AppUsers.AnyAsync(cancellationToken))
        {
            await RepairSubjectIfNeededAsync(db, adminEmail, adminSubject, cancellationToken);
            await RepairSubjectIfNeededAsync(db, hostEmail, hostSubject, cancellationToken);
            return;
        }

        db.AppUsers.AddRange(
            new AppUser
            {
                Subject = adminSubject,
                Email = adminEmail,
                FirstName = adminFirst,
                LastName = adminLast,
                EmpId = adminEmpId,
                DisplayName = $"{adminFirst} {adminLast}".Trim(),
                AppRole = AuthConstants.AppRoles.Admin,
                IsActive = true
            },
            new AppUser
            {
                Subject = hostSubject,
                Email = hostEmail,
                FirstName = hostFirst,
                LastName = hostLast,
                EmpId = hostEmpId,
                DisplayName = $"{hostFirst} {hostLast}".Trim(),
                AppRole = AuthConstants.AppRoles.Host,
                IsActive = true
            });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task RepairSubjectIfNeededAsync(CohortWebDbContext db, string email, string expectedSubject, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedSubject) || string.Equals(expectedSubject, email, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // If a row already exists with the expected subject, don't change anything.
        var bySubject = await db.AppUsers.FirstOrDefaultAsync(x => x.Subject == expectedSubject, cancellationToken);
        if (bySubject is not null)
        {
            return;
        }

        // Update the row that matches the email.
        var byEmail = await db.AppUsers.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (byEmail is null)
        {
            return;
        }

        var oldSubject = byEmail.Subject;
        byEmail.Subject = expectedSubject;
        byEmail.UpdatedUtc = DateTimeOffset.UtcNow;

        // Preserve dev data created under the old Subject scheme.
        if (!string.IsNullOrWhiteSpace(oldSubject) && !string.Equals(oldSubject, expectedSubject, StringComparison.OrdinalIgnoreCase))
        {
            var quizzes = await db.Quizzes.Where(q => q.CreatedBySubject == oldSubject).ToListAsync(cancellationToken);
            if (quizzes.Count > 0)
            {
                foreach (var quiz in quizzes)
                {
                    quiz.CreatedBySubject = expectedSubject;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
