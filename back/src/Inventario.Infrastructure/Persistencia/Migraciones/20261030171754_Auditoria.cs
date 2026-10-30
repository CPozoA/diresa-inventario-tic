using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Auditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                schema: "seguridad",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SedeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Modulo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_ActivoId_Fecha",
                schema: "seguridad",
                table: "Auditoria",
                columns: new[] { "ActivoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Fecha",
                schema: "seguridad",
                table: "Auditoria",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_SedeId_Fecha",
                schema: "seguridad",
                table: "Auditoria",
                columns: new[] { "SedeId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_UsuarioId_Fecha",
                schema: "seguridad",
                table: "Auditoria",
                columns: new[] { "UsuarioId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria",
                schema: "seguridad");
        }
    }
}
