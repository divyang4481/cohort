using System.Security.Claims;
using Cohort.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohort.Web.Controllers.Api;

[ApiController]
[Route("api/participant")]
public sealed class ParticipantSessionController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("anonymous-signin")]
    public async Task<IActionResult> AnonymousSignIn([FromBody] AnonymousSignInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Name is required." });
        }

        var displayName = request.Name.Trim();
        var participantKey = Guid.NewGuid().ToString("N");
        var pseudonym = $"User-{Guid.NewGuid():N}"[..10];

        var claims = new List<Claim>
        {
            new("auth_source", "anonymous"),
            new(AuthConstants.Claims.ParticipantMode, "anonymous"),
            // Actual name (private) stored in DisplayName claim for internal use.
            new(AuthConstants.Claims.DisplayName, displayName),
            // Public-facing pseudonym
            new("pseudonym", pseudonym),
            new(AuthConstants.Claims.AppRole, AuthConstants.AppRoles.Participant),
            new(ClaimTypes.NameIdentifier, participantKey),
            // Identity name exposed to app as pseudonym.
            new(ClaimTypes.Name, pseudonym)
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "Cookies");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("Cookies", principal);

        return Ok(new { participantKey, displayName });
    }

    [Authorize]
    [HttpPost("signout")]
    public async Task<IActionResult> SignOutParticipant()
    {
        await HttpContext.SignOutAsync("Cookies");
        return NoContent();
    }

    public sealed record AnonymousSignInRequest(string Name);
}
