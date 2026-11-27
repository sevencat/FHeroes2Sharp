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
/// The unit information window the castle settings opens, <c>Dialog::ArmyInfo()</c>
/// (<c>dialog_armyinfo.cpp:518-712</c>), answering <see cref="ArmyBarEvent.Info"/>.
/// </summary>
/// <remarks>
/// 参考在城堡窗里只是把事件交给军队条，说明窗由军队条自己弹（<c>editor_castle_details_window.cpp:992-1003</c>）。
/// 端口把窗摞在城堡详情模态自己的栈上，关窗时把答案交回军队条。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorCastleDetailsArmyInfoTests : IDisposable
{
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts.</summary>
    public EditorCastleDetailsArmyInfoTests()
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
    public void DoubleClickingAStackStacksTheUnitInformationWindow()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);

        Rect slot = modal.View.ArmyBar.GetItemRect(0);

        // 第一下只是选中，第二下落在同一个槽上才是双击（army_bar.cpp 的双击分支）。
        Click(modal, slot);
        Click(modal, slot);

        Assert.True(modal.ArmyEvent?.Info is not null, $"no info event; top={modal.TopWindow?.GetType().Name}");

        ArmyInfoModal info = Assert.IsType<ArmyInfoModal>(modal.TopWindow);

        // 编辑器里的军队条不是只读的，所以带块按钮：升级这一格没有指挥官就编不出来，解散有。
        Assert.NotNull(info.DismissArea);
        Assert.Equal(10u, info.Dialog.Troop.Count);
        Assert.False(modal.IsFinished);
    }

    [Fact]
    public void DismissingTheStackAsksFirstAndThenEmptiesTheSlot()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);

        Rect slot = modal.View.ArmyBar.GetItemRect(0);

        Click(modal, slot);
        Click(modal, slot);

        ArmyInfoModal info = Assert.IsType<ArmyInfoModal>(modal.TopWindow);

        Click(modal, info.DismissArea!.Value);

        // 参考先问一句 _( "Are you sure you want to dismiss this army?" )（dialog_armyinfo.cpp:641-651）。
        Assert.True(info.HasInnerWindow);
        Assert.True(input.State.Army.GetTroop(0).IsValid);

        modal.HotKey(ButtonHotKey.Okay);
        Pump(modal);

        Assert.Null(modal.TopWindow);
        Assert.False(input.State.Army.GetTroop(0).IsValid);
    }

    [Fact]
    public void TheExitOfTheUnitInformationWindowLeavesTheStackAlone()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);

        Rect slot = modal.View.ArmyBar.GetItemRect(0);

        Click(modal, slot);
        Click(modal, slot);

        ArmyInfoModal info = Assert.IsType<ArmyInfoModal>(modal.TopWindow);

        Click(modal, info.ExitArea);

        Assert.Null(modal.TopWindow);
        Assert.Equal(DialogButton.Cancel, info.Answer);
        Assert.True(input.State.Army.GetTroop(0).IsValid);
    }

    /// <summary>Builds the state machine and the host over the installed data.</summary>
    /// <param name="input">The state machine of the castle window.</param>
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
    /// <returns>The state machine's subject, clean at the start.</returns>
    private static CastleDetailsState CreateState()
    {
        var metadata = new CastleMetadata { CustomName = "Old Keep" };
        var army = new Army();

        _ = army.JoinTroop(MonsterType.Peasant, 10);

        new CastleDetailsState(metadata, MapRace.Knight, PlayerColor.Red, army).WriteMetadata();

        return new CastleDetailsState(metadata, MapRace.Knight, PlayerColor.Red, army);
    }

    /// <summary>Paints the window once, the frame the areas are laid out on.</summary>
    /// <param name="modal">The window to paint.</param>
    private void Draw(IModalScreen modal) => modal.Draw(_frame);

    /// <summary>Runs one frame of the window on blank input.</summary>
    /// <param name="modal">The window to pump.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one left click on an area, the press and the release in the same frame.</summary>
    /// <param name="modal">The window to feed.</param>
    /// <param name="area">Where to click.</param>
    private void Click(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }
}
