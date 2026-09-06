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

        public GeminiInvoiceExtractionService(
            IConfiguration configuration)
        {
            _apiKey =
                configuration["Gemini:ApiKey"]
                ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException(
                    "Gemini API key is not configured. " +
                    "Set GEMINI_API_KEY in the environment variables.");
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

            var prompt =
                "You are an invoice data extraction AI.\n\n" +

                "Extract structured information from the invoice text below.\n\n" +

                "IMPORTANT RULES:\n" +

                "1. Extract the invoice number exactly.\n" +
                "   Example: INV-20394 must remain INV-20394.\n\n" +

                "2. The vendor means the customer/company listed immediately " +
                "after BILL TO.\n\n" +

                "3. Do NOT treat a VAT registration number as the VAT amount.\n" +
                "   Example: VAT No: 4650198237 is a registration number, " +
                "not VAT.\n\n" +

                "4. VAT means the actual tax amount on the invoice.\n\n" +

                "5. Amount means subtotal minus discount, before VAT.\n\n" +

                "6. If Subtotal is 30850.00 and Discount is 1000.00, " +
                "Amount must be 29850.00.\n\n" +

                "7. TotalAmount means the final total amount due.\n\n" +

                "8. Never interpret an invoice number as a monetary amount.\n\n" +

                "9. Return monetary numbers without currency symbols or commas.\n\n" +

                "10. Return the invoice date in yyyy-MM-dd format.\n\n" +

                "11. If a value cannot be determined, return null.\n\n" +

                "Return ONLY valid JSON using exactly these property names:\n\n" +

                "documentType\n" +
                "invoiceNumber\n" +
                "vendor\n" +
                "invoiceDate\n" +
                "amount\n" +
                "vat\n" +
                "totalAmount\n\n" +

                "Example format:\n" +
                "{\"documentType\":\"Invoice\"," +
                "\"invoiceNumber\":\"INV-20394\"," +
                "\"vendor\":\"Northgate Retail Group (Pty) Ltd\"," +
                "\"invoiceDate\":\"2026-09-05\"," +
                "\"amount\":29850.00," +
                "\"vat\":4477.50," +
                "\"totalAmount\":34327.50}\n\n" +

                "Invoice text:\n\n" +
                extractedText;

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
                        : result.DocumentType.Trim(),

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
