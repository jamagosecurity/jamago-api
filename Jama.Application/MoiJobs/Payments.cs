using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs;

/// <summary>
/// Records money received for a MOI job. Cash: the amount and who collected it. Online (bank transfer): the
/// amount and the transaction receipt, which is required. The day defaults to today (Doha) and may be any earlier
/// day. A job can be paid in parts; the total received can never be more than the bill.
/// </summary>
public sealed record AddMoiJobPaymentCommand : IRequest<ApiResult<MoiJobPaymentDto>>
{
    public Guid JobId { get; init; }
    public string Method { get; init; } = "Cash";
    public decimal Amount { get; init; }
    public DateOnly? ReceivedOn { get; init; }
    public Guid? CollectedById { get; init; }
    public string? Note { get; init; }
    public string? ReceiptFileName { get; init; }
    public string? ReceiptContentType { get; init; }
    public long ReceiptSize { get; init; }
    public Stream? Receipt { get; init; }
}

public sealed class AddMoiJobPaymentCommandHandler(
    IApplicationDbContext context, IFileStorage storage, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<AddMoiJobPaymentCommand, ApiResult<MoiJobPaymentDto>>
{
    internal static readonly string[] ReceiptTypes = [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic"];
    internal const long MaxReceiptBytes = 10 * 1024 * 1024;

    public async Task<ApiResult<MoiJobPaymentDto>> Handle(AddMoiJobPaymentCommand r, CancellationToken ct)
    {
        static ApiResult<MoiJobPaymentDto> Fail(string m) => ApiResult<MoiJobPaymentDto>.Failure(m);

        var method = (r.Method ?? "").Trim();
        if (method.Equals("cash", StringComparison.OrdinalIgnoreCase)) method = "Cash";
        else if (method.Equals("online", StringComparison.OrdinalIgnoreCase)) method = "Online";
        else return Fail("Choose Cash or Online.");

        if (r.Amount <= 0) return Fail("Enter the amount received.");
        if (decimal.Round(r.Amount, 2) != r.Amount) return Fail("The amount can have at most 2 decimals.");

        var job = await context.MoiJobs.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == r.JobId, ct);
        if (job is null) return Fail("Job not found.");
        if (job.Status == "Assigned") return Fail("The technician has not finished this job yet, so there is nothing to pay.");

        var due = MoiJobRules.Payable(job) - job.Payments.Sum(p => p.Amount);
        if (due <= 0) return Fail("This job is already fully paid.");
        if (r.Amount > due) return Fail($"That is more than the balance. Only QAR {due:N2} is left to pay.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));
        var day = r.ReceivedOn ?? today;
        if (day > today) return Fail("The date cannot be in the future.");
        if (day < today.AddYears(-1)) return Fail("The date is more than a year ago — check it.");

        var hasReceipt = r.Receipt is not null && r.ReceiptSize > 0;
        if (method == "Online" && !hasReceipt) return Fail("Upload the transaction receipt for an online payment.");

        string? collectedByName = null;
        if (r.CollectedById is { } who)
        {
            collectedByName = await context.AdminUsers.AsNoTracking().Where(u => u.Id == who).Select(u => u.FullName).FirstOrDefaultAsync(ct);
            if (collectedByName is null) return Fail("That staff member was not found.");
        }

        var payment = new MoiJobPayment
        {
            Id = Guid.CreateVersion7(),
            MoiJobId = job.Id,
            Method = method,
            Amount = r.Amount,
            ReceivedOn = day,
            CollectedById = method == "Cash" ? r.CollectedById : null,
            CollectedByName = method == "Cash" ? collectedByName : null,
            Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim()[..Math.Min(r.Note.Trim().Length, 500)],
            RecordedById = actor.UserId,
            RecordedByName = actor.DisplayName ?? "Admin",
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };

        if (hasReceipt)
        {
            var name = Path.GetFileName(r.ReceiptFileName ?? "receipt").Trim();
            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (!ReceiptTypes.Contains(ext)) return Fail("The receipt must be a photo (JPG, PNG, WEBP, HEIC) or a PDF.");
            if (r.ReceiptSize > MaxReceiptBytes) return Fail("The receipt is too big. The limit is 10 MB.");

            var header = new byte[FileSignatures.HeaderLength];
            var read = 0;
            while (read < header.Length)
            {
                var n = await r.Receipt!.ReadAsync(header.AsMemory(read), ct);
                if (n == 0) break;
                read += n;
            }
            if (!FileSignatures.Matches(ext, header.AsSpan(0, read)))
                return Fail("This file does not open as a photo or PDF. Check it and try again.");

            Stream content;
            if (r.Receipt!.CanSeek) content = Rewound(r.Receipt);
            else
            {
                // At most 10 MB, so holding it in memory to put the first bytes back is fine.
                var buffer = new MemoryStream();
                buffer.Write(header, 0, read);
                await r.Receipt.CopyToAsync(buffer, ct);
                buffer.Position = 0;
                content = buffer;
            }
            var key = string.Join('/', "moi-payments", job.Id.ToString(), $"{payment.Id}{ext}");
            await storage.SaveAsync(content, key, ct);
            payment.ReceiptFileName = name;
            payment.ReceiptStorageKey = key;
            payment.ReceiptContentType = string.IsNullOrWhiteSpace(r.ReceiptContentType) ? "application/octet-stream" : r.ReceiptContentType;
        }

        context.MoiJobPayments.Add(payment);
        var paid = job.Payments.Sum(p => p.Amount) + payment.Amount;
        var how = method == "Cash"
            ? $"Cash QAR {payment.Amount:N2}" + (collectedByName is null ? " received at the office" : $" collected by {collectedByName}")
            : $"Online transfer QAR {payment.Amount:N2}";
        context.MoiJobEvents.Add(new MoiJobEvent
        {
            MoiJobId = job.Id, Action = "Payment", ActorName = payment.RecordedByName,
            Note = $"{how} on {day:d MMM yyyy}. Paid so far QAR {paid:N2} of QAR {MoiJobRules.Payable(job):N2}.",
            Revision = job.BillRevision, AmountPayable = payment.Amount, CreatedAt = payment.CreatedAt,
        });
        await context.SaveChangesAsync(ct);
        return ApiResult<MoiJobPaymentDto>.Success(MoiJobRules.ToPaymentDto(payment));
    }

    private static Stream Rewound(Stream s) { s.Position = 0; return s; }
}

/// <summary>Removes a payment recorded by mistake. Super admin only, and it stays in the job's history.</summary>
public sealed record DeleteMoiJobPaymentCommand(Guid PaymentId) : IRequest<ApiResult<bool>>;

public sealed class DeleteMoiJobPaymentCommandHandler(
    IApplicationDbContext context, IFileStorage storage, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<DeleteMoiJobPaymentCommand, ApiResult<bool>>
{
    public async Task<ApiResult<bool>> Handle(DeleteMoiJobPaymentCommand r, CancellationToken ct)
    {
        if (!actor.IsSuperAdmin) return ApiResult<bool>.Failure("Only the super admin can remove a payment.");
        var p = await context.MoiJobPayments.Include(x => x.MoiJob).FirstOrDefaultAsync(x => x.Id == r.PaymentId, ct);
        if (p is null) return ApiResult<bool>.Failure("Payment not found.");
        if (p.CashHandoverId is not null)
            return ApiResult<bool>.Failure("This cash was already handed over to the accountant, so it cannot be removed.");

        context.MoiJobPayments.Remove(p);
        context.MoiJobEvents.Add(new MoiJobEvent
        {
            MoiJobId = p.MoiJobId, Action = "PaymentRemoved", ActorName = actor.DisplayName ?? "Super admin",
            Note = $"{p.Method} QAR {p.Amount:N2} of {p.ReceivedOn:d MMM yyyy} removed.",
            Revision = p.MoiJob.BillRevision, AmountPayable = p.Amount, CreatedAt = clock.GetUtcNow().UtcDateTime,
        });
        await context.SaveChangesAsync(ct);
        if (p.ReceiptStorageKey is not null) await storage.DeleteAsync(p.ReceiptStorageKey, ct);
        return ApiResult<bool>.Success(true);
    }
}

public sealed record MoiPaymentReceiptContent(Stream Content, string FileName, string ContentType);

/// <summary>The receipt file of an online payment.</summary>
public sealed record GetMoiJobPaymentReceiptQuery(Guid PaymentId) : IRequest<ApiResult<MoiPaymentReceiptContent>>;

public sealed class GetMoiJobPaymentReceiptQueryHandler(IApplicationDbContext context, IFileStorage storage)
    : IRequestHandler<GetMoiJobPaymentReceiptQuery, ApiResult<MoiPaymentReceiptContent>>
{
    public async Task<ApiResult<MoiPaymentReceiptContent>> Handle(GetMoiJobPaymentReceiptQuery r, CancellationToken ct)
    {
        var p = await context.MoiJobPayments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.PaymentId, ct);
        if (p?.ReceiptStorageKey is null) return ApiResult<MoiPaymentReceiptContent>.Failure("No receipt for this payment.");
        var stream = await storage.OpenReadAsync(p.ReceiptStorageKey, ct);
        if (stream is null) return ApiResult<MoiPaymentReceiptContent>.Failure("The receipt file is missing.");
        return ApiResult<MoiPaymentReceiptContent>.Success(new(stream, p.ReceiptFileName ?? "receipt", p.ReceiptContentType ?? "application/octet-stream"));
    }
}

public sealed record MoiCollectionRowDto(
    Guid PaymentId, Guid JobId, string JobNumber, string CustomerName, string Method, decimal Amount, DateOnly ReceivedOn,
    string? CollectedByName, string TechnicianName, string? Note, bool HasReceipt, string RecordedByName, DateTime RecordedAt);

public sealed record MoiCollectionsDto(DateOnly From, DateOnly To, decimal Cash, decimal Online, decimal Total, IReadOnlyList<MoiCollectionRowDto> Rows);

/// <summary>Every payment received between two days (Doha dates, both included) with cash and online totals.</summary>
public sealed record GetMoiCollectionsQuery(DateOnly? From, DateOnly? To) : IRequest<ApiResult<MoiCollectionsDto>>;

public sealed class GetMoiCollectionsQueryHandler(IApplicationDbContext context, TimeProvider clock)
    : IRequestHandler<GetMoiCollectionsQuery, ApiResult<MoiCollectionsDto>>
{
    public async Task<ApiResult<MoiCollectionsDto>> Handle(GetMoiCollectionsQuery r, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));
        var from = r.From ?? today;
        var to = r.To ?? from;
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) return ApiResult<MoiCollectionsDto>.Failure("Pick at most one year.");

        var rows = await context.MoiJobPayments.AsNoTracking()
            .Where(p => p.ReceivedOn >= from && p.ReceivedOn <= to)
            .OrderByDescending(p => p.ReceivedOn).ThenByDescending(p => p.CreatedAt)
            .Select(p => new MoiCollectionRowDto(p.Id, p.MoiJobId, p.MoiJob.JobNumber, p.MoiJob.CustomerName, p.Method,
                p.Amount, p.ReceivedOn, p.CollectedByName, p.MoiJob.AssignedToName, p.Note, p.ReceiptStorageKey != null,
                p.RecordedByName, p.CreatedAt))
            .ToListAsync(ct);
        var cash = rows.Where(x => x.Method == "Cash").Sum(x => x.Amount);
        var online = rows.Where(x => x.Method == "Online").Sum(x => x.Amount);
        return ApiResult<MoiCollectionsDto>.Success(new(from, to, cash, online, cash + online, rows));
    }
}
