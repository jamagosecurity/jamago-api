using Jama.Application.Common.Interfaces;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf.IO;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// Stamps "DRAFT — NOT APPROVED" across every page of an already-existing PDF.
///
/// This is a different job from BoqPdfGenerator's watermark: a quotation's PDF
/// is generated fresh on every request, so its draft stamp is just another
/// layer in that same render. A drawing's PDF is a file someone uploaded —
/// QuestPDF can only build new documents, not open and mark up one that
/// already exists — so this exists purely to open, draw on, and re-save it.
/// Used only for the one file type that can be opened this way; a DWG or DXF
/// stays behind the hard download block instead of getting a partial preview.
/// </summary>
public sealed class PdfWatermarker : IPdfWatermarker
{
    /// <summary>Stamps every page of <paramref name="pdfBytes"/> and returns
    /// the result as a new byte array. The input is read fully into memory —
    /// acceptable here because this only ever runs on a drawing's plotted PDF,
    /// not the much larger native CAD file.</summary>
    public byte[] Stamp(byte[] pdfBytes)
    {
        using var input = new MemoryStream(pdfBytes);
        using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        var font = new XFont("Helvetica", 40, XFontStyle.Bold);
        var brush = new XSolidBrush(XColor.FromArgb(70, 209, 39, 61));

        foreach (var page in document.Pages)
        {
            using var graphics = XGraphics.FromPdfPage(page);

            graphics.TranslateTransform(page.Width / 2, page.Height / 2);
            graphics.RotateTransform(-30);

            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Center,
                LineAlignment = XLineAlignment.Center,
            };

            graphics.DrawString("DRAFT — NOT APPROVED", font, brush, new XPoint(0, 0), format);
        }

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }
}
