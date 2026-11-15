using System.Net;
using System.Text;
using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using Microsoft.EntityFrameworkCore;

namespace Jama.Application.Mail;

/// <summary>
/// Each company mailbox's signature, in the company design: logo, "MOI-SSD Approved CCTV Contractor", the person's name
/// and title, mobile, email, website, address and social links. One HTML version for email (the logo travels inside
/// the message as <c>cid:jamago-logo</c>) and a matching plain-text version.
/// </summary>
public static class EmailSignature
{
    /// <summary>
    /// Whose details sign this mailbox: the staff member whose login email is the mailbox (their profile's name,
    /// designation and phone — so editing the profile updates the signature), otherwise the details in the mailbox
    /// settings, otherwise the department.
    /// </summary>
    public static async Task<(string Name, string Title, string Mobile)> ResolveAsync(
        IApplicationDbContext context, MailAccountSettings account, CancellationToken ct = default)
    {
        var fallback = Card(account);
        var address = account.Address.Trim().ToLower();
        var person = await context.AdminUsers.AsNoTracking()
            .Where(u => u.IsActive && u.Email.ToLower() == address)
            .Select(u => new { u.FullName, u.Designation, u.Phone })
            .FirstOrDefaultAsync(ct);
        if (person is null) return fallback;
        return (
            string.IsNullOrWhiteSpace(person.FullName) ? fallback.Name : person.FullName.Trim(),
            string.IsNullOrWhiteSpace(person.Designation) ? fallback.Title : person.Designation.Trim(),
            string.IsNullOrWhiteSpace(person.Phone) ? fallback.Mobile : person.Phone.Trim());
    }

    public const string Website = "www.jamago.qa";
    public const string WebsiteUrl = "https://www.jamago.qa";
    public const string Address = "349 Salwa Rd, Zone 56, Street 340, Doha, Qatar";
    public const string MapUrl = "https://maps.google.com/?q=349+Salwa+Rd,+Zone+56,+Street+340,+Doha,+Qatar";
    public const string CompanyMobile = "+974 3064 4006";
    private const string Facebook = "https://www.facebook.com/share/1F9pWtY94j/";
    private const string Instagram = "https://www.instagram.com/jamago.qa";
    private const string X = "https://x.com/Jamagoqa";

    private const string Navy = "#14305F";
    private const string Red = "#D21319";
    private const string Sky = "#2D96D5";
    private const string Amber = "#F49A3A";
    private const string Ink = "#0B1730";
    private const string Muted = "#6B7280";
    private const string Sans = "'Segoe UI',Helvetica,Arial,sans-serif";

    /// <summary>Who signs: the person's details, or the department's when the mailbox is shared.</summary>
    public static (string Name, string Title, string Mobile) Card(MailAccountSettings account)
    {
        var name = Clean(account.SignatureName);
        var title = Clean(account.SignatureTitle);
        var mobile = Clean(account.SignatureMobile);
        if (name.Length == 0)
        {
            (name, var defaultTitle) = account.Key.ToLowerInvariant() switch
            {
                "info" => ("Jama Go Security Equipment", "Customer Service"),
                "support" => ("Jama Go Support", "Technical Support"),
                "moi" => ("Jama Go MOI Desk", "MOI Approvals"),
                "ceo" => ("Office of the CEO", "Jama Go Security Equipment"),
                _ => (Capitalise(account.Key), string.Empty),
            };
            if (title.Length == 0) title = defaultTitle;
        }
        return (name, title, mobile.Length == 0 ? CompanyMobile : mobile);
    }

    public static string Text(MailAccountSettings account) => Text(account, Card(account));

    public static string Text(MailAccountSettings account, (string Name, string Title, string Mobile) card)
    {
        var (name, title, mobile) = card;
        var t = new StringBuilder("Best regards,\n");
        t.Append(name).Append('\n');
        if (title.Length > 0) t.Append(title).Append('\n');
        t.Append("Jama Go Security Equipment · MOI-SSD Approved CCTV Contractor\n");
        t.Append($"Mobile: {mobile}\nEmail: {account.Address}\nWebsite: {Website}\nAddress: {Address}");
        return t.ToString();
    }

