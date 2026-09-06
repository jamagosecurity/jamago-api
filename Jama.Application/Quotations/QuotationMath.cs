using Jama.Domain.Entities;

namespace Jama.Application.Quotations;

/// <summary>
/// The one place quotation money is worked out.
///
/// Always run on the server from the stored lines, never taken from the client:
/// a request that carries its own totals is a request that can quote 500 QAR of
/// equipment for 5. The client computes the same figures purely so the form can
/// show a running total while typing.
/// </summary>
internal static class QuotationMath
{
    /// <summary>
    /// Rounds each line to fils before summing, rather than summing exact values
    /// and rounding once at the end. The printed lines have to add up to the
    /// printed total — a customer checking the arithmetic by hand must not find
    /// it off by a fil.
    /// </summary>
    internal static void Recalculate(Quotation quotation)
    {
        decimal subtotal = 0m;
        decimal discountTotal = 0m;
        decimal taxTotal = 0m;

        foreach (var line in quotation.Lines)
        {
            var (gross, discount, net, tax) =
                Figures(line.Quantity, line.UnitRate, line.DiscountPercent, line.TaxPercent);

            line.LineTotal = net + tax;

            subtotal += gross;
            discountTotal += discount;
            taxTotal += tax;
        }

        quotation.Subtotal = subtotal;
        quotation.DiscountTotal = discountTotal;
        quotation.TaxTotal = taxTotal;

        var total = subtotal - discountTotal + taxTotal;

        // Clamped rather than trusted. The validator already rejects a discount
        // larger than the lines it comes off, but a quote whose lines are cut
        // down afterwards would otherwise print a negative amount payable.
        quotation.SpecialDiscount = Math.Clamp(Round(quotation.SpecialDiscount), 0m, total);
        quotation.GrandTotal = total - quotation.SpecialDiscount;
    }

    /// <summary>
    /// What a set of submitted lines comes to before any quote-level discount —
    /// the ceiling the validator measures a proposed discount against, worked out
    /// from the same arithmetic <see cref="Recalculate"/> will apply on save.
    /// </summary>
    internal static decimal TotalOf(IEnumerable<QuotationLineInput> lines)
    {
        decimal total = 0m;

        foreach (var line in lines)
        {
            var (_, _, net, tax) =
                Figures(line.Quantity, line.UnitRate, line.DiscountPercent, line.TaxPercent);
            total += net + tax;
        }

        return total;
    }

    /// <summary>The figure the discount was taken off, for a quotation already
    /// recalculated — the "Total" line above it on the document.</summary>
    internal static decimal TotalBeforeDiscount(Quotation quotation) =>
        quotation.GrandTotal + quotation.SpecialDiscount;

    /// <summary>One line's money, rounded to fils at each step so a line reads the
    /// same whether it is being summed into a stored total or measured for a
    /// discount ceiling.</summary>
    private static (decimal Gross, decimal Discount, decimal Net, decimal Tax) Figures(
        decimal quantity, decimal unitRate, decimal discountPercent, decimal taxPercent)
    {
        var gross = Round(quantity * unitRate);
        var discount = Round(gross * discountPercent / 100m);
        var net = gross - discount;
        var tax = Round(net * taxPercent / 100m);

        return (gross, discount, net, tax);
    }

    /// <summary>Two decimals, half away from zero — how money is rounded on paper,
    /// not banker's rounding, which would surprise anyone checking a total.</summary>
    internal static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
