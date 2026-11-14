using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The fixes <c>processICN</c> applies to <c>FONT.ICN</c> / <c>SMALFONT.ICN</c> before any face is derived
/// from them (<c>src/fheroes2/game/game_assets.cpp:2461</c>).
/// </summary>
/// <remarks>
/// <para>
/// 三个独立的问题，都靠「文件内容的 CRC32」认人，所以都从这里过：
/// </para>
/// <list type="bullet">
/// <item>波兰 GoG 版小号字体整体下移了一个像素（<c>0xE9EC7A63</c>）；</item>
/// <item>波兰资材把 <c>*</c> 画成了 <c>.</c>，要从<b>基础</b>归档里再解一次同一帧换回来；</item>
/// <item>德语/意大利语资材用 CP1252、俄语资材用 CP1251，帧的顺序和 ASCII 对不上，要重排。</item>
/// </list>
/// <para>
/// 重排之后的数组仍然按「帧号 = 字符 − 0x20」解释：参考是在帧号空间里插入空帧、对调、截断的
/// （它最后才在数组前面补 32 个空帧，本项目用偏移量代替那一步），所以这里不需要额外换算。
/// </para>
/// </remarks>
internal static class FontAssetFixes
{
    /// <summary>Small font of the official Polish GoG version, shifted 1 pixel down.</summary>
    private const uint PolishSmallFontCrc = 0xE9EC7A63;

    /// <summary>German <c>FONT.ICN</c>, CP1252 (GoG version).</summary>
    private const uint GermanCrc = 0x04745D1D;

    /// <summary>Another German <c>FONT.ICN</c>, CP1252.</summary>
    private const uint GermanAltCrc = 0xD0F0D852;

    /// <summary>Italian <c>FONT.ICN</c>, CP1252.</summary>
    private const uint ItalianCrc = 0x219B3124;

    /// <summary>Another Italian <c>FONT.ICN</c>, CP1252.</summary>
    private const uint ItalianAltCrc = 0x1F3C3C74;

    /// <summary>Frame count of a CP1251 font: CP1251 letters plus the ASCII part.</summary>
    private const int CyrillicFrameCount = 162;

    /// <summary>Frame where the rearranged CP1252 tables start to diverge from ASCII.</summary>
    private const int Cp1252InsertFrame = 96;

    /// <summary>Blank frames a German table gains, <c>imageArray.insert( begin() + 96, 124, ... )</c>.</summary>
    private const int Cp1252GermanBlanks = 124;

    /// <summary>Highest frame index a German table touches, <c>std::swap( ..., imageArray[226] )</c>.</summary>
    private const int Cp1252GermanLastFrame = 226;

    /// <summary>Frame the Italian assets keep their ASCII part at before the CP1252 extension.</summary>
    private const int ItalianInsertFrame = 101;

    /// <summary>Blank frames an Italian table gains, <c>imageArray.insert( begin() + 101, 123, ... )</c>.</summary>
    private const int ItalianBlanks = 123;

    /// <summary>Highest frame index an Italian table touches, <c>imageArray[249 - 32]</c>.</summary>
    private const int ItalianLastFrame = 217;

    /// <summary>Frame of <c>'*'</c>, the sprite the Polish assets replace with a dot.</summary>
    public const int AsteriskFrame = 42 - GlyphFrameOffset;

    /// <summary>Frame index of a character in a font table, the reference's <c>ch - 0x20</c>.</summary>
    private const int GlyphFrameOffset = (int)FontCharHandler.GlyphFrameOffset;

    /// <summary>CRC32 of the archive entry the fixes of a font depend on, <c>0</c> when it is absent.</summary>
    /// <param name="fileName">Archive entry name, for example <c>FONT.ICN</c>.</param>
    /// <returns>The fingerprint, or <c>0</c>.</returns>
    public static uint GetFingerprint(string fileName) =>
        Assets.TryReadRaw(fileName) is { Length: > 0 } data ? Crc32.Compute(data) : 0;

    /// <summary>
    /// Whether <paramref name="crc"/> is the small font of the Polish GoG version, which sits 1 pixel too low.
    /// </summary>
    /// <param name="crc">Fingerprint from <see cref="GetFingerprint"/>.</param>
    /// <returns><see langword="true"/> when every letter has to be moved up.</returns>
    public static bool IsPolishSmallFont(uint crc) => crc == PolishSmallFontCrc;

    /// <summary>Moves every frame 1 pixel up, <c>letter.setPosition( letter.x(), letter.y() - 1 )</c>.</summary>
    /// <param name="frames">Frames to shift; <see langword="null"/> entries are kept as they are.</param>
    /// <returns>The shifted frames, sharing their images with the input.</returns>
    public static Sprite?[] MoveUp(Sprite?[] frames)
    {
        Sprite?[] shifted = new Sprite?[frames.Length];

        for (int index = 0; index < frames.Length; ++index)
        {
            shifted[index] = frames[index] is { } glyph ? FontGlyph.Moved(glyph, glyph.OffsetX, glyph.OffsetY - 1) : null;
        }

        return shifted;
    }

