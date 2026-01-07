using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cohort.Web.Data.Entities;

public sealed class Quiz
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(20)]
    public string JoinCode { get; set; } = string.Empty;

    public int DurationSeconds { get; set; }

    public int DefaultQuestionTimeoutSeconds { get; set; }

    public int? TargetQuestionCount { get; set; }

    public bool IsStarted { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedUtc { get; set; }

    public DateTimeOffset? PublishedUtc { get; set; }

    [MaxLength(100)]
    public string CreatedBySubject { get; set; } = string.Empty;

    public List<QuizQuestion> Questions { get; set; } = new();

    public ICollection<QuizParticipant> Participants { get; set; } = new List<QuizParticipant>();
}
