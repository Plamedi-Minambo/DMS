using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagement.API.Models
{
    public class InvoiceData
    {
        [Key]
        public int Id { get; set; }

        // Links this extracted information to the uploaded document
        [Required]
        public int DocumentId { get; set; }

        [ForeignKey(nameof(DocumentId))]
        public Document? Document { get; set; }

        // Invoice or Credit Note
        [MaxLength(50)]
        public string? DocumentType { get; set; }

        // Invoice / Credit Note number
        [MaxLength(100)]
        public string? InvoiceNumber { get; set; }

        // Vendor / supplier name
        [MaxLength(255)]
        public string? Vendor { get; set; }

        // Date shown on the invoice
        public DateTime? InvoiceDate { get; set; }

        // Amount before VAT
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; }

        // VAT / Tax amount
        [Column(TypeName = "decimal(18,2)")]
        public decimal? VAT { get; set; }

        // Final amount including VAT
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }

        // When the AI/OCR extraction was performed
        public DateTime? ExtractedAt { get; set; }

        // Allows us to track whether extraction succeeded
        [MaxLength(50)]
        public string ExtractionStatus { get; set; } = "Pending";
    }
}