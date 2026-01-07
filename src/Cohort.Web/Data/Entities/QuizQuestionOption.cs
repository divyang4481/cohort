namespace Cohort.Web.Data.Entities;

public sealed class QuizQuestionOption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuizQuestionId { get; set; }

    public QuizQuestion QuizQuestion { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    // 1-based display order within a question.
    public int Order { get; set; }

    public bool IsCorrect { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
