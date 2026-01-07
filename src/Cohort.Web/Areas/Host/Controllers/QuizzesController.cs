using System.Security.Claims;
using Cohort.Shared.Auth;
using Cohort.Web.Data;
using Cohort.Web.Data.Entities;
using Cohort.Web.Hubs;
using Cohort.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace Cohort.Web.Areas.Host.Controllers;

[Area("Host")]
[Authorize(Policy = AuthConstants.Policies.HostOnly)]
public sealed class QuizzesController : Controller
{
    private readonly CohortWebDbContext _db;
    private readonly IHubContext<QuizHub> _hub;

    public QuizzesController(CohortWebDbContext db, IHubContext<QuizHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<IActionResult> Index()
    {
        await Task.CompletedTask;
        return Redirect("/host/quizzes");
    }

    [HttpGet]
    public IActionResult Create() => Redirect("/host/quizzes/new");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, int durationSeconds)
    {
        await Task.CompletedTask;
        return Redirect("/host/quizzes");
    }

    public async Task<IActionResult> Details(Guid id)
    {
        await Task.CompletedTask;
        return Redirect($"/host/quizzes/{id}");
    }

    [HttpGet]
    public async Task<IActionResult> Qr(Guid id)
    {
        var subject = GetSubjectOrThrow();

        var quiz = await _db.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CreatedBySubject == subject);

        if (quiz is null)
        {
            return NotFound();
        }

        var joinUrl = Url.Action("Join", "Quiz", new { area = "Participant", code = quiz.JoinCode }, Request.Scheme)
            ?? string.Empty;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 10);

        return File(png, "image/png");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(Guid id)
    {
        await Task.CompletedTask;
        return Redirect($"/host/quizzes/{id}");
    }

    private string GetSubjectOrThrow()
    {
        return User.FindFirstValue("oid")
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? throw new InvalidOperationException("Missing subject claim for host user.");
    }
}
