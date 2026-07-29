using Jama.Application.Common;

namespace Jama.Application.Tests;

/// <summary>
/// Permissions.EffectiveFor decides what every account may do, and the technician
/// endpoints now depend on it, so the resolution order is pinned here: a mistake
/// either locks technicians out of their own portal or hands out capabilities
/// nobody granted.
/// </summary>
public sealed class PermissionResolutionTests
{
    [Fact]
    public void Admin_holds_every_permission_regardless_of_explicit_grants()
    {
        var effective = Permissions.EffectiveFor(Roles.Admin, []);

        Assert.Equal(
            Permissions.All.Select(p => p.Key).OrderBy(k => k),
            effective.OrderBy(k => k));
    }

    [Fact]
    public void Technician_without_explicit_grants_falls_back_to_the_role_baseline()
    {
        // An unconfigured technician account must still be able to work; the
        // alternative is a user who can reach their portal and do nothing in it.
        var effective = Permissions.EffectiveFor(Roles.Technician, []);

        Assert.Contains(Permissions.DiaInspect, effective);
        Assert.Contains(Permissions.DiaView, effective);
        Assert.Contains(Permissions.InvoiceView, effective);
    }

    [Fact]
    public void Explicit_grants_replace_the_baseline_rather_than_adding_to_it()
    {
        // Unticking "Perform inspections" has to actually remove it, otherwise
        // the checkbox lies about what it does.
        var effective = Permissions.EffectiveFor(Roles.Technician, [Permissions.InvoiceView]);

        Assert.Contains(Permissions.InvoiceView, effective);
        Assert.DoesNotContain(Permissions.DiaInspect, effective);
    }

    [Fact]
    public void Staff_without_grants_hold_nothing()
    {
        Assert.Empty(Permissions.EffectiveFor(Roles.Staff, []));
    }

    [Theory]
    [InlineData(Permissions.DiaUpload)]
    [InlineData(Permissions.DiaInspect)]
    public void Editing_or_inspecting_implies_being_able_to_view(string granted)
    {
        // Granting only "Create DIA records" previously produced an account that
        // reached the DIA screens and then 403'd on every list and detail call.
        var effective = Permissions.EffectiveFor(Roles.Staff, [granted]);

        Assert.Contains(Permissions.DiaView, effective);
        Assert.Contains(granted, effective);
    }

    [Fact]
    public void Unknown_permission_keys_are_discarded()
    {
        var effective = Permissions.EffectiveFor(Roles.Staff, ["not.a.real.permission"]);

        Assert.Empty(effective);
    }

    [Fact]
    public void Duplicate_grants_collapse()
    {
        var effective = Permissions.EffectiveFor(
            Roles.Staff,
            [Permissions.ContactView, Permissions.ContactView]);

        Assert.Equal([Permissions.ContactView], effective);
    }
}
