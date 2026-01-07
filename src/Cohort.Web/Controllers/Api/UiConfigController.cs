using Microsoft.AspNetCore.Mvc;

namespace Cohort.Web.Controllers.Api;

[ApiController]
[Route("api/ui-config")]
public sealed class UiConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public UiConfigController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        // Bootstrap classes only (keeps styling simple and consistent).
        // These can be overridden via appsettings.json / appsettings.*.json.
        var adminNavbarClass = _configuration["Ui:Themes:Admin:NavbarClass"] ?? "navbar-dark bg-dark";
        var hostNavbarClass = _configuration["Ui:Themes:Host:NavbarClass"] ?? "navbar-dark bg-primary";
        var participantNavbarClass = _configuration["Ui:Themes:Participant:NavbarClass"] ?? "navbar-dark bg-success";

        return Ok(new
        {
            themes = new
            {
                admin = new { navbarClass = adminNavbarClass },
                host = new { navbarClass = hostNavbarClass },
                participant = new { navbarClass = participantNavbarClass },
                defaultTheme = new { navbarClass = hostNavbarClass }
            }
        });
    }
}
