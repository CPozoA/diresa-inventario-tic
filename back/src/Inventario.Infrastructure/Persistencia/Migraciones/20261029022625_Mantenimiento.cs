using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Mantenimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ProximoMantenimiento",
                schema: "inventario",
                table: "Activos",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Mantenimientos",
                schema: "inventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SedeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Ejecutor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TecnicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Proveedor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OrdenServicio = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Costo = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: true),
                    TrabajoRealizado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Resultado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModificadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mantenimientos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mantenimientos_Activos_ActivoId",
                        column: x => x.ActivoId,
                        principalSchema: "inventario",
                        principalTable: "Activos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mantenimientos_Sedes_SedeId",
                        column: x => x.SedeId,
                        principalSchema: "organizacion",
                        principalTable: "Sedes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mantenimientos_Usuarios_TecnicoId",
                        column: x => x.TecnicoId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activos_SedeId_ProximoMantenimiento",
                schema: "inventario",
                table: "Activos",
                columns: new[] { "SedeId", "ProximoMantenimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_Mantenimientos_ActivoId_FechaInicio",
                schema: "inventario",
                table: "Mantenimientos",
                columns: new[] { "ActivoId", "FechaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Mantenimientos_SedeId_FechaFin_FechaInicio",
                schema: "inventario",
                table: "Mantenimientos",
                columns: new[] { "SedeId", "FechaFin", "FechaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Mantenimientos_TecnicoId",
                schema: "inventario",
                table: "Mantenimientos",
                column: "TecnicoId");

            migrationBuilder.CreateIndex(
                name: "UX_Mantenimientos_ActivoAbierto",
                schema: "inventario",
                table: "Mantenimientos",
                column: "ActivoId",
                unique: true,
                filter: "[FechaFin] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mantenimientos",
                schema: "inventario");

            migrationBuilder.DropIndex(
                name: "IX_Activos_SedeId_ProximoMantenimiento",
                schema: "inventario",
                table: "Activos");

            migrationBuilder.DropColumn(
                name: "ProximoMantenimiento",
                schema: "inventario",
                table: "Activos");
        }
    }
}
