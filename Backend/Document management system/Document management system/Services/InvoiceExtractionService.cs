
using System.Globalization;
using System.Text.RegularExpressions;
using DocumentManagement.API.Models;

namespace DocumentManagement.API.Services
{
    public class InvoiceExtractionService
    {
        public InvoiceData ExtractInvoiceData(int documentId, string extractedText)
        {
            var result = new InvoiceData
            {
                DocumentId = documentId,
                ExtractedAt = DateTime.UtcNow,
                ExtractionStatus = "Pending"
            };

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                result.ExtractionStatus = "Failed";
                return result;
            }

            // ---------------------------------------------------------
            // NORMALISE OCR / PDF TEXT
            // ---------------------------------------------------------
            var text = NormalizeText(extractedText);

            // ---------------------------------------------------------
            // DOCUMENT TYPE
            // ---------------------------------------------------------
            result.DocumentType = DetermineDocumentType(text);

            // ---------------------------------------------------------
            // INVOICE NUMBER
            // ---------------------------------------------------------
            result.InvoiceNumber = ExtractInvoiceNumber(text);

            // ---------------------------------------------------------
            // VENDOR
            // ---------------------------------------------------------
            result.Vendor = ExtractVendor(text);

            // ---------------------------------------------------------
            // DATE
            // ---------------------------------------------------------
            result.InvoiceDate = ExtractInvoiceDate(text);

            // ---------------------------------------------------------
            // FINANCIAL VALUES
            // ---------------------------------------------------------
            var subtotal = ExtractSubtotal(text);
            var discount = ExtractDiscount(text);

            // Amount = subtotal minus discount
            if (subtotal.HasValue)
            {
                result.Amount = subtotal.Value - (discount ?? 0m);
            }
            else
            {
                result.Amount = ExtractAmount(text);
            }

            result.VAT = ExtractVAT(text);

            result.TotalAmount = ExtractTotalAmount(text);

            // ---------------------------------------------------------
            // FINANCIAL FALLBACKS
            // ---------------------------------------------------------

            // If total is missing but amount + VAT exist
            if (!result.TotalAmount.HasValue &&
                result.Amount.HasValue &&
                result.VAT.HasValue)
            {
                result.TotalAmount =
                    result.Amount.Value + result.VAT.Value;
            }

            // If amount is missing but total + VAT exist
            if (!result.Amount.HasValue &&
                result.TotalAmount.HasValue &&
                result.VAT.HasValue)
            {
                result.Amount =
                    result.TotalAmount.Value - result.VAT.Value;
            }

            // ---------------------------------------------------------
            // EXTRACTION STATUS
            // ---------------------------------------------------------
            if (result.DocumentType == "Unknown")
            {
                result.ExtractionStatus = "Failed";
            }
            else if (
                !string.IsNullOrWhiteSpace(result.InvoiceNumber) ||
                !string.IsNullOrWhiteSpace(result.Vendor) ||
                result.InvoiceDate.HasValue ||
                result.Amount.HasValue ||
                result.VAT.HasValue ||
                result.TotalAmount.HasValue)
            {
                result.ExtractionStatus = "Completed";
            }
            else
            {
                result.ExtractionStatus = "Failed";
            }

            return result;
        }

        // =============================================================
        // TEXT NORMALISATION
        // =============================================================

        private string NormalizeText(string text)
        {
            text = text.Replace("\r\n", "\n")
                       .Replace("\r", "\n");

            text = Regex.Replace(text, @"[ \t]+", " ");

            // Common OCR mistakes
            text = Regex.Replace(
                text,
                @"\bT0TAL\b",
                "TOTAL",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"\bSUBT0TAL\b",
                "SUBTOTAL",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"\bAM0UNT\b",
                "AMOUNT",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"\bV4T\b|\bVAI\b",
                "VAT",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"INVOlCE|lnvoice",
                "INVOICE",
                RegexOptions.IgnoreCase);

            return text.Trim();
        }

