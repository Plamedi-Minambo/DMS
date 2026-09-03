
using DocumentManagement.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace DocumentManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Finance")]
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ========================================
        // BUILD REPORT QUERY
        // ========================================

        private IQueryable<Models.Document> BuildReportQuery(
            DateTime? startDate,
            DateTime? endDate,
            string? vendor,
            string? status,
            decimal? minAmount,
            decimal? maxAmount)
        {
            var query = _context.Documents
                .Include(d => d.InvoiceData)
                .AsQueryable();

            // ========================================
            // DATE FILTER
            // ========================================

            if (startDate.HasValue)
            {
                query = query.Where(d =>
                    d.UploadedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                var endDateExclusive =
                    endDate.Value.Date.AddDays(1);

                query = query.Where(d =>
                    d.UploadedAt < endDateExclusive);
            }

            // ========================================
            // VENDOR FILTER
            // ========================================

            if (!string.IsNullOrWhiteSpace(vendor))
            {
                var vendorSearch =
                    vendor.Trim().ToLower();

                query = query.Where(d =>
                    d.InvoiceData != null &&
                    d.InvoiceData.Vendor != null &&
                    d.InvoiceData.Vendor
                        .ToLower()
                        .Contains(vendorSearch));
            }

            // ========================================
            // STATUS FILTER
            // ========================================

            if (!string.IsNullOrWhiteSpace(status))
            {
                var statusSearch =
                    status.Trim().ToLower();

                // Pending includes all workflow stages
                if (statusSearch == "pending")
                {
                    query = query.Where(d =>
                        d.Status.ToLower() == "pending" ||
                        d.Status.ToLower() == "pending manager" ||
                        d.Status.ToLower() == "pending finance");
                }
                else
                {
                    query = query.Where(d =>
                        d.Status.ToLower() == statusSearch);
                }
            }

            // ========================================
            // MINIMUM AMOUNT
            // ========================================

            if (minAmount.HasValue)
            {
                query = query.Where(d =>
                    d.InvoiceData != null &&
                    d.InvoiceData.TotalAmount.HasValue &&
                    d.InvoiceData.TotalAmount.Value >=
                        minAmount.Value);
            }

            // ========================================
            // MAXIMUM AMOUNT
            // ========================================

            if (maxAmount.HasValue)
            {
                query = query.Where(d =>
                    d.InvoiceData != null &&
                    d.InvoiceData.TotalAmount.HasValue &&
                    d.InvoiceData.TotalAmount.Value <=
                        maxAmount.Value);
            }

            return query;
        }

        // ========================================
        // GET DOCUMENT REPORT
        // ========================================

        [HttpGet]
        public async Task<IActionResult> GetReport(
            DateTime? startDate,
            DateTime? endDate,
            string? vendor,
            string? status,
            decimal? minAmount,
            decimal? maxAmount)
        {
            if (startDate.HasValue &&
                endDate.HasValue &&
                startDate.Value.Date > endDate.Value.Date)
            {
                return BadRequest(
                    "Start date cannot be later than end date.");
            }

            if (minAmount.HasValue &&
                maxAmount.HasValue &&
                minAmount.Value > maxAmount.Value)
            {
                return BadRequest(
                    "Minimum amount cannot be greater than maximum amount.");
            }

            var query = BuildReportQuery(
                startDate,
                endDate,
                vendor,
                status,
                minAmount,
                maxAmount);

            var documents = await query
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    d.Id,
                    d.FileName,
                    d.Status,
                    d.UploadedAt,

                    Vendor = d.InvoiceData != null
                        ? d.InvoiceData.Vendor
                        : null,

                    InvoiceNumber = d.InvoiceData != null
                        ? d.InvoiceData.InvoiceNumber
                        : null,

                    InvoiceDate = d.InvoiceData != null
                        ? d.InvoiceData.InvoiceDate
                        : null,

                    Amount = d.InvoiceData != null
                        ? d.InvoiceData.Amount
                        : null,

                    VAT = d.InvoiceData != null
                        ? d.InvoiceData.VAT
                        : null,

                    TotalAmount = d.InvoiceData != null
                        ? d.InvoiceData.TotalAmount
                        : null
                })
                .ToListAsync();

            // ========================================
            // REPORT TOTALS
            // ========================================

            var totalAmount =
                documents.Sum(d => d.Amount ?? 0);

            var totalVAT =
                documents.Sum(d => d.VAT ?? 0);

            var totalAmountIncludingVAT =
                documents.Sum(d => d.TotalAmount ?? 0);

            var approvedDocuments =
                documents.Count(d =>
                    d.Status == "Approved");

            var rejectedDocuments =
                documents.Count(d =>
                    d.Status == "Rejected");

            var pendingDocuments =
                documents.Count(d =>
                    d.Status == "Pending" ||
                    d.Status == "Pending Manager" ||
                    d.Status == "Pending Finance");

            return Ok(new
            {
                summary = new
                {
                    totalDocuments = documents.Count,
                    approvedDocuments,
                    rejectedDocuments,
                    pendingDocuments,
                    totalAmount,
                    totalVAT,
                    totalAmountIncludingVAT
                },

                filters = new
                {
                    startDate,
                    endDate,
                    vendor,
                    status,
                    minAmount,
                    maxAmount
                },

                documents
            });
        }

        // ========================================
        // EXPORT REPORT TO EXCEL
        // ========================================

        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel(
            DateTime? startDate,
            DateTime? endDate,
            string? vendor,
            string? status,
            decimal? minAmount,
            decimal? maxAmount,
            string? reportType)
        {
            if (startDate.HasValue &&
                endDate.HasValue &&
                startDate.Value.Date > endDate.Value.Date)
            {
                return BadRequest(
                    "Start date cannot be later than end date.");
            }

            if (minAmount.HasValue &&
                maxAmount.HasValue &&
                minAmount.Value > maxAmount.Value)
            {
                return BadRequest(
                    "Minimum amount cannot be greater than maximum amount.");
            }

            var query = BuildReportQuery(
                startDate,
                endDate,
                vendor,
                status,
                minAmount,
                maxAmount);

            var documents = await query
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            using var workbook = new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add("Document Report");

            var reportTitle =
                reportType switch
                {
                    "vendor" => "Vendor Analysis Report",
                    "tax" => "Tax / VAT Report",
                    _ => "Spend Summary Report"
                };

            // ========================================
            // REPORT HEADER
            // ========================================

            worksheet.Cell("A1").Value =
                "Document Management System";

            worksheet.Range("A1:J1").Merge();

            worksheet.Cell("A2").Value =
                reportTitle;

            worksheet.Range("A2:J2").Merge();

            worksheet.Cell("A3").Value =
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            worksheet.Range("A3:J3").Merge();

            // ========================================
            // TABLE HEADERS
            // ========================================

            worksheet.Cell("A5").Value = "Document ID";
            worksheet.Cell("B5").Value = "File Name";
            worksheet.Cell("C5").Value = "Status";
            worksheet.Cell("D5").Value = "Vendor";
            worksheet.Cell("E5").Value = "Invoice Number";
            worksheet.Cell("F5").Value = "Invoice Date";
            worksheet.Cell("G5").Value = "Amount";
            worksheet.Cell("H5").Value = "VAT";
            worksheet.Cell("I5").Value = "Total Amount";
            worksheet.Cell("J5").Value = "Uploaded At";

            // ========================================
            // DATA
            // ========================================

            int row = 6;

            foreach (var document in documents)
            {
                worksheet.Cell(row, 1).Value =
                    document.Id;

                worksheet.Cell(row, 2).Value =
                    document.FileName;

                worksheet.Cell(row, 3).Value =
                    document.Status;

                worksheet.Cell(row, 4).Value =
                    document.InvoiceData?.Vendor ?? "";

                worksheet.Cell(row, 5).Value =
                    document.InvoiceData?.InvoiceNumber ?? "";

                if (document.InvoiceData?.InvoiceDate.HasValue == true)
                {
                    worksheet.Cell(row, 6).Value =
                        document.InvoiceData.InvoiceDate.Value;

                    worksheet.Cell(row, 6)
                        .Style
                        .DateFormat
                        .Format = "yyyy-MM-dd";
                }

                worksheet.Cell(row, 7).Value =
                    document.InvoiceData?.Amount ?? 0;

                worksheet.Cell(row, 8).Value =
                    document.InvoiceData?.VAT ?? 0;

                worksheet.Cell(row, 9).Value =
                    document.InvoiceData?.TotalAmount ?? 0;

                worksheet.Cell(row, 10).Value =
                    document.UploadedAt;

                worksheet.Cell(row, 10)
                    .Style
                    .DateFormat
                    .Format = "yyyy-MM-dd HH:mm:ss";

                row++;
            }

            // ========================================
            // REPORT SUMMARY
            // ========================================

            int summaryRow = row + 2;

            var totalAmount =
                documents.Sum(d =>
                    d.InvoiceData?.Amount ?? 0);

            var totalVAT =
                documents.Sum(d =>
                    d.InvoiceData?.VAT ?? 0);

            var totalIncludingVAT =
                documents.Sum(d =>
                    d.InvoiceData?.TotalAmount ?? 0);

            var approvedDocuments =
                documents.Count(d =>
                    d.Status == "Approved");

            var rejectedDocuments =
                documents.Count(d =>
                    d.Status == "Rejected");

            var pendingDocuments =
                documents.Count(d =>
                    d.Status == "Pending" ||
                    d.Status == "Pending Manager" ||
                    d.Status == "Pending Finance");

            worksheet.Cell(summaryRow, 1).Value =
                "REPORT SUMMARY";

            worksheet.Cell(summaryRow + 1, 1).Value =
                "Total Documents";

            worksheet.Cell(summaryRow + 1, 2).Value =
                documents.Count;

            worksheet.Cell(summaryRow + 2, 1).Value =
                "Approved Documents";

            worksheet.Cell(summaryRow + 2, 2).Value =
                approvedDocuments;

            worksheet.Cell(summaryRow + 3, 1).Value =
                "Rejected Documents";

            worksheet.Cell(summaryRow + 3, 2).Value =
                rejectedDocuments;

            worksheet.Cell(summaryRow + 4, 1).Value =
                "Pending Documents";

            worksheet.Cell(summaryRow + 4, 2).Value =
                pendingDocuments;

            worksheet.Cell(summaryRow + 5, 1).Value =
                "Total Amount";

            worksheet.Cell(summaryRow + 5, 2).Value =
                totalAmount;

            worksheet.Cell(summaryRow + 6, 1).Value =
                "Total VAT";

            worksheet.Cell(summaryRow + 6, 2).Value =
                totalVAT;

            worksheet.Cell(summaryRow + 7, 1).Value =
                "Total Including VAT";

            worksheet.Cell(summaryRow + 7, 2).Value =
                totalIncludingVAT;

            // ========================================
            // FILTER INFORMATION
            // ========================================

            int filterRow = summaryRow + 10;

            worksheet.Cell(filterRow, 1).Value =
                "REPORT FILTERS";

            worksheet.Cell(filterRow + 1, 1).Value =
                "Start Date";

            worksheet.Cell(filterRow + 1, 2).Value =
                startDate?.ToString("yyyy-MM-dd") ?? "All";

            worksheet.Cell(filterRow + 2, 1).Value =
                "End Date";

            worksheet.Cell(filterRow + 2, 2).Value =
                endDate?.ToString("yyyy-MM-dd") ?? "All";

            worksheet.Cell(filterRow + 3, 1).Value =
                "Vendor";

            worksheet.Cell(filterRow + 3, 2).Value =
                string.IsNullOrWhiteSpace(vendor)
                    ? "All"
                    : vendor;

            worksheet.Cell(filterRow + 4, 1).Value =
                "Status";

            worksheet.Cell(filterRow + 4, 2).Value =
                string.IsNullOrWhiteSpace(status)
                    ? "All"
                    : status;

            worksheet.Cell(filterRow + 5, 1).Value =
                "Minimum Amount";

            worksheet.Cell(filterRow + 5, 2).Value =
                minAmount?.ToString("0.00") ?? "All";

            worksheet.Cell(filterRow + 6, 1).Value =
                "Maximum Amount";

            worksheet.Cell(filterRow + 6, 2).Value =
                maxAmount?.ToString("0.00") ?? "All";

            // ========================================
            // FORMATTING
            // ========================================

            worksheet.Range("A1:J1")
                .Style
                .Font
                .Bold = true;

            worksheet.Range("A1:J1")
                .Style
                .Font
                .FontSize = 18;

            worksheet.Range("A2:J2")
                .Style
                .Font
                .Bold = true;

            worksheet.Range("A5:J5")
                .Style
                .Font
                .Bold = true;

            if (row > 6)
            {
                worksheet.Range($"G6:I{row - 1}")
                    .Style
                    .NumberFormat
                    .Format = "#,##0.00";
            }

            worksheet.Cell(summaryRow + 5, 2)
                .Style
                .NumberFormat
                .Format = "#,##0.00";

            worksheet.Cell(summaryRow + 6, 2)
                .Style
                .NumberFormat
                .Format = "#,##0.00";

            worksheet.Cell(summaryRow + 7, 2)
                .Style
                .NumberFormat
                .Format = "#,##0.00";

            worksheet.Columns()
                .AdjustToContents();

            // ========================================
            // RETURN EXCEL
            // ========================================

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            var content =
                stream.ToArray();

            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Document_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        // ========================================
        // EXPORT REPORT TO PDF
        // ========================================

        [HttpGet("export/pdf")]
        public async Task<IActionResult> ExportPdf(
            DateTime? startDate,
            DateTime? endDate,
            string? vendor,
            string? status,
            decimal? minAmount,
            decimal? maxAmount,
            string? reportType)
        {
            if (startDate.HasValue &&
                endDate.HasValue &&
                startDate.Value.Date > endDate.Value.Date)
            {
                return BadRequest(
                    "Start date cannot be later than end date.");
            }

            if (minAmount.HasValue &&
                maxAmount.HasValue &&
                minAmount.Value > maxAmount.Value)
            {
                return BadRequest(
                    "Minimum amount cannot be greater than maximum amount.");
            }

            var query = BuildReportQuery(
                startDate,
                endDate,
                vendor,
                status,
                minAmount,
                maxAmount);

            var documents = await query
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var totalAmount =
                documents.Sum(d =>
                    d.InvoiceData?.Amount ?? 0);

            var totalVAT =
                documents.Sum(d =>
                    d.InvoiceData?.VAT ?? 0);

            var totalIncludingVAT =
                documents.Sum(d =>
                    d.InvoiceData?.TotalAmount ?? 0);

            var approvedDocuments =
                documents.Count(d =>
                    d.Status == "Approved");

            var rejectedDocuments =
                documents.Count(d =>
                    d.Status == "Rejected");

            var pendingDocuments =
                documents.Count(d =>
                    d.Status == "Pending" ||
                    d.Status == "Pending Manager" ||
                    d.Status == "Pending Finance");

            var reportTitle =
                reportType switch
                {
                    "vendor" => "Vendor Analysis Report",
                    "tax" => "Tax / VAT Report",
                    _ => "Spend Summary Report"
                };

            var pdf =
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(35);

                        page.DefaultTextStyle(
                            x => x.FontSize(9));

                        // ========================================
                        // HEADER
                        // ========================================

                        page.Header()
                            .Column(column =>
                            {
                                column.Spacing(4);

                                column.Item()
                                    .Text(
                                        "DOCUMENT MANAGEMENT SYSTEM")
                                    .Bold()
                                    .FontSize(18);

                                column.Item()
                                    .Text(reportTitle)
                                    .Bold()
                                    .FontSize(13);

                                column.Item()
                                    .Text(
                                        $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                                    .FontSize(8);
                            });

                        // ========================================
                        // CONTENT
                        // ========================================

                        page.Content()
                            .PaddingVertical(15)
                            .Column(column =>
                            {
                                column.Spacing(12);

                                // ========================================
                                // SUMMARY
                                // ========================================

                                column.Item()
                                    .Text("Report Summary")
                                    .Bold()
                                    .FontSize(12);

                                column.Item()
                                    .Table(table =>
                                    {
                                        table.ColumnsDefinition(
                                            columns =>
                                            {
                                                columns.RelativeColumn();
                                                columns.RelativeColumn();
                                                columns.RelativeColumn();
                                            });

                                        table.Cell()
                                            .Element(SummaryCell)
                                            .Text(
                                                $"Documents\n{documents.Count}");

                                        table.Cell()
                                            .Element(SummaryCell)
                                            .Text(
                                                $"Total Spend\nR {totalAmount:N2}");

                                        table.Cell()
                                            .Element(SummaryCell)
                                            .Text(
                                                $"VAT\nR {totalVAT:N2}");

                                        table.Cell()
                                            .Element(SummaryCell)
                                            .Text(
                                                $"Including VAT\nR {totalIncludingVAT:N2}");

                                        table.Cell()
                                            .Element(SummaryCell)
                                            .Text(
                                                $"Approved\n{approvedDocuments}");

                                        table.Cell()
                                            .Element(SummaryCell)
                                            .Text(
                                                $"Pending\n{pendingDocuments}");

                                        static IContainer SummaryCell(
                                            IContainer container)
                                        {
                                            return container
                                                .Border(1)
                                                .BorderColor(
                                                    Colors.Grey.Lighten2)
                                                .Padding(8);
                                        }
                                    });

                                // ========================================
                                // FILTERS
                                // ========================================

                                column.Item()
                                    .Text("Applied Filters")
                                    .Bold()
                                    .FontSize(12);

                                column.Item()
                                    .Text(
                                        $"Date Range: " +
                                        $"{startDate?.ToString("yyyy-MM-dd") ?? "All"} " +
                                        $"to " +
                                        $"{endDate?.ToString("yyyy-MM-dd") ?? "All"}");

                                column.Item()
                                    .Text(
                                        $"Vendor: " +
                                        $"{(string.IsNullOrWhiteSpace(vendor) ? "All" : vendor)}");

                                column.Item()
                                    .Text(
                                        $"Status: " +
                                        $"{(string.IsNullOrWhiteSpace(status) ? "All" : status)}");

                                column.Item()
                                    .Text(
                                        $"Amount Range: " +
                                        $"{(minAmount.HasValue ? $"R {minAmount.Value:N2}" : "All")} " +
                                        $"to " +
                                        $"{(maxAmount.HasValue ? $"R {maxAmount.Value:N2}" : "All")}");

                                // ========================================
                                // REPORT TABLE
                                // ========================================

                                column.Item()
                                    .Text(reportTitle)
                                    .Bold()
                                    .FontSize(12);

                                if (documents.Count == 0)
                                {
                                    column.Item()
                                        .Text(
                                            "No documents matched the selected filters.");
                                }
                                else
                                {
                                    column.Item()
                                        .Table(table =>
                                        {
                                            table.ColumnsDefinition(
                                                columns =>
                                                {
                                                    columns.ConstantColumn(55);
                                                    columns.RelativeColumn(2);
                                                    columns.RelativeColumn(2);
                                                    columns.ConstantColumn(65);
                                                    columns.ConstantColumn(60);
                                                    columns.ConstantColumn(70);
                                                });

                                            table.Header(header =>
                                            {
                                                header.Cell()
                                                    .Element(HeaderCell)
                                                    .Text("Invoice");

                                                header.Cell()
                                                    .Element(HeaderCell)
                                                    .Text("Vendor");

                                                header.Cell()
                                                    .Element(HeaderCell)
                                                    .Text("Date");

                                                header.Cell()
                                                    .Element(HeaderCell)
                                                    .AlignRight()
                                                    .Text("Amount");

                                                header.Cell()
                                                    .Element(HeaderCell)
                                                    .AlignRight()
                                                    .Text("VAT");

                                                header.Cell()
                                                    .Element(HeaderCell)
                                                    .AlignRight()
                                                    .Text("Total");
                                            });

                                            foreach (var document in documents)
                                            {
                                                var invoiceNumber =
                                                    document.InvoiceData
                                                        ?.InvoiceNumber
                                                    ?? "-";

                                                var vendorName =
                                                    document.InvoiceData
                                                        ?.Vendor
                                                    ?? "-";

                                                var invoiceDate =
                                                    document.InvoiceData
                                                        ?.InvoiceDate
                                                        ?.ToString(
                                                            "yyyy-MM-dd")
                                                    ?? "-";

                                                var amount =
                                                    document.InvoiceData
                                                        ?.Amount
                                                    ?? 0;

                                                var vat =
                                                    document.InvoiceData
                                                        ?.VAT
                                                    ?? 0;

                                                var total =
                                                    document.InvoiceData
                                                        ?.TotalAmount
                                                    ?? 0;

                                                table.Cell()
                                                    .Element(DataCell)
                                                    .Text(invoiceNumber);

                                                table.Cell()
                                                    .Element(DataCell)
                                                    .Text(vendorName);

                                                table.Cell()
                                                    .Element(DataCell)
                                                    .Text(invoiceDate);

                                                table.Cell()
                                                    .Element(DataCell)
                                                    .AlignRight()
                                                    .Text(
                                                        $"R {amount:N2}");

                                                table.Cell()
                                                    .Element(DataCell)
                                                    .AlignRight()
                                                    .Text(
                                                        $"R {vat:N2}");

                                                table.Cell()
                                                    .Element(DataCell)
                                                    .AlignRight()
                                                    .Text(
                                                        $"R {total:N2}");
                                            }

                                            static IContainer HeaderCell(
                                                IContainer container)
                                            {
                                                return container
                                                    .Background(
                                                        Colors.Grey.Lighten3)
                                                    .Border(1)
                                                    .BorderColor(
                                                        Colors.Grey.Lighten1)
                                                    .Padding(5)
                                                    .DefaultTextStyle(
                                                        x => x.Bold());
                                            }

                                            static IContainer DataCell(
                                                IContainer container)
                                            {
                                                return container
                                                    .BorderBottom(1)
                                                    .BorderColor(
                                                        Colors.Grey.Lighten2)
                                                    .Padding(5);
                                            }
                                        });
                                }

                                // ========================================
                                // TOTAL
                                // ========================================

                                column.Item()
                                    .PaddingTop(5)
                                    .AlignRight()
                                    .Text(
                                        $"Total Including VAT: R {totalIncludingVAT:N2}")
                                    .Bold()
                                    .FontSize(11);
                            });

                        // ========================================
                        // FOOTER
                        // ========================================

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span(
                                    "Document Management System • Page ");

                                text.CurrentPageNumber();
                            });
                    });
                });

            var content =
                pdf.GeneratePdf();

            return File(
                content,
                "application/pdf",
                $"Document_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }
    }
}
