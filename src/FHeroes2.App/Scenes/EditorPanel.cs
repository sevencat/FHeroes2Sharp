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

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;

/// <summary>What a click on the editor panel hit.</summary>
internal enum EditorPanelHitKind
{
    /// <summary>Nothing: the point is on the bay but not on a control, or outside it.</summary>
    None = 0,

    /// <summary>One of the instrument buttons; the index is the entry of the tool table.</summary>
    Tool,

    /// <summary>One of the nine terrain buttons; the index is the entry of <see cref="EditorPanel.Grounds"/>.</summary>
    Ground,

    /// <summary>One of the brush kinds of the current instrument; the index is the entry of <see cref="EditorPanelState.BrushKinds"/>.</summary>
    BrushKind,

    /// <summary>One of the objects of the picked brush kind; the index is the entry of <see cref="EditorPanelState.BrushObjects"/>.</summary>
    Object,

    /// <summary>One of the four brush size buttons; the index is the <see cref="BrushSize"/>.</summary>
    BrushSize,

    /// <summary>
    /// One of the eleven cells of the erase page; the index is the entry of <see cref="EditorEraseTypes.Buttons"/>.
    /// </summary>
    EraseType,

    /// <summary>One of the three cells of the detail page; the index is the entry of <see cref="EditorDetailModes.All"/>.</summary>
    DetailMode,

    /// <summary>One of the six system buttons; the index is the entry of <see cref="EditorSystemButton"/>.</summary>
    System,
}

/// <summary>The six buttons of the bottom rows of the panel, in the order the reference draws them.</summary>
internal enum EditorSystemButton
{
    /// <summary>The magnifying glass, which zooms the map of the reference.</summary>
    Magnify = 0,

    /// <summary>Undo the last edit.</summary>
    Undo,

    /// <summary>Redo the last undone edit.</summary>
    Redo,

    /// <summary>The scenario specifications dialog.</summary>
    Specs,

    /// <summary>The save / load dialog.</summary>
    File,

    /// <summary>The game menu.</summary>
    System,
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
/// What the panel draws and what it accepts a click on: the state of the editor the bay mirrors.
/// </summary>
/// <param name="Tool">The instrument in use, which the pressed button and the picker follow.</param>
/// <param name="Brush">The ground of the terrain brush.</param>
/// <param name="BrushSize">The size of the terrain and eraser brushes.</param>
/// <param name="BrushKinds">The brush kinds the instrument offers, empty for the instruments that place no object.</param>
/// <param name="BrushKind">The picked brush kind, <c>-1</c> while the kind page is still open.</param>
/// <param name="BrushObjects">The objects of the picked kind, empty while no kind is picked.</param>
/// <param name="BrushObject">The picked object, <c>-1</c> while the object page is still open.</param>
/// <param name="EraseTypes">The types the eraser deletes, the checkboxes of its page.</param>
/// <param name="DetailMode">What a click of the detail instrument does, the three buttons of its page.</param>
internal readonly record struct EditorPanelState(
    EditorTool Tool,
    MapGround Brush,
    BrushSize BrushSize,
    IReadOnlyList<ObjectGroup> BrushKinds,
    int BrushKind,
    IReadOnlyList<Mp2ObjectPreset> BrushObjects,
    int BrushObject,
    EditorEraseType EraseTypes,
    EditorDetailMode DetailMode);

/// <summary>
/// The instrument panel of the map editor, the counterpart of <c>Editor::EditorPanel</c>: three rows of
/// tool buttons, the picker below them, the row of brush sizes under the picker and the two rows of
/// system buttons at the bottom.
/// </summary>
///
/// <remarks>
/// <para>
/// The reference has nine instruments (terrain, landscape objects, detail, adventure objects, kingdom
/// objects, monsters, stream, road, erase), and all nine buttons are drawn in their cells, with the
/// artwork the reference assembles for the five the game does not ship (<see cref="EditorButtonSet"/>).
/// Every cell selects its instrument; what the page below shows is the instrument's own: the terrain
/// grid, the two level object page of the four object instruments, the eleven erase types, or the two
/// lines of a hint for the detail brush and the two that lay a path.
/// </para>
/// <para>
/// The picker is the one of the reference down to the pixel offsets: nine 27x27 images cut out of
/// <c>EDITPANL.ICN</c> frame 0 at (30 + 29 column, 11 + 29 row), laid out at 36x33 steps, with the
/// 30x30 yellow rectangle of <c>TERRAINS.ICN</c> frame 9 over the selected one. That sheet is a sheet
/// of parts — the nine images, the name band the reference draws on taller displays, a few button
/// faces — and must never be blitted whole: its 29 pixel grid disagrees with the 36 pixel grid the
/// buttons are laid out on, and both sets would show, offset from each other.
/// </para>
/// <para>
/// The picker is 140 pixels tall on the 480 pixel display the editor runs at: the reference computes
/// it as <c>instrumentPanelHeight = displayHeight - displayY - 5 * buttonHeight</c>, i.e. what is left
/// of the bay between the three button rows and the two rows of system buttons. The brush row sits at
/// y = 113 of it, <c>min(instrumentPanelHeight - 27, 135)</c>, and the name of the selected instrument
/// is only drawn on displays taller than 480, so there is no band here and the row belongs to the size
/// buttons, as it does there.
/// </para>
/// <para>
/// The two level object page of the reference is a set of icons; this port has none, so it lists the
/// groups and then the objects of the picked group by name — the names are entries of the map format,
/// not pictures, and a name is enough to tell "a mine" from "a treasure".
/// </para>
/// </remarks>
internal sealed class EditorPanel
{
    /// <summary>File of the instrument buttons, the <c>EDITBTNS</c> of the reference.</summary>
    public const string ButtonsFileName = EditorButtonSet.AssetName;

