using Jama.Application.Common.Interfaces;
using Jama.Application.Maintenance;
using Jama.Domain.Entities;

namespace Jama.Application.NonMoiJobs;

internal static class NonMoiJobBill
{
    internal static JobBillModel ToModel(NonMoiJob job) => new(
        job.JobNumber,
        job.CustomerName,
        job.CustomerNumber,
        job.Location,
        job.ClosedAt ?? job.CompletedAt ?? DateTime.UtcNow,
        job.Lines.OrderBy(l => l.SortOrder)
            .Select(l => new JobBillLine(MaintenanceCategories.Label(l.Category), l.IssueType, l.Quantity, l.UnitFee, l.LineTotal))
            .ToList(),
        job.WorkTotal,
        job.Discount,
        IsSent(job) ? job.AmountPayable : job.WorkTotal - job.Discount,
        job.BillRevision);

    /// <summary>The bill has gone to the client at least once: the discount and amount payable are fixed.</summary>
    internal static bool IsSent(NonMoiJob job) => job.Status is "Sent" or "ChangesRequested" or "Closed";

    internal static string FileName(NonMoiJob job) => $"{job.JobNumber} Bill.pdf";
}
