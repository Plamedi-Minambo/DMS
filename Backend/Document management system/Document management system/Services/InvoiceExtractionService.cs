
using System.Globalization;
using System.Text.RegularExpressions;
using DocumentManagement.API.Models;

namespace DocumentManagement.API.Services
{
    public class InvoiceExtractionService
    {
        public InvoiceData ExtractInvoiceData(int documentId, string extractedText)
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

            // Clean the extracted PDF/OCR text
            string text = NormalizeText(extractedText);

            // Extract individual invoice fields
            string? documentType = DetermineDocumentType(text);
            string? invoiceNumber = ExtractInvoiceNumber(text);
            string? vendor = ExtractVendor(text);
            DateTime? invoiceDate = ExtractInvoiceDate(text);

            decimal? subtotal = ExtractSubtotal(text);
            decimal? discount = ExtractDiscount(text);

            decimal? amount = ExtractAmount(text, subtotal, discount);
            decimal? vat = ExtractVAT(text);
            decimal? totalAmount = ExtractTotalAmount(text);

            // ---------------------------------------------------------
            // Financial fallbacks
            // ---------------------------------------------------------

            // If total is available but amount is missing,
            // calculate amount = total - VAT.
            if (!amount.HasValue && totalAmount.HasValue && vat.HasValue)
            {
                amount = totalAmount.Value - vat.Value;
            }

            // If amount and VAT are available but total is missing,
            // calculate total = amount + VAT.
            if (!totalAmount.HasValue && amount.HasValue && vat.HasValue)
            {
                totalAmount = amount.Value + vat.Value;
            }

            // If amount and total are available but VAT is missing,
            // calculate VAT = total - amount.
            if (!vat.HasValue && totalAmount.HasValue && amount.HasValue)
            {
                vat = totalAmount.Value - amount.Value;
            }

            // ---------------------------------------------------------
            // Determine extraction status
            // ---------------------------------------------------------

            bool hasRequiredData =
                !string.IsNullOrWhiteSpace(invoiceNumber) ||
                !string.IsNullOrWhiteSpace(vendor) ||
                invoiceDate.HasValue ||
                amount.HasValue ||
                vat.HasValue ||
                totalAmount.HasValue;

            return new InvoiceData
            {
                DocumentId = documentId,

                DocumentType = documentType,

                InvoiceNumber = invoiceNumber,

                Vendor = vendor,

                InvoiceDate = invoiceDate,

                Amount = amount,

                VAT = vat,

                TotalAmount = totalAmount,

                ExtractedAt = DateTime.UtcNow,

                ExtractionStatus = hasRequiredData
                    ? "Completed"
                    : "Failed"
            };
        }

        // =============================================================
        // NORMALIZE TEXT
        // =============================================================

        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\t", " ")
                .Replace("\u00A0", " ");

            // Remove excessive spaces while preserving line breaks.
            text = Regex.Replace(text, @"[ ]{2,}", " ");

            // Remove excessive blank lines.
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            // Common OCR corrections.
            text = text.Replace("INVOlCE", "INVOICE");
            text = text.Replace("lnvoice", "Invoice");
            text = text.Replace("T0TAL", "TOTAL");
            text = text.Replace("VAT :", "VAT:");

