using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaFacilitador.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RealmId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cedula = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SepararContabilidadPorSectores = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Feriados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    FactorPago = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feriados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feriados_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Periodos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Etiqueta = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Periodos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Periodos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sectores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sectores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sectores_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Colaboradores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Cedula = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Funcion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SalarioBase = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Colaboradores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Colaboradores_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Colaboradores_Sectores_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CuentasContables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    QboAccountId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    QboAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasContables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuentasContables_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CuentasContables_Sectores_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodoColaboradores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColaboradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodoColaboradores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodoColaboradores_Colaboradores_ColaboradorId",
                        column: x => x.ColaboradorId,
                        principalTable: "Colaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeriodoColaboradores_Periodos_PeriodoId",
                        column: x => x.PeriodoId,
                        principalTable: "Periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Eventos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoColaboradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Horas = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Eventos_PeriodoColaboradores_PeriodoColaboradorId",
                        column: x => x.PeriodoColaboradorId,
                        principalTable: "PeriodoColaboradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_EmpresaId_Cedula",
                table: "Colaboradores",
                columns: new[] { "EmpresaId", "Cedula" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_SectorId",
                table: "Colaboradores",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasContables_EmpresaId_SectorId_Tipo",
                table: "CuentasContables",
                columns: new[] { "EmpresaId", "SectorId", "Tipo" },
                unique: true,
                filter: "[SectorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasContables_SectorId",
                table: "CuentasContables",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Cedula",
                table: "Empresas",
                column: "Cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_RealmId",
                table: "Empresas",
                column: "RealmId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_PeriodoColaboradorId_Fecha_Tipo",
                table: "Eventos",
                columns: new[] { "PeriodoColaboradorId", "Fecha", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feriados_EmpresaId_Fecha",
                table: "Feriados",
                columns: new[] { "EmpresaId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodoColaboradores_ColaboradorId",
                table: "PeriodoColaboradores",
                column: "ColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodoColaboradores_PeriodoId_ColaboradorId",
                table: "PeriodoColaboradores",
                columns: new[] { "PeriodoId", "ColaboradorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Periodos_EmpresaId_FechaInicio_FechaFin",
                table: "Periodos",
                columns: new[] { "EmpresaId", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_Sectores_EmpresaId_Nombre",
                table: "Sectores",
                columns: new[] { "EmpresaId", "Nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuentasContables");

            migrationBuilder.DropTable(
                name: "Eventos");

            migrationBuilder.DropTable(
                name: "Feriados");

            migrationBuilder.DropTable(
                name: "PeriodoColaboradores");

            migrationBuilder.DropTable(
                name: "Colaboradores");

            migrationBuilder.DropTable(
                name: "Periodos");

            migrationBuilder.DropTable(
                name: "Sectores");

            migrationBuilder.DropTable(
                name: "Empresas");
        }
    }
}
