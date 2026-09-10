using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations;

public partial class AddSectorSearchableNameConstraints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE public."Sector"
                DROP CONSTRAINT IF EXISTS "CK_Sector_SearchableNames";

            ALTER TABLE public."Sector"
                ADD CONSTRAINT "CK_Sector_SearchableNames"
                CHECK (
                    "SectorName_EN" ~ '^[[:alpha:][:digit:] .,&''()/-]+$'
                    AND "SectorName_ZH_TW" ~ '^[[:alpha:][:digit:] .,&''()/-]+$'
                    AND "SectorName_EN" !~ ('[' || chr(592) || '-' || chr(767) || chr(7424) || '-' || chr(7551) || chr(119808) || '-' || chr(122879) || ']')
                    AND "SectorName_ZH_TW" !~ ('[' || chr(592) || '-' || chr(767) || chr(7424) || '-' || chr(7551) || chr(119808) || '-' || chr(122879) || ']')
                );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE public."Sector"
                DROP CONSTRAINT IF EXISTS "CK_Sector_SearchableNames";
            """);
    }
}
