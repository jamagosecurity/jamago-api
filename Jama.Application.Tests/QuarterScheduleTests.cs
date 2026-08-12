using Jama.Application.Dia;
using Jama.Application.Technician;

namespace Jama.Application.Tests;

/// <summary>
/// The quarter a site is in follows the calendar from its activation date, and a
/// window that closes with nothing submitted is counted as overdue rather than as
/// progress. These pin both halves of that: the position must move with time, and
/// missed work must never read as done.
/// </summary>
public sealed class QuarterScheduleTests
{
    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static DiaInspectionCalculator DiaAt(string utcDate) =>
        new(new FixedTime(DateTimeOffset.Parse(utcDate + "T00:00:00Z")));

    private static TechnicianInspectionCalculator TechAt(string utcDate) =>
        new(new FixedTime(DateTimeOffset.Parse(utcDate + "T00:00:00Z")));

    [Theory]
    [InlineData("2026-08-12", "2026-08-10", 1, 0)] // activated two days ago
    [InlineData("2026-08-12", "2026-05-01", 2, 1)] // one window closed
    [InlineData("2026-08-12", "2025-12-08", 3, 2)] // two closed
    [InlineData("2026-08-12", "2025-08-12", 4, 4)] // a full year, nothing done
    [InlineData("2026-08-12", "2024-01-01", 4, 4)] // older than the cycle, clamped
    public void Quarter_follows_the_calendar_and_counts_missed_windows(
        string today, string activated, int expectedQuarter, int expectedOverdue)
    {
        var result = DiaAt(today).Calculate(true, DateTime.Parse(activated), submittedQuarters: 0);

        Assert.Equal(expectedQuarter, result.CurrentQuarter);
        Assert.Equal(expectedOverdue, result.OverdueQuarters);
        Assert.Equal((DiaStatus)expectedQuarter, result.Status);
        // Nothing was submitted, so nothing is progress however long it has run.
        Assert.Equal(0m, result.ProgressPercent);
    }

    [Fact]
    public void A_technician_working_ahead_of_the_calendar_still_advances()
    {
        // Activated a month ago, so the calendar says quarter 1 — but two quarters
        // are already submitted, which should unlock the third.
        var result = DiaAt("2026-08-12").Calculate(true, DateTime.Parse("2026-07-12"), submittedQuarters: 2);

        Assert.Equal(3, result.CurrentQuarter);
        Assert.Equal(0, result.OverdueQuarters);
        Assert.Equal(50m, result.ProgressPercent);
    }

    [Fact]
    public void Submitting_on_time_leaves_nothing_overdue()
    {
        // Two windows closed and two inspections were submitted.
        var result = DiaAt("2026-08-12").Calculate(true, DateTime.Parse("2025-12-08"), submittedQuarters: 2);

        Assert.Equal(3, result.CurrentQuarter);
        Assert.Equal(0, result.OverdueQuarters);
        Assert.Equal(50m, result.ProgressPercent);
    }

    [Fact]
    public void Four_submitted_completes_the_cycle_regardless_of_the_calendar()
    {
        var result = DiaAt("2026-08-12").Calculate(true, DateTime.Parse("2025-12-08"), submittedQuarters: 4);

        Assert.Equal(DiaStatus.Completed, result.Status);
        Assert.Equal(100m, result.ProgressPercent);
        Assert.Equal(0, result.OverdueQuarters);
    }

    [Fact]
    public void An_inactive_record_has_no_schedule()
    {
        var result = DiaAt("2026-08-12").Calculate(false, DateTime.Parse("2025-12-08"), submittedQuarters: 0);

        Assert.Equal(DiaStatus.Inactive, result.Status);
        Assert.Null(result.CurrentQuarter);
        Assert.Equal(0, result.OverdueQuarters);
    }

    [Fact]
    public void The_technician_cycle_runs_from_activation_when_no_one_has_started()
    {
        // The imported sites have an activation date and no start date. The
        // quarterly clock belongs to the MOI schedule, so it is already running.
        var result = TechAt("2026-08-12").Calculate(DateTime.Parse("2025-12-08"), submittedQuarters: 0);

        Assert.Equal(3, result.CurrentQuarter);
        Assert.Equal(2, result.OverdueQuarters);
        Assert.Equal(0m, result.ProgressPercent);
        Assert.Equal(TechnicianInspectionCycleStatus.Quarter3, result.Status);
    }

    [Fact]
    public void A_schedule_that_has_not_begun_yet_is_not_started()
    {
        var result = TechAt("2026-08-12").Calculate(DateTime.Parse("2026-12-01"), submittedQuarters: 0);

        Assert.Equal(TechnicianInspectionCycleStatus.NotStarted, result.Status);
        Assert.Null(result.CurrentQuarter);
    }

    [Fact]
    public void An_unscheduled_record_is_not_started()
    {
        var result = TechAt("2026-08-12").Calculate(null, submittedQuarters: 0);

        Assert.Equal(TechnicianInspectionCycleStatus.NotStarted, result.Status);
        Assert.Null(result.CurrentQuarter);
    }
}
