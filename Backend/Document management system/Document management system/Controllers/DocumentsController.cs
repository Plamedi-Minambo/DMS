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
        private readonly DocumentContentExtractionService _documentContentExtractionService;
        private readonly GeminiInvoiceExtractionService _geminiInvoiceExtractionService;

        public DocumentsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            DocumentContentExtractionService documentContentExtractionService,
            GeminiInvoiceExtractionService geminiInvoiceExtractionService)
        {
            _context = context;
            _environment = environment;
            _documentContentExtractionService = documentContentExtractionService;
            _geminiInvoiceExtractionService = geminiInvoiceExtractionService;
        }

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

            var originalFileName =
                Path.GetFileName(file.FileName);

            var extension =
                Path.GetExtension(originalFileName)
                    .ToLowerInvariant();

            var allowedExtensions = new[]
            {
                ".pdf",
                ".docx",
                ".jpg",
                ".jpeg",
                ".png"
            };

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    message =
                        "Unsupported file type. Please upload a PDF, DOCX, JPG, JPEG, or PNG document."
                });
            }

            var uploadsFolder =
                Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var storedFileName =
                $"{Guid.NewGuid():N}{extension}";

            var filePath =
                Path.Combine(
                    uploadsFolder,
                    storedFileName);

            try
            {
                await using (var stream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

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
                        Convert.ToHexString(
                            hashBytes);
                }

                var duplicateByFileHash =
                    await _context.Documents.AnyAsync(
                        d => d.FileHash == fileHash);

                if (duplicateByFileHash)
                {
                    DeleteFileIfExists(filePath);

                    return Conflict(new
                    {
                        message =
                            "Duplicate document detected. This exact file has already been uploaded."
                    });
                }

                string extractedText;

                try
                {
                    extractedText =
                        await _documentContentExtractionService
                            .ExtractTextAsync(
                                filePath,
                                extension);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Document content extraction failed: {ex.Message}");

                    DeleteFileIfExists(filePath);

                    return BadRequest(new
                    {
                        message =
                            "The document could not be read. Please upload a valid PDF, DOCX, JPG, JPEG, or PNG file."
                    });
                }

                if (string.IsNullOrWhiteSpace(
                    extractedText))
                {
                    DeleteFileIfExists(filePath);

                    return BadRequest(new
                    {
                        message =
                            "No readable text was found in the document. Please upload a readable Invoice or Credit Note."
                    });
                }

                InvoiceData extractedInvoiceData;

                try
                {
                   extractedInvoiceData =
    await _geminiInvoiceExtractionService
        .ExtractInvoiceDataAsync(
            0,
            filePath,
            extension,
            extractedText);
                catch (Exception ex)
                {
                    Console.WriteLine(
                     $"Gemini invoice extraction failed: {ex}");
                    DeleteFileIfExists(filePath);

                    return StatusCode(
                        StatusCodes.Status502BadGateway,
                        new
                        {
                            message =
                                "The document text was read successfully, but Gemini AI could not extract the invoice information. Please try again."
                        });
                }

                var documentType =
                    extractedInvoiceData
                        .DocumentType?
                        .Trim();

                var isInvoice =
                    string.Equals(
                        documentType,
                        "Invoice",
                        StringComparison.OrdinalIgnoreCase);

                var isCreditNote =
                    string.Equals(
                        documentType,
                        "Credit Note",
                        StringComparison.OrdinalIgnoreCase);

                if (!isInvoice && !isCreditNote)
                {
                    DeleteFileIfExists(filePath);

                    return UnprocessableEntity(new
                    {
                        message =
                            "This document was rejected because its content could not be identified as an Invoice or Credit Note."
                    });
                }

                var normalizedInvoiceNumber =
                    extractedInvoiceData
                        .InvoiceNumber?
                        .Trim()
                        .ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(
                    normalizedInvoiceNumber))
                {
                    var duplicateByInvoiceNumber =
                        await _context.InvoiceData.AnyAsync(
                            i =>
                                i.InvoiceNumber != null &&
                                i.InvoiceNumber
                                    .Trim()
                                    .ToLower() ==
                                normalizedInvoiceNumber);

                    if (duplicateByInvoiceNumber)
                    {
                        DeleteFileIfExists(filePath);

                        return Conflict(new
                        {
                            message =
                                "Duplicate document detected. " +
                                $"Invoice number '{extractedInvoiceData.InvoiceNumber}' already exists."
                        });
                    }
                }

                var userId =
    User.FindFirstValue(
        ClaimTypes.NameIdentifier);

if (string.IsNullOrWhiteSpace(userId))
{
    DeleteFileIfExists(filePath);

    return Unauthorized(new
    {
        message =
            "Your login session does not contain a valid user ID. Please log out and log in again."
    });
}

var userExists =
    await _context.Users
        .AnyAsync(u => u.Id == userId);

if (!userExists)
{
    Console.WriteLine(
        $"Authenticated user ID '{userId}' was not found in the Users table.");

    DeleteFileIfExists(filePath);

    return Unauthorized(new
    {
        message =
            "Your account could not be found in the database. Please log out and log in again."
    });
}

var document = new Document
                {
                    FileName =
                        originalFileName,

                    FileType =
                        file.ContentType,

                    FileSize =
                        file.Length,

                    FilePath =
                        filePath,

                    StoredFileName =
                        storedFileName,

                    FileHash =
                        fileHash,

                    UploadedAt =
                        DateTime.UtcNow,

                    Status =
                        "Pending",

                    Description =
                        description,

                    UploadedById =
                        userId
                };

                var invoiceData = new InvoiceData
                {
                    Document =
                        document,

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

                    ExtractedAt =
                        extractedInvoiceData.ExtractedAt,

                    ExtractionStatus =
                        extractedInvoiceData.ExtractionStatus
                };

                var approvals = new[]
                {
                    new Approval
                    {
                        Document = document,
                        Stage = 1,
                        Role = "Reviewer",
                        Status = "Pending"
                    },

                    new Approval
                    {
                        Document = document,
                        Stage = 2,
                        Role = "Manager",
                        Status = "Pending"
                    },

                    new Approval
                    {
                        Document = document,
                        Stage = 3,
                        Role = "Finance",
                        Status = "Pending"
                    }
                };

                _context.Documents.Add(
                    document);

                _context.InvoiceData.Add(
                    invoiceData);

                _context.Approvals.AddRange(
                    approvals);

                await _context.SaveChangesAsync();

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
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Document upload failed: {ex.Message}");

                DeleteFileIfExists(filePath);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "An unexpected error occurred while processing the document."
                    });
            }
        }

        [Authorize(
            Roles = "Admin,Reviewer,Manager,Finance,Viewer")]
        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            var documents =
                await _context.Documents
                    .Include(d => d.UploadedBy)
                    .Include(d => d.InvoiceData)
                    .OrderByDescending(
                        d => d.UploadedAt)
                    .Select(d => new
                    {
                        d.Id,
                        d.FileName,
                        d.FileType,
                        d.FileSize,
                        d.Status,
                        d.Description,
                        d.UploadedAt,

                        UploadedBy =
                            d.UploadedBy != null
                                ? d.UploadedBy.FullName
                                : "Unknown",

                        InvoiceData =
                            d.InvoiceData == null
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

        [Authorize(
            Roles = "Admin,Reviewer,Manager,Finance")]
        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadDocument(
            int id)
        {
            var document =
                await _context.Documents
                    .FirstOrDefaultAsync(
                        d => d.Id == id);

            if (document == null)
            {
                return NotFound(new
                {
                    message =
                        "Document not found."
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
                    document.FilePath);

            return File(
                fileBytes,
                document.FileType ??
                "application/octet-stream",
                document.FileName);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(
            int id)
        {
            var document =
                await _context.Documents
                    .FirstOrDefaultAsync(
                        d => d.Id == id);

            if (document == null)
            {
                return NotFound(new
                {
                    message =
                        "Document not found."
                });
            }

            var invoiceData =
                await _context.InvoiceData
                    .FirstOrDefaultAsync(
                        i => i.DocumentId == id);

            if (invoiceData != null)
            {
                _context.InvoiceData.Remove(
                    invoiceData);
            }

            if (!string.IsNullOrWhiteSpace(
                document.FilePath) &&
                System.IO.File.Exists(
                    document.FilePath))
            {
                System.IO.File.Delete(
                    document.FilePath);
            }

            _context.Documents.Remove(
                document);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Document deleted successfully."
            });
        }

        private static void DeleteFileIfExists(
            string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(
                    filePath) &&
                    System.IO.File.Exists(
                        filePath))
                {
                    System.IO.File.Delete(
                        filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Could not delete temporary file: {ex.Message}");
            }
        }
    }
}
