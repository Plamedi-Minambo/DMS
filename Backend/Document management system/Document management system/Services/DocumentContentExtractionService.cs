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

                var body = document.MainDocumentPart?.Document?.Body;

                if (body == null)
                {
                    return string.Empty;
                }

                var text = body
                    .Descendants<Text>()
                    .Select(t => t.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t));

                return string.Join(" ", text);
            });
        }

        private async Task<string> ExtractImageTextAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var tessDataPath = Path.Combine(
                    _environment.ContentRootPath,
                    "tessdata");

                var trainedDataPath = Path.Combine(
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

                using var engine = new Engine(
                    tessDataPath,
                    Language.English,
                    EngineMode.Default);

                using var image =
                    Image.LoadFromFile(filePath);

                using var page =
                    engine.Process(image);

                return page.Text ?? string.Empty;
            });
        }
    }
}