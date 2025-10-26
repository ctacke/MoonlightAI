using Microsoft.EntityFrameworkCore;
using MoonlightAI.Core.Data.Models;

namespace MoonlightAI.Core.Data;

/// <summary>
/// Entity Framework Core database context for MoonlightAI.
/// </summary>
public class MoonlightDbContext : DbContext
{
    public DbSet<WorkloadRunRecord> WorkloadRuns { get; set; } = null!;
    public DbSet<FileResultRecord> FileResults { get; set; } = null!;
    public DbSet<BuildAttemptRecord> BuildAttempts { get; set; } = null!;
    public DbSet<AIInteractionRecord> AIInteractions { get; set; } = null!;

    public MoonlightDbContext(DbContextOptions<MoonlightDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // WorkloadRunRecord configuration
        modelBuilder.Entity<WorkloadRunRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RunId).IsUnique();
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.ModelName);
            entity.HasIndex(e => e.WorkloadType);

            entity.HasMany(e => e.FileResults)
                .WithOne(e => e.WorkloadRun)
                .HasForeignKey(e => e.WorkloadRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FileResultRecord configuration
        modelBuilder.Entity<FileResultRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WorkloadRunId);
            entity.HasIndex(e => e.FilePath);
            entity.HasIndex(e => e.Success);

            entity.HasMany(e => e.BuildAttempts)
                .WithOne(e => e.FileResult)
                .HasForeignKey(e => e.FileResultId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.AIInteractions)
                .WithOne(e => e.FileResult)
                .HasForeignKey(e => e.FileResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BuildAttemptRecord configuration
        modelBuilder.Entity<BuildAttemptRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FileResultId);
            entity.HasIndex(e => e.Success);
        });

        // AIInteractionRecord configuration
        modelBuilder.Entity<AIInteractionRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FileResultId);
            entity.HasIndex(e => e.InteractionType);
        });
    }
}