    /// <summary>File of the picker panels, the <c>EDITPANL</c> of the reference.</summary>
    public const string PickerFileName = "EDITPANL.ICN";

    /// <summary>File of the selection rectangle, the <c>TERRAINS</c> of the reference.</summary>
    public const string SelectionFileName = "TERRAINS.ICN";

    /// <summary>
    /// File of the texture the bay is painted with, the <c>STONEBAK</c> the reference passes to
    /// <c>StandardWindow::renderBackgroundImage()</c> in <c>makeInstrumentPanelBackground()</c>.
    /// </summary>
    public const string BackgroundFileName = "STONEBAK.ICN";

    /// <summary>
    /// File the check mark of a selected erase type comes from, the <c>TOWNWIND</c> frame 11 the reference blits over
    /// a checked cell (<c>editor_interface_panel.cpp:628</c>).
    /// </summary>
    public const string EraseMarkFileName = "TOWNWIND.ICN";

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
    public const int ButtonWidth = EditorButtonSet.ButtonWidth;

    /// <summary>Height of one instrument button.</summary>
    public const int ButtonHeight = EditorButtonSet.ButtonHeight;

    private const int ButtonColumns = 3;
    private const int ButtonRows = 3;

    /// <summary>Height of the three button rows, i.e. where the picker starts inside the bay.</summary>
    public const int PickerTop = ButtonHeight * ButtonRows;

    /// <summary>Height of the two rows of system buttons.</summary>
    public const int SystemHeight = ButtonHeight * 2;

    /// <summary>
    /// Height of the picker area, the <c>instrumentPanelHeight</c> of the reference: what is left of
    /// the bay below the three button rows and above the two system rows. On the 480 pixel display the
    /// editor runs at that is 320 - 108 - 72 = 140 pixels, and the bay is exactly filled.
    /// </summary>
    public int PickerHeight => _area.Height - PickerTop - SystemHeight;

    private const int CellLeft = 23;
    private const int CellTop = 11;
    private const int CellStepX = 36;
    private const int CellStepY = 33;
    private const int CellSize = 27;
    private const int SourceLeft = 30;
    private const int SourceTop = 11;
    private const int SourceStep = 29;

    // The brush size buttons of EDITBTNS are 24x18 (frames 24 to 31, a released and a pressed frame
    // each), laid out at x = 14 + 30 * i and y = min(instrumentPanelHeight - 27, 135) of the picker.
    private const int BrushLeft = 14;
    private const int BrushStep = 30;

    private int BrushTop => Math.Min(PickerHeight - 27, 135);

    // The erase page: eleven 27x27 icons four to a row with a 33 pixel step, a 30 pixel row step and the
    // last row shifted right by half a step, i.e. the geometry of the reference's _eraseButtonsRect
    // (editor_interface_panel.cpp:436-447, the steps of a 480 pixel display).
    private const int EraseLeft = 9;
    private const int EraseTop = 24;
    private const int EraseStepX = 33;
    private const int EraseStepY = 30;
    private const int EraseCellSize = 27;
    private const int EraseColumns = 4;

