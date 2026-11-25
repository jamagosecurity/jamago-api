namespace Jama.Application.Common.Interfaces;

/// <summary>
/// The short proforma invoice of a quotation: one table of item, brand, model, unit, quantity and price, grouped by
/// the quotation's sections — no descriptions, no pictures. Each format is made from the same <see cref="ProformaModel"/>.
/// </summary>
public interface IProformaPdfGenerator
{
    byte[] Generate(ProformaModel model);
}

/// <summary>The proforma invoice as an Excel workbook (.xlsx), laid out like the PDF.</summary>
public interface IProformaExcelGenerator
{
    byte[] Generate(ProformaModel model);
}

/// <summary>The proforma invoice as a Word document (.docx), laid out like the PDF.</summary>
public interface IProformaWordGenerator
{
    byte[] Generate(ProformaModel model);
}
