using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaFacilitador.Migrations
{
    /// <inheritdoc />
    public partial class Init_Stores_To_Postgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyConnections",
                columns: table => new
                {
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyConnections", x => x.RealmId);
                });

            migrationBuilder.CreateTable(
                name: "CompanyProfiles",
                columns: table => new
                {
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyProfiles", x => x.RealmId);
                });

            migrationBuilder.CreateTable(
                name: "ParametrizacionEmpresas",
                columns: table => new
                {
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    JsonConfig = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrizacionEmpresas", x => x.RealmId);
                });

            migrationBuilder.CreateTable(
                name: "QboTokens",
                columns: table => new
                {
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QboTokens", x => x.RealmId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyConnections_RealmId",
                table: "CompanyConnections",
                column: "RealmId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_RealmId",
                table: "CompanyProfiles",
                column: "RealmId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParametrizacionEmpresas_RealmId",
                table: "ParametrizacionEmpresas",
                column: "RealmId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QboTokens_RealmId",
                table: "QboTokens",
                column: "RealmId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyConnections");

            migrationBuilder.DropTable(
                name: "CompanyProfiles");

            migrationBuilder.DropTable(
                name: "ParametrizacionEmpresas");

            migrationBuilder.DropTable(
                name: "QboTokens");
        }
    }
}
