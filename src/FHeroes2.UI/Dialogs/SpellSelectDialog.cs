using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Builds the list of spells a selection window shows and the window itself,
/// <c>Dialog::selectSpell()</c> (<c>dialog_selectitems.cpp:1116-1160</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference is one blocking function: it fills a vector, bails out with a message when nothing is
/// left, opens <c>SelectEnumSpell</c>, moves the selection to the id the caller passed and returns what the
/// loop ended on. The port splits that into the pieces a scene can drive: <see cref="BuildSpellList"/>
/// fills the vector, <see cref="DialogSize"/> gives the reference's <c>{ 340, display.height - 150 }</c>,
/// <see cref="TryCreate"/> opens the window and returns <see langword="null"/> instead of showing the
/// message, and <see cref="SpellSelectionWindow.SelectedSpell"/> is the return value once the window says
/// <see cref="Widgets.DialogButton.Ok"/>.
/// </para>
/// <para>
/// 参数逐项对应参考：<c>spellId</c> 是打开时落在哪一项（<c>Spell::RANDOM</c> 表示「随机」也进列表），
/// <c>includeRandomSpells</c> 决定列表里放不放「随机」那一项，
/// <c>excludeSpellsList</c> 是已经点过、不再露出的法术，
/// <c>spellsLevel</c> 为负表示不限等级。
/// </para>
/// </remarks>
public static class SpellSelectDialog
{
    /// <summary>Height taken off the screen height, <c>dialogHeightDeduction</c> in the reference.</summary>
    public const int DialogHeightDeduction = 150;

    /// <summary>Width of the window, 340 pixels in the reference.</summary>
    public const int DialogWidth = 340;

    /// <summary>
    /// The message the reference shows when the level and the excluded spells leave the list empty,
    /// <c>_( "There are no spells to select." )</c>.
    /// </summary>
    public const string EmptyListMessage = "There are no spells to select.";

    /// <summary>
    /// The size of the window, <c>{ 340, display.height - dialogHeightDeduction }</c>
    /// (<c>dialog_selectitems.cpp:1127-1128</c>).
    /// </summary>
    /// <param name="screen">Screen size, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The size of the active area the reference asks for.</returns>
    public static Size DialogSize(Size? screen = null)
    {
        Size display = screen ?? ScreenBuffer.DefaultSize;

        return new Size(DialogWidth, Math.Max(0, display.Height - DialogHeightDeduction));
    }

    /// <summary>
    /// Fills the id vector, the head of the reference's <c>selectSpell()</c>
    /// (<c>dialog_selectitems.cpp:1118-1124</c>).
    /// </summary>
    /// <remarks>
    /// The random entry goes first, then the spells of the level in the order of
    /// <see cref="Spell.SuitableForSpellBook"/>, exactly like the reference's <c>insert( begin(), RANDOM )</c>
    /// over <c>getAllSpellIdsSuitableForSpellBook()</c>.
    /// </remarks>
    /// <param name="includeRandomSpells">Whether the random entry is part of the list.</param>
    /// <param name="excludeSpellsList">Spells the player has already used up.</param>
    /// <param name="spellsLevel">Only this level, or a negative number for all levels.</param>
    /// <returns>The ids to show, the reference's <c>std::vector&lt;int&gt; spells</c>.</returns>
    public static List<int> BuildSpellList(bool includeRandomSpells, ISet<int>? excludeSpellsList = null,
        int spellsLevel = -1)
    {
        HashSet<SpellType>? excluded = null;

        if (excludeSpellsList is not null)
        {
            excluded = new HashSet<SpellType>(excludeSpellsList.Count);

            foreach (int id in excludeSpellsList)
            {
                excluded.Add((SpellType)id);
            }
        }

        List<int> spells = new();

        if (includeRandomSpells)
        {
            spells.Add((int)SpellType.Random);
        }

        foreach (SpellType spell in Spell.SuitableForSpellBook(spellsLevel, excluded))
        {
            spells.Add((int)spell);
        }

        return spells;
    }

    /// <summary>
    /// Opens the window of the reference's <c>selectSpell()</c> and moves the selection to the requested
    /// spell (<c>dialog_selectitems.cpp:1126-1145</c>).
    /// </summary>
    /// <param name="spellId">The spell the list starts on, or the random-spell id.</param>
    /// <param name="includeRandomSpells">Whether the random entry is part of the list.</param>
    /// <param name="excludeSpellsList">Spells the player has already used up.</param>
    /// <param name="spellsLevel">Only this level, or a negative number for all levels.</param>
    /// <param name="spellIcons">Every frame of <c>ICN::SPELLS</c>, indexed by <c>Spell::IndexSprite()</c>.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>
    /// The window, or <see langword="null"/> when there is nothing to pick — the caller shows
    /// <see cref="EmptyListMessage"/> with the heading "Spells" exactly like the reference does.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spellIcons"/>, <paramref name="artwork"/>, <paramref name="titleFont"/> or
    /// <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public static SpellSelectionWindow? TryCreate(int spellId, bool includeRandomSpells,
        ISet<int>? excludeSpellsList, int spellsLevel, IReadOnlyList<Sprite> spellIcons,
        ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(spellIcons);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(font);

        List<int> spells = BuildSpellList(includeRandomSpells, excludeSpellsList, spellsLevel);

        if (spells.Count == 0)
        {
            return null;
        }

        SpellSelectionWindow window = new(spells, spellIcons, DialogSize(screen), artwork, titleFont, font,
            screen);

        window.SelectSpell((SpellType)spellId);

        return window;
    }
}
