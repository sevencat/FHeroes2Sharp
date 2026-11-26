using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Maps;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>Artwork of <see cref="RandomMapGeneratorView"/>, gathered by the caller from the archives.</summary>
/// <remarks>
/// 五组精灵，每一组都是参考在 <c>dialog_random_map_generator.cpp</c> 里点名的那一个 ICN：
/// <see cref="BackgroundFrames"/> 是 <c>STONEBAK</c>（窗口内的石面）、<see cref="TitleBoxFrames"/> 是
/// <c>METALLIC_BORDERED_TEXTBOX_*</c>（标题的金属框）、<see cref="SliderFrames"/> 是 <c>TRADPOST</c>、
/// <see cref="ValueFrames"/> 是 <c>TOWNWIND</c>（种子的数字框与上下箭头）、<see cref="DroplistFrames"/> 是
/// <c>DROPLISL</c>（布局那一行画出来但禁用的下拉框）。
/// </remarks>
public sealed class RandomMapGeneratorArtwork
{
    /// <summary>Frame 0 is the stone surface the reference paints over the whole active area.</summary>
    public required IReadOnlyList<Sprite> BackgroundFrames { get; init; }

    /// <summary>Frame 0 is the metallic box behind the title.</summary>
    public required IReadOnlyList<Sprite> TitleBoxFrames { get; init; }

    /// <summary>Frames of <c>TRADPOST</c>: groove, thumb, four arrow states.</summary>
    public required IReadOnlyList<Sprite> SliderFrames { get; init; }

    /// <summary>Frames of <c>TOWNWIND</c>: the seed field and its two arrows.</summary>
    public required IReadOnlyList<Sprite> ValueFrames { get; init; }

    /// <summary>Frames of <c>DROPLISL</c>: the box of the layout row and its (disabled) button.</summary>
    public required IReadOnlyList<Sprite> DroplistFrames { get; init; }

    /// <summary>Source of the OK and CANCEL buttons.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window reads was supplied.</summary>
    public bool IsComplete =>
        BackgroundFrames.Count > 0
        && TitleBoxFrames.Count > 0
        && SliderFrames.Count >= Slider.RequiredSpriteCount
        && ValueFrames.Count >= ValueSelectionElement.RequiredFrameCount
        && DroplistFrames.Count > 2;
}

/// <summary>
/// The clickable, drawable random map generator dialog
/// (<c>src/fheroes2/dialog/dialog_random_map_generator.cpp:72-251</c>).
/// </summary>
/// <remarks>
/// <para>
/// 六行，行距 45，第一行离活动区顶端 70：三根滑块（玩家数、水量、怪物强度、资源密度）、一行画出来但禁用的布局、
/// 一个种子数字框，底部左边 OK、右边 CANCEL——参考就是把这个顺序与这些偏移写死的，照抄才不会看起来「差一点」。
/// </para>
/// <para>
/// <b>布局那一行是「涂暗的假下拉框」</b>：参考画了 <c>DROPLISL</c> 的框与按钮，然后把整块压一层暗色、把按钮
/// <c>disable()</c>，因为生成器只会 <c>MIRRORED</c>。端口照做，只是暗色改用 <see cref="ImageOps.ApplyPalette"/>。
/// </para>
/// <para>
/// <b>输入</b>走 <see cref="ProcessEvents"/>：滑块吃 <see cref="LocalEvent"/>（参考的循环本来就在这一层转），按钮吃
/// 热键。窗口自己不开循环，由 <see cref="Show"/> 或调用方的循环驱动，与 <see cref="AudioSettingsDialog"/> 一致。
/// </para>
/// </remarks>
public sealed class RandomMapGeneratorView
{
    /// <summary>Width of the active area; the reference's 640 wide window leaves a 16 pixel frame on each side.</summary>
    public const int ActiveWidth = 608;

    /// <summary>Height of the active area, the 480 of the reference's window less both frames.</summary>
    public const int ActiveHeight = 448;

    /// <summary>Left inset of the label column, <c>activeArea.x + 12</c> of <c>:113</c>.</summary>
    public const int LabelInset = 12;

    /// <summary>Vertical distance between two rows, <c>ySpacing</c> of <c>:117</c>.</summary>
    public const int RowSpacing = 45;

    /// <summary>Top offset of the first row, <c>activeArea.y + 70</c> of <c>:118</c>.</summary>
    public const int FirstRowOffset = 70;

    /// <summary>Top offset of the title box, <c>activeArea.y + 10</c> of <c>:103</c>.</summary>
    public const int TitleOffset = 10;

