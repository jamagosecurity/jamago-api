using Jama.Application.TravelClaims;

namespace Jama.Application.Tests;

/// <summary>Reading Uber receipts: the total (not the subtotal), the date, the pickup time and the two places.</summary>
public class UberReceiptParserTests
{
    private const string AppReceipt = """
        Uber
        Total QAR 27.50
        November 20, 2026
        Thanks for riding, Hasan
        Trip fare QAR 25.00
        Subtotal QAR 25.00
        Booking fee QAR 2.50
        Payments
        Visa ••••1234 QAR 27.50
        UberX 12.30 kilometres | 21 min
        10:41 AM | Al Sadd Street, Doha, Qatar
        11:02 AM | West Bay Tower 3, Doha, Qatar
        """;

    [Fact]
    public void A_standard_receipt_gives_the_total_day_time_and_places()
    {
        var r = UberReceiptParser.Parse(AppReceipt);

        Assert.Equal(27.50m, r.Amount);                         // the total, not the 25.00 subtotal or fare
        Assert.Equal(new DateOnly(2026, 11, 20), r.TripOn);
        Assert.Equal(new TimeOnly(10, 41), r.TripTime);
        Assert.Equal("Al Sadd Street, Doha, Qatar", r.Pickup);
        Assert.Equal("West Bay Tower 3, Doha, Qatar", r.Dropoff);
        Assert.True(r.Complete);
    }

    /// <summary>A real Uber desktop receipt (Qatar, Nov 2026), text as it comes out of the PDF. It has three other times
    /// before the route: when the receipt was made (1:20 PM) and when the cash payment went through (1:31 PM).</summary>
    private const string DesktopReceipt = """
        Nov 22, 2026
        1:20 PM
        Thanks for riding, SHAIK
        We hope you enjoyed your ride this afternoon.
        Total QAR 12.98
        QAR 1.30
        Uber One credits earned
        Trip fare QAR 12.98
        Payments
        Cash QAR 12.98
        11/22/26 1:31 PM
        Visit the trip page for more information, including invoices (where available)
        This is not a tax invoice. This is a payment receipt for the transportation service provided by Muhmmed Ali.
        Fare does not include fees that may be charged by your bank. Please contact your bank directly for
        inquiries.
        Trip details
        UberXL
        3.07 kilometres, 7 minutes
        License Plate:
        21333
        1:22 PM
        56 Salwa Rd, Doha, Qatar
        1:30 PM
        5 Ibn Aamir St, Doha, Qatar
        You rode with Muhmmed Ali 4.95
        Want to review your trip history? My trips
        """;

    [Fact]
    public void A_real_desktop_receipt_gives_the_route_not_the_receipt_or_payment_times()
    {
        var r = UberReceiptParser.Parse(DesktopReceipt);

        Assert.Equal(12.98m, r.Amount);                          // the total, not the 1.30 Uber One credit
        Assert.Equal(new DateOnly(2026, 11, 22), r.TripOn);
        Assert.Equal(new TimeOnly(13, 22), r.TripTime);           // pickup — not 1:20 (receipt) or 1:31 (payment)
        Assert.Equal("56 Salwa Rd, Doha, Qatar", r.Pickup);
        Assert.Equal("5 Ibn Aamir St, Doha, Qatar", r.Dropoff);
    }

    [Fact]
    public void Columns_that_come_out_on_separate_lines_are_still_read()
    {
        // The same receipt when the PDF puts the right-hand column on its own lines.
        var r = UberReceiptParser.Parse(DesktopReceipt.Replace("Total QAR 12.98", "Total\nQAR 12.98").Replace("Trip fare QAR 12.98", "Trip fare\nQAR 12.98"));
        Assert.Equal(12.98m, r.Amount);
        Assert.Equal("56 Salwa Rd, Doha, Qatar", r.Pickup);
    }

    [Fact]
    public void Times_on_their_own_line_take_the_place_from_the_next_line()
    {
        var r = UberReceiptParser.Parse("""
            Here's your receipt for your ride, Hasan
            21 Nov 2026
            Total
            QR 1,104.25
            2:05 PM
            Lusail Marina, Lusail
            2:40 PM
            Industrial Area Street 24, Doha
            """);

        Assert.Equal(1104.25m, r.Amount);
        Assert.Equal(new DateOnly(2026, 11, 21), r.TripOn);
        Assert.Equal(new TimeOnly(14, 5), r.TripTime);
        Assert.Equal("Lusail Marina, Lusail", r.Pickup);
        Assert.Equal("Industrial Area Street 24, Doha", r.Dropoff);
    }

    [Theory]
    [InlineData("Total 31.00 QAR", 31.00)]
    [InlineData("Total: QAR31", 31)]
    [InlineData("Amount charged QAR 9.75", 9.75)]
    [InlineData("You paid QR 14.20", 14.20)]
    public void The_total_is_found_whichever_way_the_currency_is_written(string line, double expected) =>
        Assert.Equal((decimal)expected, UberReceiptParser.Parse("Uber\n" + line + "\nNov 3, 2026").Amount);

    [Fact]
    public void Without_a_total_line_the_largest_figure_with_the_currency_is_taken()
    {
        var r = UberReceiptParser.Parse("Trip fare QAR 18.00\nBooking fee QAR 2.00\nCharged QAR 20.00");
        Assert.Equal(20.00m, r.Amount);
    }

    [Fact]
    public void A_24_hour_time_and_12_am_and_pm_are_read_correctly()
    {
        Assert.Equal(new TimeOnly(18, 30), UberReceiptParser.Parse("18:30 Doha Festival City").TripTime);
        Assert.Equal(new TimeOnly(0, 15), UberReceiptParser.Parse("12:15 AM Hamad Airport").TripTime);
        Assert.Equal(new TimeOnly(12, 15), UberReceiptParser.Parse("12:15 PM Hamad Airport").TripTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A scanned page with nothing useful on it")]
    public void Text_with_nothing_recognisable_gives_nothing_rather_than_a_guess(string? text)
    {
        var r = UberReceiptParser.Parse(text);
        Assert.Null(r.Amount);
        Assert.Null(r.TripOn);
        Assert.False(r.Complete);
    }

    [Fact]
    public void An_impossible_date_is_not_read()
    {
        Assert.Null(UberReceiptParser.Parse("February 30, 2026\nTotal QAR 10.00").TripOn);
    }
}
