using FluentValidation;
using Jama.Application.Dia;

namespace Jama.Application.Tests;

public sealed class DiaInspectionCalculatorTests
{
    private static readonly DateTime Activation = new(2024, 1, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Inactive_has_no_dynamic_schedule()
    {
        var result = Calculator(Activation.AddDays(20)).Calculate(false, Activation);
        Assert.Equal(DiaStatus.Inactive, result.Status);
        Assert.Null(result.CurrentQuarter);
        Assert.Null(result.NextInspectionDate);
        Assert.Equal(0, result.ProgressPercent);
    }

    [Theory]
    [InlineData("2024-01-31T12:00:00Z", DiaStatus.Quarter1, 1)]
    [InlineData("2024-04-30T11:59:59Z", DiaStatus.Quarter1, 1)]
    [InlineData("2024-04-30T12:00:00Z", DiaStatus.Quarter2, 2)]
    [InlineData("2024-07-31T12:00:00Z", DiaStatus.Quarter3, 3)]
    [InlineData("2024-10-31T12:00:00Z", DiaStatus.Quarter4, 4)]
    [InlineData("2025-01-31T12:00:00Z", DiaStatus.Completed, null)]
    public void Uses_half_open_quarter_boundaries(string nowValue, DiaStatus expectedStatus, int? expectedQuarter)
    {
        var result = Calculator(DateTime.Parse(nowValue).ToUniversalTime()).Calculate(true, Activation);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedQuarter, result.CurrentQuarter);
    }

    [Fact]
    public void AddMonths_handles_leap_day_activation()
    {
        var activated = new DateTime(2024, 2, 29, 8, 0, 0, DateTimeKind.Utc);
        var result = Calculator(new DateTime(2024, 5, 29, 8, 0, 0, DateTimeKind.Utc))
            .Calculate(true, activated);
        Assert.Equal(DiaStatus.Quarter2, result.Status);
        Assert.Equal(new DateTime(2024, 5, 29, 8, 0, 0, DateTimeKind.Utc), result.QuarterStartDate);
        Assert.Equal(new DateTime(2024, 8, 29, 8, 0, 0, DateTimeKind.Utc), result.QuarterEndDate);
    }

    [Fact]
    public void Month_end_schedule_uses_calendar_months()
    {
        var result = Calculator(Activation.AddDays(1)).Calculate(true, Activation);
        Assert.Equal(new DateTime(2024, 4, 30, 12, 0, 0, DateTimeKind.Utc), result.NextInspectionDate);
        Assert.Equal(25, result.ProgressPercent);
    }

    [Fact]
    public void Future_activation_is_inactive_until_the_period_starts()
    {
        var result = Calculator(Activation.AddMinutes(-1)).Calculate(true, Activation);
        Assert.Equal(DiaStatus.Inactive, result.Status);
        Assert.Null(result.CurrentQuarter);
        Assert.Equal(0, result.ProgressPercent);
    }

    [Fact]
    public void Completed_is_capped_at_one_hundred_percent()
    {
        var result = Calculator(Activation.AddYears(10)).Calculate(true, Activation);
        Assert.Equal(DiaStatus.Completed, result.Status);
        Assert.Equal(100, result.ProgressPercent);
        Assert.Equal(0, result.RemainingDays);
    }

    private static DiaInspectionCalculator Calculator(DateTime now) =>
        new(new FixedTimeProvider(now));

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

public sealed class DiaInspectionValidatorTests
{
    [Fact]
    public async Task Create_rejects_whitespace_and_accepts_trimmed_payload()
    {
        var validator = new CreateDiaInspectionValidator();
        var invalid = await validator.ValidateAsync(new CreateDiaInspectionCommand
        {
            DiaNumber = " ", ClientNumber = "C", ClientName = "Client", ClientLocation = "Doha",
        });
        Assert.False(invalid.IsValid);

        var valid = await validator.ValidateAsync(new CreateDiaInspectionCommand
        {
            DiaNumber = " DIA-1 ", ClientNumber = " C-1 ", ClientName = " Client ", ClientLocation = " Doha ",
        });
        Assert.True(valid.IsValid);
    }

    [Fact]
    public async Task Update_requires_route_identifier_and_length_limits()
    {
        var validator = new UpdateDiaInspectionValidator();
        var result = await validator.ValidateAsync(new UpdateDiaInspectionCommand
        {
            Id = Guid.Empty,
            DiaNumber = new string('D', 101),
            ClientNumber = "C",
            ClientName = "Client",
            ClientLocation = "Doha",
        });
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(UpdateDiaInspectionCommand.Id));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(UpdateDiaInspectionCommand.DiaNumber));
    }
}
