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
using Ganss.Xss;

namespace Cohort.Web.Controllers.Api;

[ApiController]
[Route("api/host/quizzes")]
[Authorize(Policy = AuthConstants.Policies.HostOnly)]
public sealed class HostQuizzesController : ControllerBase
{
    private readonly CohortWebDbContext _db;
    private readonly IHubContext<QuizHub> _hub;
    private readonly HtmlSanitizer _sanitizer;

    public HostQuizzesController(CohortWebDbContext db, IHubContext<QuizHub> hub, HtmlSanitizer sanitizer)
    {
        _db = db;
        _hub = hub;
        _sanitizer = sanitizer;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var subject = GetSubjectOrThrow();

        var quizzes = await _db.Quizzes
            .AsNoTracking()
            .Where(x => x.CreatedBySubject == subject)
            .Select(x => new QuizSummaryDto(
                x.Id,
                x.Title,
                x.JoinCode,
                x.DurationSeconds,
                x.TargetQuestionCount,
                x.IsStarted,
                x.IsPublished,
                x.CreatedUtc,
                x.StartedUtc,
                x.PublishedUtc))
            .ToListAsync();
            
        quizzes = quizzes.OrderByDescending(x => x.CreatedUtc).ToList();

        return Ok(quizzes);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuizRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { error = "Title is required." });
        }

        if (request.DurationSeconds is < 10 or > 3600)
        {
            return BadRequest(new { error = "DurationSeconds must be between 10 and 3600." });
        }

        if (request.DefaultQuestionTimeoutSeconds is < 5 or > 3600)
        {
            return BadRequest(new { error = "DefaultQuestionTimeoutSeconds must be between 5 and 3600." });
        }

        if (request.TargetQuestionCount is not null and (< 1 or > 200))
        {
            return BadRequest(new { error = "TargetQuestionCount must be between 1 and 200 when provided." });
        }

        var subject = GetSubjectOrThrow();

        var quiz = new Quiz
        {
            Title = request.Title.Trim(),
            DurationSeconds = request.DurationSeconds,
            DefaultQuestionTimeoutSeconds = request.DefaultQuestionTimeoutSeconds,
            TargetQuestionCount = request.TargetQuestionCount,
            JoinCode = JoinCodeGenerator.Create(),
            CreatedBySubject = subject
        };

        _db.Quizzes.Add(quiz);
        await _db.SaveChangesAsync();

        return Ok(new { id = quiz.Id, joinCode = quiz.JoinCode });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var subject = GetSubjectOrThrow();

        var quiz = await _db.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CreatedBySubject == subject);

        if (quiz is null)
        {
            return NotFound();
        }

        var questions = await _db.QuizQuestions
            .AsNoTracking()
            .Where(q => q.QuizId == quiz.Id)
            .OrderBy(q => q.Order)
            .Select(q => new QuizQuestionDto(
                q.Id,
                q.Order,
                q.Text,
                q.TimeoutSecondsOverride,
                q.TimeoutSecondsOverride ?? quiz.DefaultQuestionTimeoutSeconds,
                q.QuestionType,
                q.Options
                    .OrderBy(o => o.Order)
                    .Select(o => new QuizQuestionOptionDto(o.Id, o.Order, o.Text, o.IsCorrect))
                    .ToList()))
            .ToListAsync();

        var joinUrl = $"{Request.Scheme}://{Request.Host}{Url.Content("~/")}".TrimEnd('/') + $"/participant/join/{quiz.JoinCode}";
        var shareUrl = joinUrl;
        var shareMessage = $"Join quiz \"{quiz.Title}\" with code {quiz.JoinCode}: {shareUrl}";

        return Ok(new QuizDetailsDto(
            quiz.Id,
            quiz.Title,
            quiz.JoinCode,
            joinUrl,
            shareUrl,
            shareMessage,
            quiz.DurationSeconds,
            quiz.DefaultQuestionTimeoutSeconds,
            quiz.TargetQuestionCount,
            quiz.IsStarted,
            quiz.IsPublished,
            quiz.CreatedUtc,
            quiz.StartedUtc,
            quiz.PublishedUtc,
            questions));
    }

    [HttpPost("{id:guid}/questions")]
    public async Task<IActionResult> AddQuestion(Guid id, [FromBody] AddQuestionRequest request)
    {
        var sanitizedQuestionText = SanitizeContentOrNull(request.Text);
        if (string.IsNullOrWhiteSpace(sanitizedQuestionText))
        {
            return BadRequest(new { error = "Question text is required." });
        }

        if (request.TimeoutSecondsOverride is < 5 or > 3600)
        {
            return BadRequest(new { error = "TimeoutSecondsOverride must be between 5 and 3600 when provided." });
        }

        if (!TryNormalizeOptions(request.QuestionType, request.Options ?? Array.Empty<QuestionOptionRequest>(), out var questionType, out var normalizedOptions, out var normalizeError))
        {
            return BadRequest(new { error = normalizeError });
        }

        var subject = GetSubjectOrThrow();
        var quiz = await _db.Quizzes.FirstOrDefaultAsync(x => x.Id == id && x.CreatedBySubject == subject);
        if (quiz is null)
        {
            return NotFound();
        }

        var nextOrder = await _db.QuizQuestions
            .Where(q => q.QuizId == quiz.Id)
            .Select(q => (int?)q.Order)
            .MaxAsync() ?? 0;
        nextOrder += 1;

        var question = new QuizQuestion
        {
            QuizId = quiz.Id,
            Order = nextOrder,
            Text = sanitizedQuestionText,
            TimeoutSecondsOverride = request.TimeoutSecondsOverride,
            QuestionType = questionType,
            Options = normalizedOptions
                .Select(o => new QuizQuestionOption
                {
                    Id = o.Id ?? Guid.NewGuid(),
                    Order = o.Order,
                    Text = o.Text,
                    IsCorrect = o.IsCorrect
                })
                .ToList()
        };

        _db.QuizQuestions.Add(question);
        await _db.SaveChangesAsync();

        return Ok(new { id = question.Id, order = question.Order });
    }

    [HttpPut("{id:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> UpdateQuestion(Guid id, Guid questionId, [FromBody] UpdateQuestionRequest request)
    {
        var sanitizedQuestionText = SanitizeContentOrNull(request.Text);
        if (string.IsNullOrWhiteSpace(sanitizedQuestionText))
        {
            return BadRequest(new { error = "Question text is required." });
        }

        if (request.TimeoutSecondsOverride is < 5 or > 3600)
        {
            return BadRequest(new { error = "TimeoutSecondsOverride must be between 5 and 3600 when provided." });
        }

        if (!TryNormalizeOptions(request.QuestionType, request.Options ?? Array.Empty<QuestionOptionRequest>(), out var questionType, out var normalizedOptions, out var normalizeError))
        {
            return BadRequest(new { error = normalizeError });
        }

        var subject = GetSubjectOrThrow();
        var quiz = await _db.Quizzes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CreatedBySubject == subject);
        if (quiz is null)
        {
            return NotFound();
        }

        var question = await _db.QuizQuestions
            .Include(x => x.Options)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.QuizId == quiz.Id);
        if (question is null)
        {
            return NotFound();
        }

        question.Text = sanitizedQuestionText;
        question.TimeoutSecondsOverride = request.TimeoutSecondsOverride;
        question.QuestionType = questionType;

        var existing = question.Options.ToDictionary(o => o.Id, o => o);
        var keepIds = new HashSet<Guid>();

        foreach (var opt in normalizedOptions)
        {
            QuizQuestionOption entity;
            if (opt.Id is not null && existing.TryGetValue(opt.Id.Value, out entity!))
            {
                // reuse existing entity
            }
            else
            {
                entity = new QuizQuestionOption
                {
                    Id = opt.Id ?? Guid.NewGuid(),
                    QuizQuestionId = question.Id
                };
                question.Options.Add(entity);
            }

            entity.Text = opt.Text;
            entity.IsCorrect = opt.IsCorrect;
            entity.Order = opt.Order;

            keepIds.Add(entity.Id);
        }

        foreach (var toRemove in existing.Values.Where(o => !keepIds.Contains(o.Id)))
        {
            _db.QuizQuestionOptions.Remove(toRemove);
        }

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid id, Guid questionId)
    {
        var subject = GetSubjectOrThrow();
        var quiz = await _db.Quizzes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CreatedBySubject == subject);
        if (quiz is null)
        {
            return NotFound();
        }

        var question = await _db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId && q.QuizId == quiz.Id);
        if (question is null)
        {
            return NotFound();
        }

        _db.QuizQuestions.Remove(question);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}/qr")]
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

        var joinUrl = $"{Request.Scheme}://{Request.Host}{Url.Content("~/")}".TrimEnd('/') + $"/participant/join/{quiz.JoinCode}";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 10);

        return File(png, "image/png");
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id)
    {
        var subject = GetSubjectOrThrow();

        var quiz = await _db.Quizzes
            .FirstOrDefaultAsync(x => x.Id == id && x.CreatedBySubject == subject);

        if (quiz is null)
        {
            return NotFound();
        }

        if (!quiz.IsStarted)
        {
            quiz.IsStarted = true;
            quiz.StartedUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            await _hub.Clients.Group(QuizHub.GroupName(quiz.JoinCode))
                .SendAsync("QuizStarted", quiz.StartedUtc.Value.ToUnixTimeMilliseconds(), quiz.DurationSeconds);
        }

        return Ok(new { quiz.IsStarted, quiz.StartedUtc });
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var subject = GetSubjectOrThrow();

        var quiz = await _db.Quizzes
            .Include(q => q.Questions)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(x => x.Id == id && x.CreatedBySubject == subject);

        if (quiz is null)
        {
            return NotFound();
        }

        if (!quiz.Questions.Any())
        {
            return BadRequest(new { error = "Add at least one question before publishing." });
        }

        foreach (var question in quiz.Questions)
        {
            var optionRequests = question.Options
                .OrderBy(o => o.Order)
                .Select(o => new QuestionOptionRequest(o.Id, o.Text, o.IsCorrect, o.Order))
                .ToList();

            if (!TryNormalizeOptions(question.QuestionType, optionRequests, out _, out _, out var err))
            {
                return BadRequest(new { error = $"Question '{question.Text}' is incomplete: {err}" });
            }
        }

        quiz.IsPublished = true;
        quiz.PublishedUtc ??= DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var shareUrl = $"{Request.Scheme}://{Request.Host}{Url.Content("~/")}".TrimEnd('/') + $"/participant/join/{quiz.JoinCode}";
        var shareMessage = $"Join quiz \"{quiz.Title}\" with code {quiz.JoinCode}: {shareUrl}";

        return Ok(new { quiz.IsPublished, quiz.PublishedUtc, shareUrl, shareMessage });
    }

    private bool TryNormalizeOptions(string? questionType, IReadOnlyList<QuestionOptionRequest> options, out string normalizedType, out List<NormalizedOption> normalizedOptions, out string? error)
    {
        normalizedType = string.IsNullOrWhiteSpace(questionType)
            ? "single"
            : questionType.Trim().ToLowerInvariant();

        normalizedOptions = new List<NormalizedOption>();
        error = null;

        if (normalizedType is not ("single" or "multiple"))
        {
            error = "QuestionType must be 'single' or 'multiple'.";
            return false;
        }

        var ordered = (options ?? Array.Empty<QuestionOptionRequest>())
            .Select((o, idx) => new
            {
                Request = o,
                RequestedOrder = o.Order ?? idx + 1
            })
            .OrderBy(x => x.RequestedOrder)
            .Select((x, idx) => new NormalizedOption(
                x.Request.Id,
                SanitizeContentOrNull(x.Request.Text) ?? string.Empty,
                x.Request.IsCorrect,
                idx + 1))
            .ToList();

        if (ordered.Count < 2)
        {
            error = "At least two options are required.";
            return false;
        }

        if (ordered.Any(o => string.IsNullOrWhiteSpace(o.Text)))
        {
            error = "Option text is required.";
            return false;
        }

        var correctCount = ordered.Count(o => o.IsCorrect);
        if (normalizedType == "single" && correctCount != 1)
        {
            error = "Single-choice questions must have exactly one correct option.";
            return false;
        }

        if (normalizedType == "multiple" && correctCount < 1)
        {
            error = "Multiple-select questions must have at least one correct option.";
            return false;
        }

        normalizedOptions = ordered;
        return true;
    }

    private sealed record NormalizedOption(Guid? Id, string Text, bool IsCorrect, int Order);

    private string? SanitizeContentOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var sanitized = _sanitizer.Sanitize(raw).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    private string GetSubjectOrThrow()
    {
        return User.FindFirstValue("oid")
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? throw new InvalidOperationException("Missing subject claim for host user.");
    }

    public sealed record CreateQuizRequest(string Title, int DurationSeconds, int DefaultQuestionTimeoutSeconds, int? TargetQuestionCount);

    public sealed record QuizSummaryDto(
        Guid Id,
        string Title,
        string JoinCode,
        int DurationSeconds,
        int? TargetQuestionCount,
        bool IsStarted,
        bool IsPublished,
        DateTimeOffset CreatedUtc,
        DateTimeOffset? StartedUtc,
        DateTimeOffset? PublishedUtc);

    public sealed record QuizDetailsDto(
        Guid Id,
        string Title,
        string JoinCode,
        string JoinUrl,
        string ShareUrl,
        string ShareMessage,
        int DurationSeconds,
        int DefaultQuestionTimeoutSeconds,
        int? TargetQuestionCount,
        bool IsStarted,
        bool IsPublished,
        DateTimeOffset CreatedUtc,
        DateTimeOffset? StartedUtc,
        DateTimeOffset? PublishedUtc,
        IReadOnlyList<QuizQuestionDto> Questions);

    public sealed record QuizQuestionDto(
        Guid Id,
        int Order,
        string Text,
        int? TimeoutSecondsOverride,
        int EffectiveTimeoutSeconds,
        string QuestionType,
        IReadOnlyList<QuizQuestionOptionDto> Options);

    public sealed record QuizQuestionOptionDto(
        Guid Id,
        int Order,
        string Text,
        bool IsCorrect);

    public sealed record QuestionOptionRequest(Guid? Id, string Text, bool IsCorrect, int? Order);

    public sealed record AddQuestionRequest(string Text, int? TimeoutSecondsOverride, string? QuestionType, IReadOnlyList<QuestionOptionRequest> Options);

    public sealed record UpdateQuestionRequest(string Text, int? TimeoutSecondsOverride, string? QuestionType, IReadOnlyList<QuestionOptionRequest> Options);
}
