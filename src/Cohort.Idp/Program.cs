using Cohort.Idp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<IdpDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=cohort.idp.db";

    options.UseSqlite(connectionString);
    options.UseOpenIddict();
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<IdpDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
});

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<IdpDbContext>();
    })
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(builder.Configuration["Oidc:Issuer"] ?? "https://localhost:5001/"));

        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetEndSessionEndpointUris("/connect/logout");

        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange();

        options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email);

        // Dev-only certificates (replace with real certs in production).
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();

        // Keep tokens readable during development for troubleshooting.
        options.DisableAccessTokenEncryption();
    });

var app = builder.Build();

// Seed database, users, and OIDC client for development.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdpDbContext>();
    await db.Database.EnsureCreatedAsync();

    // EnsureCreated() won't update an existing SQLite schema.
    // If the local dev DB was created before adding custom ApplicationUser columns, recreate it.
    try
    {
        _ = await db.Users.Select(u => u.FirstName).Take(1).ToListAsync();
    }
    catch (SqliteException)
    {
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    async Task EnsureUserAsync(string email, string password, string firstName, string lastName, string empId, string? objectId)
    {
        var existing = await userManager.FindByNameAsync(email);
        if (existing is not null)
        {
            // If a stable object id (oid) was configured, keep it consistent across runs.
            // Identity primary keys can't be updated in-place reliably, so we recreate the user in dev.
            if (!string.IsNullOrWhiteSpace(objectId) && !string.Equals(existing.Id, objectId, StringComparison.OrdinalIgnoreCase))
            {
                var delete = await userManager.DeleteAsync(existing);
                if (!delete.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to recreate seed user '{email}' (delete failed): {string.Join(", ", delete.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                return;
            }
        }

        var user = new ApplicationUser
        {
            Id = string.IsNullOrWhiteSpace(objectId) ? Guid.NewGuid().ToString() : objectId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            EmpId = empId
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create seed user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    // Default dev users.
    var adminEmail = builder.Configuration["Seed:Admin:Email"] ?? "admin@example.com";
    var adminPassword = builder.Configuration["Seed:Admin:Password"] ?? "Pass123$";
    var adminFirst = builder.Configuration["Seed:Admin:FirstName"] ?? "Admin";
    var adminLast = builder.Configuration["Seed:Admin:LastName"] ?? "User";
    var adminEmpId = builder.Configuration["Seed:Admin:EmpId"] ?? "A001";
    var adminOid = builder.Configuration["Seed:Admin:Oid"]; 

    var hostEmail = builder.Configuration["Seed:Host:Email"] ?? "host@example.com";
    var hostPassword = builder.Configuration["Seed:Host:Password"] ?? "Pass123$";
    var hostFirst = builder.Configuration["Seed:Host:FirstName"] ?? "Host";
    var hostLast = builder.Configuration["Seed:Host:LastName"] ?? "User";
    var hostEmpId = builder.Configuration["Seed:Host:EmpId"] ?? "H001";
    var hostOid = builder.Configuration["Seed:Host:Oid"]; 

    await EnsureUserAsync(adminEmail, adminPassword, adminFirst, adminLast, adminEmpId, adminOid);
    await EnsureUserAsync(hostEmail, hostPassword, hostFirst, hostLast, hostEmpId, hostOid);

    var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    if (await appManager.FindByClientIdAsync("cohort-web") is null)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "cohort-web",
            ClientSecret = "dev-secret",
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Cohort Web",
            RedirectUris =
            {
                new Uri("https://localhost:5003/signin-oidc")
            },
            PostLogoutRedirectUris =
            {
                new Uri("https://localhost:5003/")
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Scopes.OpenId,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Email
            }
        };

        await appManager.CreateAsync(descriptor);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
