using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations;

public partial class RemoveSectorSearchableNameConstraints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE public."Sector"
                DROP CONSTRAINT IF EXISTS "CK_Sector_SearchableNames";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The searchable-text policy applies to public-user input, not admin-owned master data.
    }
}