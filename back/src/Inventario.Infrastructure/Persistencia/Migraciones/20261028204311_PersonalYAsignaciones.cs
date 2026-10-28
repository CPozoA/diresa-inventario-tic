using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PersonalYAsignaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Personal",
                schema: "organizacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SedeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dni = table.Column<string>(type: "char(8)", unicode: false, fixedLength: true, maxLength: 8, nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ApellidoMaterno = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Nombres = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(230)", maxLength: 230, nullable: false, collation: "Modern_Spanish_CI_AI"),
                    EstablecimientoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VerificadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModificadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Personal_Areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "catalogos",
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personal_Establecimientos_EstablecimientoId",
                        column: x => x.EstablecimientoId,
                        principalSchema: "organizacion",
                        principalTable: "Establecimientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personal_Sedes_SedeId",
                        column: x => x.SedeId,
                        principalSchema: "organizacion",
                        principalTable: "Sedes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Asignaciones",
                schema: "inventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SedeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaEntrega = table.Column<DateOnly>(type: "date", nullable: false),
                    ConservacionEntrega = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaDevolucion = table.Column<DateOnly>(type: "date", nullable: true),
                    ConservacionDevolucion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModificadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asignaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asignaciones_Activos_ActivoId",
                        column: x => x.ActivoId,
                        principalSchema: "inventario",
                        principalTable: "Activos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Asignaciones_Personal_PersonaId",
                        column: x => x.PersonaId,
                        principalSchema: "organizacion",
                        principalTable: "Personal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asignaciones_Sedes_SedeId",
                        column: x => x.SedeId,
                        principalSchema: "organizacion",
                        principalTable: "Sedes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asignaciones_ActivoId_FechaEntrega",
                schema: "inventario",
                table: "Asignaciones",
                columns: new[] { "ActivoId", "FechaEntrega" });

            migrationBuilder.CreateIndex(
                name: "IX_Asignaciones_PersonaId_FechaDevolucion",
                schema: "inventario",
                table: "Asignaciones",
                columns: new[] { "PersonaId", "FechaDevolucion" });

            migrationBuilder.CreateIndex(
                name: "IX_Asignaciones_SedeId_FechaDevolucion",
                schema: "inventario",
                table: "Asignaciones",
                columns: new[] { "SedeId", "FechaDevolucion" });

            migrationBuilder.CreateIndex(
                name: "IX_Asignaciones_SedeId_FechaEntrega",
                schema: "inventario",
                table: "Asignaciones",
                columns: new[] { "SedeId", "FechaEntrega" });

            migrationBuilder.CreateIndex(
                name: "UX_Asignaciones_ActivoVigente",
                schema: "inventario",
                table: "Asignaciones",
                column: "ActivoId",
                unique: true,
                filter: "[FechaDevolucion] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Personal_AreaId",
                schema: "organizacion",
                table: "Personal",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Personal_EstablecimientoId",
                schema: "organizacion",
                table: "Personal",
                column: "EstablecimientoId");

            migrationBuilder.CreateIndex(
                name: "IX_Personal_SedeId_Activa_ApellidoPaterno",
                schema: "organizacion",
                table: "Personal",
                columns: new[] { "SedeId", "Activa", "ApellidoPaterno" });

            migrationBuilder.CreateIndex(
                name: "IX_Personal_SedeId_Dni",
                schema: "organizacion",
                table: "Personal",
                columns: new[] { "SedeId", "Dni" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Asignaciones",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "Personal",
                schema: "organizacion");
        }
    }
}
