using System.Diagnostics;
using FHeroes2.Assets;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Army;

/// <summary>
/// Looks up the small creature portrait a stack is drawn with, <c>Assets::getImage( ICN::MONS32, ... )</c> of
/// <c>drawMiniMonsters()</c>. <see cref="Mons32Sprites"/> is the implementation backed by the archives; tests
/// substitute their own.
/// </summary>
public interface IMiniMonsterSprites
{
    /// <summary>
    /// The portrait of a creature, or <see langword="null"/> when the set cannot draw
    /// <paramref name="monster"/>.
    /// </summary>
    Sprite? GetMiniMonster(MonsterType monster);
}

/// <summary>
/// The small creature portraits out of <c>MONS32.ICN</c>, the port of the <c>ICN::MONS32</c> lookup of
/// <c>drawMiniMonsters()</c>.
/// </summary>
/// <remarks>
/// One frame per creature, in monster order and starting after <see cref="MonsterType.Unknown"/>: the frame of a
/// creature is <c>Monster::GetSpriteIndex()</c>, i.e. its id minus one. The sprites are the 32 by 32 pixel
/// portraits the troop bars, the battle field and the "few wolves" lines are all made of — <c>MINIMON.ICN</c>,
/// which <see cref="AdventureMap.MonsterSpriteSet"/> reads, is the map's separate set at the same size.
/// </remarks>
public sealed class Mons32Sprites : IMiniMonsterSprites
{
    /// <summary>The archive entry that holds the small portraits.</summary>
    public const string FileName = "MONS32.ICN";

    private readonly IcnSpriteCache _cache;

    private Mons32Sprites(IcnSpriteCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Creates the set for an archive bundle; <see langword="null"/> when there is no data.
    /// </summary>
    public static Mons32Sprites? TryCreate(AssetBundle? assets) =>
        assets is null ? null : new Mons32Sprites(new IcnSpriteCache(assets));

    /// <summary>Number of portraits that have been decoded so far.</summary>
    public int LoadedCount => _cache.Count;

    /// <summary>The archive entries this set asked for but could not read.</summary>
    public IReadOnlyCollection<string> Missing => _cache.Missing;

    /// <summary>The portrait of <paramref name="monster"/>.</summary>
    public Sprite? GetMiniMonster(MonsterType monster) =>
        monster > MonsterType.Unknown ? _cache.GetSprite(FileName, FrameOf(monster)) : null;

    /// <summary>
    /// The frame of a creature, <c>Monster::GetSpriteIndex()</c>: the id minus one, because the unknown creature
    /// has no portrait and takes index zero anyway.
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns>Its frame in <c>MONS32</c>.</returns>
    public static int FrameOf(MonsterType monster) => (int)monster - 1;
}

/// <summary>
/// The one line rendering of an army, <c>fheroes2::drawMiniMonsters()</c> (<c>army_ui_helper.cpp:36</c>).
/// </summary>
/// <remarks>
/// <para>
/// Five stacks of 32 pixel portraits side by side with their counts under them, which is how the game draws an army
/// anywhere it does not have room for the full sized creature sprite: the troop bars, the garrison window, the
/// battle's turn order, and the "a few peasants" lines of the kingdom overview.
/// </para>
/// <para>
/// <b>The layout is the interesting part.</b> The line is cut into <c>count</c> equal chunks of
/// <paramref name="width"/> pixels each and every portrait is centred in its chunk, so a line of three stacks is
/// spaced differently from a line of five even though the total width is the same. When the caller asks for a
/// window of the line — <paramref name="first"/> and <paramref name="count"/> both non zero — the chunks are counted
/// from the right end so that a stack keeps its position no matter which window it appears in.
/// </para>
/// <para>
/// <b>Right to left.</b> With no window and <paramref name="first"/> zero the stacks are drawn right to left. That
/// is not a quirk: overlapping portraits of neighbouring stacks ("a lot of sprites summoned next to each other")
/// look better with the later stack on top, and the reference reached the same conclusion
/// (<c>army_ui_helper.cpp:49-54</c>).
/// </para>
/// <para>
/// <b>What the count says.</b> A detailed view prints the number; a garrison seen through a thieves' guild of one
/// level prints <c>???</c> and only a second guild reveals the bracket. The reference asserts that the count is set
/// in the garrison case, and this port does the same.
/// </para>
/// </remarks>
public static class MiniMonsters
{
    /// <summary>
    /// Draws the stacks of an army into one line, <c>fheroes2::drawMiniMonsters()</c>
    /// (<c>army_ui_helper.cpp:36</c>).
    /// </summary>
    /// <param name="troops">The army to draw.</param>
    /// <param name="cx">Left edge of the line, or its centre line in compact form.</param>
    /// <param name="cy">Top edge of the line.</param>
    /// <param name="width">Width the line may use.</param>
    /// <param name="first">How many of the drawn stacks to skip; zero to start at the first.</param>
    /// <param name="count">How many stacks to draw; zero for all of them.</param>
    /// <param name="compact">Whether to draw the short form with the counts beside the portraits.</param>
    /// <param name="detailedView">Whether the counts are printed as numbers.</param>
    /// <param name="garrisonView">Whether the counts may be hidden by a thieves' guild.</param>
    /// <param name="thievesGuildCount">How many thieves' guilds the viewer has.</param>
    /// <param name="output">The image to draw into.</param>
    /// <param name="sprites">Where the portraits come from.</param>
    public static void Draw(
        Army troops,
        int cx,
        int cy,
        int width,
        uint first,
        uint count,
        bool compact,
        bool detailedView,
        bool garrisonView,
        uint thievesGuildCount,
        PixelBuffer output,
        IMiniMonsterSprites sprites)
    {
        ArgumentNullException.ThrowIfNull(troops);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(sprites);

        if (!HasAnyTroop(troops))
        {
            return;
        }

        bool rightToLeft = false;

        if (count == 0)
        {
            count = troops.OccupiedSlotCount;

            rightToLeft = first == 0;
        }

        // An army of empty slots only: there is nothing to divide and nothing to draw.
        if (count == 0)
        {
            return;
        }

        double chunk = width / (double)count;

        if (!compact)
        {
            cx += (int)(chunk / 2);
        }

        if (!rightToLeft)
        {
            cx += width;
        }

        int slots = Army.MaximumTroopCount;

        for (int slot = 0; slot < slots && count != 0; ++slot)
        {
            Troop troop = troops.GetTroop(rightToLeft ? slots - slot - 1 : slot);

            if (!troop.IsValid)
            {
                continue;
            }

            if (first != 0)
            {
                --first;
                continue;
            }

            int posX = rightToLeft ? cx + (int)(chunk * (count - 1)) : cx - (int)(chunk * count);

            DrawStack(troop, posX, cy, chunk, compact, detailedView, garrisonView, thievesGuildCount, output, sprites);

            --count;
        }
    }

