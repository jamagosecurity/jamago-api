using Jama.Application.Boqs.Commands.ApproveBoq;
using Jama.Application.Boqs.Commands.RejectBoq;
using Jama.Application.Boqs.Commands.SubmitBoq;
using Jama.Application.Boqs.Commands.UpdateBoq;
using Jama.Application.Boqs;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>
/// Building a quotation and deciding on one are different jobs held by different
/// people. These pin the part of that split which lives in the domain: where a
/// document may go from where it is, what each step records, and what the trail
/// is not allowed to forget.
///
/// Who may call which endpoint is authorization and is pinned by the policies in
/// Jama.Web — not repeated here, so the two cannot drift into disagreeing.
/// </summary>
public class BoqApprovalWorkflowTests
{
    private sealed class FakeCurrentUser(string name) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => name;
        public string Role => Roles.Staff;

        /// <summary>Not what these tests are about — the endpoint policies decide
        /// who may call what, and they are pinned in Jama.Web.</summary>
        public bool Has(string permission) => false;
    }

    private static readonly DateTime Now = new(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"boq-approval-{Guid.NewGuid()}")
            .Options);

    /// <summary>A quotation with one priced line, in the state given.</summary>
    private static async Task<(ApplicationDbContext Context, Boq Boq)> SeedAsync(
        BoqStatus status = BoqStatus.Draft)
    {
        var context = NewContext();

        var boq = new Boq
        {
            Id = Guid.CreateVersion7(),
            BoqNumber = "JMG-00001",
            ProjectName = "Villa 22",
            Status = status,
            Total = 1650m,
            GrandTotal = 1650m,
            CreatedAt = Now,
        };
        var section = new BoqSection
        {
            Id = Guid.CreateVersion7(),
            BoqId = boq.Id,
            Title = BoqSectionTitles.MainCctv,
            SortOrder = 0,
            CreatedAt = Now,
        };
        section.Lines.Add(new BoqLine
        {
            Id = Guid.CreateVersion7(),
            BoqSectionId = section.Id,
            ItemName = "DS-2CD2143G2-I",
            Uom = UnitOfMeasurement.Piece,
            Quantity = 1,
            UnitRate = 1650m,
            CatalogueRate = 1650m,
            SortOrder = 0,
            CreatedAt = Now,
        });
        boq.Sections.Add(section);

        context.Boqs.Add(boq);
        await context.SaveChangesAsync();

        return (context, boq);
    }

    private static TimeProvider Clock => TimeProvider.System;

    private static Task<Common.Models.ApiResult<BoqDto>> SubmitAsync(
        ApplicationDbContext context, Guid id, ICurrentUser actor) =>
        new SubmitBoqCommandHandler(context, actor, Clock)
            .Handle(new SubmitBoqCommand(id), CancellationToken.None);

    private static Task<Common.Models.ApiResult<BoqDto>> ApproveAsync(
        ApplicationDbContext context, Guid id, ICurrentUser actor) =>
        new ApproveBoqCommandHandler(context, actor, Clock)
            .Handle(new ApproveBoqCommand(id), CancellationToken.None);

    private static Task<Common.Models.ApiResult<BoqDto>> RejectAsync(
        ApplicationDbContext context, Guid id, ICurrentUser actor, string reason) =>
        new RejectBoqCommandHandler(context, actor, Clock)
            .Handle(new RejectBoqCommand { Id = id, Reason = reason }, CancellationToken.None);

    // ===== Permissions =====

    [Fact]
    public void Building_a_quotation_does_not_carry_the_right_to_approve_one()
    {
        // The whole point of the split: a staff account granted the build permission
        // holds exactly that, and nothing about deciding on what it built.
        var effective = Permissions.EffectiveFor(Roles.Staff, [Permissions.BoqManage]);

        Assert.Contains(Permissions.BoqManage, effective);
        Assert.DoesNotContain(Permissions.BoqApprove, effective);
    }

    [Fact]
    public void Approving_is_a_grant_of_its_own_and_carries_nothing_else()
    {
        var effective = Permissions.EffectiveFor(Roles.Staff, [Permissions.BoqApprove]);

        Assert.Contains(Permissions.BoqApprove, effective);
        // Being able to answer a quotation is not being able to write one.
        Assert.DoesNotContain(Permissions.BoqManage, effective);
    }

    [Fact]
    public void An_admin_holds_both()
    {
        var effective = Permissions.EffectiveFor(Roles.Admin, []);

        Assert.Contains(Permissions.BoqManage, effective);
        Assert.Contains(Permissions.BoqApprove, effective);
    }

    // ===== Transitions =====

    [Fact]
    public async Task Submitting_hands_the_quotation_over_and_records_the_step()
    {
        var (context, boq) = await SeedAsync();
        var author = new FakeCurrentUser("Sara <sara@jamago.qa>");

        var result = await SubmitAsync(context, boq.Id, author);

        Assert.True(result.Succeeded);
        Assert.Equal(BoqStatus.Submitted, boq.Status);
        Assert.NotNull(boq.SubmittedAt);

        var step = Assert.Single(context.BoqApprovalEvents.ToList());
        Assert.Equal(BoqApprovalAction.Submitted, step.Action);
        Assert.Equal(author.UserId, step.ActorId);
        Assert.Equal("Sara <sara@jamago.qa>", step.ActorName);
    }

    [Fact]
    public async Task Approving_records_who_decided_and_when()
    {
        var (context, boq) = await SeedAsync(BoqStatus.Submitted);
        var approver = new FakeCurrentUser("Khalid <khalid@jamago.qa>");

        var result = await ApproveAsync(context, boq.Id, approver);

        Assert.True(result.Succeeded);
        Assert.Equal(BoqStatus.Approved, boq.Status);
        Assert.Equal(approver.UserId, boq.ApprovedById);
        Assert.Equal("Khalid <khalid@jamago.qa>", boq.ApprovedByName);
        Assert.NotNull(boq.ApprovedAt);
    }

    [Fact]
    public async Task Rejecting_records_the_reason_where_the_author_will_read_it()
    {
        var (context, boq) = await SeedAsync(BoqStatus.Submitted);
        var approver = new FakeCurrentUser("Khalid <khalid@jamago.qa>");

        var result = await RejectAsync(context, boq.Id, approver, "Switch is undersized for 24 cameras.");

        Assert.True(result.Succeeded);
        Assert.Equal(BoqStatus.Rejected, boq.Status);
        Assert.Equal("Switch is undersized for 24 cameras.", boq.RejectionReason);
        Assert.Equal("Khalid <khalid@jamago.qa>", boq.RejectedByName);
        Assert.NotNull(boq.RejectedAt);

        // And on the step too, so it survives the next submission clearing the
        // document's copy.
        var step = Assert.Single(context.BoqApprovalEvents.ToList());
        Assert.Equal(BoqApprovalAction.Rejected, step.Action);
        Assert.Equal("Switch is undersized for 24 cameras.", step.Reason);
    }

    [Fact]
    public async Task Only_a_submitted_quotation_can_be_decided()
    {
        var (context, boq) = await SeedAsync();

        var approved = await ApproveAsync(context, boq.Id, new FakeCurrentUser("Khalid"));
        var rejected = await RejectAsync(context, boq.Id, new FakeCurrentUser("Khalid"), "Too expensive.");

        Assert.False(approved.Succeeded);
        Assert.False(rejected.Succeeded);
        Assert.Equal(BoqStatus.Draft, boq.Status);
        Assert.Empty(context.BoqApprovalEvents.ToList());
    }

    [Fact]
    public async Task A_second_approver_is_told_the_decision_was_already_taken()
    {
        var (context, boq) = await SeedAsync(BoqStatus.Submitted);
        await ApproveAsync(context, boq.Id, new FakeCurrentUser("Khalid <khalid@jamago.qa>"));

        // Two people working the same queue must not both come away believing
        // theirs was the decision.
        var second = await ApproveAsync(context, boq.Id, new FakeCurrentUser("Noor <noor@jamago.qa>"));

        Assert.False(second.Succeeded);
        // Named, so the second approver knows who to go and talk to.
        Assert.Contains("already approved by Khalid <khalid@jamago.qa>", Assert.Single(second.Errors));
        Assert.Equal("Khalid <khalid@jamago.qa>", boq.ApprovedByName);
    }

    [Fact]
    public async Task An_empty_quotation_cannot_be_submitted()
    {
        var context = NewContext();
        var boq = new Boq
        {
            Id = Guid.CreateVersion7(),
            BoqNumber = "JMG-00002",
            ProjectName = "Nothing on it",
            CreatedAt = Now,
        };
        context.Boqs.Add(boq);
        await context.SaveChangesAsync();

        var result = await SubmitAsync(context, boq.Id, new FakeCurrentUser("Sara"));

        Assert.False(result.Succeeded);
        Assert.Equal(BoqStatus.Draft, boq.Status);
    }

    // ===== Editing =====

    [Fact]
    public void A_quotation_is_editable_only_while_nobody_is_holding_it()
    {
        Assert.True(BoqWorkflow.IsEditable(BoqStatus.Draft));
        // Reworking after a rejection is the reason the reason exists.
        Assert.True(BoqWorkflow.IsEditable(BoqStatus.Rejected));

        // Somebody is reviewing it; it must not change underneath them.
        Assert.False(BoqWorkflow.IsEditable(BoqStatus.Submitted));
        // And an approval has to stay a statement about what was approved.
        Assert.False(BoqWorkflow.IsEditable(BoqStatus.Approved));
    }

    [Fact]
    public async Task An_approved_quotation_refuses_an_edit()
    {
        var (context, boq) = await SeedAsync(BoqStatus.Approved);

        var result = await new UpdateBoqCommandHandler(context, TimeProvider.System).Handle(
            new UpdateBoqCommand
            {
                Id = boq.Id,
                ProjectName = "Villa 22 (reworked)",
                Sections = [],
            },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("An approved quotation cannot be edited.", result.Errors);
        Assert.Equal("Villa 22", boq.ProjectName);
    }

    // ===== The trail =====

    [Fact]
    public async Task A_rework_keeps_every_step_including_the_rejection()
    {
        var (context, boq) = await SeedAsync();
        var author = new FakeCurrentUser("Sara <sara@jamago.qa>");
        var approver = new FakeCurrentUser("Khalid <khalid@jamago.qa>");

        await SubmitAsync(context, boq.Id, author);
        await RejectAsync(context, boq.Id, approver, "Switch is undersized for 24 cameras.");
        await SubmitAsync(context, boq.Id, author);
        await ApproveAsync(context, boq.Id, approver);

        var trail = context.BoqApprovalEvents.OrderBy(e => e.CreatedAt).ToList();

        Assert.Equal(
            [
                BoqApprovalAction.Submitted,
                BoqApprovalAction.Rejected,
                BoqApprovalAction.Submitted,
                BoqApprovalAction.Approved,
            ],
            trail.Select(e => e.Action));

        // The document has moved on — it is approved, and no longer carries the
        // rejection — but the rejection and its reason are still on the record.
        Assert.Equal(BoqStatus.Approved, boq.Status);
        Assert.Null(boq.RejectionReason);
        Assert.Equal(
            "Switch is undersized for 24 cameras.",
            trail.Single(e => e.Action == BoqApprovalAction.Rejected).Reason);
    }

    [Fact]
    public async Task Re_submitting_clears_the_rejection_from_the_document()
    {
        var (context, boq) = await SeedAsync(BoqStatus.Submitted);
        await RejectAsync(context, boq.Id, new FakeCurrentUser("Khalid"), "Wrong camera model.");

        await SubmitAsync(context, boq.Id, new FakeCurrentUser("Sara"));

        // It is waiting again, not still rejected: a queue that shows both is a
        // queue nobody can work from.
        Assert.Equal(BoqStatus.Submitted, boq.Status);
        Assert.Null(boq.RejectionReason);
        Assert.Null(boq.RejectedByName);
        Assert.Null(boq.RejectedAt);
    }

    // ===== The reason =====

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no")]
    public void A_rejection_without_a_usable_reason_is_refused(string? reason)
    {
        // Enforced on the server, not only by the modal that asks for it:
        // "rejected" on its own tells the person reworking it nothing.
        var result = new RejectBoqCommandValidator()
            .Validate(new RejectBoqCommand { Id = Guid.NewGuid(), Reason = reason });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_reason_that_says_something_passes()
    {
        var result = new RejectBoqCommandValidator()
            .Validate(new RejectBoqCommand
            {
                Id = Guid.NewGuid(),
                Reason = "Switch is undersized for 24 cameras.",
            });

        Assert.True(result.IsValid);
    }
}
