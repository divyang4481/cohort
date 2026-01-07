using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohort.Web.Controllers;

public class AuthController : Controller
{
    [AllowAnonymous]
    [HttpGet("auth/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("auth/logout")]
    public IActionResult Logout()
    {
        // Sign out locally and at the IdP.
        return SignOut(new AuthenticationProperties { RedirectUri = Url.Content("~/") },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