    /// <summary>Length of the groove of every slider, <c>sliderWidth</c> of <c>:48</c>.</summary>
    public const int SliderWidth = 187;

    /// <summary>Distance of the value column from the input column, the <c>230</c> of <c>:116</c>.</summary>
    public const int ValueColumnOffset = 230;

    /// <summary>Width of the value column, i.e. of the <see cref="TextRestorerArea"/> of the reference.</summary>
    public const int ValueWidth = 150;

    /// <summary>Height of the value column, the <c>40</c> of <c>:54</c>.</summary>
    public const int ValueHeight = 40;

    /// <summary>Extra distance before the seed row, the <c>+ 10</c> of <c>:179</c>.</summary>
    public const int SeedRowExtraOffset = 10;

    /// <summary>Width of the dark layout box, <c>layoutBackgroundWidth</c> of <c>:135</c>.</summary>
    public const int LayoutBoxWidth = 200;

    /// <summary>Inset of the layout box from the input column, the <c>+ 6</c> of <c>:138</c>.</summary>
    public const int LayoutBoxInset = 6;

    /// <summary>Distance of the OK and CANCEL buttons from the corners, <c>{ 30, 10 }</c> of <c>:189</c>.</summary>
    public static readonly Point ButtonOffset = new(30, 10);

    /// <summary>Title of the OK help, <c>_( "Okay" )</c>.</summary>
    public const string OkayTitleId = "Okay";

    /// <summary>Body of the OK help, <c>_( "Click to generate a new map." )</c>.</summary>
    public const string OkayHelpId = "Click to generate a new map.";

    /// <summary>Title of the CANCEL help, <c>_( "Cancel" )</c>.</summary>
    public const string CancelTitleId = "Cancel";

    /// <summary>Body of the CANCEL help, <c>_( "Return to the previous menu." )</c>.</summary>
    public const string CancelHelpId = "Return to the previous menu.";

    private readonly RandomMapGeneratorDialog _dialog;
    private readonly RandomMapGeneratorDialogText _text;
    private readonly RandomMapGeneratorArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _bodyFont;
    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly Slider _playerCountSlider;
    private readonly Slider _waterSlider;
    private readonly Slider _monsterSlider;
    private readonly Slider _resourceSlider;
    private readonly ValueSelectionElement _seed;
    private readonly ButtonSprite? _layoutButton;
    private readonly Sprite _layoutBox;
    private readonly Rect _layoutBoxArea;
    private readonly Rect _labelColumn;
    private readonly Rect _valueColumn;

    private DialogPopup? _popup;

