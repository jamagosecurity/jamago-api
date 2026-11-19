using Jama.Application.Auth;
using Jama.Application.Staffs;
using Jama.Application.Staffs.Commands.UpdateMyStaffProfile;
using Jama.Application.Staffs.Commands.UpdateStaff;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>A staff member is renamed (or gets a new email): their quotations and quotation history show the new name.</summary>
public class StaffRenameQuotationTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(AdminUser user, string password) => "H:" + password;
        public bool Verify(AdminUser user, string password) => user.PasswordHash == "H:" + password;
    }

    private const string OldName = "Abdel Rahim Yousef <abdelrahim@gmail.com>";
    private const string OtherName = "Khemaies <khemaies@jamago.qa>";

    private static async Task<(ApplicationDbContext Db, Staff Staff, AdminUser Other)> SeedAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"staff-rename-{Guid.NewGuid()}").Options);
        var yousef = new AdminUser { Id = Guid.NewGuid(), FullName = "Abdel Rahim Yousef", Email = "abdelrahim@gmail.com", Role = "Admin" };
        var other = new AdminUser { Id = Guid.NewGuid(), FullName = "Khemaies", Email = "khemaies@jamago.qa", Role = "Admin" };
        var staff = new Staff { Id = Guid.NewGuid(), FullName = yousef.FullName, AdminUserId = yousef.Id, Account = yousef };
        db.AdminUsers.AddRange(yousef, other);
        db.Staff.Add(staff);

        Boq Make(string number) => new() { Id = Guid.NewGuid(), BoqNumber = number, ProjectName = "Site " + number, PreparedById = other.Id, PreparedByName = OtherName };
        var mine = Make("JMG-00001");
        mine.PreparedById = yousef.Id; mine.PreparedByName = OldName;
        mine.ApprovedById = other.Id; mine.ApprovedByName = OtherName;
        var approvedByMe = Make("JMG-00002");
        approvedByMe.ApprovedById = yousef.Id; approvedByMe.ApprovedByName = OldName;
        var rejectedAndDeleted = Make("JMG-00003");
        rejectedAndDeleted.RejectedById = yousef.Id; rejectedAndDeleted.RejectedByName = OldName;
        rejectedAndDeleted.IsDeleted = true; rejectedAndDeleted.DeletedById = yousef.Id; rejectedAndDeleted.DeletedByName = OldName;
        db.Boqs.AddRange(mine, approvedByMe, rejectedAndDeleted);

        db.BoqApprovalEvents.AddRange(
            new BoqApprovalEvent { Id = Guid.NewGuid(), BoqId = mine.Id, Action = BoqApprovalAction.Submitted, ActorId = yousef.Id, ActorName = OldName },
            new BoqApprovalEvent { Id = Guid.NewGuid(), BoqId = rejectedAndDeleted.Id, Action = BoqApprovalAction.Rejected, ActorId = yousef.Id, ActorName = OldName },
            new BoqApprovalEvent { Id = Guid.NewGuid(), BoqId = mine.Id, Action = BoqApprovalAction.Approved, ActorId = other.Id, ActorName = OtherName });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, staff, other);
    }

    [Fact]
    public async Task Admin_renaming_staff_updates_their_quotations_and_history()
    {
        var (db, staff, other) = await SeedAsync();

        var result = await new UpdateStaffCommandHandler(db, new FakeHasher()).Handle(new UpdateStaffCommand
        {
            Id = staff.Id, FullName = " Yousef Kotb ", Email = " Youssef@JamaGo.qa ", IsActive = true, CanSignIn = true,
        }, default);
        Assert.True(result.Succeeded);

        db.ChangeTracker.Clear();
        const string NewName = "Yousef Kotb <youssef@jamago.qa>";
        var boqs = await db.Boqs.IgnoreQueryFilters().OrderBy(b => b.BoqNumber).ToListAsync();
        Assert.Equal(NewName, boqs[0].PreparedByName);
        Assert.Equal(OtherName, boqs[0].ApprovedByName);           // someone else's name is left alone
        Assert.Equal(OtherName, boqs[1].PreparedByName);
        Assert.Equal(NewName, boqs[1].ApprovedByName);
        Assert.Equal(NewName, boqs[2].RejectedByName);              // deleted quotations too
        Assert.Equal(NewName, boqs[2].DeletedByName);

        var events = await db.BoqApprovalEvents.IgnoreQueryFilters().ToListAsync();
        Assert.All(events.Where(e => e.ActorId == staff.AdminUserId), e => Assert.Equal(NewName, e.ActorName));
        Assert.Equal(OtherName, Assert.Single(events, e => e.ActorId == other.Id).ActorName);
    }

    [Fact]
    public async Task Staff_renaming_themselves_updates_their_quotations()
    {
        var (db, staff, _) = await SeedAsync();

        var result = await new UpdateMyStaffProfileCommandHandler(db).Handle(new UpdateMyStaffProfileCommand
        {
            UserId = staff.AdminUserId!.Value, FullName = "Yousef Kotb",
        }, default);
        Assert.True(result.Succeeded);

        db.ChangeTracker.Clear();
        var mine = await db.Boqs.SingleAsync(b => b.BoqNumber == "JMG-00001");
        Assert.Equal("Yousef Kotb <abdelrahim@gmail.com>", mine.PreparedByName);
    }

    [Fact]
    public async Task Saving_staff_without_a_name_or_email_change_leaves_quotations_alone()
    {
        var (db, staff, _) = await SeedAsync();
        var custom = await db.Boqs.SingleAsync(b => b.BoqNumber == "JMG-00001");
        custom.PreparedByName = "Kept as typed";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await new UpdateStaffCommandHandler(db, new FakeHasher()).Handle(new UpdateStaffCommand
        {
            Id = staff.Id, FullName = "Abdel Rahim Yousef", Email = "abdelrahim@gmail.com", IsActive = false, CanSignIn = true,
        }, default);

        db.ChangeTracker.Clear();
        Assert.Equal("Kept as typed", (await db.Boqs.SingleAsync(b => b.BoqNumber == "JMG-00001")).PreparedByName);
    }
}
