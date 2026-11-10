using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Maintenance;
using Jama.Application.Maintenance.Commands.AssignMaintenanceRequest;
using Jama.Application.Maintenance.Commands.CancelMaintenanceRequest;
using Jama.Application.Maintenance.Commands.CreateMaintenanceCatalogueItem;
using Jama.Application.Maintenance.Commands.CreateMaintenanceRequest;
using Jama.Application.Maintenance.Commands.DeleteMaintenanceCatalogueImage;
using Jama.Application.Maintenance.Commands.DeleteMaintenanceCatalogueItem;
using Jama.Application.Maintenance.Commands.DeleteMaintenanceCategory;
using Jama.Application.Maintenance.Commands.RenameMaintenanceCategory;
using Jama.Application.Maintenance.Commands.ResolveMaintenanceRequest;
using Jama.Application.Maintenance.Commands.UpdateMaintenanceCatalogueItem;
using Jama.Application.Maintenance.Commands.UpdateMaintenanceRequest;
using Jama.Application.Maintenance.Commands.UploadMaintenanceCatalogueImage;
using Jama.Application.Maintenance.Queries.GetMaintenanceCatalogue;
using Jama.Application.Maintenance.Queries.GetMyMaintenanceRequests;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

/// <summary>
/// The maintenance ticket lifecycle: filing (by anyone authenticated) a
/// ticket of one or more issue lines, pricing each line (the catalogue's own
/// fee unless maintenance.manage moves it), assignment, and closing a ticket
/// out by resolving or cancelling it.
/// </summary>
public class MaintenanceRequestTests
{
    private sealed class FakeCurrentUser(Guid userId, string name, string role = Roles.Staff, bool canManage = false) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
        public string DisplayName => name;
        public string Role => role;
        public bool Has(string permission) => canManage && permission == Permissions.MaintenanceManage;
        public bool IsSuperAdmin => false;
    }

    /// <summary>A context holding a small Non-MOI price list, the way the
    /// migration seeds the real one.</summary>
    private static ApplicationDbContext NewContext()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"maintenance-{Guid.NewGuid()}")
            .Options);
        context.MaintenanceCatalogueItems.AddRange(Catalogue);
        context.SaveChanges();
        return context;
    }

    private sealed class FakeStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public async Task<string> SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            Files[storageKey] = copy.ToArray();
            return storageKey;
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(Files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            Files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }

    /// <summary>A real PNG signature followed by filler, enough to pass the
    /// header check the upload makes against the file's own bytes.</summary>
    private static MemoryStream PngBytes() =>
        new([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3]);

    private static List<MaintenanceCatalogueItem> Catalogue =>
    [
        new() { Category = "PowerService", Name = "NVR power issue", Price = 250m },
        new() { Category = "Connectivity", Name = "Cable cut", Price = 250m },
        new() { Category = "Camera", Name = "Camera replacement", Price = 500m },
        new() { Category = "Camera", Name = "Camera removal/installation", Price = 350m },
    ];

    private static TimeProvider Clock => TimeProvider.System;

    private static MaintenanceRequest Ticket(
        string number, string category, string issue,
        MaintenanceStatus status = MaintenanceStatus.Open) => new()
    {
        Id = Guid.CreateVersion7(),
        RequestNumber = number,
        Status = status,
        ServiceFee = 250m,
        ReportedById = Guid.NewGuid(),
        Lines =
        [
            new MaintenanceRequestLine
            {
                Category = category,
                IssueType = issue,
                Quantity = 1,
                UnitFee = 250m,
                CatalogueFee = 250m,
                LineTotal = 250m,
            },
        ],
    };

    [Theory]
    [InlineData("Camera", "Camera replacement", 500)]
    [InlineData("Camera", "camera REMOVAL/installation", 350)]
    [InlineData("Other", "Cable cut", 250)]
    [InlineData("Other", "Something nobody listed", 250)]
    public void The_price_list_prices_each_issue(string category, string issueType, decimal expectedFee)
    {
        Assert.Equal(expectedFee, MaintenanceIssueCatalogue.FeeFor(category, issueType, Catalogue));
    }

    [Fact]
    public async Task Filing_a_ticket_prices_every_line_from_the_catalogue_and_totals_them()
    {
        var context = NewContext();
        var client = new FakeCurrentUser(Guid.NewGuid(), "A Client", Roles.Client);

        var result = await new CreateMaintenanceRequestCommandHandler(context, client).Handle(
            new CreateMaintenanceRequestCommand
            {
                Lines =
                [
                    new("Camera", "Camera replacement", 2, null),
                    new("Connectivity", "Cable cut", 1, null),
                ],
                SiteLocation = "Site A",
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var ticket = result.Data!;
        Assert.Equal(2, ticket.Lines.Count);
        Assert.Equal(1000m, ticket.Lines.Single(l => l.IssueType == "Camera replacement").LineTotal);
        Assert.Equal(1250m, ticket.ServiceFee);
        Assert.Equal(MaintenanceStatus.Open, ticket.Status);
        Assert.StartsWith("MNT-", ticket.RequestNumber);
        Assert.Equal(client.DisplayName, ticket.ReportedByName);
    }

    [Fact]
    public async Task A_client_submitted_fee_override_is_ignored()
    {
        var context = NewContext();
        var client = new FakeCurrentUser(Guid.NewGuid(), "A Client", Roles.Client);

        var result = await new CreateMaintenanceRequestCommandHandler(context, client).Handle(
            new CreateMaintenanceRequestCommand
            {
                Lines = [new("Camera", "Camera replacement", 1, 1m)],
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(500m, result.Data!.Lines[0].UnitFee);
        Assert.Equal(500m, result.Data.ServiceFee);
    }

    [Fact]
    public async Task A_maintenance_manage_holder_can_reprice_a_line_and_the_catalogue_fee_stays_beside_it()
    {
        var context = NewContext();
        var manager = new FakeCurrentUser(Guid.NewGuid(), "A Manager", canManage: true);

        var result = await new CreateMaintenanceRequestCommandHandler(context, manager).Handle(
            new CreateMaintenanceRequestCommand
            {
                Lines = [new("Camera", "Camera replacement", 1, 425m)],
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var line = result.Data!.Lines[0];
        Assert.Equal(425m, line.UnitFee);
        Assert.Equal(500m, line.CatalogueFee);
        Assert.Equal(425m, result.Data.ServiceFee);
    }

    [Fact]
    public async Task A_managers_discount_comes_off_the_subtotal()
    {
        var context = NewContext();
        var manager = new FakeCurrentUser(Guid.NewGuid(), "A Manager", canManage: true);

        var result = await new CreateMaintenanceRequestCommandHandler(context, manager).Handle(
            new CreateMaintenanceRequestCommand
            {
                Lines = [new("Camera", "Camera replacement", 2, null)],
                Discount = 150m,
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1000m, result.Data!.Subtotal);
        Assert.Equal(150m, result.Data.Discount);
        Assert.Equal(850m, result.Data.ServiceFee);
    }

    [Fact]
    public async Task A_client_cannot_discount_their_own_ticket()
    {
        var context = NewContext();
        var client = new FakeCurrentUser(Guid.NewGuid(), "A Client", Roles.Client);

        var result = await new CreateMaintenanceRequestCommandHandler(context, client).Handle(
            new CreateMaintenanceRequestCommand
            {
                Lines = [new("Camera", "Camera replacement", 1, null)],
                Discount = 400m,
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0m, result.Data!.Discount);
        Assert.Equal(500m, result.Data.ServiceFee);
    }

    [Fact]
    public async Task A_discount_larger_than_the_ticket_is_refused_and_nothing_is_saved()
    {
        var context = NewContext();
        var manager = new FakeCurrentUser(Guid.NewGuid(), "A Manager", canManage: true);

        var result = await new CreateMaintenanceRequestCommandHandler(context, manager).Handle(
            new CreateMaintenanceRequestCommand
            {
                Lines = [new("Connectivity", "Cable cut", 1, null)],
                Discount = 300m,
            },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("more than the ticket total", result.Errors[0]);
        Assert.Equal(0, await context.MaintenanceRequests.CountAsync());
    }

    [Theory]
    [InlineData(null, "Client email is required.")]
    [InlineData("   ", "Client email is required.")]
    [InlineData("not-an-email", "Enter a valid client email.")]
    public void A_ticket_needs_a_valid_client_email(string? email, string expected)
    {
        var result = new CreateMaintenanceRequestCommandValidator().Validate(new CreateMaintenanceRequestCommand
        {
            Lines = [new("Connectivity", "Cable cut", 1, null)],
            ClientEmail = email,
        });

        Assert.Contains(result.Errors, e => e.ErrorMessage == expected);
    }

    [Fact]
    public async Task The_client_email_is_saved_on_the_ticket()
    {
        var context = NewContext();
        var client = new FakeCurrentUser(Guid.NewGuid(), "A Client", Roles.Client);

        var result = await new CreateMaintenanceRequestCommandHandler(context, client).Handle(
            new CreateMaintenanceRequestCommand
            {
                Lines = [new("Connectivity", "Cable cut", 1, null)],
                ClientEmail = "  owner@almeera.qa ",
            },
            CancellationToken.None);

        Assert.Equal("owner@almeera.qa", result.Data!.ClientEmail);
    }

    [Fact]
    public void A_ticket_with_no_lines_is_rejected()
    {
        var result = new CreateMaintenanceRequestCommandValidator().Validate(new CreateMaintenanceRequestCommand());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Add at least one issue.");
    }

    [Fact]
    public async Task Updating_replaces_the_lines_and_recomputes_the_total()
    {
        var context = NewContext();
        var ticket = Ticket("MNT-00004", "Software", "Password reset due to loss/forgot");
        context.MaintenanceRequests.Add(ticket);
        await context.SaveChangesAsync();

        var manager = new FakeCurrentUser(Guid.NewGuid(), "A Manager", canManage: true);
        var result = await new UpdateMaintenanceRequestCommandHandler(context, manager).Handle(
            new UpdateMaintenanceRequestCommand
            {
                Id = ticket.Id,
                Lines =
                [
                    new("Camera", "Camera removal/installation", 3, null),
                    new("PowerService", "NVR power issue", 1, 200m),
                ],
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Data!.Lines.Count);
        Assert.DoesNotContain(result.Data.Lines, l => l.IssueType == "Password reset due to loss/forgot");
        Assert.Equal(350m * 3 + 200m, result.Data.ServiceFee);
        Assert.Equal(2, await context.MaintenanceRequestLines.CountAsync(l => l.MaintenanceRequestId == ticket.Id));
    }

    [Fact]
    public void The_list_row_summarises_several_issues_as_the_first_plus_a_count()
    {
        var ticket = Ticket("MNT-00005", "Connectivity", "Cable cut");
        ticket.Lines.Add(new MaintenanceRequestLine
        {
            Category = "Camera",
            IssueType = "Camera replacement",
            Quantity = 1,
            SortOrder = 1,
        });

        var row = MaintenanceMappings.ToListItemDto(ticket);

        // Category order, not entry order: Connectivity comes before Camera.
        Assert.Equal("Cable cut + 1 more", row.Summary);
        Assert.Equal(2, row.LineCount);
        Assert.Equal(["Connectivity", "Camera"], row.Categories);
    }

    [Fact]
    public async Task Assigning_an_open_ticket_moves_it_to_in_progress()
    {
        var context = NewContext();
        var staffAccountId = Guid.NewGuid();
        context.Staff.Add(new Staff
        {
            Id = Guid.CreateVersion7(),
            AdminUserId = staffAccountId,
            FullName = "A Technician",
            IsActive = true,
            Role = Roles.Staff,
        });
        await context.SaveChangesAsync();

        var ticket = Ticket("MNT-00001", "PowerService", "NVR power issue");
        context.MaintenanceRequests.Add(ticket);
        await context.SaveChangesAsync();

        var result = await new AssignMaintenanceRequestCommandHandler(context, Clock).Handle(
            new AssignMaintenanceRequestCommand(ticket.Id, staffAccountId), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(MaintenanceStatus.InProgress, result.Data!.Status);
        Assert.Equal("A Technician", result.Data.AssignedToName);
        Assert.Single(result.Data.Lines);
    }

    [Fact]
    public async Task A_resolved_ticket_cannot_be_resolved_again()
    {
        var context = NewContext();
        var ticket = Ticket(
            "MNT-00002", "Software", "Password reset due to loss/forgot", MaintenanceStatus.Resolved);
        context.MaintenanceRequests.Add(ticket);
        await context.SaveChangesAsync();

        var actor = new FakeCurrentUser(Guid.NewGuid(), "A Technician");
        var result = await new ResolveMaintenanceRequestCommandHandler(context, actor, Clock).Handle(
            new ResolveMaintenanceRequestCommand(ticket.Id, "Fixed it again"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("already closed", result.Errors[0]);
    }

    [Fact]
    public async Task Cancelling_requires_a_reason_and_closes_the_ticket()
    {
        var context = NewContext();
        var ticket = Ticket("MNT-00003", "Connectivity", "Cable cut");
        context.MaintenanceRequests.Add(ticket);
        await context.SaveChangesAsync();

        var result = await new CancelMaintenanceRequestCommandHandler(context).Handle(
            new CancelMaintenanceRequestCommand(ticket.Id, "Duplicate of MNT-00001"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(MaintenanceStatus.Cancelled, result.Data!.Status);
        Assert.Equal("Duplicate of MNT-00001", result.Data.CancellationReason);
    }

    [Fact]
    public void Visibility_allows_the_reporter_the_assignee_and_a_manager()
    {
        var reporterId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        Assert.True(MaintenanceVisibility.CanSee(reporterId, null, new FakeCurrentUser(reporterId, "Reporter")));
        Assert.True(MaintenanceVisibility.CanSee(reporterId, assigneeId, new FakeCurrentUser(assigneeId, "Assignee")));
        Assert.True(MaintenanceVisibility.CanSee(
            reporterId, assigneeId, new FakeCurrentUser(strangerId, "Manager", canManage: true)));
        Assert.False(MaintenanceVisibility.CanSee(reporterId, assigneeId, new FakeCurrentUser(strangerId, "Stranger")));
    }

    [Fact]
    public async Task A_service_admin_adds_is_what_the_next_ticket_is_charged()
    {
        var context = NewContext();
        var added = await new CreateMaintenanceCatalogueItemCommandHandler(context).Handle(
            new CreateMaintenanceCatalogueItemCommand
            {
                Category = "PowerService",
                Name = "Power adapter replacement",
                Price = 275m,
            },
            CancellationToken.None);
        Assert.True(added.Succeeded);

        var technician = new FakeCurrentUser(Guid.NewGuid(), "A Technician", Roles.Technician);
        var ticket = await new CreateMaintenanceRequestCommandHandler(context, technician).Handle(
            new CreateMaintenanceRequestCommand
            {
                Lines = [new("PowerService", "Power adapter replacement", 1, null)],
            },
            CancellationToken.None);

        Assert.Equal(275m, ticket.Data!.ServiceFee);
    }

    [Fact]
    public async Task A_services_descriptions_are_copied_onto_the_ticket_line()
    {
        var context = NewContext();
        await new CreateMaintenanceCatalogueItemCommandHandler(context).Handle(
            new CreateMaintenanceCatalogueItemCommand
            {
                Category = "NvrStorage",
                Name = "Hard disk replacement",
                DescriptionEn = "  Swap in a 4 TB surveillance drive  ",
                DescriptionAr = "استبدال القرص الصلب",
                Price = 250m,
            },
            CancellationToken.None);

        var client = new FakeCurrentUser(Guid.NewGuid(), "A Client", Roles.Client);
        var ticket = await new CreateMaintenanceRequestCommandHandler(context, client).Handle(
            new CreateMaintenanceRequestCommand
            {
                Lines = [new("NvrStorage", "Hard disk replacement", 1, null)],
                ClientEmail = "owner@example.qa",
            },
            CancellationToken.None);

        var line = ticket.Data!.Lines.Single();
        Assert.Equal("Swap in a 4 TB surveillance drive", line.DescriptionEn);
        Assert.Equal("استبدال القرص الصلب", line.DescriptionAr);
    }

    [Fact]
    public async Task Repricing_a_service_leaves_tickets_already_raised_alone()
    {
        var context = NewContext();
        var client = new FakeCurrentUser(Guid.NewGuid(), "A Client", Roles.Client);
        var before = await new CreateMaintenanceRequestCommandHandler(context, client).Handle(
            new CreateMaintenanceRequestCommand { Lines = [new("Camera", "Camera replacement", 1, null)] },
            CancellationToken.None);

        var service = await context.MaintenanceCatalogueItems.SingleAsync(i => i.Name == "Camera replacement");
        var repriced = await new UpdateMaintenanceCatalogueItemCommandHandler(context).Handle(
            new UpdateMaintenanceCatalogueItemCommand
            {
                Id = service.Id, Category = "Camera", Name = "Camera replacement", Price = 600m,
            },
            CancellationToken.None);
        Assert.True(repriced.Succeeded);

        var after = await new CreateMaintenanceRequestCommandHandler(context, client).Handle(
            new CreateMaintenanceRequestCommand { Lines = [new("Camera", "Camera replacement", 1, null)] },
            CancellationToken.None);

        var old = await context.MaintenanceRequests.Include(t => t.Lines).SingleAsync(t => t.Id == before.Data!.Id);
        Assert.Equal(500m, old.ServiceFee);
        Assert.Equal(500m, old.Lines.Single().UnitFee);
        Assert.Equal(600m, after.Data!.ServiceFee);
    }

    [Fact]
    public async Task Editing_a_service_can_move_it_to_another_category()
    {
        var context = NewContext();
        var service = await context.MaintenanceCatalogueItems.SingleAsync(i => i.Name == "NVR power issue");

        var result = await new UpdateMaintenanceCatalogueItemCommandHandler(context).Handle(
            new UpdateMaintenanceCatalogueItemCommand
            {
                Id = service.Id, Category = "NvrStorage", Name = "NVR power issue", Price = 250m,
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("NvrStorage", result.Data!.Category);
    }

    [Fact]
    public async Task The_same_service_cannot_be_listed_twice_in_a_category()
    {
        var context = NewContext();

        var result = await new CreateMaintenanceCatalogueItemCommandHandler(context).Handle(
            new CreateMaintenanceCatalogueItemCommand { Category = "Camera", Name = "camera replacement", Price = 1m },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("already on the list", result.Errors[0]);
    }

    [Fact]
    public async Task A_technicians_own_list_includes_the_jobs_assigned_to_them()
    {
        var context = NewContext();
        var technicianId = Guid.NewGuid();
        var assigned = Ticket("MNT-00009", "Connectivity", "Cable cut");
        assigned.AssignedToId = technicianId;
        context.MaintenanceRequests.AddRange(assigned, Ticket("MNT-00010", "Camera", "Camera replacement"));
        await context.SaveChangesAsync();

        var result = await new GetMyMaintenanceRequestsQueryHandler(
            context, new FakeCurrentUser(technicianId, "A Technician", Roles.Technician)).Handle(
            new GetMyMaintenanceRequestsQuery(), CancellationToken.None);

        Assert.Equal("MNT-00009", Assert.Single(result.Data!).RequestNumber);
    }
    [Fact]
    public async Task Admin_can_create_a_new_category_by_naming_it_on_a_service()
    {
        var context = NewContext();

        var result = await new CreateMaintenanceCatalogueItemCommandHandler(context).Handle(
            new CreateMaintenanceCatalogueItemCommand { Category = "  Access   control ", Name = "Door lock replacement", Price = 300m },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Access control", result.Data!.Category);

        // A second service typed in a different case joins the same section
        // rather than starting another one.
        var second = await new CreateMaintenanceCatalogueItemCommandHandler(context).Handle(
            new CreateMaintenanceCatalogueItemCommand { Category = "ACCESS CONTROL", Name = "Card reader", Price = 150m },
            CancellationToken.None);
        Assert.Equal("Access control", second.Data!.Category);
    }

    [Theory]
    [InlineData("Power service", "PowerService")]
    [InlineData("nvr & storage", "NvrStorage")]
    [InlineData("camera", "Camera")]
    [InlineData("Other", "Other")]
    public async Task Typing_a_built_in_category_by_its_label_uses_the_built_in(string typed, string stored)
    {
        var context = NewContext();

        var result = await new CreateMaintenanceCatalogueItemCommandHandler(context).Handle(
            new CreateMaintenanceCatalogueItemCommand { Category = typed, Name = "Brand new service", Price = 1m },
            CancellationToken.None);

        Assert.Equal(stored, result.Data!.Category);
    }

    [Fact]
    public async Task The_price_list_puts_added_categories_before_Other()
    {
        var context = NewContext();
        context.MaintenanceCatalogueItems.AddRange(
            new MaintenanceCatalogueItem { Category = "Other", Name = "Anything else", Price = 1m },
            new MaintenanceCatalogueItem { Category = "Access control", Name = "Card reader", Price = 150m });
        await context.SaveChangesAsync();

        var result = await new GetMaintenanceCatalogueQueryHandler(context).Handle(
            new GetMaintenanceCatalogueQuery(), CancellationToken.None);

        Assert.Equal(
            ["PowerService", "Connectivity", "Camera", "Camera", "Access control", "Other"],
            result.Data!.Select(i => i.Category));
    }

    [Fact]
    public async Task A_ticket_can_be_filed_against_an_added_category_and_is_priced_from_it()
    {
        var context = NewContext();
        context.MaintenanceCatalogueItems.Add(
            new MaintenanceCatalogueItem { Category = "Access control", Name = "Card reader", Price = 150m });
        await context.SaveChangesAsync();
        var client = new FakeCurrentUser(Guid.NewGuid(), "A Client", Roles.Client);

        var result = await new CreateMaintenanceRequestCommandHandler(context, client).Handle(
            new CreateMaintenanceRequestCommand { Lines = [new("access control", "Card reader", 2, null)] },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var line = Assert.Single(result.Data!.Lines);
        Assert.Equal("Access control", line.Category);
        Assert.Equal(300m, line.LineTotal);
    }

    [Fact]
    public async Task A_service_picture_is_stored_listed_and_removed_with_its_file()
    {
        var context = NewContext();
        var storage = new FakeStorage();
        var service = await context.MaintenanceCatalogueItems.FirstAsync();

        var uploaded = await new UploadMaintenanceCatalogueImageCommandHandler(context, storage, Clock).Handle(
            new UploadMaintenanceCatalogueImageCommand
            {
                ItemId = service.Id, FileName = "adapter.png", ContentType = "image/png",
                SizeBytes = 19, Content = PngBytes(),
            },
            CancellationToken.None);

        Assert.True(uploaded.Succeeded);
        Assert.Single(storage.Files);

        var listed = await new GetMaintenanceCatalogueQueryHandler(context).Handle(
            new GetMaintenanceCatalogueQuery(), CancellationToken.None);
        var picture = Assert.Single(listed.Data!.Single(i => i.Id == service.Id).Images);
        Assert.StartsWith("/api/maintenance/issue-catalogue/images/", picture.Url);

        var removed = await new DeleteMaintenanceCatalogueImageCommandHandler(context, storage).Handle(
            new DeleteMaintenanceCatalogueImageCommand(picture.Id), CancellationToken.None);
        Assert.True(removed.Succeeded);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task A_file_that_is_not_the_image_it_claims_to_be_is_refused()
    {
        var context = NewContext();
        var storage = new FakeStorage();
        var service = await context.MaintenanceCatalogueItems.FirstAsync();

        var result = await new UploadMaintenanceCatalogueImageCommandHandler(context, storage, Clock).Handle(
            new UploadMaintenanceCatalogueImageCommand
            {
                ItemId = service.Id, FileName = "fake.png", ContentType = "image/png",
                SizeBytes = 12, Content = new MemoryStream("not an image"u8.ToArray()),
            },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task Deleting_a_service_removes_its_picture_files_too()
    {
        var context = NewContext();
        var storage = new FakeStorage();
        var service = await context.MaintenanceCatalogueItems.FirstAsync();
        await new UploadMaintenanceCatalogueImageCommandHandler(context, storage, Clock).Handle(
            new UploadMaintenanceCatalogueImageCommand
            {
                ItemId = service.Id, FileName = "a.png", ContentType = "image/png", SizeBytes = 19, Content = PngBytes(),
            },
            CancellationToken.None);

        var result = await new DeleteMaintenanceCatalogueItemCommandHandler(context, storage).Handle(
            new DeleteMaintenanceCatalogueItemCommand(service.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(storage.Files);
    }
    [Fact]
    public async Task Renaming_a_category_moves_its_services_and_old_ticket_lines()
    {
        var context = NewContext();
        context.MaintenanceCatalogueItems.Add(new MaintenanceCatalogueItem { Category = "Access control", Name = "Card reader", Price = 150m });
        context.MaintenanceRequests.Add(Ticket("MNT-00020", "Access control", "Card reader"));
        await context.SaveChangesAsync();

        var result = await new RenameMaintenanceCategoryCommandHandler(context).Handle(
            new RenameMaintenanceCategoryCommand { From = "Access control", To = "Doors & access" },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Doors & access", (await context.MaintenanceCatalogueItems.SingleAsync(i => i.Name == "Card reader")).Category);
        Assert.Equal("Doors & access", (await context.MaintenanceRequestLines.SingleAsync(l => l.IssueType == "Card reader")).Category);
    }

    [Theory]
    [InlineData("Camera", "Anything")]          // built-in cannot be renamed
    [InlineData("Access control", "Camera")]    // would merge into a built-in
    [InlineData("Access control", "Alarms")]    // would merge into another added one
    public async Task A_category_rename_that_is_not_allowed_is_refused(string from, string to)
    {
        var context = NewContext();
        context.MaintenanceCatalogueItems.AddRange(
            new MaintenanceCatalogueItem { Category = "Access control", Name = "Card reader", Price = 150m },
            new MaintenanceCatalogueItem { Category = "Alarms", Name = "Siren", Price = 90m });
        await context.SaveChangesAsync();

        var result = await new RenameMaintenanceCategoryCommandHandler(context).Handle(
            new RenameMaintenanceCategoryCommand { From = from, To = to }, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Deleting_a_category_removes_its_services_and_files_but_not_old_tickets()
    {
        var context = NewContext();
        var storage = new FakeStorage();
        var reader = new MaintenanceCatalogueItem { Category = "Access control", Name = "Card reader", Price = 150m };
        context.MaintenanceCatalogueItems.Add(reader);
        context.MaintenanceRequests.Add(Ticket("MNT-00021", "Access control", "Card reader"));
        await context.SaveChangesAsync();
        await new UploadMaintenanceCatalogueImageCommandHandler(context, storage, Clock).Handle(
            new UploadMaintenanceCatalogueImageCommand
            {
                ItemId = reader.Id, FileName = "a.png", ContentType = "image/png", SizeBytes = 19, Content = PngBytes(),
            },
            CancellationToken.None);

        var result = await new DeleteMaintenanceCategoryCommandHandler(context, storage).Handle(
            new DeleteMaintenanceCategoryCommand("access control"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Data);
        Assert.False(await context.MaintenanceCatalogueItems.AnyAsync(i => i.Category == "Access control"));
        Assert.Empty(storage.Files);
        Assert.True(await context.MaintenanceRequestLines.AnyAsync(l => l.IssueType == "Card reader"));
    }

    [Fact]
    public async Task A_built_in_category_cannot_be_deleted()
    {
        var context = NewContext();

        var result = await new DeleteMaintenanceCategoryCommandHandler(context, new FakeStorage()).Handle(
            new DeleteMaintenanceCategoryCommand("Camera"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(await context.MaintenanceCatalogueItems.AnyAsync(i => i.Category == "Camera"));
    }
}
