using System.Globalization;
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
                return CreateFailedResult(documentId);
            }

            var normalizedText = NormalizeText(extractedText);

            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return CreateFailedResult(documentId);
            }

            var documentType = ExtractDocumentType(normalizedText);

            var invoiceNumber = ExtractInvoiceNumber(normalizedText);

            var vendor = ExtractVendor(normalizedText);

            var invoiceDate = ExtractDate(normalizedText);

            var amount = ExtractAmount(normalizedText);

            var vat = ExtractVAT(normalizedText);

            var totalAmount = ExtractTotalAmount(normalizedText);

            // --------------------------------------------------------
            // FALLBACK CALCULATION
            // --------------------------------------------------------

            if (amount == null &&
                totalAmount != null &&
                vat != null &&
                totalAmount.Value >= vat.Value)
            {
                amount = totalAmount.Value - vat.Value;
            }

            // --------------------------------------------------------
            // DOCUMENT VALIDATION
            // --------------------------------------------------------

            var hasUsefulInvoiceData =
                invoiceNumber.HasValue() ||
                vendor.HasValue() ||
                invoiceDate != null ||
                amount != null ||
                vat != null ||
                totalAmount != null;

            var extractionStatus =
                documentType == "Invoice" ||
                documentType == "Credit Note"
                    ? "Completed"
                    : hasUsefulInvoiceData
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
        // FAILED RESULT
        // ============================================================

        private InvoiceData CreateFailedResult(int documentId)
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

        // ============================================================
        // NORMALIZE OCR TEXT
        // ============================================================

        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text;

            normalized = normalized
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            normalized = normalized.Replace("\t", " ");

            normalized = Regex.Replace(
                normalized,
                @"[ \u00A0]+",
                " ");

            normalized = Regex.Replace(
                normalized,
                @"\n[ \t]*\n[ \t]*\n+",
                "\n\n");

            normalized = Regex.Replace(
                normalized,
                @"\s+([,:;])",
                "$1");

            normalized = Regex.Replace(
                normalized,
                @"\s*:\s*",
                ": ");

            return normalized.Trim();
        }

        // ============================================================
        // DOCUMENT TYPE
        // ============================================================

        private string? ExtractDocumentType(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // --------------------------------------------------------
            // CREDIT NOTE
            // --------------------------------------------------------

            var creditNoteTitle = Regex.IsMatch(
                text,
                @"\b(?:credit\s+note|credit\s+memo|credit\s+invoice)\b",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            if (creditNoteTitle)
            {
                var creditEvidence = 0;

                if (Regex.IsMatch(
                    text,
                    @"\b(?:credit\s+(?:note|memo)|credit)\s*(?:number|no\.?|#)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    creditEvidence++;
                }

                if (Regex.IsMatch(
                    text,
                    @"\b(?:original\s+invoice|invoice\s+(?:number|no\.?|#)|invoice\s+reference)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    creditEvidence++;
                }

                if (ExtractDate(text) != null)
                    creditEvidence++;

                if (ExtractTotalAmount(text) != null)
                    creditEvidence++;

                if (ExtractVAT(text) != null)
                    creditEvidence++;

                if (Regex.IsMatch(
                    text,
                    @"\b(?:reason\s+for\s+credit|refund|returned\s+goods|goods\s+returned|credited\s+amount)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    creditEvidence++;
                }

                if (creditEvidence >= 1)
                {
                    return "Credit Note";
                }
            }

            // --------------------------------------------------------
            // INVOICE
            // --------------------------------------------------------

            var invoiceTitle = Regex.IsMatch(
                text,
                @"\b(?:tax\s+invoice|invoice|inv\.?)\b",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            if (!invoiceTitle)
            {
                return null;
            }

            var evidence = 0;

            if (ExtractInvoiceNumber(text).HasValue())
                evidence++;

            if (ExtractDate(text) != null)
                evidence++;

            if (ExtractVendor(text).HasValue())
                evidence++;

            if (ExtractTotalAmount(text) != null)
                evidence++;

            if (ExtractVAT(text) != null)
                evidence++;

            if (Regex.IsMatch(
                text,
                @"\b(?:bill\s+to|billed\s+to|customer|client|buyer|sold\s+to)\b",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            {
                evidence++;
            }

            if (Regex.IsMatch(
                text,
                @"\b(?:subtotal|sub\s*total|amount\s+due|balance\s+due|payment\s+terms|due\s+date)\b",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            {
                evidence++;
            }

            if (evidence >= 3)
            {
                return "Invoice";
            }

            if (evidence >= 2 &&
                (
                    ExtractInvoiceNumber(text).HasValue() ||
                    ExtractTotalAmount(text) != null
                ))
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
                @"\b(?:invoice|inv\.?)\s*(?:number|no\.?|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,100})",

                @"\binvoice\s*#\s*[:\-]?\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,100})",

                @"\binvoice\s*[:\-]\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,100})",

                @"\binv\.?\s*[:\-]\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,100})",

                @"\binvoice\s+no\s*\.?\s*[:\-]?\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,100})",

                @"\bcredit\s*(?:note|memo)\s*(?:number|no\.?|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,100})",

                @"\blnvoice\s*(?:number|no\.?|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\/\-_\.]{2,100})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                var value = CleanExtractedValue(
                    match.Groups[1].Value);

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

        private bool IsValidInvoiceNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();

            if (value.Length < 3 ||
                value.Length > 100)
            {
                return false;
            }

            if (!Regex.IsMatch(
                value,
                @"\d",
                RegexOptions.CultureInvariant))
            {
                return false;
            }

            var invalidValues = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
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
                "n/a",
                "na",
                "vat"
            };

            if (invalidValues.Contains(value))
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

                @"^\s*(?:bill\s+from|billed\s+from)\s*[:\-]\s*(.+)$",

                @"^\s*(?:seller\s+name)\s*[:\-]\s*(.+)$",

                @"^\s*(?:supplier\s+name)\s*[:\-]\s*(.+)$"
            };

            foreach (var line in lines)
            {
                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(
                        line,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant);

                    if (!match.Success)
                    {
                        continue;
                    }

                    var vendor = CleanVendor(
                        match.Groups[1].Value);

                    if (IsValidVendor(vendor))
                    {
                        return vendor;
                    }
                }
            }

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.Length < 3)
                {
                    continue;
                }

                if (Regex.IsMatch(
                    trimmed,
                    @"\b(?:Pty|Ltd|Limited|Inc|Incorporated|LLC|CC|Corporation|Corp)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    var vendor = CleanVendor(trimmed);

                    if (IsValidVendor(vendor))
                    {
                        return vendor;
                    }
                }
            }

            var firstLines = lines
                .Take(Math.Min(8, lines.Count))
                .ToList();

            foreach (var line in firstLines)
            {
                var candidate = CleanVendor(line);

                if (!IsValidVendor(candidate))
                {
                    continue;
                }

                if (LooksLikeInvoiceHeading(candidate))
                {
                    continue;
                }

                if (ContainsFinancialInformation(candidate))
                {
                    continue;
                }

                return candidate;
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

            var cleaned = value.Trim();

            cleaned = Regex.Replace(
                cleaned,
                @"\s{2,}",
                " ");

            cleaned = cleaned.Trim(
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

            var invalidValues = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
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
                "na",
                "none",
                "date",
                "invoice number",
                "invoice no",
                "invoice total",
                "subtotal",
                "total",
                "vat"
            };

            if (invalidValues.Contains(value.Trim()))
            {
                return false;
            }

            var letters = value.Count(char.IsLetter);

            if (letters < 2)
            {
                return false;
            }

            if (value.Length > 120)
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
                @"\b(?:invoice\s*)?(?:date|dated|issue\s+date|date\s+issued)\s*[:\-]?\s*(\d{1,2}[\/\-\.]\d{1,2}[\/\-\.]\d{2,4})",

                @"\b(?:invoice\s*)?(?:date|dated|issue\s+date|date\s+issued)\s*[:\-]?\s*(\d{4}[\/\-\.]\d{1,2}[\/\-\.]\d{1,2})",

                @"\b(?:invoice\s*)?(?:date|dated|issue\s+date|date\s+issued)\s*[:\-]?\s*([A-Za-z]{3,12}\s+\d{1,2},?\s+\d{4})",

                @"\b(?:invoice\s*)?(?:date|dated|issue\s+date|date\s+issued)\s*[:\-]?\s*(\d{1,2}\s+[A-Za-z]{3,12}\s+\d{4})"
            };

            var formats = new[]
            {
                "dd/MM/yyyy",
                "d/M/yyyy",

                "dd-MM-yyyy",
                "d-M-yyyy",

                "dd.MM.yyyy",
                "d.M.yyyy",

                "yyyy/MM/dd",
                "yyyy-MM-dd",
                "yyyy.MM.dd",

                "MMMM d, yyyy",
                "MMMM d yyyy",

                "MMM d, yyyy",
                "MMM d yyyy",

                "d MMMM yyyy",
                "d MMM yyyy"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
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
                @"\b(?:amount|subtotal|sub\s*total|net\s*amount|net)\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\b(?:amount\s+due|balance\s+due)\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\b(?:total\s+before\s+VAT|total\s+before\s+tax)\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                var parsed = ParseMoney(
                    match.Groups[1].Value);

                if (parsed != null)
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
                @"\bVAT\s*(?:amount)?\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\b(?:VAT\s+amount|tax\s+amount|sales\s+tax)\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\bVAT\s+\d{1,2}(?:\.\d+)?%\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                var parsed = ParseMoney(
                    match.Groups[1].Value);

                if (parsed != null)
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
                @"\bgrand\s+total\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\btotal\s+amount\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\btotal\s+due\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\bamount\s+due\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\bbalance\s+due\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)",

                @"\btotal\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                var parsed = ParseMoney(
                    match.Groups[1].Value);

                if (parsed != null)
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
                $@"\b(?:{labelPattern})\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([0-9][0-9,\.\s]*)";

            var match = Regex.Match(
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

            var cleaned = value.Trim();

            cleaned = Regex.Replace(
                cleaned,
                @"[^\d,\.\-]",
                "");

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return null;
            }

            cleaned = cleaned.Replace(" ", "");

            if (cleaned.Contains(',') &&
                cleaned.Contains('.'))
            {
                var lastComma =
                    cleaned.LastIndexOf(',');

                var lastDot =
                    cleaned.LastIndexOf('.');

                if (lastComma > lastDot)
                {
                    cleaned = cleaned.Replace(
                        ".",
                        "");

                    cleaned = cleaned.Replace(
                        ",",
                        ".");
                }
                else
                {
                    cleaned = cleaned.Replace(
                        ",",
                        "");
                }
            }
            else if (cleaned.Contains(','))
            {
                var parts =
                    cleaned.Split(',');

                if (parts.Length == 2 &&
                    parts[1].Length == 2)
                {
                    cleaned = cleaned.Replace(
                        ",",
                        ".");
                }
                else
                {
                    cleaned = cleaned.Replace(
                        ",",
                        "");
                }
            }
            else if (cleaned.Contains('.'))
            {
                var parts =
                    cleaned.Split('.');

                if (parts.Length > 2)
                {
                    cleaned = cleaned.Replace(
                        ".",
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

        // ============================================================
        // CLEAN EXTRACTED VALUE
        // ============================================================

        private string CleanExtractedValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var cleaned = value.Trim();

            cleaned = Regex.Replace(
                cleaned,
                @"\s{2,}",
                " ");

            cleaned = cleaned.Trim(
                ':',
                '-',
                '|',
                ' ',
                '.',
                ',');

            return cleaned;
        }

        // ============================================================
        // INVOICE HEADING CHECK
        // ============================================================

        private bool LooksLikeInvoiceHeading(string value)
        {
            return Regex.IsMatch(
                value,
                @"^(?:invoice|tax invoice|credit note|invoice number|invoice date|bill to|ship to|customer|vendor|supplier)$",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);
        }

        // ============================================================
        // FINANCIAL INFORMATION CHECK
        // ============================================================

        private bool ContainsFinancialInformation(string value)
        {
            if (Regex.IsMatch(
                value,
                @"(?:R|ZAR|\$|€|£)\s*\d",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            {
                return true;
            }

            if (Regex.IsMatch(
                value,
                @"\b(?:VAT|subtotal|total|amount|balance|invoice number|invoice date)\b",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            {
                return true;
            }

            return false;
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



