using Jama.Application.Quotations;
using Jama.Application.Quotations.Commands.CreateQuotation;
using Jama.Domain.Entities;
using Jama.Infrastructure.Documents;

namespace Jama.Application.Tests;

/// <summary>
/// The discount given on a finished quotation, in QAR, and the two things that
/// have to stay true of it: the printed total is the one the customer pays, and
/// the sentence under it says the same figure in both languages.
/// </summary>
public class QuotationDiscountTests
{
    private static Quotation QuoteOf(decimal specialDiscount, params (decimal Qty, decimal Rate)[] lines)
    {
        var quotation = new Quotation { SpecialDiscount = specialDiscount };

        foreach (var (quantity, rate) in lines)
            quotation.Lines.Add(new QuotationLine { Quantity = quantity, UnitRate = rate });

        QuotationMath.Recalculate(quotation);
        return quotation;
    }

    [Fact]
    public void Discount_comes_off_the_total()
    {
        var quote = QuoteOf(250m, (2, 500m), (1, 200m));

        Assert.Equal(1200m, quote.Subtotal);
        Assert.Equal(1200m, QuotationMath.TotalBeforeDiscount(quote));
        Assert.Equal(250m, quote.SpecialDiscount);
        Assert.Equal(950m, quote.GrandTotal);
    }

    [Fact]
    public void No_discount_leaves_the_total_alone()
    {
        var quote = QuoteOf(0m, (3, 400m));

        Assert.Equal(0m, quote.SpecialDiscount);
        Assert.Equal(1200m, quote.GrandTotal);
        Assert.Equal(quote.GrandTotal, QuotationMath.TotalBeforeDiscount(quote));
    }

    [Fact]
    public void Line_discount_and_tax_are_both_inside_the_figure_the_discount_comes_off()
    {
        var quotation = new Quotation { SpecialDiscount = 100m };
        quotation.Lines.Add(new QuotationLine
        {
            Quantity = 1,
            UnitRate = 1000m,
            DiscountPercent = 10m,
            TaxPercent = 5m,
        });

        QuotationMath.Recalculate(quotation);

        // 1000 gross, 100 off the line, 45 tax on the 900 net = 945 to discount from.
        Assert.Equal(945m, QuotationMath.TotalBeforeDiscount(quotation));
        Assert.Equal(845m, quotation.GrandTotal);
    }

    /// <summary>
    /// The validator rejects a discount larger than the quote, but lines can also
    /// be deleted after one was agreed. Nothing payable may print as a negative.
    /// </summary>
    [Fact]
    public void A_discount_larger_than_the_quote_is_clamped_not_carried_negative()
    {
        var quote = QuoteOf(5_000m, (1, 500m));

        Assert.Equal(500m, quote.SpecialDiscount);
        Assert.Equal(0m, quote.GrandTotal);
    }

    [Fact]
    public void A_negative_discount_is_treated_as_none()
    {
        var quote = QuoteOf(-50m, (1, 500m));

        Assert.Equal(0m, quote.SpecialDiscount);
        Assert.Equal(500m, quote.GrandTotal);
    }

    // ===== Validation =====

    private static CreateQuotationCommand Command(decimal specialDiscount) =>
        new()
        {
            CustomerName = "Al Kaabi Trading",
            SpecialDiscount = specialDiscount,
            Lines =
            [
                new QuotationLineInput { ItemName = "Dome camera", Quantity = 2, UnitRate = 500m },
            ],
        };

    [Fact]
    public void A_discount_over_the_quotation_total_is_refused()
    {
        var result = new CreateQuotationCommandValidator().Validate(Command(1_200m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("1,000.00 QAR"));
    }

    [Fact]
    public void A_discount_of_the_whole_quotation_is_allowed()
    {
        Assert.True(new CreateQuotationCommandValidator().Validate(Command(1_000m)).IsValid);
    }

    [Fact]
    public void A_negative_discount_is_refused()
    {
        var result = new CreateQuotationCommandValidator().Validate(Command(-1m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("cannot be negative"));
    }

    // ===== The sentence under the total =====

    [Theory]
    [InlineData(0, "Zero riyals only")]
    [InlineData(1, "One riyal only")]
    [InlineData(950, "Nine hundred fifty riyals only")]
    [InlineData(85_248.70, "Eighty-five thousand two hundred forty-eight riyals and seventy dirhams only")]
    public void The_amount_is_written_out_in_English(decimal amount, string expected) =>
        Assert.Equal(expected, MoneyWords.English(amount));

    [Theory]
    [InlineData(1, "فقط ريال قطري واحد لا غير")]
    [InlineData(2, "فقط ريالان قطريان لا غير")]
    [InlineData(3, "فقط ثلاثة ريالات قطرية لا غير")]
    [InlineData(11, "فقط أحد عشر ريالاً قطرياً لا غير")]
    [InlineData(100, "فقط مائة ريال قطري لا غير")]
    [InlineData(950.50, "فقط تسعمائة وخمسون ريالاً قطرياً وخمسون درهماً لا غير")]
    [InlineData(85_248.70, "فقط خمسة وثمانون ألفاً ومائتان وثمانية وأربعون ريالاً قطرياً وسبعون درهماً لا غير")]
    public void The_amount_is_written_out_in_Arabic(decimal amount, string expected) =>
        Assert.Equal(expected, MoneyWords.Arabic(amount));

    /// <summary>Words and figure are rounded the same way, so a half-fil cannot
    /// resolve one up and the other down.</summary>
    [Fact]
    public void The_words_round_with_the_printed_figure()
    {
        Assert.Equal("One riyal and fifty-seven dirhams only", MoneyWords.English(1.565m));
        Assert.Equal("فقط ريال قطري واحد وسبعة وخمسون درهماً لا غير", MoneyWords.Arabic(1.565m));
    }
}
