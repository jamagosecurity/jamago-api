namespace Jama.Application.Common.Interfaces;

public sealed record InvoicePdfField(string Label, string Value);

public sealed record InvoicePdfSection(string Title, IReadOnlyList<InvoicePdfField> Fields);

public sealed record InvoicePdfCamera(
    string Brand,
    string Model,
    int Quantity,
    string Location,
    string Remarks);

public sealed record InvoicePdfModel(
    string InvoiceNumber,
    string DiaNumber,
    string ClientName,
    string ClientNumber,
    string ClientLocation,
    int Quarter,
    DateTime GeneratedAt,
    string TechnicianName,
    IReadOnlyList<InvoicePdfCamera> Cameras,
    IReadOnlyList<InvoicePdfSection> Sections);

public interface IInvoicePdfGenerator
{
    byte[] Generate(InvoicePdfModel model);
}
