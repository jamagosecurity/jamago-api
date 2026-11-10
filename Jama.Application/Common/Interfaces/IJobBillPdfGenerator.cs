namespace Jama.Application.Common.Interfaces;

public sealed record JobBillLine(string Category, string Issue, int Quantity, decimal UnitFee, decimal LineTotal);

/// <summary>Everything the bill for a Non-MOI job prints.</summary>
public sealed record JobBillModel(
    string JobNumber,
    string CustomerName,
    string CustomerNumber,
    string Location,
    DateTime Date,
    IReadOnlyList<JobBillLine> Lines,
    decimal Total,
    decimal Discount,
    decimal AmountPayable,
    int Revision = 1);

public interface IJobBillPdfGenerator
{
    byte[] Generate(JobBillModel model);
}
