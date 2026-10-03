using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Onboarding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Market = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Nationality = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    IdentifierType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    IdentifierValue = table.Column<string>(type: "varchar(13)", unicode: false, maxLength: 13, nullable: false),
                    IdentifierIssuingCountry = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BlockingIdentity = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true, computedColumnSql: "CONCAT([Market], '|', [IdentifierType], '|', [IdentifierIssuingCountry], '|', [IdentifierValue])", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Applications_BlockingIdentity",
                table: "Applications",
                column: "BlockingIdentity",
                unique: true,
                filter: "[Status] <> 'Rejected'");

            migrationBuilder.CreateIndex(
                name: "UX_Applications_IdempotencyKey",
                table: "Applications",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Applications");
        }
    }
}
