using System.Security.Claims;
using Cohort.Web.Data;
using Cohort.Shared.Auth;
using Cohort.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Microsoft.Extensions.FileProviders;

// Choose content/web roots that work in both dev (repo root or project dir) and publish (app base).
// Angular 21+ creates a "browser" subfolder automatically with @angular/build:application.
string? spaRootPath = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "browser"),
    Path.Combine(Directory.GetCurrentDirectory(), "src", "Cohort.Web", "wwwroot", "browser"),
    Path.Combine(AppContext.BaseDirectory, "wwwroot", "browser")
}.FirstOrDefault(Directory.Exists);

var options = spaRootPath is not null
    ? new WebApplicationOptions
    {
        // content root is the parent of wwwroot
        ContentRootPath = Directory.GetParent(spaRootPath)?.Parent?.FullName ?? Directory.GetCurrentDirectory(),
        WebRootPath = spaRootPath
    }
    : new WebApplicationOptions();

var builder = WebApplication.CreateBuilder(options);

builder.Services.AddControllersWithViews()
    .AddApplicationPart(typeof(Cohort.Web.Controllers.Api.HostQuizzesController).Assembly)
    .AddJsonOptions(options =>
    {
        // Use camelCase for JSON properties so TypeScript/Angular clients use standard naming.
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddDbContext<CohortWebDbContext>(options =>
{
    var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
    var connectionString = builder.Configuration.GetConnectionString("CohortWeb")
        ?? "Data Source=cohort-web.db";

    switch (provider.Trim().ToLowerInvariant())
    {
        case "sqlite":
            options.UseSqlite(connectionString);
            break;
        case "sqlserver":
            options.UseSqlServer(connectionString);
            break;
        case "postgres":
        case "postgresql":
            options.UseNpgsql(connectionString);
            break;
        default:
            throw new InvalidOperationException($"Unsupported Database:Provider '{provider}'. Use Sqlite, SqlServer, or PostgreSQL.");
    }
});

builder.Services.AddScoped<AppUserAuthorizer>();

builder.Services.AddSignalR();

// Sanitizer for rich text: allow basic HTML formatting, links, and images
var sanitizer = new Ganss.Xss.HtmlSanitizer();
sanitizer.AllowedTags.Clear();
sanitizer.AllowedTags.UnionWith(new[]
{
    "p", "br", "strong", "em", "u", "s", "code", "pre", "blockquote",
    "ul", "ol", "li", "span", "div", "h1", "h2", "h3", "h4",
    "img", "a"
});
sanitizer.AllowedAttributes.Clear();
sanitizer.AllowedAttributes.UnionWith(new[]
{
    "href", "title", "target", "rel", // links
    "src", "alt" // images
});
sanitizer.AllowedSchemes.Clear();
sanitizer.AllowedSchemes.UnionWith(new[] { "http", "https" });
sanitizer.AllowDataAttributes = false;
sanitizer.AllowedCssProperties.Clear();

builder.Services.AddSingleton(sanitizer);

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Select(o => o.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredCors", policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                // DEBUG LOGGING
                Console.WriteLine($"[DEBUG] OnRedirectToLogin: {context.Request.Path}");

                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    Console.WriteLine("[DEBUG] Cookie: Intercepting /api request, returning 401.");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                // If an anonymous participant tries to access an OIDC-only area, send them to the IdP login
                // rather than showing a 403.
                var authSource = context.HttpContext.User.FindFirst("auth_source")?.Value;
                var isHostOrAdmin = context.Request.Path.StartsWithSegments("/Host") || context.Request.Path.StartsWithSegments("/Admin");

                if (isHostOrAdmin && string.Equals(authSource, "oidc", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect("/access/not-authorized");
                    return Task.CompletedTask;
                }

                if (isHostOrAdmin && !string.Equals(authSource, "oidc", StringComparison.OrdinalIgnoreCase))
                {
                    var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                    var loginUrl = "/auth/login?returnUrl=" + Uri.EscapeDataString(returnUrl);
                    context.Response.Redirect(loginUrl);
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = builder.Configuration["Auth:Oidc:Authority"] ?? "https://localhost:5001";
        options.ClientId = builder.Configuration["Auth:Oidc:ClientId"] ?? "cohort-web";
        options.ClientSecret = builder.Configuration["Auth:Oidc:ClientSecret"] ?? "dev-secret";
        options.ResponseType = "code";
        options.SaveTokens = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = async context =>
            {
                // Tag the source of authentication so policies can distinguish OIDC vs anonymous participant.
                if (context.Principal?.Identity is ClaimsIdentity id)
                {
                    id.AddClaim(new Claim("auth_source", "oidc"));
                    id.AddClaim(new Claim(AuthConstants.Claims.ParticipantMode, "oidc"));

                    // Our app-side authorization (DB-backed): Admin/Host are allowed only if present in Cohort.Web DB.
                    var authorizer = context.HttpContext.RequestServices.GetRequiredService<AppUserAuthorizer>();
                    var role = await authorizer.ResolveAppRoleForOidcUserAsync(context.Principal, context.HttpContext.RequestAborted);
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        id.AddClaim(new Claim(AuthConstants.Claims.AppRole, role));
                    }
                }

                return;
            },
            OnRedirectToIdentityProvider = context =>
            {
                // DEBUG LOGGING
                Console.WriteLine($"[DEBUG] OnRedirectToIdentityProvider: {context.Request.Path}");

                // Prevent redirect for API calls; return 401 instead to avoid CORS errors.
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    Console.WriteLine("[DEBUG] Intercepting /api request, returning 401.");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.HandleResponse();
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.Policies.AdminOnly, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim("auth_source", "oidc")
            .RequireClaim(AuthConstants.Claims.AppRole, AuthConstants.AppRoles.Admin));

    options.AddPolicy(AuthConstants.Policies.HostOnly, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim("auth_source", "oidc")
            .RequireClaim(AuthConstants.Claims.AppRole, AuthConstants.AppRoles.Host));

    // Participant can be OIDC or anonymous; both become authenticated via the app cookie.
    // Note: we only DB-authorize admin/host. Participants are governed by the Participant flow.
    options.AddPolicy(AuthConstants.Policies.ParticipantAnonymousOrOidc, policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(ctx =>
            {
                // Anonymous participant (our cookie)
                if (ctx.User.HasClaim(AuthConstants.Claims.AppRole, AuthConstants.AppRoles.Participant))
                {
                    return true;
                }

                // OIDC participant mode (future / optional): allow if explicitly marked.
                return string.Equals(ctx.User.FindFirst("auth_source")?.Value, "oidc", StringComparison.OrdinalIgnoreCase)
                       && string.Equals(ctx.User.FindFirst(AuthConstants.Claims.ParticipantMode)?.Value, "oidc", StringComparison.OrdinalIgnoreCase);
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CohortWebDbContext>();
    await db.Database.EnsureCreatedAsync();

    // EnsureCreated() won't add new tables/columns to an existing database file.
    // In dev, if the local DB is missing newer schema, recreate it.
    var needsRecreate = false;

    if (db.Database.IsSqlite())
    {
        // Avoid throwing noisy query errors by inspecting schema directly.
        var hasAppUsers = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='AppUsers'")
            .SingleAsync() > 0;

        var hasQuizzes = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='Quizzes'")
            .SingleAsync() > 0;

        var hasQuizQuestions = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='QuizQuestions'")
            .SingleAsync() > 0;

        var hasQuizParticipants = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='QuizParticipants'")
            .SingleAsync() > 0;

        var hasQuizQuestionOptions = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='QuizQuestionOptions'")
            .SingleAsync() > 0;

        var hasParticipantRealName = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('QuizParticipants') WHERE name='RealName'")
            .SingleAsync() > 0;

        var hasParticipantPseudonym = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('QuizParticipants') WHERE name='Pseudonym'")
            .SingleAsync() > 0;

        var hasQuestionType = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('QuizQuestions') WHERE name='QuestionType'")
            .SingleAsync() > 0;

        var hasTargetQuestionCount = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Quizzes') WHERE name='TargetQuestionCount'")
            .SingleAsync() > 0;

        var hasIsPublished = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Quizzes') WHERE name='IsPublished'")
            .SingleAsync() > 0;

        var hasPublishedUtc = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Quizzes') WHERE name='PublishedUtc'")
            .SingleAsync() > 0;

        // Column presence check (will be 0 if missing).
        var hasDefaultTimeoutColumn = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Quizzes') WHERE name='DefaultQuestionTimeoutSeconds'")
            .SingleAsync() > 0;

        needsRecreate = !hasAppUsers
            || !hasQuizzes
            || !hasQuizQuestions
            || !hasQuizParticipants
            || !hasQuizQuestionOptions
            || !hasDefaultTimeoutColumn
            || !hasParticipantRealName
            || !hasParticipantPseudonym
            || !hasQuestionType
            || !hasTargetQuestionCount
            || !hasIsPublished
            || !hasPublishedUtc;
    }
    else
    {
        // Provider-agnostic fallback: touch the new bits; recreate on any failure.
        try
        {
            _ = await db.AppUsers.AnyAsync();
            _ = await db.QuizQuestions.AnyAsync();
            _ = await db.Quizzes.Select(x => x.DefaultQuestionTimeoutSeconds).OrderBy(x => x).Take(1).ToListAsync();
        }
        catch
        {
            needsRecreate = true;
        }
    }

    if (needsRecreate)
    {
        await db.Database.CloseConnectionAsync();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    await DevAppUserSeeder.SeedAsync(db, app.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve SPA assets from wwwroot/browser (the configured web root) if it exists.
var spaWebRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot", "browser");
if (Directory.Exists(spaWebRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(spaWebRoot),
        RequestPath = ""
    });
}

// Also serve any top-level wwwroot files (e.g., 3rdpartylicenses.txt) if present.
var topLevelWwwRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(topLevelWwwRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(topLevelWwwRoot),
        RequestPath = ""
    });
}

// Map API/controller routes BEFORE the SPA fallback so they take precedence.
app.UseRouting();

app.UseCors("ConfiguredCors");

// Middleware to prevent API redirects to login/IDP -> force 401 instead.
// Middleware to prevent API redirects to login/IDP -> force 401 instead.
app.Use(async (context, next) =>
{
    // Intercept redirects (302/301) for API calls and return 401 instead
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        await next();

        if (context.Response.StatusCode == StatusCodes.Status302Found || 
            context.Response.StatusCode == StatusCodes.Status301MovedPermanently)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.Remove("Location");
        }
        return;
    }

    await next();

    if (context.Request.Path.StartsWithSegments("/api"))
    {
        Console.WriteLine($"[DEBUG] Middleware END: {context.Request.Path}, StatusCode: {context.Response.StatusCode}");
    }

    // Check if it's an API call that was redirected (likely to login).
    if (context.Request.Path.StartsWithSegments("/api") && 
        (context.Response.StatusCode == StatusCodes.Status302Found || 
         context.Response.StatusCode == StatusCodes.Status301MovedPermanently)) 
    {
        Console.WriteLine($"[DEBUG] Middleware: Intercepting {context.Response.StatusCode} for {context.Request.Path}, forcing 401.");
        
        // Clear the redirect location and set 401
        context.Response.GetTypedHeaders().Location = null;
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }
});

app.UseAuthentication();
app.UseAuthorization();

// API and SignalR hubs must be explicitly mapped before fallback.
app.MapControllers();
app.MapHub<Cohort.Web.Hubs.QuizHub>("/hubs/quiz");

// Legacy MVC routes (moved here for clarity, after API routes).
app.MapControllerRoute(
    name: "legacy-areas",
    pattern: "legacy/{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "legacy-default",
    pattern: "legacy/{controller=Home}/{action=Index}/{id?}");

// Final SPA fallback: any request not matched by static files, API, or hubs → index.html
// Use the SPA web root's file provider to serve index.html for all unmatched routes.
// Final SPA fallback: any request not matched by static files, API, or hubs → index.html
// Use the SPA web root's file provider to serve index.html for all unmatched routes.
// Final SPA fallback: any request not matched by static files, API, or hubs → index.html
// Use the SPA web root's file provider to serve index.html for all unmatched routes.
var spaIndexPath = "index.html";
if (!string.IsNullOrEmpty(spaWebRoot) && Directory.Exists(spaWebRoot))
{
    var spaFileProvider = new PhysicalFileProvider(spaWebRoot);
    app.MapFallback(() =>
    {
        var file = spaFileProvider.GetFileInfo(spaIndexPath);
        if (file.Exists)
        {
            // Explicitly cast or return Results.File to match Func<IResult>
            return Results.File(file.PhysicalPath!, "text/html");
        }
        return Results.NotFound();
    });
}
else
{
    // Fallback if SPA root not found; try serving from wwwroot/browser directly
    var fallbackPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "browser", "index.html");
    if (File.Exists(fallbackPath))
    {
        app.MapFallback(() => Results.File(fallbackPath, "text/html"));
    }
}

app.Run();
