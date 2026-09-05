using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Pix;

namespace DocumentManagement.API.Services
{
    public class DocumentContentExtractionService
    {
        private readonly PdfTextExtractionService _pdfTextExtractionService;
        private readonly IWebHostEnvironment _environment;

        public DocumentContentExtractionService(
            PdfTextExtractionService pdfTextExtractionService,
            IWebHostEnvironment environment)
        {
            _pdfTextExtractionService = pdfTextExtractionService;
            _environment = environment;
        }

        // ============================================================
        // MAIN EXTRACTION METHOD
        // ============================================================

        public async Task<string> ExtractTextAsync(
            string filePath,
            string fileExtension)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "File path cannot be empty.",
                    nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Document file could not be found.",
                    filePath);
            }

            var extension =
                fileExtension?
                    .Trim()
                    .ToLowerInvariant();

            return extension switch
            {
                ".pdf" =>
                    await ExtractPdfTextAsync(filePath),

                ".docx" =>
                    await ExtractDocxTextAsync(filePath),

                ".jpg" or
                ".jpeg" or
                ".png" =>
                    await ExtractImageTextAsync(filePath),

                _ =>
                    throw new NotSupportedException(
                        $"The file type '{extension}' is not supported.")
            };
        }

        // ============================================================
        // PDF
        // ============================================================

        private async Task<string> ExtractPdfTextAsync(
            string filePath)
        {
            var text =
                await _pdfTextExtractionService
                    .ExtractTextAsync(filePath);

            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim();
        }

        // ============================================================
        // DOCX
        // ============================================================

        private async Task<string> ExtractDocxTextAsync(
            string filePath)
        {
            return await Task.Run(() =>
            {
                using var document =
                    WordprocessingDocument.Open(
                        filePath,
                        false);

                var body =
                    document
                        .MainDocumentPart?
                        .Document?
                        .Body;

                if (body == null)
                {
                    return string.Empty;
                }

                var paragraphs =
                    body
                        .Descendants<Paragraph>()
                        .Select(paragraph =>
                            string.Concat(
                                paragraph
                                    .Descendants<Text>()
                                    .Select(t => t.Text)))
                        .Select(text => text.Trim())
                        .Where(text =>
                            !string.IsNullOrWhiteSpace(text));

                return string.Join(
                    Environment.NewLine,
                    paragraphs);
            });
        }

        // ============================================================
        // IMAGE / OCR
        // ============================================================

        private async Task<string> ExtractImageTextAsync(
            string filePath)
        {
            return await Task.Run(() =>
            {
                var tessDataPath =
                    Path.Combine(
                        _environment.ContentRootPath,
                        "tessdata");

                var trainedDataPath =
                    Path.Combine(
                        tessDataPath,
                        "eng.traineddata");

                // ----------------------------------------------------
                // CHECK TESSDATA
                // ----------------------------------------------------

                if (!Directory.Exists(tessDataPath))
                {
                    throw new DirectoryNotFoundException(
                        $"Tesseract tessdata folder was not found: {tessDataPath}");
                }

                if (!File.Exists(trainedDataPath))
                {
                    throw new FileNotFoundException(
                        "Tesseract English trained data was not found.",
                        trainedDataPath);
                }

                // ----------------------------------------------------
                // CREATE OCR ENGINE
                // ----------------------------------------------------

                using var engine =
                    new Engine(
                        tessDataPath,
                        Language.English,
                        EngineMode.Default);

                // ----------------------------------------------------
                // LOAD IMAGE
                // ----------------------------------------------------

                using var image =
                    Image.LoadFromFile(filePath);

                // ----------------------------------------------------
                // OCR PASS
                // ----------------------------------------------------

                using var page =
                    engine.Process(image);

                var extractedText =
                    page.Text ?? string.Empty;

                // ----------------------------------------------------
                // CLEAN OCR RESULT
                // ----------------------------------------------------

                extractedText =
                    CleanOcrText(extractedText);

                return extractedText;
            });
        }

        // ============================================================
        // CLEAN OCR TEXT
        // ============================================================

        private static string CleanOcrText(
            string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text =
                text
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n");

            var rawLines =
                text.Split('\n');

            var cleanedLines =
                new List<string>();

            foreach (var rawLine in rawLines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                var line =
                    rawLine
                        .Replace("\t", " ")
                        .Replace("\u00A0", " ")
                        .Trim();

                // Collapse repeated spaces.
                line =
                    System.Text.RegularExpressions.Regex.Replace(
                        line,
                        @" {2,}",
                        " ");

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                line =
                    FixCommonInvoiceOcrErrors(line);

                cleanedLines.Add(line);
            }

            return string.Join(
                Environment.NewLine,
                cleanedLines);
        }

        // ============================================================
        // COMMON OCR CORRECTIONS
        // ============================================================

        private static string FixCommonInvoiceOcrErrors(
            string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            var result = line;

            // --------------------------------------------------------
            // BILL TO
            // --------------------------------------------------------

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"^\s*BILL\s*T[O0]\s*:?\s*$",
                    "BILL TO",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"^\s*BILLTO\s*:?\s*$",
                    "BILL TO",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"^\s*BILLED\s*T[O0]\s*:?\s*$",
                    "BILLED TO",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // --------------------------------------------------------
            // INVOICE
            // --------------------------------------------------------

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"^\s*INVOlCE\b",
                    "INVOICE",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"^\s*lnvoice\b",
                    "INVOICE",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // --------------------------------------------------------
            // TOTAL
            // --------------------------------------------------------

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"^\s*T0TAL\b",
                    "TOTAL",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // --------------------------------------------------------
            // VAT
            // --------------------------------------------------------

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"^\s*V[A4]T\b",
                    "VAT",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // --------------------------------------------------------
            // DATE
            // --------------------------------------------------------

            result =
                System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"^\s*D[A4]TE\b",
                    "DATE",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return result.Trim();
        }
    }
}
