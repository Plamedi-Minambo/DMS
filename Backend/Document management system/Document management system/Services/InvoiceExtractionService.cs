```csharp
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
            // CALCULATE MISSING AMOUNT FROM TOTAL - VAT
            // --------------------------------------------------------

            if (amount == null &&
                totalAmount != null &&
                vat != null &&
                totalAmount.Value >= vat.Value)
            {
                amount = decimal.Round(
                    totalAmount.Value - vat.Value,
                    2);
            }

            // --------------------------------------------------------
            // CALCULATE MISSING TOTAL FROM AMOUNT + VAT
            // --------------------------------------------------------

            if (totalAmount == null &&
                amount != null &&
                vat != null)
            {
                totalAmount = decimal.Round(
                    amount.Value + vat.Value,
                    2);
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

            normalized = normalized
                .Replace("\t", " ");

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

            // Common OCR mistakes in financial labels.
            normalized = Regex.Replace(
                normalized,
                @"\bT[0O]TAL\b",
                "TOTAL",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            normalized = Regex.Replace(
                normalized,
                @"\bSUBT[0O]TAL\b",
                "SUBTOTAL",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            normalized = Regex.Replace(
                normalized,
                @"\bV[A4]I\b",
                "VAT",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            normalized = Regex.Replace(
                normalized,
                @"\bV[A4]T\b",
                "VAT",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            normalized = Regex.Replace(
                normalized,
                @"\bAM0UNT\b",
                "AMOUNT",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

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
                {
                    creditEvidence++;
                }

                if (ExtractTotalAmount(text) != null)
                {
                    creditEvidence++;
                }

                if (ExtractVAT(text) != null)
                {
                    creditEvidence++;
                }

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
            {
                evidence++;
            }

            if (ExtractDate(text) != null)
            {
                evidence++;
            }

            if (ExtractVendor(text).HasValue())
            {
                evidence++;
            }

            if (ExtractTotalAmount(text) != null)
            {
                evidence++;
            }

            if (ExtractVAT(text) != null)
            {
                evidence++;
            }

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

            return !invalidValues.Contains(value);
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
        // AMOUNT BEFORE VAT
        // ============================================================

        private decimal? ExtractAmount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var lines = GetFinancialLines(text);

            // --------------------------------------------------------
            // FIRST: look for explicit pre-VAT amount labels
            // --------------------------------------------------------

            var labelPatterns = new[]
            {
                @"\bsubtotal\b",
                @"\bsub\s*total\b",
                @"\bnet\s+amount\b",
                @"\bnet\s+total\b",
                @"\btotal\s+before\s+VAT\b",
                @"\btotal\s+before\s+tax\b",
                @"\bamount\s+before\s+VAT\b",
                @"\bamount\s+before\s+tax\b",
                @"\bnet\b"
            };

            foreach (var line in lines)
            {
                foreach (var labelPattern in labelPatterns)
                {
                    if (!Regex.IsMatch(
                        line,
                        labelPattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant))
                    {
                        continue;
                    }

                    var amount = ExtractNumberNearLabel(
                        line,
                        labelPattern);

                    if (amount != null)
                    {
                        return amount;
                    }
                }
            }

            // --------------------------------------------------------
            // SECOND: common "AMOUNT: value" format
            // --------------------------------------------------------

            foreach (var line in lines)
            {
                if (!Regex.IsMatch(
                    line,
                    @"\bAMOUNT\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    continue;
                }

                if (Regex.IsMatch(
                    line,
                    @"\b(?:amount\s+due|balance\s+due)\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    continue;
                }

                var amount = ExtractFirstMoneyValue(line);

                if (amount != null)
                {
                    return amount;
                }
            }

            // --------------------------------------------------------
            // IMPORTANT:
            //
            // We deliberately DO NOT treat "Amount Due" or
            // "Balance Due" as Amount-before-VAT.
            //
            // If Total and VAT are available, the main method will
            // calculate:
            //
            // Amount = Total - VAT
            // --------------------------------------------------------

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

            var lines = GetFinancialLines(text);

            // --------------------------------------------------------
            // VAT LABELS
            // --------------------------------------------------------

            foreach (var line in lines)
            {
                if (!Regex.IsMatch(
                    line,
                    @"\bVAT\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    continue;
                }

                // Ignore lines that only contain a VAT percentage
                // without an actual VAT amount.
                var values = ExtractMoneyCandidates(line);

                if (values.Count == 0)
                {
                    continue;
                }

                // ----------------------------------------------------
                // Remove percentage values such as 15%
                // ----------------------------------------------------

                var nonPercentageValues =
                    values
                        .Where(candidate =>
                            !IsPercentageValueNearCandidate(
                                line,
                                candidate.Raw))
                        .ToList();

                if (nonPercentageValues.Count > 0)
                {
                    // Prefer the final monetary value on a VAT line.
                    return nonPercentageValues
                        .OrderByDescending(x => x.Position)
                        .First()
                        .Value;
                }

                // If the line contained a number but our percentage
                // detection was uncertain, use the final value.
                return values
                    .OrderByDescending(x => x.Position)
                    .First()
                    .Value;
            }

            // --------------------------------------------------------
            // TAX AMOUNT / SALES TAX
            // --------------------------------------------------------

            var taxPatterns = new[]
            {
                @"\btax\s+amount\b",
                @"\bsales\s+tax\b",
                @"\btax\b"
            };

            foreach (var line in lines)
            {
                foreach (var pattern in taxPatterns)
                {
                    if (!Regex.IsMatch(
                        line,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant))
                    {
                        continue;
                    }

                    var values =
                        ExtractMoneyCandidates(line);

                    if (values.Count > 0)
                    {
                        return values
                            .OrderByDescending(x => x.Position)
                            .First()
                            .Value;
                    }
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

            var lines = GetFinancialLines(text);

            var totalPatterns = new[]
            {
                @"\bgrand\s+total\b",
                @"\btotal\s+amount\b",
                @"\btotal\s+due\b",
                @"\bamount\s+due\b",
                @"\bbalance\s+due\b",
                @"\btotal\b"
            };

            // --------------------------------------------------------
            // FIRST: strongest total labels
            // --------------------------------------------------------

            foreach (var line in lines)
            {
                foreach (var pattern in totalPatterns.Take(5))
                {
                    if (!Regex.IsMatch(
                        line,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant))
                    {
                        continue;
                    }

                    // Don't mistake "total before VAT" for the final
                    // total.
                    if (Regex.IsMatch(
                        line,
                        @"\btotal\s+before\s+(?:VAT|tax)\b",
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant))
                    {
                        continue;
                    }

                    var values =
                        ExtractMoneyCandidates(line);

                    if (values.Count > 0)
                    {
                        return values
                            .OrderByDescending(x => x.Position)
                            .First()
                            .Value;
                    }
                }
            }

            // --------------------------------------------------------
            // SECOND: normal TOTAL
            // --------------------------------------------------------

            foreach (var line in lines)
            {
                if (!Regex.IsMatch(
                    line,
                    @"\btotal\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    continue;
                }

                if (Regex.IsMatch(
                    line,
                    @"\b(?:subtotal|sub\s*total|total\s+before\s+(?:VAT|tax))\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
                {
                    continue;
                }

                var values =
                    ExtractMoneyCandidates(line);

                if (values.Count > 0)
                {
                    return values
                        .OrderByDescending(x => x.Position)
                        .First()
                        .Value;
                }
            }

            return null;
        }

        // ============================================================
        // GET FINANCIAL LINES
        // ============================================================

        private List<string> GetFinancialLines(string text)
        {
            return text
                .Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line))
                .Select(NormalizeFinancialLine)
                .Where(line =>
                    Regex.IsMatch(
                        line,
                        @"\d",
                        RegexOptions.CultureInvariant))
                .ToList();
        }

        // ============================================================
        // NORMALIZE FINANCIAL LINE
        // ============================================================

        private string NormalizeFinancialLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            var result = line.Trim();

            // Common OCR errors in labels.
            result = Regex.Replace(
                result,
                @"\bT[0O]TAL\b",
                "TOTAL",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            result = Regex.Replace(
                result,
                @"\bSUBT[0O]TAL\b",
                "SUBTOTAL",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            result = Regex.Replace(
                result,
                @"\bV[A4]T\b",
                "VAT",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            result = Regex.Replace(
                result,
                @"\bVAI\b",
                "VAT",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            result = Regex.Replace(
                result,
                @"\bAM0UNT\b",
                "AMOUNT",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            result = Regex.Replace(
                result,
                @"\s{2,}",
                " ");

            return result.Trim();
        }

        // ============================================================
        // EXTRACT NUMBER NEAR A LABEL
        // ============================================================

        private decimal? ExtractNumberNearLabel(
            string line,
            string labelPattern)
        {
            var labelMatch = Regex.Match(
                line,
                labelPattern,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            if (!labelMatch.Success)
            {
                return null;
            }

            var remainingText =
                line.Substring(labelMatch.Index + labelMatch.Length);

            var values =
                ExtractMoneyCandidates(remainingText);

            if (values.Count == 0)
            {
                return null;
            }

            return values
                .OrderBy(x => x.Position)
                .First()
                .Value;
        }

        // ============================================================
        // EXTRACT FIRST MONEY VALUE
        // ============================================================

        private decimal? ExtractFirstMoneyValue(string text)
        {
            var values =
                ExtractMoneyCandidates(text);

            if (values.Count == 0)
            {
                return null;
            }

            return values
                .OrderBy(x => x.Position)
                .First()
                .Value;
        }

        // ============================================================
        // EXTRACT MONEY CANDIDATES
        // ============================================================

        private List<MoneyCandidate> ExtractMoneyCandidates(
            string text)
        {
            var candidates =
                new List<MoneyCandidate>();

            if (string.IsNullOrWhiteSpace(text))
            {
                return candidates;
            }

            // --------------------------------------------------------
            // Matches:
            //
            // R 1,250.00
            // R1,250.00
            // ZAR 1250.00
            // $1250.00
            // 1,250.00
            // 1 250.00
            // 1250.00
            // --------------------------------------------------------

            var pattern =
                @"(?<![\d%])" +
                @"(?:(?:R|ZAR|\$|€|£)\s*)?" +
                @"[-+]?" +
                @"\d" +
                @"(?:[\d\s,\.]*\d)?" +
                @"(?!\s*%)";

            foreach (Match match in Regex.Matches(
                text,
                pattern,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            {
                var raw =
                    match.Value.Trim();

                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                // Skip a standalone percentage.
                if (raw.Contains("%"))
                {
                    continue;
                }

                var value =
                    ParseMoney(raw);

                if (value == null)
                {
                    continue;
                }

                // Avoid absurdly large OCR captures.
                if (Math.Abs(value.Value) > 999999999999m)
                {
                    continue;
                }

                candidates.Add(
                    new MoneyCandidate
                    {
                        Raw = raw,
                        Value = value.Value,
                        Position = match.Index
                    });
            }

            return candidates;
        }

        // ============================================================
        // CHECK WHETHER VALUE IS A PERCENTAGE
        // ============================================================

        private bool IsPercentageValueNearCandidate(
            string line,
            string candidate)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            var index =
                line.IndexOf(
                    candidate,
                    StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return false;
            }

            var before =
                line.Substring(
                    Math.Max(0, index - 5),
                    Math.Min(5, index));

            var afterStart =
                index + candidate.Length;

            var after =
                afterStart < line.Length
                    ? line.Substring(
                        afterStart,
                        Math.Min(
                            5,
                            line.Length - afterStart))
                    : string.Empty;

            return before.Contains("%") ||
                   after.Contains("%");
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

            // Remove currency symbols and other characters.
            cleaned = Regex.Replace(
                cleaned,
                @"[^\d,\.\-\s]",
                "");

            cleaned =
                cleaned.Replace(" ", "");

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return null;
            }

            // --------------------------------------------------------
            // BOTH COMMA AND DOT
            // --------------------------------------------------------

            if (cleaned.Contains(',') &&
                cleaned.Contains('.'))
            {
                var lastComma =
                    cleaned.LastIndexOf(',');

                var lastDot =
                    cleaned.LastIndexOf('.');

                // Example:
                // 1.250,50
                //
                // comma is decimal separator.
                if (lastComma > lastDot)
                {
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
                    // Example:
                    // 1,250.50
                    //
                    // dot is decimal separator.
                    cleaned =
                        cleaned.Replace(
                            ",",
                            "");
                }
            }

            // --------------------------------------------------------
            // COMMA ONLY
            // --------------------------------------------------------

            else if (cleaned.Contains(','))
            {
                var commaParts =
                    cleaned.Split(',');

                // 1250,50 -> 1250.50
                if (commaParts.Length == 2 &&
                    commaParts[1].Length == 2)
                {
                    cleaned =
                        cleaned.Replace(
                            ",",
                            ".");
                }
                else
                {
                    // 1,250 -> 1250
                    // 1,250,000 -> 1250000
                    cleaned =
                        cleaned.Replace(
                            ",",
                            "");
                }
            }

            // --------------------------------------------------------
            // DOT ONLY
            // --------------------------------------------------------

            else if (cleaned.Contains('.'))
            {
                var dotParts =
                    cleaned.Split('.');

                // 1.250.50 is likely OCR/grouping noise.
                // Preserve the final two digits as decimals.
                if (dotParts.Length > 2)
                {
                    var lastPart =
                        dotParts[^1];

                    if (lastPart.Length == 2)
                    {
                        var wholePart =
                            string.Join(
                                "",
                                dotParts.Take(
                                    dotParts.Length - 1));

                        cleaned =
                            wholePart +
                            "." +
                            lastPart;
                    }
                    else
                    {
                        cleaned =
                            cleaned.Replace(
                                ".",
                                "");
                    }
                }
            }

            if (decimal.TryParse(
                cleaned,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var amount))
            {
                return decimal.Round(
                    amount,
                    2);
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
                $@"\b(?:{labelPattern})\s*[:\-]?\s*" +
                @"(?:R|ZAR|\$|€|£)?\s*" +
                @"([0-9][0-9,\.\s]*)";

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
        // CLEAN EXTRACTED VALUE
        // ============================================================

        private string CleanExtractedValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var cleaned =
                value.Trim();

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

        // ============================================================
        // MONEY CANDIDATE CLASS
        // ============================================================

        private sealed class MoneyCandidate
        {
            public string Raw { get; set; } = string.Empty;

            public decimal Value { get; set; }

            public int Position { get; set; }
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
```
