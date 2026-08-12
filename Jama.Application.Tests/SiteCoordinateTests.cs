using Jama.Application.Dia;
using Jama.Application.Dia.Commands.CreateDiaInspection;
using Jama.Application.Dia.Commands.UpdateDiaInspection;

namespace Jama.Application.Tests;

/// <summary>
/// The site pin drives the technician's Navigate button, so a pin that saves has
/// to be one a maps app can actually route to.
/// </summary>
public sealed class SiteCoordinateTests
{
    [Theory]
    [InlineData(null, null)] // unpinned is the default and stays valid
    [InlineData(25.286106, 51.534817)] // Doha
    [InlineData(-90d, -180d)] // the corners of the valid range are inclusive
    [InlineData(90d, 180d)]
    public async Task Accepts_a_complete_in_range_pin_or_none_at_all(double? latitude, double? longitude)
    {
        var result = await new CreateDiaInspectionCommandValidator()
            .ValidateAsync(Command(latitude, longitude));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(25.286106, null)] // half a pin cannot be navigated to
    [InlineData(null, 51.534817)]
    [InlineData(91d, 51.534817)] // out of range
    [InlineData(25.286106, 181d)]
    [InlineData(0d, 0d)] // the Atlantic — in practice, a failed paste
    public async Task Rejects_pins_that_cannot_be_navigated_to(double? latitude, double? longitude)
    {
        var result = await new CreateDiaInspectionCommandValidator()
            .ValidateAsync(Command(latitude, longitude));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Update_applies_the_same_pin_rules_as_create()
    {
        var validator = new UpdateDiaInspectionCommandValidator();
        var half = await validator.ValidateAsync(new UpdateDiaInspectionCommand
        {
            Id = Guid.NewGuid(),
            DiaNumber = "DIA-1",
            ClientNumber = "C-1",
            ClientName = "Client",
            ClientLocation = "Doha",
            Latitude = 25.286106,
        });
        Assert.False(half.IsValid);

        var pinned = await validator.ValidateAsync(new UpdateDiaInspectionCommand
        {
            Id = Guid.NewGuid(),
            DiaNumber = "DIA-1",
            ClientNumber = "C-1",
            ClientName = "Client",
            ClientLocation = "Doha",
            Latitude = 25.286106,
            Longitude = 51.534817,
        });
        Assert.True(pinned.IsValid);
    }

    [Fact]
    public void Rounds_to_six_decimals_so_float_noise_never_reads_as_an_edit()
    {
        Assert.Equal(25.286106, SiteCoordinates.Round(25.2861064999));
        Assert.Equal(51.534818, SiteCoordinates.Round(51.5348175));
        Assert.Null(SiteCoordinates.Round(null));
    }

    private static CreateDiaInspectionCommand Command(double? latitude, double? longitude) => new()
    {
        DiaNumber = "DIA-1",
        ClientNumber = "C-1",
        ClientName = "Client",
        ClientLocation = "Doha",
        Latitude = latitude,
        Longitude = longitude,
    };
}
