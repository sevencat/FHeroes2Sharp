using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.App.Scenes;

/// <summary>What a click on the editor panel hit.</summary>
internal enum EditorPanelHitKind
{
    /// <summary>Nothing: the point is on the bay but not on a control, or outside it.</summary>
    None = 0,

    /// <summary>One of the instrument buttons; the index is the entry of the tool table.</summary>
    Tool,

    /// <summary>One of the nine terrain buttons; the index is the entry of <see cref="EditorPanel.Grounds"/>.</summary>
    Ground,

    /// <summary>One of the object presets; the index is the entry of <see cref="Mp2ObjectPresets.All"/>.</summary>
    Object,

    /// <summary>One of the four brush size buttons; the index is the <see cref="BrushSize"/>.</summary>
    BrushSize,
}

/// <summary>A click on the editor panel: what was hit and which entry of it.</summary>
/// <param name="Kind">The kind of control.</param>
/// <param name="Index">The entry of the control that was hit, meaningless for <see cref="EditorPanelHitKind.None"/>.</param>
internal readonly record struct EditorPanelHit(EditorPanelHitKind Kind, int Index)
{
    /// <summary>A click that hit nothing.</summary>
    public static EditorPanelHit None => default;

    /// <summary>Whether the click hit nothing.</summary>
    public bool IsNone => Kind == EditorPanelHitKind.None;
}

/// <summary>
/// The instrument panel of the map editor, the counterpart of <c>Editor::EditorPanel</c>: three rows of
/// tool buttons, the picker below them and the row of brush size buttons under the picker.
/// </summary>
///
/// <remarks>
/// <para>
/// The reference has nine instruments (terrain, landscape objects, detail, adventure objects, kingdom
/// objects, monsters, stream, road, erase) and six system buttons. This port has three tools, so only
/// the buttons of those three are drawn, each in the cell the reference puts it in — terrain first,
/// landscape objects second, erase last — and the cells of the tools that are not ported stay empty on
/// the bay. The system row is left out: magnify, specs, file and system have no dialog behind them, and
/// undo and redo have no artwork in this release of the game (its <c>EDITBTNS</c> holds 35 frames, the
/// reference expects at least 45).
/// </para>
/// <para>
/// The terrain picker is the one of the reference down to the pixel offsets: nine 27x27 images cut out
/// of <c>EDITPANL.ICN</c> frame 0 at (30 + 29 column, 11 + 29 row), laid out at 36x33 steps, with the
/// 30x30 yellow rectangle of <c>TERRAINS.ICN</c> frame 9 over the selected one. That sheet is a sheet
/// of parts — the nine images, the name band the reference draws on taller displays, a few button
/// faces — and must never be blitted whole: its 29 pixel grid disagrees with the 36 pixel grid the
/// buttons are laid out on, and both sets would show, offset from each other.
/// </para>
/// <para>
/// The brush row is the four size buttons of <c>EDITBTNS</c> frames 24 to 31 at (14 + 30 step, 113) of
/// the instrument area, drawn for the ground and the eraser only. The reference also draws the name of
/// the selected instrument in that row, but only on displays taller than 480; the logical frame of this
/// port is exactly 480, so there is no band and the row belongs to the size buttons, as it does there.
/// </para>
/// <para>
/// The object picker has no artwork to reuse: the nine presets are entries of the map format, not
/// icons. It lists them by name, which is what the panel shows until an object catalogue exists.
/// </para>
/// </remarks>
internal sealed class EditorPanel
{
    /// <summary>File of the instrument buttons, the <c>EDITBTNS</c> of the reference.</summary>
    public const string ButtonsFileName = "EDITBTNS.ICN";

    /// <summary>File of the picker panels, the <c>EDITPANL</c> of the reference.</summary>
    public const string PickerFileName = "EDITPANL.ICN";

    /// <summary>File of the selection rectangle, the <c>TERRAINS</c> of the reference.</summary>
    public const string SelectionFileName = "TERRAINS.ICN";

    /// <summary>
    /// File of the texture the bay is painted with, the <c>STONEBAK</c> the reference passes to
    /// <c>StandardWindow::renderBackgroundImage()</c> in <c>makeInstrumentPanelBackground()</c>.
    /// </summary>
    public const string BackgroundFileName = StandardWindowChrome.BackgroundAssetName;

    /// <summary>The nine grounds of the picker, in the order the reference draws them.</summary>
    public static readonly IReadOnlyList<MapGround> Grounds =
    [
        MapGround.Water,
        MapGround.Grass,
        MapGround.Snow,
        MapGround.Swamp,
        MapGround.Lava,
        MapGround.Desert,
        MapGround.Dirt,
        MapGround.Wasteland,
        MapGround.Beach,
    ];

