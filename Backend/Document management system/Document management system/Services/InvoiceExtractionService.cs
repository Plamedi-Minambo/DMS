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
                return new InvoiceData
                {
                    DocumentId = documentId,
                    ExtractionStatus = "Failed",
                    ExtractedAt = DateTime.UtcNow
                };
            }

            var text = NormalizeText(extractedText);

            var invoiceNumber = ExtractInvoiceNumber(text);
            var vendor = ExtractVendor(text);
            var invoiceDate = ExtractInvoiceDate(text);

            var amount = ExtractAmount(text);
            var vat = ExtractVAT(text);
            var totalAmount = ExtractTotalAmount(text);

            // --------------------------------------------------------
            // Financial fallback calculations
            // --------------------------------------------------------

            if (!amount.HasValue &&
                totalAmount.HasValue &&
                vat.HasValue)
            {
                amount = totalAmount.Value - vat.Value;
            }

            if (!totalAmount.HasValue &&
                amount.HasValue &&
                vat.HasValue)
            {
                totalAmount = amount.Value + vat.Value;
            }

            return new InvoiceData
            {
                DocumentId = documentId,

                DocumentType = DetermineDocumentType(text),

                InvoiceNumber = invoiceNumber,

                Vendor = vendor,

                InvoiceDate = invoiceDate,

                Amount = amount,

                VAT = vat,

                TotalAmount = totalAmount,

                ExtractedAt = DateTime.UtcNow,

                ExtractionStatus =
                    !string.IsNullOrWhiteSpace(invoiceNumber) ||
                    !string.IsNullOrWhiteSpace(vendor) ||
                    invoiceDate.HasValue ||
                    amount.HasValue ||
                    vat.HasValue ||
                    totalAmount.HasValue
                        ? "Completed"
                        : "Failed"
            };
        }

        // ============================================================
        // NORMALIZE TEXT
        // ============================================================

        private static string NormalizeText(string text)
        {
            text = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace('\u00A0', ' ');

            var lines = text
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var cleanedLines = new List<string>();

            foreach (var line in lines)
            {
                var cleaned = line;

                // Common OCR/PDF extraction errors.
                cleaned = cleaned
                    .Replace(
                        "T0TAL",
                        "TOTAL",
                        StringComparison.OrdinalIgnoreCase)

                    .Replace(
                        "SUBT0TAL",
                        "SUBTOTAL",
                        StringComparison.OrdinalIgnoreCase)

                    .Replace(
                        "AM0UNT",
                        "AMOUNT",
                        StringComparison.OrdinalIgnoreCase)

                    .Replace(
                        "V4T",
                        "VAT",
                        StringComparison.OrdinalIgnoreCase)

                    .Replace(
                        "VAI",
                        "VAT",
                        StringComparison.OrdinalIgnoreCase)

                    .Replace(
                        "INVOlCE",
                        "INVOICE",
                        StringComparison.OrdinalIgnoreCase)

                    .Replace(
                        "lnvoice",
                        "INVOICE",
                        StringComparison.OrdinalIgnoreCase);

                cleaned = Regex.Replace(
                    cleaned,
                    @"[ \t]+",
                    " ");

                cleanedLines.Add(cleaned.Trim());
            }

            return string.Join(
                Environment.NewLine,
                cleanedLines);
        }

        // ============================================================
        // DOCUMENT TYPE
        // ============================================================

        private static string DetermineDocumentType(string text)
        {
            if (Regex.IsMatch(
                text,
                @"\bCREDIT\s+NOTE\b",
                RegexOptions.IgnoreCase))
            {
                return "Credit Note";
            }

            if (Regex.IsMatch(
                text,
                @"\bINVOICE\b",
                RegexOptions.IgnoreCase))
            {
                return "Invoice";
            }

            return "Invoice";
        }

        // ============================================================
        // INVOICE NUMBER
        // ============================================================

        private static string? ExtractInvoiceNumber(string text)
        {
            var patterns = new[]
            {
                @"(?:Invoice\s*(?:Number|No\.?|#))\s*[:\-]?\s*([A-Za-z0-9][A-Za-z0-9\/\-_]*)",

                @"(?:Inv\.?\s*(?:Number|No\.?|#))\s*[:\-]?\s*([A-Za-z0-9][A-Za-z0-9\/\-_]*)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    continue;
                }

                var value =
                    match.Groups[1].Value.Trim();

                value = Regex.Replace(
                    value,
                    @"(?i)(Invoice|Date|Due|PO|BILL|TO)$",
                    "");

                value = value.Trim();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        // ============================================================
        // VENDOR
        // ============================================================

        private static string? ExtractVendor(string text)
        {
            var lines = GetLines(text);

            // --------------------------------------------------------
            // The vendor is normally the company name at the top
            // of the invoice, before the invoice heading.
            //
            // Example:
            //
            // Aurora Digital Solutions
            // 45 Ridge Road, Durban, 4001
            // 031 555 0192 | hello@auroradigital.co.za
            // VAT No: 4650198237
            // INVOICE
            //
            // Therefore the first meaningful company-style line
            // before INVOICE is treated as the vendor.
            // --------------------------------------------------------

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();

                if (Regex.IsMatch(
                    line,
                    @"^INVOICE$",
                    RegexOptions.IgnoreCase))
                {
                    break;
                }

                // Ignore obvious address/contact lines.
                if (LooksLikeAddress(line) ||
                    LooksLikeEmailOrPhone(line) ||
                    LooksLikeVatRegistration(line))
                {
                    continue;
                }

                // Ignore common headings.
                if (Regex.IsMatch(
                    line,
                    @"^(INVOICE|TAX\s+INVOICE|CREDIT\s+NOTE)$",
                    RegexOptions.IgnoreCase))
                {
                    continue;
                }

                // The first meaningful line before INVOICE
                // is treated as the vendor.
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            // --------------------------------------------------------
            // Fallback:
            // Look for a company name immediately before the
            // address/contact information.
            // --------------------------------------------------------

            var invoiceIndex = lines.FindIndex(
                line =>
                    Regex.IsMatch(
                        line,
                        @"^INVOICE$",
                        RegexOptions.IgnoreCase));

            if (invoiceIndex > 0)
            {
                for (int i = invoiceIndex - 1; i >= 0; i--)
                {
                    var candidate = lines[i].Trim();

                    if (LooksLikeAddress(candidate) ||
                        LooksLikeEmailOrPhone(candidate) ||
                        LooksLikeVatRegistration(candidate))
                    {
                        continue;
                    }

                    if (!Regex.IsMatch(
                        candidate,
                        @"^(INVOICE|TAX\s+INVOICE|CREDIT\s+NOTE)$",
                        RegexOptions.IgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static bool LooksLikeAddress(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Regex.IsMatch(
                value,
                @"\b\d{1,5}\b.*(?:road|rd|street|st|avenue|ave|park|drive|dr|lane|ln|way|close|crescent|cres)\b",
                RegexOptions.IgnoreCase)
                ||
                Regex.IsMatch(
                    value,
                    @"\b\d{4}\b$");
        }

        private static bool LooksLikeEmailOrPhone(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Contains("@") ||
                   Regex.IsMatch(
                       value,
                       @"\b\d{3}[\s\-]?\d{3}[\s\-]?\d{4}\b");
        }

        private static bool LooksLikeVatRegistration(string value)
        {
            return Regex.IsMatch(
                value,
                @"^\s*VAT\s*(?:NO|NUMBER|REGISTRATION|REGISTRATION\s+NUMBER)\b",
                RegexOptions.IgnoreCase);
        }

        // ============================================================
        // INVOICE DATE
        // ============================================================

        private static DateTime? ExtractInvoiceDate(string text)
        {
            var patterns = new[]
            {
                @"Invoice\s*Date\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",

                @"Invoice\s*Date\s*[:\-]?\s*(\d{1,2}\s+[A-Za-z]{3,9}\s+\d{2,4})",

                @"Date\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",

                @"Date\s*[:\-]?\s*(\d{1,2}\s+[A-Za-z]{3,9}\s+\d{2,4})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    continue;
                }

                var value =
                    match.Groups[1].Value.Trim();

                var formats = new[]
                {
                    "dd/MM/yyyy",
                    "d/M/yyyy",
                    "dd-MM-yyyy",
                    "d-M-yyyy",

                    "dd/MM/yy",
                    "d/M/yy",
                    "dd-MM-yy",
                    "d-M-yy",

                    "dd MMMM yyyy",
                    "d MMMM yyyy",
                    "dd MMM yyyy",
                    "d MMM yyyy"
                };

                if (DateTime.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
                {
                    return date;
                }

                if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out date))
                {
                    return date;
                }
            }

            return null;
        }

        // ============================================================
        // AMOUNT BEFORE VAT
        // ============================================================

        private static decimal? ExtractAmount(string text)
        {
            var lines = GetLines(text);

            decimal? subtotal = null;
            decimal? discount = null;

            foreach (var line in lines)
            {
                if (Regex.IsMatch(
                    line,
                    @"^\s*SUBTOTAL\b",
                    RegexOptions.IgnoreCase))
                {
                    subtotal =
                        ExtractFirstMoneyValue(line);
                }

                if (Regex.IsMatch(
                    line,
                    @"^\s*DISCOUNT\b",
                    RegexOptions.IgnoreCase))
                {
                    discount =
                        ExtractFirstMoneyValue(line);
                }
            }

            // --------------------------------------------------------
            // Subtotal - Discount
            //
            // 30,850.00 - 1,000.00
            // = 29,850.00
            // --------------------------------------------------------

            if (subtotal.HasValue)
            {
                if (discount.HasValue)
                {
                    return subtotal.Value - discount.Value;
                }

                return subtotal.Value;
            }

            // --------------------------------------------------------
            // Explicit amount labels
            // --------------------------------------------------------

            var amountPatterns = new[]
            {
                @"^\s*(?:NET\s+AMOUNT|AMOUNT\s+BEFORE\s+VAT|TOTAL\s+BEFORE\s+VAT|TOTAL\s+BEFORE\s+TAX)\s*[:\-]?\s*(.+)$",

                @"^\s*AMOUNT\s*[:\-]?\s*(.+)$"
            };

            foreach (var line in lines)
            {
                foreach (var pattern in amountPatterns)
                {
                    var match = Regex.Match(
                        line,
                        pattern,
                        RegexOptions.IgnoreCase);

                    if (!match.Success)
                    {
                        continue;
                    }

                    var value =
                        ExtractFirstMoneyValue(
                            match.Groups[1].Value);

                    if (value.HasValue)
                    {
                        return value.Value;
                    }
                }
            }

            return null;
        }

        // ============================================================
        // VAT
        // ============================================================

        private static decimal? ExtractVAT(string text)
        {
            var lines = GetLines(text);

            // --------------------------------------------------------
            // Tax (VAT 15%) 4,477.50
            // --------------------------------------------------------

            foreach (var line in lines)
            {
                if (Regex.IsMatch(
                    line,
                    @"^\s*VAT\s*(?:NO|NUMBER|REGISTRATION|REGISTRATION\s+NUMBER)\b",
                    RegexOptions.IgnoreCase))
                {
                    continue;
                }

                if (Regex.IsMatch(
                    line,
                    @"\bTAX\s*\(\s*VAT\s*\d{1,2}(?:\.\d+)?\s*%\s*\)",
                    RegexOptions.IgnoreCase))
                {
                    var value =
                        ExtractMoneyAfterPercentage(line);

                    if (value.HasValue)
                    {
                        return value.Value;
                    }
                }
            }

            // --------------------------------------------------------
            // Tax label and amount on separate lines
            // --------------------------------------------------------

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (!Regex.IsMatch(
                    line,
                    @"\bTAX\s*\(\s*VAT\s*\d{1,2}(?:\.\d+)?\s*%\s*\)",
                    RegexOptions.IgnoreCase))
                {
                    continue;
                }

                for (int j = i + 1; j < lines.Count; j++)
                {
                    var value =
                        ExtractFirstMoneyValue(lines[j]);

                    if (value.HasValue)
                    {
                        return value.Value;
                    }

                    if (Regex.IsMatch(
                        lines[j],
                        @"^(TOTAL|PAYMENT|NOTES|DISCOUNT|SUBTOTAL)",
                        RegexOptions.IgnoreCase))
                    {
                        break;
                    }
                }
            }

            // --------------------------------------------------------
            // Other VAT formats
            // --------------------------------------------------------

            var vatPatterns = new[]
            {
                @"^\s*VAT\s*(?:AMOUNT)?\s*[:\-]?\s*(.+)$",

                @"^\s*TAX\s*(?:AMOUNT)?\s*[:\-]?\s*(.+)$",

                @"^\s*SALES\s+TAX\s*[:\-]?\s*(.+)$"
            };

            foreach (var line in lines)
            {
                if (Regex.IsMatch(
                    line,
                    @"^\s*VAT\s*(?:NO|NUMBER|REGISTRATION)",
                    RegexOptions.IgnoreCase))
                {
                    continue;
                }

                foreach (var pattern in vatPatterns)
                {
                    var match = Regex.Match(
                        line,
                        pattern,
                        RegexOptions.IgnoreCase);

                    if (!match.Success)
                    {
                        continue;
                    }

                    var value =
                        ExtractFirstMoneyValue(
                            match.Groups[1].Value);

                    if (value.HasValue)
                    {
                        return value.Value;
                    }
                }
            }

            return null;
        }

        // ============================================================
        // TOTAL
        // ============================================================

        private static decimal? ExtractTotalAmount(string text)
        {
            var lines = GetLines(text);

            var priorityPatterns = new[]
            {
                @"^\s*GRAND\s+TOTAL\s*[:\-]?\s*(.+)$",

                @"^\s*TOTAL\s+AMOUNT\s*[:\-]?\s*(.+)$",

                @"^\s*TOTAL\s+DUE\s*[:\-]?\s*(.+)$",

                @"^\s*AMOUNT\s+DUE\s*[:\-]?\s*(.+)$",

                @"^\s*BALANCE\s+DUE\s*[:\-]?\s*(.+)$"
            };

            foreach (var line in lines)
            {
                foreach (var pattern in priorityPatterns)
                {
                    var match = Regex.Match(
                        line,
                        pattern,
                        RegexOptions.IgnoreCase);

                    if (!match.Success)
                    {
                        continue;
                    }

                    var value =
                        ExtractFirstMoneyValue(
                            match.Groups[1].Value);

                    if (value.HasValue)
                    {
                        return value.Value;
                    }
                }
            }

            // --------------------------------------------------------
            // Generic TOTAL
            // --------------------------------------------------------

            foreach (var line in lines)
            {
                if (Regex.IsMatch(
                    line,
                    @"^\s*TOTAL\s*$",
                    RegexOptions.IgnoreCase))
                {
                    continue;
                }

                if (Regex.IsMatch(
                    line,
                    @"^\s*TOTAL\s*[:\-]",
                    RegexOptions.IgnoreCase))
                {
                    var value =
                        ExtractFirstMoneyValue(line);

                    if (value.HasValue)
                    {
                        return value.Value;
                    }
                }
            }

            return null;
        }

        // ============================================================
        // MONEY EXTRACTION
        // ============================================================

        private static decimal? ExtractFirstMoneyValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Remove percentage values first.
            var cleaned =
                Regex.Replace(
                    text,
                    @"\d+(?:[.,]\d+)?\s*%",
                    "",
                    RegexOptions.IgnoreCase);

            // --------------------------------------------------------
            // Currency + number
            // --------------------------------------------------------

            var currencyMatch =
                Regex.Match(
                    cleaned,
                    @"(?:R|ZAR|\$|€|£)\s*([-+]?\d[\d\s,\.]*\d|\d+)",
                    RegexOptions.IgnoreCase);

            if (currencyMatch.Success)
            {
                return ParseMoney(
                    currencyMatch.Groups[1].Value);
            }

            // --------------------------------------------------------
            // Decimal / grouped monetary number
            // --------------------------------------------------------

            var numberMatches =
                Regex.Matches(
                    cleaned,
                    @"[-+]?\d[\d\s]*(?:[.,]\d{2})(?!\s*%)");

            foreach (Match match in numberMatches)
            {
                var value =
                    ParseMoney(match.Value);

                if (value.HasValue)
                {
                    return value.Value;
                }
            }

            // --------------------------------------------------------
            // Integer fallback
            // --------------------------------------------------------

            var integerMatches =
                Regex.Matches(
                    cleaned,
                    @"(?<![\d\-])[-+]?\d[\d\s]*(?![\d\-])");

            foreach (Match match in integerMatches)
            {
                var raw =
                    match.Value.Trim();

                if (raw.Length == 0)
                {
                    continue;
                }

                // Avoid interpreting long IDs as money.
                if (raw.Replace(" ", "").Length >= 5 &&
                    !raw.Contains(",") &&
                    !raw.Contains("."))
                {
                    continue;
                }

                var value =
                    ParseMoney(raw);

                if (value.HasValue)
                {
                    return value.Value;
                }
            }

            return null;
        }

        // ============================================================
        // MONEY AFTER VAT PERCENTAGE
        // ============================================================

        private static decimal? ExtractMoneyAfterPercentage(
            string line)
        {
            var percentageMatch =
                Regex.Match(
                    line,
                    @"\d{1,2}(?:\.\d+)?\s*%",
                    RegexOptions.IgnoreCase);

            if (!percentageMatch.Success)
            {
                return null;
            }

            var afterPercentage =
                line.Substring(
                    percentageMatch.Index +
                    percentageMatch.Length);

            return ExtractFirstMoneyValue(
                afterPercentage);
        }

        // ============================================================
        // MONEY PARSER
        // ============================================================

        private static decimal? ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value
                .Trim()
                .Replace(
                    "ZAR",
                    "",
                    StringComparison.OrdinalIgnoreCase)
                .Replace(
                    "R",
                    "",
                    StringComparison.OrdinalIgnoreCase)
                .Replace("$", "")
                .Replace("€", "")
                .Replace("£", "")
                .Replace("\u00A0", "")
                .Replace(" ", "");

            if (value.Length == 0)
            {
                return null;
            }

            // --------------------------------------------------------
            // Both comma and dot:
            //
            // 4.477,50 -> 4477.50
            // 4,477.50 -> 4477.50
            // --------------------------------------------------------

            if (value.Contains(',') &&
                value.Contains('.'))
            {
                var lastComma =
                    value.LastIndexOf(',');

                var lastDot =
                    value.LastIndexOf('.');

                if (lastComma > lastDot)
                {
                    value = value
                        .Replace(".", "")
                        .Replace(",", ".");
                }
                else
                {
                    value =
                        value.Replace(",", "");
                }
            }

            // --------------------------------------------------------
            // Only comma
            // --------------------------------------------------------

            else if (value.Contains(','))
            {
                var parts =
                    value.Split(',');

                if (parts.Length == 2 &&
                    parts[1].Length == 2)
                {
                    // 18 500,00
                    value =
                        value.Replace(",", ".");
                }
                else
                {
                    // 30,850
                    value =
                        value.Replace(",", "");
                }
            }

            // --------------------------------------------------------
            // Multiple dots
            // --------------------------------------------------------

            else if (value.Count(c => c == '.') > 1)
            {
                value =
                    value.Replace(".", "");
            }

            // --------------------------------------------------------
            // Parse final decimal value
            // --------------------------------------------------------

            if (decimal.TryParse(
                value,
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var result))
            {
                return result;
            }

            return null;
        }

        // ============================================================
        // GET LINES
        // ============================================================

        private static List<string> GetLines(string text)
        {
            return text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line))
                .ToList();
        }
    }
}
