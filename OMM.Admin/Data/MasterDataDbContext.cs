using Microsoft.EntityFrameworkCore;
using OMM.Shared.DecisionSupport;
using OMM.Shared.Models;
using OMM.Shared.Models.MasterData;


namespace OMM.Admin.Data;

public class MasterDataDbContext(DbContextOptions<MasterDataDbContext> options) : DbContext(options)
{
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<SubSector> SubSectors => Set<SubSector>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<SignalTemplate> DecisionSignalTemplates => Set<SignalTemplate>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SignalTemplate>(entity =>
        {
            entity.ToTable("DecisionSignalTemplate", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(40);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(600).IsRequired();
            entity.Property(e => e.SampleQuery).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.DefaultSecurity).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DefaultSector).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DefaultConcern).HasMaxLength(60).IsRequired();
            entity.Property(e => e.DefaultScope).HasMaxLength(60).IsRequired();
            entity.Property(e => e.DefaultObjective).HasMaxLength(60).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedByUserId).HasColumnType("text");
            entity.Property(e => e.ModifiedByUserId).HasColumnType("text");
            entity.Property(e => e.DeletedByUserId).HasColumnType("text");
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasIndex(e => new { e.IsActive, e.SortOrder });
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("Country", "public");
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasMany(e => e.Exchanges).WithOne(e => e.Country).HasForeignKey(e => e.CountryId);
            entity.HasMany(e => e.Sectors).WithOne(e => e.Country).HasForeignKey(e => e.CountryId);
            entity.HasMany(e => e.Institutions).WithOne(e => e.Country).HasForeignKey(e => e.CountryId);
        });

        modelBuilder.Entity<Exchange>(entity =>
        {
            entity.ToTable("Exchange", "public");
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasMany(e => e.Markets).WithOne(e => e.Exchange).HasForeignKey(e => e.ExchangeId);
        });

        modelBuilder.Entity<Market>(entity =>
        {
            entity.ToTable("Market", "public");
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasMany(e => e.Stocks).WithOne(e => e.Market).HasForeignKey(e => e.MarketId);
        });

        modelBuilder.Entity<Sector>(entity =>
        {
            entity.ToTable("Sector", "public");
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasMany(e => e.SubSectors).WithOne(e => e.Sector).HasForeignKey(e => e.SectorId);
            entity.HasMany(e => e.Stocks).WithOne(e => e.Sector).HasForeignKey(e => e.SectorId);
        });

        modelBuilder.Entity<SubSector>(entity =>
        {
            entity.ToTable("SubSector", "public");
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasMany(e => e.Stocks).WithOne(e => e.SubSector).HasForeignKey(e => e.SubSectorId);
        });

        modelBuilder.Entity<Institution>(entity =>
        {
            entity.ToTable("Institution", "public");
            entity.HasQueryFilter(e => !e.IsDeleted);

            // Admin identity is stored in the 'admin' schema. Audit user IDs
            // are plain strings because public master data cannot reference
            // either application's identity table safely.
            entity.Property(e => e.CreatedByUserId).HasColumnType("text");
            entity.Property(e => e.ModifiedByUserId).HasColumnType("text");
            entity.Property(e => e.DeletedByUserId).HasColumnType("text");
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("Stock", "public");
            entity.HasQueryFilter(e => !e.IsDeleted);

            // Audit userId columns are plain strings — no FK to AspNetUsers.
            // Identity lives in the 'admin' schema (ApplicationDbContext) and
            // MasterDataDbContext must not generate a cross-schema FK constraint.
            entity.Property(e => e.CreatedByUserId).HasColumnType("text");
            entity.Property(e => e.ModifiedByUserId).HasColumnType("text");
            entity.Property(e => e.DeletedByUserId).HasColumnType("text");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSetting", "public");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Value).HasColumnType("text").IsRequired();
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.ModifiedByUserId).HasColumnType("text");
        });
    }
}