    /// <summary>Width of one instrument button, <c>EDITBTNS</c> is 48x36 per frame.</summary>
    public const int ButtonWidth = 48;

    /// <summary>Height of one instrument button.</summary>
    public const int ButtonHeight = 36;

    private const int ButtonColumns = 3;
    private const int ButtonRows = 3;

    /// <summary>Height of the three button rows, i.e. where the picker starts inside the bay.</summary>
    public const int PickerTop = ButtonHeight * ButtonRows;

    /// <summary>Height of the picker area, the <c>instrumentPanelHeight</c> of the reference.</summary>
    public const int PickerHeight = 140;

    private const int CellLeft = 23;
    private const int CellTop = 11;
    private const int CellStepX = 36;
    private const int CellStepY = 33;
    private const int CellSize = 27;
    private const int SourceLeft = 30;
    private const int SourceTop = 11;
    private const int SourceStep = 29;

    // The brush size buttons of EDITBTNS are 24x18 (frames 24 to 31, a released and a pressed frame
    // each), laid out at x = 14 + 30 * i and y = 113 of the instrument area, nine pixels under the last
    // cell row and inside the height the reference clamps to (min(height - 27, 135)).
    private const int BrushFrame = 24;
    private const int BrushLeft = 14;
    private const int BrushTop = 113;
    private const int BrushStep = 30;
    private const int BrushWidth = 24;
    private const int BrushHeight = 18;

    private const int RowLeft = 4;
    private const int RowTop = 6;
    private const int RowHeight = 14;

    /// <summary>The instrument buttons, in the cells the reference lays out.</summary>
    private static readonly (EditorTool Tool, int Column, int Row, int Frame)[] Tools =
    [
        (EditorTool.Terrain, 0, 0, 0),
        (EditorTool.Object, 1, 0, 2),
        (EditorTool.Erase, 2, 2, 10),
    ];

    private readonly Rect _area;
    private readonly Sprite[] _buttons;
    private readonly Sprite _picker;
    private readonly Sprite? _selection;
    private readonly Sprite? _background;

    private EditorPanel(Rect area, Sprite[] buttons, Sprite picker, Sprite? selection, Sprite? background)
    {
        _area = area;
        _buttons = buttons;
        _picker = picker;
        _selection = selection;
        _background = background;
    }

    /// <summary>The rectangle the panel draws into, <see cref="EditorScene.PanelArea"/>.</summary>
    public Rect Area => _area;

    /// <summary>Whether the panel paints the stone texture of the bay itself, or leaves it to the scene.</summary>
    public bool HasBackground => _background is not null;

    /// <summary>
    /// Reads the artwork of the panel; <see langword="null"/> when the archive has no <c>EDITBTNS</c>
    /// or no <c>EDITPANL</c>, in which case the editor keeps its text panel.
    /// </summary>
    /// <param name="area">The bay of the frame the panel fills.</param>
    /// <param name="assets">The archive to read from, may be <see langword="null"/>.</param>
    /// <returns>The panel, or <see langword="null"/> when the archive lacks its artwork.</returns>
    public static EditorPanel? TryCreate(in Rect area, AssetBundle? assets)
    {
        if (assets?.TryReadIcn(ButtonsFileName) is not { Count: > 0 } buttons)
        {
            GameLog.Warn($"'{ButtonsFileName}' is not available, the editor panel stays text only.");
            return null;
        }

        if (assets.TryReadIcn(PickerFileName) is not { Count: > 0 } pickers)
        {
            GameLog.Warn($"'{PickerFileName}' is not available, the editor panel stays text only.");
            return null;
        }

        // The yellow rectangle is only decoration: a missing TERRAINS is not worth a text panel.
        Sprite? selection = assets.TryReadIcn(SelectionFileName) is { Sprites.Count: > 9 } terrains
            ? terrains.Sprites[9]
            : null;

        // Same for the texture: without STONEBAK the scene paints the bay flat, the buttons still read.
        Sprite? background = assets.TryReadIcn(BackgroundFileName) is { Count: > 0 } stone
            ? stone.Sprites[0]
            : null;

        return new EditorPanel(area, [.. buttons.Sprites], pickers.Sprites[0], selection, background);
    }

