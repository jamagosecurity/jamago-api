using Jama.Application.Boqs;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Enums;

namespace Jama.Application.Tests;

/// <summary>
/// Before this rule existed, the approval workflow controlled editing and
/// deciding but nothing about the document itself: any account holding only
/// boq.manage could create a Draft and read another builder's just as freely.
/// CanSee pins the fix: a Draft is nobody's business but its author's and
/// whoever can approve it. Download stays open to anyone who can see the row,
/// at any status — see Watermark below for what actually keeps an unapproved
/// copy from passing as the finished document.
/// </summary>
public class BoqVisibilityTests
{
    private sealed class FakeCurrentUser(Guid userId, bool canApprove) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
        public string DisplayName => "Test User";
        public string Role => Roles.Staff;
        public bool Has(string permission) => canApprove && permission == Permissions.BoqApprove;
        public bool IsSuperAdmin => false;
    }

    private static readonly Guid AuthorId = Guid.CreateVersion7();

    // ===== CanSee =====

    [Fact]
    public void The_author_can_see_their_own_draft()
    {
        var actor = new FakeCurrentUser(AuthorId, canApprove: false);

        Assert.True(BoqVisibility.CanSee(BoqStatus.Draft, AuthorId, actor));
    }

    [Fact]
    public void A_different_builder_cannot_see_someone_elses_draft()
    {
        var actor = new FakeCurrentUser(Guid.CreateVersion7(), canApprove: false);

        Assert.False(BoqVisibility.CanSee(BoqStatus.Draft, AuthorId, actor));
    }

    [Theory]
    [InlineData(BoqStatus.Draft)]
    [InlineData(BoqStatus.Submitted)]
    [InlineData(BoqStatus.Rejected)]
    public void An_approver_can_see_anyones_unapproved_quotation(BoqStatus status)
    {
        var actor = new FakeCurrentUser(Guid.CreateVersion7(), canApprove: true);

        Assert.True(BoqVisibility.CanSee(status, AuthorId, actor));
    }

    [Fact]
    public void Anyone_can_see_an_approved_quotation()
    {
        var actor = new FakeCurrentUser(Guid.CreateVersion7(), canApprove: false);

        Assert.True(BoqVisibility.CanSee(BoqStatus.Approved, AuthorId, actor));
    }

    // ===== Watermark =====

    [Theory]
    [InlineData(BoqStatus.Draft, true)]
    [InlineData(BoqStatus.Submitted, true)]
    [InlineData(BoqStatus.Rejected, true)]
    [InlineData(BoqStatus.Approved, false)]
    public void Only_an_approved_quotation_prints_without_the_draft_stamp(BoqStatus status, bool expected)
    {
        // Covers the approver's own preview copy too: if it is ever forwarded
        // by mistake, it must never pass for the finished document.
        Assert.Equal(expected, BoqVisibility.Watermark(status));
    }
}
