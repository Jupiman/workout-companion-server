using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace WorkoutCompanion.Server.Data.Migrations;

[DbContext(typeof(WorkoutDbContext))]
public partial class WorkoutDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

        modelBuilder.Entity("WorkoutCompanion.Server.Data.Entities.SessionExerciseEntity", entity =>
        {
            entity.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER").HasAnnotation("Sqlite:Autoincrement", true);
            entity.Property<string>("ExerciseNameSnapshot").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            entity.Property<int?>("PrescribedWeightCentiKgSnapshot").HasColumnType("INTEGER");
            entity.Property<int?>("RepMaxSnapshot").HasColumnType("INTEGER");
            entity.Property<int?>("RepMinSnapshot").HasColumnType("INTEGER");
            entity.Property<int?>("ResultingProgressionDurationSeconds").HasColumnType("INTEGER");
            entity.Property<int?>("ResultingProgressionTargetReps").HasColumnType("INTEGER");
            entity.Property<int?>("ResultingProgressionWeightCentiKg").HasColumnType("INTEGER");
            entity.Property<int>("SortOrderSnapshot").HasColumnType("INTEGER");
            entity.Property<Guid?>("SourceProgressionTrackSyncId").HasMaxLength(36).HasColumnType("TEXT").HasConversion<string>();
            entity.Property<Guid>("SyncId").HasMaxLength(36).HasColumnType("TEXT").HasConversion<string>();
            entity.Property<int?>("TargetDurationSecondsSnapshot").HasColumnType("INTEGER");
            entity.Property<int?>("TargetRepsSnapshot").HasColumnType("INTEGER");
            entity.Property<WorkoutCompanion.Server.Contracts.TrackingMode>("TrackingMode").HasMaxLength(32).HasColumnType("TEXT").HasConversion<string>();
            entity.Property<long>("WorkoutSessionId").HasColumnType("INTEGER");
            entity.HasKey("Id");
            entity.HasIndex("SourceProgressionTrackSyncId");
            entity.HasIndex("SyncId").IsUnique();
            entity.HasIndex("WorkoutSessionId", "SortOrderSnapshot");
            entity.ToTable("SessionExercises");
        });

        modelBuilder.Entity("WorkoutCompanion.Server.Data.Entities.SessionSetEntity", entity =>
        {
            entity.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER").HasAnnotation("Sqlite:Autoincrement", true);
            entity.Property<int?>("ActualDurationSeconds").HasColumnType("INTEGER");
            entity.Property<int?>("ActualReps").HasColumnType("INTEGER");
            entity.Property<int?>("ActualWeightCentiKg").HasColumnType("INTEGER");
            entity.Property<DateTimeOffset?>("CompletedAt").HasColumnType("TEXT");
            entity.Property<bool>("CountsForProgression").HasColumnType("INTEGER");
            entity.Property<bool>("IsPlanned").HasColumnType("INTEGER");
            entity.Property<int?>("PrescribedDurationSeconds").HasColumnType("INTEGER");
            entity.Property<int?>("PrescribedReps").HasColumnType("INTEGER");
            entity.Property<int?>("PrescribedWeightCentiKg").HasColumnType("INTEGER");
            entity.Property<long>("SessionExerciseId").HasColumnType("INTEGER");
            entity.Property<int>("SetOrder").HasColumnType("INTEGER");
            entity.Property<WorkoutCompanion.Server.Contracts.WorkoutSetType>("SetType").HasMaxLength(32).HasColumnType("TEXT").HasConversion<string>();
            entity.Property<WorkoutCompanion.Server.Contracts.WorkoutSetStatus>("Status").HasMaxLength(32).HasColumnType("TEXT").HasConversion<string>();
            entity.Property<Guid>("SyncId").HasMaxLength(36).HasColumnType("TEXT").HasConversion<string>();
            entity.HasKey("Id");
            entity.HasIndex("SyncId").IsUnique();
            entity.HasIndex("SessionExerciseId", "SetOrder");
            entity.ToTable("SessionSets");
        });

        modelBuilder.Entity("WorkoutCompanion.Server.Data.Entities.WorkoutSessionEntity", entity =>
        {
            entity.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER").HasAnnotation("Sqlite:Autoincrement", true);
            entity.Property<DateTimeOffset>("CompletedAt").HasColumnType("TEXT");
            entity.Property<DateTimeOffset>("LastReceivedAt").HasColumnType("TEXT");
            entity.Property<int>("PayloadSchemaVersion").HasColumnType("INTEGER");
            entity.Property<string>("ProgramNameSnapshot").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            entity.Property<DateTimeOffset>("ReceivedAt").HasColumnType("TEXT");
            entity.Property<string>("SourceDeviceId").HasMaxLength(128).HasColumnType("TEXT");
            entity.Property<DateTimeOffset>("StartedAt").HasColumnType("TEXT");
            entity.Property<WorkoutCompanion.Server.Contracts.WorkoutStatus>("Status").HasMaxLength(32).HasColumnType("TEXT").HasConversion<string>();
            entity.Property<Guid>("SyncId").HasMaxLength(36).HasColumnType("TEXT").HasConversion<string>();
            entity.Property<string>("WorkoutNameSnapshot").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.HasIndex("CompletedAt");
            entity.HasIndex("SyncId").IsUnique();
            entity.ToTable("WorkoutSessions");
        });

        modelBuilder.Entity("WorkoutCompanion.Server.Data.Entities.SessionExerciseEntity", entity =>
        {
            entity.HasOne("WorkoutCompanion.Server.Data.Entities.WorkoutSessionEntity", "WorkoutSession")
                .WithMany("Exercises")
                .HasForeignKey("WorkoutSessionId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            entity.Navigation("WorkoutSession");
        });

        modelBuilder.Entity("WorkoutCompanion.Server.Data.Entities.SessionSetEntity", entity =>
        {
            entity.HasOne("WorkoutCompanion.Server.Data.Entities.SessionExerciseEntity", "SessionExercise")
                .WithMany("Sets")
                .HasForeignKey("SessionExerciseId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            entity.Navigation("SessionExercise");
        });

        modelBuilder.Entity("WorkoutCompanion.Server.Data.Entities.SessionExerciseEntity", entity =>
        {
            entity.Navigation("Sets");
        });

        modelBuilder.Entity("WorkoutCompanion.Server.Data.Entities.WorkoutSessionEntity", entity =>
        {
            entity.Navigation("Exercises");
        });
#pragma warning restore 612, 618
    }
}