    /// <summary>
    /// The icons of the erase page, in the order of <see cref="EditorEraseTypes.Buttons"/>: <c>EDITPANL</c> frames 6,
    /// 7, 8, 10, 13, 16 and 17, and four 27x27 crops of frame 1 for the towns, the pickable adventure objects, the
    /// monsters and the heroes (<c>editor_interface_panel.cpp:600-627</c>). A crop of <c>(0, 0)</c> means the frame is
    /// the icon itself.
    /// </summary>
    private static readonly (int Frame, int CropX, int CropY)[] EraseIconSources =
    [
        (6, 0, 0),
        (7, 0, 0),
        (8, 0, 0),
        (10, 0, 0),
        (13, 0, 0),
        (1, 44, 68),
        (1, 102, 96),
        (1, 73, 68),
        (1, 102, 68),
        (16, 0, 0),
        (17, 0, 0),
    ];

    private const int RowLeft = 6;
    private const int RowTop = 4;
    private const int RowHeight = 13;

    /// <summary>
    /// The nine instrument buttons in the cells the reference lays them out in, in the reference's own order
    /// (<c>editor/editor_interface_panel.h:171-186</c>): the order is the order of the artwork, so a cell and the
    /// instrument it selects must not be reordered independently.
    /// </summary>
    private static readonly (EditorTool Tool, int Column, int Row, int Frame)[] Tools =
    [
        (EditorTool.Terrain, 0, 0, EditorButtonSet.TerrainFrame),
        (EditorTool.LandscapeObjects, 1, 0, EditorButtonSet.LandscapeObjectsFrame),
        (EditorTool.Detail, 2, 0, EditorButtonSet.DetailFrame),
        (EditorTool.AdventureObjects, 0, 1, EditorButtonSet.AdventureObjectsFrame),
        (EditorTool.KingdomObjects, 1, 1, EditorButtonSet.KingdomObjectsFrame),
        (EditorTool.Monsters, 2, 1, EditorButtonSet.MonstersFrame),
        (EditorTool.Stream, 0, 2, EditorButtonSet.StreamFrame),
        (EditorTool.Road, 1, 2, EditorButtonSet.RoadFrame),
        (EditorTool.Erase, 2, 2, EditorButtonSet.EraseFrame),
    ];

    /// <summary>The six system buttons, three per row, at the bottom of the bay.</summary>
    private static readonly (EditorSystemButton Button, int Column, int Row, int Frame)[] SystemButtons =
    [
        (EditorSystemButton.Magnify, 0, 0, EditorButtonSet.MagnifyFrame),
        (EditorSystemButton.Undo, 1, 0, EditorButtonSet.UndoFrame),
        (EditorSystemButton.Redo, 2, 0, EditorButtonSet.RedoFrame),
        (EditorSystemButton.Specs, 0, 1, EditorButtonSet.SpecsFrame),
        (EditorSystemButton.File, 1, 1, EditorButtonSet.FileFrame),
        (EditorSystemButton.System, 2, 1, EditorButtonSet.SystemFrame),
    ];

    private readonly Rect _area;
    private readonly EditorButtonSet _buttons;
    private readonly Sprite _picker;
    private readonly Sprite? _selection;
    private readonly Sprite? _background;
    private readonly IReadOnlyList<(Sprite Sprite, Rect Source)?> _eraseIcons;
    private readonly Sprite? _eraseMark;
    private readonly IReadOnlyList<(Sprite Sprite, Rect Source)?> _detailIcons;

    private EditorPanel(
        Rect area,
        EditorButtonSet buttons,
        Sprite picker,
        Sprite? selection,
        Sprite? background,
        IReadOnlyList<(Sprite Sprite, Rect Source)?> eraseIcons,
        Sprite? eraseMark,
        IReadOnlyList<(Sprite Sprite, Rect Source)?> detailIcons)
    {
        _area = area;
        _buttons = buttons;
        _picker = picker;
        _selection = selection;
        _background = background;
        _eraseIcons = eraseIcons;
        _eraseMark = eraseMark;
        _detailIcons = detailIcons;
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
        if (EditorButtonSet.TryCreate(assets) is not { } buttons)
        {
            GameLog.Warn($"'{ButtonsFileName}' is not available, the editor panel stays text only.");
            return null;
        }

        if (assets?.TryReadIcn(PickerFileName) is not { Count: > 0 } pickers)
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

        // The mark the reference blits over a checked erase type, TOWNWIND frame 11. Without it the checked cells are
        // outlined instead, which reads the same on the stone texture.
        Sprite? mark = assets.TryReadIcn(EraseMarkFileName) is { Sprites.Count: > 11 } wind
            ? wind.Sprites[11]
            : null;

        return new EditorPanel(
            area,
            buttons,
            pickers.Sprites[0],
            selection,
            background,
            CreateEraseIcons(pickers.Sprites),
            mark,
            CreatePickedIcons(pickers.Sprites, DetailModeFrames, CellSize));
    }

