using DocumentManagement.API.Data;
using DocumentManagement.API.Models;
using DocumentManagement.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace DocumentManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly PdfTextExtractionService _pdfTextExtractionService;
        private readonly InvoiceExtractionService _invoiceExtractionService;

        public DocumentsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            PdfTextExtractionService pdfTextExtractionService,
            InvoiceExtractionService invoiceExtractionService)
        {
            _context = context;
            _environment = environment;
            _pdfTextExtractionService = pdfTextExtractionService;
            _invoiceExtractionService = invoiceExtractionService;
        }

        // ========================================
        // UPLOAD DOCUMENT
        // ========================================

        [Authorize(Roles = "Admin,Reviewer,Manager,Finance")]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(
            IFormFile file,
            [FromForm] string? description)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Please select a file to upload."
                });
            }

            var uploadsFolder = Path.Combine(
                _environment.ContentRootPath,
                "Uploads"
            );

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var originalFileName =
                Path.GetFileName(file.FileName);

            var extension =
                Path.GetExtension(originalFileName);

            var storedFileName =
                $"{Guid.NewGuid()}{extension}";

            var filePath = Path.Combine(
                uploadsFolder,
                storedFileName
            );

            // ========================================
            // SAVE PHYSICAL FILE
            // ========================================

            await using (var stream = new FileStream(
                filePath,
                FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // ========================================
            // GENERATE FILE HASH
            // ========================================

            string fileHash;

            await using (var hashStream =
                System.IO.File.OpenRead(filePath))
            {
                using var sha256 =
                    SHA256.Create();

                var hashBytes =
                    await sha256.ComputeHashAsync(
                        hashStream);

                fileHash =
                    Convert.ToHexString(hashBytes);
            }

            // ========================================
            // DUPLICATE CHECK 1
            // EXACT SAME FILE
            // ========================================

            var duplicateByFileHash =
                await _context.Documents
                    .AnyAsync(d =>
                        d.FileHash == fileHash);

            if (duplicateByFileHash)
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                return Conflict(new
                {
                    message =
                        "Duplicate document detected. " +
                        "This exact file has already been uploaded."
                });
            }

            // ========================================
            // GET LOGGED-IN USER
            // ========================================

            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            // ========================================
            // CREATE DOCUMENT
            // ========================================

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
                Description = description,
                UploadedById = userId
            };

            _context.Documents.Add(document);

            await _context.SaveChangesAsync();

            // ========================================
            // CREATE 3-STAGE APPROVAL WORKFLOW
            // ========================================

            _context.Approvals.AddRange(
                new Approval
                {
                    DocumentId = document.Id,
                    Stage = 1,
                    Role = "Reviewer",
                    Status = "Pending"
                },
                new Approval
                {
                    DocumentId = document.Id,
                    Stage = 2,
                    Role = "Manager",
                    Status = "Pending"
                },
                new Approval
                {
                    DocumentId = document.Id,
                    Stage = 3,
                    Role = "Finance",
                    Status = "Pending"
                }
            );

            await _context.SaveChangesAsync();

            // ========================================
            // CREATE INVOICE DATA RECORD
            // ========================================

            var invoiceData = new InvoiceData
            {
                DocumentId = document.Id,
                DocumentType = null,
                InvoiceNumber = null,
                Vendor = null,
                InvoiceDate = null,
                Amount = null,
                VAT = null,
                TotalAmount = null,
                ExtractedAt = null,
                ExtractionStatus = "Pending"
            };

            _context.InvoiceData.Add(invoiceData);

            await _context.SaveChangesAsync();

            // ========================================
            // PDF TEXT EXTRACTION
            // ========================================

            if (string.Equals(
                extension,
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var extractedText =
                        await _pdfTextExtractionService
                            .ExtractTextAsync(filePath);

                    // ========================================
                    // INVOICE DATA EXTRACTION
                    // ========================================

                    var extractedInvoiceData =
                        _invoiceExtractionService
                            .ExtractInvoiceData(
                                document.Id,
                                extractedText);

                    invoiceData.DocumentType =
                        extractedInvoiceData.DocumentType;

                    invoiceData.InvoiceNumber =
                        extractedInvoiceData.InvoiceNumber;

                    invoiceData.Vendor =
                        extractedInvoiceData.Vendor;

                    invoiceData.InvoiceDate =
                        extractedInvoiceData.InvoiceDate;

                    invoiceData.Amount =
                        extractedInvoiceData.Amount;

                    invoiceData.VAT =
                        extractedInvoiceData.VAT;

                    invoiceData.TotalAmount =
                        extractedInvoiceData.TotalAmount;

                    invoiceData.ExtractedAt =
                        extractedInvoiceData.ExtractedAt;

                    invoiceData.ExtractionStatus =
                        extractedInvoiceData.ExtractionStatus;

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    invoiceData.ExtractionStatus = "Failed";
                    invoiceData.ExtractedAt =
                        DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    Console.WriteLine(
                        $"PDF extraction failed: {ex.Message}");
                }
            }
            else
            {
                invoiceData.ExtractionStatus =
                    "Pending";

                await _context.SaveChangesAsync();
            }

            // ========================================
            // DUPLICATE CHECK 2
            // SAME INVOICE NUMBER
            // ========================================

            var normalizedInvoiceNumber =
                invoiceData.InvoiceNumber?
                    .Trim()
                    .ToLower();

            if (!string.IsNullOrWhiteSpace(
                normalizedInvoiceNumber))
            {
                var duplicateByInvoiceNumber =
                    await _context.InvoiceData
                        .AnyAsync(i =>
                            i.DocumentId != document.Id &&
                            i.InvoiceNumber != null &&
                            i.InvoiceNumber
                                .Trim()
                                .ToLower() ==
                            normalizedInvoiceNumber);

                if (duplicateByInvoiceNumber)
                {
                    // Delete invoice data
                    _context.InvoiceData.Remove(
                        invoiceData);

                    // Delete document
                    _context.Documents.Remove(
                        document);

                    await _context.SaveChangesAsync();

                    // Delete physical file
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }

                    return Conflict(new
                    {
                        message =
                            "Duplicate document detected. " +
                            $"Invoice number '{invoiceData.InvoiceNumber}' " +
                            "already exists."
                    });
                }
            }

            // ========================================
            // DUPLICATE CHECK 3
            // SAME VENDOR + SAME AMOUNT
            // ========================================

            var normalizedVendor =
                invoiceData.Vendor?
                    .Trim()
                    .ToLower();

            if (!string.IsNullOrWhiteSpace(
                    normalizedVendor) &&
                invoiceData.Amount.HasValue)
            {
                var duplicateByVendorAndAmount =
                    await _context.InvoiceData
                        .AnyAsync(i =>
                            i.DocumentId != document.Id &&
                            i.Vendor != null &&
                            i.Vendor
                                .Trim()
                                .ToLower() ==
                            normalizedVendor &&
                            i.Amount.HasValue &&
                            i.Amount.Value ==
                            invoiceData.Amount.Value);

                if (duplicateByVendorAndAmount)
                {
                    // Delete invoice data
                    _context.InvoiceData.Remove(
                        invoiceData);

                    // Delete document
                    _context.Documents.Remove(
                        document);

                    await _context.SaveChangesAsync();

                    // Delete physical file
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }

                    return Conflict(new
                    {
                        message =
                            "Duplicate document detected. " +
                            "A document with the same vendor " +
                            "and amount already exists."
                    });
                }
            }

            // ========================================
            // RETURN RESPONSE
            // ========================================

            return Ok(new
            {
                message =
                    "Document uploaded successfully.",

                document = new
                {
                    document.Id,
                    document.FileName,
                    document.FileType,
                    document.FileSize,
                    document.Status,
                    document.Description,
                    document.UploadedAt,

                    InvoiceData = new
                    {
                        invoiceData.Id,
                        invoiceData.DocumentType,
                        invoiceData.InvoiceNumber,
                        invoiceData.Vendor,
                        invoiceData.InvoiceDate,
                        invoiceData.Amount,
                        invoiceData.VAT,
                        invoiceData.TotalAmount,
                        invoiceData.ExtractedAt,
                        invoiceData.ExtractionStatus
                    }
                }
            });
        }

        // ========================================
        // GET ALL DOCUMENTS
        // ========================================

        [Authorize(Roles = "Admin,Reviewer,Manager,Finance,Viewer")]
        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            var documents = await _context.Documents
                .Include(d => d.UploadedBy)
                .Include(d => d.InvoiceData)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    d.Id,
                    d.FileName,
                    d.FileType,
                    d.FileSize,
                    d.Status,
                    d.Description,
                    d.UploadedAt,

                    UploadedBy = d.UploadedBy != null
                        ? d.UploadedBy.FullName
                        : "Unknown",

                    InvoiceData = d.InvoiceData == null
                        ? null
                        : new
                        {
                            d.InvoiceData.Id,
                            d.InvoiceData.DocumentType,
                            d.InvoiceData.InvoiceNumber,
                            d.InvoiceData.Vendor,
                            d.InvoiceData.InvoiceDate,
                            d.InvoiceData.Amount,
                            d.InvoiceData.VAT,
                            d.InvoiceData.TotalAmount,
                            d.InvoiceData.ExtractedAt,
                            d.InvoiceData.ExtractionStatus
                        }
                })
                .ToListAsync();

            return Ok(documents);
        }

        // ========================================
        // VIEW DOCUMENT
        // ========================================

        [Authorize(Roles = "Admin,Reviewer,Manager,Finance,Viewer")]
        [HttpGet("{id}/view")]
        public async Task<IActionResult> ViewDocument(int id)
        {
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            if (string.IsNullOrWhiteSpace(
                document.FilePath))
            {
                return NotFound(new
                {
                    message =
                        "Document file path is missing."
                });
            }

            if (!System.IO.File.Exists(
                document.FilePath))
            {
                return NotFound(new
                {
                    message =
                        "Document file could not be found."
                });
            }

            var fileBytes =
                await System.IO.File.ReadAllBytesAsync(
                    document.FilePath
                );

            var contentType =
                string.IsNullOrWhiteSpace(
                    document.FileType)
                    ? "application/pdf"
                    : document.FileType;

            Response.Headers.ContentDisposition =
                "inline";

            return File(
                fileBytes,
                contentType
            );
        }

        // ========================================
        // DOWNLOAD DOCUMENT
        // ========================================

        [Authorize(Roles = "Admin,Reviewer,Manager,Finance")]
        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadDocument(
            int id)
        {
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            if (string.IsNullOrWhiteSpace(
                document.FilePath))
            {
                return NotFound(new
                {
                    message =
                        "Document file path is missing."
                });
            }

            if (!System.IO.File.Exists(
                document.FilePath))
            {
                return NotFound(new
                {
                    message =
                        "Document file could not be found."
                });
            }

            var fileBytes =
                await System.IO.File.ReadAllBytesAsync(
                    document.FilePath
                );

            return File(
                fileBytes,
                document.FileType ??
                    "application/octet-stream",
                document.FileName
            );
        }

        // ========================================
        // DELETE DOCUMENT
        // ========================================

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(
            int id)
        {
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            // ========================================
            // DELETE ASSOCIATED INVOICE DATA
            // ========================================

            var invoiceData =
                await _context.InvoiceData
                    .FirstOrDefaultAsync(
                        i => i.DocumentId == id);

            if (invoiceData != null)
            {
                _context.InvoiceData.Remove(
                    invoiceData);
            }

            // ========================================
            // DELETE PHYSICAL FILE
            // ========================================

            if (!string.IsNullOrWhiteSpace(
                document.FilePath))
            {
                if (System.IO.File.Exists(
                    document.FilePath))
                {
                    System.IO.File.Delete(
                        document.FilePath);
                }
            }

            // ========================================
            // DELETE DATABASE RECORD
            // ========================================

            _context.Documents.Remove(document);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Document deleted successfully."
            });
        }
    }
}

