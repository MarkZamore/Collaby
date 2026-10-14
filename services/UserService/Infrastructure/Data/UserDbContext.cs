using Microsoft.EntityFrameworkCore;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Data;

public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<UserOnboarding> UserOnboarding => Set<UserOnboarding>();
    public DbSet<UserActivity> UserActivity => Set<UserActivity>();
    public DbSet<IdentityDirectoryEntry> IdentityDirectoryEntries => Set<IdentityDirectoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profiles");
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(120);
            entity.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(1024);
            entity.Property(x => x.Bio).HasColumnName("bio").HasMaxLength(2000);
            entity.Property(x => x.Timezone).HasColumnName("timezone").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Language).HasColumnName("language").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(x => x.DisplayName).HasDatabaseName("ix_user_profiles_display_name");
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.ToTable("user_preferences");
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Theme).HasColumnName("theme").HasMaxLength(16).IsRequired();
            entity.Property(x => x.DefaultBoardBackgroundJson).HasColumnName("default_board_background_json").HasColumnType("text");
            entity.Property(x => x.EmailNotifications).HasColumnName("email_notifications");
            entity.Property(x => x.PushNotifications).HasColumnName("push_notifications");
            entity.Property(x => x.WeeklyDigest).HasColumnName("weekly_digest");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<UserOnboarding>(entity =>
        {
            entity.ToTable("user_onboarding");
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.CurrentStep).HasColumnName("current_step");
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<UserActivity>(entity =>
        {
            entity.ToTable("user_activity");
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.LastActiveAt).HasColumnName("last_active_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.LastActiveWorkspaceId).HasColumnName("last_active_workspace_id");
            entity.Property(x => x.TaskCompletedCount).HasColumnName("task_completed_count").HasDefaultValue(0);
            entity.Property(x => x.CommentCount).HasColumnName("comment_count").HasDefaultValue(0);
            entity.Property(x => x.WorkspaceVisitCount).HasColumnName("workspace_visit_count").HasDefaultValue(0);
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(x => x.LastActiveAt).HasDatabaseName("ix_user_activity_last_active_at");
        });

        modelBuilder.Entity<IdentityDirectoryEntry>(entity =>
        {
            entity.ToTable("identity_directory_entries");
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.IsSystemAdmin).HasColumnName("is_system_admin");
            entity.Property(x => x.LifecycleState).HasColumnName("lifecycle_state").HasMaxLength(64).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

            entity.HasIndex(x => x.Username).HasDatabaseName("ix_identity_directory_entries_username");
            entity.HasIndex(x => x.Email).HasDatabaseName("ix_identity_directory_entries_email");
        });
    }
}

