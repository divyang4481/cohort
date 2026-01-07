using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cohort.Shared.Auth;

namespace Cohort.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthConstants.Policies.AdminOnly)]
public class HomeController : Controller
{
    public IActionResult Index() => Redirect("/admin");
}
