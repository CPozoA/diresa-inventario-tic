using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Procesadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Procesador",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "VelocidadGeneracion",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.AddColumn<string>(
                name: "GeneracionProcesador",
                schema: "inventario",
                table: "Activos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcesadorId",
                schema: "inventario",
                table: "Activos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Procesadores",
                schema: "catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Generaciones = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModificadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Procesadores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activos_ProcesadorId",
                schema: "inventario",
                table: "Activos",
                column: "ProcesadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Procesadores_Nombre",
                schema: "catalogos",
                table: "Procesadores",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Activos_Procesadores_ProcesadorId",
                schema: "inventario",
                table: "Activos",
                column: "ProcesadorId",
                principalSchema: "catalogos",
                principalTable: "Procesadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activos_Procesadores_ProcesadorId",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropTable(
                name: "Procesadores",
                schema: "catalogos");

            migrationBuilder.DropIndex(
                name: "IX_Activos_ProcesadorId",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "GeneracionProcesador",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "ProcesadorId",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.AddColumn<string>(
                name: "Procesador",
                schema: "inventario",
                table: "Activos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VelocidadGeneracion",
                schema: "inventario",
                table: "Activos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
