using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OMM.Shared.Models.MasterData;

namespace OMM.Public.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<SubSector> SubSectors => Set<SubSector>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Stock> Stocks => Set<Stock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("Country");
            entity.Property(e => e.CountryCode).HasMaxLength(2).IsRequired();
            entity.Property(e => e.CountryName_EN).IsRequired();
            entity.Property(e => e.CountryName_ZH_TW).IsRequired();
            entity.Property(e => e.CountryName_ZH_CN).IsRequired();
            entity.Property(e => e.DefaultCurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasMany(e => e.Exchanges).WithOne(e => e.Country).HasForeignKey(e => e.CountryId);
            entity.HasMany(e => e.Sectors).WithOne(e => e.Country).HasForeignKey(e => e.CountryId);
            entity.HasMany(e => e.Institutions).WithOne(e => e.Country).HasForeignKey(e => e.CountryId);
        });

        modelBuilder.Entity<Exchange>(entity =>
        {
            entity.ToTable("Exchange");
            entity.Property(e => e.ExchangeCode).IsRequired();
            entity.Property(e => e.ExchangeName_EN).IsRequired();
            entity.Property(e => e.ExchangeName_ZH_TW).IsRequired();
            entity.Property(e => e.ExchangeName_ZH_CN).IsRequired();
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasMany(e => e.Markets).WithOne(e => e.Exchange).HasForeignKey(e => e.ExchangeId);
        });

        modelBuilder.Entity<Market>(entity =>
        {
            entity.ToTable("Market");
            entity.Property(e => e.MarketCode).IsRequired();
            entity.Property(e => e.MarketName_EN).IsRequired();
            entity.Property(e => e.MarketName_ZH_TW).IsRequired();
            entity.Property(e => e.MarketName_ZH_CN).IsRequired();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Sector>(entity =>
        {
            entity.ToTable("Sector");
            entity.Property(e => e.SectorCode).IsRequired();
            entity.Property(e => e.SectorName_EN).IsRequired();
            entity.Property(e => e.SectorName_ZH_TW).IsRequired();
            entity.Property(e => e.SectorName_ZH_CN).IsRequired();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<SubSector>(entity =>
        {
            entity.ToTable("SubSector");
            entity.Property(e => e.SubSectorCode).IsRequired();
            entity.Property(e => e.SubSectorName_EN).IsRequired();
            entity.Property(e => e.SubSectorName_ZH_TW).IsRequired();
            entity.Property(e => e.SubSectorName_ZH_CN).IsRequired();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Institution>(entity =>
        {
            entity.ToTable("Institution");
            entity.Property(e => e.InstitutionCode).IsRequired();
            entity.Property(e => e.InstitutionName_EN).IsRequired();
            entity.Property(e => e.InstitutionName_ZH_TW).IsRequired();
            entity.Property(e => e.InstitutionName_ZH_CN).IsRequired();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("Stock");
            entity.Property(e => e.StockCode).IsRequired();
            entity.Property(e => e.ShortName_EN).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasOne(e => e.Market).WithMany(e => e.Stocks).HasForeignKey(e => e.MarketId);
            entity.HasOne(e => e.Sector).WithMany(e => e.Stocks).HasForeignKey(e => e.SectorId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.SubSector).WithMany(e => e.Stocks).HasForeignKey(e => e.SubSectorId).OnDelete(DeleteBehavior.NoAction);
            // Specify precision for decimal properties to avoid silent truncation on SQL Server
            entity.Property(e => e.CurrentPrice).HasPrecision(18, 4);
            entity.Property(e => e.MarketCap).HasPrecision(18, 4);
            entity.Property(e => e.EPS).HasPrecision(18, 4);
            entity.Property(e => e.DPS).HasPrecision(18, 4);
            entity.Property(e => e.NTA).HasPrecision(18, 4);
            entity.Property(e => e.ROE).HasPrecision(18, 4);
            entity.Property(e => e.ROA).HasPrecision(18, 4);
            entity.Property(e => e.DebtToEquity).HasPrecision(18, 4);
            entity.Property(e => e.CurrentRatio).HasPrecision(18, 4);
            entity.Property(e => e.PB).HasPrecision(18, 4);
            entity.Property(e => e.PE).HasPrecision(18, 4);
            entity.Property(e => e.DividendYield).HasPrecision(18, 4);
        });

        ConfigureAudit(modelBuilder.Entity<Country>());
        ConfigureAudit(modelBuilder.Entity<Exchange>());
        ConfigureAudit(modelBuilder.Entity<Market>());
        ConfigureAudit(modelBuilder.Entity<Sector>());
        ConfigureAudit(modelBuilder.Entity<SubSector>());
        ConfigureAudit(modelBuilder.Entity<Institution>());
        ConfigureAudit(modelBuilder.Entity<Stock>());

        modelBuilder.Entity<Country>().HasData(
            new Country { Id = 1, CountryCode = "MY", CountryName_EN = "Malaysia", CountryName_ZH_TW = "Malaysia", CountryName_ZH_CN = "Malaysia", DefaultCurrencyCode = "MYR", IsActive = true, CreatedAt = SeedDate });

        modelBuilder.Entity<Exchange>().HasData(
            new Exchange { Id = 1, CountryId = 1, ExchangeCode = "BURSA", ExchangeName_EN = "Bursa Malaysia", ExchangeName_ZH_TW = "Bursa Malaysia", ExchangeName_ZH_CN = "Bursa Malaysia", IsActive = true, CreatedAt = SeedDate });

        modelBuilder.Entity<Market>().HasData(
            new Market { Id = 1, ExchangeId = 1, MarketCode = "MAIN", MarketName_EN = "Main Market", MarketName_ZH_TW = "Main Market", MarketName_ZH_CN = "Main Market", IsActive = true, CreatedAt = SeedDate },
            new Market { Id = 2, ExchangeId = 1, MarketCode = "ACE", MarketName_EN = "ACE Market", MarketName_ZH_TW = "ACE Market", MarketName_ZH_CN = "ACE Market", IsActive = true, CreatedAt = SeedDate },
            new Market { Id = 3, ExchangeId = 1, MarketCode = "LEAP", MarketName_EN = "LEAP Market", MarketName_ZH_TW = "LEAP Market", MarketName_ZH_CN = "LEAP Market", IsActive = true, CreatedAt = SeedDate });

        modelBuilder.Entity<Sector>().HasData(
            new Sector { Id = 1, CountryId = 1, SectorCode = "FIN-SVC", SectorName_EN = "Financial Services", SectorName_ZH_TW = "Financial Services", SectorName_ZH_CN = "Financial Services", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 2, CountryId = 1, SectorCode = "CONSUMER", SectorName_EN = "Consumer Products & Services", SectorName_ZH_TW = "Consumer Products & Services", SectorName_ZH_CN = "Consumer Products & Services", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 3, CountryId = 1, SectorCode = "INDUSTRIAL", SectorName_EN = "Industrial Products & Services", SectorName_ZH_TW = "Industrial Products & Services", SectorName_ZH_CN = "Industrial Products & Services", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 4, CountryId = 1, SectorCode = "TECH", SectorName_EN = "Technology", SectorName_ZH_TW = "Technology", SectorName_ZH_CN = "Technology", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 5, CountryId = 1, SectorCode = "TEL-MEDIA", SectorName_EN = "Telecommunications & Media", SectorName_ZH_TW = "Telecommunications & Media", SectorName_ZH_CN = "Telecommunications & Media", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 6, CountryId = 1, SectorCode = "HEALTH", SectorName_EN = "Health Care", SectorName_ZH_TW = "Health Care", SectorName_ZH_CN = "Health Care", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 7, CountryId = 1, SectorCode = "PROPERTY", SectorName_EN = "Property", SectorName_ZH_TW = "Property", SectorName_ZH_CN = "Property", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 8, CountryId = 1, SectorCode = "REIT", SectorName_EN = "Real Estate Investment Trusts (REITs)", SectorName_ZH_TW = "Real Estate Investment Trusts (REITs)", SectorName_ZH_CN = "Real Estate Investment Trusts (REITs)", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 9, CountryId = 1, SectorCode = "PLANTATION", SectorName_EN = "Plantation", SectorName_ZH_TW = "Plantation", SectorName_ZH_CN = "Plantation", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 10, CountryId = 1, SectorCode = "ENERGY", SectorName_EN = "Energy", SectorName_ZH_TW = "Energy", SectorName_ZH_CN = "Energy", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 11, CountryId = 1, SectorCode = "CONSTRUCTION", SectorName_EN = "Construction", SectorName_ZH_TW = "Construction", SectorName_ZH_CN = "Construction", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 12, CountryId = 1, SectorCode = "TRANSPORT", SectorName_EN = "Transportation & Logistics", SectorName_ZH_TW = "Transportation & Logistics", SectorName_ZH_CN = "Transportation & Logistics", IsActive = true, CreatedAt = SeedDate },
            new Sector { Id = 13, CountryId = 1, SectorCode = "UTILITIES", SectorName_EN = "Utilities", SectorName_ZH_TW = "Utilities", SectorName_ZH_CN = "Utilities", IsActive = true, CreatedAt = SeedDate });

        modelBuilder.Entity<SubSector>().HasData(
            SeedSubSectors());

        modelBuilder.Entity<Institution>().HasData(
            new Institution { Id = 1, CountryId = 1, InstitutionCode = "MAYBANK", InstitutionName_EN = "Maybank", InstitutionName_ZH_TW = "Maybank", InstitutionName_ZH_CN = "Maybank", InstitutionCategory = InstitutionCategory.Bank, IsActive = true, CreatedAt = SeedDate },
            new Institution { Id = 2, CountryId = 1, InstitutionCode = "CIMB", InstitutionName_EN = "CIMB", InstitutionName_ZH_TW = "CIMB", InstitutionName_ZH_CN = "CIMB", InstitutionCategory = InstitutionCategory.Bank, IsActive = true, CreatedAt = SeedDate },
            new Institution { Id = 3, CountryId = 1, InstitutionCode = "PUBLIC-BANK", InstitutionName_EN = "Public Bank", InstitutionName_ZH_TW = "Public Bank", InstitutionName_ZH_CN = "Public Bank", InstitutionCategory = InstitutionCategory.Bank, IsActive = true, CreatedAt = SeedDate },
            new Institution { Id = 4, CountryId = 1, InstitutionCode = "KWSP", InstitutionName_EN = "KWSP", InstitutionName_ZH_TW = "KWSP", InstitutionName_ZH_CN = "KWSP", InstitutionCategory = InstitutionCategory.EpfKwsp, IsActive = true, CreatedAt = SeedDate },
            new Institution { Id = 5, CountryId = 1, InstitutionCode = "BURSA", InstitutionName_EN = "Bursa Malaysia", InstitutionName_ZH_TW = "Bursa Malaysia", InstitutionName_ZH_CN = "Bursa Malaysia", InstitutionCategory = InstitutionCategory.Other, IsActive = true, CreatedAt = SeedDate });
    }

    private static readonly DateTimeOffset SeedDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static void ConfigureAudit<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : AuditableEntity
    {
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.ModifiedByUserId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.DeletedByUserId).OnDelete(DeleteBehavior.NoAction);
    }

    private static SubSector[] SeedSubSectors() =>
    [
        new() { Id = 1, SectorId = 1, SubSectorCode = "BANKING", SubSectorName_EN = "Banking", SubSectorName_ZH_TW = "Banking", SubSectorName_ZH_CN = "Banking", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 2, SectorId = 1, SubSectorCode = "INSURANCE", SubSectorName_EN = "Insurance", SubSectorName_ZH_TW = "Insurance", SubSectorName_ZH_CN = "Insurance", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 3, SectorId = 1, SubSectorCode = "OTHER-FIN", SubSectorName_EN = "Other Financial Services", SubSectorName_ZH_TW = "Other Financial Services", SubSectorName_ZH_CN = "Other Financial Services", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 4, SectorId = 2, SubSectorCode = "FOOD-BEV", SubSectorName_EN = "Food & Beverages", SubSectorName_ZH_TW = "Food & Beverages", SubSectorName_ZH_CN = "Food & Beverages", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 5, SectorId = 2, SubSectorCode = "RETAILERS", SubSectorName_EN = "Retailers", SubSectorName_ZH_TW = "Retailers", SubSectorName_ZH_CN = "Retailers", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 6, SectorId = 2, SubSectorCode = "AUTOMOTIVE", SubSectorName_EN = "Automotive", SubSectorName_ZH_TW = "Automotive", SubSectorName_ZH_CN = "Automotive", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 7, SectorId = 2, SubSectorCode = "CONSUMER-SVC", SubSectorName_EN = "Consumer Services", SubSectorName_ZH_TW = "Consumer Services", SubSectorName_ZH_CN = "Consumer Services", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 8, SectorId = 2, SubSectorCode = "HOUSEHOLD", SubSectorName_EN = "Household Goods", SubSectorName_ZH_TW = "Household Goods", SubSectorName_ZH_CN = "Household Goods", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 9, SectorId = 2, SubSectorCode = "AGRI", SubSectorName_EN = "Agricultural Products", SubSectorName_ZH_TW = "Agricultural Products", SubSectorName_ZH_CN = "Agricultural Products", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 10, SectorId = 2, SubSectorCode = "TRAVEL", SubSectorName_EN = "Travel Leisure & Hospitality", SubSectorName_ZH_TW = "Travel Leisure & Hospitality", SubSectorName_ZH_CN = "Travel Leisure & Hospitality", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 11, SectorId = 3, SubSectorCode = "BUILDING", SubSectorName_EN = "Building Materials", SubSectorName_ZH_TW = "Building Materials", SubSectorName_ZH_CN = "Building Materials", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 12, SectorId = 3, SubSectorCode = "CHEMICALS", SubSectorName_EN = "Chemicals", SubSectorName_ZH_TW = "Chemicals", SubSectorName_ZH_CN = "Chemicals", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 13, SectorId = 3, SubSectorCode = "METALS", SubSectorName_EN = "Metals", SubSectorName_ZH_TW = "Metals", SubSectorName_ZH_CN = "Metals", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 14, SectorId = 3, SubSectorCode = "PACKAGING", SubSectorName_EN = "Packaging Materials", SubSectorName_ZH_TW = "Packaging Materials", SubSectorName_ZH_CN = "Packaging Materials", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 15, SectorId = 3, SubSectorCode = "DIVERSIFIED", SubSectorName_EN = "Diversified Industrials", SubSectorName_ZH_TW = "Diversified Industrials", SubSectorName_ZH_CN = "Diversified Industrials", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 16, SectorId = 3, SubSectorCode = "IND-ENGINEERING", SubSectorName_EN = "Industrial Engineering", SubSectorName_ZH_TW = "Industrial Engineering", SubSectorName_ZH_CN = "Industrial Engineering", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 17, SectorId = 4, SubSectorCode = "SEMICONDUCTORS", SubSectorName_EN = "Semiconductors", SubSectorName_ZH_TW = "Semiconductors", SubSectorName_ZH_CN = "Semiconductors", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 18, SectorId = 4, SubSectorCode = "SOFTWARE", SubSectorName_EN = "Software", SubSectorName_ZH_TW = "Software", SubSectorName_ZH_CN = "Software", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 19, SectorId = 4, SubSectorCode = "DIGITAL", SubSectorName_EN = "Digital Services", SubSectorName_ZH_TW = "Digital Services", SubSectorName_ZH_CN = "Digital Services", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 20, SectorId = 4, SubSectorCode = "HARDWARE", SubSectorName_EN = "Hardware", SubSectorName_ZH_TW = "Hardware", SubSectorName_ZH_CN = "Hardware", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 21, SectorId = 5, SubSectorCode = "TELCO-SVC", SubSectorName_EN = "Telecommunications Service Providers", SubSectorName_ZH_TW = "Telecommunications Service Providers", SubSectorName_ZH_CN = "Telecommunications Service Providers", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 22, SectorId = 5, SubSectorCode = "MEDIA", SubSectorName_EN = "Media & Advertising", SubSectorName_ZH_TW = "Media & Advertising", SubSectorName_ZH_CN = "Media & Advertising", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 23, SectorId = 5, SubSectorCode = "TELCO-EQUIP", SubSectorName_EN = "Telecommunications Equipment", SubSectorName_ZH_TW = "Telecommunications Equipment", SubSectorName_ZH_CN = "Telecommunications Equipment", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 24, SectorId = 6, SubSectorCode = "HEALTHCARE", SubSectorName_EN = "Healthcare Providers", SubSectorName_ZH_TW = "Healthcare Providers", SubSectorName_ZH_CN = "Healthcare Providers", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 25, SectorId = 6, SubSectorCode = "PHARMA", SubSectorName_EN = "Pharmaceuticals", SubSectorName_ZH_TW = "Pharmaceuticals", SubSectorName_ZH_CN = "Pharmaceuticals", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 26, SectorId = 6, SubSectorCode = "HEALTH-EQUIP", SubSectorName_EN = "Healthcare Equipment & Supplies", SubSectorName_ZH_TW = "Healthcare Equipment & Supplies", SubSectorName_ZH_CN = "Healthcare Equipment & Supplies", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 27, SectorId = 7, SubSectorCode = "PROPERTY-DEV", SubSectorName_EN = "Property Development", SubSectorName_ZH_TW = "Property Development", SubSectorName_ZH_CN = "Property Development", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 28, SectorId = 7, SubSectorCode = "PROPERTY-INV", SubSectorName_EN = "Property Investment & Management", SubSectorName_ZH_TW = "Property Investment & Management", SubSectorName_ZH_CN = "Property Investment & Management", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 29, SectorId = 8, SubSectorCode = "COMMERCIAL", SubSectorName_EN = "Commercial", SubSectorName_ZH_TW = "Commercial", SubSectorName_ZH_CN = "Commercial", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 30, SectorId = 8, SubSectorCode = "RETAIL", SubSectorName_EN = "Retail", SubSectorName_ZH_TW = "Retail", SubSectorName_ZH_CN = "Retail", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 31, SectorId = 8, SubSectorCode = "INDUSTRIAL", SubSectorName_EN = "Industrial", SubSectorName_ZH_TW = "Industrial", SubSectorName_ZH_CN = "Industrial", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 32, SectorId = 8, SubSectorCode = "HOSPITALITY", SubSectorName_EN = "Hospitality", SubSectorName_ZH_TW = "Hospitality", SubSectorName_ZH_CN = "Hospitality", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 33, SectorId = 8, SubSectorCode = "HEALTHCARE-REIT", SubSectorName_EN = "Healthcare", SubSectorName_ZH_TW = "Healthcare", SubSectorName_ZH_CN = "Healthcare", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 34, SectorId = 9, SubSectorCode = "UPSTREAM", SubSectorName_EN = "Upstream Plantation", SubSectorName_ZH_TW = "Upstream Plantation", SubSectorName_ZH_CN = "Upstream Plantation", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 35, SectorId = 9, SubSectorCode = "INTEGRATED", SubSectorName_EN = "Integrated Cultivation", SubSectorName_ZH_TW = "Integrated Cultivation", SubSectorName_ZH_CN = "Integrated Cultivation", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 36, SectorId = 10, SubSectorCode = "OIL-GAS-PROD", SubSectorName_EN = "Oil & Gas Producers", SubSectorName_ZH_TW = "Oil & Gas Producers", SubSectorName_ZH_CN = "Oil & Gas Producers", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 37, SectorId = 10, SubSectorCode = "OIL-GAS-EQUIP", SubSectorName_EN = "Oil & Gas Equipment & Services", SubSectorName_ZH_TW = "Oil & Gas Equipment & Services", SubSectorName_ZH_CN = "Oil & Gas Equipment & Services", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 38, SectorId = 10, SubSectorCode = "RENEWABLE", SubSectorName_EN = "Renewable Energy", SubSectorName_ZH_TW = "Renewable Energy", SubSectorName_ZH_CN = "Renewable Energy", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 39, SectorId = 11, SubSectorCode = "CIVIL", SubSectorName_EN = "Civil Engineering", SubSectorName_ZH_TW = "Civil Engineering", SubSectorName_ZH_CN = "Civil Engineering", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 40, SectorId = 11, SubSectorCode = "HEAVY", SubSectorName_EN = "Heavy Construction", SubSectorName_ZH_TW = "Heavy Construction", SubSectorName_ZH_CN = "Heavy Construction", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 41, SectorId = 11, SubSectorCode = "SPECIALISED", SubSectorName_EN = "Specialised Construction", SubSectorName_ZH_TW = "Specialised Construction", SubSectorName_ZH_CN = "Specialised Construction", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 42, SectorId = 12, SubSectorCode = "LOGISTICS", SubSectorName_EN = "Logistics Services", SubSectorName_ZH_TW = "Logistics Services", SubSectorName_ZH_CN = "Logistics Services", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 43, SectorId = 12, SubSectorCode = "PORTS", SubSectorName_EN = "Ports & Shipping", SubSectorName_ZH_TW = "Ports & Shipping", SubSectorName_ZH_CN = "Ports & Shipping", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 44, SectorId = 12, SubSectorCode = "AIRLINES", SubSectorName_EN = "Airlines & Aviation", SubSectorName_ZH_TW = "Airlines & Aviation", SubSectorName_ZH_CN = "Airlines & Aviation", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 45, SectorId = 12, SubSectorCode = "ROAD-RAIL", SubSectorName_EN = "Road & Rail", SubSectorName_ZH_TW = "Road & Rail", SubSectorName_ZH_CN = "Road & Rail", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 46, SectorId = 13, SubSectorCode = "ELECTRICITY", SubSectorName_EN = "Electricity", SubSectorName_ZH_TW = "Electricity", SubSectorName_ZH_CN = "Electricity", IsActive = true, CreatedAt = SeedDate },
        new() { Id = 47, SectorId = 13, SubSectorCode = "GAS-WATER", SubSectorName_EN = "Gas & Water Distribution", SubSectorName_ZH_TW = "Gas & Water Distribution", SubSectorName_ZH_CN = "Gas & Water Distribution", IsActive = true, CreatedAt = SeedDate }
    ];
}