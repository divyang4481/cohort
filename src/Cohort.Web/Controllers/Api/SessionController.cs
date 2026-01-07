using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Cohort.Web.Controllers.Api;

[ApiController]
[Route("api/session")]
public sealed class SessionController : ControllerBase
{
    // For SPA usage: clears the local app cookie (works for both anonymous participant and OIDC sessions).
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return NoContent();
    }
}
