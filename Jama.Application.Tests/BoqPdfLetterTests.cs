using Jama.Application.Common.Interfaces;
using Jama.Infrastructure.Documents;
using UglyToad.PdfPig;

namespace Jama.Application.Tests;

/// <summary>
/// The quotation's commercial offer is a one-page letter — the signature must never spill onto a page of its own. A
/// quotation with no items is three pages: the cover, the letter, the items page. English and Arabic together are two
/// letters and may take two pages.
/// </summary>
public class BoqPdfLetterTests
{
    /// <summary>The typed terms of a real quotation (JMG-00177) whose signature used to land on page 3.</summary>
    private const string Jmg177Terms = """
        1. Offer Validity and Price Escalation
        These prices are valid for a Contract or Purchase Order coming into force during the validity of this offer.
        The offer is valid for 14 days from the date of this offer.
        - Prices may be subject to revision after that validity date.
        - For any variation in quantity, Jama Go reserves the right to revise the pricing and the delivery period.
        - All unit prices are in QAR, inclusive of customs, transportation and any other charges within Qatar, and apply only to the quantities stated.
        2. Terms of Payment
        - 50% advance payment with the LPO, 40% upon delivery, and the remaining 10% upon completion.
        3. Delivery of Materials
        - Material delivery within 3–6 days after official confirmation and advance payment.
        - MOI documentation and approval process 4–8 weeks.
        - Project completion within 8–12 weeks.
        4. Scope
        - Supply of the materials listed in the item descriptions.
        5. Out of Scope
        - Installation, electrical and fibre cabling, and copper cabling for the cameras.
        - Both-side termination, labelling and camera installation, MOI documentation, and project sign-off.
        - Anything not mentioned in the scope.
        6. Warranty
        - Cameras and NVR — 2 years.
        - All other equipment — 1 year.
        - Physical damage is not covered. We follow the manufacturer's standard warranty against manufacturing defects only.
        THIS WARRANTY IS VOID IF the device has been damaged by negligence, mishandling, acts of third parties, accident, fire, flood, lightning, power surges or outages, or other events, or has not been operated in accordance with the operating and installation instructions.
        Any variation in quantity or design suggested by the client or by MOI-SSD will be calculated as a variation and invoiced separately.
        """;


    private static BoqPdfModel Model(string? terms, BoqTermsLanguage language) =>
        new("JMG-00177", "RESIDENTIAL BUILDING MOI SSD -", "Lusail", "MrAHMED MOHD AA -AL IMADI", "+974 5555 0000", new DateOnly(2026, 11, 23), "Draft", null,
            "Khemaies <hichrismart@hotmail.com>", 1000m, 0m, 1000m, [])
        { Watermark = true, TermsOverride = terms, TermsLanguage = language, PreparedByDesignation = "Sales Engineer" };

    private static (int Pages, string LetterText) Render(BoqPdfModel model)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        using var pdf = PdfDocument.Open(new BoqPdfGenerator().Generate(model));
        return (pdf.NumberOfPages, string.Join(" ", pdf.GetPage(2).GetWords().Select(w => w.Text)));
    }

    [Theory]
    [InlineData(true, BoqTermsLanguage.English)]    // the typed terms of JMG-00177
    [InlineData(false, BoqTermsLanguage.English)]   // the standard terms
    [InlineData(false, BoqTermsLanguage.Arabic)]
    public void The_offer_letter_with_its_signature_fits_on_one_page(bool typedTerms, BoqTermsLanguage language)
    {
        var (pages, letter) = Render(Model(typedTerms ? Jmg177Terms : null, language));

        Assert.Equal(3, pages);                          // cover, letter, items — no extra page for the closing
        Assert.Contains("faithfully", letter);           // the signature is on the letter page itself
        Assert.Contains("Khemaies", letter);
    }

    [Fact]
    public void Even_very_long_typed_terms_keep_the_letter_on_one_page()
    {
        var doubled = Jmg177Terms + "\n" + Jmg177Terms;
        Assert.Equal(3, Render(Model(doubled, BoqTermsLanguage.English)).Pages);
    }

    [Fact]
    public void English_and_arabic_together_may_take_two_pages()
    {
        Assert.Equal(4, Render(Model(null, BoqTermsLanguage.Both)).Pages);
    }
}
