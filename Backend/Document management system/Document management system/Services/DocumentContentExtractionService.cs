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

            var extension = fileExtension.Trim().ToLowerInvariant();

            return extension switch
            {
                ".pdf" => await ExtractPdfTextAsync(filePath),

                ".docx" => await ExtractDocxTextAsync(filePath),

                ".jpg" or ".jpeg" or ".png"
                    => await ExtractImageTextAsync(filePath),

                _ => throw new NotSupportedException(
                    $"The file type '{extension}' is not supported.")
            };
        }

        private async Task<string> ExtractPdfTextAsync(string filePath)
        {
            return await _pdfTextExtractionService.ExtractTextAsync(filePath);
        }

        private async Task<string> ExtractDocxTextAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                using var document =
                    WordprocessingDocument.Open(filePath, false);

                var body =
                    document.MainDocumentPart?
                        .Document?
                        .Body;

                if (body == null)
                {
                    return string.Empty;
                }

                var paragraphs = body
                    .Descendants<Paragraph>()
                    .Select(paragraph =>
                        string.Concat(
                            paragraph
                                .Descendants<Text>()
                                .Select(t => t.Text)))
                    .Where(text =>
                        !string.IsNullOrWhiteSpace(text));

                return string.Join(
                    Environment.NewLine,
                    paragraphs);
            });
        }

        private async Task<string> ExtractImageTextAsync(string filePath)
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

                using var engine =
                    new Engine(
                        tessDataPath,
                        Language.English,
                        EngineMode.Default);

                using var image =
                    Image.LoadFromFile(filePath);

                /*
                 * First OCR pass.
                 *
                 * This is the normal/general-purpose
                 * Tesseract recognition mode.
                 */
                using var page =
                    engine.Process(image);

                var extractedText =
                    page.Text ?? string.Empty;

                /*
                 * Clean the OCR result without destroying
                 * line structure.
                 *
                 * Line structure is important because invoice
                 * fields such as:
                 *
                 * BILL TO
                 * ABC COMPANY
                 *
                 * are often detected using neighbouring lines.
                 */
                extractedText =
                    CleanOcrText(extractedText);

                return extractedText;
            });
        }

        private static string CleanOcrText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            var lines =
                text
                    .Split('\n')
                    .Select(line => line.Trim())
                    .Where(line =>
                        !string.IsNullOrWhiteSpace(line));

            var cleanedLines =
                new List<string>();

            foreach (var line in lines)
            {
                var cleanedLine =
                    line
                        .Replace("\t", " ")
                        .Trim();

                while (cleanedLine.Contains("  "))
                {
                    cleanedLine =
                        cleanedLine.Replace("  ", " ");
                }

                if (!string.IsNullOrWhiteSpace(cleanedLine))
                {
                    cleanedLines.Add(cleanedLine);
                }
            }

            /*
             * Fix a few common OCR mistakes in invoice labels.
             *
             * Examples:
             *
             * BILL T0  -> BILL TO
             * BILLTO   -> BILL TO
             * BILLED T0 -> BILLED TO
             */
            for (int i = 0; i < cleanedLines.Count; i++)
            {
                cleanedLines[i] =
                    FixCommonInvoiceOcrErrors(
                        cleanedLines[i]);
            }

            return string.Join(
                Environment.NewLine,
                cleanedLines);
        }

        private static string FixCommonInvoiceOcrErrors(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            var result = line;

            /*
             * Only correct obvious invoice labels.
             * We do NOT globally replace letters in the
             * document because that could corrupt company names.
             */

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"^\s*BILL\s*T[O0]\s*:?\s*$",
                "BILL TO",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"^\s*BILLTO\s*:?\s*$",
                "BILL TO",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"^\s*BILLED\s*T[O0]\s*:?\s*$",
                "BILLED TO",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"^\s*BILL\s*TO\s*:\s*",
                "BILL TO: ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"^\s*BILLED\s*TO\s*:\s*",
                "BILLED TO: ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return result.Trim();
        }
    }
}
