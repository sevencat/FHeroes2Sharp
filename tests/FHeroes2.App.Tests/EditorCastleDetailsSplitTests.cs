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
/// The split question of the castle settings, <c>army_bar.cpp:96-142</c>: the split key held over an empty slot
/// opens <c>Dialog::ArmySplitTroop()</c>, and its two answers feed <see cref="ArmyBar.ApplySplit"/>.
/// </summary>
/// <remarks>
/// 参考在军队条里阻塞调 <c>Dialog::ArmySplitTroop()</c>，端口把窗摞在城堡详情模态自己的栈上：槽位那排选定几格、
/// 数字框决定移动几个兵，确定之后才落账。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorCastleDetailsSplitTests : IDisposable
{
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts.</summary>
    public EditorCastleDetailsSplitTests()
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
    public void TheSplitKeyOverAnEmptySlotOpensTheSplitWindow()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);

        // 先选中唯一的兵堆，再按住拆半键点空槽——参考的 ( !troop.isValid() || isSameTroopType ) 那一支。
        Click(modal, modal.View.ArmyBar.GetItemRect(0));
        ShiftClick(modal, modal.View.ArmyBar.GetItemRect(1));

        ArmySplitModal split = Assert.IsType<ArmySplitModal>(modal.TopWindow);

        // 五个槽位、一个兵堆：freeSlots 是 5，槽位那排是 4 张图（2 到 5 格），数字最大就是那一堆的 10 个。
        Assert.Equal(4, split.View.SlotAreas.Count);
        Assert.Equal(10, split.View.Value.Maximum);
        Assert.Equal(10u, input.State.Army.GetTroop(0).Count);
    }

    [Fact]
    public void ChoosingFiveSlotsSpreadsTheStackOverTheArmy()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);

        Click(modal, modal.View.ArmyBar.GetItemRect(0));
        ShiftClick(modal, modal.View.ArmyBar.GetItemRect(1));

        ArmySplitModal split = Assert.IsType<ArmySplitModal>(modal.TopWindow);

        // 第三张图是「拆成五格」。
        Rect icon = split.View.SlotAreas[3];

        ClickWindow(modal, icon);
        ClickWindow(modal, split.View.Buttons[0].Area);

        Assert.Null(modal.TopWindow);

        for (int index = 0; index < Army.MaximumTroopCount; ++index)
        {
            Troop troop = input.State.Army.GetTroop(index);

            Assert.True(troop.IsValid, $"slot {index} is empty");
            Assert.Equal(MonsterType.Peasant, troop.Monster);
            Assert.Equal(2u, troop.Count);
        }
    }

    [Fact]
    public void CancellingTheSplitWindowLeavesTheArmyAlone()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);

        Click(modal, modal.View.ArmyBar.GetItemRect(0));
        ShiftClick(modal, modal.View.ArmyBar.GetItemRect(1));

        ArmySplitModal split = Assert.IsType<ArmySplitModal>(modal.TopWindow);

        ClickWindow(modal, split.View.Buttons[1].Area);

        Assert.Null(modal.TopWindow);
        Assert.Equal(10u, input.State.Army.GetTroop(0).Count);
        Assert.False(input.State.Army.GetTroop(1).IsValid);
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

    /// <summary>Feeds one click through the frame's event state.</summary>
    /// <param name="modal">The window to feed.</param>
    /// <param name="area">The rectangle whose centre is clicked.</param>
    private void Click(IModalScreen modal, Rect area)
    {
        Point point = area.Center;
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one click with the split key held, the Shift of the default bindings.</summary>
    /// <param name="modal">The window to feed.</param>
    /// <param name="area">The rectangle whose centre is clicked.</param>
    private void ShiftClick(IModalScreen modal, Rect area)
    {
        Point point = area.Center;
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnKeyboard(Key.LeftShift, KeyModifier.Shift, KeyboardEventState.Down);
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);

        events.OnKeyboard(Key.LeftShift, KeyModifier.None, KeyboardEventState.Up);
    }

    /// <summary>Feeds one click through the cursor and the left button, which the split window reads.</summary>
    /// <param name="modal">The window to feed.</param>
    /// <param name="area">The rectangle whose centre is clicked.</param>
    private void ClickWindow(IModalScreen modal, Rect area)
    {
        Point point = area.Center;

        LocalEvent.Current.BeginFrame();
        modal.SetCursor(point);
        modal.SetLeftButton(true);
        modal.SetLeftButton(false);

        modal.Update(_frame, 0.016);
    }
}
