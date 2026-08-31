using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMasterDataAuditForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var tables = new[] { "Country", "Exchange", "Institution", "Market", "Sector", "Stock", "SubSector" };
            var auditFields = new[] { "CreatedByUserId", "DeletedByUserId", "ModifiedByUserId" };

            foreach (var table in tables)
            {
                foreach (var auditField in auditFields)
                {
                    migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" DROP CONSTRAINT IF EXISTS \"FK_{table}_AspNetUsers_{auditField}\";");
                    migrationBuilder.Sql($"DROP INDEX IF EXISTS public.\"IX_{table}_{auditField}\";");
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The audit columns intentionally remain unconstrained because admin
            // and public identities live in different PostgreSQL schemas.
        }
    }
}