        // =============================================================
        // DOCUMENT TYPE
        // =============================================================

        private string DetermineDocumentType(string text)
        {
            bool isCreditNote =
                Regex.IsMatch(
                    text,
                    @"\bCREDIT\s+NOTE\b|\bCREDIT\s+MEMO\b",
                    RegexOptions.IgnoreCase);

            if (isCreditNote)
                return "Credit Note";

            bool hasInvoiceHeading =
                Regex.IsMatch(
                    text,
                    @"\bTAX\s+INVOICE\b|\bINVOICE\b",
                    RegexOptions.IgnoreCase);

            bool hasInvoiceNumber =
                Regex.IsMatch(
                    text,
                    @"\bINVOICE\s*(?:NUMBER|NO|#)\s*[:\-]?\s*[A-Z0-9\-]+",
                    RegexOptions.IgnoreCase);

            bool hasInvoiceDate =
                Regex.IsMatch(
                    text,
                    @"\bINVOICE\s+DATE\b",
                    RegexOptions.IgnoreCase);

            bool hasTotal =
                Regex.IsMatch(
                    text,
                    @"\b(?:TOTAL|TOTAL\s+DUE|AMOUNT\s+DUE|BALANCE\s+DUE)\b",
                    RegexOptions.IgnoreCase);

            // Strong classification:
            // Heading + at least one invoice-specific field
            if (hasInvoiceHeading &&
                (hasInvoiceNumber || hasInvoiceDate || hasTotal))
            {
                return "Invoice";
            }

            // If only the heading exists, still allow it.
            // This helps with simple invoice templates.
            if (hasInvoiceHeading)
                return "Invoice";

            // IMPORTANT:
            // Do NOT default to Invoice.
            return "Unknown";
        }

        // =============================================================
        // INVOICE NUMBER
        // =============================================================

        private string? ExtractInvoiceNumber(string text)
        {
            var patterns = new[]
            {
                @"\bInvoice\s*(?:Number|No|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)",
                @"\bInv\s*(?:Number|No|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (match.Success)
                    return match.Groups[1].Value.Trim();
            }

            return null;
        }

        // =============================================================
        // VENDOR
        // =============================================================

        private string? ExtractVendor(string text)
        {
            var lines = text
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (lines.Count == 0)
                return null;

            // Find the INVOICE heading.
            int invoiceIndex = lines.FindIndex(
                x => Regex.IsMatch(
                    x,
                    @"^\s*(?:TAX\s+)?INVOICE\s*$",
                    RegexOptions.IgnoreCase));

            // Usually the vendor is near the top, before INVOICE.
            int searchLimit =
                invoiceIndex >= 0
                    ? invoiceIndex
                    : Math.Min(lines.Count, 15);

            for (int i = 0; i < searchLimit; i++)
            {
                string line = lines[i];

                if (IsVendorCandidate(line))
                    return line;
            }

            // Fallback: first meaningful line.
            foreach (var line in lines.Take(10))
            {
                if (IsVendorCandidate(line))
                    return line;
            }

            return null;
        }

        private bool IsVendorCandidate(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string upper = line.ToUpperInvariant();

            // Ignore headings
            string[] ignoredHeadings =
            {
                "INVOICE",
                "TAX INVOICE",
                "BILL TO",
                "SHIP TO",
                "PAYMENT TERMS",
                "PAYMENT METHOD",
                "NOTES"
            };

            if (ignoredHeadings.Contains(upper))
                return false;

            // Ignore VAT registration
            if (Regex.IsMatch(
                line,
                @"\bVAT\s*(?:NO|NUMBER|REGISTRATION)?\b",
                RegexOptions.IgnoreCase))
            {
                return false;
            }

            // Ignore phone numbers
            if (Regex.IsMatch(
                line,
                @"(?:\+?\d[\d\s\-]{7,})"))
            {
                return false;
            }

            // Ignore email
            if (line.Contains("@"))
                return false;

            // Ignore obvious addresses
            if (Regex.IsMatch(
                upper,
                @"\b(ROAD|RD|STREET|ST|AVENUE|AVE|PARK|DURBAN|CAPE TOWN|JOHANNESBURG)\b"))
            {
                return false;
            }

            // Ignore lines consisting mainly of numbers
            if (Regex.IsMatch(line, @"^\d[\d\s,\.\-]*$"))
                return false;

            // Must contain at least one letter
            return Regex.IsMatch(line, @"[A-Za-z]");
        }

