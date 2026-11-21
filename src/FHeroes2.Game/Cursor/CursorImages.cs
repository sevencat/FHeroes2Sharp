using System.Collections.Concurrent;

using FHeroes2.Assets.Cursor;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Cursor;

/// <summary>
/// Resolves a <see cref="CursorImageSet"/> frame to the sprite the cursor draws, the image half of
/// <c>Cursor::SetThemes()</c> (<c>src/fheroes2/gui/cursor.cpp:46-60</c>) together with the <c>Assets::getImage</c>
/// calls it relies on.
/// </summary>
/// <remarks>
/// <para>
/// The color sets are ordinary ICN files (<c>ADVMCO.ICN</c>, <c>CMSECO.ICN</c>, <c>SPELCO.ICN</c>) and go through
/// <see cref="Assets.GetImage"/>. The monochrome sets are not ICN at all: the reference builds them out of per sprite
/// <c>.BMP</c> entries (<c>game_assets.cpp:4094-4138</c>), which is what <see cref="MonoCursorFile"/> was ported for.
/// The entry names carry the upstream quirks: <c>ADVMBW01.BMP</c> and <c>CMSEBW01.BMP</c> count from one, while
/// <c>SPELBW00.BMP</c> counts from zero.
/// </para>
/// <para>
/// 偏差：<see cref="CursorImageSet.AdventureMapDistanceColor"/> / <see cref="CursorImageSet.AdventureMapDistanceMonochrome"/>
/// 返回 <see langword="null"/>。参考的这两套是 <c>generateDefaultImages()</c> 在启动时用 <c>ADVMCO.ICN</c> 的 4..11 帧加程序画的
/// 数字现拼的（<c>game_assets.cpp:3694-3740</c>），属于资材合成而不是光标逻辑，B 步没搬。缺图时
/// <see cref="CursorState.SetThemes"/> 只记主题、不换图，见其注释。
/// </para>
/// </remarks>
public static class CursorImages
{
    /// <summary>Color adventure map cursors, <c>ICN::ADVMCO</c>.</summary>
    public const string AdventureMapIcn = "ADVMCO.ICN";

    /// <summary>Color combat cursors, <c>ICN::CMSECO</c>.</summary>
    public const string CombatIcn = "CMSECO.ICN";

    /// <summary>Color spell cursors, <c>ICN::SPELCO</c>.</summary>
    public const string SpellIcn = "SPELCO.ICN";

    private static readonly ConcurrentDictionary<(string Prefix, int Index), Sprite?> MonoCache = new();

    /// <summary>
    /// One frame of a cursor set, or <see langword="null"/> when the archive does not have it — the reference's
    /// <c>Assets::getImage( icnID, 0xFF &amp; theme )</c>.
    /// </summary>
    /// <param name="set">The set selected by <see cref="CursorLayout.ImageSet"/>.</param>
    /// <param name="index">Sprite index inside the set, the low byte of the theme.</param>
    public static Sprite? GetFrame(CursorImageSet set, int index)
    {
        if (index < 0)
        {
            return null;
        }

        return set switch
        {
            CursorImageSet.AdventureMap => Assets.GetImage(AdventureMapIcn, index),
            CursorImageSet.Combat => Assets.GetImage(CombatIcn, index),
            CursorImageSet.Spell => Assets.GetImage(SpellIcn, index),

            // The monochrome sets count from one, the exception of SPELBW is handled below.
            CursorImageSet.AdventureMapMonochrome => GetMonochrome("ADVMBW", index + 1, index),
            CursorImageSet.CombatMonochrome => GetMonochrome("CMSEBW", index + 1, index),
            CursorImageSet.SpellMonochrome => GetMonochrome("SPELBW", index, index),

            _ => null,
        };
    }

    /// <summary>
    /// Drops the cached monochrome frames, the counterpart of what <see cref="Assets.Current"/> does when the archive
    /// bundle is replaced.
    /// </summary>
    public static void ClearCache() => MonoCache.Clear();

    private static Sprite? GetMonochrome(string prefix, int number, int index)
    {
        if (MonoCache.TryGetValue((prefix, index), out Sprite? cached))
        {
            return cached;
        }

        string fileName = prefix + (number < 10 ? "0" : string.Empty) + number + ".BMP";

        Sprite? sprite = Assets.Current?.TryRead(fileName) is { } data ? MonoCursorFile.Parse(data) : null;

        MonoCache[(prefix, index)] = sprite;

        return sprite;
    }
}
