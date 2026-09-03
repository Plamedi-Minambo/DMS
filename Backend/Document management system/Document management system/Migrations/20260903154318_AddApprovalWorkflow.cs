using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document_management_system.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceData_DocumentId",
                table: "InvoiceData");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceData_DocumentId",
                table: "InvoiceData",
                column: "DocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceData_DocumentId",
                table: "InvoiceData");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceData_DocumentId",
                table: "InvoiceData",
                column: "DocumentId");
        }
    }
}
