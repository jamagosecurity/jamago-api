using System.Globalization;
using System.Reflection;
using Jama.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// The signed contract a client receives once their quotation is approved —
/// transcribed verbatim (both languages) from the company's reference
/// contract, JMG-00037, so a reviewer who has seen that document recognises
/// this one as the same form rather than a redesign of it.
///
/// Almost everything on it is fixed company boilerplate — warranty, payment
/// split, project phases, scope of work, delivery terms, cancellation — the
/// same on every contract. Only a handful of fields come from the quotation
/// itself: <see cref="ContractPdfModel"/> is exactly that handful.
///
/// Three A4 portrait pages, matching the reference's own pagination.
/// </summary>
public sealed class ContractPdfGenerator : IContractPdfGenerator
{
    private static readonly Color Brand = Color.FromHex("#2594D2");
    private static readonly Color Accent = Color.FromHex("#F6993D");
    private static readonly Color Ink = Color.FromHex("#0F2846");
    private static readonly Color InkSoft = Color.FromHex("#3E5872");
    private static readonly Color Muted = Color.FromHex("#7C93A6");
    private static readonly Color Line = Color.FromHex("#E3EBF1");
    private static readonly Color White = Color.FromHex("#FFFFFF");
    private static readonly Color FooterBg = Color.FromHex("#0F2846");

    private const string CompanyName = "JAMA GO SECURITY EQUIPMENTS";
    private const string CompanyNameAr = "شركة جاما جو للمعدات الأمنية";
    private const string CompanyPhone1 = "+974 4001 3599";
    private const string CompanyPhone2 = "+974 3064 4006";
    private const string CompanyEmail = "info@jamaelectronics.com";
    private const string CompanyAddress =
        "Showroom, Unit 41, Building 349, St 340, Zone 56, Al Ain Complex, Salwa Road";

    private static readonly byte[] LogoBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-logo.png");

    /// <summary>The authorised stamp, same asset and same graceful-degrade
    /// rule as <see cref="MoiStoragePdfGenerator"/>: absent on disk, the page
    /// prints without it rather than failing.</summary>
    private static readonly byte[] StampBytes =
        LoadEmbedded("Jama.Infrastructure.Documents.Assets.jamago-stamp.png");

    private static byte[] LoadEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return [];
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>One line of a bilingual bulleted section. A continuation line
    /// (<see cref="Bullet"/> false) carries no marker of its own and sits
    /// indented under the point it belongs to — the payment section's
    /// security-cheque clause is one of these, not a point in its own right.</summary>
    private sealed record Clause(string Text, bool Bullet = true);

    /// <summary>A heading plus its lines, in both languages, lines paired by
    /// position — line N of <paramref name="En"/> is line N of
    /// <paramref name="Ar"/>.</summary>
    private sealed record Section(string HeadingEn, string HeadingAr, Clause[] En, Clause[] Ar);

