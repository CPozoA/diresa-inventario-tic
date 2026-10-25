using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ProcesadoresYListas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccesoriosIncluidos",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "Antivirus",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "PuertosPrincipales",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.RenameColumn(
                name: "TarjetaGrafica",
                schema: "inventario",
                table: "Activos",
                newName: "ModeloGrafica");

            migrationBuilder.AddColumn<string>(
                name: "Accesorios",
                schema: "inventario",
                table: "Activos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AntivirusId",
                schema: "inventario",
                table: "Activos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Puertos",
                schema: "inventario",
                table: "Activos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoGrafica",
                schema: "inventario",
                table: "Activos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Antivirus",
                schema: "catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModificadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Antivirus", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activos_AntivirusId",
                schema: "inventario",
                table: "Activos",
                column: "AntivirusId");

            migrationBuilder.CreateIndex(
                name: "IX_Antivirus_Nombre",
                schema: "catalogos",
                table: "Antivirus",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Activos_Antivirus_AntivirusId",
                schema: "inventario",
                table: "Activos",
                column: "AntivirusId",
                principalSchema: "catalogos",
                principalTable: "Antivirus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activos_Antivirus_AntivirusId",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropTable(
                name: "Antivirus",
                schema: "catalogos");

            migrationBuilder.DropIndex(
                name: "IX_Activos_AntivirusId",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "Accesorios",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "AntivirusId",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "Puertos",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "TipoGrafica",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.RenameColumn(
                name: "ModeloGrafica",
                schema: "inventario",
                table: "Activos",
                newName: "TarjetaGrafica");

            migrationBuilder.AddColumn<string>(
                name: "AccesoriosIncluidos",
                schema: "inventario",
                table: "Activos",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Antivirus",
                schema: "inventario",
                table: "Activos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PuertosPrincipales",
                schema: "inventario",
                table: "Activos",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);
        }
    }
}
