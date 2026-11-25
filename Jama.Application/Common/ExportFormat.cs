namespace Jama.Application.Common;

/// <summary>The file formats a document can be downloaded in. Each format is made from the same document model, so
/// the three always agree.</summary>
public enum ExportFormat
{
    Pdf,
    Excel,
    Word,
}

public static class ExportFormats
{
    public static string Extension(this ExportFormat format) => format switch
    {
        ExportFormat.Excel => "xlsx",
        ExportFormat.Word => "docx",
        _ => "pdf",
    };

    public static string ContentType(this ExportFormat format) => format switch
    {
        ExportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ExportFormat.Word => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/pdf",
    };

    /// <summary>"pdf", "excel" or "word" (any case); null for anything else. Missing means PDF.</summary>
    public static ExportFormat? Parse(string? value) =>
        string.IsNullOrWhiteSpace(value) ? ExportFormat.Pdf
        : Enum.TryParse<ExportFormat>(value.Trim(), ignoreCase: true, out var format) && Enum.IsDefined(format) ? format
        : null;
}