    /// <summary>
    /// The frames of <c>EDITPANL</c> the three detail modes are drawn from, in the order of
    /// <see cref="EditorDetailModes.All"/>: the edit, the move and the copy icon, the
    /// <c>i + 18</c> of <c>editor_interface_panel.cpp:525</c>.
    /// </summary>
    private static readonly int[] DetailModeFrames = [18, 19, 20];

    /// <summary>
    /// Reads whole frames of <c>EDITPANL</c>: the icons of the pages the reference draws straight
    /// from the sheet. A frame the archive does not have stays <see langword="null"/>, so a partial
    /// sheet still gives a usable page.
    /// </summary>
    /// <param name="pickers">The frames of <c>EDITPANL</c>.</param>
    /// <param name="frames">The frames to read, in the order of the page.</param>
    /// <param name="size">Side of the cell the frame must fit into.</param>
    /// <returns>One entry per frame, in the same order.</returns>
    private static IReadOnlyList<(Sprite Sprite, Rect Source)?> CreatePickedIcons(
        IReadOnlyList<Sprite> pickers,
        IReadOnlyList<int> frames,
        int size)
    {
        (Sprite Sprite, Rect Source)?[] icons = new (Sprite Sprite, Rect Source)?[frames.Count];

        for (int i = 0; i < frames.Count; ++i)
        {
            if (frames[i] < 0 || frames[i] >= pickers.Count)
            {
                continue;
            }

            Sprite sprite = pickers[frames[i]];
            Rect source = sprite.Image.Bounds;

            if (source.Width == size && source.Height == size)
            {
                icons[i] = (sprite, source);
            }
        }

        return icons;
    }

    /// <summary>
    /// Reads the eleven icons of the erase page out of <c>EDITPANL</c>. An entry the archive cannot provide stays
    /// <see langword="null"/> and that cell is drawn empty, so a partial sheet still gives a usable page.
    /// </summary>
    /// <param name="pickers">The frames of <c>EDITPANL</c>.</param>
    /// <returns>One entry per <see cref="EditorEraseTypes.Buttons"/>, in the same order.</returns>
    private static IReadOnlyList<(Sprite Sprite, Rect Source)?> CreateEraseIcons(IReadOnlyList<Sprite> pickers)
    {
        (Sprite Sprite, Rect Source)?[] icons = new (Sprite Sprite, Rect Source)?[EraseIconSources.Length];

        for (int i = 0; i < EraseIconSources.Length; ++i)
        {
            (int frame, int cropX, int cropY) = EraseIconSources[i];

            if (frame < 0 || frame >= pickers.Count)
            {
                continue;
            }

            Sprite sprite = pickers[frame];

            Rect source = cropX == 0 && cropY == 0
                ? sprite.Image.Bounds
                : new Rect(cropX, cropY, EraseCellSize, EraseCellSize);

            if (source.Right <= sprite.Width && source.Bottom <= sprite.Height)
            {
                icons[i] = (sprite, source);
            }
        }

        return icons;
    }

    /// <summary>
    /// Draws the tool buttons, the picker, the brush size row and the system buttons. Text is drawn
    /// with <paramref name="font"/>, the small font of the interface.
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
        EditorPanelState state,
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
            // frame, the others their released one.
            int index = frame + (item == state.Tool ? 1 : 0);

            if (_buttons[index] is { } button)
            {
                ImageOps.BlitSprite(
                    target,
                    new Point(_area.X + (column * ButtonWidth), _area.Y + (row * ButtonHeight)),
                    button);
            }
        }

        DrawPickerBackground(target);

