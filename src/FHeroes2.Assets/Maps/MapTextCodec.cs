using System.Text;

namespace FHeroes2.Assets.Maps;

/// <summary>
/// Decodes the text fields of an MP2 header: the map name, its description and the victory and
/// loss conditions.
/// </summary>
/// <remarks>
/// <para>
/// The header stores plain bytes without an encoding marker, so the original game printed them with
/// the code page of the data set: the maps that ship with the game are ASCII, translated maps usually
/// keep the code page of their translator and the Chinese map packs are GBK.
/// </para>
/// <para>
/// Latin-1 never fails and therefore cannot be used as a probe, so the field is tried as UTF-8 and
/// then as GBK first. GBK is only accepted when it produces at least two CJK characters: one is not
/// enough, because a CP1252 <c>Château</c> has a byte pair that also parses as a single CJK glyph.
/// Everything else stays Latin-1, which keeps the accented characters of the continental maps.
/// </para>
/// </remarks>
public static class MapTextCodec
{
    /// <summary>Code page of the Chinese data sets, <c>GBK</c>.</summary>
    public const int GbkCodePage = 936;

    /// <summary>How many CJK characters a GBK decode needs before it overrules Latin-1.</summary>
    public const int MinimumCjkCharacters = 2;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Encoding? Gbk = TryGetGbk();

    /// <summary>Whether the GBK code page is available at all.</summary>
    public static bool SupportsGbk => Gbk is not null;

    /// <summary>
    /// Decodes one NUL padded field. The terminator and the trailing padding are dropped.
    /// </summary>
    /// <param name="field">Raw bytes of the field.</param>
    /// <returns>The decoded text, never <see langword="null"/>.</returns>
    public static string Decode(ReadOnlySpan<byte> field)
    {
        int terminator = field.IndexOf((byte)0);
        if (terminator >= 0)
        {
            field = field[..terminator];
        }

        if (field.IsEmpty)
        {
            return string.Empty;
        }

        if (IsAscii(field))
        {
            return Utf8.GetString(field).TrimEnd();
        }

        if (TryDecode(field, Utf8, out string utf8) && !IsAscii(utf8))
        {
            return utf8.TrimEnd();
        }

        if (Gbk is not null
            && TryDecode(field, Gbk, out string gbk)
            && CountCjkCharacters(gbk) >= MinimumCjkCharacters)
        {
            return gbk.TrimEnd();
        }

        return Encoding.Latin1.GetString(field).TrimEnd();
    }

    /// <summary>
    /// Encodes one NUL padded field, the inverse of <see cref="Decode"/> bounded by the size the field
    /// has in the header.
    /// </summary>
    /// <remarks>
    /// A field that is cut in the middle of a multi byte character loses the character instead of
    /// storing half of it, and the last byte of the field always stays NUL, so a reader that trusts
    /// the padding cannot run past the text.
    /// </remarks>
    /// <param name="text">Text to store; NUL characters are dropped.</param>
    /// <param name="fieldLength">Size of the field; the result is always exactly this long.</param>
    /// <returns>The field, NUL padded and NUL terminated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fieldLength"/> is not positive.</exception>
    public static byte[] Encode(string text, int fieldLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fieldLength);

        byte[] field = new byte[fieldLength];
        Encoding encoding = ChooseEncoding(text);
        int offset = 0;

        foreach (Rune rune in text.EnumerateRunes())
        {
            if (rune.Value == 0)
            {
                continue;
            }

            byte[] bytes = encoding.GetBytes(rune.ToString());

            if (offset + bytes.Length >= fieldLength)
            {
                break;
            }

            bytes.CopyTo(field, offset);
            offset += bytes.Length;
        }

