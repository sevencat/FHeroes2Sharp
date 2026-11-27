using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Builds the list of creatures a selection window shows and the window itself, <c>Dialog::selectMonster()</c>
/// (<c>dialog_selectitems.cpp:1204-1229</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference is one blocking function: it fills a vector with every creature but the five random markers, bails
/// out with a message when nothing is left, opens <c>SelectEnumMonster</c>, moves the selection to the id the caller
/// passed and returns what the loop ended on. The port splits that into the pieces a scene can drive:
/// <see cref="BuildMonsterList"/> fills the vector, <see cref="DialogSize"/> gives the reference's
/// <c>{ 320, display.height - 150 }</c>, <see cref="TryCreate"/> opens the window and returns <see langword="null"/>
/// instead of showing the message, and <see cref="MonsterSelectionWindow.SelectedMonster"/> is the return value once
/// the window says <see cref="Widgets.DialogButton.Ok"/>.
/// </para>
/// <para>
/// 参数逐项对应参考：<c>monsterId</c> 是打开时落在哪一只（<see cref="MonsterType.Unknown"/> 表示从头开始）。
/// </para>
/// </remarks>
public static class MonsterSelectDialog
{
    /// <summary>Height taken off the screen height, <c>dialogHeightDeduction</c> in the reference.</summary>
    public const int DialogHeightDeduction = 150;

    /// <summary>Width of the window, 320 pixels in the reference.</summary>
    public const int DialogWidth = 320;

    /// <summary>
    /// The message the reference shows when the list turns out empty,
    /// <c>_( "There are no monsters to select from." )</c>.
    /// </summary>
    public const string EmptyListMessage = "There are no monsters to select from.";

    /// <summary>
    /// The size of the window, <c>{ 320, display.height - dialogHeightDeduction }</c>
    /// (<c>dialog_selectitems.cpp:1217</c>).
    /// </summary>
    /// <param name="screen">Screen size, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The size of the active area the reference asks for.</returns>
    public static Size DialogSize(Size? screen = null)
    {
        Size display = screen ?? ScreenBuffer.DefaultSize;

        return new Size(DialogWidth, Math.Max(0, display.Height - DialogHeightDeduction));
    }

    /// <summary>
    /// Fills the id vector, the head of the reference's <c>selectMonster()</c>
    /// (<c>dialog_selectitems.cpp:1206-1211</c>).
    /// </summary>
    /// <remarks>
    /// Every id from <see cref="MonsterType.Unknown"/> + 1 up to the last creature is listed in order, and the five
    /// random markers are dropped: the editor cannot add a stack of "some level 2 creatures" to an army.
    /// </remarks>
    /// <returns>The ids to show, the reference's <c>std::vector&lt;int32_t&gt; monsters</c>.</returns>
    public static List<int> BuildMonsterList()
    {
        List<int> monsters = new();

        for (int id = (int)MonsterType.Unknown + 1; id < (int)MonsterType.MonsterCount; ++id)
        {
            if (!MonsterStatistics.IsRandomMonster((MonsterType)id))
            {
                monsters.Add(id);
            }
        }

        return monsters;
    }

    /// <summary>
    /// Opens the window of the reference's <c>selectMonster()</c> and moves the selection to the requested creature
    /// (<c>dialog_selectitems.cpp:1213-1228</c>).
    /// </summary>
    /// <param name="monsterId">The creature the list starts on, or <see cref="MonsterType.Unknown"/> for the first row.</param>
    /// <param name="sprites">The small portraits, <c>ICN::MONS32</c>.</param>
    /// <param name="plate">The 43x43 plate the portraits stand on, <see cref="MonsterSelectionWindow.CutPlate"/>.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>
    /// The window, or <see langword="null"/> when there is nothing to pick — the caller shows
    /// <see cref="EmptyListMessage"/> with the heading "Warning" exactly like the reference does.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sprites"/>, <paramref name="artwork"/>, <paramref name="titleFont"/> or
    /// <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public static MonsterSelectionWindow? TryCreate(MonsterType monsterId, IMiniMonsterSprites sprites,
        Sprite? plate, ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(font);

        List<int> monsters = BuildMonsterList();

        if (monsters.Count == 0)
        {
            return null;
        }

        MonsterSelectionWindow window = new(monsters, sprites, plate, DialogSize(screen), artwork, titleFont, font,
            screen);

        if (monsterId != MonsterType.Unknown)
        {
            window.SelectMonster(monsterId);
        }

        return window;
    }
}
