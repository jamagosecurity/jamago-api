using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Jama.Application.Mail;

/// <summary>
/// Cleans the formatted message an administrator writes in the mail editor before it is sent.
///
/// Nothing from the input is passed through as-is: the text is split into tags and text, every piece of text is
/// decoded and re-encoded, and only a short list of formatting tags is rebuilt — by us, without any of the original
/// attributes, except a link's address, which must be http(s) or mailto. Scripts, styles, images, frames, event
/// handlers and anything else simply do not survive.
/// </summary>
public static partial class MailHtml
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "b", "strong", "i", "em", "u", "br", "p", "div", "ul", "ol", "li", "a", "blockquote",
    };

    private static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase) { "br" };

    [GeneratedRegex(@"<(script|style|head|title|iframe|object|embed|template|noscript|svg|math)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DangerousBlocks();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"<(/?)([a-zA-Z][a-zA-Z0-9]*)([^>]*)>", RegexOptions.Singleline)]
    private static partial Regex Tag();

    [GeneratedRegex(@"href\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex Href();

    public static string Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var input = Comments().Replace(DangerousBlocks().Replace(html, string.Empty), string.Empty);

        var output = new StringBuilder(input.Length);
        var open = new Stack<string>();
        var position = 0;
        foreach (Match m in Tag().Matches(input))
        {
            AppendText(output, input[position..m.Index]);
            position = m.Index + m.Length;

            var closing = m.Groups[1].Value == "/";
            var name = m.Groups[2].Value.ToLowerInvariant();
            if (name == "strong") name = "b";
            if (name == "em") name = "i";
            if (!Allowed.Contains(name)) continue;

            if (Void.Contains(name)) { output.Append("<br>"); continue; }

            if (closing)
            {
                // Close only what is open, so the output can never break out of the message.
                if (!open.Contains(name)) continue;
                while (open.Count > 0)
                {
                    var top = open.Pop();
                    output.Append("</").Append(top).Append('>');
                    if (top == name) break;
                }
                continue;
            }

            if (name == "a")
            {
                var href = SafeHref(m.Groups[3].Value);
                if (href is null) continue;
                output.Append("<a href=\"").Append(WebUtility.HtmlEncode(href)).Append("\" style=\"color:#1f6fb8\">");
            }
            else
            {
                output.Append('<').Append(name).Append('>');
            }
            open.Push(name);
        }
        AppendText(output, input[position..]);
        while (open.Count > 0) output.Append("</").Append(open.Pop()).Append('>');
        return output.ToString();
    }

    private static void AppendText(StringBuilder output, string raw)
    {
        if (raw.Length == 0) return;
        // A stray "<" that is not a tag is text: decode what the browser encoded, then encode it all again.
        output.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(raw)));
    }

    private static string? SafeHref(string attributes)
    {
        var m = Href().Match(attributes);
        if (!m.Success) return null;
        var value = WebUtility.HtmlDecode(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value).Trim();
        return value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            ? value : null;
    }
}
