using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cohort.Shared.Auth;

namespace Cohort.Web.Areas.Host.Controllers;

[Area("Host")]
[Authorize(Policy = AuthConstants.Policies.HostOnly)]
public class HomeController : Controller
{
    public IActionResult Index() => Redirect("/host");
}
