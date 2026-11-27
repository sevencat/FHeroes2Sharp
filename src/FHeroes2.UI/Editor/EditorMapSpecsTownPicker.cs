using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Editor;

/// <summary>
/// The list of towns a victory or loss condition can point at, a port of <c>SelectMapCastle</c>
/// (<c>editor_map_specs_window.cpp:180-240</c> and <c>:264-330</c>).
/// </summary>
/// <remarks>
/// 参考的行由 <c>getTownIcon()</c> 的图标加 <c>getTownTitle()</c> 的标题组成，图标中心在第 45 列、标题从第 95 列起
/// （<c>SelectMapCastle::RedrawItem</c>）；这一行与条件面板里的城镇行画的是同一套东西。
/// </remarks>
public sealed class EditorMapSpecsTownPicker : EditorMapSpecsTargetPicker
{
    /// <summary>Horizontal middle of the icon inside a row, <c>renderItem( ..., 45, 95, ... )</c>.</summary>
    public const int IconOffsetX = 45;

    /// <summary>Left edge of the title inside a row, <c>renderItem( ..., 45, 95, ... )</c>.</summary>
    public const int TextOffsetX = 95;

    private readonly IReadOnlyList<MapSpecsConditionTownRowData> _towns;
    private readonly Dictionary<int, Sprite?> _icons;

    /// <summary>Lays out the window and fills the list with every town of the map.</summary>
    /// <param name="towns">The towns, normally <see cref="MapSpecsPanelViews.CreateTownRows"/>.</param>
    /// <param name="mapWidth">Width of the map, which turns a row id into a tile.</param>
    /// <param name="current">The tile the condition points at right now, selected when the window opens.</param>
    /// <param name="title">Heading, one of the titles of <see cref="EditorMapSpecsTargetPicker"/>.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="evilInterface">Whether the evil interface artwork is used.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="Game.System.ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="towns"/>, <paramref name="artwork"/>, <paramref name="titleFont"/> or <paramref name="font"/> is
    /// <see langword="null"/>.
    /// </exception>
    public EditorMapSpecsTownPicker(IReadOnlyList<MapSpecsConditionTownRowData> towns, int mapWidth, Point current,
        string title, ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font,
        bool evilInterface = false, Size? screen = null)
        : base(GetDialogSize(towns?.Count ?? 0, screen), title, mapWidth, CreateRows(towns), artwork, titleFont,
            font, screen)
    {
        _towns = towns ?? throw new ArgumentNullException(nameof(towns));
        _icons = new Dictionary<int, Sprite?>(_towns.Count);

        foreach (MapSpecsConditionTownRowData town in _towns)
        {
            _icons[town.TileIndex] =
                MapSpecsConditionIcons.BuildTownIcon(town.Race, town.Color, town.HasFort, evilInterface);
        }

        SelectTile(current);
    }

    /// <summary>Paints one town of the list, <c>SelectMapCastle::RedrawItem()</c>.</summary>
    /// <param name="item">The row, whose id is the tile index of its town.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is the selected one.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public override void RedrawItem(ItemSelectionEntry item, int x, int y, bool current, PixelBuffer output)
    {
        if (!TryFindTown(item.Id, out MapSpecsConditionTownRowData town))
        {
            return;
        }

        _icons.TryGetValue(item.Id, out Sprite? icon);

        RenderRow(
            icon,
            MapSpecsConditionTitles.GetTownTitle(town.Name, town.Race, !town.HasFort, town.TileIndex, MapWidth),
            IconOffsetX, TextOffsetX, x, y, current, output);
    }

    /// <summary>Shows the title of the town the cursor is over, <c>SelectMapCastle::ActionListPressRight()</c>.</summary>
    /// <param name="item">The row the cursor is over.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public override void ActionListPressRight(ItemSelectionEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (TryFindTown(item.Id, out MapSpecsConditionTownRowData town))
        {
            Popup = new DialogPopup(
                string.Empty,
                MapSpecsConditionTitles.GetTownTitle(town.Name, town.Race, !town.HasFort, town.TileIndex, MapWidth));
        }
    }

    /// <summary>Looks the town of a row up by its tile index.</summary>
    /// <param name="tileIndex">Tile index of the row.</param>
    /// <param name="town">The town of that row, when there is one.</param>
    /// <returns>Whether the row belongs to a town.</returns>
    private bool TryFindTown(int tileIndex, out MapSpecsConditionTownRowData town)
    {
        foreach (MapSpecsConditionTownRowData candidate in _towns)
        {
            if (candidate.TileIndex == tileIndex)
            {
                town = candidate;

                return true;
            }
        }

        town = default;

        return false;
    }

    private static List<ItemSelectionEntry> CreateRows(IReadOnlyList<MapSpecsConditionTownRowData>? towns)
    {
        ArgumentNullException.ThrowIfNull(towns);

        List<ItemSelectionEntry> rows = new(towns.Count);

        foreach (MapSpecsConditionTownRowData town in towns)
        {
            rows.Add(new ItemSelectionEntry(town.TileIndex));
        }

        return rows;
    }
}
