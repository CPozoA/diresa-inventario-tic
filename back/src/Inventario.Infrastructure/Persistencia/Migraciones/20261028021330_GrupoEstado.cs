using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class GrupoEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Grupo",
                schema: "catalogos",
                table: "EstadosActivo",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Grupo",
                schema: "catalogos",
                table: "EstadosActivo");
        }
    }
}
