using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHorarioAlquilerConfigurable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HorarioAlquiler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    AperturaEntreSemana = table.Column<TimeSpan>(type: "time", nullable: false),
                    CierreEntreSemana = table.Column<TimeSpan>(type: "time", nullable: false),
                    AperturaFinDeSemana = table.Column<TimeSpan>(type: "time", nullable: false),
                    CierreFinDeSemana = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorarioAlquiler", x => x.Id);
                    table.CheckConstraint("CK_HorarioAlquiler_EntreSemana", "[CierreEntreSemana] > [AperturaEntreSemana]");
                    table.CheckConstraint("CK_HorarioAlquiler_FilaUnica", "[Id] = 1");
                    table.CheckConstraint("CK_HorarioAlquiler_FinDeSemana", "[CierreFinDeSemana] > [AperturaFinDeSemana]");
                });

            migrationBuilder.InsertData(
                table: "HorarioAlquiler",
                columns: new[] { "Id", "AperturaEntreSemana", "AperturaFinDeSemana", "CierreEntreSemana", "CierreFinDeSemana" },
                values: new object[] { 1, new TimeSpan(0, 8, 0, 0, 0), new TimeSpan(0, 8, 0, 0, 0), new TimeSpan(0, 20, 0, 0, 0), new TimeSpan(0, 17, 0, 0, 0) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HorarioAlquiler");
        }
    }
}
