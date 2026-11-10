using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Options;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.NonMoiJobs.Queries;

public sealed record NonMoiJobBillDto(byte[] Content, string FileName);

/// <summary>The bill as a PDF, for staff who manage Non-MOI jobs — a preview of what the client will get.</summary>
public sealed record GetNonMoiJobBillQuery(Guid Id) : IRequest<ApiResult<NonMoiJobBillDto>>;

public sealed class GetNonMoiJobBillQueryHandler(IApplicationDbContext context, IJobBillPdfGenerator pdf)
    : IRequestHandler<GetNonMoiJobBillQuery, ApiResult<NonMoiJobBillDto>>
{
    public async Task<ApiResult<NonMoiJobBillDto>> Handle(GetNonMoiJobBillQuery request, CancellationToken cancellationToken)
    {
        var job = await context.NonMoiJobs.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (job is null) return ApiResult<NonMoiJobBillDto>.Failure("Job not found.");
        if (job.Status == "Assigned") return ApiResult<NonMoiJobBillDto>.Failure("There is no bill until the technician completes the job.");
        return ApiResult<NonMoiJobBillDto>.Success(new NonMoiJobBillDto(pdf.Generate(NonMoiJobBill.ToModel(job)), NonMoiJobBill.FileName(job)));
    }
}

/// <summary>The same bill for the client, found by the unguessable token in the link they were sent. Only a job that
/// has actually been sent has a token, so a link cannot be guessed or forged.</summary>
public sealed record GetNonMoiJobBillByTokenQuery(string Token) : IRequest<ApiResult<NonMoiJobBillDto>>;

public sealed class GetNonMoiJobBillByTokenQueryHandler(IApplicationDbContext context, IJobBillPdfGenerator pdf)
    : IRequestHandler<GetNonMoiJobBillByTokenQuery, ApiResult<NonMoiJobBillDto>>
{
    public async Task<ApiResult<NonMoiJobBillDto>> Handle(GetNonMoiJobBillByTokenQuery request, CancellationToken cancellationToken)
    {
        var token = request.Token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length < 24)
            return ApiResult<NonMoiJobBillDto>.Failure("Bill not found.");

        var job = await context.NonMoiJobs.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.BillToken == token, cancellationToken);
        if (job is null || !NonMoiJobBill.IsSent(job))
            return ApiResult<NonMoiJobBillDto>.Failure("Bill not found.");
        return ApiResult<NonMoiJobBillDto>.Success(new NonMoiJobBillDto(pdf.Generate(NonMoiJobBill.ToModel(job)), NonMoiJobBill.FileName(job)));
    }
}

/// <summary>The company mailboxes a bill can be sent from: key and address only, never a password. Listed here rather
/// than through the Company mail screen because the person closing a deal may not be allowed to read the mailboxes.</summary>
public sealed record GetBillMailboxesQuery : IRequest<ApiResult<IReadOnlyList<BillMailboxDto>>>;

public sealed record BillMailboxDto(string Key, string Address);

public sealed class GetBillMailboxesQueryHandler(IOptions<MailSettings> options)
    : IRequestHandler<GetBillMailboxesQuery, ApiResult<IReadOnlyList<BillMailboxDto>>>
{
    public Task<ApiResult<IReadOnlyList<BillMailboxDto>>> Handle(GetBillMailboxesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(ApiResult<IReadOnlyList<BillMailboxDto>>.Success(
            options.Value.Accounts.Where(a => a.IsConfigured).Select(a => new BillMailboxDto(a.Key, a.Address)).ToList()));
}
