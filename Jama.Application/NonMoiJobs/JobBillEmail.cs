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

    /// <summary>The bill sent to the client. <paramref name="pageUrl"/> is the client's own page for this bill.</summary>
    internal static (string Subject, string Text, string Html) Bill(NonMoiJob job, string pageUrl, string signature)
    {
        var revised = job.BillRevision > 1;
        var subject = revised
            ? $"Updated bill {job.JobNumber} (revision {job.BillRevision}) — Jama Go Security Equipment"
            : $"Your bill {job.JobNumber} — Jama Go Security Equipment";
        var intro = revised
            ? $"We have updated your bill for job {job.JobNumber} (revision {job.BillRevision}) after your feedback."
            : $"Thank you for choosing Jama Go Security Equipment. Our technician has completed the work for job {job.JobNumber}.";

        // ---------- plain text
        var t = new StringBuilder();
        t.Append($"Dear {job.CustomerName},\n\n{intro}\n\n");
        foreach (var l in job.Lines.OrderBy(l => l.SortOrder))
            t.Append($"- {l.IssueType} x{l.Quantity}: QAR {Money(l.LineTotal)}\n");
        t.Append($"\nTotal: QAR {Money(job.WorkTotal)}\n");
        if (job.Discount > 0) t.Append($"Discount: QAR {Money(job.Discount)}\n");
        t.Append($"Amount payable: QAR {Money(job.AmountPayable)}\n\n");
        t.Append("Please look at your bill and tell us what you decide — Approve, Reject, or add a Comment:\n");
        t.Append($"Approve:  {pageUrl}?do=approve\nReject:   {pageUrl}?do=reject\nComment:  {pageUrl}?do=comment\n\n");
        t.Append("Your bill is also attached as a PDF.\n\n");
        t.Append("Payment details:\n" + CompanyPaymentDetails.AsText() + "\n");
        if (!string.IsNullOrWhiteSpace(signature)) t.Append($"\n-- \n{signature.Trim()}\n");

        // ---------- HTML — a quiet, premium layout: deep navy, warm stone, serif headings, plenty of air.
        var rows = new StringBuilder();
        foreach (var l in job.Lines.OrderBy(l => l.SortOrder))
        {
            rows.Append($@"<tr>
<td style=""padding:14px 0;border-bottom:1px solid {Hair};font-family:{Sans};font-size:15px;color:#000""><span style=""font-weight:600"">{H(l.IssueType)}</span><br><span style=""font-size:12px;color:{Muted};letter-spacing:0.5px"">{H(MaintenanceCategories.Label(l.Category))}</span></td>
<td align=""center"" style=""padding:14px 8px;border-bottom:1px solid {Hair};font-family:{Sans};font-size:15px;color:#000"">{l.Quantity}</td>
<td align=""right"" style=""padding:14px 0;border-bottom:1px solid {Hair};font-family:{Sans};font-size:15px;color:#000;white-space:nowrap"">{Money(l.LineTotal)}</td></tr>");
        }

        var discountRow = job.Discount > 0
            ? $@"<tr><td style=""padding:7px 0;font-family:{Sans};font-size:15px;color:#000"">Discount</td><td align=""right"" style=""padding:7px 0;font-family:{Sans};font-size:15px;color:#000"">&minus; {Money(job.Discount)}</td></tr>"
            : string.Empty;

        string Button(string label, string href, bool solid) =>
            $@"<a href=""{H(href)}"" style=""display:inline-block;margin:6px 8px 6px 0;padding:15px 34px;border:1.5px solid {Navy};background:{(solid ? Navy : "#ffffff")};color:{(solid ? "#ffffff" : Navy)};font-family:{Sans};font-size:13px;font-weight:700;letter-spacing:2px;text-transform:uppercase;text-decoration:none;border-radius:2px"">{label}</a>";

        var sig = string.IsNullOrWhiteSpace(signature)
            ? string.Empty
            : $@"<p style=""margin:34px 0 0;padding-top:20px;border-top:1px solid {Hair};font-family:{Serif};font-size:15px;line-height:1.7;color:#000"">{H(signature.Trim()).Replace("\n", "<br>")}</p>";

        var html = $@"<!doctype html><html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:{Page}"">
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:{Page}""><tr><td align=""center"" style=""padding:34px 14px"">
<table role=""presentation"" width=""640"" cellspacing=""0"" cellpadding=""0"" style=""width:100%;max-width:640px;background:#ffffff;border-radius:3px;overflow:hidden"">
<tr><td align=""center"" style=""background:{Navy};padding:38px 24px 30px"">
<div style=""font-family:{Serif};font-size:30px;letter-spacing:9px;color:#ffffff;font-weight:400"">JAMA&nbsp;GO</div>
<div style=""margin-top:10px;font-family:{Sans};font-size:10px;letter-spacing:5px;color:{Stone};text-transform:uppercase"">Security Equipment</div></td></tr>
<tr><td style=""background:{Stone};height:3px;line-height:3px;font-size:0"">&nbsp;</td></tr>
<tr><td style=""padding:46px 44px 40px"">
<p style=""margin:0 0 12px;font-family:{Sans};font-size:11px;letter-spacing:3px;color:{Muted};text-transform:uppercase"">{(revised ? $"Revised bill &nbsp;·&nbsp; Revision {job.BillRevision}" : "Your bill")} &nbsp;·&nbsp; {H(job.JobNumber)}</p>
<h1 style=""margin:0 0 20px;font-family:{Serif};font-size:28px;line-height:1.25;font-weight:400;color:{Navy}"">Dear {H(job.CustomerName)},</h1>
<p style=""margin:0 0 30px;font-family:{Sans};font-size:16px;line-height:1.75;color:#000"">{H(intro)}</p>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""border-collapse:collapse"">
<tr><td style=""padding:0 0 10px;border-bottom:2px solid {Navy};font-family:{Sans};font-size:11px;letter-spacing:2.5px;color:{Navy};text-transform:uppercase;font-weight:700"">Work completed</td>
<td align=""center"" style=""padding:0 8px 10px;border-bottom:2px solid {Navy};font-family:{Sans};font-size:11px;letter-spacing:2.5px;color:{Navy};text-transform:uppercase;font-weight:700"">Qty</td>
<td align=""right"" style=""padding:0 0 10px;border-bottom:2px solid {Navy};font-family:{Sans};font-size:11px;letter-spacing:2.5px;color:{Navy};text-transform:uppercase;font-weight:700"">QAR</td></tr>
{rows}
</table>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:14px""><tr><td style=""padding:7px 0;font-family:{Sans};font-size:15px;color:#000"">Total</td><td align=""right"" style=""padding:7px 0;font-family:{Sans};font-size:15px;color:#000"">{Money(job.WorkTotal)}</td></tr>{discountRow}</table>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:16px;background:{Navy};border-radius:3px""><tr>
<td style=""padding:24px 28px;font-family:{Sans};font-size:11px;letter-spacing:3px;color:{Stone};text-transform:uppercase"">Amount payable</td>
<td align=""right"" style=""padding:24px 28px;font-family:{Serif};font-size:32px;color:#ffffff;white-space:nowrap"">QAR&nbsp;{Money(job.AmountPayable)}</td></tr></table>

<p style=""margin:42px 0 6px;font-family:{Serif};font-size:21px;color:{Navy}"">We would value your decision</p>
<p style=""margin:0 0 18px;font-family:{Sans};font-size:14px;line-height:1.7;color:#000"">Please review your bill and choose one of the options below. If you would like anything changed, choose <strong>Comment</strong> and tell us.</p>
<div>{Button("Approve", pageUrl + "?do=approve", true)}{Button("Reject", pageUrl + "?do=reject", false)}{Button("Comment", pageUrl + "?do=comment", false)}</div>
<p style=""margin:10px 0 0;font-family:{Sans};font-size:12px;color:{Muted}"">Your bill is also attached to this email as a PDF.</p>

<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:36px;background:{Warm};border-left:3px solid {Navy}""><tr><td style=""padding:22px 26px"">
<div style=""font-family:{Sans};font-size:11px;letter-spacing:3px;color:{Navy};text-transform:uppercase;font-weight:700"">Payment details</div>
<div style=""margin-top:12px;font-family:{Serif};font-size:16px;line-height:1.8;color:#000""><strong>{H(CompanyPaymentDetails.AccountName)}</strong><br>CR NO: {H(CompanyPaymentDetails.CrNumber)}<br>IBAN: <strong>{H(CompanyPaymentDetails.Iban)}</strong></div>
</td></tr></table>
{sig}
</td></tr>
<tr><td style=""background:{Navy};padding:26px 44px;font-family:{Sans};font-size:12px;line-height:1.8;color:#ffffff;text-align:center"">
<span style=""letter-spacing:1px"">+974 4001 3599 &nbsp;·&nbsp; +974 3064 4006 &nbsp;·&nbsp; info@jamago.com</span><br>
<span style=""color:{Stone}"">Showroom, Unit 41, Building 349, St 340, Zone 56, Al Ain Complex, Salwa Road, Doha</span></td></tr>
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
            body.Append($@"<p style=""margin:0 0 14px;font-family:{Sans};font-size:16px;line-height:1.75;color:#000"">{H(line)}</p>");
        if (payment is not null)
            body.Append($@"<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:22px;background:{Warm};border-left:3px solid {Navy}""><tr><td style=""padding:20px 24px;font-family:{Serif};font-size:16px;line-height:1.8;color:#000""><div style=""font-family:{Sans};font-size:11px;letter-spacing:3px;color:{Navy};text-transform:uppercase;font-weight:700;margin-bottom:10px"">Payment details</div>{H(payment).Replace("\n", "<br>")}</td></tr></table>");

        var html = $@"<!doctype html><html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:{Page}""><table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0""><tr><td align=""center"" style=""padding:34px 14px"">
<table role=""presentation"" width=""580"" cellspacing=""0"" cellpadding=""0"" style=""width:100%;max-width:580px;background:#fff;border-radius:3px;overflow:hidden"">
<tr><td align=""center"" style=""background:{Navy};padding:30px 24px 24px""><div style=""font-family:{Serif};font-size:25px;letter-spacing:8px;color:#fff"">JAMA&nbsp;GO</div><div style=""margin-top:8px;font-family:{Sans};font-size:10px;letter-spacing:4px;color:{Stone};text-transform:uppercase"">Security Equipment</div></td></tr>
<tr><td style=""background:{Stone};height:3px;line-height:3px;font-size:0"">&nbsp;</td></tr>
<tr><td style=""padding:38px 40px 36px""><h2 style=""margin:0 0 20px;font-family:{Serif};font-size:24px;font-weight:400;color:{Navy};line-height:1.3"">{H(heading)}</h2>{body}</td></tr></table></td></tr></table></body></html>";
        return (text.ToString(), html);
    }
}
