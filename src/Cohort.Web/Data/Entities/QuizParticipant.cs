namespace Cohort.Web.Data.Entities;

public sealed class QuizParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuizId { get; set; }

    public Quiz Quiz { get; set; } = null!;

    public string ParticipantKey { get; set; } = string.Empty;

    // The participant's actual name (private to host).
    public string RealName { get; set; } = string.Empty;

    // The participant's public-facing username/pseudonym.
    public string Pseudonym { get; set; } = string.Empty;

    public DateTimeOffset JoinedUtc { get; set; } = DateTimeOffset.UtcNow;
}
