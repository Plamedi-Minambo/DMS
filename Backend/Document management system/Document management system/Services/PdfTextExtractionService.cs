
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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
                var extractedText = new StringBuilder();

                try
                {
                    using var pdfDocument = PdfDocument.Open(filePath);

                    foreach (Page page in pdfDocument.GetPages())
                    {
                        // -------------------------------------------------
                        // Get words from the PDF instead of relying only
                        // on page.Text.
                        // -------------------------------------------------

                        var words = page.GetWords().ToList();

                        if (words.Count > 0)
                        {
                            foreach (var word in words)
                            {
                                if (!string.IsNullOrWhiteSpace(word.Text))
                                {
                                    extractedText.Append(word.Text);
                                    extractedText.Append(' ');
                                }
                            }

                            extractedText.AppendLine();
                        }
                        else
                        {
                            // Fallback to PdfPig's normal page.Text
                            // extraction if no words were returned.
                            string pageText = page.Text;

                            if (!string.IsNullOrWhiteSpace(pageText))
                            {
                                extractedText.AppendLine(pageText);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "An error occurred while extracting text from the PDF.",
                        ex);
                }

                string result = extractedText.ToString().Trim();

                if (string.IsNullOrWhiteSpace(result))
                {
                    throw new InvalidOperationException(
                        "The PDF was opened successfully, but no readable text " +
                        "could be extracted from it.");
                }

                return result;
            });
        }
    }
}
