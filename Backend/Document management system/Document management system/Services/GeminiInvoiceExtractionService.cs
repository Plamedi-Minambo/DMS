using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentManagement.API.Models;
using Google.GenAI;
using Google.GenAI.Types;

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
                ?? System.Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException(
                    "Gemini API key is not configured. " +
                    "Set GEMINI_API_KEY in the environment variables.");
            }
        }

        // ============================================================
        // MAIN EXTRACTION METHOD
        // ============================================================

        public async Task<InvoiceData> ExtractInvoiceDataAsync(
            int documentId,
            string filePath,
            string fileExtension,
            string? extractedText = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "File path cannot be empty.",
                    nameof(filePath));
            }

            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Invoice file could not be found.",
                    filePath);
            }

            var extension =
                fileExtension?
                    .Trim()
                    .ToLowerInvariant();

            var client =
                new Client(apiKey: _apiKey);

            var prompt =
                BuildInvoiceExtractionPrompt();

            Content contents;

            // ========================================================
            // IMAGE / PDF
            //
            // Send the ORIGINAL document directly to Gemini.
            // This bypasses OCR as the primary source for these files.
            // ========================================================

            if (extension is ".png" or ".jpg" or ".jpeg" or ".pdf")
            {
                var fileBytes =
                    await System.IO.File.ReadAllBytesAsync(filePath);

                if (fileBytes.Length == 0)
                {
                    throw new InvalidOperationException(
                        "The uploaded document is empty.");
                }

                var mimeType =
                    GetMimeType(extension);

                contents =
                    new Content
                    {
                        Role = "user",

                        Parts = new List<Part>
                        {
                            new Part
                            {
                                Text = prompt
                            },

                            new Part
                            {
                                InlineData = new Blob
                                {
                                    Data = fileBytes,
                                    MimeType = mimeType
                                }
                            }
                        }
                    };
            }

            // ========================================================
            // DOCX
            //
            // Gemini receives the extracted DOCX text.
            // ========================================================

            else if (extension == ".docx")
            {
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    throw new InvalidOperationException(
                        "No readable text was found in the DOCX document.");
                }

                contents =
                    new Content
                    {
                        Role = "user",

                        Parts = new List<Part>
                        {
                            new Part
                            {
                                Text =
                                    prompt +
                                    "\n\n" +
                                    "DOCUMENT TEXT:\n\n" +
                                    extractedText
                            }
                        }
                    };
            }
            else
            {
                throw new NotSupportedException(
                    $"The file type '{extension}' is not supported for AI invoice extraction.");
            }

            // ========================================================
            // GEMINI MODELS
            //
            // Primary model:
            // gemini-3.8-flash
            //
            // Fallback model:
            // gemini-3.7-flash
            //
            // If the primary model is temporarily unavailable,
            // the service automatically falls back to the second model.
            // ========================================================

            var models =
                new[]
                {
                    "gemini-3.7-flash",
                    "gemini-3.6-flash"
                };

            Google.GenAI.Types.GenerateContentResponse? response = null;

            const int maxAttemptsPerModel = 3;

            // ========================================================
            // GEMINI REQUEST + RETRY + FALLBACK
            // ========================================================

            foreach (var model in models)
            {
                for (var attempt = 1;
                     attempt <= maxAttemptsPerModel;
                     attempt++)
                {
                    try
                    {
                        Console.WriteLine(
                            $"Gemini invoice extraction using model '{model}', " +
                            $"attempt {attempt} of {maxAttemptsPerModel}.");

                        response =
                            await client.Models.GenerateContentAsync(
                                model: model,
                                contents: contents,
                                config: new GenerateContentConfig
                                {
                                    ResponseMimeType =
                                        "application/json"
                                });

                        Console.WriteLine(
                            $"Gemini invoice extraction succeeded using model '{model}'.");

                        break;
                    }
                    catch (Google.GenAI.ServerError ex)
                    {
                        Console.WriteLine(
                            $"Gemini server error using model '{model}' " +
                            $"on attempt {attempt}: {ex.Message}");

                        if (attempt == maxAttemptsPerModel)
                        {
                            Console.WriteLine(
                                $"Gemini model '{model}' failed all " +
                                $"{maxAttemptsPerModel} attempts.");

                            break;
                        }

                        var delaySeconds =
                            Math.Pow(2, attempt);

                        Console.WriteLine(
                            $"Gemini model '{model}' appears temporarily unavailable. " +
                            $"Retrying in {delaySeconds} seconds...");

                        await Task.Delay(
                            TimeSpan.FromSeconds(delaySeconds));
                    }
                }

                if (response != null)
                {
                    break;
                }

                Console.WriteLine(
                    $"Switching to Gemini fallback model after " +
                    $"'{model}' was unavailable.");
            }

            if (response == null)
            {
                throw new InvalidOperationException(
                    "Gemini invoice extraction failed. " +
                    "Both Gemini models were temporarily unavailable.");
            }

            // ========================================================
            // READ GEMINI RESPONSE
            // ========================================================

            var responseText =
                response.Text?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new InvalidOperationException(
                    "Gemini returned an empty response.");
            }

            Console.WriteLine(
                $"Gemini raw invoice response: {responseText}");

            responseText =
                CleanJsonResponse(responseText);

            GeminiInvoiceResult? result;

            try
            {
                result =
                    JsonSerializer.Deserialize<GeminiInvoiceResult>(
                        responseText,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
            }
            catch (JsonException ex)
            {
                Console.WriteLine(
                    $"Gemini JSON parsing failed: {ex.Message}");

                throw new InvalidOperationException(
                    "Gemini returned invoice information in an invalid format.",
                    ex);
            }

            if (result == null)
            {
                throw new InvalidOperationException(
                    "Gemini returned an invalid invoice response.");
            }

            // ========================================================
            // NORMALIZE DOCUMENT TYPE
            // ========================================================

            var documentType =
                NormalizeDocumentType(
                    result.DocumentType);

            // ========================================================
            // PARSE DATE
            // ========================================================

            DateTime? invoiceDate = null;

            if (!string.IsNullOrWhiteSpace(
                result.InvoiceDate))
            {
                if (DateTime.TryParseExact(
                    result.InvoiceDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
                {
                    invoiceDate =
                        parsedDate;
                }
            }

            // ========================================================
            // RETURN RESULT
            //
            // The CONTROLLER performs final business validation.
            // ========================================================

            return new InvoiceData
            {
                DocumentId =
                    documentId,

                DocumentType =
                    documentType,

                InvoiceNumber =
                    CleanString(
                        result.InvoiceNumber),

                Vendor =
                    CleanString(
                        result.Vendor),

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
                    documentType == "Other"
                        ? "Rejected"
                        : "Completed"
            };
        }

        // ============================================================
        // GEMINI PROMPT
        // ============================================================

        private static string BuildInvoiceExtractionPrompt()
        {
            return
                """
                You are an AI system used inside a Document Management System
                to validate and extract financial invoice information.

                Carefully inspect the supplied document.

                Your first responsibility is to determine whether the document
                is genuinely an Invoice or Credit Note.

                ============================================================
                DOCUMENT CLASSIFICATION
                ============================================================

                documentType MUST be exactly one of:

                "Invoice"
                "Credit Note"
                "Other"

                Return "Invoice" only when the document genuinely represents
                an invoice requesting or recording payment for goods or services.

                Return "Credit Note" only when the document genuinely represents
                a credit note.

                Return "Other" for documents such as:

                - CVs
                - identity documents
                - contracts
                - letters
                - bank statements
                - random screenshots
                - quotations
                - purchase orders
                - delivery notes
                - receipts that are not invoices
                - blank images
                - unrelated documents
                - documents pretending to be invoices but without meaningful
                  invoice information

                Do not classify a document as an Invoice merely because the word
                "invoice" appears somewhere in the document.

                ============================================================
                REQUIRED EXTRACTION RULES
                ============================================================

                1. Extract the invoice number exactly as displayed.

                   Example:
                   INV-20394 must remain INV-20394.

                2. In this system, "vendor" means the customer/company shown in
                   the BILL TO, BILLED TO, CUSTOMER, or equivalent customer
                   section.

                3. Never use a VAT registration number as the VAT monetary value.

                   Example:
                   VAT No: 4650198237

                   This is a registration number and must NOT become the VAT
                   amount.

                4. VAT means the actual tax amount charged on the document.

                5. amount means the amount before VAT after discounts.

                   Example:

                   Subtotal = 30850.00
                   Discount = 1000.00

                   amount = 29850.00

                6. totalAmount means the final amount payable / amount due after
                   VAT and applicable discounts.

                7. Never interpret an invoice number, account number, VAT number,
                   telephone number, registration number, quantity, postal code,
                   or reference number as a monetary value.

                8. Monetary fields must contain numbers only.

                   Do not include:
                   R
                   $
                   ZAR
                   commas
                   spaces

                9. Return invoiceDate in exactly:

                   yyyy-MM-dd

                10. If a field cannot be determined reliably, return null.

                11. Never invent missing information.

                12. Read values from the actual document. Use the layout,
                    headings, tables and visual relationships where applicable.

                ============================================================
                REAL INVOICE REQUIREMENTS
                ============================================================

                A valid Invoice or Credit Note should contain sufficient
                financial-document information.

                Look for evidence such as:

                - an invoice or credit note number
                - invoice/credit-note date
                - customer or BILL TO information
                - supplier/business information
                - goods or services
                - subtotal / amount
                - VAT/tax when applicable
                - final total or amount due

                VAT itself is NOT mandatory because some legitimate invoices may
                have no VAT.

                If the document does not contain enough evidence to reasonably
                identify it as a genuine Invoice or Credit Note, return
                documentType as "Other".

                ============================================================
                RESPONSE
                ============================================================

                Return ONLY a JSON object.

                Use exactly these property names:

                documentType
                invoiceNumber
                vendor
                invoiceDate
                amount
                vat
                totalAmount

                Example:

                {
                  "documentType": "Invoice",
                  "invoiceNumber": "INV-20394",
                  "vendor": "Northgate Retail Group (Pty) Ltd",
                  "invoiceDate": "2026-09-05",
                  "amount": 29850.00,
                  "vat": 4477.50,
                  "totalAmount": 34327.50
                }

                For an unrelated document:

                {
                  "documentType": "Other",
                  "invoiceNumber": null,
                  "vendor": null,
                  "invoiceDate": null,
                  "amount": null,
                  "vat": null,
                  "totalAmount": null
                }
                """;
        }

        // ============================================================
        // MIME TYPE
        // ============================================================

        private static string GetMimeType(
            string extension)
        {
            return extension switch
            {
                ".png" =>
                    "image/png",

                ".jpg" =>
                    "image/jpeg",

                ".jpeg" =>
                    "image/jpeg",

                ".pdf" =>
                    "application/pdf",

                _ =>
                    throw new NotSupportedException(
                        $"Unsupported Gemini file type: {extension}")
            };
        }

        // ============================================================
        // DOCUMENT TYPE NORMALIZATION
        // ============================================================

        private static string NormalizeDocumentType(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Other";
            }

            var normalized =
                value.Trim();

            if (string.Equals(
                normalized,
                "Invoice",
                StringComparison.OrdinalIgnoreCase))
            {
                return "Invoice";
            }

            if (string.Equals(
                normalized,
                "Credit Note",
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    normalized,
                    "CreditNote",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Credit Note";
            }

            return "Other";
        }

        // ============================================================
        // JSON CLEANUP
        // ============================================================

        private static string CleanJsonResponse(
            string response)
        {
            response =
                response.Trim();

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

            if (start >= 0 &&
                end > start)
            {
                response =
                    response.Substring(
                        start,
                        end - start + 1);
            }

            return response.Trim();
        }

        // ============================================================
        // STRING CLEANUP
        // ============================================================

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
