using Jama.Domain.Entities;

namespace Jama.Application.Boqs;

/// <summary>
/// The one place BOQ money is worked out. Always run on the server from the
/// stored lines — a request never carries its own totals.
/// </summary>
internal static class BoqMath
{
    /// <summary>
    /// Rounds each line to fils before summing, so the printed lines add up to
    /// the printed total. Summing exact values and rounding once at the end
    /// leaves a bill that is off by a fil when checked by hand.
    /// </summary>
    internal static void Recalculate(Boq boq)
    {
        decimal total = 0m;

        foreach (var section in boq.Sections)
        {
            foreach (var line in section.Lines)
            {
                line.LineTotal = Round(line.Quantity * line.UnitRate);
                total += line.LineTotal;
            }
        }

        boq.Total = total;
    }

    internal static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