    /// <summary>
    /// Draws the tool buttons, the picker and the brush size row. Text is drawn with
    /// <paramref name="font"/>, the small font of the interface.
    /// </summary>
    /// <param name="target">The frame buffer.</param>
    /// <param name="font">Font of the labels.</param>
    /// <param name="tool">The tool in use, its button is drawn pressed.</param>
    /// <param name="brush">The ground of the terrain tool.</param>
    /// <param name="objectBrush">The preset of the object tool.</param>
    /// <param name="size">The brush size in use, its button is drawn pressed.</param>
    /// <param name="highlight">Palette index of the selection outline, the brightest colour of the frame.</param>
    public void Draw(
        PixelBuffer target,
        IGlyphSource font,
        EditorTool tool,
        MapGround brush,
        Mp2ObjectPreset objectBrush,
        BrushSize size,
        byte highlight)
    {
        if (_background is not null)
        {
            // The reference tiles STONEBAK into the bay from its own top left corner, whatever part of
            // the texture ends up visible; one blit covers the 144x320 bay, so the repeat loops of
            // renderBackgroundImage() have nothing to do here.
            ImageOps.BlitSprite(target, new Point(_area.X, _area.Y), _background);
        }

        foreach ((EditorTool item, int column, int row, int frame) in Tools)
        {
            // The frame pairs of EDITBTNS are (released, pressed): the tool in use shows its pressed
            // frame, the other two their released one.
            int index = frame + (item == tool ? 1 : 0);

            if (index < _buttons.Length)
            {
                ImageOps.BlitSprite(
                    target,
                    new Point(_area.X + (column * ButtonWidth), _area.Y + (row * ButtonHeight)),
                    _buttons[index]);
            }
        }

        switch (tool)
        {
            case EditorTool.Terrain:
                DrawTerrainPicker(target, brush);
                DrawBrushRow(target, size);
                break;

            case EditorTool.Object:
                DrawObjectPicker(target, font, objectBrush, highlight);
                break;

            default:
                DrawEraseHint(target, font);
                DrawBrushRow(target, size);
                break;
        }
    }

    /// <summary>
    /// Blits the four size buttons of the brush, the selected one pressed. The reference draws them for
    /// the instruments that paint (ground, landscape objects, detail) and for the eraser, which is the
    /// same set this port has.
    /// </summary>
    /// <param name="target">The frame buffer.</param>
    /// <param name="size">The size in use.</param>
    private void DrawBrushRow(PixelBuffer target, BrushSize size)
    {
        IReadOnlyList<BrushSize> sizes = BrushSizeExtensions.All;

        for (int i = 0; i < sizes.Count; i++)
        {
            int index = BrushFrame + (i * 2) + (sizes[i] == size ? 1 : 0);

            if (index < _buttons.Length)
            {
                ImageOps.BlitSprite(
                    target,
                    new Point(_area.X + BrushLeft + (i * BrushStep), _area.Y + PickerTop + BrushTop),
                    _buttons[index]);
            }
        }
    }

    /// <summary>
    /// Reports which control the point of the bay is over, in the coordinates of the frame.
    /// </summary>
    /// <param name="screen">Pointer in logical pixels.</param>
    /// <param name="tool">The tool in use, the picker of the bay depends on it.</param>
    /// <returns>What the point hits, <see cref="EditorPanelHit.None"/> when nothing.</returns>
    public EditorPanelHit HitTest(Point screen, EditorTool tool)
    {
        if (!_area.Contains(screen))
        {
            return EditorPanelHit.None;
        }

        Point local = new(screen.X - _area.X, screen.Y - _area.Y);

        for (int i = 0; i < Tools.Length; i++)
        {
            (EditorTool _, int column, int row, int _) = Tools[i];

            if (new Rect(column * ButtonWidth, row * ButtonHeight, ButtonWidth, ButtonHeight).Contains(local))
            {
                return new EditorPanelHit(EditorPanelHitKind.Tool, i);
            }
        }

        Point picker = new(local.X, local.Y - PickerTop);

        if (!new Rect(0, 0, _area.Width, PickerHeight).Contains(picker))
        {
            return EditorPanelHit.None;
        }

        switch (tool)
        {
            case EditorTool.Terrain:
                for (int i = 0; i < Grounds.Count; i++)
                {
                    if (Cell(i).Contains(picker))
                    {
                        return new EditorPanelHit(EditorPanelHitKind.Ground, i);
                    }
                }

                break;

            case EditorTool.Object:
                for (int i = 0; i < Mp2ObjectPresets.Count; i++)
                {
                    if (Row(i).Contains(picker))
                    {
                        return new EditorPanelHit(EditorPanelHitKind.Object, i);
                    }
                }

                break;

            case EditorTool.Erase:
            default:
                break;
        }

        // The brush row comes last: the nine rows of the object picker reach down to it, and the
        // object tool has no brush sizes to pick (an object is as big as its own artwork).
        if (tool is EditorTool.Terrain or EditorTool.Erase)
        {
            for (int i = 0; i < BrushSizeExtensions.All.Count; i++)
            {
                if (BrushButton(i).Contains(picker))
                {
                    return new EditorPanelHit(EditorPanelHitKind.BrushSize, i);
                }
            }
        }

        return EditorPanelHit.None;
    }

