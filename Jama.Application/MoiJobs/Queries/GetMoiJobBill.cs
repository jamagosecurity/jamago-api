using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Options;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.MoiJobs.Queries;

public sealed record MoiJobBillDto(byte[] Content, string FileName);

/// <summary>The bill as a PDF, for staff who manage MOI jobs — a preview of what the client will get.</summary>
public sealed record GetMoiJobBillQuery(Guid Id) : IRequest<ApiResult<MoiJobBillDto>>;

public sealed class GetMoiJobBillQueryHandler(IApplicationDbContext context, IJobBillPdfGenerator pdf)
    : IRequestHandler<GetMoiJobBillQuery, ApiResult<MoiJobBillDto>>
{
    public async Task<ApiResult<MoiJobBillDto>> Handle(GetMoiJobBillQuery request, CancellationToken cancellationToken)
    {
        var job = await context.MoiJobs.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (job is null) return ApiResult<MoiJobBillDto>.Failure("Job not found.");
        if (job.Status == "Assigned") return ApiResult<MoiJobBillDto>.Failure("There is no bill until the technician completes the job.");
        return ApiResult<MoiJobBillDto>.Success(new MoiJobBillDto(pdf.Generate(MoiJobBill.ToModel(job)), MoiJobBill.FileName(job)));
    }
}

/// <summary>The same bill for the client, found by the unguessable token in the link they were sent. Only a job that
/// has actually been sent has a token, so a link cannot be guessed or forged.</summary>
public sealed record GetMoiJobBillByTokenQuery(string Token) : IRequest<ApiResult<MoiJobBillDto>>;

public sealed class GetMoiJobBillByTokenQueryHandler(IApplicationDbContext context, IJobBillPdfGenerator pdf)
    : IRequestHandler<GetMoiJobBillByTokenQuery, ApiResult<MoiJobBillDto>>
{
    public async Task<ApiResult<MoiJobBillDto>> Handle(GetMoiJobBillByTokenQuery request, CancellationToken cancellationToken)
    {
        var token = request.Token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length < 24)
            return ApiResult<MoiJobBillDto>.Failure("Bill not found.");

        var job = await context.MoiJobs.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.BillToken == token, cancellationToken);
        if (job is null || !MoiJobBill.IsSent(job))
            return ApiResult<MoiJobBillDto>.Failure("Bill not found.");
        return ApiResult<MoiJobBillDto>.Success(new MoiJobBillDto(pdf.Generate(MoiJobBill.ToModel(job)), MoiJobBill.FileName(job)));
    }
}

/// <summary>How a bill can be sent from this server: the company mailboxes (key and address only, never a password) and whether
/// WhatsApp is set up. Listed here rather than through the Company mail screen because the person sending a bill may not be
/// allowed to read the mailboxes — and so the page can offer only what will actually work, instead of letting someone tick
/// WhatsApp and be refused.</summary>
public sealed record GetBillMailboxesQuery : IRequest<ApiResult<BillSendOptionsDto>>;

public sealed record BillMailboxDto(string Key, string Address);

public sealed record BillSendOptionsDto(IReadOnlyList<BillMailboxDto> Mailboxes, bool WhatsAppAvailable);

public sealed class GetBillMailboxesQueryHandler(IOptions<MailSettings> options, IOptions<TwilioSettings> twilio)
    : IRequestHandler<GetBillMailboxesQuery, ApiResult<BillSendOptionsDto>>
{
    public Task<ApiResult<BillSendOptionsDto>> Handle(GetBillMailboxesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(ApiResult<BillSendOptionsDto>.Success(new BillSendOptionsDto(
            options.Value.Accounts.Where(a => a.IsConfigured).Select(a => new BillMailboxDto(a.Key, a.Address)).ToList(),
            twilio.Value.IsConfigured)));
}