    public byte[] Generate(ContractPdfModel model)
    {
        DocumentFonts.EnsureRegistered();

        var sections1 = PageOneSections(model);
        var sections2 = PageTwoSections();

        return Document.Create(container =>
        {
            container.Page(page => ComposePage(page, model, 1, content =>
            {
                content.Item().AlignCenter().Text("عقد توريد و تركيب نظام كاميرات مراقبة")
                    .FontFamily(DocumentFonts.Arabic).FontSize(14).Bold().FontColor(Ink);

                content.Item().PaddingTop(10).Element(x => ComposeToBlock(x, model));

                foreach (var section in sections1)
                    content.Item().PaddingTop(9).Element(x => ComposeSection(x, section));
            }));

            container.Page(page => ComposePage(page, model, 2, content =>
            {
                foreach (var section in sections2)
                    content.Item().PaddingTop(9).Element(x => ComposeSection(x, section));
            }));

            container.Page(page => ComposePage(page, model, 3, content =>
            {
                content.Item().PaddingTop(60).AlignCenter().Text("ختم و توقيع / Signature & Stamp")
                    .FontSize(12).Bold().FontColor(Ink);

                content.Item().PaddingTop(40).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignCenter().Text("CUSTOMER").FontSize(10).Bold().FontColor(InkSoft);
                        col.Item().PaddingTop(50).AlignCenter()
                            .Width(160).BorderBottom(1).BorderColor(Line);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignCenter().Text(CompanyName)
                            .FontSize(9.5f).Bold().FontColor(InkSoft);
                        col.Item().AlignCenter().Text(CompanyNameAr)
                            .FontFamily(DocumentFonts.Arabic).FontSize(9.5f).Bold().FontColor(InkSoft);

                        if (StampBytes.Length > 0)
                            col.Item().PaddingTop(10).AlignCenter().Width(150).Image(StampBytes);
                        else
                            col.Item().PaddingTop(50).AlignCenter()
                                .Width(160).BorderBottom(1).BorderColor(Line);
                    });
                });
            }));
        }).GeneratePdf();
    }

    private static void ComposePage(
        PageDescriptor page,
        ContractPdfModel model,
        int pageNumber,
        Action<ColumnDescriptor> content)
    {
        page.Size(PageSizes.A4);
        page.Margin(26);
        page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Ink).FontFamily(DocumentFonts.Body));

        page.Header().Element(header => ComposeHeader(header, model, pageNumber));
        page.Content().PaddingTop(10).Column(content);
        page.Footer().Element(ComposeFooter);
    }

    // ===== Letterhead =====

    private static void ComposeHeader(IContainer container, ContractPdfModel model, int pageNumber)
    {
        container.Row(row =>
        {
            if (LogoBytes.Length > 0)
                row.ConstantItem(140).AlignMiddle().Image(LogoBytes).FitWidth();
            else
                row.ConstantItem(140).AlignMiddle().Text(CompanyName)
                    .FontSize(11).Bold().FontColor(Brand);

            row.RelativeItem();

            row.ConstantItem(150).Border(1).BorderColor(Line).Padding(6).Column(col =>
            {
                col.Item().Text($"Contract # : {model.ContractNumber}").FontSize(8).FontColor(InkSoft);
                col.Item().Text($"Date : {LongDate(model.ApprovedAt)}").FontSize(8).FontColor(InkSoft);
                col.Item().Text($"Page : {pageNumber}/3").FontSize(8).FontColor(InkSoft);
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Background(FooterBg).PaddingVertical(8).PaddingHorizontal(16).Row(row =>
        {
            row.RelativeItem().Text($"{CompanyPhone1}   {CompanyPhone2}")
                .FontSize(7.5f).FontColor(White);
            row.RelativeItem().AlignCenter().Text(CompanyEmail).FontSize(7.5f).FontColor(White);
            row.RelativeItem().AlignRight().Text(CompanyAddress).FontSize(7).FontColor(White);
        });
    }

    // ===== To: block =====

    private static void ComposeToBlock(IContainer container, ContractPdfModel model)
    {
        container.Border(1).BorderColor(Line).Padding(9).Column(col =>
        {
            ToRow(col, "To:", "السادة", "Mr.:", model.ClientName ?? "—");
            ToRow(col, "", "", "Mobile:", model.ContactNumber ?? "—");
            ToRow(col, "", "", "Project:", model.ProjectName);
            ToRow(col, "", "", "Address:", model.SiteLocation ?? "—");
        });
    }

    private static void ToRow(ColumnDescriptor col, string en, string ar, string labelEn, string value)
    {
        col.Item().PaddingTop(2).Row(row =>
        {
            row.RelativeItem().Row(inner =>
            {
                inner.ConstantItem(50).Text(labelEn).FontSize(8.5f).Bold().FontColor(InkSoft);
                inner.RelativeItem().Text(value).FontSize(8.5f).FontColor(Ink);
            });

            row.RelativeItem().ContentFromRightToLeft().AlignRight().Text(ar)
                .FontFamily(DocumentFonts.Arabic).FontSize(8.5f).Bold().FontColor(InkSoft);
        });
    }

    // ===== Bilingual sections =====

    private static void ComposeSection(IContainer container, Section section)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(section.HeadingEn).FontSize(9.5f).Bold().FontColor(Brand);
                row.RelativeItem().ContentFromRightToLeft().AlignRight().Text(section.HeadingAr)
                    .FontFamily(DocumentFonts.Arabic).FontSize(9.5f).Bold().FontColor(Brand);
            });

            col.Item().PaddingTop(2).Height(1).Background(Line);

            var rows = Math.Max(section.En.Length, section.Ar.Length);
            for (var i = 0; i < rows; i++)
            {
                var en = i < section.En.Length ? section.En[i] : null;
                var ar = i < section.Ar.Length ? section.Ar[i] : null;

                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Element(x => ComposeLine(x, en, rightToLeft: false));
                    row.ConstantItem(10);
                    row.RelativeItem().Element(x => ComposeLine(x, ar, rightToLeft: true));
                });
            }
        });
    }

    private static void ComposeLine(IContainer container, Clause? line, bool rightToLeft)
    {
        if (line is null) return;

        var family = rightToLeft ? DocumentFonts.Arabic : Fonts.Calibri;

        if (!line.Bullet)
        {
            // A continuation, not a point of its own — indented under the
            // bullet above it, no marker.
            if (rightToLeft)
                container.PaddingRight(14).ContentFromRightToLeft().AlignRight()
                    .Text(line.Text).FontFamily(family).FontSize(8.2f).FontColor(InkSoft).LineHeight(1.3f);
            else
                container.PaddingLeft(14).Text(line.Text)
                    .FontFamily(family).FontSize(8.2f).FontColor(InkSoft).LineHeight(1.3f);
            return;
        }

        if (rightToLeft)
        {
            container.ContentFromRightToLeft().Row(row =>
            {
                row.RelativeItem().AlignRight().Text(line.Text)
                    .FontFamily(family).FontSize(8.2f).FontColor(InkSoft).LineHeight(1.3f);
                row.ConstantItem(10).AlignRight().Text("•").FontSize(8.2f).FontColor(Accent);
            });
        }
        else
        {
            container.Row(row =>
            {
                row.ConstantItem(10).Text("•").FontSize(8.2f).FontColor(Accent);
                row.RelativeItem().Text(line.Text)
                    .FontFamily(family).FontSize(8.2f).FontColor(InkSoft).LineHeight(1.3f);
            });
        }
    }

    // ===== Content: page 1 =====

    private static Section[] PageOneSections(ContractPdfModel model)
    {
        var amount = model.GrandTotal.ToString("N2", CultureInfo.InvariantCulture);
        var wordsEn = MoneyWords.English(model.GrandTotal);
        var wordsAr = MoneyWords.Arabic(model.GrandTotal);
        var date = LongDate(model.ApprovedAt);

        return
        [
            new Section(
                "Warranty:", "الضمان :",
                [
                    new Clause("2 Years warranty for Cameras and NVR."),
                    new Clause("1 Year warranty for devices."),
                ],
                [
                    new Clause("ضمان 2 سنة على الكاميرات و الجهاز التسجيل ."),
                    new Clause("ضمان سنة على المعدات من تاريخ التسليم ."),
                ]),

            new Section(
                "Quotation:", "عرض السعر:",
                [
                    new Clause(
                        $"Quotation No. {model.ContractNumber} amount of QR.{amount} " +
                        $"( {wordsEn} ) dated: {date} agreed by both parties."),
                ],
                [
                    new Clause(
                        $"عرض السعر رقم: {model.ContractNumber} بمبلغ: {amount} " +
                        $") {wordsAr} ( ريال قطري بتاريخ: {date} والمتوافق عليه من الطرفين."),
                ]),

            new Section(
                "Payment:", "الدفعات:",
                [
                    new Clause("70% advance payment with the LPO."),
                    new Clause(
                        "A security cheque is to be submitted against the down payment, to be " +
                        "returned upon completion of the equipment delivery and installation.",
                        Bullet: false),
                    new Clause("20% upon Material delivery & During project Installation."),
                    new Clause("10% upon DIA completion."),
                ],
                [
                    new Clause("70% مقدمة عند ايداع أمر الشراء."),
                    new Clause(
                        "يتم تسليم شيك ضمان مقابل الدفعة الأولى، على أن يسترد بعد انتهاء " +
                        "تسليم المعدات و التركيبات.",
                        Bullet: false),
                    new Clause("20% عند توريد المعدات و بدء أعمال التركيب ."),
                    new Clause(
                        "10% بعد تسليم المشروع و استلام كامل التراخيص من ادارة الانظمة الأمنية ."),
                ]),

            new Section(
                "Project implementation phases:", "مراحل تنفيذ المشروع:",
                [
                    new Clause("DSA design phase. ( Up to MOI )"),
                    new Clause("Piping, cable pulling, and control room establishment phase."),
                    new Clause("System installation, configuration"),
                    new Clause("Inspection phase (DIA). ( Up to MOI )"),
                ],
                [
                    new Clause("مرحلة التصميم DSA .( حسب وزارة الداخلية )"),
                    new Clause("مرحلة تمديد البيبات و سحب الكيبلات و تأسيس غرفة التحكم ."),
                    new Clause("مرحلة تثبيت و تركيب و برمجة النظام"),
                    new Clause("عمل المرحلة التفتيشية DIA .( حسب وزارة الداخلية )"),
                ]),

            new Section(
                "Project Completion :", "مدة تنفيذ المشروع:",
                [new Clause("Within 4 Months.")],
                [new Clause("4 أشهر من تاريخ استلام الدفعة المقدمة.")]),
        ];
    }

    // ===== Content: page 2 =====

    private static Section[] PageTwoSections() =>
    [
        new Section(
            "Scope Of Work:", "مجال العمل:",
            [
                new Clause(
                    "Design submission, Supply, Installation, Configuration, Testing, " +
                    "Commissioning and Submitting for Moi Approvals for CCTV System.",
                    Bullet: false),
                new Clause("The proposed offer is based on BOQ approved by the client."),
                new Clause(
                    "The client must provide us with suitable and valid AutoCAD drawings, and " +
                    "the company must be notified of any significant changes made to the building."),
                new Clause("Electrical work is excluded from this scope."),
            ],
            [
                new Clause(
                    "اعداد المخططات و توريد و تركيب نظام كاميرات المراقبة و المعدات اللازمة و " +
                    "الحصول على كل الموافقات الأمنية لنظام كاميرات المراقبة حسب ماهو موضح في " +
                    "عرض السعر المرفق.",
                    Bullet: false),
                new Clause("يعتمد العرض المقترح على جدول الكميات المتفق عليه مع العميل."),
                new Clause(
                    "يجب أن يتم توفير رسومات أتوكاد المناسبة والصالحة لنا من قبل العميل، وفي حال " +
                    "احداث تغييرات جوهرية عن المبنى، يتم اعلام الشركة بذلك."),
                new Clause("تُستثنى الأعمال الكهربائية من هذا الاتفاق."),
            ]),

        new Section(
            "Prices:", "الأسعار:",
            [new Clause("All Prices are in Qatari Riyals (QAR).")],
            [new Clause("جميع الأسعار بالريال القطري")]),

        new Section(
            "Delivery Terms:", "شروط التسليم :",
            [
                new Clause(
                    "In Case of any Additions of Products/Materials/Wiring for the project will " +
                    "be subjected for a Revised Contract upon the Existing or a new contract with " +
                    "the given details by the client, any verbal Variations will not be confirmed " +
                    "or considered as confirmed variations."),
                new Clause(
                    "Delivery Products/Materials/Wiring only as per the items stated in the " +
                    "contract, any other deals by mouth/emails/social media is not approved."),
                new Clause(
                    "In case the project is Under Construction Project, Delivery of the project " +
                    "on time depending on Coordination with Main Contractor/Electrical " +
                    "Contractor/Decoration Contractor to ensure the delivery to be as per " +
                    "submitted Timeline & as per Operations Department schedule."),
                new Clause(
                    "In Case of Non-Successful payment on time the project Execution will be " +
                    "postponed until the due payment to be completed. While a Per Completing the " +
                    "concerned payment another appointment will be adjusted by the Operations " +
                    "Department."),
            ],
            [
                new Clause(
                    "في حاله إضافة معدات أو ملحقات إضافية يتم عمل ملحق عقد أو عمل عقد معدل " +
                    "بالمتطلبات ولا يتم تأكيد أي زيادات أو تعديلات بشكل شفهي."),
                new Clause(
                    "التسليم للبضائع/المنتجات/التمديدات المذكورة في العقد فقط ولا يتم إعتماد أي " +
                    "اتفاقات أخرى (شفاهية، تواصل اجتماعي، بريد إلكتروني) ولن يتم إعتمادها."),
                new Clause(
                    "في حالة اذا كان المشروع تحت الإنشاء تسليم المشروع في الموعد يتطلب التنسيق مع " +
                    "المقاول الرئيسي/مقاول الكهرباء/مقاول الجبس لضمان التسليم في الموعد المحدد بناءا " +
                    "على مراحل العمل وما يتناسق مع قسم العمليات."),
                new Clause(
                    "في حاله عدم تسليم الدفعات في موعدها يتم تأجيل المشروع ويؤجل لحين الانتهاء من " +
                    "الدفعة المقررة ويحدد موعد عن طريق قسم العمليات في حاله تأكيد الدفعات."),
            ]),

        new Section(
            "Accessories:", "الملحقات:",
            [
                new Clause(
                    "If there is any conflict or inconsistency between the Arabic Version and The " +
                    "English Version, The Arabic Version shall prevail."),
            ],
            [
                new Clause(
                    "في حال وجود أي اختلاف بين النسخة العربية والنسخة الإنجليزية تكون أولوية " +
                    "التطبيق لبنود العقد بالنسخة العربية."),
            ]),

        new Section(
            "Cancellation:", "الإلغاء:",
            [
                new Clause(
                    "In case the customer cancels the project for any reason, he is obligated to " +
                    "pay for the work completed in the project, and the project engineer " +
                    "determines this."),
            ],
            [
                new Clause(
                    "في حال تم إلغاء المشروع من قبل العميل يكون ملزم بسداد ما تم إنجازه من أعمال " +
                    "بالمشروع ويحدد ذلك عن طريق مهندس المشروع."),
            ]),
    ];

    /// <summary>"19th September 2026" — the form both the header badge and the
    /// quotation clause use.</summary>
    private static string LongDate(DateTime value) =>
        $"{value.Day}{DayOrdinal(value.Day)} {value:MMMM yyyy}";

    private static string DayOrdinal(int day) => day switch
    {
        11 or 12 or 13 => "th",
        _ when day % 10 == 1 => "st",
        _ when day % 10 == 2 => "nd",
        _ when day % 10 == 3 => "rd",
        _ => "th",
    };
}
