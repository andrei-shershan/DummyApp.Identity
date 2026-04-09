using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace DummyApp.Identity.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCountries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Countries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Country = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }
    }
}
