using Microsoft.EntityFrameworkCore;
using GamificationService.Domain.Constants;
using GamificationService.Domain.Entities;
using GamificationService.Domain.Enums;
using System.Linq;

namespace GamificationService.Infrastructure.Data;

/// <summary>
/// Главный EF Core контекст игровой модели.
/// Здесь задаются проекции прогресса, каталог наград и все опорные сущности,
/// которыми владеет GamificationService.
/// </summary>
public class GamificationDbContext : DbContext
{
    public GamificationDbContext(DbContextOptions<GamificationDbContext> options) : base(options) { }

    public DbSet<UserGlobalXp> UserGlobalXp { get; set; }
    public DbSet<UserProjectXp> UserProjectXp { get; set; }
    public DbSet<XpTransaction> XpTransactions { get; set; }
    public DbSet<Quest> Quests { get; set; }
    public DbSet<UserQuest> UserQuests { get; set; }
    public DbSet<ShopItem> ShopItems { get; set; }
    public DbSet<UserInventory> UserInventory { get; set; }
    public DbSet<LeaderboardSnapshot> LeaderboardSnapshots { get; set; }
    public DbSet<SkillTreeNode> SkillTreeNodes { get; set; }
    public DbSet<UserSkillTreeChoice> UserSkillTreeChoices { get; set; }
    public DbSet<XpConfig> XpConfigs { get; set; }
    public DbSet<ProcessedIntegrationEvent> ProcessedIntegrationEvents { get; set; }
    public DbSet<IntegrationOutboxMessage> IntegrationOutboxMessages { get; set; }
    public DbSet<ProjectProgressSnapshotEntry> ProjectProgressSnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Глобальная проекция прогресса пользователя по всему продукту.
        modelBuilder.Entity<UserGlobalXp>(e =>
        {
            e.ToTable("user_global_xp");
            e.HasKey(x => x.UserId);
            e.Property(x => x.TotalXp).HasDefaultValue(0);
            e.Property(x => x.Level).HasDefaultValue(1);
            e.Property(x => x.Coins).HasDefaultValue(0);
            e.Property(x => x.StreakDays).HasDefaultValue(0);
            e.Property(x => x.FreezeCount).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Проекция прогресса пользователя внутри конкретного проекта.
        modelBuilder.Entity<UserProjectXp>(e =>
        {
            e.ToTable("user_project_xp");
            e.HasKey(x => new { x.UserId, x.ProjectId });
            e.Property(x => x.Xp).HasDefaultValue(0);
            e.Property(x => x.Level).HasDefaultValue(1);
            e.Property(x => x.TasksCompleted).HasDefaultValue(0);
            e.HasIndex(x => x.ProjectId);
        });

        // Журнал всех начислений и откатов XP/coins.
        modelBuilder.Entity<XpTransaction>(e =>
        {
            e.ToTable("xp_transactions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.ProjectId });
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.Reason).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(32).HasDefaultValue("Applied");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Каталог квестов, из которого пользователям материализуются window-based задания.
        modelBuilder.Entity<Quest>(e =>
        {
            e.ToTable("quests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        });

        // Состояние конкретного квеста у конкретного пользователя в конкретном окне.
        modelBuilder.Entity<UserQuest>(e =>
        {
            e.ToTable("user_quests");
            e.HasKey(x => new { x.UserId, x.QuestId });
            e.HasOne(x => x.Quest).WithMany().HasForeignKey(x => x.QuestId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.WindowKey).HasMaxLength(64);
        });

        // Каталог предметов магазина.
        modelBuilder.Entity<ShopItem>(e =>
        {
            e.ToTable("shop_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Category).IsRequired().HasMaxLength(50);
            e.Property(x => x.IsPurchasable).HasDefaultValue(true);
        });

        // Инвентарь пользователя, связанный с каталогом предметов.
        modelBuilder.Entity<UserInventory>(e =>
        {
            e.ToTable("user_inventory");
            e.HasKey(x => new { x.UserId, x.ItemId });
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
        });

        // Исторические снимки leaderboard для годовых наград и отчётности.
        modelBuilder.Entity<LeaderboardSnapshot>(e =>
        {
            e.ToTable("leaderboard_snapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Type, x.Year });
        });

        // Каталог узлов skill tree, который может быть общим шаблоном или проектной настройкой.
        modelBuilder.Entity<SkillTreeNode>(e =>
        {
            e.ToTable("skill_tree_nodes");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProjectId);
            e.Property(x => x.Branch).HasMaxLength(10);
            e.Property(x => x.IsActive).HasDefaultValue(true);
        });

        // Выбор пользователя в skill tree по tier внутри проекта.
        modelBuilder.Entity<UserSkillTreeChoice>(e =>
        {
            e.ToTable("user_skill_choices");
            e.HasKey(x => new { x.UserId, x.ProjectId, x.Tier });
            e.HasOne(x => x.Node).WithMany().HasForeignKey(x => x.NodeId).OnDelete(DeleteBehavior.Cascade);
        });

        // Пер-проектная конфигурация правил расчёта наград.
        modelBuilder.Entity<XpConfig>(e =>
        {
            e.ToTable("xp_config");
            e.HasKey(x => x.ProjectId);
        });

        modelBuilder.Entity<ProcessedIntegrationEvent>(e =>
        {
            e.ToTable("processed_integration_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.SourceService).IsRequired().HasMaxLength(100);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<IntegrationOutboxMessage>(e =>
        {
            e.ToTable("integration_outbox_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.MessageType).IsRequired().HasMaxLength(200);
            e.Property(x => x.Destination).IsRequired().HasMaxLength(200);
            e.Property(x => x.PayloadJson).IsRequired();
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.HasIndex(x => new { x.ProcessedAtUtc, x.NextAttemptAtUtc });
        });

        modelBuilder.Entity<ProjectProgressSnapshotEntry>(e =>
        {
            e.ToTable("project_progress_snapshots");
            e.HasKey(x => x.ProjectId);
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.ProgressPercent).HasColumnName("progress_percent");
            e.Property(x => x.TotalWeight).HasColumnName("total_weight");
            e.Property(x => x.CompletedWeight).HasColumnName("completed_weight");
            e.Property(x => x.TotalTasks).HasColumnName("total_tasks");
            e.Property(x => x.CompletedTasks).HasColumnName("completed_tasks");
            e.Property(x => x.TotalSubtasks).HasColumnName("total_subtasks");
            e.Property(x => x.CompletedSubtasks).HasColumnName("completed_subtasks");
            e.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });

        SeedShopItems(modelBuilder);
        SeedQuests(modelBuilder);
        SeedSkillTreeNodes(modelBuilder);
    }

    private static void SeedShopItems(ModelBuilder modelBuilder)
    {
        // Seed-каталог задаёт стартовый набор косметики, расходников и наградных бейджей.
        modelBuilder.Entity<ShopItem>().HasData(
            GamificationCatalog.ShopItems
                .Select(item => new ShopItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    Category = item.Category,
                    Price = item.Price,
                    Description = item.Description,
                    IsPurchasable = item.IsPurchasable,
                    CreatedAt = GamificationCatalog.SeedTimestampUtc,
                })
                .ToArray()
        );
    }

    private static void SeedQuests(ModelBuilder modelBuilder)
    {
        // Daily и weekly квесты задаются как декларативный каталог.
        // Requirement остаётся JSON, чтобы можно было расширять механику без новой таблицы под каждый тип условия.
        modelBuilder.Entity<Quest>().HasData(
            GamificationCatalog.Quests
                .Select(quest => new Quest
                {
                    Id = quest.Id,
                    Type = quest.Type,
                    Title = quest.Title,
                    Description = quest.Description,
                    XpReward = quest.XpReward,
                    CoinReward = quest.CoinReward,
                    Requirement = quest.Requirement,
                    CreatedAt = GamificationCatalog.SeedTimestampUtc,
                })
                .ToArray()
        );
    }

    private static void SeedSkillTreeNodes(ModelBuilder modelBuilder)
    {
        // Skill tree по умолчанию хранится как шаблонный проект.
        // Конкретный проект может использовать этот шаблон или переопределить узлы собственным набором.
        modelBuilder.Entity<SkillTreeNode>().HasData(
            GamificationCatalog.SkillTreeNodes
                .Select(node => new SkillTreeNode
                {
                    Id = node.Id,
                    ProjectId = GamificationCatalog.SkillTreeTemplateProjectId,
                    Tier = node.Tier,
                    Branch = node.Branch,
                    Title = node.Title,
                    Description = node.Description,
                    RewardJson = node.RewardJson,
                    IsActive = true,
                    CreatedAt = GamificationCatalog.SeedTimestampUtc,
                })
                .ToArray()
        );
    }
}
