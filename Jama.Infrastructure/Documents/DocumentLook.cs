using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// What the exported documents share — the logo, the QR code, the palette and how numbers and dates are written — so
/// a PDF, its Excel workbook and its Word document look like one document.
/// </summary>
internal static class DocumentLook
{
    public static readonly byte[] Logo = LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");

    /// <summary>The QR code to the website, printed in the corner of the quotation's cover and letter.</summary>
    public static readonly byte[] Qr = LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-qr.png");

    /// <summary>The logo's size in pixels, read from the PNG header (width, height).</summary>
    public static readonly (int Width, int Height) LogoSize = PngSize(Logo);

    // Palette, as hex without '#'.
    public const string Brand = "2594D2";
    public const string BrandStrong = "1A7BB5";
    public const string Ink = "0F2846";
    public const string Muted = "5F7487";
    public const string Border = "9FB6CA";
    public const string HeadBg = "D9E9F6";
    public const string SectionBg = "EAF3FA";
    public const string DraftRed = "D1273D";
    public const string Accent = "C2660F";
    public const string InkSoft = "3E5872";
    public const string PanelBg = "F2F8FC";

    public static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
    public static string Date(DateOnly value) => value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
    public static string Qty(decimal value) => value.ToString(value % 1 == 0 ? "0" : "0.##", CultureInfo.InvariantCulture);
    public static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    /// <summary>"P.O. No. X dated dd-MM-yyyy", or null when there is no P.O.</summary>
    public static string? PoNote(string? poNumber, DateOnly? poDate) =>
        string.IsNullOrWhiteSpace(poNumber) ? null
        : poDate is { } date ? $"P.O. No. {poNumber} dated {Date(date)}"
        : $"P.O. No. {poNumber}";

    private static byte[] LoadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return [];
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public static (int, int) PngSize(byte[] png) =>
        png.Length >= 24
            ? (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)))
            : (1, 1);
}
