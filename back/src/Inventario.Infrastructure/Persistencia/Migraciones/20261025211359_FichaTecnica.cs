using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class FichaTecnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ficha",
                schema: "catalogos",
                table: "TiposActivo",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ficha",
                schema: "catalogos",
                table: "TiposActivo");
        }
    }
}
