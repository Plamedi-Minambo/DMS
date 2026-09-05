using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.GenAI;
using DocumentManagement.API.Models;

namespace DocumentManagement.API.Services
{
    public class GeminiInvoiceExtractionService
    {
        private readonly string _apiKey;

        public GeminiInvoiceExtractionService(IConfiguration configuration)
        {
            _apiKey =
                configuration["Gemini:ApiKey"]
                ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException(
                    "Gemini API key is not configured. " +
                    "Set Gemini:ApiKey or GEMINI_API_KEY.");
            }
        }

        public async Task<InvoiceData> ExtractInvoiceDataAsync(
            int documentId,
            string extractedText)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return new InvoiceData
                {
                    DocumentId = documentId,
                    ExtractionStatus = "Failed",
                    ExtractedAt = DateTime.UtcNow
                };
            }

            var client = new Client(
                apiKey: _apiKey);

            var prompt = $"""
                You are an invoice data extraction AI.

                Extract structured information from the invoice text below.

                IMPORTANT RULES:

                1. Extract the invoice number exactly.
                   Example: INV-20394 must remain INV-20394.

                2. The vendor/customer is the company name immediately
                   following "BILL TO".

                3. Do NOT treat a VAT registration number as VAT.
                   For example:
                   "VAT No: 4650198237"
                   is a registration number, NOT the VAT amount.

                4. VAT must be the actual tax amount.
                   In this invoice:
                   "Tax (VAT 15%) 4,477.50"
                   means VAT = 4477.50.

                5. Amount means the amount before VAT after discounts.

                6. If the invoice contains:
                   Subtotal = 30850.00
                   Discount = 1000.00
                   then Amount = 29850.00.

                7. Total Amount means the final amount due.

                8. Never interpret an invoice number as a monetary value.

                9. Return numbers without currency symbols or commas.

                10. Return the invoice date as yyyy-MM-dd.

                11. If a field cannot be determined, return null.

                Return ONLY valid JSON matching this structure:

                {{
                    "documentType": "Invoice",
                    "invoiceNumber": "INV-20394",
                    "vendor": "Northgate Retail Group (Pty) Ltd",
                    "invoiceDate": "2026-09-05",
                    "amount": 29850.00,
                    "vat": 4477.50,
                    "totalAmount": 34327.50
                }}

                Invoice text:

                {extractedText}
                """;

            var response =
                await client.Models.GenerateContentAsync(
                    model: "gemini-3.8-flash",
                    contents: prompt);

            var responseText =
                response.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new InvalidOperationException(
                    "Gemini returned an empty response.");
            }

            responseText =
                CleanJsonResponse(responseText);

            var result =
                JsonSerializer.Deserialize<GeminiInvoiceResult>(
                    responseText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (result == null)
            {
                throw new InvalidOperationException(
                    "Gemini returned an invalid invoice response.");
            }

            DateTime? invoiceDate = null;

            if (!string.IsNullOrWhiteSpace(result.InvoiceDate))
            {
                if (DateTime.TryParseExact(
                    result.InvoiceDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
                {
                    invoiceDate = parsedDate;
                }
            }

            return new InvoiceData
            {
                DocumentId = documentId,

                DocumentType =
                    string.IsNullOrWhiteSpace(result.DocumentType)
                        ? "Invoice"
                        : result.DocumentType,

                InvoiceNumber =
                    CleanString(result.InvoiceNumber),

                Vendor =
                    CleanString(result.Vendor),

                InvoiceDate =
                    invoiceDate,

                Amount =
                    result.Amount,

                VAT =
                    result.VAT,

                TotalAmount =
                    result.TotalAmount,

                ExtractedAt =
                    DateTime.UtcNow,

                ExtractionStatus =
                    "Completed"
            };
        }

        private static string CleanJsonResponse(
            string response)
        {
            response = response.Trim();

            if (response.StartsWith("```"))
            {
                response =
                    response
                        .Replace("```json", "")
                        .Replace("```JSON", "")
                        .Replace("```", "")
                        .Trim();
            }

            var start =
                response.IndexOf('{');

            var end =
                response.LastIndexOf('}');

            if (start >= 0 && end > start)
            {
                response =
                    response.Substring(
                        start,
                        end - start + 1);
            }

            return response.Trim();
        }

        private static string? CleanString(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        // ============================================================
        // GEMINI RESPONSE MODEL
        // ============================================================

        private class GeminiInvoiceResult
        {
            [JsonPropertyName("documentType")]
            public string? DocumentType { get; set; }

            [JsonPropertyName("invoiceNumber")]
            public string? InvoiceNumber { get; set; }

            [JsonPropertyName("vendor")]
            public string? Vendor { get; set; }

            [JsonPropertyName("invoiceDate")]
            public string? InvoiceDate { get; set; }

            [JsonPropertyName("amount")]
            public decimal? Amount { get; set; }

            [JsonPropertyName("vat")]
            public decimal? VAT { get; set; }

            [JsonPropertyName("totalAmount")]
            public decimal? TotalAmount { get; set; }
        }
    }
}