    private RandomMapGeneratorView(RandomMapGeneratorDialog dialog, RandomMapGeneratorDialogText text,
        RandomMapGeneratorArtwork artwork, IGlyphSource titleFont, IGlyphSource bodyFont, Size screen)
    {
        _dialog = dialog;
        _text = text;
        _artwork = artwork;
        _titleFont = titleFont;
        _bodyFont = bodyFont;

        _window = StandardWindow.CreateCentered(new Size(ActiveWidth, ActiveHeight), renderBackground: true, screen);

        Rect active = _window.ActiveArea;
        int labelWidth = active.Width / 3;
        int inputX = active.X + LabelInset + labelWidth;
        int valueX = inputX + ValueColumnOffset;
        int y = active.Y + FirstRowOffset;

        _labelColumn = new Rect(active.X + LabelInset, y, labelWidth, RowSpacing * 6);
        _valueColumn = new Rect(valueX, y, ValueWidth, ValueHeight);

        _playerCountSlider = new Slider(
            artwork.SliderFrames, SliderWidth, new Point(inputX, y),
            RandomMapGeneratorDialog.MinimumPlayers, RandomMapGeneratorDialog.MaximumPlayers, dialog.PlayerCount);

        _playerCountSlider.PopupTitle = text.PlayerCountLabel;

        y += RowSpacing;

        // 布局：参考画下拉框的底与按钮，把整块涂暗、按钮禁用（生成器只会 MIRRORED）。
        _layoutBox = artwork.DroplistFrames[0];
        _layoutBoxArea = new Rect(inputX + LayoutBoxInset, y - 5, LayoutBoxWidth, _layoutBox.Height);

        ButtonSprite layoutButton = new(
            artwork.DroplistFrames[1], artwork.DroplistFrames[2],
            offsetX: inputX + LayoutBoxWidth + LayoutBoxInset, offsetY: y - 5);

        layoutButton.Disable();
        _layoutButton = layoutButton;

        y += RowSpacing;

        _waterSlider = new Slider(
            artwork.SliderFrames, SliderWidth, new Point(inputX, y),
            0, dialog.MaximumWaterPercentage, dialog.WaterPercentage);

        _waterSlider.PopupTitle = text.WaterPercentageLabel;

        y += RowSpacing;

        _monsterSlider = new Slider(
            artwork.SliderFrames, SliderWidth, new Point(inputX, y),
            0, (int)RandomMapMonsterStrength.Deadly, (int)dialog.MonsterStrength);

        _monsterSlider.PopupTitle = text.MonsterStrengthLabel;

        y += RowSpacing;

        _resourceSlider = new Slider(
            artwork.SliderFrames, SliderWidth, new Point(inputX, y),
            0, (int)RandomMapResourceDensity.Abundant, (int)dialog.ResourceDensity);

        _resourceSlider.PopupTitle = text.ResourceAvailabilityLabel;

        y += RowSpacing + SeedRowExtraOffset;

        _seed = new ValueSelectionElement(
            RandomMapGeneratorDialog.MinimumSeed, RandomMapGeneratorDialog.MaximumSeed, dialog.Seed, 1,
            new Point(active.X + LabelInset + labelWidth + 4, y - 5), bodyFont, artwork.ValueFrames);

        _buttons = new ButtonGroup();

        DialogButtonArtwork okay = artwork.Buttons.Get(DialogButton.Ok);
        DialogButtonArtwork cancel = artwork.Buttons.Get(DialogButton.Cancel);

        Point cancelPosition = _window.RenderPosition(
            ButtonOffset, new Size(cancel.Released.Width, cancel.Released.Height), WindowPadding.BottomRight);

        _buttons.CreateButton(cancelPosition.X, cancelPosition.Y, cancel.Released, cancel.Pressed, DialogButton.Cancel);

        Point okayPosition = _window.RenderPosition(
            ButtonOffset, new Size(okay.Released.Width, okay.Released.Height), WindowPadding.BottomLeft);

        _buttons.CreateButton(okayPosition.X, okayPosition.Y, okay.Released, okay.Pressed, DialogButton.Ok);

        _rowTops = [.. Enumerable.Range(0, 6).Select(index => active.Y + FirstRowOffset + (index * RowSpacing))];
    }

    private readonly int[] _rowTops;

    /// <summary>The model the window edits.</summary>
    public RandomMapGeneratorDialog Dialog => _dialog;

    /// <summary>The window the dialog is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The OK and CANCEL buttons.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>The player count slider.</summary>
    public Slider PlayerCountSlider => _playerCountSlider;

    /// <summary>The water percentage slider.</summary>
    public Slider WaterSlider => _waterSlider;

    /// <summary>The monster strength slider.</summary>
    public Slider MonsterSlider => _monsterSlider;

    /// <summary>The resource density slider.</summary>
    public Slider ResourceSlider => _resourceSlider;

    /// <summary>The seed field.</summary>
    public ValueSelectionElement Seed => _seed;

    /// <summary>
    /// The message a right click asked for, or <see langword="null"/>; <see cref="TakePopup"/> clears it.
    /// </summary>
    public DialogPopup? PendingPopup => _popup;

    /// <summary>Takes the message a right click asked for, if any.</summary>
    /// <returns>The message, or <see langword="null"/> when none was asked for.</returns>
    public DialogPopup? TakePopup()
    {
        DialogPopup? popup = _popup;
        _popup = null;

        return popup;
    }

