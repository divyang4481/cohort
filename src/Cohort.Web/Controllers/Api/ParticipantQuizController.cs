using System.Security.Claims;
using Cohort.Shared.Auth;
using Cohort.Web.Data;
using Cohort.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cohort.Web.Controllers.Api;

[ApiController]
[Route("api/participant/quizzes")]
public sealed class ParticipantQuizController : ControllerBase
{
    private readonly CohortWebDbContext _db;

    public ParticipantQuizController(CohortWebDbContext db)
    {
        _db = db;
    }

    // Used by the join page (anonymous): returns basic info about the quiz.
    [AllowAnonymous]
    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return NotFound();
        }

        var normalized = code.Trim().ToUpperInvariant();
        var quiz = await _db.Quizzes.AsNoTracking().FirstOrDefaultAsync(x => x.JoinCode == normalized);
        if (quiz is null)
        {
            return NotFound();
        }

        return Ok(new QuizPublicDto(
            quiz.Title,
            quiz.JoinCode,
            quiz.DurationSeconds,
            quiz.IsStarted,
            quiz.StartedUtc));
    }

    // Used by the room page: ensures the participant is recorded and returns room status.
    [Authorize(Policy = AuthConstants.Policies.ParticipantAnonymousOrOidc)]
    [HttpPost("{code}/enter")]
    public async Task<IActionResult> Enter(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return NotFound();
        }

        var normalized = code.Trim().ToUpperInvariant();
        var quiz = await _db.Quizzes.FirstOrDefaultAsync(x => x.JoinCode == normalized);
        if (quiz is null)
        {
            return NotFound();
        }

        var participantKey = User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? User.FindFirstValue("oid")
                             ?? User.FindFirstValue("sub")
                             ?? string.Empty;

        var realName = User.FindFirstValue(AuthConstants.Claims.DisplayName)
                   ?? "Participant";

        var pseudonym = User.FindFirstValue("pseudonym")
                ?? User.Identity?.Name
                ?? "Player";

        if (!string.IsNullOrWhiteSpace(participantKey))
        {
            var exists = await _db.QuizParticipants.AnyAsync(x => x.QuizId == quiz.Id && x.ParticipantKey == participantKey);
            if (!exists)
            {
                _db.QuizParticipants.Add(new QuizParticipant
                {
                    QuizId = quiz.Id,
                    ParticipantKey = participantKey,
                    RealName = realName,
                    Pseudonym = pseudonym
                });
                await _db.SaveChangesAsync();
            }
        }

        return Ok(new QuizRoomDto(
            quiz.Title,
            quiz.JoinCode,
            pseudonym,
            quiz.DurationSeconds,
            quiz.IsStarted,
            quiz.StartedUtc));
    }

    public sealed record QuizPublicDto(
        string Title,
        string JoinCode,
        int DurationSeconds,
        bool IsStarted,
        DateTimeOffset? StartedUtc);

    public sealed record QuizRoomDto(
        string Title,
        string JoinCode,
        string DisplayName,
        int DurationSeconds,
        bool IsStarted,
        DateTimeOffset? StartedUtc);
}
