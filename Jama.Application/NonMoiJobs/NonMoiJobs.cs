using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Dia;
using Jama.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.NonMoiJobs;

public sealed record NonMoiJobDto(
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
    IReadOnlyList<NonMoiJobLineDto>? Lines = null,
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
    IReadOnlyList<NonMoiJobEventDto>? Events = null);

public sealed record NonMoiJobEventDto(string Action, string ActorName, string? Note, int Revision, decimal AmountPayable, DateTime At);

public sealed record NonMoiJobLineDto(string Category, string IssueType, int Quantity, decimal UnitFee, decimal LineTotal);

/// <summary>What Create and Update receive — the same fields either way.</summary>
public interface INonMoiJobRequest : ISiteCoordinateRequest
{
    string? CustomerName { get; }
    string? CustomerNumber { get; }
    string? Location { get; }
    string? DescriptionEn { get; }
    string? DescriptionAr { get; }
    Guid AssignedToId { get; }

    /// <summary>Optional client email for sending the bill. Null when the request does not carry one.</summary>
    string? CustomerEmail => null;
}

internal static class NonMoiJobRules
{
    internal const string Prefix = "NMJ-";

    internal static NonMoiJobDto ToDto(NonMoiJob job) => new(
        job.Id, job.JobNumber, job.CustomerName, job.CustomerNumber, job.Location,
        job.Latitude, job.Longitude, job.DescriptionEn, job.DescriptionAr,
        job.AssignedToId, job.AssignedToName, job.CreatedAt, job.UpdatedAt,
        job.Status, job.CompletedAt, job.WorkTotal, job.CompletionNotes,
        job.Lines.OrderBy(l => l.SortOrder)
            .Select(l => new NonMoiJobLineDto(l.Category, l.IssueType, l.Quantity, l.UnitFee, l.LineTotal)).ToList(),
        job.CustomerEmail, job.AssignedById, job.AssignedByName, job.Discount,
        // Until the bill has been sent the amount payable is simply the total less any discount typed so far.
        NonMoiJobBill.IsSent(job) ? job.AmountPayable : job.WorkTotal - job.Discount,
        job.ClosedAt, job.ClosedByName, job.EmailSentAt, job.WhatsAppSentAt,
        job.BillRevision, job.ClientDecision, job.ClientMessage, job.ClientResponderName, job.ClientRespondedAt,
        job.Events.OrderBy(e => e.CreatedAt)
            .Select(e => new NonMoiJobEventDto(e.Action, e.ActorName, e.Note, e.Revision, e.AmountPayable, e.CreatedAt)).ToList());

    /// <summary>Next reference, derived from the highest issued so a deleted
    /// job never lets the next one reuse its number.</summary>
    internal static async Task<string> NextNumberAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var used = await context.NonMoiJobs.AsNoTracking()
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
        return (staff, null);
    }

    internal static void AddRulesTo<T>(AbstractValidator<T> validator) where T : INonMoiJobRequest
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
