using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Helpers;

namespace Jama.Infrastructure.Documents;

/// <summary>
/// Fonts the documents carry with them.
///
/// The Latin text takes whatever the machine offers, which is why the generators
/// can name Calibri and still print on a Linux box that has never heard of it.
/// Arabic cannot be left to that: the API runs on a stock Ubuntu server with no
/// Arabic face installed, and QuestPDF does not quietly drop a glyph it cannot
/// draw — it throws, which would take the whole quotation PDF down rather than
/// just the two bilingual lines. So the Arabic face ships inside the assembly.
/// </summary>
internal static class DocumentFonts
{
    /// <summary>The family name to set on Arabic text. Registered from the
    /// embedded file below, so it resolves on any machine.</summary>
    internal const string Arabic = "Noto Naskh Arabic";

    /// <summary>
    /// The family chain for body text: the Latin face first, the embedded Arabic
    /// face behind it. QuestPDF walks the chain per glyph, so a run of Arabic
    /// inside an otherwise Latin field is drawn from the second family without
    /// anyone having to know it was there.
    ///
    /// Naming the Arabic family on a field only works when the field is KNOWN to
    /// hold Arabic — a label, a written-out amount. It cannot work for a client
    /// name or a project title, which are free text and are exactly where Arabic
    /// turned up: those printed as empty boxes, one per letter, because Calibri
    /// has no glyph for them and nothing was behind it to ask.
    ///
    /// Applied as the DEFAULT style so it covers every field a document has,
    /// including the ones nobody has thought of yet.
    /// </summary>
    internal static readonly string[] Body = [Fonts.Calibri, Arabic];

    private static readonly bool Registered = RegisterArabic();

    /// <summary>Called before laying out a document that sets <see cref="Arabic"/>
    /// on anything. Registration happens once, on the first call.</summary>
    internal static void EnsureRegistered() => _ = Registered;

    private static bool RegisterArabic()
    {
        var loaded = false;

        foreach (var name in (string[])
                 [
                     "Jama.Infrastructure.Documents.Assets.noto-naskh-arabic-regular.ttf",
                     "Jama.Infrastructure.Documents.Assets.noto-naskh-arabic-bold.ttf",
                 ])
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream is null) continue;

            FontManager.RegisterFont(stream);
            loaded = true;
        }

        return loaded;
    }
}
