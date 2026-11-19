using Jama.Application.MoiJobs.Services;
using Jama.Application.Common.Interfaces;
using Jama.Application.Maintenance;
using Jama.Domain.Entities;

namespace Jama.Application.MoiJobs;

internal static class MoiJobBill
{
    internal static JobBillModel ToModel(MoiJob job) => new(
        job.JobNumber,
        job.CustomerName,
        job.CustomerNumber,
        job.Location,
        job.ClosedAt ?? job.CompletedAt ?? DateTime.UtcNow,
        job.Lines.OrderBy(l => l.SortOrder)
            .Select(l => new JobBillLine(MoiServiceCategories.Label(l.Category), l.IssueType, l.Quantity, l.UnitFee, l.LineTotal))
            .ToList(),
        job.WorkTotal,
        job.Discount,
        IsSent(job) ? job.AmountPayable : job.WorkTotal - job.Discount,
        job.BillRevision);

    /// <summary>The bill has gone to the client at least once: the discount and amount payable are fixed.</summary>
    internal static bool IsSent(MoiJob job) => job.Status is "Sent" or "ChangesRequested" or "Closed";

    internal static string FileName(MoiJob job) => $"{job.JobNumber} Bill.pdf";
}
