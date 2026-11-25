using FluentValidation;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Proformas.Commands.SaveProforma;

/// <summary>
/// Makes the proforma invoice of an approved quotation — giving it its PI number and date the first time — and saves
/// the client's P.O. number and date, and any customer details changed for this proforma. Saving again keeps the
/// number and date. <paramref name="Customer"/> null leaves the customer details as they are.
/// </summary>
public sealed record SaveProformaCommand(Guid BoqId, string? PoNumber, DateOnly? PoDate, ProformaCustomer? Customer = null)
    : IRequest<ApiResult<ProformaDto>>;

public sealed class SaveProformaCommandValidator : AbstractValidator<SaveProformaCommand>
{
    public SaveProformaCommandValidator()
    {
        RuleFor(x => x.BoqId).NotEmpty();
        RuleFor(x => x.PoNumber).MaximumLength(60).WithMessage("The P.O. number is too long (60 characters at most).");
        RuleFor(x => x.PoNumber).NotEmpty().When(x => x.PoDate is not null)
            .WithMessage("Enter the P.O. number for that P.O. date.");
        RuleFor(x => x.Customer!.Name).MaximumLength(200).WithMessage("The customer name is too long (200 characters at most).").When(x => x.Customer is not null);
        RuleFor(x => x.Customer!.Project).MaximumLength(200).WithMessage("The project is too long (200 characters at most).").When(x => x.Customer is not null);
        RuleFor(x => x.Customer!.ContactNumber).MaximumLength(40).WithMessage("The contact number is too long (40 characters at most).").When(x => x.Customer is not null);
        RuleFor(x => x.Customer!.Location).MaximumLength(200).WithMessage("The location is too long (200 characters at most).").When(x => x.Customer is not null);
    }
}

public sealed class SaveProformaCommandHandler(IApplicationDbContext context, ICurrentUser actor, TimeProvider clock)
    : IRequestHandler<SaveProformaCommand, ApiResult<ProformaDto>>
{
    public async Task<ApiResult<ProformaDto>> Handle(SaveProformaCommand r, CancellationToken ct)
    {
        var boq = await context.Boqs.AsNoTracking()
            .Where(b => b.Id == r.BoqId)
            .Select(b => new { b.Status, b.ClientName, b.ProjectName, b.ContactNumber, b.SiteLocation })
            .FirstOrDefaultAsync(ct);
        if (boq is null) return ApiResult<ProformaDto>.Failure("Quotation not found.");

        var poNumber = string.IsNullOrWhiteSpace(r.PoNumber) ? null : r.PoNumber.Trim();
        var poDate = poNumber is null ? null : r.PoDate;
        var proforma = await context.ProformaInvoices.FirstOrDefaultAsync(p => p.BoqId == r.BoqId, ct);
        var isNew = proforma is null;

        if (proforma is null)
        {
            if (!ProformaRules.CanIssue(boq.Status)) return ApiResult<ProformaDto>.Failure(ProformaRules.NotApproved);

            proforma = new ProformaInvoice
            {
                Id = Guid.CreateVersion7(),
                BoqId = r.BoqId,
                Number = await ProformaRules.NextNumberAsync(context, ct),
                IssuedOn = ProformaRules.DohaToday(clock),
                IssuedById = actor.UserId,
                IssuedByName = actor.DisplayName ?? "Administrator",
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            };
            context.ProformaInvoices.Add(proforma);
        }

        var name = r.Customer is null ? proforma.CustomerName : ProformaRules.Changed(r.Customer.Name, boq.ClientName);
        var project = r.Customer is null ? proforma.ProjectName : ProformaRules.Changed(r.Customer.Project, boq.ProjectName);
        var contact = r.Customer is null ? proforma.ContactNumber : ProformaRules.Changed(r.Customer.ContactNumber, boq.ContactNumber);
        var location = r.Customer is null ? proforma.Location : ProformaRules.Changed(r.Customer.Location, boq.SiteLocation);

        if (!isNew
            && proforma.PoNumber == poNumber && proforma.PoDate == poDate
            && proforma.CustomerName == name && proforma.ProjectName == project
            && proforma.ContactNumber == contact && proforma.Location == location)
        {
            return ApiResult<ProformaDto>.Success(ProformaRules.ToDto(proforma));   // nothing changed
        }

        if (!isNew) proforma.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        proforma.PoNumber = poNumber;
        proforma.PoDate = poDate;
        proforma.CustomerName = name;
        proforma.ProjectName = project;
        proforma.ContactNumber = contact;
        proforma.Location = location;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)   // two people made the same quotation's proforma at once; the unique index kept one
        {
            return ApiResult<ProformaDto>.Failure(ProformaRules.ChangedMeanwhile);
        }

        return ApiResult<ProformaDto>.Success(ProformaRules.ToDto(proforma));
    }
}
