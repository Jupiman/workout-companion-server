using Microsoft.EntityFrameworkCore;
using WorkoutCompanion.Server.Data.Entities;

namespace WorkoutCompanion.Server.Data;

public sealed class WorkoutDbContext(DbContextOptions<WorkoutDbContext> options) : DbContext(options)
{
    public DbSet<WorkoutSessionEntity> WorkoutSessions => Set<WorkoutSessionEntity>();
    public DbSet<SessionExerciseEntity> SessionExercises => Set<SessionExerciseEntity>();
    public DbSet<SessionSetEntity> SessionSets => Set<SessionSetEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var workout = modelBuilder.Entity<WorkoutSessionEntity>();
        workout.ToTable("WorkoutSessions");
        workout.HasKey(entity => entity.Id);
        workout.HasIndex(entity => entity.SyncId).IsUnique();
        workout.HasIndex(entity => entity.CompletedAt);
        workout.Property(entity => entity.SyncId).HasConversion<string>().HasMaxLength(36);
        workout.Property(entity => entity.ProgramNameSnapshot).HasMaxLength(200);
        workout.Property(entity => entity.WorkoutNameSnapshot).HasMaxLength(200);
        workout.Property(entity => entity.SourceDeviceId).HasMaxLength(128);
        workout.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        workout.HasMany(entity => entity.Exercises)
            .WithOne(entity => entity.WorkoutSession)
            .HasForeignKey(entity => entity.WorkoutSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        var exercise = modelBuilder.Entity<SessionExerciseEntity>();
        exercise.ToTable("SessionExercises");
        exercise.HasKey(entity => entity.Id);
        exercise.HasIndex(entity => entity.SyncId).IsUnique();
        exercise.HasIndex(entity => new { entity.WorkoutSessionId, entity.SortOrderSnapshot });
        exercise.HasIndex(entity => entity.SourceProgressionTrackSyncId);
        exercise.Property(entity => entity.SyncId).HasConversion<string>().HasMaxLength(36);
        exercise.Property(entity => entity.SourceProgressionTrackSyncId).HasConversion<string>().HasMaxLength(36);
        exercise.Property(entity => entity.ExerciseNameSnapshot).HasMaxLength(200);
        exercise.Property(entity => entity.TrackingMode).HasConversion<string>().HasMaxLength(32);
        exercise.HasMany(entity => entity.Sets)
            .WithOne(entity => entity.SessionExercise)
            .HasForeignKey(entity => entity.SessionExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        var set = modelBuilder.Entity<SessionSetEntity>();
        set.ToTable("SessionSets");
        set.HasKey(entity => entity.Id);
        set.HasIndex(entity => entity.SyncId).IsUnique();
        set.HasIndex(entity => new { entity.SessionExerciseId, entity.SetOrder });
        set.Property(entity => entity.SyncId).HasConversion<string>().HasMaxLength(36);
        set.Property(entity => entity.SetType).HasConversion<string>().HasMaxLength(32);
        set.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
    }
}

