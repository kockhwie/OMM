using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMineMetadataJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "Mine",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Mine");
        }
    }
}