    /// <summary>The tool of the instrument button at <paramref name="index"/> of the tool table.</summary>
    /// <param name="index">Entry of the tool table.</param>
    /// <returns>The tool the button selects.</returns>
    public static EditorTool ToolAt(int index) => Tools[index].Tool;

    /// <summary>
    /// Copies a part of the sheet to the frame. <see cref="ImageOps.BlitSprite(PixelBuffer, Point,
    /// Sprite, in Rect)"/> adds the source rectangle to the destination point, so the point is biased
    /// by it to make <paramref name="position"/> the top left corner of the part.
    /// </summary>
    /// <param name="target">The frame buffer.</param>
    /// <param name="position">Where the top left corner of the part goes.</param>
    /// <param name="source">The part of <see cref="_picker"/> to copy.</param>
    private void BlitPart(PixelBuffer target, Point position, in Rect source) =>
        ImageOps.BlitSprite(
            target,
            new Point(position.X - source.X, position.Y - source.Y),
            _picker,
            source);

    /// <summary>
    /// Cell of a terrain button inside the picker: the 27x27 image, so the gap between the cells is
    /// dead space exactly as it is in the reference (<c>_terrainButtonsRect</c> is 27x27 as well).
    /// </summary>
    private static Rect Cell(int index) => new(
        CellLeft + ((index % ButtonColumns) * CellStepX),
        CellTop + ((index / ButtonColumns) * CellStepY),
        CellSize,
        CellSize);

    /// <summary>Row of an object preset inside the picker.</summary>
    private static Rect Row(int index) =>
        new(RowLeft, RowTop + (index * RowHeight), RowWidth, RowHeight);

    /// <summary>Rectangle of one brush size button inside the picker, the 24x18 artwork itself.</summary>
    private static Rect BrushButton(int index) =>
        new(BrushLeft + (index * BrushStep), BrushTop, BrushWidth, BrushHeight);

    private static int RowWidth => 136;

    private void DrawTerrainPicker(PixelBuffer target, MapGround brush)
    {
        int selected = -1;

        for (int i = 0; i < Grounds.Count; i++)
        {
            Rect cell = Cell(i);

            // The images of EDITPANL.ICN frame 0 are 29 pixels apart and 27 pixels wide, which leaves
            // the gap the reference uses for its cell borders.
            BlitPart(
                target,
                new Point(_area.X + cell.X, _area.Y + PickerTop + cell.Y),
                new Rect(
                    SourceLeft + ((i % ButtonColumns) * SourceStep),
                    SourceTop + ((i / ButtonColumns) * SourceStep),
                    CellSize,
                    CellSize));

            if (Grounds[i] == brush)
            {
                selected = i;
            }
        }

        if (selected >= 0 && _selection is not null)
        {
            Rect cell = Cell(selected);

            // The rectangle is 30x30 and the reference centres it on a 27x27 image, i.e. at -2.
            ImageOps.BlitSprite(
                target,
                new Point(_area.X + cell.X - 2, _area.Y + PickerTop + cell.Y - 2),
                _selection);
        }
    }

    private void DrawObjectPicker(PixelBuffer target, IGlyphSource font, Mp2ObjectPreset brush, byte highlight)
    {
        IReadOnlyList<Mp2ObjectPreset> presets = Mp2ObjectPresets.All;
        int selected = Mp2ObjectPresets.IndexOf(brush);

        for (int i = 0; i < presets.Count; i++)
        {
            Rect row = Row(i);

            // The rows have no artwork behind them — the presets are entries of the map format, not
            // icons — so the selection is marked with the outline of the frame instead of the yellow
            // rectangle of TERRAINS.ICN, which is 30x30 and does not fit a 136x14 row.
            if (i == selected)
            {
                ImageOps.DrawRect(
                    target,
                    new Rect(_area.X + row.X, _area.Y + PickerTop + row.Y, row.Width, row.Height),
                    highlight);
            }

            Point position = new(_area.X + row.X + 3, _area.Y + PickerTop + row.Y + 2);

            GlyphText.DrawLine(
                font,
                target,
                position,
                FormattableString.Invariant($"{i + 1} {presets[i].Name}"),
                new Rect(position.X, position.Y, RowWidth, RowHeight));
        }

    }

    private void DrawEraseHint(PixelBuffer target, IGlyphSource font)
    {
        Point position = new(_area.X + BrushLeft, _area.Y + PickerTop + 16);

        GlyphText.DrawLine(font, target, position, "click to clear the");

        GlyphText.DrawLine(
            font,
            target,
            new Point(position.X, position.Y + font.LineHeight),
            "objects under it");
    }
}
