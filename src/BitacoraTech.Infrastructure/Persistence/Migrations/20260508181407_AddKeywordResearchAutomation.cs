using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitacoraTech.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKeywordResearchAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cluster",
                table: "Keywords",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Intent",
                table: "Keywords",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "informational");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSeenAt",
                table: "Keywords",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<int>(
                name: "Mentions",
                table: "Keywords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OpportunityScore",
                table: "Keywords",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Keywords",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<decimal>(
                name: "TrendScore",
                table: "Keywords",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "KeywordResearchRuns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "KeywordResearchRuns",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_SiteId_Cluster",
                table: "Keywords",
                columns: new[] { "SiteId", "Cluster" });

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_SiteId_OpportunityScore",
                table: "Keywords",
                columns: new[] { "SiteId", "OpportunityScore" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Keywords_SiteId_Cluster",
                table: "Keywords");

            migrationBuilder.DropIndex(
                name: "IX_Keywords_SiteId_OpportunityScore",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "Cluster",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "Intent",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "Mentions",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "OpportunityScore",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "TrendScore",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "KeywordResearchRuns");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "KeywordResearchRuns");
        }
    }
}