            return text.Trim();
        }

        // =============================================================
        // DOCUMENT TYPE
        // =============================================================

        private string DetermineDocumentType(string text)
        {
            if (Regex.IsMatch(
                text,
                @"\bCREDIT\s+(NOTE|MEMO)\b",
                RegexOptions.IgnoreCase))
            {
                return "Credit Note";
            }

            if (Regex.IsMatch(
                text,
                @"\bTAX\s+INVOICE\b",
                RegexOptions.IgnoreCase))
            {
                return "Invoice";
            }

            if (Regex.IsMatch(
                text,
                @"\bINVOICE\b",
                RegexOptions.IgnoreCase))
            {
                return "Invoice";
            }

            return "Unknown";
        }

        // =============================================================
        // INVOICE NUMBER
        // =============================================================

        private string? ExtractInvoiceNumber(string text)
        {
            string[] patterns =
            {
                // Invoice #: INV-20394
                @"(?im)^\s*INVOICE\s*(?:NUMBER|NO\.?|#)\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)\s*$",

                // Invoice Number: INV-20394
                @"(?im)^\s*INVOICE\s+NUMBER\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)\s*$",

                // Invoice No: INV-20394
                @"(?im)^\s*INVOICE\s+NO\.?\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)\s*$",

                // Invoice #: INV-20394
                @"(?im)^\s*INVOICE\s*#\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)\s*$",

                // Inv #: INV-20394
                @"(?im)^\s*INV\s*#\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)\s*$",

                // Inv No: INV-20394
                @"(?im)^\s*INV\s+NO\.?\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]*)\s*$"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    string value = match.Groups[1].Value.Trim();

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
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
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToList();

            if (lines.Count == 0)
                return null;

            foreach (string line in lines)
            {
                string upper = line.ToUpperInvariant();

                // The vendor normally appears before the INVOICE heading.
                if (upper == "INVOICE" ||
                    upper == "TAX INVOICE")
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Ignore VAT registration lines.
                if (Regex.IsMatch(
                    line,
                    @"^VAT\s*(NO|NUMBER|REGISTRATION)?\s*:",
                    RegexOptions.IgnoreCase))
                {
                    continue;
                }

                // Ignore email addresses.
                if (line.Contains("@"))
                    continue;

                // Ignore telephone numbers.
                if (Regex.IsMatch(
                    line,
                    @"^\+?[0-9][0-9\s\-()]{7,}$"))
                {
                    continue;
                }

                // Ignore common address lines.
                if (Regex.IsMatch(
                    line,
                    @"^\d+\s+.*\b(ROAD|RD|STREET|ST|AVENUE|AVE|DRIVE|DR|PARK|BUSINESS PARK|LANE|LANE)\b",
                    RegexOptions.IgnoreCase))
                {
                    continue;
                }

                // Ignore obvious headings.
                if (upper == "BILL TO" ||
                    upper == "SHIP TO" ||
                    upper == "PAYMENT TERMS" ||
                    upper == "PAYMENT METHOD" ||
                    upper == "NOTES")
                {
                    continue;
                }

                // The first meaningful text line is normally the vendor.
                if (Regex.IsMatch(line, @"[A-Za-z]"))
                {
                    return line.Trim();
                }
            }

            return null;
        }

        // =============================================================
        // INVOICE DATE
        // =============================================================

        private DateTime? ExtractInvoiceDate(string text)
        {
            string[] patterns =
            {
                // Invoice Date: 05/09/2026
                @"(?im)^\s*INVOICE\s+DATE\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})\s*$",

                // Invoice Date: 05-09-2026
                @"(?im)^\s*DATE\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})\s*$",

                // Invoice Date: 05 September 2026
                @"(?im)^\s*INVOICE\s+DATE\s*[:\-]?\s*(\d{1,2}\s+[A-Za-z]+\s+\d{4})\s*$",

                // Invoice Date: September 05, 2026
                @"(?im)^\s*INVOICE\s+DATE\s*[:\-]?\s*([A-Za-z]+\s+\d{1,2},?\s+\d{4})\s*$"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                string dateValue = match.Groups[1].Value.Trim();

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

                    "dd MMMM yyyy",
                    "d MMMM yyyy",

                    "MMMM dd, yyyy",
                    "MMMM d, yyyy",

                    "MMMM dd yyyy",
                    "MMMM d yyyy"
                };

                if (DateTime.TryParseExact(
                    dateValue,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsedDate))
                {
                    return parsedDate;
                }

                if (DateTime.TryParse(
                    dateValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsedDate))
                {
                    return parsedDate;
                }
            }

            return null;
        }

        // =============================================================
        // SUBTOTAL
        // =============================================================

        private decimal? ExtractSubtotal(string text)
        {
            string[] patterns =
            {
                @"(?im)^\s*SUBTOTAL\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                @"(?im)^\s*SUB\s*TOTAL\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(text, pattern);

                if (match.Success &&
                    TryParseMoney(match.Groups[1].Value, out decimal value))
                {
                    return value;
                }
            }

            return null;
        }

        // =============================================================
        // DISCOUNT
        // =============================================================

        private decimal? ExtractDiscount(string text)
        {
            string[] patterns =
            {
                @"(?im)^\s*DISCOUNT\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                @"(?im)^\s*LESS\s+DISCOUNT\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(text, pattern);

                if (match.Success &&
                    TryParseMoney(match.Groups[1].Value, out decimal value))
                {
                    return value;
                }
            }

            return null;
        }

        // =============================================================
        // AMOUNT
        // =============================================================

        private decimal? ExtractAmount(
            string text,
            decimal? subtotal,
            decimal? discount)
        {
            // ---------------------------------------------------------
            // Best case:
            // Amount = Subtotal - Discount
            // ---------------------------------------------------------

            if (subtotal.HasValue)
            {
                decimal calculatedAmount = subtotal.Value;

                if (discount.HasValue)
                    calculatedAmount -= discount.Value;

                return calculatedAmount;
            }

            // ---------------------------------------------------------
            // Direct amount patterns
            // ---------------------------------------------------------

            string[] patterns =
            {
                @"(?im)^\s*NET\s+AMOUNT\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                @"(?im)^\s*AMOUNT\s+BEFORE\s+VAT\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                @"(?im)^\s*AMOUNT\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(text, pattern);

                if (match.Success &&
                    TryParseMoney(match.Groups[1].Value, out decimal value))
                {
                    return value;
                }
            }

            return null;
        }

        // =============================================================
        // VAT
        // =============================================================

        private decimal? ExtractVAT(string text)
        {
            string[] patterns =
            {
                // Tax (VAT 15%) 4,477.50
                @"(?im)^\s*TAX\s*\(\s*VAT\s*\d+(?:\.\d+)?%\s*\)\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                // VAT 15%: 4,477.50
                @"(?im)^\s*VAT\s*\d+(?:\.\d+)?%\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                // VAT Amount: 4,477.50
                @"(?im)^\s*VAT\s+(?:AMOUNT|TOTAL)\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                // VAT: 4,477.50
                @"(?im)^\s*VAT\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                // Tax: 4,477.50
                @"(?im)^\s*TAX\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(text, pattern);

                if (match.Success &&
                    TryParseMoney(match.Groups[1].Value, out decimal value))
                {
                    return value;
                }
            }

            return null;
        }

        // =============================================================
        // TOTAL AMOUNT
        // =============================================================

        private decimal? ExtractTotalAmount(string text)
        {
            string[] patterns =
            {
                // TOTAL DUE 34,327.50
                @"(?im)^\s*TOTAL\s+DUE\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                // GRAND TOTAL 34,327.50
                @"(?im)^\s*GRAND\s+TOTAL\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                // TOTAL AMOUNT 34,327.50
                @"(?im)^\s*TOTAL\s+AMOUNT\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                // AMOUNT DUE 34,327.50
                @"(?im)^\s*AMOUNT\s+DUE\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                // BALANCE DUE 34,327.50
                @"(?im)^\s*BALANCE\s+DUE\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$",

                // TOTAL 34,327.50
                @"(?im)^\s*TOTAL\s*[:\-]?\s*(?:R|ZAR)?\s*([0-9,]+\.[0-9]{2})\s*$"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(text, pattern);

                if (match.Success &&
                    TryParseMoney(match.Groups[1].Value, out decimal value))
                {
                    return value;
                }
            }

            return null;
        }

        // =============================================================
        // MONEY PARSER
        // =============================================================

        private bool TryParseMoney(
            string value,
            out decimal amount)
        {
            amount = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            // Remove currency symbols and letters.
            value = Regex.Replace(
                value,
                @"[RrZzAaNnDd$€£]",
                "");

            value = value.Trim();

            // Remove spaces.
            value = value.Replace(" ", "");

            // ---------------------------------------------------------
            // Standard format:
            // 30,850.00
            // ---------------------------------------------------------

            if (decimal.TryParse(
                value,
                NumberStyles.AllowThousands |
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out amount))
            {
                return true;
            }

            // ---------------------------------------------------------
            // South African / European style:
            // 30.850,00
            // ---------------------------------------------------------

            if (decimal.TryParse(
                value,
                NumberStyles.AllowThousands |
                NumberStyles.AllowDecimalPoint,
                new CultureInfo("de-DE"),
                out amount))
            {
                return true;
            }

            // ---------------------------------------------------------
            // Plain number:
            // 30850
            // ---------------------------------------------------------

            if (decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out amount))
            {
                return true;
            }

            return false;
        }
    }
}

