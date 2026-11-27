using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Editor;

/// <summary>
/// The list of heroes a victory or loss condition can point at, a port of <c>SelectMapHero</c>
/// (<c>editor_map_specs_window.cpp:241-262</c> and <c>:361-395</c>).
/// </summary>
/// <remarks>
/// 参考的行由 <c>getHeroIcon()</c> 的小头像加 <c>getHeroTitle()</c> 的标题组成，图标中心在第 40 列、标题从第 85 列起
/// （<c>SelectMapHero::RedrawItem</c>），与城镇列表只差这两个偏移。
/// </remarks>
public sealed class EditorMapSpecsHeroPicker : EditorMapSpecsTargetPicker
{
    /// <summary>Horizontal middle of the icon inside a row, <c>renderItem( ..., 40, 85, ... )</c>.</summary>
    public const int IconOffsetX = 40;

    /// <summary>Left edge of the title inside a row, <c>renderItem( ..., 40, 85, ... )</c>.</summary>
    public const int TextOffsetX = 85;

    private readonly IReadOnlyList<MapSpecsConditionHeroRowData> _heroes;
    private readonly Dictionary<int, Sprite?> _icons;

    /// <summary>Lays out the window and fills the list with every candidate hero.</summary>
    /// <param name="heroes">The heroes, normally <see cref="MapSpecsPanelViews.CreateHeroRows"/>.</param>
    /// <param name="mapWidth">Width of the map, which turns a row id into a tile.</param>
    /// <param name="current">The tile the condition points at right now, selected when the window opens.</param>
    /// <param name="title">Heading, one of the titles of <see cref="EditorMapSpecsTargetPicker"/>.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="evilInterface">Whether the evil interface artwork is used.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="Game.System.ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="heroes"/>, <paramref name="artwork"/>, <paramref name="titleFont"/> or <paramref name="font"/> is
    /// <see langword="null"/>.
    /// </exception>
    public EditorMapSpecsHeroPicker(IReadOnlyList<MapSpecsConditionHeroRowData> heroes, int mapWidth, Point current,
        string title, ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font,
        bool evilInterface = false, Size? screen = null)
        : base(GetDialogSize(heroes?.Count ?? 0, screen), title, mapWidth, CreateRows(heroes), artwork, titleFont,
            font, screen)
    {
        _heroes = heroes ?? throw new ArgumentNullException(nameof(heroes));
        _icons = new Dictionary<int, Sprite?>(_heroes.Count);

        foreach (MapSpecsConditionHeroRowData hero in _heroes)
        {
            _icons[hero.TileIndex] = MapSpecsConditionIcons.BuildHeroIcon(hero.Race, hero.Color, evilInterface);
        }

        SelectTile(current);
    }

    /// <summary>Paints one hero of the list, <c>SelectMapHero::RedrawItem()</c>.</summary>
    /// <param name="item">The row, whose id is the tile index of its hero.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is the selected one.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public override void RedrawItem(ItemSelectionEntry item, int x, int y, bool current, PixelBuffer output)
    {
        if (!TryFindHero(item.Id, out MapSpecsConditionHeroRowData hero))
        {
            return;
        }

        _icons.TryGetValue(item.Id, out Sprite? icon);

        RenderRow(icon, MapSpecsConditionTitles.GetHeroTitle(hero.Name, hero.Race, hero.TileIndex, MapWidth),
            IconOffsetX, TextOffsetX, x, y, current, output);
    }

    /// <summary>Shows the title of the hero the cursor is over, <c>SelectMapHero::ActionListPressRight()</c>.</summary>
    /// <param name="item">The row the cursor is over.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public override void ActionListPressRight(ItemSelectionEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (TryFindHero(item.Id, out MapSpecsConditionHeroRowData hero))
        {
            Popup = new DialogPopup(
                string.Empty, MapSpecsConditionTitles.GetHeroTitle(hero.Name, hero.Race, hero.TileIndex, MapWidth));
        }
    }

    /// <summary>Looks the hero of a row up by its tile index.</summary>
    /// <param name="tileIndex">Tile index of the row.</param>
    /// <param name="hero">The hero of that row, when there is one.</param>
    /// <returns>Whether the row belongs to a hero.</returns>
    private bool TryFindHero(int tileIndex, out MapSpecsConditionHeroRowData hero)
    {
        foreach (MapSpecsConditionHeroRowData candidate in _heroes)
        {
            if (candidate.TileIndex == tileIndex)
            {
                hero = candidate;

                return true;
            }
        }

        hero = default;

        return false;
    }

    private static List<ItemSelectionEntry> CreateRows(IReadOnlyList<MapSpecsConditionHeroRowData>? heroes)
    {
        ArgumentNullException.ThrowIfNull(heroes);

        List<ItemSelectionEntry> rows = new(heroes.Count);

        foreach (MapSpecsConditionHeroRowData hero in heroes)
        {
            rows.Add(new ItemSelectionEntry(hero.TileIndex));
        }

        return rows;
    }
}
