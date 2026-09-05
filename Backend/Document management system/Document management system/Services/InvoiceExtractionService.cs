
using System.Globalization;
using System.Text.RegularExpressions;
using DocumentManagement.API.Models;

namespace DocumentManagement.API.Services
{
    public class InvoiceExtractionService
    {
        public InvoiceData ExtractInvoiceData(
            int documentId,
            string extractedText)
        {
            var result = new InvoiceData
            {
                DocumentId = documentId,
                ExtractedAt = DateTime.UtcNow,
                ExtractionStatus = "Pending"
            };

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                result.ExtractionStatus = "Failed";
                return result;
            }

            // =========================================================
            // NORMALISE TEXT
            // =========================================================

            string text = NormalizeText(extractedText);

            // =========================================================
            // DOCUMENT TYPE
            // =========================================================

            result.DocumentType =
                DetermineDocumentType(text);

            // =========================================================
            // INVOICE NUMBER
            // =========================================================

            result.InvoiceNumber =
                ExtractInvoiceNumber(text);

            // =========================================================
            // VENDOR
            // =========================================================

            result.Vendor =
                ExtractVendor(text);

            // =========================================================
            // INVOICE DATE
            // =========================================================

            result.InvoiceDate =
                ExtractInvoiceDate(text);

            // =========================================================
            // SUBTOTAL
            // =========================================================

            decimal? subtotal =
                ExtractSubtotal(text);

            // =========================================================
            // DISCOUNT
            // =========================================================

            decimal? discount =
                ExtractDiscount(text);

            // =========================================================
            // AMOUNT BEFORE VAT
            // =========================================================

            if (subtotal.HasValue)
            {
                result.Amount =
                    subtotal.Value - (discount ?? 0m);
            }
            else
            {
                result.Amount =
                    ExtractAmount(text);
            }

            // =========================================================
            // VAT
            // =========================================================

            result.VAT =
                ExtractVAT(text);

            // =========================================================
            // TOTAL
            // =========================================================

            result.TotalAmount =
                ExtractTotalAmount(text);

            // =========================================================
            // FINANCIAL FALLBACKS
            // =========================================================

            if (!result.TotalAmount.HasValue &&
                result.Amount.HasValue &&
                result.VAT.HasValue)
            {
                result.TotalAmount =
                    result.Amount.Value +
                    result.VAT.Value;
            }

            if (!result.Amount.HasValue &&
                result.TotalAmount.HasValue &&
                result.VAT.HasValue)
            {
                result.Amount =
                    result.TotalAmount.Value -
                    result.VAT.Value;
            }

            // =========================================================
            // EXTRACTION STATUS
            // =========================================================

            if (result.DocumentType == "Unknown")
            {
                result.ExtractionStatus = "Failed";
            }
            else if (
                !string.IsNullOrWhiteSpace(result.InvoiceNumber) ||
                !string.IsNullOrWhiteSpace(result.Vendor) ||
                result.InvoiceDate.HasValue ||
                result.Amount.HasValue ||
                result.VAT.HasValue ||
                result.TotalAmount.HasValue)
            {
                result.ExtractionStatus = "Completed";
            }
            else
            {
                result.ExtractionStatus = "Failed";
            }

            return result;
        }

        // =============================================================
        // NORMALISE TEXT
        // =============================================================

        private string NormalizeText(string text)
        {
            text = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            // Remove tabs
            text = text.Replace("\t", " ");

            // Replace non-breaking spaces
            text = text.Replace('\u00A0', ' ');

            // Common OCR mistakes
            text = Regex.Replace(
                text,
                @"T0TAL",
                "TOTAL",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"SUBT0TAL",
                "SUBTOTAL",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"AM0UNT",
                "AMOUNT",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"V4T|VAI",
                "VAT",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"INVOlCE|lnvoice",
                "INVOICE",
                RegexOptions.IgnoreCase);

            // Clean repeated spaces but preserve new lines
            text = Regex.Replace(
                text,
                @"[ ]{2,}",
                " ");

            return text.Trim();
        }

        // =============================================================
        // DOCUMENT TYPE
        // =============================================================

        private string DetermineDocumentType(string text)
        {
            // Credit Note
            if (Regex.IsMatch(
                text,
                @"\bCREDIT\s+NOTE\b|\bCREDIT\s+MEMO\b",
                RegexOptions.IgnoreCase))
            {
                return "Credit Note";
            }

            // Invoice
            if (Regex.IsMatch(
                text,
                @"\bTAX\s+INVOICE\b|\bINVOICE\b",
                RegexOptions.IgnoreCase))
            {
                return "Invoice";
            }

            // IMPORTANT:
            // Do not automatically classify unknown documents
            // as invoices.
            return "Unknown";
        }

