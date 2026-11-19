using Jama.Application.Boqs.Queries.GetBoqPdf;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>The quotation PDF signature carries the preparer's name, email and designation from their account, as it is today.</summary>
public class BoqPdfDesignationTests
{
    private sealed class Viewer : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => "Viewer";
        public string Role => Roles.Staff;
        public bool Has(string permission) => permission == Permissions.BoqManage;
        public bool IsSuperAdmin => false;
    }

    private sealed class CapturingGenerator : IBoqPdfGenerator
    {
        public BoqPdfModel? Last { get; private set; }
        public byte[] Generate(BoqPdfModel model) { Last = model; return [1]; }
        public string DefaultTermsText() => string.Empty;
        public string DefaultTermsTextArabic() => string.Empty;
    }

    private sealed class NoFiles : IFileStorage
    {
        public Task<string> SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken) => Task.FromResult(storageKey);
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static async Task<(ApplicationDbContext Db, AdminUser Preparer, Boq Boq)> SeedAsync(string? designation)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"boq-pdf-desig-{Guid.NewGuid()}").Options);
        var preparer = new AdminUser { Id = Guid.NewGuid(), FullName = "Yousef Kotb", Email = "youssef@jamago.qa", Role = "Staff", Designation = designation };
        var boq = new Boq
        {
            Id = Guid.NewGuid(), BoqNumber = "JMG-00154", ProjectName = "Test", IssueDate = new DateOnly(2026, 11, 19),
            Status = BoqStatus.Approved, PreparedById = preparer.Id, PreparedByName = "Yousef Kotb <youssef@jamago.qa>",
        };
        db.AdminUsers.Add(preparer);
        db.Boqs.Add(boq);
        await db.SaveChangesAsync();
        return (db, preparer, boq);
    }

    private static async Task<BoqPdfModel> RenderAsync(ApplicationDbContext db, Guid boqId)
    {
        var generator = new CapturingGenerator();
        var result = await new GetBoqPdfQueryHandler(db, generator, new NoFiles(), new Viewer()).Handle(new GetBoqPdfQuery(boqId), default);
        Assert.True(result.Succeeded);
        return generator.Last!;
    }

    [Fact]
    public async Task Signature_gets_the_designation_from_the_preparers_profile_as_it_is_now()
    {
        var (db, preparer, boq) = await SeedAsync(" Head of Sales ");
        Assert.Equal("Head of Sales", (await RenderAsync(db, boq.Id)).PreparedByDesignation);

        // Changed in the profile later: the same quotation picks up the new designation.
        preparer.Designation = "Sales Director";
        await db.SaveChangesAsync();
        Assert.Equal("Sales Director", (await RenderAsync(db, boq.Id)).PreparedByDesignation);
    }

    [Fact]
    public async Task Signature_name_and_email_come_from_the_account_as_it_is_now()
    {
        var (db, _, boq) = await SeedAsync("Head of Sales");
        boq.PreparedByName = "Old Name <old@gmail.com>";   // what was saved when the quotation was made
        await db.SaveChangesAsync();

        Assert.Equal("Yousef Kotb <youssef@jamago.qa>", (await RenderAsync(db, boq.Id)).PreparedByName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task No_designation_in_the_profile_prints_none(string? designation)
    {
        var (db, _, boq) = await SeedAsync(designation);
        Assert.Null((await RenderAsync(db, boq.Id)).PreparedByDesignation);
    }
}