    public static string Html(MailAccountSettings account) => Html(account, Card(account));

    public static string Html(MailAccountSettings account, (string Name, string Title, string Mobile) card)
    {
        var (name, title, mobile) = card;
        string H(string v) => WebUtility.HtmlEncode(v);
        var tel = "tel:" + new string(mobile.Where(c => char.IsDigit(c) || c == '+').ToArray());

        string Row(string label, string value) =>
            $@"<tr><td style=""padding:1px 10px 1px 0;font-family:{Sans};font-size:12px;color:{Muted};white-space:nowrap;vertical-align:top"">{label}</td><td style=""padding:1px 0;font-family:{Sans};font-size:12px;color:{Ink}"">{value}</td></tr>";

        string Social(string label, string url) =>
            $@"<a href=""{url}"" style=""display:inline-block;margin-right:6px;padding:3px 9px;border:1px solid #D9DBE2;border-radius:999px;font-family:{Sans};font-size:11px;font-weight:700;color:{Navy};text-decoration:none"">{label}</a>";

        // On narrow screens (phones) the logo sits on top and the details below it.
        const string css = "<style>@media only screen and (max-width:540px){.jg-sig-l,.jg-sig-r{display:block!important;width:auto!important}"
            + ".jg-sig-r{border-left:0!important;border-top:3px solid #14305F!important;margin-top:12px!important;padding:12px 0 0!important}}</style>";
        return css + $@"<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:22px;border-collapse:collapse""><tr>
<td class=""jg-sig-l"" valign=""top"" style=""padding:4px 18px 4px 0"">
<a href=""{WebsiteUrl}"" style=""text-decoration:none""><img src=""cid:jamago-logo"" width=""150"" alt=""Jama Go Security Equipment"" style=""display:block;width:150px;height:auto;border:0""></a>
<img src=""cid:jamago-moi"" width=""142"" height=""52"" alt=""MOI-SSD Approved CCTV Contractor"" style=""display:block;margin-top:10px;width:142px;height:52px;border:0"">
</td>
<td class=""jg-sig-r"" valign=""top"" style=""padding:2px 0 2px 18px;border-left:3px solid {Navy}"">
<div style=""font-family:{Sans};font-size:16px;font-weight:700;color:{Navy}"">{H(name)}</div>
{(title.Length > 0 ? $@"<div style=""margin-top:2px;font-family:{Sans};font-size:11px;font-weight:700;letter-spacing:1.5px;color:{Red};text-transform:uppercase"">{H(title)}</div>" : "")}
<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" style=""margin-top:10px;border-collapse:collapse"">
{Row("Mobile", $@"<a href=""{tel}"" style=""color:{Ink};text-decoration:none"">{H(mobile)}</a>")}
{Row("Email", $@"<a href=""mailto:{H(account.Address)}"" style=""color:{Ink};text-decoration:none"">{H(account.Address)}</a>")}
{Row("Website", $@"<a href=""{WebsiteUrl}"" style=""color:{Sky};text-decoration:none"">{Website}</a>")}
{Row("Address", $@"<a href=""{MapUrl}"" style=""color:{Ink};text-decoration:none"">{H(Address)}</a>")}
</table>
<div style=""margin-top:10px"">{Social("Facebook", Facebook)}{Social("Instagram", Instagram)}{Social("X", X)}</div>
</td></tr>
<tr><td colspan=""2"" style=""padding-top:12px""><div style=""height:3px;width:220px;background:linear-gradient(90deg,{Navy} 0 70%,{Sky} 70% 88%,{Amber} 88% 100%);background-color:{Navy}""></div></td></tr></table>";
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim();

    private static string Capitalise(string key) =>
        key.Length == 0 ? key : char.ToUpperInvariant(key[0]) + key[1..];
}
