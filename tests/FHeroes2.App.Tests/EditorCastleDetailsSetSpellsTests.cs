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
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The SET SPELLS button of the castle settings, <c>mageGuildSpellsDialog()</c>
/// (<c>editor_castle_details_window.cpp:383-605</c>).
/// </summary>
/// <remarks>
/// 参考在这个模态里再调三个模态；端口不能走宿主的 <c>RunModal</c>（城堡详情窗已经开着），所以这些窗都摞在
/// <see cref="MageGuildSpellsModal"/> 自己的栈上。这三条用例盯的就是这个栈：点开、点格子、点禁用清单，再带一帧画出来。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorCastleDetailsSetSpellsTests : IDisposable
{
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts.</summary>
    public EditorCastleDetailsSetSpellsTests()
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
    public void SetSpellsOpensTheMageGuildWindow()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);
        Click(modal, modal.View.SpellsArea);

        MageGuildSpellsModal guild = Assert.IsType<MageGuildSpellsModal>(modal.TopWindow);

        // 五个等级各一行（:471-486）。
        Assert.Equal(5, guild.View.Rows.Count);
        Assert.False(modal.IsFinished);
    }

    [Fact]
    public void ClickingASlotStacksTheSpellPickerOnTheSubWindow()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);
        Click(modal, modal.View.SpellsArea);
        Draw(modal);

        MageGuildSpellsModal guild = Assert.IsType<MageGuildSpellsModal>(modal.TopWindow);
        Rect slot = guild.View.Rows[0].Coords[0];

        Click(modal, slot);

        // 这一层不是宿主开的：它压在魔法行会窗自己的栈上。
        Assert.IsType<SpellSelectModal>(guild.TopWindow);
        Assert.False(modal.IsFinished);
    }

    [Fact]
    public void ClickingBannedSpellsStacksTheSpellSelectionWindow()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);
        Click(modal, modal.View.SpellsArea);
        Draw(modal);

        MageGuildSpellsModal guild = Assert.IsType<MageGuildSpellsModal>(modal.TopWindow);

        Click(modal, guild.View.BannedSpellsArea);

        EditorSpellSelectionModal selection = Assert.IsType<EditorSpellSelectionModal>(guild.TopWindow);

        // 从当前等级的标题与禁用清单开出来（:515-521）。
        Assert.Equal(1, selection.Level);
        Assert.True(guild.HasInnerWindow);
    }

    [Fact]
    public void TheOkayOfTheMageGuildWindowWritesTheTableBack()
    {
        if (!TryCreate(out CastleDetailsInput input, out EditorSceneHost host))
        {
            return;
        }

        var modal = new EditorCastleDetailsModal(input, host);

        Draw(modal);
        Click(modal, modal.View.SpellsArea);
        Draw(modal);

        MageGuildSpellsModal guild = Assert.IsType<MageGuildSpellsModal>(modal.TopWindow);

        guild.View.Table.SetSpell(0, 0, SpellType.Bless);
        guild.View.Refresh();

        Click(modal, guild.View.OkayArea);

        Assert.Null(modal.TopWindow);
        Assert.Equal((int)SpellType.Bless, input.State.Metadata.MustHaveSpells[0]);
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
