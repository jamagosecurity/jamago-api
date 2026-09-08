using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Drawings;
using Jama.Domain.Enums;

namespace Jama.Application.Tests;

/// <summary>Mirrors BoqVisibilityTests — see there for the reasoning. A
/// drawing's files carry the same leak as a quotation's PDF.</summary>
public class DrawingVisibilityTests
{
    private sealed class FakeCurrentUser(Guid userId, bool canApprove) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
        public string DisplayName => "Test User";
        public string Role => Roles.Staff;
        public bool Has(string permission) => canApprove && permission == Permissions.DrawingApprove;
        public bool IsSuperAdmin => false;
    }

    private static readonly Guid AuthorId = Guid.CreateVersion7();

    [Fact]
    public void The_author_can_see_their_own_draft()
    {
        var actor = new FakeCurrentUser(AuthorId, canApprove: false);

        Assert.True(DrawingVisibility.CanSee(DrawingStatus.Draft, AuthorId, actor));
    }

    [Fact]
    public void A_different_builder_cannot_see_someone_elses_draft()
    {
        var actor = new FakeCurrentUser(Guid.CreateVersion7(), canApprove: false);

        Assert.False(DrawingVisibility.CanSee(DrawingStatus.Draft, AuthorId, actor));
    }

    [Theory]
    [InlineData(DrawingStatus.Draft)]
    [InlineData(DrawingStatus.Submitted)]
    [InlineData(DrawingStatus.Rejected)]
    public void An_approver_can_see_anyones_unapproved_drawing(DrawingStatus status)
    {
        var actor = new FakeCurrentUser(Guid.CreateVersion7(), canApprove: true);

        Assert.True(DrawingVisibility.CanSee(status, AuthorId, actor));
    }

    [Theory]
    [InlineData(DrawingStatus.Draft)]
    [InlineData(DrawingStatus.Submitted)]
    [InlineData(DrawingStatus.Rejected)]
    public void The_author_cannot_download_their_own_unapproved_drawing(DrawingStatus status)
    {
        var actor = new FakeCurrentUser(AuthorId, canApprove: false);

        Assert.False(DrawingVisibility.CanDownload(status, actor));
    }

    [Theory]
    [InlineData(DrawingStatus.Draft)]
    [InlineData(DrawingStatus.Submitted)]
    [InlineData(DrawingStatus.Rejected)]
    public void An_approver_can_download_before_approval_to_review_it(DrawingStatus status)
    {
        var actor = new FakeCurrentUser(Guid.CreateVersion7(), canApprove: true);

        Assert.True(DrawingVisibility.CanDownload(status, actor));
    }

    [Fact]
    public void Anyone_who_can_see_it_can_download_it_once_approved()
    {
        var actor = new FakeCurrentUser(AuthorId, canApprove: false);

        Assert.True(DrawingVisibility.CanDownload(DrawingStatus.Approved, actor));
    }

    [Theory]
    [InlineData(DrawingStatus.Draft, true)]
    [InlineData(DrawingStatus.Submitted, true)]
    [InlineData(DrawingStatus.Rejected, true)]
    [InlineData(DrawingStatus.Approved, false)]
    public void Only_an_approved_drawing_serves_without_the_draft_stamp(DrawingStatus status, bool expected)
    {
        Assert.Equal(expected, DrawingVisibility.Watermark(status));
    }
}
