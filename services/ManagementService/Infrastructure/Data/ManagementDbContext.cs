using Microsoft.EntityFrameworkCore;
using ManagementService.Domain.Entities;
using ManagementService.Domain.Enums;

namespace ManagementService.Infrastructure.Data;

public class ManagementDbContext : DbContext
{
    public ManagementDbContext(DbContextOptions<ManagementDbContext> options) 
        : base(options)
    {
    }

    public DbSet<Workspace> Workspaces { get; set; } = null!;
    public DbSet<WorkspaceMember> WorkspaceMembers { get; set; } = null!;
    public DbSet<WorkspaceInvitation> WorkspaceInvitations { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<ProjectStatus> ProjectStatuses { get; set; } = null!;
    public DbSet<TaskItem> Tasks { get; set; } = null!;
    public DbSet<SubTaskItem> SubTasks { get; set; } = null!;
    public DbSet<TaskComment> TaskComments { get; set; } = null!;
    public DbSet<ProjectLabel> ProjectLabels { get; set; } = null!;
    public DbSet<TaskLabelAssignment> TaskLabelAssignments { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<IntegrationOutboxMessage> IntegrationOutboxMessages { get; set; } = null!;
    public DbSet<ServiceRestartOperation> ServiceRestartOperations { get; set; } = null!;
    public DbSet<LandingLocaleContent> LandingLocaleContents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Конфигурация сущности Workspace.
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Конфигурация сущности WorkspaceMember.
        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Members)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();
        });

        // Конфигурация сущности Project.
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Projects)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Конфигурация сущности ProjectStatus.
        modelBuilder.Entity<ProjectStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Statuses)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Конфигурация сущности TaskItem.
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(5000);
            entity.Property(e => e.Priority)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Status)
                .WithMany(s => s.Tasks)
                .HasForeignKey(e => e.StatusId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Конфигурация сущности SubTaskItem.
        modelBuilder.Entity<SubTaskItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Task)
                .WithMany(t => t.SubTasks)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Конфигурация сущности TaskComment.
        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Task)
                .WithMany(t => t.Comments)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Конфигурация сущности ProjectLabel.
        modelBuilder.Entity<ProjectLabel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Labels)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Конфигурация сущности TaskLabelAssignment.
        modelBuilder.Entity<TaskLabelAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AssignedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Task)
                .WithMany(t => t.LabelAssignments)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Label)
                .WithMany()
                .HasForeignKey(e => e.LabelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.TaskId, e.LabelId }).IsUnique();
        });

        // Конфигурация сущности WorkspaceInvitation.
        modelBuilder.Entity<WorkspaceInvitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Workspace)
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => new { e.WorkspaceId, e.Email, e.Status });
        });

        // Конфигурация сущности Notification.
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.ActionStatus).HasMaxLength(50);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<IntegrationOutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Destination).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.HasIndex(e => new { e.ProcessedAtUtc, e.NextAttemptAtUtc });
        });

        modelBuilder.Entity<ServiceRestartOperation>(entity =>
        {
            entity.ToTable("service_restart_operations");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TargetService, e.RequestedAtUtc });
            entity.HasIndex(e => e.TargetService).IsUnique().HasDatabaseName("IX_service_restart_operations_TargetService_active").HasFilter("\"Status\" IN ('queued', 'triggered', 'running')");
            entity.Property(e => e.TargetService).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(64).IsRequired();
            entity.Property(e => e.RequestedByEmail).HasMaxLength(255);
            entity.Property(e => e.DeploymentUid).HasMaxLength(512);
            entity.Property(e => e.RolloutStatus).HasMaxLength(128);
            entity.Property(e => e.DeploymentNamespace).HasMaxLength(128);
            entity.Property(e => e.FailureMessage).HasMaxLength(2000);
            entity.Property(e => e.OwnedStateUnitsJson).IsRequired();
        });

        // CMS-документ публичного landing хранится одной записью на локаль.
        modelBuilder.Entity<LandingLocaleContent>(entity =>
        {
            entity.ToTable("landing_locale_contents");
            entity.HasKey(e => e.Locale);
            entity.Property(e => e.Locale).HasMaxLength(8).IsRequired();
            entity.Property(e => e.ContentJson).IsRequired();
            entity.Property(e => e.UpdatedByEmail).HasMaxLength(255);
        });
    }
}
