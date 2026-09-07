using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentManagement.API.Services
{
    /// <summary>
    /// Extracts text from uploaded documents where text is actually
    /// needed before calling Gemini.
    ///
    /// Images (JPG/JPEG/PNG) and PDFs are sent to Gemini as raw file
    /// bytes and read directly by its vision model, so no local
    /// OCR/text pre-extraction is performed for them here - Tesseract
    /// OCR has been removed entirely. Gemini's own reading of the
    /// document image/PDF is now the only extraction step for those
    /// file types, instead of running a full local OCR pass whose
    /// result was previously being discarded.
    ///
    /// DOCX still needs local text extraction, since Gemini receives
    /// DOCX content as plain text rather than as a file attachment.
    /// </summary>
    public class DocumentContentExtractionService
    {
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
                ".docx" =>
                    await ExtractDocxTextAsync(filePath),

                // Images and PDFs go straight to Gemini as file bytes -
                // no local extraction step needed or performed.
                ".pdf" or
                ".jpg" or
                ".jpeg" or
                ".png" =>
                    string.Empty,

                _ =>
                    throw new NotSupportedException(
                        $"The file type '{extension}' is not supported.")
            };
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
    }
}
