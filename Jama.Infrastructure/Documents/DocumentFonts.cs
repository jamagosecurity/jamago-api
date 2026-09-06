using System.Reflection;
using QuestPDF.Drawing;

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
