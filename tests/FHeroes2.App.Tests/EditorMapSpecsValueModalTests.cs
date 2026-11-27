using System.Globalization;
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
/// The number pad a click on the edit box of a value element opens, <c>openVirtualNumpad()</c>
/// (<c>ui_keyboard.cpp:1295-1370</c>): the host builds it, the modal runs it on the frame pump, and a bad number
/// stacks an error box instead of closing the pad.
/// </summary>
/// <remarks>
/// 参考在这段循环里没有「取消」：点确定、回车、ESC 都算关窗，关窗之后才校验。用例按这个口径写——ESC 与回车
/// 交出同一个数，越界只弹窗不关键盘。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorMapSpecsValueModalTests : IDisposable
{
    private readonly AssetBundle? _previousAssets;
    private readonly AssetBundle? _assets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive for the whole test and remembers the one of the process.</summary>
    public EditorMapSpecsValueModalTests()
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
    public void TheHostBuildsTheNumberPadOfTheRange()
    {
        if (!TryCreateHost(out EditorSceneHost host, out _))
        {
            return;
        }

        VirtualKeyboardView? pad = host.TryCreateNumpad(500, 0, 5000);

        Assert.NotNull(pad);
        Assert.Equal("500", pad.Text);
        Assert.Equal(KeyboardLayoutType.UnsignedNumeric, pad.LayoutType);

        // 负数下限才给正负号键，参考的 ( minValue < 0 ) 那一行。
        VirtualKeyboardView? signed = host.TryCreateNumpad(-5, -100, 100);

        Assert.NotNull(signed);
        Assert.Equal(KeyboardLayoutType.SignedNumeric, signed.LayoutType);
    }

    [Fact]
    public void AcceptingTheShownNumberHandsItBack()
    {
        if (!TryCreateValueModal(500, 0, 5000, out EditorMapSpecsValueModal modal, out _))
        {
            return;
        }

        Click(modal, modal.Pad.OkayArea);

        Assert.True(modal.IsFinished);
        Assert.Equal(500, modal.Value);
    }

    [Fact]
    public void EnterAndEscapeBothValidate()
    {
        if (!TryCreateValueModal(500, 0, 5000, out EditorMapSpecsValueModal enter, out _))
        {
            return;
        }

        enter.HotKey(ButtonHotKey.Okay);
        Pump(enter);

        Assert.True(enter.IsFinished);
        Assert.Equal(500, enter.Value);

        if (!TryCreateValueModal(500, 0, 5000, out EditorMapSpecsValueModal escape, out _))
        {
            return;
        }

        // Game::HotKeyCloseWindow() 把 DEFAULT_CANCEL 也算关窗，这里没有「取消」这条出口。
        escape.HotKey(ButtonHotKey.Cancel);
        Pump(escape);

        Assert.True(escape.IsFinished);
        Assert.Equal(500, escape.Value);
    }

    [Fact]
    public void ATypedNumberPastTheMaximumStacksAnErrorAndKeepsThePad()
    {
        if (!TryCreateValueModal(100, 0, 199, out EditorMapSpecsValueModal modal, out _))
        {
            return;
        }

        if (FindKey(modal.Pad, "9") is not { } nine)
        {
            return;
        }

        Click(modal, nine.Sprite.Area);

        Assert.Equal("1009", modal.Pad.Text);

        Click(modal, modal.Pad.OkayArea);

        Assert.False(modal.IsFinished);
        Assert.Null(modal.Value);
        Assert.True(modal.HasInnerWindow);
        Assert.Equal(modal.Pad.Labels.ErrorTitle, modal.TopMessage?.Box.Header);
        Assert.Equal(modal.Pad.Labels.GetOutOfRangeMessage(0, 199), modal.TopMessage?.Box.Body);

        // 关掉错误框之后键盘还在，数字也没被清掉，可以继续改。
        modal.HotKey(ButtonHotKey.Okay);
        Pump(modal);

        Assert.False(modal.IsFinished);
        Assert.False(modal.HasInnerWindow);
        Assert.Equal("1009", modal.Pad.Text);
    }

    [Fact]
    public void AnEmptyTextStacksTheInvalidValueError()
    {
        if (!TryCreateValueModal(100, 0, 199, out EditorMapSpecsValueModal modal, out _))
        {
            return;
        }

        if (FindKey(modal.Pad, modal.Pad.Labels.Backspace) is not { } backspace)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            Click(modal, backspace.Sprite.Area);
        }

        Assert.Equal(string.Empty, modal.Pad.Text);

        Click(modal, modal.Pad.OkayArea);

        Assert.False(modal.IsFinished);
        Assert.Equal(modal.Pad.Labels.InvalidValue, modal.TopMessage?.Box.Body);
    }

    [Fact]
    public void ThePagePointsThePadAtItsGoldAndDayElements()
    {
        if (!TryCreateHost(out EditorSceneHost host, out GameFonts fonts) ||
            MapSpecsPageArtwork.TryCreate(isEvilInterface: false) is not { } artwork)
        {
            return;
        }

        var state = new MapSpecsState(Mp2Map.CreateBlank(36, terrainImageIndex: 0));
        state.VictoryCondition = MapVictoryCondition.CollectGold;
        state.UpdateVictoryCondition();

        // 面板只在有调色板时画，数字元素也是画的时候才建出来。
        EditorMapSpecsWindow window = EditorMapSpecsWindow.Create(artwork, state, fonts.White, new Palette());
        var input = new EditorMapSpecsInput(window);

        window.Paint(new PixelBuffer(640, 480));

        ValueSelectionElement? gold = window.GetValueElement(EditorMapSpecsValueField.VictoryGold);

        Assert.NotNull(gold);

        // 点的是数字框本身：参考就在那里 openVirtualNumpad( _value, _minimum, _maximum )。
        var point = new Point(gold.EditBox.X + (gold.EditBox.Width / 2), gold.EditBox.Y + (gold.EditBox.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        _ = input.Process(events);

        Assert.Equal(EditorMapSpecsRequestKind.EditValue, input.Request.Kind);
        Assert.Equal(EditorMapSpecsValueField.VictoryGold, input.Request.ValueField);

        VirtualKeyboardView? pad = host.TryCreateNumpad(gold.Value, gold.Minimum, gold.Maximum);

        Assert.NotNull(pad);
        Assert.Equal(gold.Value.ToString(CultureInfo.InvariantCulture), pad.Text);

        // 上限是 1,000,000 / 1000 = 1000 千，500 在里面，写回就能看见。
        Assert.True(input.ResolveValue(500));
        Assert.Equal(500, state.VictoryGoldInThousands);
    }

    /// <summary>Finds the key of the pad whose label is the given text.</summary>
    /// <param name="pad">The pad to search.</param>
    /// <param name="text">The label, e.g. the digit or the backspace label.</param>
    /// <returns>The key, or <see langword="null"/> when the layout has no such key.</returns>
    private static VirtualKeyboardButton? FindKey(VirtualKeyboardView pad, string text)
    {
        foreach (List<VirtualKeyboardButton> row in pad.Rows)
        {
            foreach (VirtualKeyboardButton button in row)
            {
                if (button.Text == text)
                {
                    return button;
                }
            }
        }

        return null;
    }

    /// <summary>Feeds one left click on an area of the pad, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void Click(EditorMapSpecsValueModal modal, Rect area)
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
    private void Pump(EditorMapSpecsValueModal modal)
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

    /// <summary>Builds a pad showing <paramref name="value"/> and the modal around it.</summary>
    /// <param name="value">The number the pad shows first.</param>
    /// <param name="minimum">Smallest accepted number.</param>
    /// <param name="maximum">Largest accepted number.</param>
    /// <param name="modal">The window, when the data is installed.</param>
    /// <param name="host">The host the error boxes come from.</param>
    private bool TryCreateValueModal(int value, int minimum, int maximum, out EditorMapSpecsValueModal modal,
        out EditorSceneHost host)
    {
        modal = null!;

        if (!TryCreateHost(out host, out _) ||
            host.TryCreateNumpad(value, minimum, maximum) is not { } pad)
        {
            return false;
        }

        modal = new EditorMapSpecsValueModal(pad, minimum, maximum, host);

        return true;
    }
}
