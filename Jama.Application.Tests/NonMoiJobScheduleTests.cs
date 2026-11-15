using Jama.Application.NonMoiJobs.Commands;

namespace Jama.Application.Tests;

/// <summary>Each Non-MOI job carries the visit date and time the issuer chose.</summary>
public class NonMoiJobScheduleTests
{
    private static CreateNonMoiJobCommand Job(DateTimeOffset? at) => new()
    {
        CustomerName = "Al Noor", CustomerNumber = "+974 5550 1234", Location = "Salwa Road", DescriptionEn = "Fix cameras",
        AssignedToId = Guid.NewGuid(), ScheduledAt = at,
    };

    [Fact]
    public void A_visit_time_is_required()
    {
        var v = new CreateNonMoiJobCommandValidator();
        Assert.False(v.Validate(Job(null)).IsValid);
        Assert.True(v.Validate(Job(DateTimeOffset.UtcNow.AddDays(1))).IsValid);
    }

    [Fact]
    public void A_visit_absurdly_far_ahead_or_long_ago_is_refused()
    {
        var v = new CreateNonMoiJobCommandValidator();
        Assert.False(v.Validate(Job(DateTimeOffset.UtcNow.AddYears(5))).IsValid);
        Assert.False(v.Validate(Job(new DateTimeOffset(2001, 1, 1, 9, 0, 0, TimeSpan.FromHours(3)))).IsValid);
    }

    [Fact]
    public void Doha_time_is_kept_exactly_as_the_same_moment_in_utc()
    {
        var dohaTenAm = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.FromHours(3));
        Assert.Equal(new DateTime(2026, 11, 16, 7, 0, 0, DateTimeKind.Utc), dohaTenAm.UtcDateTime);
    }
}