        // =============================================================
        // INVOICE NUMBER
        // =============================================================

        private string? ExtractInvoiceNumber(string text)
        {
            string[] patterns =
            {
                @"\bInvoice\s*(?:Number|No|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)",

                @"\bInv\s*(?:Number|No|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)"
            };

            foreach (string pattern in patterns)
            {
                Match match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    return match.Groups[1]
                        .Value
                        .Trim();
                }
            }

            return null;
        }

        // =============================================================
        // VENDOR
        // =============================================================

        private string? ExtractVendor(string text)
        {
            var lines = text
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (lines.Count == 0)
                return null;

            // Find INVOICE heading
            int invoiceIndex =
                lines.FindIndex(x =>
                    Regex.IsMatch(
                        x,
                        @"^(?:TAX\s+)?INVOICE$",
                        RegexOptions.IgnoreCase));

            // Only inspect the area before INVOICE
            int limit =
                invoiceIndex >= 0
                    ? invoiceIndex
                    : Math.Min(lines.Count, 10);

            /*
             * Typical invoice structure:
             *
             * Aurora Digital Solutions       <-- VENDOR
             * 45 Ridge Road, Durban, 4001
             * 031 555 0192 | email
             * VAT No: 4650198237
             * INVOICE
             */

            for (int i = 0; i < limit; i++)
            {
                string line = lines[i];

                if (IsValidVendorLine(line))
                {
                    return line;
                }
            }

            return null;
        }

        private bool IsValidVendorLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string upper =
                line.ToUpperInvariant();

            // ---------------------------------------------------------
            // Ignore headings
            // ---------------------------------------------------------

            string[] ignored =
            {
                "INVOICE",
                "TAX INVOICE",
                "BILL TO",
                "SHIP TO",
                "PAYMENT TERMS",
                "PAYMENT METHOD",
                "NOTES"
            };

            if (ignored.Contains(upper))
                return false;

            // ---------------------------------------------------------
            // Ignore VAT registration number
            // ---------------------------------------------------------

            if (Regex.IsMatch(
                line,
                @"^\s*VAT\s*(?:NO|NUMBER|REGISTRATION)?\s*[:\-]",
                RegexOptions.IgnoreCase))
            {
                return false;
            }

            // ---------------------------------------------------------
            // Ignore email
            // ---------------------------------------------------------

            if (line.Contains("@"))
                return false;

            // ---------------------------------------------------------
            // Ignore telephone numbers
            // ---------------------------------------------------------

            if (Regex.IsMatch(
                line,
                @"(?:\+27|0)\d[\d\s\-]{7,}",
                RegexOptions.IgnoreCase))
            {
                return false;
            }

            // ---------------------------------------------------------
            // Ignore address lines
            // ---------------------------------------------------------

            if (Regex.IsMatch(
                upper,
                @"^\d+\s+.*\b(ROAD|RD|STREET|ST|AVENUE|AVE|DRIVE|DR|PARK)\b",
                RegexOptions.IgnoreCase))
            {
                return false;
            }

            // ---------------------------------------------------------
            // Ignore lines that are only numbers
            // ---------------------------------------------------------

            if (Regex.IsMatch(
                line,
                @"^\s*[\d\s,\.\-]+\s*$"))
            {
                return false;
            }

            // ---------------------------------------------------------
            // Must contain letters
            // ---------------------------------------------------------

            return Regex.IsMatch(
                line,
                @"[A-Za-z]");
        }

        // =============================================================
        // INVOICE DATE
        // =============================================================

        private DateTime? ExtractInvoiceDate(string text)
        {
            string[] patterns =
            {
                @"\bInvoice\s+Date\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",

                @"\bDate\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",

                @"\bInvoice\s+Date\s*[:\-]?\s*(\d{1,2}\s+[A-Za-z]+\s+\d{4})"
            };

            foreach (string pattern in patterns)
            {
                Match match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                string value =
                    match.Groups[1]
                        .Value
                        .Trim();

                string[] formats =
                {
                    "dd/MM/yyyy",
                    "d/M/yyyy",
                    "dd-MM-yyyy",
                    "d-M-yyyy",

                    "dd/MM/yy",
                    "d/M/yy",
                    "dd-MM-yy",
                    "d-M-yy",

                    "d MMMM yyyy",
                    "dd MMMM yyyy"
                };

                if (DateTime.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsed))
                {
                    return parsed;
                }

                if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        // =============================================================
        // SUBTOTAL
        // =============================================================

        private decimal? ExtractSubtotal(string text)
        {
            Match match =
                Regex.Match(
                    text,
                    @"\bSUBTOTAL\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",
                    RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            return ParseMoney(
                match.Groups[1].Value);
        }

        // =============================================================
        // DISCOUNT
        // =============================================================

        private decimal? ExtractDiscount(string text)
        {
            Match match =
                Regex.Match(
                    text,
                    @"\bDISCOUNT\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",
                    RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            return ParseMoney(
                match.Groups[1].Value);
        }

        // =============================================================
        // AMOUNT
        // =============================================================

        private decimal? ExtractAmount(string text)
        {
            string[] patterns =
            {
                @"\bNET\s+AMOUNT\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",

                @"\bAMOUNT\s+BEFORE\s+VAT\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",

                @"\bTOTAL\s+BEFORE\s+VAT\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",

                @"\bTOTAL\s+BEFORE\s+TAX\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",

                @"\bAMOUNT\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)"
            };

            foreach (string pattern in patterns)
            {
                Match match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                decimal? value =
                    ParseMoney(
                        match.Groups[1].Value);

                if (value.HasValue)
                    return value;
            }

            return null;
        }

        // =============================================================
        // VAT
        // =============================================================

        private decimal? ExtractVAT(string text)
        {
            /*
             * IMPORTANT:
             *
             * Your invoice contains:
             *
             * VAT No: 4650198237
             *
             * and later:
             *
             * Tax (VAT 15%) 4,477.50
             *
             * We MUST NOT extract 4650198237.
             */

            string[] patterns =
            {
                // Tax (VAT 15%) 4,477.50
                @"\bTAX\s*\(\s*VAT\s*\d+(?:\.\d+)?%\s*\)\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",

                // VAT 15% 4,477.50
                @"\bVAT\s*\d+(?:\.\d+)?%\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",

                // VAT Amount: 4,477.50
                @"\bVAT\s+(?:AMOUNT|TOTAL)\s*[:\-]?\s*(?:R|ZAR)?\s*([\d\s,\.]+)",

                // VAT: 4,477.50
                @"\bVAT\s*[:\-]\s*(?:R|ZAR)?\s*([\d\s,\.]+)"
            };

            foreach (string pattern in patterns)
            {
                Match match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                decimal? value =
                    ParseMoney(
                        match.Groups[1].Value);

                if (value.HasValue)
                    return value;
            }

            return null;
        }

        // =============================================================
        // TOTAL
        // =============================================================

        private decimal? ExtractTotalAmount(string text)
        {
            string[] patterns =
            {
                @"\bGRAND\s+TOTAL\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",

                @"\bTOTAL\s+DUE\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",

                @"\bTOTAL\s+AMOUNT\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",

                @"\bAMOUNT\s+DUE\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",

                @"\bBALANCE\s+DUE\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)",

                @"\bTOTAL\b\s*[:\-]?\s*(?:R|ZAR|\$|€|£)?\s*([\d\s,\.]+)"
            };

            foreach (string pattern in patterns)
            {
                Match match =
                    Regex.Match(
                        text,
                        pattern,
                        RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                decimal? value =
                    ParseMoney(
                        match.Groups[1].Value);

                if (value.HasValue)
                    return value;
            }

            return null;
        }

        // =============================================================
        // MONEY PARSER
        // =============================================================

        private decimal? ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Trim();

            // Remove currency symbols
            value = Regex.Replace(
                value,
                @"R|ZAR|\$|€|£",
                "",
                RegexOptions.IgnoreCase);

            // Remove spaces
            value = value.Replace(" ", "");

            /*
             * Example:
             *
             * 30,850.00
             * 4,477.50
             * 34,327.50
             *
             * These use:
             * comma = thousands
             * dot   = decimal
             */

            if (value.Contains(",") &&
                value.Contains("."))
            {
                int commaIndex =
                    value.LastIndexOf(',');

                int dotIndex =
                    value.LastIndexOf('.');

                if (dotIndex > commaIndex)
                {
                    // 30,850.00
                    value = value.Replace(",", "");
                }
                else
                {
                    // 30.850,00
                    value = value.Replace(".", "");
                    value = value.Replace(",", ".");
                }
            }
            else if (value.Contains(","))
            {
                int commaIndex =
                    value.LastIndexOf(',');

                int digitsAfter =
                    value.Length -
                    commaIndex -
                    1;

                if (digitsAfter == 2)
                {
                    // 850,00
                    value = value.Replace(".", "");
                    value = value.Replace(",", ".");
                }
                else
                {
                    // 30,850
                    value = value.Replace(",", "");
                }
            }

            if (decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out decimal result))
            {
                return result;
            }

            return null;
        }
    }
}

