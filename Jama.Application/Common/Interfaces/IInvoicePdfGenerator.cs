namespace Jama.Application.Common.Interfaces;

public sealed record InvoicePdfModel(
    string InvoiceNumber,
    string DiaNumber,
    string ClientName,
    string ClientNumber,
    string ClientLocation,
    int Quarter,
    DateTime GeneratedAt,
    string TechnicianName);

public interface IInvoicePdfGenerator
{
    byte[] Generate(InvoicePdfModel model);
}