    /// <summary>
    /// Runs the window until it closes, the loop of <c>randomMapGeneratorDialog()</c> (<c>:198-249</c>).
    /// </summary>
    /// <param name="target">Indexed frame buffer the scene draws and renders.</param>
    /// <param name="view">The window to run.</param>
    /// <param name="events">Input state, <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// The configuration the player accepted, or <see langword="null"/> when the dialog was cancelled.
    /// </returns>
    /// <remarks>
    /// 与参考一样，窗口转自己的帧循环：画、渲染、睡一小会儿，直到 OK 或 CANCEL。右击按钮的说明用
    /// <see cref="MessageBox"/> 弹（参考是 <c>showStandardTextMessage()</c>）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="view"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">There is no event engine to run the loop on.</exception>
    public static RandomMapConfig? Show(PixelBuffer target, RandomMapGeneratorView view, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(view);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine
            ?? throw new InvalidOperationException("The random map generator window needs an event engine to run.");

        while (eventState.HandleEvents(sleepAfterEventProcessing: false))
        {
            if (view.ProcessEvents(eventState, HotKeyOf(eventState)))
            {
                return view.Dialog.Result;
            }

            if (view.TakePopup() is { } popup)
            {
                MessageBox.Show(target, popup.Header, popup.Body, view._titleFont, view._bodyFont,
                    DialogButton.Ok, view._artwork.Buttons, eventState);
            }

            view.Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        // 窗口被强行关掉（引擎停了）与取消同义：没有配置可用。
        view.Dialog.Cancel();

        return null;
    }

    /// <summary>
    /// The standard-button key of the frame, <c>Game::HotKeyPressEvent( DEFAULT_OKAY / DEFAULT_CANCEL )</c>
    /// (<c>:202-206</c>).
    /// </summary>
    private static ButtonHotKey? HotKeyOf(LocalEvent events)
    {
        if (events.IsKeyPressed(Key.Enter))
        {
            return ButtonHotKey.Okay;
        }

        return events.IsKeyPressed(Key.Escape) || events.IsKeyPressed(Key.F10) ? ButtonHotKey.Cancel : null;
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="dialog">The model the window edits.</param>
    /// <param name="text">Labels the window draws.</param>
    /// <param name="artwork">Sprites of every part of the window.</param>
    /// <param name="titleFont">Font of the title, <c>FontType::normalWhite()</c> in the reference.</param>
    /// <param name="bodyFont">Font of the labels and the values.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The view, or <see langword="null"/> when an ICN frame is missing.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static RandomMapGeneratorView? TryCreate(RandomMapGeneratorDialog dialog, RandomMapGeneratorDialogText text,
        RandomMapGeneratorArtwork artwork, IGlyphSource titleFont, IGlyphSource bodyFont, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        if (!artwork.IsComplete)
        {
            return null;
        }

        return new RandomMapGeneratorView(
            dialog, text, artwork, titleFont, bodyFont, screen ?? ScreenBuffer.DefaultSize);
    }

    /// <summary>
    /// Processes one frame of input, the <c>while ( le.HandleEvents() )</c> body of the reference (<c>:198-249</c>).
    /// </summary>
    /// <remarks>
    /// 顺序照参考：先按钮的状态，再看 CANCEL 热键与点击、OK 热键与点击，最后才把这一帧交给三根滑块与种子框。滑块的
    /// 「值变了」会写回模型，所以调用方只要在返回 <see langword="true"/> 时重画即可。
    /// </remarks>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="hotKey">Standard-button key that was pressed this frame, if any.</param>
    /// <returns>
    /// <see langword="true"/> when the window has closed: <see cref="RandomMapGeneratorDialog.Result"/> then holds the
    /// configuration, or <see langword="null"/> when the player cancelled.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events, ButtonHotKey? hotKey = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        _buttons.SetCursor(events.CursorPosition);

        DialogButton answer = hotKey is { } key ? _buttons.ProcessHotKey(key) : DialogButton.None;

        if (answer == DialogButton.None)
        {
            answer = _buttons.SetLeftButton(events.IsMouseLeftButtonPressed);
        }

        if (answer == DialogButton.Cancel)
        {
            _dialog.Cancel();

            return true;
        }

        if (answer == DialogButton.Ok)
        {
            _dialog.Accept();

            return true;
        }

        if (_playerCountSlider.ProcessEvents(events).Changed)
        {
            _dialog.SetPlayerCount(_playerCountSlider.CurrentValue);

            // 上限跟着玩家数走，滑块的范围也要改，否则玩家还能把水量推回去。
            _waterSlider.SetRange(0, _dialog.MaximumWaterPercentage);

            return false;
        }

        if (_waterSlider.ProcessEvents(events).Changed)
        {
            _dialog.SetWaterPercentage(_waterSlider.CurrentValue);

            return false;
        }

        if (_monsterSlider.ProcessEvents(events).Changed)
        {
            _dialog.SetMonsterStrength((RandomMapMonsterStrength)_monsterSlider.CurrentValue);

            return false;
        }

        if (_resourceSlider.ProcessEvents(events).Changed)
        {
            _dialog.SetResourceDensity((RandomMapResourceDensity)_resourceSlider.CurrentValue);

            return false;
        }

        if (_seed.SetLeftButton(events.IsMouseLeftButtonPressed, events.CursorPosition))
        {
            _dialog.SetSeed(_seed.Value);
        }

        ShowHelp(events);

        return false;
    }

