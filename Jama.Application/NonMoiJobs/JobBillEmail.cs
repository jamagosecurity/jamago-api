using System.Globalization;
using System.Net;
using System.Text;
using Jama.Application.Common;
using Jama.Application.Maintenance;
using Jama.Domain.Entities;

namespace Jama.Application.NonMoiJobs;

/// <summary>
/// The emails around a job's bill: the bill itself (with Approve / Reject / Comment buttons), and the short notes sent
/// when the client answers. Each comes as HTML and as plain text, because some mail apps show only one.
/// House style: black text, one blue for the header, buttons and amount.
/// </summary>
internal static class JobBillEmail
{
    // A deeper navy and a warm stone, for an unhurried, premium feel; black for all the reading text.
    private const string Navy = "#14305F";
    private const string Stone = "#AFAEA2";
    private const string Page = "#EFECE6";
    private const string Warm = "#F7F5F0";
    private const string Hair = "#E3E0D8";
    private const string Muted = "#6B7280";
    private const string Serif = "Georgia,'Times New Roman',serif";
    private const string Sans = "'Segoe UI',Helvetica,Arial,sans-serif";
    // Used by the short notes below.
    private const string Blue = Navy;
    private const string Soft = Warm;
    private const string Line = Hair;

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
    private static string H(string? value) => WebHtml(value);
    private static string WebHtml(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    // Brand colours from the logo, on the house navy.
    private const string Sky = "#2D96D5";
    private const string Amber = "#F49A3A";
    private const string Ink = "#0B1730";

    /// <summary>The company logo, carried inside the message by the mail sender (see MailSender.LogoContentId).</summary>
    private const string LogoSrc = "cid:jamago-logo";

    private static string Doha(DateTime utc, string format) =>
        utc.AddHours(3).ToString(format, CultureInfo.InvariantCulture);

    /// <summary>The white letterhead every email opens with: the logo, and a thin brand rule underneath.</summary>
    private static string Letterhead(string right) => $@"
<tr><td class=""px"" style=""padding:34px 44px 26px;background:#ffffff"">
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0""><tr>
<td valign=""middle""><img class=""lg"" src=""{LogoSrc}"" width=""200"" height=""54"" alt=""Jama Go Security Equipment"" style=""display:block;width:200px;height:auto;border:0""></td>
<td valign=""middle"" align=""right"">{right}</td></tr></table></td></tr>
<tr><td style=""font-size:0;line-height:0;height:4px""><table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0""><tr>
<td style=""background:{Navy};height:4px;font-size:0"" width=""72%"">&nbsp;</td><td style=""background:{Sky};height:4px;font-size:0"" width=""18%"">&nbsp;</td><td style=""background:{Amber};height:4px;font-size:0"" width=""10%"">&nbsp;</td></tr></table></td></tr>";

    private static string Footer() => $@"
<tr><td class=""px"" style=""background:{Ink};padding:28px 44px;font-family:{Sans};font-size:12px;line-height:1.9;color:#C9D2E3;text-align:center"">
<div style=""font-family:{Serif};font-size:14px;letter-spacing:3px;color:#ffffff"">JAMA GO SECURITY EQUIPMENT</div>
<span>+974 4001 3599 &nbsp;·&nbsp; +974 3064 4006 &nbsp;·&nbsp; <a href=""mailto:info@jamago.com"" style=""color:#ffffff;text-decoration:none"">info@jamago.com</a></span><br>
<span style=""color:#8E9BB5"">Showroom · Al Ain Complex, Salwa Road, Doha, Qatar</span></td></tr>";

    /// <summary>The bill sent to the client, laid out as a proper invoice. <paramref name="pageUrl"/> is the client's own page for it.</summary>
    internal static (string Subject, string Text, string Html) Bill(NonMoiJob job, string pageUrl, Jama.Application.Options.MailAccountSettings? account, DateTime issuedUtc,
        (string Name, string Title, string Mobile)? card = null)
    {
        var revised = job.BillRevision > 1;
        var subject = revised
            ? $"Updated invoice {job.JobNumber} (revision {job.BillRevision}) — Jama Go Security Equipment"
            : $"Invoice {job.JobNumber} — Jama Go Security Equipment";
        var intro = revised
            ? $"We have updated your invoice for job {job.JobNumber} after your feedback. Please review the revised amount below."
            : $"Thank you for choosing Jama Go Security Equipment. Our technician has completed the work at your site — your invoice is below.";
        var lines = job.Lines.OrderBy(l => l.SortOrder).ToList();

        // ---------- plain text
        var t = new StringBuilder();
        t.Append($"INVOICE {job.JobNumber}{(revised ? $" (revision {job.BillRevision})" : "")}\nDate: {Doha(issuedUtc, "d MMM yyyy")}\n\n");
        t.Append($"Dear {job.CustomerName},\n\n{intro}\n\n");
        foreach (var l in lines)
            t.Append($"- {l.IssueType} x{l.Quantity} @ {Money(l.UnitFee)}: QAR {Money(l.LineTotal)}\n");
        t.Append($"\nSubtotal: QAR {Money(job.WorkTotal)}\n");
        if (job.Discount > 0) t.Append($"Discount: QAR {Money(job.Discount)}\n");
        t.Append($"Amount due: QAR {Money(job.AmountPayable)}\n\n");
        t.Append($"Review and approve, reject or comment on your invoice here:\n{pageUrl}\n\n");
        t.Append("Your invoice is also attached as a PDF.\n\n");
        t.Append("Payment details:\n" + CompanyPaymentDetails.AsText() + "\n");
        if (account is not null) t.Append($"\n-- \n{Jama.Application.Mail.EmailSignature.Text(account, card ?? Jama.Application.Mail.EmailSignature.Card(account))}\n");

        // ---------- HTML
        const string th = "padding:12px 14px;font-size:10px;letter-spacing:2px;text-transform:uppercase;font-weight:700;color:#ffffff";
        var rows = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var bg = i % 2 == 1 ? Warm : "#ffffff";
            rows.Append($@"<tr style=""background:{bg}"">
<td class=""hide-sm"" valign=""top"" style=""padding:16px 14px;border-bottom:1px solid {Hair};font-family:{Sans};font-size:13px;color:{Muted}"">{i + 1:00}</td>
<td valign=""top"" style=""padding:16px 14px;border-bottom:1px solid {Hair};font-family:{Sans};font-size:15px;color:{Ink}""><strong style=""font-weight:600"">{H(l.IssueType)}</strong><br><span style=""font-size:12px;color:{Muted}"">{H(MaintenanceCategories.Label(l.Category))}</span></td>
<td valign=""top"" align=""center"" style=""padding:16px 8px;border-bottom:1px solid {Hair};font-family:{Sans};font-size:15px;color:{Ink}"">{l.Quantity}</td>
<td class=""hide-sm"" valign=""top"" align=""right"" style=""padding:16px 14px;border-bottom:1px solid {Hair};font-family:{Sans};font-size:15px;color:{Ink};white-space:nowrap"">{Money(l.UnitFee)}</td>
<td valign=""top"" align=""right"" style=""padding:16px 14px;border-bottom:1px solid {Hair};font-family:{Sans};font-size:15px;color:{Ink};font-weight:600;white-space:nowrap"">{Money(l.LineTotal)}</td></tr>");
        }

        string SumRow(string label, string value) =>
            $@"<tr><td style=""padding:8px 0;font-family:{Sans};font-size:14px;color:{Muted}"">{label}</td><td align=""right"" style=""padding:8px 0;font-family:{Sans};font-size:15px;color:{Ink};white-space:nowrap"">{value}</td></tr>";

        string Fact(string label, string value) =>
            $@"<div style=""font-family:{Sans};font-size:10px;letter-spacing:2px;text-transform:uppercase;color:{Muted};font-weight:700"">{label}</div><div style=""margin:5px 0 16px;font-family:{Sans};font-size:14px;line-height:1.5;color:{Ink}"">{value}</div>";

        var billTo = $"<strong>{H(job.CustomerName)}</strong><br>{H(job.CustomerNumber)}" +
                     (string.IsNullOrWhiteSpace(job.CustomerEmail) ? "" : $"<br>{H(job.CustomerEmail)}");
        var service = Fact("Site", H(job.Location)) +
                      (job.CompletedAt is { } done ? Fact("Work completed", Doha(done, "d MMM yyyy")) : "");

        var sig = account is null ? string.Empty : Jama.Application.Mail.EmailSignature.Html(account, card ?? Jama.Application.Mail.EmailSignature.Card(account));

        var right = $@"<div class=""inv"" style=""font-family:{Serif};font-size:30px;letter-spacing:6px;color:{Navy}"">INVOICE</div>
<div style=""margin-top:6px;font-family:{Sans};font-size:12px;color:{Muted};letter-spacing:1px"">No. <strong style=""color:{Ink}"">{H(job.JobNumber)}</strong>{(revised ? $" &nbsp;·&nbsp; Rev. {job.BillRevision}" : "")}</div>
<div style=""margin-top:2px;font-family:{Sans};font-size:12px;color:{Muted};letter-spacing:1px"">{Doha(issuedUtc, "d MMMM yyyy")}</div>";

        var html = $@"<!doctype html><html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><style>@media only screen and (max-width:540px){{.wrap{{padding:12px 0!important}}.px{{padding-left:16px!important;padding-right:16px!important}}.lg{{width:140px!important}}.inv{{font-size:21px!important;letter-spacing:3px!important}}.stack{{display:block!important;width:100%!important;border-left:0!important;padding:14px 0 4px!important}}.hide-sm{{display:none!important}}.sum{{width:100%!important}}}}</style><title>Invoice {H(job.JobNumber)}</title></head>
<body style=""margin:0;padding:0;background:{Page}"">
<div style=""display:none;max-height:0;overflow:hidden"">Invoice {H(job.JobNumber)} · Amount due QAR {Money(job.AmountPayable)}</div>
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:{Page}""><tr><td class=""wrap"" align=""center"" style=""padding:34px 12px"">
<table role=""presentation"" width=""660"" cellspacing=""0"" cellpadding=""0"" style=""width:100%;max-width:660px;background:#ffffff;border-radius:6px;overflow:hidden;border:1px solid {Hair}"">
{Letterhead(right)}
<tr><td class=""px"" style=""padding:38px 44px 10px"">
<p style=""margin:0 0 10px;font-family:{Serif};font-size:22px;color:{Navy}"">Dear {H(job.CustomerName)},</p>
<p style=""margin:0 0 30px;font-family:{Sans};font-size:15px;line-height:1.75;color:#333A48"">{H(intro)}</p>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""border-top:1px solid {Hair};border-bottom:1px solid {Hair}""><tr>
<td class=""stack"" valign=""top"" width=""50%"" style=""padding:22px 16px 6px 0"">{Fact("Billed to", billTo)}</td>
<td class=""stack"" valign=""top"" width=""50%"" style=""padding:22px 0 6px 16px;border-left:1px solid {Hair}"">{service}</td></tr></table>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:30px;border-collapse:collapse"">
<tr style=""background:{Navy}""><td class=""hide-sm"" style=""{th};font-family:{Sans};border-radius:4px 0 0 0"">#</td><td style=""{th};font-family:{Sans}"">Description</td><td align=""center"" style=""{th};font-family:{Sans}"">Qty</td><td class=""hide-sm"" align=""right"" style=""{th};font-family:{Sans}"">Unit</td><td align=""right"" style=""{th};font-family:{Sans};border-radius:0 4px 0 0"">Amount</td></tr>
{rows}
</table>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:18px""><tr><td class=""hide-sm"" width=""40%"">&nbsp;</td><td class=""sum"" width=""60%"">
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"">
{SumRow("Subtotal", "QAR " + Money(job.WorkTotal))}
{(job.Discount > 0 ? SumRow("Discount", "&minus; QAR " + Money(job.Discount)) : "")}
</table>
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:10px;background:{Navy};border-radius:6px""><tr>
<td style=""padding:18px 20px;font-family:{Sans};font-size:10px;letter-spacing:2.5px;color:#C9D2E3;text-transform:uppercase;font-weight:700"">Amount due</td>
<td align=""right"" style=""padding:18px 20px;font-family:{Serif};font-size:26px;color:#ffffff;white-space:nowrap"">QAR&nbsp;{Money(job.AmountPayable)}</td></tr></table>
</td></tr></table>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:38px;background:{Warm};border-radius:6px""><tr><td align=""center"" style=""padding:30px 24px"">
<p style=""margin:0 0 6px;font-family:{Serif};font-size:20px;color:{Navy}"">Your decision, in one tap</p>
<p style=""margin:0 0 20px;font-family:{Sans};font-size:14px;line-height:1.6;color:#333A48"">Approve the invoice, or tell us what you would like changed.</p>
<a href=""{H(pageUrl)}"" style=""display:inline-block;padding:16px 40px;background:{Navy};color:#ffffff;font-family:{Sans};font-size:13px;font-weight:700;letter-spacing:2px;text-transform:uppercase;text-decoration:none;border-radius:4px"">Review &amp; respond</a>
<p style=""margin:14px 0 0;font-family:{Sans};font-size:12px;color:{Muted}"">The invoice is also attached as a PDF.</p>
</td></tr></table>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:24px;border:1px solid {Hair};border-radius:6px""><tr><td style=""padding:20px 24px"">
<div style=""font-family:{Sans};font-size:10px;letter-spacing:2px;color:{Navy};text-transform:uppercase;font-weight:700"">Payment details</div>
<div style=""margin-top:10px;font-family:{Sans};font-size:14px;line-height:1.8;color:{Ink}""><strong>{H(CompanyPaymentDetails.AccountName)}</strong><br>CR NO: {H(CompanyPaymentDetails.CrNumber)}<br>IBAN: <strong>{H(CompanyPaymentDetails.Iban)}</strong></div>
</td></tr></table>
{sig}
<p style=""margin:30px 0 30px;font-family:{Sans};font-size:12px;color:{Muted};text-align:center"">Thank you for your business.</p>
</td></tr>
{Footer()}
</table></td></tr></table></body></html>";

        return (subject, t.ToString(), html);
    }

    /// <summary>A short note for staff or the client, as text and as a plain HTML card in the same style.</summary>
    internal static (string Text, string Html) Note(string heading, IReadOnlyList<string> lines, string? payment = null)
    {
        var text = new StringBuilder(heading + "\n\n");
        foreach (var line in lines) text.Append(line + "\n");
        if (payment is not null) text.Append("\nPayment details:\n" + payment + "\n");

        var body = new StringBuilder();
        foreach (var line in lines)
            body.Append($@"<p style=""margin:0 0 14px;font-family:{Sans};font-size:15px;line-height:1.75;color:#333A48"">{H(line)}</p>");
        if (payment is not null)
            body.Append($@"<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:22px;border:1px solid {Hair};border-radius:6px""><tr><td style=""padding:20px 24px""><div style=""font-family:{Sans};font-size:10px;letter-spacing:2px;color:{Navy};text-transform:uppercase;font-weight:700"">Payment details</div><div style=""margin-top:10px;font-family:{Sans};font-size:14px;line-height:1.8;color:{Ink}"">{H(payment).Replace("\n", "<br>")}</div></td></tr></table>");

        var html = $@"<!doctype html><html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><style>@media only screen and (max-width:540px){{.wrap{{padding:12px 0!important}}.px{{padding-left:16px!important;padding-right:16px!important}}.lg{{width:140px!important}}.inv{{font-size:21px!important;letter-spacing:3px!important}}.stack{{display:block!important;width:100%!important;border-left:0!important;padding:14px 0 4px!important}}.hide-sm{{display:none!important}}.sum{{width:100%!important}}}}</style></head>
<body style=""margin:0;padding:0;background:{Page}""><table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0""><tr><td class=""wrap"" align=""center"" style=""padding:34px 14px"">
<table role=""presentation"" width=""660"" cellspacing=""0"" cellpadding=""0"" style=""width:100%;max-width:660px;background:#fff;border-radius:6px;overflow:hidden;border:1px solid {Hair}"">
{Letterhead(string.Empty)}
<tr><td class=""px"" style=""padding:38px 44px 36px""><h2 style=""margin:0 0 20px;font-family:{Serif};font-size:24px;font-weight:400;color:{Navy};line-height:1.3"">{H(heading)}</h2>{body}</td></tr>{Footer()}</table></td></tr></table></body></html>";
        return (text.ToString(), html);
    }
}
