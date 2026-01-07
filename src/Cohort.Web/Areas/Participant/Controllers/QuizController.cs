using System.Security.Claims;
using Cohort.Shared.Auth;
using Cohort.Web.Data;
using Cohort.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cohort.Web.Areas.Participant.Controllers;

[Area("Participant")]
public sealed class QuizController : Controller
{
    private readonly CohortWebDbContext _db;

    public QuizController(CohortWebDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("legacy/participant/join/{code}")]
    public async Task<IActionResult> Join(string code)
    {
        await Task.CompletedTask;
        return Redirect($"/participant/join/{code?.Trim()}");
    }

    [Authorize(Policy = AuthConstants.Policies.ParticipantAnonymousOrOidc)]
    [HttpGet("legacy/participant/room/{code}")]
    public async Task<IActionResult> Room(string code)
    {
        await Task.CompletedTask;
        return Redirect($"/participant/room/{code?.Trim()}");
    }
}
