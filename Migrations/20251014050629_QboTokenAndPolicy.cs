using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaFacilitador.Migrations
{
    /// <inheritdoc />
    public partial class QboTokenAndPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Colaboradores_Empresas_EmpresaId",
                table: "Colaboradores");

            migrationBuilder.DropForeignKey(
                name: "FK_Colaboradores_Sectores_SectorId",
                table: "Colaboradores");

            migrationBuilder.DropForeignKey(
                name: "FK_PeriodoColaboradores_Colaboradores_ColaboradorId",
                table: "PeriodoColaboradores");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Colaboradores",
                table: "Colaboradores");

            migrationBuilder.DropColumn(
                name: "SepararContabilidadPorSectores",
                table: "Empresas");

            migrationBuilder.RenameTable(
                name: "Colaboradores",
                newName: "Colaborador");

            migrationBuilder.RenameIndex(
                name: "IX_Colaboradores_SectorId",
                table: "Colaborador",
                newName: "IX_Colaborador_SectorId");

            migrationBuilder.RenameIndex(
                name: "IX_Colaboradores_EmpresaId_Cedula",
                table: "Colaborador",
                newName: "IX_Colaborador_EmpresaId_Cedula");

            migrationBuilder.AddColumn<string>(
                name: "PayPolicy",
                table: "Empresas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Colaborador",
                table: "Colaborador",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "PayrollQboTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RealmId = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollQboTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollQboTokens_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollQboTokens_EmpresaId_RealmId",
                table: "PayrollQboTokens",
                columns: new[] { "EmpresaId", "RealmId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Colaborador_Empresas_EmpresaId",
                table: "Colaborador",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Colaborador_Sectores_SectorId",
                table: "Colaborador",
                column: "SectorId",
                principalTable: "Sectores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PeriodoColaboradores_Colaborador_ColaboradorId",
                table: "PeriodoColaboradores",
                column: "ColaboradorId",
                principalTable: "Colaborador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Colaborador_Empresas_EmpresaId",
                table: "Colaborador");

            migrationBuilder.DropForeignKey(
                name: "FK_Colaborador_Sectores_SectorId",
                table: "Colaborador");

            migrationBuilder.DropForeignKey(
                name: "FK_PeriodoColaboradores_Colaborador_ColaboradorId",
                table: "PeriodoColaboradores");

            migrationBuilder.DropTable(
                name: "PayrollQboTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Colaborador",
                table: "Colaborador");

            migrationBuilder.DropColumn(
                name: "PayPolicy",
                table: "Empresas");

            migrationBuilder.RenameTable(
                name: "Colaborador",
                newName: "Colaboradores");

            migrationBuilder.RenameIndex(
                name: "IX_Colaborador_SectorId",
                table: "Colaboradores",
                newName: "IX_Colaboradores_SectorId");

            migrationBuilder.RenameIndex(
                name: "IX_Colaborador_EmpresaId_Cedula",
                table: "Colaboradores",
                newName: "IX_Colaboradores_EmpresaId_Cedula");

            migrationBuilder.AddColumn<bool>(
                name: "SepararContabilidadPorSectores",
                table: "Empresas",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Colaboradores",
                table: "Colaboradores",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Colaboradores_Empresas_EmpresaId",
                table: "Colaboradores",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Colaboradores_Sectores_SectorId",
                table: "Colaboradores",
                column: "SectorId",
                principalTable: "Sectores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PeriodoColaboradores_Colaboradores_ColaboradorId",
                table: "PeriodoColaboradores",
                column: "ColaboradorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
