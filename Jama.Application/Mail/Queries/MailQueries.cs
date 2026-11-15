using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Application.Options;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jama.Application.Mail.Queries;

/// <summary>Combined list across all mailboxes, newest first. Filter by mailbox, folder
/// (Inbox = coming in, Sent = going out) and a search over sender, recipient and subject.</summary>
public sealed record GetMailMessagesQuery(string? Account, string? Folder, string? Search, int Page = 1, int PageSize = 50, bool UnreadOnly = false)
    : IRequest<ApiResult<MailPageDto>>;

public sealed class GetMailMessagesQueryHandler(IApplicationDbContext context, IOptions<MailSettings> options)
    : IRequestHandler<GetMailMessagesQuery, ApiResult<MailPageDto>>
{
    public async Task<ApiResult<MailPageDto>> Handle(GetMailMessagesQuery r, CancellationToken ct)
    {
        // Only mailboxes still in the settings: a mailbox taken off the list disappears with its mail.
        var keys = options.Value.Accounts.Select(a => a.Key).ToList();
        var q = context.MailMessages.AsNoTracking().Where(m => keys.Contains(m.AccountKey));
        if (!string.IsNullOrWhiteSpace(r.Account)) q = q.Where(m => m.AccountKey == r.Account);
        if (!string.IsNullOrWhiteSpace(r.Folder)) q = q.Where(m => m.Folder == r.Folder);
        if (r.UnreadOnly) q = q.Where(m => !m.IsRead);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            q = q.Where(m => m.Subject.ToLower().Contains(term) || m.FromAddress.ToLower().Contains(term)
                          || m.FromName.ToLower().Contains(term) || m.ToAddresses.ToLower().Contains(term)
                          || m.Snippet.ToLower().Contains(term));
        }

        var size = Math.Clamp(r.PageSize, 1, 100);
        var page = Math.Max(1, r.Page);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * size).Take(size)
            .Select(m => new MailListItemDto(m.Id, m.AccountKey, m.AccountAddress, m.Folder, m.FromName,
                m.FromAddress, m.ToAddresses, m.Subject, m.SentAt, m.Snippet, m.HasAttachments, m.IsRead))
            .ToListAsync(ct);
        return ApiResult<MailPageDto>.Success(new MailPageDto(items, total));
    }
}

public sealed record GetMailMessageQuery(Guid Id) : IRequest<ApiResult<MailDetailDto>>;

public sealed class GetMailMessageQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMailMessageQuery, ApiResult<MailDetailDto>>
{
    public async Task<ApiResult<MailDetailDto>> Handle(GetMailMessageQuery r, CancellationToken ct)
    {
        var m = await context.MailMessages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (m is null) return ApiResult<MailDetailDto>.Failure("Message not found.");
        return ApiResult<MailDetailDto>.Success(new MailDetailDto(m.Id, m.AccountKey, m.AccountAddress, m.Folder,
            m.FromName, m.FromAddress, m.ToAddresses, m.Subject, m.SentAt, m.BodyText, m.BodyHtml,
            m.HasAttachments, m.AttachmentNames, m.IsRead));
    }
}

/// <summary>One row per configured mailbox, so the page can show tabs, counts and which one is failing.</summary>
public sealed record GetMailAccountsQuery : IRequest<ApiResult<IReadOnlyList<MailAccountDto>>>;

public sealed class GetMailAccountsQueryHandler(IApplicationDbContext context, IOptions<MailSettings> options)
    : IRequestHandler<GetMailAccountsQuery, ApiResult<IReadOnlyList<MailAccountDto>>>
{
    public async Task<ApiResult<IReadOnlyList<MailAccountDto>>> Handle(GetMailAccountsQuery r, CancellationToken ct)
    {
        var counts = await context.MailMessages.AsNoTracking()
            .GroupBy(m => new { m.AccountKey, m.Folder })
            .Select(g => new { g.Key.AccountKey, g.Key.Folder, N = g.Count(), Unread = g.Count(m => !m.IsRead) })
            .ToListAsync(ct);
        var states = await context.MailAccountStates.AsNoTracking().ToListAsync(ct);

        var cards = new Dictionary<string, (string Name, string Title, string Mobile)>();
        foreach (var a in options.Value.Accounts) cards[a.Key] = await EmailSignature.ResolveAsync(context, a, ct);

        var archive = options.Value.Accounts.FirstOrDefault(a =>
            string.Equals(a.Key, options.Value.ArchiveKey, StringComparison.OrdinalIgnoreCase) && a.IsConfigured);

        var list = options.Value.Accounts.Select(a =>
        {
            var mine = states.Where(s => s.AccountKey == a.Key).ToList();
            return new MailAccountDto(
                a.Key, a.Address, a.IsConfigured,
                counts.Where(c => c.AccountKey == a.Key && c.Folder == "Inbox").Sum(c => c.N),
                counts.Where(c => c.AccountKey == a.Key && c.Folder == "Sent").Sum(c => c.N),
                mine.Max(s => s.LastSyncAt),
                mine.Select(s => s.LastError).FirstOrDefault(e => e is not null),
                EmailSignature.Text(a, cards[a.Key]),
                EmailSignature.Html(a, cards[a.Key]),
                cards[a.Key].Name,
                counts.Where(c => c.AccountKey == a.Key && c.Folder == "Inbox").Sum(c => c.Unread),
                !a.IsConfigured && archive is not null);
        }).ToList();
        return ApiResult<IReadOnlyList<MailAccountDto>>.Success(list);
    }
}
