using System.Text;
using Jama.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Jama.Infrastructure.Documents;

/// <summary>Text of a PDF, page by page, in reading order (PdfPig). A file that is not a PDF, is locked, or has no text
/// layer gives null, never an error.</summary>
public sealed class PdfPigTextReader(ILogger<PdfPigTextReader> logger) : IPdfTextReader
{
    public string? ReadText(byte[] pdf)
    {
        try
        {
            using var document = PdfDocument.Open(pdf);
            var text = new StringBuilder();
            foreach (var page in document.GetPages().Take(10))
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
            var result = text.ToString();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Could not read the text of an uploaded PDF.");
            return null;
        }
    }
}
