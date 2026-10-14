using AnalyticsService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Data;

public sealed class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
        : base(options)
    {
    }

    public DbSet<AnalyticsInboxEvent> AnalyticsInboxEvents => Set<AnalyticsInboxEvent>();
    public DbSet<UserAnalyticsSummary> UserAnalyticsSummaries => Set<UserAnalyticsSummary>();
    public DbSet<ProjectAnalyticsSummary> ProjectAnalyticsSummaries => Set<ProjectAnalyticsSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalyticsInboxEvent>(entity =>
        {
            entity.ToTable("analytics_inbox_events");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.SourceService).IsRequired().HasMaxLength(100);
            entity.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Priority).HasMaxLength(32);
            entity.HasIndex(x => new { x.SourceService, x.EventType });
            entity.HasIndex(x => x.OccurredAtUtc);
        });

        modelBuilder.Entity<UserAnalyticsSummary>(entity =>
        {
            entity.ToTable("user_analytics_summaries");
            entity.HasKey(x => x.UserId);
        });

        modelBuilder.Entity<ProjectAnalyticsSummary>(entity =>
        {
            entity.ToTable("project_analytics_summaries");
            entity.HasKey(x => x.ProjectId);
        });
    }
}
