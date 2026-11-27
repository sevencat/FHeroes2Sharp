using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The castle settings window of the editor on the host's frame pump,
/// <c>openCastleDialog()</c> of <c>editor_castle_details_window.cpp:790-1100</c>: the two hot keys, the unsaved
/// changes confirmation, the rename box and the right click explanations.
/// </summary>
[Collection(LocalEventCollection.Name)]
public sealed class EditorCastleDetailsModalTests : IDisposable
{
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts.</summary>
    public EditorCastleDetailsModalTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }

        _fonts = GameFonts.TryLoad(_assets);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();
    }

    [Fact]
    public void WithoutTheGameDataThereIsNoWindow()
    {
        var host = new EditorSceneHost(assets: null, fonts: null);
        CastleDetailsState state = CreateState();

        Assert.Null(host.TryCreateCastleDetails(state, new Palette()));
        Assert.Equal(CastleDetailsOutcome.None, host.RunCastleDetails(state, new Palette()));
    }

    [Fact]
    public void OkayClosesTheWindowAsAccepted()
    {
        Run(modal =>
        {
            Draw(modal);
            Click(modal, modal.View.OkayArea);

            Assert.True(modal.IsFinished);
            Assert.Equal(CastleDetailsOutcome.Accepted, modal.Outcome);
        }, CastleDetailsOutcome.Accepted);
    }

    [Fact]
    public void HotKeyOkayClosesTheWindowAsAccepted()
    {
        Run(modal =>
        {
            Draw(modal);
            modal.HotKey(ButtonHotKey.Okay);

            Assert.True(modal.IsFinished);
            Assert.Equal(CastleDetailsOutcome.Accepted, modal.Outcome);
        }, CastleDetailsOutcome.Accepted);
    }

    [Fact]
    public void ACleanWindowLeavesOnEscapeWithoutAsking()
    {
        Run(modal =>
        {
            Draw(modal);
            modal.HotKey(ButtonHotKey.Cancel);

            Assert.Null(modal.TopWindow);
            Assert.True(modal.IsFinished);
            Assert.Equal(CastleDetailsOutcome.Discarded, modal.Outcome);
        }, CastleDetailsOutcome.Discarded);
    }

    [Fact]
    public void EscapeWithChangesAsksFirstAndOkayDiscards()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        _ = input.State.AllowCastle = !input.State.AllowCastle;

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);
        modal.HotKey(ButtonHotKey.Cancel);

        Assert.False(modal.IsFinished);
        Assert.IsType<MessageBoxModal>(modal.TopWindow);

        // 确认框的 OK 表示「放弃改动」，与点 EXIT 后回 OK 一样（:855-867）。
        modal.HotKey(ButtonHotKey.Okay);
        Pump(modal);

        Assert.Null(modal.TopWindow);
        Assert.True(modal.IsFinished);
        Assert.Equal(CastleDetailsOutcome.Discarded, modal.Outcome);
    }

    [Fact]
    public void CancelInTheConfirmationKeepsTheWindowOpen()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        _ = input.State.AllowCastle = !input.State.AllowCastle;

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);
        modal.HotKey(ButtonHotKey.Cancel);
        Assert.IsType<MessageBoxModal>(modal.TopWindow);

        modal.HotKey(ButtonHotKey.Cancel);
        Pump(modal);

        Assert.Null(modal.TopWindow);
        Assert.False(modal.IsFinished);

        modal.HotKey(ButtonHotKey.Okay);

        Assert.True(modal.IsFinished);
        Assert.Equal(CastleDetailsOutcome.Accepted, modal.Outcome);
    }

    [Fact]
    public void RenamingWritesTheMetadata()
    {
        Run(modal =>
        {
            Draw(modal);
            Click(modal, modal.View.NameArea);

            var input = Assert.IsType<InputStringModal>(modal.TopWindow);
            _ = input.Window.PressKey(Key.H, KeyModifier.None);
            _ = input.Window.PressKey(Key.O, KeyModifier.None);
            _ = input.Window.PressKey(Key.M, KeyModifier.None);

            modal.HotKey(ButtonHotKey.Okay);
            Pump(modal);

            Assert.Null(modal.TopWindow);
            Assert.False(modal.IsFinished);
            Assert.False(string.IsNullOrEmpty(modal.Input.State.Metadata.CustomName));
        });
    }

    [Fact]
    public void ARightClickOnTheNameExplainsIt()
    {
        Run(modal =>
        {
            Draw(modal);
            RightClick(modal, modal.View.NameArea);

            Assert.IsType<MessageBoxModal>(modal.TopWindow);

            modal.HotKey(ButtonHotKey.Okay);
            Pump(modal);

            Assert.Null(modal.TopWindow);
            Assert.False(modal.IsFinished);
        });
    }

    /// <summary>Runs one window and lets <paramref name="body"/> drive it.</summary>
    /// <param name="body">The driver; called once with the window on the pump.</param>
    /// <param name="expected">Outcome the host has to report.</param>
    private void Run(Action<EditorCastleDetailsModal> body, CastleDetailsOutcome? expected = null)
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        CastleDetailsOutcome reported = CastleDetailsOutcome.None;
        bool seen = false;

        host.RunModal = modal =>
        {
            var window = (EditorCastleDetailsModal)modal;

            seen = true;
            body(window);
            reported = window.Outcome;
        };

        CastleDetailsOutcome outcome = host.RunCastleDetails(input.State, new Palette());

        Assert.True(seen, "The window never reached the frame pump.");
        Assert.Equal(reported, outcome);

        if (expected is { } value)
        {
            Assert.Equal(value, outcome);
        }
    }

    /// <summary>Builds the state machine and the host over the installed data.</summary>
    /// <param name="input">The state machine of the window.</param>
    /// <param name="host">The host that owns the frame pump.</param>
    /// <returns><see langword="false"/> when the machine has no game data.</returns>
    private bool TryCreate(out CastleDetailsInput input, out EditorSceneHost host)
    {
        host = new EditorSceneHost(_assets, _fonts);
        input = null!;

        if (_assets is null || _fonts is null)
        {
            return false;
        }

        CastleDetailsInput? created = host.TryCreateCastleDetails(CreateState(), new Palette());

        if (created is null)
        {
            return false;
        }

        input = created;

        return true;
    }

    /// <summary>A castle of the red player with one stack in the army.</summary>
    private static CastleDetailsState CreateState()
    {
        var metadata = new CastleMetadata { CustomName = "Old Keep" };
        var army = new Army();

        _ = army.JoinTroop(MonsterType.Peasant, 10);

        // 参考的快照取在第一次 updateMetadata() 之后（:845-860），端口在构造时深拷元数据；先把界面默认写一遍再开窗，
        // 干净窗口才真的是干净的。
        new CastleDetailsState(metadata, MapRace.Knight, PlayerColor.Red, army).WriteMetadata();

        return new CastleDetailsState(metadata, MapRace.Knight, PlayerColor.Red, army);
    }

    /// <summary>Paints the window once, the frame the areas are laid out on.</summary>
    private void Draw(IModalScreen modal) => modal.Draw(_frame);

    /// <summary>Runs one frame of the window on blank input.</summary>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one left click on an area of the window, the press and the release in the same frame.</summary>
    private void Click(IModalScreen modal, Rect area) => Click(modal, area, MouseButtonType.Left);

    /// <summary>
    /// Feeds one right click on an area of the window; the window asks for the pressed state, not the release
    /// (<c>IsMouseRightButtonPressedInArea</c>), so the button stays down for the frame.
    /// </summary>
    private void RightClick(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Right, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one click on an area of the window, the press and the release in the same frame.</summary>
    private void Click(IModalScreen modal, Rect area, MouseButtonType button)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, button, point);
        events.OnMouseButton(false, button, point);

        modal.Update(_frame, 0.016);
    }
}
