using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DniUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Dni",
                schema: "seguridad",
                table: "Usuarios",
                type: "char(8)",
                unicode: false,
                fixedLength: true,
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Dni",
                schema: "seguridad",
                table: "Usuarios",
                column: "Dni",
                unique: true,
                filter: "[Dni] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Dni",
                schema: "seguridad",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Dni",
                schema: "seguridad",
                table: "Usuarios");
        }
    }
}
