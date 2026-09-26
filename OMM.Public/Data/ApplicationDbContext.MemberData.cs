using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMM.Public.Data.Entities;
using OMM.Shared.Models.MasterData;

namespace OMM.Public.Data;

public partial class ApplicationDbContext
{
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<MinerProfileEntity> MinerProfiles => Set<MinerProfileEntity>();
    public DbSet<MineEntity> Mines => Set<MineEntity>();
    public DbSet<MinePositionEntity> MinePositions => Set<MinePositionEntity>();
    public DbSet<BurdenEntity> Burdens => Set<BurdenEntity>();
    public DbSet<IncomeRecordEntity> IncomeRecords => Set<IncomeRecordEntity>();
    public DbSet<ExpenseEntity> Expenses => Set<ExpenseEntity>();
    public DbSet<GoalEntity> Goals => Set<GoalEntity>();
    public DbSet<GoalMineEntity> GoalMines => Set<GoalMineEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    partial void ConfigureMemberData(ModelBuilder modelBuilder)
    {
        ConfigureCurrency(modelBuilder);
        ConfigureMinerProfile(modelBuilder);
        ConfigureMine(modelBuilder);
        ConfigureMinePosition(modelBuilder);
        ConfigureBurden(modelBuilder);
        ConfigureIncomeRecord(modelBuilder);
        ConfigureExpense(modelBuilder);
        ConfigureGoal(modelBuilder);
        ConfigureGoalMine(modelBuilder);
        ConfigureNotification(modelBuilder);
    }

    private static void ConfigureCurrency(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Currency>(entity =>
        {
            entity.ToTable("Currency");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Name_EN).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Currency>().HasData(
            new Currency { Id = 1, Code = "MYR", Name_EN = "Malaysian Ringgit", IsActive = true, CreatedAt = SeedDate },
            new Currency { Id = 2, Code = "SGD", Name_EN = "Singapore Dollar", IsActive = true, CreatedAt = SeedDate },
            new Currency { Id = 3, Code = "USD", Name_EN = "US Dollar", IsActive = true, CreatedAt = SeedDate },
            new Currency { Id = 4, Code = "EUR", Name_EN = "Euro", IsActive = true, CreatedAt = SeedDate });
    }

    private static void ConfigureMinerProfile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MinerProfileEntity>(entity =>
        {
            entity.ToTable("MinerProfile");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.Language).HasMaxLength(50);
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasOne(e => e.User).WithOne().HasForeignKey<MinerProfileEntity>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Country).WithMany().HasForeignKey(e => e.CountryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Currency).WithMany().HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureMine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MineEntity>(entity =>
        {
            ConfigureUserOwnedEntity(entity, "Mine");
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.CurrentValue).HasPrecision(18, 2);
            entity.Property(e => e.PurchaseCost).HasPrecision(18, 2);
            entity.Property(e => e.Growth).HasPrecision(18, 2);
            entity.Property(e => e.GrowthPct).HasPrecision(18, 4);
            entity.Property(e => e.MonthlyIncome).HasPrecision(18, 2);
            entity.HasOne(e => e.Institution).WithMany().HasForeignKey(e => e.InstitutionId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Currency).WithMany().HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.LinkedBurden).WithMany().HasForeignKey(e => e.LinkedBurdenId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureMinePosition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MinePositionEntity>(entity =>
        {
            ConfigureUserOwnedEntity(entity, "MinePosition");
            entity.Property(e => e.Label).IsRequired();
            entity.Property(e => e.Principal).HasPrecision(18, 2);
            entity.Property(e => e.Rate).HasPrecision(18, 4);
            entity.Property(e => e.PurchasePrice).HasPrecision(18, 4);
            entity.HasIndex(e => e.MaturityDate);
            entity.HasOne(e => e.Mine).WithMany(e => e.Positions).HasForeignKey(e => e.MineId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Stock>().WithMany().HasForeignKey(e => e.StockId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureBurden(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BurdenEntity>(entity =>
        {
            ConfigureUserOwnedEntity(entity, "Burden");
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Balance).HasPrecision(18, 2);
            entity.Property(e => e.OriginalAmount).HasPrecision(18, 2);
            entity.Property(e => e.InterestRate).HasPrecision(18, 4);
            entity.Property(e => e.MonthlyPayment).HasPrecision(18, 2);
            entity.HasIndex(e => e.MaturityDate);
            entity.HasOne(e => e.Currency).WithMany().HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.LinkedMine).WithMany().HasForeignKey(e => e.LinkedMineId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureIncomeRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IncomeRecordEntity>(entity =>
        {
            ConfigureUserOwnedEntity(entity, "IncomeRecord");
            entity.Property(e => e.Source).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasOne(e => e.Currency).WithMany().HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Mine).WithMany().HasForeignKey(e => e.MineId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureExpense(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseEntity>(entity =>
        {
            ConfigureUserOwnedEntity(entity, "Expense");
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasOne(e => e.Currency).WithMany().HasForeignKey(e => e.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureGoal(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GoalEntity>(entity =>
        {
            ConfigureUserOwnedEntity(entity, "Goal");
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Target).HasPrecision(18, 2);
            entity.Property(e => e.Current).HasPrecision(18, 2);
        });
    }

    private static void ConfigureGoalMine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GoalMineEntity>(entity =>
        {
            entity.ToTable("GoalMine");
            entity.HasKey(e => new { e.GoalId, e.MineId });
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasIndex(e => new { e.UserId, e.IsDeleted });
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Goal).WithMany(e => e.MineLinks).HasForeignKey(e => e.GoalId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Mine).WithMany(e => e.GoalLinks).HasForeignKey(e => e.MineId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureNotification(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            ConfigureUserOwnedEntity(entity, "Notification");
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(32).IsRequired();
            entity.Property(e => e.DedupeKey).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.DedupeKey }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.ReadAt, e.DismissedAt });
        });
    }

    private static void ConfigureUserOwnedEntity<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : UserOwnedEntity
    {
        entity.ToTable(tableName);
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.UserId, e.Id }).IsUnique();
        entity.HasIndex(e => new { e.UserId, e.IsDeleted });
        entity.HasQueryFilter(e => !e.IsDeleted);
        entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
