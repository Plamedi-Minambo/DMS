using UglyToad.PdfPig;

namespace DocumentManagement.API.Services
{
    public class PdfTextExtractionService
    {
        public async Task<string> ExtractTextAsync(string filePath)
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
                    "PDF file could not be found.",
                    filePath);
            }

            return await Task.Run(() =>
            {
                using var pdfDocument =
                    PdfDocument.Open(filePath);

                var extractedText =
                    new System.Text.StringBuilder();

                foreach (var page in pdfDocument.GetPages())
                {
                    extractedText.AppendLine(page.Text);
                }

                return extractedText.ToString();
            });
        }
    }
}