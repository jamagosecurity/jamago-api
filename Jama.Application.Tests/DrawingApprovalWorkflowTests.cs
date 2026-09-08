using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Drawings;
using Jama.Application.Drawings.Commands.ApproveDrawing;
using Jama.Application.Drawings.Commands.RejectDrawing;
using Jama.Application.Drawings.Commands.SubmitDrawing;
using Jama.Application.Drawings.Commands.UpdateDrawing;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>
/// Drafting a drawing and deciding on one are different jobs, on the same split
/// as quotations — see BoqApprovalWorkflowTests, which this mirrors. Pins where
/// a drawing may go from where it is, what each step records, and what the
/// trail is not allowed to forget.
///
/// Who may call which endpoint is pinned by the policies in Jama.Web, not
/// repeated here.
/// </summary>
public class DrawingApprovalWorkflowTests
{
    private sealed class FakeCurrentUser(string name) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => name;
        public string Role => Roles.Staff;
        public bool Has(string permission) => false;
        public bool IsSuperAdmin { get; init; }
    }

    private static readonly DateTime Now = new(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"drawing-approval-{Guid.NewGuid()}")
            .Options);

    /// <summary>A drawing carrying one PDF, in the state given — enough files to
    /// pass the submit check without a real upload.</summary>
    private static async Task<(ApplicationDbContext Context, Drawing Drawing)> SeedAsync(
        DrawingStatus status = DrawingStatus.Draft)
    {
        var context = NewContext();

        var drawing = new Drawing
        {
            Id = Guid.CreateVersion7(),
            DrawingNumber = "DRW-00001",
            ProjectName = "Villa 22 — Ground Floor Layout",
            Status = status,
            CreatedAt = Now,
        };
        drawing.Files.Add(new DrawingFile
        {
            Id = Guid.CreateVersion7(),
            DrawingId = drawing.Id,
            FileName = "villa-22-ground-floor.pdf",
            StorageKey = "drawings/x/y.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            CreatedAt = Now,
        });

        context.Drawings.Add(drawing);
        await context.SaveChangesAsync();

        return (context, drawing);
    }

    private static TimeProvider Clock => TimeProvider.System;

    private static Task<Common.Models.ApiResult<DrawingDto>> SubmitAsync(
        ApplicationDbContext context, Guid id, ICurrentUser actor) =>
        new SubmitDrawingCommandHandler(context, actor, Clock)
            .Handle(new SubmitDrawingCommand(id), CancellationToken.None);

    private static Task<Common.Models.ApiResult<DrawingDto>> ApproveAsync(
        ApplicationDbContext context, Guid id, ICurrentUser actor) =>
        new ApproveDrawingCommandHandler(context, actor, Clock)
            .Handle(new ApproveDrawingCommand(id), CancellationToken.None);

    private static Task<Common.Models.ApiResult<DrawingDto>> RejectAsync(
        ApplicationDbContext context, Guid id, ICurrentUser actor, string reason) =>
        new RejectDrawingCommandHandler(context, actor, Clock)
            .Handle(new RejectDrawingCommand { Id = id, Reason = reason }, CancellationToken.None);

    // ===== Permissions =====

    [Fact]
    public void Building_a_drawing_does_not_carry_the_right_to_approve_one()
    {
        var effective = Permissions.EffectiveFor(Roles.Staff, [Permissions.DrawingManage]);

        Assert.Contains(Permissions.DrawingManage, effective);
        Assert.DoesNotContain(Permissions.DrawingApprove, effective);
    }

    [Fact]
    public void Approving_is_a_grant_of_its_own_and_carries_nothing_else()
    {
        var effective = Permissions.EffectiveFor(Roles.Staff, [Permissions.DrawingApprove]);

        Assert.Contains(Permissions.DrawingApprove, effective);
        Assert.DoesNotContain(Permissions.DrawingManage, effective);
    }

    [Fact]
    public void An_admin_holds_both()
    {
        var effective = Permissions.EffectiveFor(Roles.Admin, []);

        Assert.Contains(Permissions.DrawingManage, effective);
        Assert.Contains(Permissions.DrawingApprove, effective);
    }

    // ===== Transitions =====

    [Fact]
    public async Task Submitting_hands_the_drawing_over_and_records_the_step()
    {
        var (context, drawing) = await SeedAsync();
        var drafter = new FakeCurrentUser("Sara <sara@jamago.qa>");

        var result = await SubmitAsync(context, drawing.Id, drafter);

        Assert.True(result.Succeeded);
        Assert.Equal(DrawingStatus.Submitted, drawing.Status);
        Assert.NotNull(drawing.SubmittedAt);

        var step = Assert.Single(context.DrawingApprovalEvents.ToList());
        Assert.Equal(DrawingApprovalAction.Submitted, step.Action);
        Assert.Equal(drafter.UserId, step.ActorId);
    }

    [Fact]
    public async Task A_drawing_with_no_files_cannot_be_submitted()
    {
        var context = NewContext();
        var drawing = new Drawing
        {
            Id = Guid.CreateVersion7(),
            DrawingNumber = "DRW-00002",
            ProjectName = "Nothing attached yet",
            CreatedAt = Now,
        };
        context.Drawings.Add(drawing);
        await context.SaveChangesAsync();

        var result = await SubmitAsync(context, drawing.Id, new FakeCurrentUser("Sara"));

        Assert.False(result.Succeeded);
        Assert.Equal(DrawingStatus.Draft, drawing.Status);
    }

    [Fact]
    public async Task A_bare_DWG_with_no_plotted_PDF_cannot_be_submitted()
    {
        var context = NewContext();
        var drawing = new Drawing
        {
            Id = Guid.CreateVersion7(),
            DrawingNumber = "DRW-00003",
            ProjectName = "DWG only",
            CreatedAt = Now,
        };
        // A DWG cannot be opened on screen, so a submission carrying only one is
        // one nobody can actually review.
        drawing.Files.Add(new DrawingFile
        {
            Id = Guid.CreateVersion7(),
            DrawingId = drawing.Id,
            FileName = "layout.dwg",
            StorageKey = "drawings/x/y.dwg",
            ContentType = "application/acad",
            SizeBytes = 2048,
            CreatedAt = Now,
        });
        context.Drawings.Add(drawing);
        await context.SaveChangesAsync();

        var result = await SubmitAsync(context, drawing.Id, new FakeCurrentUser("Sara"));

        Assert.False(result.Succeeded);
        Assert.Contains("plotted PDF", result.Errors.Single());
    }

    [Fact]
    public async Task Approving_records_who_decided_and_when()
    {
        var (context, drawing) = await SeedAsync(DrawingStatus.Submitted);
        var approver = new FakeCurrentUser("Khalid <khalid@jamago.qa>");

        var result = await ApproveAsync(context, drawing.Id, approver);

        Assert.True(result.Succeeded);
        Assert.Equal(DrawingStatus.Approved, drawing.Status);
        Assert.Equal(approver.UserId, drawing.ApprovedById);
        Assert.Equal("Khalid <khalid@jamago.qa>", drawing.ApprovedByName);
        Assert.NotNull(drawing.ApprovedAt);
    }

    [Fact]
    public async Task Rejecting_records_the_reason_where_the_drafter_will_read_it()
    {
        var (context, drawing) = await SeedAsync(DrawingStatus.Submitted);
        var approver = new FakeCurrentUser("Khalid <khalid@jamago.qa>");

        var result = await RejectAsync(context, drawing.Id, approver, "North elevation dimensions don't match the site survey.");

        Assert.True(result.Succeeded);
        Assert.Equal(DrawingStatus.Rejected, drawing.Status);
        Assert.Equal("North elevation dimensions don't match the site survey.", drawing.RejectionReason);
        Assert.Equal("Khalid <khalid@jamago.qa>", drawing.RejectedByName);
        Assert.NotNull(drawing.RejectedAt);

        var step = Assert.Single(context.DrawingApprovalEvents.ToList());
        Assert.Equal(DrawingApprovalAction.Rejected, step.Action);
        Assert.Equal("North elevation dimensions don't match the site survey.", step.Reason);
    }

    [Fact]
    public async Task Only_a_submitted_drawing_can_be_decided()
    {
        var (context, drawing) = await SeedAsync();

        var approved = await ApproveAsync(context, drawing.Id, new FakeCurrentUser("Khalid"));
        var rejected = await RejectAsync(context, drawing.Id, new FakeCurrentUser("Khalid"), "Too early to review.");

        Assert.False(approved.Succeeded);
        Assert.False(rejected.Succeeded);
        Assert.Equal(DrawingStatus.Draft, drawing.Status);
        Assert.Empty(context.DrawingApprovalEvents.ToList());
    }

    [Fact]
    public async Task A_second_approver_is_told_the_decision_was_already_taken()
    {
        var (context, drawing) = await SeedAsync(DrawingStatus.Submitted);
        await ApproveAsync(context, drawing.Id, new FakeCurrentUser("Khalid <khalid@jamago.qa>"));

        var second = await ApproveAsync(context, drawing.Id, new FakeCurrentUser("Noor <noor@jamago.qa>"));

        Assert.False(second.Succeeded);
        Assert.Contains("already approved by Khalid <khalid@jamago.qa>", Assert.Single(second.Errors));
    }

    // ===== Editing =====

    [Fact]
    public void A_drawing_stays_editable_until_it_is_approved()
    {
        Assert.True(DrawingWorkflow.IsEditable(DrawingStatus.Draft));
        Assert.True(DrawingWorkflow.IsEditable(DrawingStatus.Submitted));
        Assert.True(DrawingWorkflow.IsEditable(DrawingStatus.Rejected));
        Assert.False(DrawingWorkflow.IsEditable(DrawingStatus.Approved));
    }

    [Fact]
    public async Task A_submitted_drawing_may_be_corrected_by_its_drafter()
    {
        var (context, drawing) = await SeedAsync(DrawingStatus.Submitted);

        var result = await new UpdateDrawingCommandHandler(
            context, new FakeCurrentUser("Sara"), TimeProvider.System).Handle(
            new UpdateDrawingCommand { Id = drawing.Id, ProjectName = "Villa 22 (note added)" },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Villa 22 (note added)", drawing.ProjectName);
        // Still in the queue — correcting it is not withdrawing it.
        Assert.Equal(DrawingStatus.Submitted, drawing.Status);
        Assert.Empty(context.DrawingApprovalEvents.ToList());
    }

    [Fact]
    public async Task An_approved_drawing_refuses_an_edit()
    {
        var (context, drawing) = await SeedAsync(DrawingStatus.Approved);

        var result = await new UpdateDrawingCommandHandler(
            context, new FakeCurrentUser("Sara"), TimeProvider.System).Handle(
            new UpdateDrawingCommand { Id = drawing.Id, ProjectName = "Villa 22 (reworked)" },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "An approved drawing can only be edited by the super administrator.",
            result.Errors);
        Assert.Equal("Villa 22 — Ground Floor Layout", drawing.ProjectName);
    }

    [Fact]
    public async Task The_super_administrator_may_amend_an_approved_drawing()
    {
        var (context, drawing) = await SeedAsync(DrawingStatus.Approved);
        var root = new FakeCurrentUser("Root <admin@jamago.qa>") { IsSuperAdmin = true };

        var result = await new UpdateDrawingCommandHandler(context, root, TimeProvider.System).Handle(
            new UpdateDrawingCommand { Id = drawing.Id, ProjectName = "Villa 22 (corrected)" },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Villa 22 (corrected)", drawing.ProjectName);

        var step = Assert.Single(context.DrawingApprovalEvents.ToList());
        Assert.Equal(DrawingApprovalAction.Amended, step.Action);
        Assert.Equal("Root <admin@jamago.qa>", step.ActorName);
    }

    // ===== The trail =====

    [Fact]
    public async Task A_rework_keeps_every_step_including_the_rejection()
    {
        var (context, drawing) = await SeedAsync();
        var drafter = new FakeCurrentUser("Sara <sara@jamago.qa>");
        var approver = new FakeCurrentUser("Khalid <khalid@jamago.qa>");

        await SubmitAsync(context, drawing.Id, drafter);
        await RejectAsync(context, drawing.Id, approver, "North elevation dimensions don't match the site survey.");
        await SubmitAsync(context, drawing.Id, drafter);
        await ApproveAsync(context, drawing.Id, approver);

        var trail = context.DrawingApprovalEvents.OrderBy(e => e.CreatedAt).ToList();

        Assert.Equal(
            [
                DrawingApprovalAction.Submitted,
                DrawingApprovalAction.Rejected,
                DrawingApprovalAction.Submitted,
                DrawingApprovalAction.Approved,
            ],
            trail.Select(e => e.Action));

        Assert.Equal(DrawingStatus.Approved, drawing.Status);
        Assert.Null(drawing.RejectionReason);
        Assert.Equal(
            "North elevation dimensions don't match the site survey.",
            trail.Single(e => e.Action == DrawingApprovalAction.Rejected).Reason);
    }

    [Fact]
    public async Task Re_submitting_clears_the_rejection_from_the_document()
    {
        var (context, drawing) = await SeedAsync(DrawingStatus.Submitted);
        await RejectAsync(context, drawing.Id, new FakeCurrentUser("Khalid"), "Wrong sheet size.");

        await SubmitAsync(context, drawing.Id, new FakeCurrentUser("Sara"));

        Assert.Equal(DrawingStatus.Submitted, drawing.Status);
        Assert.Null(drawing.RejectionReason);
        Assert.Null(drawing.RejectedByName);
        Assert.Null(drawing.RejectedAt);
    }

    // ===== The reason =====

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no")]
    public void A_rejection_without_a_usable_reason_is_refused(string? reason)
    {
        var result = new RejectDrawingCommandValidator()
            .Validate(new RejectDrawingCommand { Id = Guid.NewGuid(), Reason = reason });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_reason_that_says_something_passes()
    {
        var result = new RejectDrawingCommandValidator()
            .Validate(new RejectDrawingCommand
            {
                Id = Guid.NewGuid(),
                Reason = "North elevation dimensions don't match the site survey.",
            });

        Assert.True(result.IsValid);
    }
}
