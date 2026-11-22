using Jama.Application.Common;

namespace Jama.Application.Tests;

/// <summary>Customer numbers are typed any which way; WhatsApp needs "+<country><number>". Qatar is the default country.</summary>
public class WhatsAppNumbersTests
{
    [Theory]
    [InlineData("5518 1866", "+97455181866")]
    [InlineData("55181866", "+97455181866")]
    [InlineData("+974 5518 1866", "+97455181866")]
    [InlineData("00974 5518 1866", "+97455181866")]
    [InlineData("974 5518 1866", "+97455181866")]
    [InlineData("+974-5518-1866", "+97455181866")]
    [InlineData("87 521252", "+97487521252")]
    [InlineData("+44 7700 900123", "+447700900123")]
    public void A_typed_number_becomes_the_international_form(string typed, string expected) =>
        Assert.Equal(expected, WhatsAppNumbers.ToE164(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("n/a")]
    [InlineData("123")]
    [InlineData("+1234567890123456")]
    public void Something_that_is_not_a_phone_number_gives_nothing(string? typed) =>
        Assert.Null(WhatsAppNumbers.ToE164(typed));
}
