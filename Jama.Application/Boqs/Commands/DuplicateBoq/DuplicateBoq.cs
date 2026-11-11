using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Boqs.Commands.DuplicateBoq;

/// <summary>
/// Makes a brand-new quotation out of an existing one: same sections, items, quantities, rates, discount, notes and terms,
/// but with its own number, as a Draft, prepared by whoever pressed the button, dated today.
///
/// The original is never touched. Nothing is shared between the two afterwards — every section and line is a fresh row — so
/// changing the copy's name, quantities or items changes only the copy, and the original stays exactly as it was.
///
/// What is deliberately NOT carried over, because it describes the original's life rather than its content: the approval
/// trail and who approved or rejected it, its revision number, and its contract wording. The copy starts clean.
/// </summary>
public sealed record DuplicateBoqCommand(Guid Id) : IRequest<ApiResult<BoqDto>>;

public sealed class DuplicateBoqCommandHandler(
    IApplicationDbContext context,
    ICurrentUser actor,
    TimeProvider timeProvider) : IRequestHandler<DuplicateBoqCommand, ApiResult<BoqDto>>
{
    public async Task<ApiResult<BoqDto>> Handle(DuplicateBoqCommand request, CancellationToken cancellationToken)
    {
        var source = await context.Boqs
            .AsNoTracking()
            .Include(x => x.Sections)
            .ThenInclude(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        // Same rule as opening it: whoever cannot see the original cannot copy it either, and gets the same answer.
        if (source is null || !BoqVisibility.CanSee(source.Status, source.PreparedById, actor))
            return ApiResult<BoqDto>.Failure("BOQ not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var copy = new Boq
        {
            Id = Guid.CreateVersion7(),
            BoqNumber = await BoqNumbers.NextAsync(context, cancellationToken),
            ProjectName = source.ProjectName,
            SiteLocation = source.SiteLocation,
            ClientName = source.ClientName,
            ContactNumber = source.ContactNumber,
            IssueDate = DateOnly.FromDateTime(now),
            Status = BoqStatus.Draft,
            Notes = source.Notes,
            TermsAndConditions = source.TermsAndConditions,
            TermsAndConditionsAr = source.TermsAndConditionsAr,
            PreparedById = actor.UserId,
            PreparedByName = actor.DisplayName,
            Total = source.Total,
            SpecialDiscount = source.SpecialDiscount,
            GrandTotal = source.GrandTotal,
            RevisionNumber = 0,
            CreatedAt = now,
        };

        foreach (var section in source.Sections.OrderBy(s => s.SortOrder))
        {
            var newSection = new BoqSection
            {
                Id = Guid.CreateVersion7(),
                Title = section.Title,
                SortOrder = section.SortOrder,
            };

            foreach (var line in section.Lines.OrderBy(l => l.SortOrder))
            {
                newSection.Lines.Add(new BoqLine
                {
                    Id = Guid.CreateVersion7(),
                    CameraId = line.CameraId,
                    ItemName = line.ItemName,
                    ModelNo = line.ModelNo,
                    Brand = line.Brand,
                    Type = line.Type,
                    Uom = line.Uom,
                    Quantity = line.Quantity,
                    Resolution = line.Resolution,
                    BitrateMbps = line.BitrateMbps,
                    UnitRate = line.UnitRate,
                    CatalogueRate = line.CatalogueRate,
                    LineTotal = line.LineTotal,
                    SortOrder = line.SortOrder,
                });
            }

            copy.Sections.Add(newSection);
        }

        context.Boqs.Add(copy);
        BoqWorkflow.Record(context, copy, BoqApprovalAction.Created, actor, now, $"Duplicated from {source.BoqNumber}");
        await context.SaveChangesAsync(cancellationToken);

        return ApiResult<BoqDto>.Success(BoqMappings.ToDto(copy));
    }
}