    /// <summary>
    /// Puts the asterisk of the English assets in place of the dot the Polish assets carry,
    /// <c>replacePOLAssetWithSW( id, 42 - 32 )</c>.
    /// </summary>
    /// <param name="fileName">Archive entry the frames came from.</param>
    /// <param name="frames">Frames to fix in place.</param>
    public static void RestoreAsterisk(string fileName, Sprite?[] frames)
    {
        if (AsteriskFrame >= frames.Length)
        {
            return;
        }

        if (Assets.GetBaseArchiveImage(fileName, AsteriskFrame) is { } asterisk)
        {
            frames[AsteriskFrame] = asterisk;
        }
    }

    /// <summary>
    /// Reorders the frames of a translated font so that the frame index still equals the character code
    /// minus <c>0x20</c>.
    /// </summary>
    /// <param name="frames">Frames as read from the archive (or already shifted); not modified.</param>
    /// <param name="crc">Fingerprint from <see cref="GetFingerprint"/>.</param>
    /// <param name="size">Size of the font; picks the width expected of a CP1251 letter.</param>
    /// <returns>The reordered frames, the input itself when no rearrangement applies.</returns>
    public static Sprite?[] Arrange(Sprite?[] frames, uint crc, FontSize size)
    {
        List<Sprite?> arranged = new(frames);

        ArrangeCyrillic(arranged, size);

        if (crc is GermanCrc or GermanAltCrc)
        {
            ArrangeGerman(arranged);
        }

        if (crc is ItalianCrc or ItalianAltCrc)
        {
            ArrangeItalian(arranged);
        }

        return arranged.Count == frames.Length ? frames : arranged.ToArray();
    }

    /// <summary>
    /// CP1251 fonts store <c>0x20-0x7F</c>, <c>0xC0-0xDF</c>, <c>0xA8</c>, <c>0xE0-0xFF</c>, <c>0xB8</c>
    /// in that order; this puts <c>0xA8</c> and <c>0xB8</c> where they belong.
    /// </summary>
    private static void ArrangeCyrillic(List<Sprite?> frames, FontSize size)
    {
        int expectedWidth = size == FontSize.Normal ? 19 : 12;

        // 参考拿「帧数正好 162」加「161 号帧的宽度」双重判断，怕的是把别的语言版本误认成 CP1251。
        if (frames.Count != CyrillicFrameCount || frames[121]?.Width != expectedWidth)
        {
            return;
        }

        InsertBlankFrames(frames, Cp1252InsertFrame, 64);

        Swap(frames, 136, 192);
        Swap(frames, 152, 225);

        frames.RemoveAt(frames.Count - 1);
        frames.RemoveAt(192);
    }

    /// <summary>The German assets use CP1252, so frames <c>0xA0</c> and up come in a different order.</summary>
    private static void ArrangeGerman(List<Sprite?> frames)
    {
        // 参考直接 insert / swap / erase，数组比 226 短时那些下标就越界（未定义行为）；这里当作没有命中。
        if (frames.Count + Cp1252GermanBlanks <= Cp1252GermanLastFrame)
        {
            return;
        }

        InsertBlankFrames(frames, Cp1252InsertFrame, Cp1252GermanBlanks);

        Swap(frames, 164, 224);
        Swap(frames, 182, 225);
        Swap(frames, 188, 226);
        Swap(frames, 191, 223);
        Swap(frames, 196, 220);
        Swap(frames, 214, 221);
        Swap(frames, 220, 222);

        frames.RemoveRange(221, frames.Count - 221);
    }

    /// <summary>The Italian assets use CP1252 and, on top of that, borrow glyphs from the ASCII part.</summary>
    private static void ArrangeItalian(List<Sprite?> frames)
    {
        // 同德语：参考的 insert 位置和最高借用下标都按 218 帧的表写的，短数组一律跳过。
        if (frames.Count + ItalianBlanks <= ItalianLastFrame)
        {
            return;
        }

        InsertBlankFrames(frames, ItalianInsertFrame, ItalianBlanks);

        // 参考逐条把基础字的字形借到 CP1252 的位置上（é、è、ì、ò、ù 等）。
        frames[192 - GlyphFrameOffset] = frames[33];
        frames[200 - GlyphFrameOffset] = frames[37];
        frames[201 - GlyphFrameOffset] = frames[37];
        frames[204 - GlyphFrameOffset] = frames[41];
        frames[210 - GlyphFrameOffset] = frames[47];
        frames[217 - GlyphFrameOffset] = frames[53];
        frames[224 - GlyphFrameOffset] = frames[96];
        frames[232 - GlyphFrameOffset] = frames[97];
        frames[233 - GlyphFrameOffset] = frames[69];
        frames[236 - GlyphFrameOffset] = frames[98];
        frames[242 - GlyphFrameOffset] = frames[99];
        frames[249 - GlyphFrameOffset] = frames[100];

        frames.RemoveRange(218, frames.Count - 218);
    }

    /// <summary>
    /// Inserts copies of the first frame (the space glyph), the reference's
    /// <c>imageArray.insert( begin() + frame, count, firstSprite )</c>.
    /// </summary>
    private static void InsertBlankFrames(List<Sprite?> frames, int frame, int count)
    {
        if (frame > frames.Count)
        {
            return;
        }

        Sprite? blank = frames[0];
        List<Sprite?> blanks = new(count);

        for (int index = 0; index < count; ++index)
        {
            blanks.Add(blank);
        }

        frames.InsertRange(frame, blanks);
    }

    private static void Swap(List<Sprite?> frames, int first, int second) =>
        (frames[first], frames[second]) = (frames[second], frames[first]);
}
