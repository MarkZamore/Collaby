using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<OAuthAccount> OAuthAccounts => Set<OAuthAccount>();
    public DbSet<TwoFactorSetting> TwoFactorSettings => Set<TwoFactorSetting>();
    public DbSet<UserDeletionOperation> UserDeletionOperations => Set<UserDeletionOperation>();
    public DbSet<UserDeletionOperationStep> UserDeletionOperationSteps => Set<UserDeletionOperationStep>();
    public DbSet<IntegrationOutboxMessage> IntegrationOutboxMessages => Set<IntegrationOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();

            entity.HasOne(e => e.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailVerification>(entity =>
        {
            entity.ToTable("email_verifications");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Token }).IsUnique();

            entity.HasOne(e => e.User)
                  .WithMany(u => u.EmailVerifications)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Token).IsRequired();
        });

        modelBuilder.Entity<PasswordReset>(entity =>
        {
            entity.ToTable("password_resets");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();

            entity.HasOne(e => e.User)
                  .WithMany(u => u.PasswordResets)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Token).IsRequired();
        });

        modelBuilder.Entity<OAuthAccount>(entity =>
        {
            entity.ToTable("oauth_accounts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Provider, e.ProviderId }).IsUnique();

            entity.HasOne(e => e.User)
                  .WithMany(u => u.OAuthAccounts)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.ProviderId).HasMaxLength(255).IsRequired();
        });

        modelBuilder.Entity<TwoFactorSetting>(entity =>
        {
            entity.ToTable("two_factor_settings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();

            entity.HasOne(e => e.User)
                  .WithOne(u => u.TwoFactorSetting)
                  .HasForeignKey<TwoFactorSetting>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Secret).IsRequired();
        });

        modelBuilder.Entity<UserDeletionOperation>(entity =>
        {
            entity.ToTable("user_deletion_operations");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.RequestedAtUtc });
            entity.Property(e => e.UserEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.RequestedByEmail).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.FailureMessage).HasMaxLength(2000);
        });

        modelBuilder.Entity<UserDeletionOperationStep>(entity =>
        {
            entity.ToTable("user_deletion_operation_steps");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OperationId, e.ServiceName }).IsUnique();
            entity.Property(e => e.ServiceName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(64).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);

            entity.HasOne(e => e.Operation)
                .WithMany(operation => operation.Steps)
                .HasForeignKey(e => e.OperationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IntegrationOutboxMessage>(entity =>
        {
            entity.ToTable("integration_outbox_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageType).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Destination).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.HasIndex(e => new { e.ProcessedAtUtc, e.NextAttemptAtUtc });
        });
    }
}

