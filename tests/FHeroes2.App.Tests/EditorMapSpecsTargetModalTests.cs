using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The candidate list the condition panels open, <c>SelectMapCastle</c> of the reference
/// (<c>editor_map_specs_window.cpp:1252-1264</c>): the host builds it, the mode runs it on the frame pump, and
/// only OKAY hands a tile back through <c>ResolveTarget</c>.
/// </summary>
/// <remarks>
/// 用例自己当帧泵：把事件塞进 <see cref="LocalEvent.Current"/> 再叫模态的 <c>Update</c>，宿主平时也是这么喂的。
/// 列表窗自己读 <see cref="LocalEvent.Current"/>，所以光标位置也要一起给。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorMapSpecsTargetModalTests : IDisposable
{
    private const byte Canvas = 3;

    private static readonly Point FirstTownTile = new(5, 6);

    private static readonly Point SecondTownTile = new(10, 11);

    private readonly AssetBundle? _previousAssets;
    private readonly AssetBundle? _assets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive for the whole test and remembers the one of the process.</summary>
    public EditorMapSpecsTargetModalTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;

        _assets?.Dispose();
    }

    [Fact]
    public void TheHostBuildsTheListFromTheArchives()
    {
        if (!TryCreateHost(out EditorSceneHost host, out _))
        {
            return;
        }

        // 滚动条、槽与两颗小按钮都来自真实归档；缺一帧就得说清楚，别在点下去的时候才发现。
        Assert.NotNull(host.TryCreateItemSelectionArtwork());
    }

    [Fact]
    public void TheListShowsEveryTownAndStartsOnTheCurrentTarget()
    {
        if (!TryCreatePicker(out EditorMapSpecsTargetModal modal, out MapSpecsState state))
        {
            return;
        }

        Assert.Equal(2, modal.Picker.Candidates.Count);
        Assert.Equal(state.VictoryTarget, modal.Picker.SelectedPosition);
        Assert.False(modal.IsFinished);
        Assert.Null(modal.SelectedPosition);
    }

    [Fact]
    public void AcceptingTheCurrentTargetHandsItBack()
    {
        if (!TryCreatePicker(out EditorMapSpecsTargetModal modal, out MapSpecsState state))
        {
            return;
        }

        Click(modal, modal.Picker.Buttons[0].Area);

        Assert.True(modal.IsFinished);
        Assert.True(modal.Accepted);
        Assert.Equal(state.VictoryTarget, modal.SelectedPosition);
    }

    [Fact]
    public void PickingAnotherTownHandsThatTileBack()
    {
        if (!TryCreatePicker(out EditorMapSpecsTargetModal modal, out _))
        {
            return;
        }

        // 第二行：列表顶 30 像素往下数，行高照参考的 35。
        Rect items = modal.Picker.ItemsArea;
        var secondRow = new Rect(items.X + 10,
            items.Y + EditorMapSpecsTargetPicker.RowHeight + (EditorMapSpecsTargetPicker.RowHeight / 2), 20, 10);

        Click(modal, secondRow);

        Assert.Equal(SecondTownTile, modal.Picker.SelectedPosition);

        Click(modal, modal.Picker.Buttons[0].Area);

        Assert.True(modal.Accepted);
        Assert.Equal(SecondTownTile, modal.SelectedPosition);
    }

    [Fact]
    public void CancellingHandsNothingBack()
    {
        if (!TryCreatePicker(out EditorMapSpecsTargetModal modal, out _))
        {
            return;
        }

        Click(modal, modal.Picker.Buttons[1].Area);

        Assert.True(modal.IsFinished);
        Assert.False(modal.Accepted);
        Assert.Null(modal.SelectedPosition);
    }

    [Fact]
    public void EnterAcceptsAndEscapeCancels()
    {
        if (TryCreatePicker(out EditorMapSpecsTargetModal accepted, out _))
        {
            // 热键单独走一条通道：先记下，下一帧才和鼠标一起交给列表窗。
            accepted.HotKey(ButtonHotKey.Okay);
            Pump(accepted);

            Assert.True(accepted.Accepted);
        }

        if (!TryCreatePicker(out EditorMapSpecsTargetModal cancelled, out _))
        {
            return;
        }

        cancelled.HotKey(ButtonHotKey.Cancel);
        Pump(cancelled);

        Assert.True(cancelled.IsFinished);
        Assert.Null(cancelled.SelectedPosition);
    }

    /// <summary>Feeds one left click on an area of the window, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void Click(EditorMapSpecsTargetModal modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Draw(_frame);
        modal.Update(_frame, 0.016);
    }

    /// <summary>Runs one modal frame without input, the frame the remembered hot key is passed in.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(EditorMapSpecsTargetModal modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Draw(_frame);
        modal.Update(_frame, 0.016);
    }

    /// <summary>Collects the archives and the fonts the host hands its windows out of.</summary>
    /// <param name="host">The host, when the data is installed.</param>
    /// <param name="fonts">The fonts, when the data is installed.</param>
    private bool TryCreateHost(out EditorSceneHost host, out GameFonts fonts)
    {
        host = null!;
        fonts = null!;

        if (_assets is null || GameFonts.TryLoad(_assets) is not { } loaded)
        {
            return false;
        }

        GameAssets.Current = _assets;
        host = new EditorSceneHost(_assets, loaded);
        fonts = loaded;

        return true;
    }

    /// <summary>Builds the list of a map with two towns, the victory target being the first one.</summary>
    /// <param name="modal">The window, when the data is installed.</param>
    /// <param name="state">The specifications the list reads its candidates from.</param>
    private bool TryCreatePicker(out EditorMapSpecsTargetModal modal, out MapSpecsState state)
    {
        modal = null!;
        state = null!;

        if (!TryCreateHost(out EditorSceneHost host, out GameFonts fonts) ||
            host.TryCreateItemSelectionArtwork() is not { } artwork)
        {
            return false;
        }

        Mp2Map map = Mp2Map.CreateBlank(36, terrainImageIndex: 0);
        map.AddCastle(new Mp2CastlePosition(FirstTownTile.X, FirstTownTile.Y, 0x00, MapRace.Knight));
        map.AddCastle(new Mp2CastlePosition(SecondTownTile.X, SecondTownTile.Y, 0x00, MapRace.Wizard));

        state = new MapSpecsState(map);
        state.VictoryCondition = MapVictoryCondition.CaptureTown;
        state.UpdateVictoryCondition();

        if (EditorMapSpecsTargetPicker.Create(EditorMapSpecsTargetKind.VictoryTown, state, artwork, fonts.Yellow,
                fonts.White) is not { } picker)
        {
            return false;
        }

        modal = new EditorMapSpecsTargetModal(picker);

        return true;
    }
}