        return field;
    }

    /// <summary>
    /// Undoes the character stealing of the French maps, <c>fheroes2::fixFrenchCharactersForMP2Map()</c>
    /// (<c>ui_font.cpp:7190</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The French editor had no accented letters in its text fields and borrowed ASCII characters for them, so a
    /// French map stores <c>î</c> as a horizontal tab and <c>ô</c> as a number sign. The table below is the exact
    /// one of the reference; every other character, the accented letters the reference writes as a byte in
    /// <c>0xE0…0xFB</c> included, stays as it is.
    /// </para>
    /// <para>
    /// The reference edits the bytes in place and its callers decide when the fix applies — the original does it
    /// only while the interface runs in French (<c>world_loadmap.cpp</c>). The port takes and returns a string, so a
    /// caller has to keep the same gate: applying this to English or Chinese text would turn its punctuation into
    /// accents.
    /// </para>
    /// </remarks>
    /// <param name="text">Text read from a legacy French map.</param>
    /// <returns>The fixed text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string FixFrenchCharacters(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return text;
        }

        char[]? fixedText = null;

        for (int i = 0; i < text.Length; ++i)
        {
            char replacement = text[i] switch
            {
                '\t' => 'î',      // 9, the horizontal tab, used for the lowercase i with circumflex
                '#' => 'ô',       // 35, the number sign, used for the lowercase o with circumflex
                '$' => 'û',       // 36, the dollar sign, used for the lowercase u with circumflex
                '&' => 'ù',       // 38, the ampersand, used for the lowercase u with grave accent
                '*' => 'â',       // 42, the asterisk, used for the lowercase a with circumflex
                '<' => 'ï',       // 60, the less than sign, used for the lowercase i with diaeresis
                '>' => 'î',       // 62, the greater than sign, used for the lowercase i with circumflex
                '@' => 'à',       // 64, the at sign, used for the lowercase a with grave accent
                '\\' => '.',      // 92, the backslash, some maps use it as a full stop
                '^' => 'ç',       // 94, the caret, used for the lowercase c with cedilla
                '`' => 'è',       // 96, the grave accent, used for the lowercase e with grave accent
                '{' => 'ï',       // 123, the opening brace, used for the lowercase i with diaeresis
                '|' => 'ê',       // 124, the vertical bar, used for the lowercase e with circumflex
                '}' => 'ï',       // 125, the closing brace, used for the lowercase i with diaeresis
                '~' => 'é',       // 126, the tilde, used for the lowercase e with acute
                '\u007F' => 'î',  // 127, the delete, used for the lowercase i with circumflex
                _ => text[i],
            };

            if (replacement != text[i])
            {
                fixedText ??= text.ToCharArray();
                fixedText[i] = replacement;
            }
        }

        return fixedText is null ? text : new string(fixedText);
    }

    /// <summary>
    /// Picks the code page a new or edited field is stored in: ASCII as is, Chinese text in GBK so the
    /// Chinese map packs can be written back, everything else in Latin-1.
    /// </summary>
    private static Encoding ChooseEncoding(string text)
    {
        if (IsAscii(text))
        {
            return Utf8;
        }

        if (Gbk is not null)
        {
            try
            {
                _ = Gbk.GetBytes(text);
                return Gbk;
            }
            catch (EncoderFallbackException)
            {
                // Not representable in GBK; Latin-1 keeps at least the accented letters.
            }
        }

        return Encoding.Latin1;
    }

    private static bool IsAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value >= 0x80)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAscii(string text)
    {
        foreach (char value in text)
        {
            if (value >= 0x80)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryDecode(ReadOnlySpan<byte> bytes, Encoding encoding, out string text)
    {
        try
        {
            text = encoding.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }

        // A decode that does not re-encode to the same bytes is a guess, not a decode.
        return encoding.GetBytes(text).AsSpan().SequenceEqual(bytes);
    }

    /// <summary>Counts the characters that make a GBK guess believable.</summary>
    /// <param name="text">Decoded text.</param>
    /// <returns>Number of CJK ideographs and full width punctuation marks.</returns>
    public static int CountCjkCharacters(string text)
    {
        int count = 0;

        foreach (char value in text)
        {
            if (value is >= '\u4E00' and <= '\u9FFF'
                or >= '\u3400' and <= '\u4DBF'
                or >= '\uF900' and <= '\uFAFF'
                or >= '\u3000' and <= '\u303F'
                or >= '\uFF00' and <= '\uFFEF')
            {
                ++count;
            }
        }

        return count;
    }

    private static Encoding? TryGetGbk()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            return Encoding.GetEncoding(GbkCodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
