namespace Jama.Application.Common;

/// <summary>
/// Checks that an upload's leading bytes match the file type its name claims.
///
/// The extension allow-list decides what may be uploaded, but a name is just a
/// string: anything at all can be called "report.pdf". This reads the actual
/// content so a mislabelled — or deliberately disguised — file is refused before
/// it is written to disk beside genuine client documents.
///
/// Deliberately not a virus scanner and not a guarantee: a real PDF can still
/// carry a malicious payload. It closes the cheap hole, which is uploading
/// something that was never the declared type at all.
/// </summary>
public static class FileSignatures
{
    /// <summary>Bytes needed to recognise the longest signature below.</summary>
    public const int HeaderLength = 16;

    private sealed record Signature(byte[] Magic, int Offset = 0);

    // Several extensions share a container: .docx and .xlsx are ZIP archives, and
    // .doc/.xls are the older OLE2 compound format, so both map to those markers
    // rather than to anything Word- or Excel-specific.
    private static readonly Dictionary<string, Signature[]> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = [new("%PDF"u8.ToArray())],

        // ZIP: normal archive, plus the empty and spanned variants Office can emit.
        [".zip"] = [new([0x50, 0x4B, 0x03, 0x04]), new([0x50, 0x4B, 0x05, 0x06]), new([0x50, 0x4B, 0x07, 0x08])],
        [".docx"] = [new([0x50, 0x4B, 0x03, 0x04]), new([0x50, 0x4B, 0x05, 0x06]), new([0x50, 0x4B, 0x07, 0x08])],
        [".xlsx"] = [new([0x50, 0x4B, 0x03, 0x04]), new([0x50, 0x4B, 0x05, 0x06]), new([0x50, 0x4B, 0x07, 0x08])],

        // OLE2 compound document, used by Word and Excel before the XML formats.
        [".doc"] = [new([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1])],
        [".xls"] = [new([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1])],

        [".png"] = [new([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])],
        [".jpg"] = [new([0xFF, 0xD8, 0xFF])],
        [".jpeg"] = [new([0xFF, 0xD8, 0xFF])],

        // RIFF container with a WEBP form type four bytes later.
        [".webp"] = [new("RIFF"u8.ToArray()), new("WEBP"u8.ToArray(), Offset: 8)],

        // ISO base media: the brand sits after the four-byte box length.
        [".heic"] = [new("ftyp"u8.ToArray(), Offset: 4)],
    };

    /// <summary>
    /// True when <paramref name="header"/> is consistent with <paramref name="extension"/>.
    ///
    /// Extensions with no reliable signature — .csv is plain text — are accepted:
    /// there is nothing to check, and rejecting them would block a legitimate
    /// upload on the basis of a test that cannot be run.
    /// </summary>
    public static bool Matches(string extension, ReadOnlySpan<byte> header)
    {
        if (!Known.TryGetValue(extension, out var signatures))
        {
            return true;
        }

        // Written as loops rather than LINQ: header is a span, which cannot be
        // captured by a lambda.
        //
        // .webp needs every marker present — RIFF at the start and WEBP at byte
        // eight. Every other entry lists alternative forms of the same type, so
        // one match is enough.
        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var signature in signatures)
            {
                if (!StartsWith(header, signature))
                {
                    return false;
                }
            }

            return true;
        }

        foreach (var signature in signatures)
        {
            if (StartsWith(header, signature))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWith(ReadOnlySpan<byte> header, Signature signature)
    {
        if (header.Length < signature.Offset + signature.Magic.Length)
        {
            return false;
        }

        return header.Slice(signature.Offset, signature.Magic.Length).SequenceEqual(signature.Magic);
    }
}
