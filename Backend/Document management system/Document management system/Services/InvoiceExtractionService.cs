using DocumentManagement.API.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DocumentManagement.API.Services
{
    public class InvoiceExtractionService
    {
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

            // Normalize the extracted PDF text first.
            var text = NormalizeText(extractedText);

            var invoiceData = new InvoiceData
            {
                DocumentId = documentId,

                DocumentType =
                    ExtractDocumentType(text),

                InvoiceNumber =
                    ExtractInvoiceNumber(text),

                Vendor =
                    ExtractVendor(text),

                InvoiceDate =
                    ExtractDate(text),

                Amount =
                    ExtractAmount(text),

                VAT =
                    ExtractVAT(text),

                TotalAmount =
                    ExtractTotalAmount(text),

                ExtractedAt =
                    DateTime.UtcNow,

                ExtractionStatus =
                    "Completed"
            };

            // If the total was not found directly,
            // calculate it from Amount + VAT.
            if (!invoiceData.TotalAmount.HasValue &&
                invoiceData.Amount.HasValue &&
                invoiceData.VAT.HasValue)
            {
                invoiceData.TotalAmount =
                    invoiceData.Amount.Value +
                    invoiceData.VAT.Value;
            }

            // If absolutely no useful information was extracted,
            // mark the extraction as failed.
            if (!invoiceData.DocumentType.HasValue() &&
                !invoiceData.InvoiceNumber.HasValue() &&
                !invoiceData.Vendor.HasValue() &&
                !invoiceData.InvoiceDate.HasValue &&
                !invoiceData.Amount.HasValue &&
                !invoiceData.VAT.HasValue &&
                !invoiceData.TotalAmount.HasValue)
            {
                invoiceData.ExtractionStatus = "Failed";
            }

            return invoiceData;
        }

        // ============================================================
        // TEXT NORMALIZATION
        // ============================================================

        private string NormalizeText(string text)
        {
            text = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\u00A0", " ");

            // Replace tabs with spaces.
            text = text.Replace("\t", " ");

            // Normalize multiple spaces.
            text = Regex.Replace(
                text,
                @"[ ]{2,}",
                " ");

            // Normalize excessive blank lines.
            text = Regex.Replace(
                text,
                @"\n{3,}",
                "\n\n");

            return text.Trim();
        }

        // ============================================================
        // DOCUMENT TYPE
        // ============================================================

        private string? ExtractDocumentType(string text)
        {
            // Credit Note gets priority.
            if (Regex.IsMatch(
                text,
                @"\bcredit\s*note\b",
                RegexOptions.IgnoreCase))
            {
                return "Credit Note";
            }

            if (Regex.IsMatch(
                text,
                @"\bcredit\s*memo\b",
                RegexOptions.IgnoreCase))
            {
                return "Credit Note";
            }

            if (Regex.IsMatch(
                text,
                @"\bcredit\b",
                RegexOptions.IgnoreCase))
            {
                return "Credit Note";
            }

            if (Regex.IsMatch(
                text,
                @"\btax\s+invoice\b",
                RegexOptions.IgnoreCase))
            {
                return "Invoice";
            }

            if (Regex.IsMatch(
                text,
                @"\binvoice\b",
                RegexOptions.IgnoreCase))
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
            var patterns = new[]
            {
        @"invoice\s*(?:number|no\.?|#)\s*[:\-#]?\s*([A-Z0-9][A-Z0-9\/\-_]*?)(?=\s*(?:vendor|supplier|company|seller|date|dated|amount|vat|tax|total|status)\b|$)",

        @"\binv\.?\s*(?:number|no\.?|#)\s*[:\-#]?\s*([A-Z0-9][A-Z0-9\/\-_]*?)(?=\s*(?:vendor|supplier|company|seller|date|dated|amount|vat|tax|total|status)\b|$)",

        @"credit\s*(?:note|memo)\s*(?:number|no\.?|#)\s*[:\-#]?\s*([A-Z0-9][A-Z0-9\/\-_]*?)(?=\s*(?:vendor|supplier|company|seller|date|dated|amount|vat|tax|total|status)\b|$)"
    };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    if (!match.Success ||
                        match.Groups.Count < 2)
                    {
                        continue;
                    }

                    var value =
                        match.Groups[1]
                            .Value
                            .Trim();

                    if (IsValidInvoiceNumber(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        private bool IsValidInvoiceNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var invalidValues = new[]
            {
                "invoice",
                "inv",
                "number",
                "no",
                "date",
                "vendor",
                "supplier",
                "amount",
                "vat",
                "tax",
                "total",
                "status",
                "credit",
                "note",
                "memo"
            };

            if (invalidValues.Any(
                x => string.Equals(
                    x,
                    value,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Invoice numbers should normally contain
            // at least one digit.
            if (!Regex.IsMatch(
                value,
                @"\d"))
            {
                return false;
            }

            // Prevent obviously invalid values.
            if (value.Length > 100)
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
            var vendorPatterns = new[]
            {
                @"vendor\s*(?:name)?\s*[:\-]?\s*(.*?)(?=\s*(?:invoice\s*(?:number|no\.?|#)|date|dated|amount|vat|tax|total|status|invoice\s*date)\s*[:\-]|$)",

                @"supplier\s*(?:name)?\s*[:\-]?\s*(.*?)(?=\s*(?:invoice\s*(?:number|no\.?|#)|date|dated|amount|vat|tax|total|status|invoice\s*date)\s*[:\-]|$)",

                @"company\s*(?:name)?\s*[:\-]?\s*(.*?)(?=\s*(?:invoice\s*(?:number|no\.?|#)|date|dated|amount|vat|tax|total|status|invoice\s*date)\s*[:\-]|$)",

                @"seller\s*(?:name)?\s*[:\-]?\s*(.*?)(?=\s*(?:invoice\s*(?:number|no\.?|#)|date|dated|amount|vat|tax|total|status|invoice\s*date)\s*[:\-]|$)"
            };

            foreach (var pattern in vendorPatterns)
            {
                var match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

                if (match.Success)
                {
                    var vendor =
                        CleanVendor(
                            match.Groups[1].Value);

                    if (IsValidVendor(vendor))
                    {
                        return vendor;
                    }
                }
            }

            // Try line-by-line extraction.
            var lines = GetLines(text);

            for (int i = 0; i < lines.Count - 1; i++)
            {
                var currentLine =
                    lines[i].Trim();

                if (Regex.IsMatch(
                    currentLine,
                    @"^(vendor|vendor\s*name|supplier|supplier\s*name|company|company\s*name|seller|seller\s*name)\s*:?\s*$",
                    RegexOptions.IgnoreCase))
                {
                    var nextLine =
                        CleanVendor(lines[i + 1]);

                    if (IsValidVendor(nextLine))
                    {
                        return nextLine;
                    }
                }
            }

            // Handle:
            // Vendor: ABC Supplies
            foreach (var line in lines.Take(30))
            {
                var match = Regex.Match(
                    line,
                    @"^(?:vendor|vendor\s*name|supplier|supplier\s*name|company|company\s*name|seller|seller\s*name)\s*[:\-]\s*(.+)$",
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var vendor =
                        CleanVendor(match.Groups[1].Value);

                    if (IsValidVendor(vendor))
                    {
                        return vendor;
                    }
                }
            }

            // Try company names containing common business suffixes.
            foreach (var line in lines.Take(30))
            {
                var cleanLine =
                    CleanVendor(line);

                if (!IsValidVendor(cleanLine))
                {
                    continue;
                }

                if (Regex.IsMatch(
                    cleanLine,
                    @"\b(?:\(pty\)\s*ltd|pty\s*ltd|ltd|limited|cc|inc|incorporated|corp|corporation|enterprises|enterprise|trading|solutions|services|group)\b",
                    RegexOptions.IgnoreCase))
                {
                    return cleanLine;
                }
            }

            // Final fallback.
            foreach (var line in lines.Take(20))
            {
                var cleanLine =
                    CleanVendor(line);

                if (IsValidVendor(cleanLine))
                {
                    return cleanLine;
                }
            }

            return null;
        }

        private List<string> GetLines(string text)
        {
            return text
                .Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        private string CleanVendor(string vendor)
        {
            if (string.IsNullOrWhiteSpace(vendor))
            {
                return string.Empty;
            }

            vendor = vendor
                .Replace("\r", " ")
                .Trim();

            var nextFieldMatch =
                Regex.Match(
                    vendor,
                    @"(?i)(?:date|dated|amount|vat|tax|total|status|invoice\s*(?:number|no\.?|#))\s*[:\-]");

            if (nextFieldMatch.Success)
            {
                vendor =
                    vendor.Substring(
                        0,
                        nextFieldMatch.Index);
            }

            vendor = Regex.Replace(
                vendor,
                @"\s{2,}",
                " ");

            return vendor.Trim(
                ' ',
                ':',
                '-',
                '\t');
        }

        private bool IsValidVendor(string? vendor)
        {
            if (string.IsNullOrWhiteSpace(vendor))
            {
                return false;
            }

            vendor = vendor.Trim();

            if (vendor.Length < 2 ||
                vendor.Length > 255)
            {
                return false;
            }

            if (Regex.IsMatch(
                vendor,
                @"^(invoice|tax invoice|credit note|credit memo|bill|receipt)$",
                RegexOptions.IgnoreCase))
            {
                return false;
            }

            if (Regex.IsMatch(
                vendor,
                @"^(invoice|invoice number|invoice no|invoice date|date|dated|vat|vat amount|tax|subtotal|sub total|amount|total|grand total|amount due|balance due|due date|number|address|telephone|tel|phone|email)$",
                RegexOptions.IgnoreCase))
            {
                return false;
            }

            if (Regex.IsMatch(
                vendor,
                @"^[\d\s\-\/.,:]+$"))
            {
                return false;
            }

            if (Regex.IsMatch(
                vendor,
                @"^(address|po box|p\.o\. box|tel|telephone|phone|email|website)\b",
                RegexOptions.IgnoreCase))
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
            var patterns = new[]
            {
                @"(?:invoice\s*)?(?:date|dated)\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",

                @"(?:invoice\s*)?(?:date|dated)\s*[:\-]?\s*(\d{4}[\/\-]\d{1,2}[\/\-]\d{1,2})",

                @"(?:invoice\s*)?(?:date|dated)\s*[:\-]?\s*(\d{1,2}\s+(?:Jan|January|Feb|February|Mar|March|Apr|April|May|Jun|June|Jul|July|Aug|August|Sep|September|Oct|October|Nov|November|Dec|December)\s+\d{4})",

                @"(?:invoice\s*)?(?:date|dated)\s*[:\-]?\s*((?:Jan|January|Feb|February|Mar|March|Apr|April|May|Jun|June|Jul|July|Aug|August|Sep|September|Oct|October|Nov|November|Dec|December)\s+\d{1,2},?\s+\d{4})"
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

                var dateText =
                    match.Groups[1]
                        .Value
                        .Trim();

                string[] formats =
                {
                    "dd/MM/yyyy",
                    "d/MM/yyyy",
                    "dd/M/yyyy",
                    "d/M/yyyy",

                    "dd-MM-yyyy",
                    "d-MM-yyyy",
                    "dd-M-yyyy",
                    "d-M-yyyy",

                    "dd/MM/yy",
                    "d/M/yy",

                    "dd-MM-yy",
                    "d-M-yy",

                    "yyyy/MM/dd",
                    "yyyy/M/dd",
                    "yyyy/MM/d",
                    "yyyy/M/d",

                    "yyyy-MM-dd",
                    "yyyy-M-dd",
                    "yyyy-MM-d",
                    "yyyy-M-d",

                    "dd MMMM yyyy",
                    "d MMMM yyyy",
                    "dd MMM yyyy",
                    "d MMM yyyy",

                    "MMMM dd yyyy",
                    "MMMM d yyyy",
                    "MMMM dd, yyyy",
                    "MMMM d, yyyy",

                    "MMM dd yyyy",
                    "MMM d yyyy",
                    "MMM dd, yyyy",
                    "MMM d, yyyy"
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(
                        dateText,
                        format,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out var date))
                    {
                        return date;
                    }
                }
            }

            return null;
        }

        // ============================================================
        // AMOUNT
        // ============================================================

        private decimal? ExtractAmount(string text)
        {
            var patterns = new[]
            {
                @"amount\s+before\s+vat\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"amount\s+(?:excluding|excl(?:uding)?\.?)\s+vat\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"net\s+amount\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"sub\s*total\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"subtotal\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"total\s+(?:excluding|excl(?:uding)?\.?)\s+vat\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"(?:^|\n)\s*amount\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"amount\s+before\s+vat\s+(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"net\s+amount\s+(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"subtotal\s+(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"amount\s*[:\-]\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)(?=\s*(?:vat|tax|total|status|$))"
            };

            return ExtractMoneyValue(
                text,
                patterns);
        }

        // ============================================================
        // VAT
        // ============================================================

        private decimal? ExtractVAT(string text)
        {
            var patterns = new[]
            {
                @"vat\s*(?:amount)?\s*(?:\(?\s*\d+(?:[.,]\d+)?\s*%\s*\)?)?\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"tax\s*(?:amount)?\s*(?:\(?\s*\d+(?:[.,]\d+)?\s*%\s*\)?)?\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)"
            };

            return ExtractMoneyValue(
                text,
                patterns);
        }

        // ============================================================
        // TOTAL
        // ============================================================

        private decimal? ExtractTotalAmount(string text)
        {
            var patterns = new[]
            {
                @"grand\s+total\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"total\s+amount\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"amount\s+due\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"balance\s+due\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"total\s+due\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)",

                @"(?:^|\n|[^\w])total\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)(?=\s*(?:status|$))",

                @"total\s*[:\-]\s*(?:R|ZAR)?\s*([0-9][0-9\s,.]*)(?=\s*(?:status|invoice|date|amount|vat|$))"
            };

            return ExtractMoneyValue(
                text,
                patterns);
        }

        // ============================================================
        // MONEY EXTRACTION
        // ============================================================

        private decimal? ExtractMoneyValue(
            string text,
            string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                var matches =
                    Regex.Matches(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    if (!match.Success ||
                        match.Groups.Count < 2)
                    {
                        continue;
                    }

                    var rawValue =
                        match.Groups[1]
                            .Value
                            .Trim();

                    var value =
                        ParseMoney(rawValue);

                    if (value.HasValue &&
                        value.Value >= 0)
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        // ============================================================
        // MONEY PARSER
        // ============================================================

        private decimal? ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value
                .Trim()
                .Replace("\u00A0", " ");

            // Remove ZAR.
            value = Regex.Replace(
                value,
                @"\bZAR\b",
                "",
                RegexOptions.IgnoreCase);

            // Remove currency symbol.
            value = value
                .Replace("R", "")
                .Replace("r", "")
                .Trim();

            // Remove spaces.
            value = value.Replace(" ", "");

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // Both comma and period exist.
            if (value.Contains(",") &&
                value.Contains("."))
            {
                var lastComma =
                    value.LastIndexOf(',');

                var lastDot =
                    value.LastIndexOf('.');

                if (lastDot > lastComma)
                {
                    // Example:
                    // 1,000.50
                    value =
                        value.Replace(",", "");
                }
                else
                {
                    // Example:
                    // 1.000,50
                    value =
                        value.Replace(".", "");

                    value =
                        value.Replace(",", ".");
                }
            }
            else if (value.Contains(","))
            {
                var lastComma =
                    value.LastIndexOf(',');

                var digitsAfterComma =
                    value.Length -
                    lastComma -
                    1;

                if (digitsAfterComma == 2)
                {
                    // Example:
                    // 1000,50
                    value =
                        value.Replace(",", ".");
                }
                else
                {
                    // Example:
                    // 1,000
                    value =
                        value.Replace(",", "");
                }
            }

            // Remove anything that isn't a number,
            // decimal point or minus sign.
            value = Regex.Replace(
                value,
                @"[^\d.\-]",
                "");

            if (decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var result))
            {
                return result;
            }

            return null;
        }
    }

    // ================================================================
    // STRING EXTENSION HELPERS
    // ================================================================

    internal static class StringExtensions
    {
        public static bool HasValue(
            this string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}