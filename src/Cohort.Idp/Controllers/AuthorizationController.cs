using System.Security.Claims;
using System.Net;
using Cohort.Idp.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Cohort.Idp.Controllers;

public class AuthorizationController(UserManager<ApplicationUser> userManager, IConfiguration configuration) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!result.Succeeded)
        {
            // Redirect to the login page (Identity cookie).
            var returnUrl = (Request.PathBase + Request.Path + Request.QueryString).ToString();
            return Redirect($"/account/login?returnUrl={WebUtility.UrlEncode(returnUrl)}");
        }

        var user = await userManager.GetUserAsync(result.Principal!)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        // Entra ID / Azure AD style:
        // - oid: stable object id (GUID) for the user in the tenant
        // - tid: tenant id (GUID)
        // - preferred_username: login name (often email/UPN)
        // - ver: token version (v2.0)
        // In this dev IdP, we use the Identity user.Id as the object id (oid) and also as the OIDC subject.
        var tenantId = configuration["Oidc:TenantId"]
                       ?? configuration["Seed:TenantId"]
                       ?? "00000000-0000-0000-0000-000000000000";

        var oid = user.Id;
        identity.AddClaim(OpenIddictConstants.Claims.Subject, oid);
        identity.AddClaim("oid", oid);
        identity.AddClaim("tid", tenantId);
        identity.AddClaim("ver", "2.0");

        var preferredUsername = user.UserName ?? user.Email ?? user.Id;
        identity.AddClaim(OpenIddictConstants.Claims.PreferredUsername, preferredUsername);

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            identity.AddClaim(OpenIddictConstants.Claims.Email, user.Email);
        }

        identity.AddClaim(OpenIddictConstants.Claims.Name, preferredUsername);

        if (!string.IsNullOrWhiteSpace(user.FirstName))
        {
            identity.AddClaim(OpenIddictConstants.Claims.GivenName, user.FirstName);
        }

        if (!string.IsNullOrWhiteSpace(user.LastName))
        {
            identity.AddClaim(OpenIddictConstants.Claims.FamilyName, user.LastName);
        }

        if (!string.IsNullOrWhiteSpace(user.EmpId))
        {
            identity.AddClaim("empid", user.EmpId);
        }

        foreach (var claim in identity.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        var principal = new ClaimsPrincipal(identity);

        // Minimal: always allow the basic identity scopes.
        principal.SetScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        // OpenIddict validates the token request before invoking this endpoint.
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal ?? throw new InvalidOperationException("The authentication ticket is missing.");

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // Put user identity claims in the id_token so the web app can establish a session.
        return claim.Type switch
        {
            OpenIddictConstants.Claims.Subject or
            "oid" or
            "tid" or
            "ver" or
            OpenIddictConstants.Claims.Name or
            OpenIddictConstants.Claims.PreferredUsername or
            OpenIddictConstants.Claims.Email or
            OpenIddictConstants.Claims.GivenName or
            OpenIddictConstants.Claims.FamilyName or
            "empid"
                => new[] { OpenIddictConstants.Destinations.IdentityToken },

            _ => Array.Empty<string>()
        };
    }
}
