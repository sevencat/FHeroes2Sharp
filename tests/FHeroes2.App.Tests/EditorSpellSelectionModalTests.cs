using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The spell selection window on the host's frame pump, <c>openSpellSelectionWindow()</c>: the two hotkeys close it,
/// and a level button hands the caller a window of the new level instead of a result.
/// </summary>
/// <remarks>
/// 用例自己当帧泵：<see cref="EditorSceneHost.RunModal"/> 换成一段直接推进模态的代码，事件塞进
/// <see cref="LocalEvent.Current"/>。三条用例都不经过编辑器场景——窗口要的只有美术、字体与帧泵。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorSpellSelectionModalTests : IDisposable
{
    private readonly AssetBundle? _previousAssets;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts once for the whole test.</summary>
    public EditorSpellSelectionModalTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (_assets is null)
        {
            return;
        }

        GameAssets.Current = _assets;
        _fonts = GameFonts.TryLoad(_assets);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;

        _assets?.Dispose();
    }

    [Fact]
    public void EscapeCancelsTheSelection()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        host.RunModal = modal => Feed(modal, Key.Escape);

        EditorSpellSelectionDialogResult result = host.RunSpellSelection("Shrine:", 1, []);

        Assert.False(result.Accepted);
        Assert.Empty(result.Spells);
        Assert.Equal(1, result.SpellLevel);
    }

    [Fact]
    public void EnterAcceptsWhatIsEnabled()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        int enabled = 0;

        host.RunModal = modal =>
        {
            var spell = (EditorSpellSelectionModal)modal;

            // 先关掉一个法术：全都开着时窗口交回空列表（「用默认」），那就看不出这一下点了谁。
            Click(spell, spell.Window.Grid.Areas[0]);

            enabled = spell.Window.Grid.Count - 1;

            Feed(spell, Key.Enter);
        };

        EditorSpellSelectionDialogResult result = host.RunSpellSelection("Pyramid:", 5, []);

        Assert.True(result.Accepted);
        Assert.Equal(5, result.SpellLevel);
        Assert.Equal(enabled, result.Spells.Count);
    }

    [Fact]
    public void ALevelButtonOpensTheWindowOfThatLevel()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        int windows = 0;

        host.RunModal = modal =>
        {
            var spell = (EditorSpellSelectionModal)modal;

            ++windows;

            if (windows == 1)
            {
                // 第一层按「2」：参考在这一下 break，调用方拿着新的 spellLevel 重开一层（:324-326）。
                Click(spell, spell.Window.LevelButtons[1].Area);

                return;
            }

            Assert.Equal(2, spell.Level);

            Feed(spell, Key.Enter);
        };

        EditorSpellSelectionDialogResult result =
            host.RunSpellSelection("Mage Guild:", 1, [], isMultiLevelSelectionEnabled: true);

        Assert.Equal(2, windows);
        Assert.True(result.Accepted);
        Assert.Equal(2, result.SpellLevel);
    }

    [Fact]
    public void RightClickingASpellShowsItsDescription()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        bool described = false;

        host.RunModal = modal =>
        {
            var spell = (EditorSpellSelectionModal)modal;

            RightClick(spell, spell.Window.Grid.Areas[0]);

            Assert.True(spell.TopWindow is MessageBoxModal, "the right click did not open a description");

            var box = (MessageBoxModal)spell.TopWindow!;
            SpellType describedSpell = spell.Window.Grid.Spells[0];

            // 参考 SpellDialogElement::showPopup()：标题是法术名，正文是说明，框里还带着法术自己那个元素。
            Assert.Equal(new Spell(describedSpell).GetName(), box.Box.Header);
            Assert.Equal(new Spell(describedSpell).GetDescription(), box.Box.Body);
            Assert.Single(box.Box.Elements);
            Assert.NotNull(box.Box.Buttons);

            described = true;

            // 说明框只有 OKAY：回车关掉它，选择窗还在。
            spell.HotKey(ButtonHotKey.Okay);
            Pump(spell);

            Assert.False(spell.HasInnerWindow, "the description did not close");

            Feed(spell, Key.Escape);
        };

        EditorSpellSelectionDialogResult result = host.RunSpellSelection("Shrine:", 1, []);

        Assert.True(described);
        Assert.False(result.Accepted);
    }

    /// <summary>Runs one modal frame with a key pressed.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="key">The key the frame carries.</param>
    private void Feed(IModalScreen modal, Key key)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one left click on an area of the window, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void Click(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>
    /// Feeds one right click on an area of the window. 右键的判定是「按住且在区域内」（<c>isMouseRightButtonPressedInArea</c>），
    /// 所以这一帧只按下，抬起留到帧之后再补。
    /// </summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void RightClick(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Right, point);

        modal.Update(_frame, 0.016);

        events.OnMouseButton(false, MouseButtonType.Right, point);
    }

    /// <summary>Runs one modal frame without input, the frame after an inner window closes.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }

    /// <summary>
    /// Builds a host over the installed data; <see langword="false"/> when the machine has none, the same way the
    /// smoke tests give up.
    /// </summary>
    /// <param name="host">The host, when one could be made.</param>
    /// <returns>Whether the host could be made.</returns>
    private bool TryCreateHost(out EditorSceneHost host)
    {
        host = new EditorSceneHost(_assets, _fonts);

        return _assets is not null && _fonts is not null;
    }
}
