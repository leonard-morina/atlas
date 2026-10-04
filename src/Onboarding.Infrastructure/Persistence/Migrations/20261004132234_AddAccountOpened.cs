using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Onboarding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountOpened : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Applications",
                type: "varchar(34)",
                unicode: false,
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AccountOpenedAt",
                table: "Applications",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "AccountOpenedAt",
                table: "Applications");
        }
    }
}
