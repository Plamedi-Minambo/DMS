using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentManagement.API.Models;

namespace DocumentManagement.API.Services
{
    public class InvoiceExtractionService
    {
        // ============================================================
        // MAIN EXTRACTION METHOD
        // ============================================================

        public InvoiceData ExtractInvoiceData(
            int documentId,
            string extractedText)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return new InvoiceData
                {
                    DocumentId = documentId,
                    DocumentType = null,
                    InvoiceNumber = null,
                    Vendor = null,
                    InvoiceDate = null,
                    Amount = null,
                    VAT = null,
                    TotalAmount = null,
                    ExtractedAt = DateTime.UtcNow,
                    ExtractionStatus = "Failed"
                };
            }

            var normalizedText = NormalizeText(extractedText);

            var documentType =
                ExtractDocumentType(normalizedText);

            var invoiceNumber =
                ExtractInvoiceNumber(normalizedText);

            var vendor =
                ExtractVendor(normalizedText);

            var invoiceDate =
                ExtractDate(normalizedText);

            var amount =
                ExtractAmount(normalizedText);

            var vat =
                ExtractVAT(normalizedText);

            var totalAmount =
                ExtractTotalAmount(normalizedText);

            var extractionStatus =
                documentType == "Invoice" ||
                documentType == "Credit Note"
                    ? "Completed"
                    : "Failed";