    /// <summary>Fills <see cref="_popup"/> for a right click, the <c>showStandardTextMessage()</c> calls of <c>:210-215</c>.</summary>
    /// <param name="events">Input state of the frame.</param>
    private void ShowHelp(LocalEvent events)
    {
        for (int index = 0; index < _buttons.Count; ++index)
        {
            if (!events.MouseClickRight(_buttons[index].Area))
            {
                continue;
            }

            _popup = _buttons.ValueOf(index) == DialogButton.Ok
                ? new DialogPopup(OkayTitleId, OkayHelpId)
                : new DialogPopup(CancelTitleId, CancelHelpId);

            return;
        }
    }

    /// <summary>Draws the window and everything in it.</summary>
    /// <param name="target">Indexed frame buffer of the display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        Rect active = _window.ActiveArea;

        // 石面：参考把整张 STONEBAK 贴进活动区（多出来的部分被活动区裁掉），端口按同样的大小裁一次。
        ImageOps.BlitSprite(
            target, active.Position, _artwork.BackgroundFrames[0], new Rect(0, 0, active.Width, active.Height));

        DrawTitle(target, active);
        DrawLabels(target);
        DrawValues(target);

        _playerCountSlider.Draw(target);
        DrawLayoutRow(target);
        _waterSlider.Draw(target);
        _monsterSlider.Draw(target);
        _resourceSlider.Draw(target);
        _seed.Draw(target);

        _buttons.Draw(target);
        _buttons.DrawShadows(target);
    }

    private void DrawTitle(PixelBuffer target, Rect active)
    {
        Sprite box = _artwork.TitleBoxFrames[0];
        Rect roi = new(active.X + ((active.Width - box.Width) / 2), active.Y + TitleOffset, box.Width, box.Height);

        ImageOps.BlitSprite(target, roi.Position, box);
        ImageOps.AddGradientShadow(box, target, roi.Position, new Point(-5, 5));

        Rect textRoi = new(roi.X + 6, roi.Y + 5, roi.Width - 12, roi.Height - 11);
        string title = GlyphText.FitToOneRow(_titleFont, _text.Title, textRoi.Width);

        GlyphText.DrawLine(_titleFont, target, new Point(textRoi.X, textRoi.Y + 3), title);
    }

    private void DrawLabels(PixelBuffer target)
    {
        string[] labels =
        [
            _text.PlayerCountLabel,
            _text.MapLayoutLabel,
            _text.WaterPercentageLabel,
            _text.MonsterStrengthLabel,
            _text.ResourceAvailabilityLabel,
            _text.MapSeedLabel,
        ];

        for (int index = 0; index < labels.Length && index < _rowTops.Length; ++index)
        {
            // 参考把标签在「说明栏」里居中，不是左对齐。
            int width = GlyphText.Measure(_bodyFont, labels[index]);
            int x = _labelColumn.X + ((_labelColumn.Width - width) / 2);

            GlyphText.DrawLine(_bodyFont, target, new Point(x, _rowTops[index]), labels[index]);
        }
    }

    private void DrawValues(PixelBuffer target)
    {
        DrawValue(target, 0, _dialog.PlayerCount.ToString());
        DrawValue(target, 2, $"{_dialog.WaterPercentage}/{_dialog.MaximumWaterPercentage}%");
        DrawValue(target, 3, _text.MonsterStrength(_dialog.MonsterStrength));
        DrawValue(target, 4, _text.ResourceDensity(_dialog.ResourceDensity));
    }

    private void DrawValue(PixelBuffer target, int row, string value)
    {
        if (row < 0 || row >= _rowTops.Length)
        {
            return;
        }

        Rect roi = new(_valueColumn.X, _rowTops[row], ValueWidth, ValueHeight);

        GlyphText.DrawMultiline(_bodyFont, target, new Point(roi.X, roi.Y + 2), value, ValueWidth);
    }

    private void DrawLayoutRow(PixelBuffer target)
    {
        // 参考：先画下拉框的底与名字，再把整块压暗，最后画一个禁用的按钮。
        ImageOps.BlitSprite(
            target, _layoutBoxArea.Position, _layoutBox,
            new Rect(0, 0, _layoutBoxArea.Width, _layoutBoxArea.Height));

        string name = _text.Layout(_dialog.Layout);

        GlyphText.DrawLine(_bodyFont, target, new Point(_layoutBoxArea.X + 6, _layoutBoxArea.Y + 5), name);

        ImageOps.ApplyPalette(target, _layoutBoxArea, PaletteTable.Get(PaletteId.Darkening));

        _layoutButton?.Draw(target);
    }
}
