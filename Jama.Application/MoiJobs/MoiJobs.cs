using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Dia;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.MoiJobs;

public sealed record MoiJobDto(
    Guid Id,
    string JobNumber,
    string CustomerName,
    string CustomerNumber,
    string Location,
    double? Latitude,
    double? Longitude,
    string DescriptionEn,
    string? DescriptionAr,
    Guid AssignedToId,
    string AssignedToName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Status = "Assigned",
    DateTime? CompletedAt = null,
    decimal WorkTotal = 0,
    string? CompletionNotes = null,
    IReadOnlyList<MoiJobLineDto>? Lines = null,
    string? CustomerEmail = null,
    Guid AssignedById = default,
    string AssignedByName = "",
    decimal Discount = 0,
    decimal AmountPayable = 0,
    DateTime? ClosedAt = null,
    string? ClosedByName = null,
    DateTime? EmailSentAt = null,
    DateTime? WhatsAppSentAt = null,
    int BillRevision = 1,
    string? ClientDecision = null,
    string? ClientMessage = null,
    string? ClientResponderName = null,
    DateTime? ClientRespondedAt = null,
    IReadOnlyList<MoiJobEventDto>? Events = null,
    DateTime? ScheduledAt = null,
    decimal AmountPaid = 0,
    IReadOnlyList<MoiJobPaymentDto>? Payments = null);

/// <summary>One payment on a job. HasReceipt: a receipt file can be downloaded.</summary>
public sealed record MoiJobPaymentDto(
    Guid Id, string Method, decimal Amount, DateOnly ReceivedOn, string? CollectedByName, string? Note,
    bool HasReceipt, string? ReceiptFileName, string RecordedByName, DateTime RecordedAt);

public sealed record MoiJobEventDto(string Action, string ActorName, string? Note, int Revision, decimal AmountPayable, DateTime At);

public sealed record MoiJobLineDto(string Category, string IssueType, int Quantity, decimal UnitFee, decimal LineTotal);

/// <summary>What Create and Update receive — the same fields either way.</summary>
public interface IMoiJobRequest : ISiteCoordinateRequest
{
    string? CustomerName { get; }
    string? CustomerNumber { get; }
    string? Location { get; }
    string? DescriptionEn { get; }
    string? DescriptionAr { get; }
    Guid AssignedToId { get; }

    /// <summary>Optional client email for sending the bill. Null when the request does not carry one.</summary>
    string? CustomerEmail => null;

    /// <summary>When the technician should visit. Sent with the time zone (Doha, +03:00).</summary>
    DateTimeOffset? ScheduledAt => null;
}

internal static class MoiJobRules
{
    internal const string Prefix = "MOJ-";

    internal static MoiJobDto ToDto(MoiJob job) => new(
        job.Id, job.JobNumber, job.CustomerName, job.CustomerNumber, job.Location,
        job.Latitude, job.Longitude, job.DescriptionEn, job.DescriptionAr,
        job.AssignedToId, job.AssignedToName, job.CreatedAt, job.UpdatedAt,
        job.Status, job.CompletedAt, job.WorkTotal, job.CompletionNotes,
        job.Lines.OrderBy(l => l.SortOrder)
            .Select(l => new MoiJobLineDto(l.Category, l.IssueType, l.Quantity, l.UnitFee, l.LineTotal)).ToList(),
        job.CustomerEmail, job.AssignedById, job.AssignedByName, job.Discount,
        // Until the bill has been sent the amount payable is simply the total less any discount typed so far.
        MoiJobBill.IsSent(job) ? job.AmountPayable : job.WorkTotal - job.Discount,
        job.ClosedAt, job.ClosedByName, job.EmailSentAt, job.WhatsAppSentAt,
        job.BillRevision, job.ClientDecision, job.ClientMessage, job.ClientResponderName, job.ClientRespondedAt,
        job.Events.OrderBy(e => e.CreatedAt)
            .Select(e => new MoiJobEventDto(e.Action, e.ActorName, e.Note, e.Revision, e.AmountPayable, e.CreatedAt)).ToList(),
        job.ScheduledAt,
        job.Payments.Sum(p => p.Amount),
        job.Payments.OrderBy(p => p.ReceivedOn).ThenBy(p => p.CreatedAt).Select(ToPaymentDto).ToList());

    internal static MoiJobPaymentDto ToPaymentDto(MoiJobPayment p) => new(
        p.Id, p.Method, p.Amount, p.ReceivedOn, p.CollectedByName, p.Note,
        p.ReceiptStorageKey is not null, p.ReceiptFileName, p.RecordedByName, p.CreatedAt);

    /// <summary>What the client owes for the job: the sent bill, or until then the total less the discount.</summary>
    internal static decimal Payable(MoiJob job) =>
        MoiJobBill.IsSent(job) ? job.AmountPayable : job.WorkTotal - job.Discount;