        switch (state.Tool)
        {
            case EditorTool.Terrain:
                DrawTerrainPicker(target, state.Brush);
                DrawBrushRow(target, state.BrushSize);
                break;

            case EditorTool.LandscapeObjects or EditorTool.AdventureObjects or EditorTool.KingdomObjects
                or EditorTool.Monsters:
                // 两级页面照参考：先选「哪一类」，页面里才有物件。类别还没选时列类别，选了就列物件。
                if (state.BrushKind < 0)
                {
                    DrawKindPicker(target, font, state, highlight);
                }
                else
                {
                    DrawObjectPicker(target, font, state, highlight);
                }

                break;

            case EditorTool.Erase:
                DrawErasePicker(target, font, state, highlight);
                DrawBrushRow(target, state.BrushSize);
                break;

            case EditorTool.Detail:
                DrawDetailPicker(target, font, state, highlight);
                DrawBrushRow(target, state.BrushSize);
                break;

            default:
                DrawToolHint(target, font, state.Tool);
                DrawBrushRow(target, state.BrushSize);
                break;
        }

        DrawSystemButtons(target);
    }

    /// <summary>
    /// Paints the picker area: the stone texture again, this time with the six concentric bevels of
    /// <c>makeInstrumentPanelBackground()</c>.
    /// </summary>
    /// <remarks>
    /// The reference builds a 144x140 image, tiles STONEBAK into it and then applies six palette
    /// tables to ever smaller frames, which is what gives the area its sunken look. The bevels are
    /// four one pixel edges per pass, applied top and bottom over the full width and left and right
    /// without the corners.
    /// </remarks>
    /// <param name="target">The frame buffer.</param>
    private void DrawPickerBackground(PixelBuffer target)
    {
        Rect picker = new(_area.X, _area.Y + PickerTop, _area.Width, PickerHeight);

        if (_background is not null)
        {
            ImageOps.BlitSprite(target, picker.Position, _background);
        }

        // offset 0 is the outer edge, then the next three passes walk inwards; the fourth offset is
        // used twice, with different tables.
        ReadOnlySpan<(int Offset, int Transform)> bevels =
        [
            (0, 2),
            (1, 4),
            (2, 5),
            (2, 9),
            (3, 8),
            (4, 9),
        ];

        foreach ((int offset, int transform) in bevels)
        {
            ApplyBevel(target, picker, offset, transform);
        }
    }

    /// <summary>Applies <paramref name="transform"/> to the one pixel frame of <paramref name="area"/>.</summary>
    private static void ApplyBevel(PixelBuffer target, in Rect area, int offset, int transform)
    {
        if (area.Width <= (offset * 2) || area.Height <= (offset * 2))
        {
            return;
        }

        ImageOps.ApplyTransform(
            target,
            new Rect(area.X + offset, area.Y + offset, area.Width - (offset * 2), 1),
            transform);
        ImageOps.ApplyTransform(
            target,
            new Rect(area.X + offset, area.Bottom - 1 - offset, area.Width - (offset * 2), 1),
            transform);
        ImageOps.ApplyTransform(
            target,
            new Rect(area.X + offset, area.Y + 1 + offset, 1, area.Height - 2 - (offset * 2)),
            transform);
        ImageOps.ApplyTransform(
            target,
            new Rect(area.Right - 1 - offset, area.Y + 1 + offset, 1, area.Height - 2 - (offset * 2)),
            transform);
    }

    /// <summary>Blits the six system buttons, three per row, at the bottom of the bay.</summary>
    /// <param name="target">The frame buffer.</param>
    private void DrawSystemButtons(PixelBuffer target)
    {
        foreach ((EditorSystemButton _, int column, int row, int frame) in SystemButtons)
        {
            if (_buttons[frame] is { } button)
            {
                ImageOps.BlitSprite(
                    target,
                    new Point(
                        _area.X + (column * ButtonWidth),
                        _area.Y + PickerTop + PickerHeight + (row * ButtonHeight)),
                    button);
            }
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
            int index = EditorButtonSet.BrushFrame + (i * 2) + (sizes[i] == size ? 1 : 0);

            if (_buttons[index] is { } button)
            {
                ImageOps.BlitSprite(
                    target,
                    new Point(_area.X + BrushLeft + (i * BrushStep), _area.Y + PickerTop + BrushTop),
                    button);
            }
        }
    }

    /// <summary>
    /// Reports which control the point of the bay is over, in the coordinates of the frame.
    /// </summary>
    /// <param name="screen">Pointer in logical pixels.</param>
    /// <param name="tool">The tool in use, the picker of the bay depends on it.</param>
    /// <returns>What the point hits, <see cref="EditorPanelHit.None"/> when nothing.</returns>
    public EditorPanelHit HitTest(Point screen, EditorPanelState state)
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

        for (int i = 0; i < SystemButtons.Length; i++)
        {
            (EditorSystemButton _, int column, int row, int _) = SystemButtons[i];

            if (new Rect(
                    column * ButtonWidth,
                    PickerTop + PickerHeight + (row * ButtonHeight),
                    ButtonWidth,
                    ButtonHeight).Contains(local))
            {
                return new EditorPanelHit(EditorPanelHitKind.System, i);
            }
        }

        Point picker = new(local.X, local.Y - PickerTop);

        if (!new Rect(0, 0, _area.Width, PickerHeight).Contains(picker))
        {
            return EditorPanelHit.None;
        }

        switch (state.Tool)
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

            case EditorTool.LandscapeObjects or EditorTool.AdventureObjects or EditorTool.KingdomObjects
                or EditorTool.Monsters:
                // 类别页与物件页共用同一批行：先有类别、后有物件，所以两张表不会同时被点到。
                if (state.BrushKind < 0)
                {
                    for (int i = 0; i < state.BrushKinds.Count; i++)
                    {
                        if (Row(i).Contains(picker))
                        {
                            return new EditorPanelHit(EditorPanelHitKind.BrushKind, i);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < state.BrushObjects.Count; i++)
                    {
                        if (Row(i).Contains(picker))
                        {
                            return new EditorPanelHit(EditorPanelHitKind.Object, i);
                        }
                    }
                }

                break;

            case EditorTool.Erase:
                for (int i = 0; i < EditorEraseTypes.Buttons.Count; ++i)
                {
                    if (EraseCell(i).Contains(picker))
                    {
                        return new EditorPanelHit(EditorPanelHitKind.EraseType, i);
                    }
                }

                break;

            case EditorTool.Detail:
                for (int i = 0; i < EditorDetailModes.All.Count; ++i)
                {
                    if ((HasDetailIcons ? Cell(i) : Row(i)).Contains(picker))
                    {
                        return new EditorPanelHit(EditorPanelHitKind.DetailMode, i);
                    }
                }

                break;

            default:
                break;
        }

        // The brush row comes last: the nine rows of the object picker reach down to it, and the
        // object instruments have no brush sizes to pick (an object is as big as its own artwork).
        if (state.Tool is EditorTool.Terrain or EditorTool.Erase or EditorTool.Detail
            or EditorTool.Stream or EditorTool.Road)
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

    /// <summary>
    /// The instrument the button at <paramref name="index"/> of the tool table selects.
    /// </summary>
    /// <param name="index">Entry of the tool table.</param>
    /// <returns>The instrument of that cell.</returns>
    public static EditorTool ToolAt(int index) => Tools[index].Tool;

    /// <summary>The system button at <paramref name="index"/> of the system table.</summary>
    /// <param name="index">Entry of the system table.</param>
    /// <returns>The button the cell shows.</returns>
    public static EditorSystemButton SystemAt(int index) => SystemButtons[index].Button;

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
    private Rect BrushButton(int index) =>
        new(BrushLeft + (index * BrushStep), BrushTop, EditorButtonSet.BrushWidth, EditorButtonSet.BrushHeight);

    private static int RowWidth => 132;

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

    private void DrawObjectPicker(PixelBuffer target, IGlyphSource font, EditorPanelState state, byte highlight)
    {
        IReadOnlyList<Mp2ObjectPreset> presets = state.BrushObjects;

        for (int i = 0; i < presets.Count; i++)
        {
            Rect row = Row(i);

            // The rows have no artwork behind them — the presets are entries of the map format, not
            // icons — so the selection is marked with the outline of the frame instead of the yellow
            // rectangle of TERRAINS.ICN, which is 30x30 and does not fit a 132x13 row.
            if (i == state.BrushObject)
            {
                ImageOps.DrawRect(
                    target,
                    new Rect(_area.X + row.X, _area.Y + PickerTop + row.Y, row.Width, row.Height),
                    highlight);
            }

            Point position = new(_area.X + row.X + 3, _area.Y + PickerTop + row.Y + 1);

            GlyphText.DrawLine(
                font,
                target,
                position,
                FormattableString.Invariant($"{i + 1} {presets[i].Name}"),
                new Rect(position.X, position.Y, RowWidth, RowHeight));
        }
    }

    /// <summary>
    /// Paints the page of the brush kinds, the first level of an object instrument.
    /// </summary>
    /// <remarks>
    /// 参考那一页用的是一组图标（山、石头、树……）；端口没有那套图，就用分组名当条目——名字本身足以让作者认出
    /// 「这一页是地形物件还是冒险物件」，而把资源留给真正需要图像的地方（地形格、物件精灵）。
    /// </remarks>
    private void DrawKindPicker(PixelBuffer target, IGlyphSource font, EditorPanelState state, byte highlight)
    {
        for (int i = 0; i < state.BrushKinds.Count; i++)
        {
            Rect row = Row(i);

            if (i == state.BrushKind)
            {
                ImageOps.DrawRect(
                    target,
                    new Rect(_area.X + row.X, _area.Y + PickerTop + row.Y, row.Width, row.Height),
                    highlight);
            }

            Point position = new(_area.X + row.X + 3, _area.Y + PickerTop + row.Y + 1);

            GlyphText.DrawLine(
                font,
                target,
                position,
                FormattableString.Invariant($"{i + 1} {KindName(state.BrushKinds[i])}"),
                new Rect(position.X, position.Y, RowWidth, RowHeight));
        }
    }

    /// <summary>A group's name without the <c>LANDSCAPE_</c> / <c>ADVENTURE_</c> prefix, for the brush page.</summary>
    /// <param name="group">The catalogue group.</param>
    /// <returns>The short name the panel shows.</returns>
    private static string KindName(ObjectGroup group)
    {
        string name = group.ToString();

        int separator = name.IndexOf('_', StringComparison.Ordinal);

        return separator >= 0 && separator + 1 < name.Length ? name[(separator + 1)..] : name;
    }

    /// <summary>
    /// Draws the erase page: the eleven object types the eraser can delete, a checked one marked the way the reference
    /// marks it, plus the size row the reference shows for this instrument too.
    /// </summary>
    /// <remarks>
    /// 十一个图标就是参考的十一个按钮（见 <see cref="EraseIconSources"/>），勾中的那一格再盖一个 TOWNWIND 第 11 帧的勾。
    /// 整张表都缺图时退回两行说明文字，页面不至于空白——那正是没有 <c>EDITPANL</c> 时的情况，只是那时连面板都不存在。
    /// </remarks>
    /// <param name="target">The frame buffer.</param>
    /// <param name="font">Font of the fallback hint.</param>
    /// <param name="state">The editor state, whose <see cref="EditorPanelState.EraseTypes"/> says which cells are checked.</param>
    /// <param name="highlight">Palette index of the outline a checked cell gets when the check mark is missing.</param>
    private void DrawErasePicker(PixelBuffer target, IGlyphSource font, EditorPanelState state, byte highlight)
    {
        bool drawn = false;

        for (int i = 0; i < EditorEraseTypes.Buttons.Count; ++i)
        {
            Rect cell = EraseCell(i);

            if (i < _eraseIcons.Count && _eraseIcons[i] is { } icon)
            {
                // BlitSprite() adds the source rectangle to the destination point, so the point is biased by it to
                // make the cell the top left corner of the icon.
                ImageOps.BlitSprite(
                    target,
                    new Point(
                        _area.X + cell.X - icon.Source.X,
                        _area.Y + PickerTop + cell.Y - icon.Source.Y),
                    icon.Sprite,
                    icon.Source);

                drawn = true;
            }

            if ((state.EraseTypes & EditorEraseTypes.Buttons[i]) == 0)
            {
                continue;
            }

            Point mark = new(_area.X + cell.X + 10, _area.Y + PickerTop + cell.Y + 11);

            if (_eraseMark is not null)
            {
                ImageOps.BlitSprite(target, mark, _eraseMark);
            }
            else
            {
                ImageOps.DrawRect(
                    target,
                    new Rect(_area.X + cell.X, _area.Y + PickerTop + cell.Y, cell.Width, cell.Height),
                    highlight);
            }
        }

        if (!drawn)
        {
            DrawToolHint(target, font, EditorTool.Erase);
        }
    }

    /// <summary>Cell of one erase type inside the picker, the 27x27 icon of the reference.</summary>
    /// <param name="index">Entry of <see cref="EditorEraseTypes.Buttons"/>.</param>
    /// <returns>The rectangle in picker coordinates.</returns>
    private static Rect EraseCell(int index) => new(
        EraseLeft + ((index % EraseColumns) * EraseStepX) + (index > 7 ? EraseStepX / 2 : 0),
        EraseTop + ((index / EraseColumns) * EraseStepY),
        EraseCellSize,
        EraseCellSize);

    /// <summary>
    /// Draws the detail page: the three modes, the one in use marked the way the reference marks it
    /// (<c>editor_interface_panel.cpp:523-529</c>).
    /// </summary>
    /// <remarks>
    /// 三个图标就是参考的三个按钮（<c>EDITPANL</c> 第 18、19、20 帧），排在与地形格同一套 36x33 网格的前三格里，
    /// 选中的那一格盖一个 TERRAINS 第 9 帧的黄框。仓库里那套 EDITPANL 只有六页（地形、擦除图标、Cell Details、
    /// Streams、Roads、Erase），没有这三帧，于是这一页退回三行文字：与物件选择页同一套行布局，选中的那行加框，
    /// 面板的热区也跟着换成行。
    /// </remarks>
    /// <param name="target">The frame buffer.</param>
    /// <param name="font">Font of the rows.</param>
    /// <param name="state">The editor state, whose <see cref="EditorPanelState.DetailMode"/> is the pressed cell.</param>
    /// <param name="highlight">Palette index of the outline a selected cell gets when the yellow rectangle is missing.</param>
    private void DrawDetailPicker(PixelBuffer target, IGlyphSource font, EditorPanelState state, byte highlight)
    {
        int selected = EditorDetailModes.IndexOf(state.DetailMode);

        if (!HasDetailIcons)
        {
            for (int i = 0; i < EditorDetailModes.All.Count; ++i)
            {
                Rect row = Row(i);

                if (i == selected)
                {
                    ImageOps.DrawRect(
                        target,
                        new Rect(_area.X + row.X, _area.Y + PickerTop + row.Y, row.Width, row.Height),
                        highlight);
                }

                EditorDetailMode mode = EditorDetailModes.All[i];
                Point position = new(_area.X + row.X + 3, _area.Y + PickerTop + row.Y + 1);

                GlyphText.DrawLine(
                    font,
                    target,
                    position,
                    FormattableString.Invariant($"{i + 1} {EditorDetailModes.Name(mode)}"),
                    new Rect(position.X, position.Y, RowWidth, RowHeight));
            }

            return;
        }

        for (int i = 0; i < _detailIcons.Count; ++i)
        {
            Rect cell = Cell(i);

            if (_detailIcons[i] is not { } icon)
            {
                continue;
            }

            ImageOps.BlitSprite(
                target,
                new Point(
                    _area.X + cell.X - icon.Source.X,
                    _area.Y + PickerTop + cell.Y - icon.Source.Y),
                icon.Sprite,
                icon.Source);
        }

        if (selected < 0)
        {
            return;
        }

        Rect frame = Cell(selected);
        Rect box = new(_area.X + frame.X, _area.Y + PickerTop + frame.Y, frame.Width, frame.Height);

        if (_selection is not null)
        {
            // The rectangle is 30x30 and the reference centres it on a 27x27 image, i.e. at -2.
            ImageOps.BlitSprite(target, new Point(box.X - 2, box.Y - 2), _selection);
        }
        else
        {
            ImageOps.DrawRect(target, box, highlight);
        }
    }

    /// <summary>
    /// Whether the sheet has the three icons of the detail page. Without them the page is three rows of
    /// text, and <see cref="HitTest"/> answers the rows instead of the cells.
    /// </summary>
    private bool HasDetailIcons => _detailIcons.Any(icon => icon is not null);

    /// <summary>
    /// Two lines naming what an instrument does, for the pages the port has no artwork for: the two that lay a path,
    /// and the pages whose icons are missing from the sheet.
    /// </summary>
    /// <param name="target">The frame buffer.</param>
    /// <param name="font">Font of the hint.</param>
    /// <param name="tool">The instrument whose page is empty.</param>
    private void DrawToolHint(PixelBuffer target, IGlyphSource font, EditorTool tool)
    {
        Point position = new(_area.X + BrushLeft, _area.Y + PickerTop + 16);

        string[] lines = tool switch
        {
            EditorTool.Detail => ["click an object to", "edit its properties"],
            EditorTool.Stream => ["drag to lay a", "stream"],
            EditorTool.Road => ["drag to lay a", "road"],
            _ => ["click to clear the", "objects under it"],
        };

        GlyphText.DrawLine(font, target, position, lines[0]);
        GlyphText.DrawLine(font, target, new Point(position.X, position.Y + font.LineHeight), lines[1]);
    }
}
