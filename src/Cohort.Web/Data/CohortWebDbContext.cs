using Cohort.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cohort.Web.Data;

public sealed class CohortWebDbContext : DbContext
{
    public CohortWebDbContext(DbContextOptions<CohortWebDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizParticipant> QuizParticipants => Set<QuizParticipant>();
    public DbSet<QuizQuestionOption> QuizQuestionOptions => Set<QuizQuestionOption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Quiz>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.JoinCode).HasMaxLength(32).IsRequired();
            b.Property(x => x.CreatedBySubject).HasMaxLength(200).IsRequired();
            b.Property(x => x.TargetQuestionCount);
            b.Property(x => x.IsPublished).HasDefaultValue(false);
            b.Property(x => x.PublishedUtc);

            b.HasIndex(x => x.JoinCode).IsUnique();

            b.HasMany(x => x.Participants)
                .WithOne(x => x.Quiz)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.Questions)
                .WithOne(x => x.Quiz)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuizQuestion>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            b.Property(x => x.Order).IsRequired();
            b.Property(x => x.QuestionType).HasMaxLength(32).IsRequired().HasDefaultValue("single");

            b.HasIndex(x => new { x.QuizId, x.Order }).IsUnique();

            b.HasMany(x => x.Options)
                .WithOne(x => x.QuizQuestion)
                .HasForeignKey(x => x.QuizQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuizQuestionOption>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Text).HasMaxLength(1000).IsRequired();
            b.Property(x => x.Order).IsRequired();
            b.Property(x => x.IsCorrect).IsRequired();

            b.HasIndex(x => new { x.QuizQuestionId, x.Order }).IsUnique();
        });

        modelBuilder.Entity<QuizParticipant>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.ParticipantKey).HasMaxLength(200).IsRequired();
            b.Property(x => x.RealName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Pseudonym).HasMaxLength(200).IsRequired();

            b.HasIndex(x => new { x.QuizId, x.ParticipantKey }).IsUnique();
        });

        modelBuilder.Entity<AppUser>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            b.Property(x => x.Email).HasMaxLength(320).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            b.Property(x => x.EmpId).HasMaxLength(64).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            b.Property(x => x.AppRole).HasMaxLength(32).IsRequired();

            b.HasIndex(x => x.Subject).IsUnique();
            b.HasIndex(x => x.Email);
        });
    }
}
