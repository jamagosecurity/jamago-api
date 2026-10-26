using Jama.Application.Auth;
using Jama.Application.Auth.Commands.Login;
using Jama.Application.ClientRegistrations;
using Jama.Application.ClientRegistrations.Commands.ApproveClientRegistration;
using Jama.Application.ClientRegistrations.Commands.RegisterClient;
using Jama.Application.ClientRegistrations.Commands.RejectClientRegistration;
using Jama.Application.ClientRegistrations.Queries.GetClientRegistrations;
using Jama.Application.ClientRegistrations.Queries.GetMyClientRegistration;
using Jama.Application.Common;
using Jama.Application.Common.Interfaces;
using Jama.Application.Common.Models;
using Jama.Domain.Entities;
using Jama.Domain.Enums;
using Jama.Infrastructure.Data;
using Jama.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Tests;

public sealed class ClientRegistrationTests
{
    private const string Password = "Secure123";

    private sealed class FakeCurrentUser(Guid? userId = null) : ICurrentUser
    {
        public Guid UserId { get; } = userId ?? Guid.NewGuid();
        public string DisplayName => "Admin <admin@jamago.qa>";
        public string Role => Roles.Admin;
        public bool Has(string permission) => true;
        public bool IsSuperAdmin => true;
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public LoginResponse Generate(AdminUser user) => new("token", DateTime.UtcNow, null!);
    }

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"client-registration-{Guid.NewGuid()}")
            .Options);

    private static RegisterClientCommand Registration(string email = "client@example.com") => new()
    {
        FullName = "Ahmed Al-Kuwari",
        Email = email,
        Mobile = "+974 3312 3456",
        SiteLocation = "Villa 12, Street 340, Al Waab, Doha",
        Password = Password,
    };

    private static async Task<ClientRegistration> RegisterAsync(ApplicationDbContext context, string email = "client@example.com")
    {
        var result = await new RegisterClientCommandHandler(context, new PasswordHasherService(), TimeProvider.System)
            .Handle(Registration(email), CancellationToken.None);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return await context.ClientRegistrations.Include(x => x.Account).SingleAsync(x => x.Email == email.ToLowerInvariant());
    }

    private static Task<ApiResult<ClientRegistrationDto>> ApproveAsync(
        ApplicationDbContext context, ApproveClientRegistrationCommand command) =>
        new ApproveClientRegistrationCommandHandler(context, new FakeCurrentUser(), TimeProvider.System)
            .Handle(command, CancellationToken.None);

    private static Task<TypedResult<LoginResponse>> LoginAsync(
        ApplicationDbContext context, string email, string password) =>
        new LoginCommandHandler(context, new PasswordHasherService(), new FakeTokenGenerator())
            .Handle(new LoginCommand { Email = email, Password = password }, CancellationToken.None);

    // ===== Registering =====

    [Fact]
    public async Task Registering_creates_a_switched_off_client_login_waiting_in_the_queue()
    {
        var context = NewContext();

        var registration = await RegisterAsync(context, "Client@Example.com");

        Assert.Equal(ClientRegistrationStatus.Pending, registration.Status);
        Assert.Equal("client@example.com", registration.Email);
        Assert.Equal(Roles.Client, registration.Account.Role);
        Assert.False(registration.Account.IsActive);
        Assert.Null(registration.Category);
        Assert.Empty(registration.Teams);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_says_it_is_already_waiting()
    {
        var context = NewContext();
        await RegisterAsync(context);

        var second = await new RegisterClientCommandHandler(context, new PasswordHasherService(), TimeProvider.System)
            .Handle(Registration(), CancellationToken.None);

        Assert.False(second.Succeeded);
        Assert.Contains("A registration with this email is already waiting for approval.", second.Errors);
        Assert.Single(context.ClientRegistrations);
    }

    [Fact]
    public async Task Registering_with_a_staff_accounts_email_is_refused_and_leaves_that_account_alone()
    {
        var context = NewContext();
        var staff = new AdminUser { Email = "staff@jamago.qa", FullName = "Staff", Role = Roles.Staff, PasswordHash = "x" };
        context.AdminUsers.Add(staff);
        await context.SaveChangesAsync();

        var result = await new RegisterClientCommandHandler(context, new PasswordHasherService(), TimeProvider.System)
            .Handle(Registration("staff@jamago.qa"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("An account with this email already exists. Sign in instead.", result.Errors);
        Assert.Equal("x", (await context.AdminUsers.SingleAsync()).PasswordHash);
        Assert.Empty(context.ClientRegistrations);
    }

    [Fact]
    public async Task A_rejected_client_can_register_again_and_goes_back_into_the_queue()
    {
        var context = NewContext();
        var registration = await RegisterAsync(context);
        await new RejectClientRegistrationCommandHandler(context, new FakeCurrentUser(), TimeProvider.System)
            .Handle(new RejectClientRegistrationCommand { Id = registration.Id, Reason = "Site address incomplete." },
                CancellationToken.None);

        var again = await new RegisterClientCommandHandler(context, new PasswordHasherService(), TimeProvider.System)
            .Handle(Registration() with { SiteLocation = "Villa 12, Street 340, Zone 56, Al Waab, Doha" },
                CancellationToken.None);

        Assert.True(again.Succeeded);
        var reopened = await context.ClientRegistrations.SingleAsync();
        Assert.Equal(ClientRegistrationStatus.Pending, reopened.Status);
        Assert.Equal("Villa 12, Street 340, Zone 56, Al Waab, Doha", reopened.SiteLocation);
        Assert.Null(reopened.RejectionReason);
        Assert.Null(reopened.ReviewedAt);
    }

    [Theory]
    [InlineData("12", "Enter a valid mobile number.")]
    [InlineData("call me maybe", "Enter a valid mobile number.")]
    [InlineData("", "Mobile number is required.")]
    public void Mobile_numbers_are_checked_loosely_but_must_hold_enough_digits(string mobile, string expected)
    {
        var result = new RegisterClientCommandValidator().Validate(Registration() with { Mobile = mobile });

        Assert.Contains(expected, result.Errors.Select(e => e.ErrorMessage));
        // Stops at the first problem — never "required" AND "invalid" together.
        Assert.Single(result.Errors, e => e.PropertyName == nameof(RegisterClientCommand.Mobile));
    }

    [Theory]
    [InlineData("+974 3139 2007")]
    [InlineData("33123456")]
    [InlineData("+97433123456")]
    public void Ordinary_qatari_numbers_are_accepted(string mobile) =>
        Assert.True(new RegisterClientCommandValidator().Validate(Registration() with { Mobile = mobile }).IsValid);

    [Fact]
    public void Registration_uses_the_same_password_policy_as_every_other_account() =>
        Assert.Contains("Password must include an uppercase letter.",
            new RegisterClientCommandValidator().Validate(Registration() with { Password = "lowercase123" })
                .Errors.Select(e => e.ErrorMessage));

    // ===== Signing in before a decision =====

    [Fact]
    public async Task Signing_in_while_waiting_says_so_but_only_with_the_right_password()
    {
        var context = NewContext();
        await RegisterAsync(context);

        var wrong = await LoginAsync(context, "client@example.com", "WrongPass1");
        Assert.Contains("Invalid email or password.", wrong.Errors);

        var right = await LoginAsync(context, "client@example.com", Password);
        Assert.False(right.Succeeded);
        Assert.Contains(right.Errors, e => e.StartsWith("Your registration is waiting for approval."));
    }

    [Fact]
    public async Task Signing_in_after_rejection_says_it_was_not_approved_without_the_internal_reason()
    {
        var context = NewContext();
        var registration = await RegisterAsync(context);
        await new RejectClientRegistrationCommandHandler(context, new FakeCurrentUser(), TimeProvider.System)
            .Handle(new RejectClientRegistrationCommand { Id = registration.Id, Reason = "Looks like a duplicate of another client." },
                CancellationToken.None);

        var login = await LoginAsync(context, "client@example.com", Password);

        Assert.False(login.Succeeded);
        Assert.Contains("Your registration was not approved. Please contact Jama Go for details.", login.Errors);
        Assert.DoesNotContain(login.Errors, e => e.Contains("duplicate"));
    }

    [Fact]
    public async Task A_deactivated_account_that_never_registered_still_gets_the_vague_message()
    {
        var context = NewContext();
        var hasher = new PasswordHasherService();
        var staff = new AdminUser { Email = "old@jamago.qa", FullName = "Old", Role = Roles.Staff, IsActive = false };
        staff.PasswordHash = hasher.Hash(staff, Password);
        context.AdminUsers.Add(staff);
        await context.SaveChangesAsync();

        var login = await LoginAsync(context, "old@jamago.qa", Password);

        Assert.Contains("Invalid email or password.", login.Errors);
    }

    // ===== Accepting =====

    [Fact]
    public async Task Accepting_a_non_moi_client_switches_the_login_on_with_its_teams_and_no_dia()
    {
        var context = NewContext();
        var registration = await RegisterAsync(context);

        var result = await ApproveAsync(context, new ApproveClientRegistrationCommand
        {
            Id = registration.Id,
            Category = ClientCategory.NonMoi,
            // Ticked out of order on purpose — stored in department order.
            Teams = ["Panels", "Technician"],
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(ClientRegistrationStatus.Approved, registration.Status);
        Assert.Equal(ClientCategory.NonMoi, registration.Category);
        Assert.Equal(["Technician", "Panels"], registration.Teams);
        Assert.True(registration.Account.IsActive);
        Assert.Null(registration.DiaInspectionId);
        Assert.Empty(context.DiaInspections);

        Assert.True((await LoginAsync(context, "client@example.com", Password)).Succeeded);
    }

    [Fact]
    public async Task Accepting_an_moi_client_opens_their_dia_inspection_from_what_they_registered()
    {
        var context = NewContext();
        var registration = await RegisterAsync(context);

        var result = await ApproveAsync(context, new ApproveClientRegistrationCommand
        {
            Id = registration.Id,
            Category = ClientCategory.Moi,
            Teams = ["MoiDiaUpload", "MoiDiaInspection"],
            DiaNumber = "  DIA-2026-0457 ",
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        var dia = await context.DiaInspections.SingleAsync();
        Assert.Equal("DIA-2026-0457", dia.DiaNumber);
        Assert.Equal("DIA-2026-0457", dia.NormalizedDiaNumber);
        Assert.Equal("Ahmed Al-Kuwari", dia.ClientName);
        Assert.Equal("+974 3312 3456", dia.ClientNumber);
        Assert.Equal("Villa 12, Street 340, Al Waab, Doha", dia.ClientLocation);
        // Same as one made by hand: activating starts the inspection clock,
        // and that stays the DIA team's decision.
        Assert.False(dia.IsActive);
        Assert.Equal(dia.Id, registration.DiaInspectionId);
        Assert.Equal("DIA-2026-0457", result.Data!.DiaNumber);

        var history = await context.DiaInspectionHistory.SingleAsync();
        Assert.Equal(DiaInspectionAction.Create, history.Action);
        Assert.Equal(dia.Id, history.DiaInspectionId);
    }

    [Fact]
    public async Task The_dia_record_can_take_a_different_client_number_and_location_than_registered()
    {
        var context = NewContext();
        var registration = await RegisterAsync(context);

        await ApproveAsync(context, new ApproveClientRegistrationCommand
        {
            Id = registration.Id,
            Category = ClientCategory.Moi,
            Teams = ["MoiDiaInspection"],
            DiaNumber = "DIA-77",
            DiaClientNumber = "QID 28763400123",
            DiaClientLocation = "Zone 56, Street 340, Building 349",
        });

        var dia = await context.DiaInspections.SingleAsync();
        Assert.Equal("QID 28763400123", dia.ClientNumber);
        Assert.Equal("Zone 56, Street 340, Building 349", dia.ClientLocation);
    }

    [Fact]
    public async Task An_moi_acceptance_with_a_dia_number_already_in_use_changes_nothing()
    {
        var context = NewContext();
        context.DiaInspections.Add(new DiaInspection
        {
            DiaNumber = "dia-77", NormalizedDiaNumber = "DIA-77",
            ClientName = "Someone else", ClientNumber = "1", ClientLocation = "Lusail",
        });
        await context.SaveChangesAsync();
        var registration = await RegisterAsync(context);

        var result = await ApproveAsync(context, new ApproveClientRegistrationCommand
        {
            Id = registration.Id,
            Category = ClientCategory.Moi,
            Teams = ["MoiDiaInspection"],
            DiaNumber = "DIA-77",
        });

        Assert.False(result.Succeeded);
        Assert.Contains("A DIA inspection with this DIA number already exists.", result.Errors);
        var fresh = await context.ClientRegistrations.AsNoTracking().Include(x => x.Account).SingleAsync();
        Assert.Equal(ClientRegistrationStatus.Pending, fresh.Status);
        Assert.False(fresh.Account.IsActive);
        Assert.Single(context.DiaInspections);
    }

    [Fact]
    public async Task An_accepted_registration_cannot_be_accepted_again_or_rejected()
    {
        var context = NewContext();
        var registration = await RegisterAsync(context);
        var accept = new ApproveClientRegistrationCommand
        {
            Id = registration.Id, Category = ClientCategory.NonMoi, Teams = ["Technician"],
        };
        await ApproveAsync(context, accept);

        var twice = await ApproveAsync(context, accept);
        var reject = await new RejectClientRegistrationCommandHandler(context, new FakeCurrentUser(), TimeProvider.System)
            .Handle(new RejectClientRegistrationCommand { Id = registration.Id, Reason = "Changed my mind." },
                CancellationToken.None);

        Assert.Contains("This registration has already been accepted.", twice.Errors);
        Assert.Contains("This registration has already been accepted.", reject.Errors);
    }

    [Fact]
    public async Task A_rejection_can_be_reversed_by_accepting_it()
    {
        var context = NewContext();
        var registration = await RegisterAsync(context);
        await new RejectClientRegistrationCommandHandler(context, new FakeCurrentUser(), TimeProvider.System)
            .Handle(new RejectClientRegistrationCommand { Id = registration.Id, Reason = "Rejected by mistake." },
                CancellationToken.None);

        var result = await ApproveAsync(context, new ApproveClientRegistrationCommand
        {
            Id = registration.Id, Category = ClientCategory.NonMoi, Teams = ["Technician"],
        });

        Assert.True(result.Succeeded);
        Assert.Null(registration.RejectionReason);
        Assert.True(registration.Account.IsActive);
    }

    [Fact]
    public void Accepting_needs_a_category_a_real_team_and_for_moi_a_dia_number()
    {
        var validator = new ApproveClientRegistrationCommandValidator();

        Assert.Contains("Choose MOI or Non-MOI.",
            validator.Validate(new ApproveClientRegistrationCommand { Teams = ["Technician"] })
                .Errors.Select(e => e.ErrorMessage));
        Assert.Contains("Allot at least one team.",
            validator.Validate(new ApproveClientRegistrationCommand { Category = ClientCategory.NonMoi })
                .Errors.Select(e => e.ErrorMessage));
        // "1" would parse as a department through Enum.TryParse — refused by name.
        Assert.Contains("Choose teams from the list.",
            validator.Validate(new ApproveClientRegistrationCommand { Category = ClientCategory.NonMoi, Teams = ["1"] })
                .Errors.Select(e => e.ErrorMessage));
        Assert.Contains("Enter the DIA number for this MOI client.",
            validator.Validate(new ApproveClientRegistrationCommand { Category = ClientCategory.Moi, Teams = ["MoiDiaInspection"] })
                .Errors.Select(e => e.ErrorMessage));
        Assert.True(validator.Validate(new ApproveClientRegistrationCommand
        {
            Category = ClientCategory.NonMoi, Teams = ["Technician"],
        }).IsValid);
    }

    // ===== Rejecting =====

    [Fact]
    public async Task Rejecting_keeps_the_login_off_and_records_who_and_why()
    {
        var context = NewContext();
        var registration = await RegisterAsync(context);

        var result = await new RejectClientRegistrationCommandHandler(context, new FakeCurrentUser(), TimeProvider.System)
            .Handle(new RejectClientRegistrationCommand { Id = registration.Id, Reason = "  Not in our service area.  " },
                CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ClientRegistrationStatus.Rejected, registration.Status);
        Assert.Equal("Not in our service area.", registration.RejectionReason);
        Assert.NotNull(registration.ReviewedAt);
        Assert.False(registration.Account.IsActive);
    }

    [Fact]
    public void A_rejection_needs_a_real_reason() =>
        Assert.False(new RejectClientRegistrationCommandValidator()
            .Validate(new RejectClientRegistrationCommand { Reason = "no" }).IsValid);

    // ===== Reading =====

    [Fact]
    public async Task The_admin_list_filters_by_status_and_counts_every_status()
    {
        var context = NewContext();
        var first = await RegisterAsync(context, "one@example.com");
        await RegisterAsync(context, "two@example.com");
        await ApproveAsync(context, new ApproveClientRegistrationCommand
        {
            Id = first.Id, Category = ClientCategory.NonMoi, Teams = ["Technician"],
        });

        var pending = await new GetClientRegistrationsQueryHandler(context)
            .Handle(new GetClientRegistrationsQuery(null, null), CancellationToken.None);
        var approved = await new GetClientRegistrationsQueryHandler(context)
            .Handle(new GetClientRegistrationsQuery(ClientRegistrationStatus.Approved, "ONE@"), CancellationToken.None);

        Assert.Equal("two@example.com", Assert.Single(pending.Data!.Items).Email);
        Assert.Equal(new ClientRegistrationCountsDto(1, 1, 0), pending.Data.Counts);
        Assert.Equal(4, pending.Data.AvailableTeams.Count);
        var accepted = Assert.Single(approved.Data!.Items);
        Assert.Equal("Technician", Assert.Single(accepted.Teams).Name);
    }

    [Fact]
    public async Task A_client_sees_their_own_status_but_not_who_reviewed_it()
    {
        var context = NewContext();
        var registration = await RegisterAsync(context);
        await ApproveAsync(context, new ApproveClientRegistrationCommand
        {
            Id = registration.Id,
            Category = ClientCategory.Moi,
            Teams = ["MoiDiaInspection"],
            DiaNumber = "DIA-9",
        });

        var mine = await new GetMyClientRegistrationQueryHandler(context, new FakeCurrentUser(registration.AdminUserId))
            .Handle(new GetMyClientRegistrationQuery(), CancellationToken.None);

        Assert.True(mine.Succeeded);
        Assert.Equal(ClientRegistrationStatus.Approved, mine.Data!.Status);
        Assert.Equal("DIA-9", mine.Data.DiaNumber);
        Assert.Equal("MOI DIA Inspection", Assert.Single(mine.Data.Teams).Name);
    }
}
