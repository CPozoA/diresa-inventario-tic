using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Activos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventario");

            migrationBuilder.CreateTable(
                name: "Activos",
                schema: "inventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SedeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstablecimientoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoPatrimonial = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TipoActivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarcaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NumeroSerie = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UbicacionFisica = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Responsable = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EstadoActivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Conservacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Criticidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaAdquisicion = table.Column<DateOnly>(type: "date", nullable: true),
                    Proveedor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NumeroOrdenCompra = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TipoAdquisicion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ValorAdquisicion = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    VidaUtilAnios = table.Column<int>(type: "int", nullable: true),
                    FinGarantia = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaRenovacion = table.Column<DateOnly>(type: "date", nullable: true),
                    Procesador = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VelocidadGeneracion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Nucleos = table.Column<int>(type: "int", nullable: true),
                    TarjetaGrafica = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PantallaPulgadas = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    SistemaOperativoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersionSistemaOperativo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NombreEquipo = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: true),
                    DominioGrupo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DireccionIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    DireccionMac = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: true),
                    Antivirus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiscoCifrado = table.Column<bool>(type: "bit", nullable: true),
                    PuertosPrincipales = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AccesoriosIncluidos = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModificadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activos_Areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "catalogos",
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activos_Establecimientos_EstablecimientoId",
                        column: x => x.EstablecimientoId,
                        principalSchema: "organizacion",
                        principalTable: "Establecimientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activos_EstadosActivo_EstadoActivoId",
                        column: x => x.EstadoActivoId,
                        principalSchema: "catalogos",
                        principalTable: "EstadosActivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activos_Marcas_MarcaId",
                        column: x => x.MarcaId,
                        principalSchema: "catalogos",
                        principalTable: "Marcas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activos_Sedes_SedeId",
                        column: x => x.SedeId,
                        principalSchema: "organizacion",
                        principalTable: "Sedes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activos_SistemasOperativos_SistemaOperativoId",
                        column: x => x.SistemaOperativoId,
                        principalSchema: "catalogos",
                        principalTable: "SistemasOperativos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activos_TiposActivo_TipoActivoId",
                        column: x => x.TipoActivoId,
                        principalSchema: "catalogos",
                        principalTable: "TiposActivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Discos",
                schema: "inventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Ranura = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CapacidadGb = table.Column<int>(type: "int", nullable: false),
                    MarcaModelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NumeroSerie = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Uso = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Discos_Activos_ActivoId",
                        column: x => x.ActivoId,
                        principalSchema: "inventario",
                        principalTable: "Activos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModulosRam",
                schema: "inventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Ranura = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CapacidadGb = table.Column<int>(type: "int", nullable: false),
                    VelocidadMhz = table.Column<int>(type: "int", nullable: true),
                    MarcaModelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulosRam", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModulosRam_Activos_ActivoId",
                        column: x => x.ActivoId,
                        principalSchema: "inventario",
                        principalTable: "Activos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activos_AreaId",
                schema: "inventario",
                table: "Activos",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Activos_EstablecimientoId",
                schema: "inventario",
                table: "Activos",
                column: "EstablecimientoId");

            migrationBuilder.CreateIndex(
                name: "IX_Activos_EstadoActivoId",
                schema: "inventario",
                table: "Activos",
                column: "EstadoActivoId");

            migrationBuilder.CreateIndex(
                name: "IX_Activos_FechaRenovacion",
                schema: "inventario",
                table: "Activos",
                column: "FechaRenovacion");

            migrationBuilder.CreateIndex(
                name: "IX_Activos_FinGarantia",
                schema: "inventario",
                table: "Activos",
                column: "FinGarantia");

            migrationBuilder.CreateIndex(
                name: "IX_Activos_MarcaId",
                schema: "inventario",
                table: "Activos",
                column: "MarcaId");

            migrationBuilder.CreateIndex(
                name: "IX_Activos_SedeId_CodigoPatrimonial",
                schema: "inventario",
                table: "Activos",
                columns: new[] { "SedeId", "CodigoPatrimonial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activos_SedeId_NumeroSerie",
                schema: "inventario",
                table: "Activos",
                columns: new[] { "SedeId", "NumeroSerie" });

            migrationBuilder.CreateIndex(
                name: "IX_Activos_SistemaOperativoId",
                schema: "inventario",
                table: "Activos",
                column: "SistemaOperativoId");

            migrationBuilder.CreateIndex(
                name: "IX_Activos_TipoActivoId",
                schema: "inventario",
                table: "Activos",
                column: "TipoActivoId");

            migrationBuilder.CreateIndex(
                name: "IX_Discos_ActivoId",
                schema: "inventario",
                table: "Discos",
                column: "ActivoId");

            migrationBuilder.CreateIndex(
                name: "IX_ModulosRam_ActivoId",
                schema: "inventario",
                table: "ModulosRam",
                column: "ActivoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Discos",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "ModulosRam",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "Activos",
                schema: "inventario");
        }
    }
}
