using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohort.Web.Controllers;

public sealed class AccessController : Controller
{
    [AllowAnonymous]
    [HttpGet("access/not-authorized")]
    public IActionResult NotAuthorized() => View();
}
