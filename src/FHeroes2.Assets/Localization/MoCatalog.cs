using System.Buffers.Binary;
using System.Text;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Localization;

/// <summary>
/// A compiled gettext catalog (<c>.mo</c>), the format the reference engine uses for
/// <c>&lt;data&gt;/files/lang/&lt;language&gt;.mo</c>.
/// </summary>
/// <remarks>
/// <para>
/// The layout mirrors <c>Translation::MOFile</c> (<c>src/engine/translations.cpp</c>): the GNU MO
/// header (magic <c>0x950412de</c> little-endian or <c>0xde120495</c> big-endian, major version 0,
/// then the original and translated string tables), plural forms stored NUL separated in a single
/// <c>msgstr</c>, and empty original strings skipped.
/// </para>
/// <para>
/// Two deliberate differences from the engine:
/// </para>
/// <list type="bullet">
///   <item><description>
///   entries are keyed by <c>msgid</c> instead of its CRC32. The hash is an engine implementation
///   detail of the reader, not part of the file format, and a dictionary cannot silently collide.
///   </description></item>
///   <item><description>
///   only UTF-8 (and pure ASCII) catalogs are decoded. The engine renders raw code page bytes
///   (CP1251, CP1258, ...) because its fonts carry those 8 bit glyphs; this port renders Unicode,
///   so a non UTF-8 catalog would need a code page decoder that does not exist yet.
///   <see cref="Parse"/> throws <see cref="NotSupportedException"/> instead of producing mojibake.
///   </description></item>
/// </list>
/// </remarks>
public sealed class MoCatalog
{
    /// <summary>Magic number of a little-endian catalog.</summary>
    public const uint LittleEndianMagic = 0x950412DE;

    /// <summary>Magic number of a big-endian catalog, as read little-endian.</summary>
    public const uint BigEndianMagic = 0xDE120495;

    /// <summary>
    /// Separator between the lookup context and the displayed text, for example <c>army|Few</c>.
    /// The reference engine keeps the context in the <c>msgid</c> and strips it before rendering.
    /// </summary>
    public const char ContextSeparator = '|';

    private const string DefaultCharset = "UTF-8";

    private readonly Dictionary<string, string[]> _entries;

    private MoCatalog(Dictionary<string, string[]> entries, string language, string charset)
    {
        _entries = entries;
        Language = language;
        Charset = charset;
    }

    /// <summary>Value of the <c>Language:</c> header entry, or an empty string when it is absent.</summary>
    public string Language { get; }

    /// <summary>Character set declared by the <c>Content-Type:</c> header entry.</summary>
    public string Charset { get; }

    /// <summary>Number of translated strings, plural forms counted once.</summary>
    public int Count => _entries.Count;

