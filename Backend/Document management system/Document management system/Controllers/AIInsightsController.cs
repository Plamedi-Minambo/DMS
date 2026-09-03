using DocumentManagement.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Finance")]
    public class AIInsightsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AIInsightsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // ========================================
        // GET AI INSIGHTS
        // ========================================

        [HttpGet]
        public async Task<IActionResult> GetInsights()
        {
            var documents = await _context.Documents
                .Include(d => d.InvoiceData)
                .Where(d =>
                    d.InvoiceData != null &&
                    d.InvoiceData.TotalAmount.HasValue)
                .OrderBy(d => d.UploadedAt)
                .ToListAsync();

            if (documents.Count == 0)
            {
                return Ok(new
                {
                    hasData = false,
                    message =
                        "There is not enough invoice data to generate AI insights.",
                    summary = new
                    {
                        totalDocuments = 0,
                        totalSpend = 0,
                        averageInvoice = 0,
                        totalVAT = 0
                    },
                    topVendor = (object?)null,
                    spendingTrend = Array.Empty<object>(),
                    anomalies = Array.Empty<object>(),
                    insights = Array.Empty<string>()
                });
            }

            // ========================================
            // BASIC FINANCIAL ANALYSIS
            // ========================================

            var totalSpend = documents.Sum(d =>
                d.InvoiceData?.Amount ?? 0);

            var totalVAT = documents.Sum(d =>
                d.InvoiceData?.VAT ?? 0);

            var totalIncludingVAT = documents.Sum(d =>
                d.InvoiceData?.TotalAmount ?? 0);

            var averageInvoice =
                documents.Count > 0
                    ? totalIncludingVAT / documents.Count
                    : 0;

            var vatPercentage =
                totalSpend > 0
                    ? (totalVAT / totalSpend) * 100
                    : 0;

            // ========================================
            // VENDOR ANALYSIS
            // ========================================

            var vendorGroups = documents
                .GroupBy(d =>
                    string.IsNullOrWhiteSpace(
                        d.InvoiceData?.Vendor)
                        ? "Unknown Vendor"
                        : d.InvoiceData!.Vendor!.Trim())
                .Select(group => new
                {
                    vendor = group.Key,
                    invoiceCount = group.Count(),
                    totalSpend = group.Sum(d =>
                        d.InvoiceData?.TotalAmount ?? 0),
                    averageSpend =
                        group.Count() > 0
                            ? group.Sum(d =>
                                d.InvoiceData?.TotalAmount ?? 0)
                                / group.Count()
                            : 0
                })
                .OrderByDescending(x => x.totalSpend)
                .ToList();

            var topVendor =
                vendorGroups.FirstOrDefault();

            // ========================================
            // MONTHLY SPENDING TREND
            // ========================================

            var spendingTrend = documents
                .GroupBy(d =>
                    new
                    {
                        Year = d.UploadedAt.Year,
                        Month = d.UploadedAt.Month
                    })
                .Select(group => new
                {
                    period =
                        $"{group.Key.Year:D4}-{group.Key.Month:D2}",

                    invoiceCount =
                        group.Count(),

                    totalSpend =
                        group.Sum(d =>
                            d.InvoiceData?.TotalAmount ?? 0)
                })
                .OrderBy(x => x.period)
                .ToList();

            // ========================================
            // ANOMALY DETECTION
            // ========================================
            //
            // An invoice is considered unusually high
            // when its total is above:
            //
            // Average + 2 Standard Deviations
            //
            // This gives us a simple statistical anomaly
            // detection mechanism without requiring an
            // external AI service.
            // ========================================

            var amounts = documents
                .Select(d =>
                    d.InvoiceData?.TotalAmount ?? 0)
                .ToList();

            var mean = amounts.Count > 0
                ? amounts.Average()
                : 0;

            var variance = amounts.Count > 0
                ? amounts.Sum(amount =>
                    Math.Pow(
                        (double)(amount - mean),
                        2)) / amounts.Count
                : 0;

            var standardDeviation =
                (decimal)Math.Sqrt(variance);

            var anomalyThreshold =
                mean + (2 * standardDeviation);

            var anomalies = documents
                .Where(d =>
                    (d.InvoiceData?.TotalAmount ?? 0)
                    > anomalyThreshold)
                .Select(d => new
                {
                    documentId = d.Id,
                    fileName = d.FileName,

                    invoiceNumber =
                        d.InvoiceData?.InvoiceNumber,

                    vendor =
                        d.InvoiceData?.Vendor,

                    amount =
                        d.InvoiceData?.TotalAmount ?? 0,

                    invoiceDate =
                        d.InvoiceData?.InvoiceDate,

                    reason =
                        "Invoice value is significantly higher than the normal invoice range."
                })
                .OrderByDescending(x => x.amount)
                .ToList();

            // ========================================
            // AUTOMATED INSIGHTS
            // ========================================

            var insights =
                new List<string>();

            // Top vendor insight
            if (topVendor != null)
            {
                insights.Add(
                    $"{topVendor.vendor} is currently the highest-spending vendor with total invoice value of R {topVendor.totalSpend:N2}.");
            }

            // Average invoice insight
            insights.Add(
                $"The average invoice value is R {averageInvoice:N2}.");

            // VAT insight
            if (vatPercentage > 0)
            {
                insights.Add(
                    $"VAT represents approximately {vatPercentage:N2}% of the pre-VAT spending.");
            }

            // Anomaly insight
            if (anomalies.Count > 0)
            {
                insights.Add(
                    $"{anomalies.Count} unusually high invoice(s) were detected and should be reviewed.");
            }
            else
            {
                insights.Add(
                    "No unusually high invoices were detected using the current anomaly threshold.");
            }

            // Approval insight
            var pendingCount = documents.Count(d =>
                d.Status == "Pending" ||
                d.Status == "Pending Manager" ||
                d.Status == "Pending Finance");

            var approvedCount = documents.Count(d =>
                d.Status == "Approved");

            var rejectedCount = documents.Count(d =>
                d.Status == "Rejected");

            if (pendingCount > 0)
            {
                insights.Add(
                    $"{pendingCount} invoice(s) are currently waiting for approval.");
            }

            if (rejectedCount > 0)
            {
                insights.Add(
                    $"{rejectedCount} invoice(s) have been rejected and may require follow-up.");
            }

            // ========================================
            // RETURN INSIGHTS
            // ========================================

            return Ok(new
            {
                hasData = true,

                summary = new
                {
                    totalDocuments =
                        documents.Count,

                    totalSpend,

                    totalVAT,

                    totalIncludingVAT,

                    averageInvoice,

                    vatPercentage,

                    pendingDocuments =
                        pendingCount,

                    approvedDocuments =
                        approvedCount,

                    rejectedDocuments =
                        rejectedCount
                },

                topVendor,

                spendingTrend,

                anomalyDetection = new
                {
                    averageInvoiceValue =
                        mean,

                    standardDeviation,

                    anomalyThreshold,

                    anomalyCount =
                        anomalies.Count,

                    anomalies
                },

                vendorAnalysis =
                    vendorGroups.Take(10),

                insights
            });
        }
    }
}