    /// <summary>Next reference, derived from the highest issued so a deleted
    /// job never lets the next one reuse its number.</summary>
    internal static async Task<string> NextNumberAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var used = await context.MoiJobs.AsNoTracking()
            .Where(x => x.JobNumber.StartsWith(Prefix))
            .Select(x => x.JobNumber)
            .ToListAsync(cancellationToken);

        var highest = used
            .Select(n => int.TryParse(n[Prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{Prefix}{highest + 1:D5}";
    }

    /// <summary>Staff who can be handed a job: the Technician department, with
    /// a login account that is switched on. Not <c>Staff.IsActive</c> — that only
    /// decides whether someone is shown on the public Our Team page, and says
    /// nothing about whether they can sign in to the portal the job appears in.</summary>
    internal static IQueryable<Staff> Technicians(IApplicationDbContext context) =>
        context.Staff.AsNoTracking().Where(s =>
            s.Department == "Technician" && s.AdminUserId != null && s.Account != null && s.Account.IsActive);

    /// <summary>The technician the job goes to, or an error.</summary>
    internal static async Task<(Staff? Technician, string? Error)> FindTechnicianAsync(
        IApplicationDbContext context, Guid adminUserId, CancellationToken cancellationToken)
    {
        var staff = await context.Staff.AsNoTracking().Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.AdminUserId == adminUserId, cancellationToken);
        if (staff is null)
            return (null, "That technician was not found.");
        if (!string.Equals(staff.Department, "Technician", StringComparison.OrdinalIgnoreCase))
            return (null, "Jobs can only be assigned to a technician.");
        if (staff.Account is not { IsActive: true })
            return (null, "That technician's account is switched off.");
        if (!await CanDoMoiJobsAsync(context, staff.Account, cancellationToken))
            return (null, $"{staff.FullName} cannot do MOI jobs yet — tick “Do MOI jobs (technician)” on their staff profile first.");
        return (staff, null);
    }

    /// <summary>Whether this account would see MOI jobs in the technician portal (moijob.work, explicit or from the role).</summary>
    internal static async Task<bool> CanDoMoiJobsAsync(IApplicationDbContext context, AdminUser account, CancellationToken cancellationToken)
    {
        var grants = await context.UserPermissions.AsNoTracking()
            .Where(p => p.AdminUserId == account.Id).Select(p => p.Permission).ToListAsync(cancellationToken);
        return Jama.Application.Common.Permissions.EffectiveFor(account.Role, grants).Contains(Jama.Application.Common.Permissions.MoiJobWork);
    }

    internal static void AddRulesTo<T>(AbstractValidator<T> validator) where T : IMoiJobRequest
    {
        validator.RuleFor(x => x.CustomerName).NotEmpty().WithMessage("Enter the customer's name.")
            .MinimumLength(2).WithMessage("The customer's name must be at least 2 characters.").MaximumLength(200);
        validator.RuleFor(x => x.CustomerNumber).NotEmpty().WithMessage("Enter the customer's number.")
            .Must(IsDialablePhone).WithMessage("Enter a valid phone number — 6 to 15 digits, e.g. +974 5550 1234.")
            .MaximumLength(30);
        validator.RuleFor(x => x.Location).NotEmpty().WithMessage("Enter the site location.")
            .MinimumLength(3).WithMessage("The location must be at least 3 characters.").MaximumLength(300);
        validator.RuleFor(x => x.DescriptionEn).NotEmpty().WithMessage("Describe the job in English.")
            .MinimumLength(3).WithMessage("The description must be at least 3 characters.").MaximumLength(2000);
        validator.RuleFor(x => x.DescriptionAr).MaximumLength(2000);
        validator.RuleFor(x => x.CustomerEmail)
            .Must(e => string.IsNullOrWhiteSpace(e) || Jama.Application.Mail.Commands.SendMailCommandValidator.IsAddress(e))
            .WithMessage("Enter a valid email address, like name@example.com.")
            .MaximumLength(320);
        validator.RuleFor(x => x.AssignedToId).NotEmpty().WithMessage("Choose the technician for this job.");
        validator.RuleFor(x => x.ScheduledAt)
            .NotNull().WithMessage("Choose the date and time of the visit.")
            .Must(at => at is null || (at.Value.Year >= 2024 && at.Value <= DateTimeOffset.UtcNow.AddYears(2)))
            .WithMessage("Choose a visit date within the next two years.");
        SiteCoordinates.AddRulesTo(validator);
    }

    /// <summary>An optional +, then digits with spaces, dashes or brackets between them; 6–15 digits (E.164).
    /// Mirrors phoneNumber() in the Angular form-validators util.</summary>
    internal static bool IsDialablePhone(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(text, @"^\+?[0-9\s\-()]+$")) return false;
        var digits = text.Count(char.IsAsciiDigit);
        return digits is >= 6 and <= 15;
    }

    internal static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
