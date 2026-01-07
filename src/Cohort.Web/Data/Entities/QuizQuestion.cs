namespace Cohort.Web.Data.Entities;

public sealed class QuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuizId { get; set; }

    public Quiz Quiz { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    // 1-based display order within a quiz.
    public int Order { get; set; }

    // If null, the quiz-level default applies.
    public int? TimeoutSecondsOverride { get; set; }

    // "single" (radio) or "multiple" (checkbox).
    public string QuestionType { get; set; } = "single";

    public ICollection<QuizQuestionOption> Options { get; set; } = new List<QuizQuestionOption>();

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