    /// <summary>Loads and parses a catalog from disk.</summary>
    /// <exception cref="BinaryFormatException">The file is not a valid catalog.</exception>
    /// <exception cref="NotSupportedException">The catalog declares a non UTF-8 character set.</exception>
    public static MoCatalog Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Parse(File.ReadAllBytes(path));
    }

    /// <summary>Parses an in-memory catalog.</summary>
    /// <exception cref="BinaryFormatException">The data is not a valid catalog.</exception>
    /// <exception cref="NotSupportedException">The catalog declares a non UTF-8 character set.</exception>
    public static MoCatalog Parse(ReadOnlySpan<byte> data)
    {
        const int HeaderSize = 28;

        if (data.Length < HeaderSize)
        {
            throw new BinaryFormatException(
                $"A MO catalog needs at least {HeaderSize} bytes but only {data.Length} were provided.");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        bool bigEndian = magic switch
        {
            LittleEndianMagic => false,
            BigEndianMagic => true,
            _ => throw new BinaryFormatException($"Incorrect MO magic number 0x{magic:X8}."),
        };

        ushort majorVersion = ReadUInt16(data, 4, bigEndian);
        if (majorVersion != 0)
        {
            throw new BinaryFormatException($"Incorrect MO major version 0x{majorVersion:X4}.");
        }

        uint count = ReadUInt32(data, 8, bigEndian);
        uint originalTable = ReadUInt32(data, 12, bigEndian);
        uint translationTable = ReadUInt32(data, 16, bigEndian);

        if (count == 0)
        {
            throw new BinaryFormatException("The MO catalog does not contain any translated strings.");
        }

        // Each table entry is 8 bytes (length plus offset); reject tables that cannot fit.
        if (!FitsInData(originalTable, (ulong)count * 8, data.Length)
            || !FitsInData(translationTable, (ulong)count * 8, data.Length))
        {
            throw new BinaryFormatException(
                $"The MO string tables ({count} entries) do not fit into the {data.Length} byte file.");
        }

        (uint Length, uint Offset)[] originals = ReadTable(data, originalTable, (int)count, bigEndian);
        (uint Length, uint Offset)[] translations = ReadTable(data, translationTable, (int)count, bigEndian);

        // The header entry (empty msgid) declares the character set and language of the catalog.
        string charset = DefaultCharset;
        string language = string.Empty;
        for (int index = 0; index < originals.Length; ++index)
        {
            if (originals[index].Length != 0)
            {
                continue;
            }

            // Header entries are plain ASCII, so decoding them as UTF-8 is safe at this point.
            string header = Encoding.UTF8.GetString(GetSlice(data, translations[index], "header"));
            charset = ReadHeaderValue(header, "Content-Type", "charset=") ?? DefaultCharset;
            language = ReadHeaderValue(header, "Language") ?? string.Empty;
            break;
        }

        Encoding encoding = CreateEncoding(charset);

        Dictionary<string, string[]> entries = new(originals.Length, StringComparer.Ordinal);
        for (int index = 0; index < originals.Length; ++index)
        {
            // The engine skips empty original strings and empty translations (translations.cpp).
            if (originals[index].Length == 0 || translations[index].Length == 0)
            {
                continue;
            }

            string id = Decode(encoding, GetSlice(data, originals[index], "msgid"), "msgid");
            string text = Decode(encoding, GetSlice(data, translations[index], "msgstr"), "msgstr");

            if (!entries.TryAdd(id, text.Split('\0')))
            {
                throw new BinaryFormatException($"The MO catalog contains the duplicate msgid \"{id}\".");
            }
        }

        if (entries.Count == 0)
        {
            throw new BinaryFormatException("The MO catalog does not contain any translated strings.");
        }

        return new MoCatalog(entries, language, charset.Trim());
    }

    /// <summary>
    /// Finds the catalog of <paramref name="culture"/>. The reference engine keeps the catalogs in
    /// <c>files/lang</c> next to the executable while the game data may live in a <c>Data</c> sub
    /// folder, so both the directory and its parent are probed.
    /// </summary>
    /// <returns>The full path of the catalog, or <see langword="null"/> when it does not exist.</returns>
    public static string? FindCatalog(string? dataDirectory, string culture)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory) || string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        string fileName = culture + ".mo";
        List<string> candidates = new()
        {
            Path.Combine(dataDirectory, "files", "lang", fileName),
            Path.Combine(dataDirectory, "lang", fileName),
        };

        string? parent = Path.GetDirectoryName(Path.GetFullPath(dataDirectory));
        if (parent is not null)
        {
            candidates.Add(Path.Combine(parent, "files", "lang", fileName));
            candidates.Add(Path.Combine(parent, "lang", fileName));
        }

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    /// <summary>Checks whether the catalog translates <paramref name="messageId"/>.</summary>
    public bool Contains(string messageId) => _entries.ContainsKey(messageId);

    /// <summary>
    /// Looks up <paramref name="messageId"/> and returns its translation, or the message itself
    /// (with the <c>context|</c> prefix removed) when the catalog has no entry for it. This is the
    /// fallback of <c>Translation::gettext()</c>.
    /// </summary>
    public string Translate(string messageId)
    {
        ArgumentNullException.ThrowIfNull(messageId);

        return _entries.TryGetValue(messageId, out string[]? forms) && forms[0].Length > 0
            ? forms[0]
            : StripContext(messageId);
    }

    /// <summary>
    /// Looks up one plural form of <paramref name="messageId"/>. Which index belongs to which count
    /// is locale specific (the engine hardcodes that switch in <c>Translation::ngettext</c>);
    /// Simplified Chinese declares a single form, so index 0 is the only one in use.
    /// </summary>
    public string Translate(string messageId, int pluralIndex)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentOutOfRangeException.ThrowIfNegative(pluralIndex);

        if (!_entries.TryGetValue(messageId, out string[]? forms))
        {
            return StripContext(messageId);
        }

        return pluralIndex < forms.Length && forms[pluralIndex].Length > 0
            ? forms[pluralIndex]
            : StripContext(messageId);
    }

    /// <summary>Removes the <c>context|</c> prefix, mirroring <c>stripContext()</c>.</summary>
    public static string StripContext(string messageId)
    {
        ArgumentNullException.ThrowIfNull(messageId);

        int separator = messageId.IndexOf(ContextSeparator, StringComparison.Ordinal);

        return separator < 0 ? messageId : messageId[(separator + 1)..];
    }

    private static string? ReadHeaderValue(string header, string field, string? valuePrefix = null)
    {
        string fieldWithColon = field + ":";

        foreach (string line in header.Split('\n'))
        {
            if (!line.StartsWith(fieldWithColon, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (valuePrefix is null)
            {
                return line[fieldWithColon.Length..].Trim();
            }

            int start = line.IndexOf(valuePrefix, StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
            {
                return line[(start + valuePrefix.Length)..].Trim();
            }
        }

        return null;
    }

    private static Encoding CreateEncoding(string charset)
    {
        string normalized = charset.Trim().ToUpperInvariant();

        return normalized switch
        {
            "" or "CHARSET" or "UTF-8" or "UTF8" or "ASCII" or "US-ASCII" => new UTF8Encoding(false, true),
            _ => throw new NotSupportedException(
                $"The MO catalog declares the character set '{charset}', but only UTF-8 catalogs are supported."),
        };
    }

    private static string Decode(Encoding encoding, ReadOnlySpan<byte> bytes, string field)
    {
        try
        {
            return encoding.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new BinaryFormatException($"{field} contains bytes that are not valid {encoding.WebName}.", exception);
        }
    }

    private static (uint Length, uint Offset)[] ReadTable(ReadOnlySpan<byte> data, uint table, int count, bool bigEndian)
    {
        (uint Length, uint Offset)[] entries = new (uint, uint)[count];

        for (int index = 0; index < count; ++index)
        {
            uint position = table + ((uint)index * 8);
            entries[index] = (ReadUInt32(data, position, bigEndian), ReadUInt32(data, position + 4, bigEndian));
        }

        return entries;
    }

    private static ReadOnlySpan<byte> GetSlice(ReadOnlySpan<byte> data, (uint Length, uint Offset) entry, string field)
    {
        if (!FitsInData(entry.Offset, entry.Length, data.Length))
        {
            throw new BinaryFormatException(
                $"The MO {field} at offset {entry.Offset} with {entry.Length} bytes exceeds the {data.Length} byte file.");
        }

        return data.Slice((int)entry.Offset, (int)entry.Length);
    }

    private static bool FitsInData(uint offset, ulong length, int dataLength)
        => (ulong)offset + length <= (ulong)dataLength;

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool bigEndian)
        => bigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, uint offset, bool bigEndian)
        => bigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(data[(int)offset..])
            : BinaryPrimitives.ReadUInt32LittleEndian(data[(int)offset..]);
}



