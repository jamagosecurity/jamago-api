namespace Jama.Application.Common.Interfaces;

/// <summary>Reads the text inside a PDF (an Uber receipt, say). Null when the file is not a readable PDF — a scan or a
/// photo has no text to read, and the person types the figures instead.</summary>
public interface IPdfTextReader
{
    string? ReadText(byte[] pdf);
}