        // =============================================================
        // INVOICE DATE
        // =============================================================

        private DateTime? ExtractInvoiceDate(string text)
        {
            var patterns = new[]
            {
                @"\bInvoice\s+Date\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
                @"\bDate\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
                @"\bInvoice\s+Date\s*[:\-]?\s*(\d{1,2}\s+[A-Za-z]+\s+\d{4})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                string value = match.Groups[1].Value.Trim();

                string[] formats =
                {
                    "dd/MM/yyyy",
                    "d/M/yyyy",
                    "dd-MM-yyyy",
                    "d-M-yyyy",
                    "dd/MM/yy",
                    "d/M/yy",
                    "dd-MM-yy",
                    "d-M-yy",
                    "d MMMM yyyy",
                    "dd MMMM yyyy"
                };

                if (DateTime.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsed))
                {
                    return parsed;
                }

                if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        // =============================================================
        // SUBTOTAL
        // =============================================================

        private decimal? ExtractSubtotal(string text)
        {
            var match = Regex.Match(
                text,
                @"\bSUBTOTAL\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            return ParseMoney(match.Groups[1].Value);
        }

        // =============================================================
        // DISCOUNT
        // =============================================================

        private decimal? ExtractDiscount(string text)
        {
            var match = Regex.Match(
                text,
                @"\bDISCOUNT\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            return ParseMoney(match.Groups[1].Value);
        }

        // =============================================================
        // AMOUNT
        // =============================================================

        private decimal? ExtractAmount(string text)
        {
            var patterns = new[]
            {
                @"\bNET\s+AMOUNT\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bAMOUNT\s+BEFORE\s+VAT\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bTOTAL\s+BEFORE\s+VAT\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bTOTAL\s+BEFORE\s+TAX\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bAMOUNT\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var amount = ParseMoney(match.Groups[1].Value);

                    if (amount.HasValue)
                        return amount;
                }
            }

            return null;
        }

        // =============================================================
        // VAT
        // =============================================================

        private decimal? ExtractVAT(string text)
        {
            var patterns = new[]
            {
                @"\bTax\s*\(VAT\s*\d+(?:\.\d+)?%\)\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bVAT\s*\d+(?:\.\d+)?%\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bVAT\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bTAX\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                var value = ParseMoney(match.Groups[1].Value);

                if (value.HasValue)
                    return value;
            }

            return null;
        }

        // =============================================================
        // TOTAL
        // =============================================================

        private decimal? ExtractTotalAmount(string text)
        {
            var patterns = new[]
            {
                @"\bGRAND\s+TOTAL\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bTOTAL\s+DUE\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bTOTAL\s+AMOUNT\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bAMOUNT\s+DUE\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bBALANCE\s+DUE\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",
                @"\bTOTAL\b\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                var value = ParseMoney(match.Groups[1].Value);

                if (value.HasValue)
                    return value;
            }

            return null;
        }

        // =============================================================
        // MONEY PARSER
        // =============================================================

        private decimal? ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Trim();

            // Remove currency symbols
            value = Regex.Replace(
                value,
                @"R|ZAR|\$|€|£",
                "",
                RegexOptions.IgnoreCase);

            value = value.Replace(" ", "");

            // South African format:
            // 30,850.00
            // 4,477.50
            // 34,327.50
            if (value.Contains(",") && value.Contains("."))
            {
                int commaIndex = value.LastIndexOf(',');
                int dotIndex = value.LastIndexOf('.');

                if (dotIndex > commaIndex)
                {
                    // Comma is thousands separator
                    value = value.Replace(",", "");
                }
                else
                {
                    // Dot is thousands separator
                    value = value.Replace(".", "");
                    value = value.Replace(",", ".");
                }
            }
            else if (value.Contains(","))
            {
                // If exactly two digits follow comma,
                // treat comma as decimal separator.
                int commaIndex = value.LastIndexOf(',');

                int digitsAfter =
                    value.Length - commaIndex - 1;

                if (digitsAfter == 2)
                {
                    value = value.Replace(".", "");
                    value = value.Replace(",", ".");
                }
                else
                {
                    value = value.Replace(",", "");
                }
            }

            if (decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out decimal result))
            {
                return result;
            }

            return null;
        }
    }
}
```

### 2. Replace `DocumentsController.cs`

The controller below keeps your existing approval workflow:

**Reviewer → Manager → Finance**

It also improves duplicate detection. In particular, I would **not** reject two invoices merely because the vendor and amount happen to be the same.

```csharp
using System.Security.Claims;
using System.Security.Cryptography;
using DocumentManagement.API.Data;
using DocumentManagement.API.Models;
using DocumentManagement.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly DocumentContentExtractionService _contentExtractionService;
        private readonly InvoiceExtractionService _invoiceExtractionService;

        private static readonly string[] AllowedExtensions =
        {
            ".pdf",
            ".docx",
            ".jpg",
            ".jpeg",
            ".png"
        };

        public DocumentsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            DocumentContentExtractionService contentExtractionService,
            InvoiceExtractionService invoiceExtractionService)
        {
            _context = context;
            _environment = environment;
            _contentExtractionService = contentExtractionService;
            _invoiceExtractionService = invoiceExtractionService;
        }

        // =============================================================
        // UPLOAD
        // =============================================================

        [HttpPost("upload")]
        [Authorize(Roles = "Admin,Reviewer,Manager,Finance")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Please select a file to upload."
                });
            }

            string originalFileName =
                Path.GetFileName(file.FileName);

            string extension =
                Path.GetExtension(originalFileName)
                    .ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    message =
                        "Unsupported file type. Allowed files are PDF, DOCX, JPG, JPEG and PNG."
                });
            }

            // ---------------------------------------------------------
            // CREATE UPLOAD DIRECTORY
            // ---------------------------------------------------------

            string uploadsFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads");

            Directory.CreateDirectory(uploadsFolder);

            // ---------------------------------------------------------
            // CREATE SAFE STORED FILE NAME
            // ---------------------------------------------------------

            string storedFileName =
                $"{Guid.NewGuid():N}{extension}";

            string filePath =
                Path.Combine(
                    uploadsFolder,
                    storedFileName);

            try
            {
                // -----------------------------------------------------
                // SAVE FILE
                // -----------------------------------------------------

                await using (var stream =
                    new FileStream(
                        filePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                // -----------------------------------------------------
                // HASH FILE
                // -----------------------------------------------------

                string fileHash =
                    await CalculateSha256Async(filePath);

                // -----------------------------------------------------
                // EXACT FILE DUPLICATE CHECK
                // -----------------------------------------------------

                bool duplicateFile =
                    await _context.Documents
                        .AnyAsync(d =>
                            d.FileHash == fileHash);

                if (duplicateFile)
                {
                    DeleteFileIfExists(filePath);

                    return Conflict(new
                    {
                        message =
                            "This exact file has already been uploaded."
                    });
                }

                // -----------------------------------------------------
                // EXTRACT TEXT
                // -----------------------------------------------------

                string extractedText =
                    await _contentExtractionService
                        .ExtractTextAsync(
                            filePath,
                            extension);

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    DeleteFileIfExists(filePath);

                    return BadRequest(new
                    {
                        message =
                            "No readable text could be extracted from this document. If this is a scanned PDF, OCR support is required."
                    });
                }

                // -----------------------------------------------------
                // EXTRACT INVOICE DATA
                // -----------------------------------------------------

                var extractedInvoiceData =
                    _invoiceExtractionService
                        .ExtractInvoiceData(
                            0,
                            extractedText);

                // -----------------------------------------------------
                // VALIDATE DOCUMENT TYPE
                // -----------------------------------------------------

                if (extractedInvoiceData.DocumentType != "Invoice" &&
                    extractedInvoiceData.DocumentType != "Credit Note")
                {
                    DeleteFileIfExists(filePath);

                    return BadRequest(new
                    {
                        message =
                            "The uploaded document does not appear to be a valid invoice or credit note.",
                        detectedType =
                            extractedInvoiceData.DocumentType
                    });
                }

                // -----------------------------------------------------
                // DUPLICATE INVOICE NUMBER
                // -----------------------------------------------------

                if (!string.IsNullOrWhiteSpace(
                        extractedInvoiceData.InvoiceNumber))
                {
                    string invoiceNumber =
                        extractedInvoiceData.InvoiceNumber
                            .Trim()
                            .ToLowerInvariant();

                    bool duplicateInvoice =
                        await _context.InvoiceData
                            .AnyAsync(i =>
                                i.InvoiceNumber != null &&
                                i.InvoiceNumber
                                    .ToLower() ==
                                invoiceNumber);

                    if (duplicateInvoice)
                    {
                        DeleteFileIfExists(filePath);

                        return Conflict(new
                        {
                            message =
                                "An invoice with this invoice number already exists.",
                            invoiceNumber =
                                extractedInvoiceData.InvoiceNumber
                        });
                    }
                }

                // -----------------------------------------------------
                // CURRENT USER
                // -----------------------------------------------------

                string? userId =
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    DeleteFileIfExists(filePath);

                    return Unauthorized(new
                    {
                        message =
                            "The current user could not be identified."
                    });
                }

                // -----------------------------------------------------
                // CREATE DOCUMENT
                // -----------------------------------------------------

                var document = new Document
                {
                    FileName = originalFileName,
                    FileType = file.ContentType,
                    FileSize = file.Length,
                    FilePath = filePath,
                    StoredFileName = storedFileName,
                    FileHash = fileHash,
                    UploadedAt = DateTime.UtcNow,
                    Status = "Pending",
                    Description =
                        $"{extractedInvoiceData.DocumentType} - {extractedInvoiceData.InvoiceNumber}",
                    UploadedById = userId
                };

                // -----------------------------------------------------
                // CREATE INVOICE DATA
                // -----------------------------------------------------

                var invoiceData = new InvoiceData
                {
                    Document = document,
                    DocumentType =
                        extractedInvoiceData.DocumentType,
                    InvoiceNumber =
                        extractedInvoiceData.InvoiceNumber,
                    Vendor =
                        extractedInvoiceData.Vendor,
                    InvoiceDate =
                        extractedInvoiceData.InvoiceDate,
                    Amount =
                        extractedInvoiceData.Amount,
                    VAT =
                        extractedInvoiceData.VAT,
                    TotalAmount =
                        extractedInvoiceData.TotalAmount,
                    ExtractedAt = DateTime.UtcNow,
                    ExtractionStatus =
                        extractedInvoiceData.ExtractionStatus
                };

                // -----------------------------------------------------
                // APPROVAL WORKFLOW
                // -----------------------------------------------------

                var reviewerApproval = new Approval
                {
                    Document = document,
                    Stage = "Reviewer",
                    Status = "Pending"
                };

                var managerApproval = new Approval
                {
                    Document = document,
                    Stage = "Manager",
                    Status = "Pending"
                };

                var financeApproval = new Approval
                {
                    Document = document,
                    Stage = "Finance",
                    Status = "Pending"
                };

                // -----------------------------------------------------
                // SAVE EVERYTHING
                // -----------------------------------------------------

                _context.Documents.Add(document);
                _context.InvoiceData.Add(invoiceData);

                _context.Approvals.Add(reviewerApproval);
                _context.Approvals.Add(managerApproval);
                _context.Approvals.Add(financeApproval);

                await _context.SaveChangesAsync();

                // -----------------------------------------------------
                // RESPONSE
                // -----------------------------------------------------

                return Ok(new
                {
                    message =
                        "Document uploaded and invoice data extracted successfully.",

                    document = new
                    {
                        document.Id,
                        document.FileName,
                        document.FileType,
                        document.FileSize,
                        document.Status,
                        document.UploadedAt
                    },

                    invoiceData = new
                    {
                        invoiceData.Id,
                        invoiceData.DocumentType,
                        invoiceData.InvoiceNumber,
                        invoiceData.Vendor,
                        invoiceData.InvoiceDate,
                        invoiceData.Amount,
                        invoiceData.VAT,
                        invoiceData.TotalAmount,
                        invoiceData.ExtractionStatus
                    }
                });
            }
            catch (Exception ex)
            {
                DeleteFileIfExists(filePath);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "An error occurred while uploading and processing the document.",
                        error = ex.Message
                    });
            }
        }

        // =============================================================
        // GET ALL DOCUMENTS
        // =============================================================

        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            var documents =
                await _context.Documents
                    .Include(d => d.UploadedBy)
                    .Include(d => d.InvoiceData)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();

            return Ok(documents);
        }

        // =============================================================
        // VIEW DOCUMENT
        // =============================================================

        [HttpGet("{id}/view")]
        public async Task<IActionResult> ViewDocument(int id)
        {
            var document =
                await _context.Documents
                    .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            if (!System.IO.File.Exists(document.FilePath))
            {
                return NotFound(new
                {
                    message = "The document file could not be found on the server."
                });
            }

            byte[] fileBytes =
                await System.IO.File.ReadAllBytesAsync(
                    document.FilePath);

            return File(
                fileBytes,
                document.FileType ?? "application/octet-stream");
        }

        // =============================================================
        // DOWNLOAD DOCUMENT
        // =============================================================

        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            var document =
                await _context.Documents
                    .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            if (!System.IO.File.Exists(document.FilePath))
            {
                return NotFound(new
                {
                    message = "The document file could not be found on the server."
                });
            }

            byte[] fileBytes =
                await System.IO.File.ReadAllBytesAsync(
                    document.FilePath);

            return File(
                fileBytes,
                document.FileType ?? "application/octet-stream",
                document.FileName);
        }

        // =============================================================
        // DELETE DOCUMENT
        // =============================================================

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var document =
                await _context.Documents
                    .Include(d => d.InvoiceData)
                    .Include(d => d.Approvals)
                    .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            DeleteFileIfExists(document.FilePath);

            if (document.InvoiceData != null)
            {
                _context.InvoiceData.Remove(
                    document.InvoiceData);
            }

            if (document.Approvals != null &&
                document.Approvals.Any())
            {
                _context.Approvals.RemoveRange(
                    document.Approvals);
            }

            _context.Documents.Remove(document);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Document deleted successfully."
            });
        }

        // =============================================================
        // SHA256
        // =============================================================

        private async Task<string> CalculateSha256Async(
            string filePath)
        {
            await using var stream =
                System.IO.File.OpenRead(filePath);

            using var sha256 =
                SHA256.Create();

            byte[] hash =
                await sha256.ComputeHashAsync(stream);

            return Convert.ToHexString(hash)
                .ToLowerInvariant();
        }

        // =============================================================
        // DELETE FILE
        // =============================================================

        private void DeleteFileIfExists(
            string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch
            {
                // Do not hide the original exception.
            }
        }
    }
}
