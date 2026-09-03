using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document_management_system.Migrations
{
    /// <inheritdoc />
    public partial class UpdateApplicationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InvoiceDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "VATAmount",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "VendorName",
                table: "Documents",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "StoredFileName",
                table: "Documents",
                newName: "FilePath");

            migrationBuilder.AlterColumn<string>(
                name: "UploadedById",
                table: "Documents",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedById",
                table: "Documents",
                column: "UploadedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_AspNetUsers_UploadedById",
                table: "Documents",
                column: "UploadedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_AspNetUsers_UploadedById",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UploadedById",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileType",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "Documents",
                newName: "StoredFileName");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Documents",
                newName: "VendorName");

            migrationBuilder.AlterColumn<string>(
                name: "UploadedById",
                table: "Documents",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Documents",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "Documents",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceDate",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VATAmount",
                table: "Documents",
                type: "TEXT",
                nullable: true);
        }
    }
}
