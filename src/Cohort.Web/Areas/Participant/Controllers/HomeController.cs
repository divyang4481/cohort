using System.Security.Claims;
using Cohort.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohort.Web.Areas.Participant.Controllers;

[Area("Participant")]
public class HomeController : Controller
{
    [Authorize(Policy = AuthConstants.Policies.ParticipantAnonymousOrOidc)]
    public IActionResult Index() => Redirect("/participant");

    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignIn() => Redirect("/participant/signin");

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(string name, string? returnUrl = null)
    {
        // Prefer the SPA flow via POST /api/participant/anonymous-signin.
        await Task.CompletedTask;
        return Redirect("/participant/signin");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutParticipant()
    {
        await HttpContext.SignOutAsync("Cookies");
        return Redirect("/participant/signin");
    }
}
