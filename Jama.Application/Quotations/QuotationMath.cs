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
            var gross = Round(line.Quantity * line.UnitRate);
            var discount = Round(gross * line.DiscountPercent / 100m);
            var net = gross - discount;
            var tax = Round(net * line.TaxPercent / 100m);

            line.LineTotal = net + tax;

            subtotal += gross;
            discountTotal += discount;
            taxTotal += tax;
        }

        quotation.Subtotal = subtotal;
        quotation.DiscountTotal = discountTotal;
        quotation.TaxTotal = taxTotal;
        quotation.GrandTotal = subtotal - discountTotal + taxTotal;
    }

    /// <summary>Two decimals, half away from zero — how money is rounded on paper,
    /// not banker's rounding, which would surprise anyone checking a total.</summary>
    internal static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
