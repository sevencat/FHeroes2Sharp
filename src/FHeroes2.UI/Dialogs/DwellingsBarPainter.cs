using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The drawing half of the town screen's dwelling bar, <c>DwellingsBar</c>'s constructor tail and
/// <c>DwellingsBar::RedrawBackground()</c> / <c>RedrawItem()</c> (<c>castle/buildinginfo.cpp:644-687</c>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DwellingsBar"/> in the game assembly answers what the bar holds, what it grows and what a click would do;
/// this class is what puts it on the screen. The split matches the reference, where the same file holds both.
/// </para>
/// <para>
/// The reference builds the 39x52 plate once in the constructor, because drawing the border for six slots every frame
/// is wasted work; <see cref="CreatePlate"/> is that step.
/// </para>
/// </remarks>
public static class DwellingsBarPainter
{
    /// <summary>The plate of one dwelling slot, the constructor's <c>backsf</c>.</summary>
    /// <param name="size">Side of a slot, <c>DwellingsBar::getRenderingSize()</c>.</param>
    /// <param name="borderColor">The palette index of the frame, <c>GetColorId( 0xD0, 0xC0, 0x48 )</c>.</param>
    /// <returns>The plate, transparent outside the frame.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive.</exception>
    public static Sprite CreatePlate(Size size, byte borderColor)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A dwelling slot needs a positive size.");
        }

        Sprite plate = new(size.Width, size.Height);
        plate.Reset();
        ImageOps.DrawBorder(plate.Image, borderColor);

        return plate;
    }

    /// <summary>Paints the plate of one slot, <c>DwellingsBar::RedrawBackground()</c>.</summary>
    /// <param name="position">Where the slot goes.</param>
    /// <param name="plate">The plate of <see cref="CreatePlate"/>.</param>
    /// <param name="output">The frame to paint into.</param>
    public static void RedrawBackground(Rect position, Sprite plate, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(plate);
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, position.Position, plate);
    }

    /// <summary>Paints one dwelling, <c>DwellingsBar::RedrawItem()</c> (<c>buildinginfo.cpp:653-687</c>).</summary>
    /// <param name="bar">The bar the dwelling belongs to.</param>
    /// <param name="item">The dwelling.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <param name="monsters">The 32x32 creature icons, <c>MONS32</c>.</param>
    /// <param name="smallWhite">The font of the creature count, <c>smallWhite</c>.</param>
    /// <param name="smallYellow">The font of the weekly growth, <c>smallYellow</c>.</param>
    /// <param name="notBuiltMarker">The padlock of an unbuilt dwelling, <c>CSLMARKER</c> frame 0.</param>
    /// <param name="output">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// An unbuilt dwelling shows the padlock and nothing else — not the creature it would hold, and not its growth,
    /// which the reference skips with an early return.
    /// </remarks>
    public static void RedrawItem(DwellingsBar bar, DwellingItem item, Rect position, IMiniMonsterSprites monsters,
        IGlyphSource smallWhite, IGlyphSource smallYellow, Sprite notBuiltMarker, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(bar);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(smallWhite);
        ArgumentNullException.ThrowIfNull(smallYellow);
        ArgumentNullException.ThrowIfNull(notBuiltMarker);
        ArgumentNullException.ThrowIfNull(output);

        Castle castle = bar.Castle;
        MonsterType monster = DwellingMonsters.FromDwelling(castle.Race, castle.GetActualDwelling(item.Dwelling));

        Sprite icon = monsters.GetMiniMonster(monster)
            ?? throw new InvalidOperationException($"The MONS32 icon of {monster} is missing.");

        // Centred horizontally, sitting on the bottom edge of the slot.
        ImageOps.BlitSprite(
            output,
            new Point(position.X + ((position.Width - icon.Width) / 2), position.Y + position.Height - 3 - icon.Height),
            icon);

        if (!bar.IsBuilt(item))
        {
            ImageOps.BlitSprite(
                output,
                new Point(position.X + position.Width - 10, position.Y + 4),
                notBuiltMarker);

            return;
        }

        // The stock, right aligned along the bottom.
        string count = bar.GetMonstersInDwelling(item).ToString(System.Globalization.CultureInfo.InvariantCulture);
        GlyphText.DrawLine(
            smallWhite,
            output,
            new Point(
                position.X + position.Width - GlyphText.Measure(smallWhite, count) - 3,
                position.Y + position.Height - smallWhite.LineHeight + 1),
            count);

        // The weekly growth, right aligned at the top.
        string growth = "+" + bar.GetGrowth(item).ToString(System.Globalization.CultureInfo.InvariantCulture);
        GlyphText.DrawLine(
            smallYellow,
            output,
            new Point(position.X + position.Width - GlyphText.Measure(smallYellow, growth) - 3, position.Y + 4),
            growth);
    }
}