            return new InvoiceData
            {
                DocumentId = documentId,

                DocumentType =
                    documentType,

                InvoiceNumber =
                    invoiceNumber,

                Vendor =
                    vendor,

                InvoiceDate =
                    invoiceDate,

                Amount =
                    amount,

                VAT =
                    vat,

                TotalAmount =
                    totalAmount,

                ExtractedAt =
                    DateTime.UtcNow,

                ExtractionStatus =
                    extractionStatus
            };
        }

        // ============================================================
        // NORMALIZE TEXT
        // ============================================================

        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized =
                text.Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Replace("\t", " ");

            normalized =
                Regex.Replace(
                    normalized,
                    @"[ ]{2,}",
                    " ");

            normalized =
                Regex.Replace(
                    normalized,
                    @"\n{3,}",
                    "\n\n");

            return normalized.Trim();
        }

        // ============================================================
        // DOCUMENT TYPE
        // ============================================================
        //
        // IMPORTANT:
        //
        // The filename is NOT used here.
        //
        // The document must contain actual evidence that it is an
        // Invoice or Credit Note.
        //
        // A random document mentioning the word "invoice" should
        // not automatically be classified as an Invoice.
        // ============================================================

        private string? ExtractDocumentType(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // --------------------------------------------------------
            // CREDIT NOTE DETECTION
            // --------------------------------------------------------

            var hasCreditNoteTitle =
                Regex.IsMatch(
                    text,
                    @"\bcredit\s*(?:note|memo)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasCreditNoteNumber =
                Regex.IsMatch(
                    text,
                    @"\bcredit\s*(?:note|memo)\s*(?:number|no\.?|#)\s*[:\-]?",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasOriginalInvoiceReference =
                Regex.IsMatch(
                    text,
                    @"\b(?:original\s+invoice|invoice\s+(?:number|no\.?|#)|invoice\s+ref(?:erence)?)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasDate =
                Regex.IsMatch(
                    text,
                    @"\b(?:invoice\s*)?(?:date|dated|issue\s+date)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasVendor =
                Regex.IsMatch(
                    text,
                    @"\b(?:vendor|supplier|seller|issued\s+by|from)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasCustomer =
                Regex.IsMatch(
                    text,
                    @"\b(?:customer|client|bill\s+to|sold\s+to|buyer)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasAmount =
                Regex.IsMatch(
                    text,
                    @"\b(?:amount|subtotal|sub\s*total|net\s*amount|total\s*(?:amount|due)?|grand\s*total|balance\s*due|credit\s*amount)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasVAT =
                Regex.IsMatch(
                    text,
                    @"\b(?:VAT|tax|sales\s+tax)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasCreditReason =
                Regex.IsMatch(
                    text,
                    @"\b(?:reason\s+for\s+credit|credit\s+reason|returned\s+goods|goods\s+returned|refund|refund\s+amount|credited\s+amount|overpayment|damaged\s+goods|incorrect\s+invoice)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var creditSupportingEvidence = 0;

            if (hasCreditNoteNumber)
                creditSupportingEvidence++;

            if (hasOriginalInvoiceReference)
                creditSupportingEvidence++;

            if (hasDate)
                creditSupportingEvidence++;

            if (hasVendor)
                creditSupportingEvidence++;

            if (hasCustomer)
                creditSupportingEvidence++;

            if (hasAmount)
                creditSupportingEvidence++;

            if (hasVAT)
                creditSupportingEvidence++;

            if (hasCreditReason)
                creditSupportingEvidence++;

            // A Credit Note must have a strong title AND at least
            // two pieces of supporting document evidence.
            if (hasCreditNoteTitle &&
                creditSupportingEvidence >= 2)
            {
                return "Credit Note";
            }

            // --------------------------------------------------------
            // INVOICE DETECTION
            // --------------------------------------------------------

            var hasInvoiceTitle =
                Regex.IsMatch(
                    text,
                    @"\b(?:tax\s+invoice|invoice)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasInvoiceNumber =
                Regex.IsMatch(
                    text,
                    @"\b(?:invoice|inv\.?)\s*(?:number|no\.?|#)\s*[:\-]?",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasPurchaseOrder =
                Regex.IsMatch(
                    text,
                    @"\b(?:purchase\s+order|PO)\s*(?:number|no\.?|#)?\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var hasPaymentTerms =
                Regex.IsMatch(
                    text,
                    @"\b(?:payment\s+terms|terms\s+of\s+payment|due\s+date|payment\s+due)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            var invoiceSupportingEvidence = 0;

            if (hasInvoiceNumber)
                invoiceSupportingEvidence++;

            if (hasDate)
                invoiceSupportingEvidence++;

            if (hasVendor)
                invoiceSupportingEvidence++;

            if (hasCustomer)
                invoiceSupportingEvidence++;

            if (hasAmount)
                invoiceSupportingEvidence++;

            if (hasVAT)
                invoiceSupportingEvidence++;

            if (hasPurchaseOrder)
                invoiceSupportingEvidence++;

            if (hasPaymentTerms)
                invoiceSupportingEvidence++;

            // An Invoice must contain the invoice title plus at least
            // three supporting indicators.
            //
            // This is deliberately stricter than simply searching
            // for the word "invoice".
            if (hasInvoiceTitle &&
                invoiceSupportingEvidence >= 3)
            {
                return "Invoice";
            }

            return null;
        }

        // ============================================================
        // INVOICE NUMBER
        // ============================================================

        private string? ExtractInvoiceNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var patterns = new[]
            {
                @"\b(?:invoice|inv\.?)\s*(?:number|no\.?|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,})",

                @"\binvoice\s*[:\-]\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,})",

                @"\binv\.?\s*[:\-]\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,})",

                @"\b(?:credit\s*note|credit\s*memo)\s*(?:number|no\.?|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,})",

                @"\b(?:credit\s*note|credit\s*memo)\s*[:\-]\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,})"
            };

            foreach (var pattern in patterns)
            {
                var match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                var value =
                    match.Groups[1].Value.Trim();

                if (IsValidInvoiceNumber(value))
                {
                    return value;
                }
            }

            return null;
        }

        // ============================================================
        // VALIDATE INVOICE NUMBER
        // ============================================================

        private bool IsValidInvoiceNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.Length < 3 ||
                value.Length > 100)
            {
                return false;
            }

            // Must contain at least one digit.
            if (!Regex.IsMatch(
                value,
                @"\d",
                RegexOptions.CultureInvariant))
            {
                return false;
            }

            // Prevent common false positives.
            var invalidValues = new[]
            {
                "number",
                "no",
                "invoice",
                "inv",
                "date",
                "total",
                "amount",
                "value",
                "none",
                "n/a"
            };

            if (invalidValues.Contains(
                value.Trim().ToLowerInvariant()))
            {
                return false;
            }

            return true;
        }

        // ============================================================
        // VENDOR
        // ============================================================

        private string? ExtractVendor(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var lines = GetLines(text);

            var patterns = new[]
            {
                @"^\s*(?:vendor|supplier|seller|issued\s+by|from)\s*[:\-]\s*(.+)$",

                @"^\s*(?:company|company\s+name)\s*[:\-]\s*(.+)$",

                @"^\s*(?:bill\s+from|billed\s+from)\s*[:\-]\s*(.+)$"
            };

            foreach (var line in lines)
            {
                foreach (var pattern in patterns)
                {
                    var match =
                        Regex.Match(
                            line,
                            pattern,
                            RegexOptions.IgnoreCase |
                            RegexOptions.CultureInvariant);

                    if (!match.Success)
                    {
                        continue;
                    }

                    var vendor =
                        CleanVendor(
                            match.Groups[1].Value);

                    if (IsValidVendor(vendor))
                    {
                        return vendor;
                    }
                }
            }

            // Look for common business suffixes.
            foreach (var line in lines)
            {
                var trimmedLine =
                    line.Trim();

                if (Regex.IsMatch(
                    trimmedLine,
                    @"\b(?:Pty|Ltd|Limited|Inc|Incorporated|LLC|CC|Corporation|Corp)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    var vendor =
                        CleanVendor(trimmedLine);

                    if (IsValidVendor(vendor))
                    {
                        return vendor;
                    }
                }
            }

            return null;
        }

        // ============================================================
        // GET LINES
        // ============================================================

        private List<string> GetLines(string text)
        {
            return text
                .Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        // ============================================================
        // CLEAN VENDOR
        // ============================================================

        private string CleanVendor(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var cleaned =
                value.Trim();

            cleaned =
                Regex.Replace(
                    cleaned,
                    @"\s{2,}",
                    " ");

            cleaned =
                cleaned.Trim(
                    ' ',
                    ':',
                    '-',
                    '|',
                    '.',
                    ',',
                    ';');

            return cleaned;
        }

        // ============================================================
        // VALIDATE VENDOR
        // ============================================================

        private bool IsValidVendor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.Length < 2 ||
                value.Length > 255)
            {
                return false;
            }

            var invalidValues = new[]
            {
                "vendor",
                "supplier",
                "seller",
                "company",
                "company name",
                "invoice",
                "tax invoice",
                "credit note",
                "customer",
                "client",
                "unknown",
                "n/a",
                "none"
            };

            if (invalidValues.Contains(
                value.Trim().ToLowerInvariant()))
            {
                return false;
            }

            // Reject lines that are almost entirely numeric.
            var letters =
                value.Count(char.IsLetter);

            if (letters < 2)
            {
                return false;
            }

            return true;
        }

        // ============================================================
        // DATE
        // ============================================================

        private DateTime? ExtractDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var patterns = new[]
            {
                @"\b(?:invoice\s*)?(?:date|dated|issue\s+date)\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",

                @"\b(?:invoice\s*)?(?:date|dated|issue\s+date)\s*[:\-]?\s*(\d{4}[\/\-]\d{1,2}[\/\-]\d{1,2})",

                @"\b(?:invoice\s*)?(?:date|dated|issue\s+date)\s*[:\-]?\s*([A-Za-z]{3,9}\s+\d{1,2},?\s+\d{4})",

                @"\b(?:invoice\s*)?(?:date|dated|issue\s+date)\s*[:\-]?\s*(\d{1,2}\s+[A-Za-z]{3,9}\s+\d{4})"
            };

            foreach (var pattern in patterns)
            {
                var match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                var dateText =
                    match.Groups[1].Value.Trim();

                var formats = new[]
                {
                    "dd/MM/yyyy",
                    "d/M/yyyy",
                    "dd-MM-yyyy",
                    "d-M-yyyy",
                    "yyyy/MM/dd",
                    "yyyy-MM-dd",
                    "MMMM d, yyyy",
                    "MMMM d yyyy",
                    "MMM d, yyyy",
                    "MMM d yyyy",
                    "d MMMM yyyy",
                    "d MMM yyyy"
                };

                if (DateTime.TryParseExact(
                    dateText,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var exactDate))
                {
                    return exactDate;
                }

                if (DateTime.TryParse(
                    dateText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsedDate))
                {
                    return parsedDate;
                }
            }

            return null;
        }

        // ============================================================
        // AMOUNT
        // ============================================================

        private decimal? ExtractAmount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var patterns = new[]
            {
                @"\b(?:amount|subtotal|sub\s*total|net\s*amount)\s*[:\-]?\s*(R|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\b(?:amount\s+due|balance\s+due)\s*[:\-]?\s*(R|\$|€|£)?\s*([0-9][0-9,\.\s]*)"
            };

            foreach (var pattern in patterns)
            {
                var match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                var value =
                    match.Groups[2].Value.Trim();

                var parsed =
                    ParseMoney(value);

                if (parsed.HasValue)
                {
                    return parsed;
                }
            }

            return null;
        }

        // ============================================================
        // VAT
        // ============================================================

        private decimal? ExtractVAT(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var patterns = new[]
            {
                @"\bVAT\s*[:\-]?\s*(R|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\b(?:VAT\s+amount|tax\s+amount|sales\s+tax)\s*[:\-]?\s*(R|\$|€|£)?\s*([0-9][0-9,\.\s]*)"
            };

            foreach (var pattern in patterns)
            {
                var match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                var value =
                    match.Groups[2].Value.Trim();

                var parsed =
                    ParseMoney(value);

                if (parsed.HasValue)
                {
                    return parsed;
                }
            }

            return null;
        }

        // ============================================================
        // TOTAL AMOUNT
        // ============================================================

        private decimal? ExtractTotalAmount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var patterns = new[]
            {
                @"\bgrand\s+total\s*[:\-]?\s*(R|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\btotal\s+amount\s*[:\-]?\s*(R|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\btotal\s+due\s*[:\-]?\s*(R|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\btotal\s*[:\-]?\s*(R|\$|€|£)?\s*([0-9][0-9,\.\s]*)"
            };

            foreach (var pattern in patterns)
            {
                var match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                var value =
                    match.Groups[2].Value.Trim();

                var parsed =
                    ParseMoney(value);

                if (parsed.HasValue)
                {
                    return parsed;
                }
            }

            return null;
        }

        // ============================================================
        // GENERIC MONEY EXTRACTION
        // ============================================================

        private decimal? ExtractMoneyValue(
            string text,
            string labelPattern)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var pattern =
                $@"\b(?:{labelPattern})\s*[:\-]?\s*(?:R|\$|€|£)?\s*([0-9][0-9,\.\s]*)";

            var match =
                Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                return null;
            }

            return ParseMoney(
                match.Groups[1].Value);
        }

        // ============================================================
        // PARSE MONEY
        // ============================================================

        private decimal? ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var cleaned =
                value.Trim();

            cleaned =
                Regex.Replace(
                    cleaned,
                    @"[^\d,\.\-]",
                    "");

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return null;
            }

            // Handle common South African / international formats.
            //
            // Examples:
            // 1,234.56 -> 1234.56
            // 1 234.56 -> 1234.56
            // 1234.56  -> 1234.56
            // 1.234,56 -> 1234.56

            cleaned =
                cleaned.Replace(
                    " ",
                    "");

            if (cleaned.Contains(',') &&
                cleaned.Contains('.'))
            {
                var lastComma =
                    cleaned.LastIndexOf(',');

                var lastDot =
                    cleaned.LastIndexOf('.');

                if (lastComma > lastDot)
                {
                    // European format:
                    // 1.234,56
                    cleaned =
                        cleaned.Replace(
                            ".",
                            "");

                    cleaned =
                        cleaned.Replace(
                            ",",
                            ".");
                }
                else
                {
                    // Standard format:
                    // 1,234.56
                    cleaned =
                        cleaned.Replace(
                            ",",
                            "");
                }
            }
            else if (cleaned.Contains(','))
            {
                var commaParts =
                    cleaned.Split(',');

                if (commaParts.Length == 2 &&
                    commaParts[1].Length == 2)
                {
                    // Example:
                    // 1234,56
                    cleaned =
                        cleaned.Replace(
                            ",",
                            ".");
                }
                else
                {
                    // Example:
                    // 1,234
                    cleaned =
                        cleaned.Replace(
                            ",",
                            "");
                }
            }

            if (decimal.TryParse(
                cleaned,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var amount))
            {
                return amount;
            }

            return null;
        }
    }

    // ================================================================
    // STRING EXTENSIONS
    // ================================================================

    public static class StringExtensions
    {
        public static bool HasValue(
            this string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}