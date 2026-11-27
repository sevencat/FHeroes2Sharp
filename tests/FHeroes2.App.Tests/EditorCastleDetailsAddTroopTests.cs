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
/// The add-troop flow of the castle settings, <c>army_bar.cpp:448-489</c>: the editor clicks an empty slot, the
/// host opens <c>Dialog::selectMonster()</c> and then <c>Dialog::SelectCount()</c>, and the slot is filled with the
/// answer.
/// </summary>
/// <remarks>
/// 参考在军队条里直接阻塞调这两扇窗（<c>Dialog::selectMonster()</c> 与 <c>Dialog::SelectCount()</c>，<c>:476-487</c>），
/// 端口把它们摞在城堡详情模态自己的栈上：选生物窗关掉就开数量窗，数量窗确定才写回槽位。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorCastleDetailsAddTroopTests : IDisposable
{
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts.</summary>
    public EditorCastleDetailsAddTroopTests()
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
    public void ClickingAnEmptySlotPicksACreatureAndFillsItWithTheChosenCount()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);

        // 第一个槽里有兵，第二格是空的：编辑器点空槽就是「加兵」。
        Click(modal, modal.View.ArmyBar.GetItemRect(1));

        MonsterSelectModal picker = Assert.IsType<MonsterSelectModal>(modal.TopWindow);

        // 第二行是弓箭手；第一下只是选中，第二下落在同一行才算确定（列表把双击当 OK）。
        Point row = new(picker.Window.ItemsArea.X + 60,
            picker.Window.ItemsArea.Y + (picker.Window.ItemsArea.Height / picker.Window.VisibleItemCount) + 10);

        Click(modal, row);
        Assert.Equal(MonsterType.Archer, picker.Window.SelectedMonster);

        Click(modal, row);

        SelectCountModal count = Assert.IsType<SelectCountModal>(modal.TopWindow);

        // 数量窗从 1 开始，两次向上箭头到 3，再按 OK。
        ClickWindow(modal, count.View.Value.UpButton.Area);
        ClickWindow(modal, count.View.Value.UpButton.Area);
        ClickWindow(modal, count.View.Buttons[0].Area);

        Assert.Null(modal.TopWindow);

        Troop added = input.State.Army.GetTroop(1);

        Assert.True(added.IsValid);
        Assert.Equal(MonsterType.Archer, added.Monster);
        Assert.Equal(3u, added.Count);
    }

    [Fact]
    public void CancellingTheCreaturePickerLeavesTheSlotEmpty()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);

        Click(modal, modal.View.ArmyBar.GetItemRect(1));

        MonsterSelectModal picker = Assert.IsType<MonsterSelectModal>(modal.TopWindow);

        Click(modal, picker.Window.Buttons[1].Area);

        Assert.Null(modal.TopWindow);
        Assert.Equal(MonsterType.Unknown, picker.SelectedMonster);
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

    /// <summary>
    /// Feeds one click through the frame's event state, which is what the castle window, the army bar, the creature
    /// list and the number window read.
    /// </summary>
    /// <param name="modal">The window to feed.</param>
    /// <param name="area">The rectangle whose centre is clicked.</param>
    private void Click(IModalScreen modal, Rect area) => Click(modal, area.Center);

    /// <inheritdoc cref="Click(IModalScreen, Rect)"/>
    /// <param name="modal">The window to feed.</param>
    /// <param name="point">Where to click.</param>
    private void Click(IModalScreen modal, Point point)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>
    /// Feeds one click through the frame's event state, which is what the number window reads: it walks the whole
    /// input surface of the reference's loop (typing, the wheel, the held arrow), so it takes the events of the
    /// frame rather than a boolean button transition.
    /// </summary>
    /// <param name="modal">The window to feed.</param>
    /// <param name="area">The rectangle whose centre is clicked.</param>
    private void ClickWindow(IModalScreen modal, Rect area) => Click(modal, area.Center);
}
