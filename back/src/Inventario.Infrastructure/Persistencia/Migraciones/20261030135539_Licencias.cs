using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Licencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Licencias",
                schema: "inventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SedeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Producto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Proveedor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Puestos = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    DocumentoCompra = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Costo = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Archivada = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModificadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Licencias_Sedes_SedeId",
                        column: x => x.SedeId,
                        principalSchema: "organizacion",
                        principalTable: "Sedes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquiposLicencia",
                schema: "inventario",
                columns: table => new
                {
                    LicenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignadoEn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquiposLicencia", x => new { x.LicenciaId, x.ActivoId });
                    table.ForeignKey(
                        name: "FK_EquiposLicencia_Activos_ActivoId",
                        column: x => x.ActivoId,
                        principalSchema: "inventario",
                        principalTable: "Activos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EquiposLicencia_Licencias_LicenciaId",
                        column: x => x.LicenciaId,
                        principalSchema: "inventario",
                        principalTable: "Licencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquiposLicencia_ActivoId",
                schema: "inventario",
                table: "EquiposLicencia",
                column: "ActivoId");

            migrationBuilder.CreateIndex(
                name: "IX_Licencias_SedeId_Archivada_FechaFin",
                schema: "inventario",
                table: "Licencias",
                columns: new[] { "SedeId", "Archivada", "FechaFin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquiposLicencia",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "Licencias",
                schema: "inventario");
        }
    }
}
