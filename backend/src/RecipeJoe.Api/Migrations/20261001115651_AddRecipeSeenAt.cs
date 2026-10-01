using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeJoe.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeSeenAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SeenAt",
                table: "Recipes",
                type: "timestamp with time zone",
                nullable: true);

            // Existing Recipes predate the "Neu" marker, so they're backfilled as already seen.
            migrationBuilder.Sql("""UPDATE "Recipes" SET "SeenAt" = "CreatedAt";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeenAt",
                table: "Recipes");
        }
    }
}