    /// <summary>
    /// Draws a list of stacks right to left in one line, <c>drawMiniMonsters()</c> over a <c>Troops</c> vector.
    /// </summary>
    /// <remarks>
    /// 参考的 <c>Troops</c> 是不限长的 <c>std::vector</c>，<c>Army</c> 从它继承；端口的 <see cref="Army"/> 固定
    /// 七槽，所以「阵亡部队」这种可能超过七条的列表走这个重载。摆放规则与「无窗口、从右往左」的军队行一致：
    /// 第一项在最右，最后一项在最左。
    /// </remarks>
    /// <param name="troops">The stacks to draw.</param>
    /// <param name="cx">Left edge of the line.</param>
    /// <param name="cy">Top edge of the line.</param>
    /// <param name="width">Width the line may use.</param>
    /// <param name="output">The image to draw into.</param>
    /// <param name="sprites">Where the portraits come from.</param>
    public static void Draw(IReadOnlyList<Troop> troops, int cx, int cy, int width, PixelBuffer output,
        IMiniMonsterSprites sprites)
    {
        ArgumentNullException.ThrowIfNull(troops);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(sprites);

        int count = 0;

        foreach (Troop troop in troops)
        {
            if (troop.IsValid)
            {
                ++count;
            }
        }

        if (count == 0)
        {
            return;
        }

        double chunk = width / (double)count;
        int remaining = count;

        cx += (int)(chunk / 2);

        foreach (Troop troop in troops)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            int posX = cx + (int)(chunk * (remaining - 1));

            DrawStack(troop, posX, cy, chunk, compact: false, detailedView: true, garrisonView: false,
                thievesGuildCount: 0, output, sprites);

            --remaining;
        }
    }

    /// <summary>Draws one stack whose chunk's reference point is <paramref name="posX"/>, the body of the line loops.</summary>
    private static void DrawStack(Troop troop, int posX, int cy, double chunk, bool compact, bool detailedView,
        bool garrisonView, uint thievesGuildCount, PixelBuffer output, IMiniMonsterSprites sprites)
    {
        string countText;

        if (detailedView || !garrisonView)
        {
            countText = Army.FormatMonsterCount(troop.Count, detailedView, compact);
        }
        else
        {
            Debug.Assert(thievesGuildCount > 0, "a hidden garrison count needs a thieves' guild");

            countText = thievesGuildCount == 1 ? "???" : Army.SizeString(troop.Count);
        }

        Sprite? monster = sprites.GetMiniMonster(troop.Monster);

        if (monster is null)
        {
            // The reference asserts here; a port has no business stopping the game for a missing picture.
            GameLog.Warn($"No small portrait for {troop.Monster}, the stack is not drawn.");

            return;
        }

        Text text = new(countText, FontType.SmallWhite());

        if (compact)
        {
            // The short form: the portrait and the count share one chunk, the portrait as high as the row.
            int offsetY = monster.Height < 37 ? 37 - monster.Height : 0;
            int offset = ((int)chunk - monster.Width - text.Width()) / 2;

            if (offset < 0)
            {
                offset = 0;
            }

            ImageOps.BlitSprite(output, new Point(posX + offset, cy + offsetY + monster.OffsetY), monster, monster.Image.Bounds);
            text.Draw(posX - text.Width() - offset + (int)chunk, cy + 23, output);
        }
        else
        {
            // The full form: every portrait sits on the line's baseline and its count is centred under it.
            int offsetY = 28 - monster.Height + monster.OffsetY;

            ImageOps.BlitSprite(
                output,
                new Point(posX - (monster.Width / 2) + monster.OffsetX + 2, cy + offsetY),
                monster,
                monster.Image.Bounds);

            text.Draw(posX - (text.Width() / 2), cy + 29, output);
        }
    }

    /// <summary>Whether an army has at least one stack that can be drawn, <c>Troops::isValid()</c>.</summary>
    /// <param name="troops">The army to look at.</param>
    /// <returns><see langword="true"/> when some slot holds a valid stack.</returns>
    private static bool HasAnyTroop(Army troops)
    {
        for (int slot = 0; slot < Army.MaximumTroopCount; ++slot)
        {
            if (troops.GetTroop(slot).IsValid)
            {
                return true;
            }
        }

        return false;
    }
}
