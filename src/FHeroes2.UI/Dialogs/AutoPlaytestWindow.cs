using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// One player of the playtest, as the setup window shows it: a colour icon and the class (race) icon
/// under it, <c>Player</c> of <c>game_auto_playtest.cpp:498-520</c>.
/// </summary>
/// <param name="Color">The player's colour.</param>
/// <param name="Race">The player's class.</param>
public readonly record struct AutoPlaytestPlayer(PlayerColor Color, MapRace Race);

/// <summary>
/// The strings the auto playtest window draws, <c>_( "autoPlaytest|..." )</c> of
/// <c>src/fheroes2/game/game_auto_playtest.cpp</c>.
/// </summary>
/// <remarks>
/// 与 <see cref="RandomMapGeneratorDialogText"/> 一样，UI 层不读翻译目录：调用方把 msgid 解析好再传进来，
/// 默认值是参考英文去掉 <c>autoPlaytest|</c> 前缀。
/// </remarks>
public sealed record AutoPlaytestWindowText
{
    /// <summary>Title of the window, <c>_( "Auto Playtest" )</c>.</summary>
    public required string Title { get; init; }

    /// <summary>Label of the playthrough count row (<c>:432</c>).</summary>
    public required string PlaythroughCountLabel { get; init; }

    /// <summary>Help of the playthrough count slider (<c>:433</c>).</summary>
    public required string PlaythroughCountHelp { get; init; }

    /// <summary>Label of the day count row (<c>:441</c>).</summary>
    public required string MaxDaysLabel { get; init; }

    /// <summary>Help of the day count slider (<c>:442</c>).</summary>
    public required string MaxDaysHelp { get; init; }

    /// <summary>Label over the date of the last day (<c>:449</c>).</summary>
    public required string LastDayLabel { get; init; }

    /// <summary>Label of the animation checkbox (<c>:459</c>).</summary>
    public required string AnimationLabel { get; init; }

    /// <summary>Help of the animation checkbox (<c>:645</c>).</summary>
    public required string AnimationHelp { get; init; }

    /// <summary>Label of the animation speed row (<c>:467</c>).</summary>
    public required string AnimationSpeedLabel { get; init; }

    /// <summary>Help of the animation speed slider (<c>:475</c>).</summary>
    public required string AnimationSpeedHelp { get; init; }

    /// <summary>Label of the sound effects checkbox (<c>:486</c>).</summary>
    public required string SoundsLabel { get; init; }

    /// <summary>Help of the sound effects checkbox (<c>:648</c>).</summary>
    public required string SoundsHelp { get; init; }

    /// <summary>The yellow line under the player row (<c>:526</c>).</summary>
    public required string InterruptHint { get; init; }

    /// <summary>Title of the OKAY help (<c>:633</c>).</summary>
    public required string OkayTitle { get; init; }

    /// <summary>Body of the OKAY help (<c>:633</c>).</summary>
    public required string OkayHelp { get; init; }

    /// <summary>Title of the CANCEL help (<c>:636</c>).</summary>
    public required string CancelTitle { get; init; }

    /// <summary>Body of the CANCEL help (<c>:636</c>).</summary>
    public required string CancelHelp { get; init; }

    /// <summary>Title of the colour icon help (<c>:621</c>).</summary>
    public required string OpponentsTitle { get; init; }

    /// <summary>Body of the colour icon help, <c>%{color}</c> is filled with the colour's name.</summary>
    public required string ColorPlayerFormat { get; init; }

    /// <summary>Title of the class icon help (<c>:601</c>).</summary>
    public required string ClassTitle { get; init; }

    /// <summary>Body of the class icon help (<c>:601</c>).</summary>
    public required string ClassHelp { get; init; }

    /// <summary>The untranslated strings of the reference.</summary>
    public static AutoPlaytestWindowText Default { get; } = new()
    {
        Title = "Auto Playtest",
        PlaythroughCountLabel = "Number of playthroughs:",
        PlaythroughCountHelp = "Set the number of playthroughs.",
        MaxDaysLabel = "Max days per playthrough:",
        MaxDaysHelp = "Set the maximum number of days that each playthrough is run for.",
        LastDayLabel = "Last day of playthrough:",
        AnimationLabel = "Animation",
        AnimationHelp = "Toggle animation during playthroughs.",
        AnimationSpeedLabel = "Animation speed:",
        AnimationSpeedHelp = "Set animation speed during playthrough.",
        SoundsLabel = "Sound Effects",
        SoundsHelp = "Toggle sound effects during playthroughs.",
        InterruptHint = "Left-clicking at any point will interrupt the playtest.",
        OkayTitle = "Okay",
        OkayHelp = "Click to run an automated map playtest.",
        CancelTitle = "Cancel",
        CancelHelp = "Return to the previous menu.",
        OpponentsTitle = "Opponents",
        ColorPlayerFormat = "%{color} player",
        ClassTitle = "Class",
        ClassHelp =
            "This lets you change the class of a player. Classes are not always changeable. Depending on the " +
            "scenario, a player may receive additional towns and/or heroes not of their primary alignment.",
    };

    /// <summary>Fills the colour name into <see cref="ColorPlayerFormat"/>, the <c>StringReplace</c> of <c>:620</c>.</summary>
    /// <param name="color">The player.</param>
    /// <returns>The text of the help box.</returns>
    public string ColorPlayer(PlayerColor color) =>
        ColorPlayerFormat.Replace("%{color}", PlayerColors.Describe(color), StringComparison.Ordinal);
}

/// <summary>
/// The artwork the auto playtest window is assembled from: the metallic title box, the slider frames, the
/// checkbox frames, the player icons and the two buttons.
/// </summary>
public sealed class AutoPlaytestArtwork
{
    /// <summary>Frame 0 is the metallic box behind the title.</summary>
    public required IReadOnlyList<Sprite> TitleBoxFrames { get; init; }

    /// <summary>Frames of <c>TRADPOST</c>/<c>TRADPOSE</c>: groove, thumb and the four arrow states.</summary>
    public required IReadOnlyList<Sprite> SliderFrames { get; init; }

    /// <summary>Frame 1 of <c>CELLWIN</c>: the empty box.</summary>
    public required Sprite CheckboxBox { get; init; }

    /// <summary>Frame 2 of <c>CELLWIN</c>: the tick; <see langword="null"/> when the archive lacks it.</summary>
    public Sprite? CheckboxTick { get; init; }

    /// <summary><c>NGEXTRA</c>, for the colour and class icons and their shadow.</summary>
    public required IPlayersInfoArtwork PlayerIcons { get; init; }

    /// <summary>Source of the OKAY and CANCEL buttons.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window needs was supplied.</summary>
    public bool IsComplete =>
        TitleBoxFrames.Count > 0 && SliderFrames.Count >= Slider.RequiredSpriteCount;
}

/// <summary>How the author left the auto playtest setup window.</summary>
public enum AutoPlaytestOutcome
{
    /// <summary>Still open.</summary>
    None,

    /// <summary>OKAY: run the playtest with <see cref="AutoPlaytestWindow.Settings"/>.</summary>
    Accepted,

    /// <summary>CANCEL: run nothing.</summary>
    Cancelled
}

/// <summary>
/// The editor's AUTO PLAYTEST setup window, <c>openMapAutoPlayTest()</c>
/// (<c>game_auto_playtest.cpp:386-660</c>).
/// </summary>
/// <remarks>
/// <para>
/// 550×450 的活动区，内容照参考的偏移逐行摆：标题框离顶端 10；第一行（轮数）离顶端 70，行距 45；之后每行 30。
/// 三条滑条宽 150，值列在滑条右 57 像素处；标签在宽 250 的栏里右对齐、离栏右缘再退 10 像素。
/// </para>
/// <para>
/// <b>动画开关联动两处</b>：关掉动画时，速度滑条 <see cref="Slider.Disable"/>，音效复选变成非活动（灰掉、点了不翻）。
/// 参考在点击分支里分别调 <c>disable()</c>/<c>enable()</c> 并重画那几个字；端口每帧整窗重画，所以只要把状态摆对。
/// </para>
/// <para>
/// <b>职业图标只显示、不可改</b>：参考允许在「这张图允许改职业」时左键换职业（<c>:604-616</c>），那要读地图头；
/// 本窗只收玩家列表，所以跳过这一支，右键的说明照开。
/// </para>
/// </remarks>
public sealed class AutoPlaytestWindow
{
    /// <summary>Width of the active area, the <c>550</c> of <c>:390</c>.</summary>
    public const int WindowWidth = 550;

    /// <summary>Height of the active area, the <c>450</c> of <c>:390</c>.</summary>
    public const int WindowHeight = 450;

    /// <summary>Width of the label column, <c>optionTextMaxWidth</c> of <c>:423</c>.</summary>
    public const int OptionTextMaxWidth = 250;

    /// <summary>Room the label leaves on the right of its column, <c>optionTitleOffsetX</c>.</summary>
    public const int OptionTitleOffsetX = 10;

    /// <summary>Left inset of the whole content, <c>activeArea.x + 10</c> of <c>:425</c>.</summary>
    public const int ContentInset = 10;

    /// <summary>Length of every slider's groove, <c>sliderWidth</c> of <c>:69</c>.</summary>
    public const int SliderWidth = 150;

    /// <summary>Distance of the value column from the input column, the <c>+ 57</c> of <c>:427</c>.</summary>
    public const int ValueColumnOffset = 57;

    /// <summary>Distance between the first two rows, <c>ySpacing</c> of <c>:430</c>.</summary>
    public const int RowSpacing = 45;

    /// <summary>Distance between the rows after the second one, the three <c>+= 30</c> of <c>:452-483</c>.</summary>
    public const int CompactRowSpacing = 30;

    /// <summary>Top offset of the title box, <c>activeArea.y + 10</c> of <c>:397</c>.</summary>
    public const int TitleOffset = 10;

    /// <summary>Top offset of the first row, <c>activeArea.y + 70</c> of <c>:431</c>.</summary>
    public const int FirstRowOffset = 70;

    /// <summary>Horizontal distance between two players, <c>playerStepX</c> of <c>:70</c>.</summary>
    public const int PlayerStepX = 80;

    /// <summary>Distance of the class icon under the colour icon, the <c>+ 60</c> of <c>:516</c>.</summary>
    public const int ClassIconOffsetY = 60;

    /// <summary>Room taken by the player icons, the <c>+= 120</c> of <c>:525</c>.</summary>
    public const int PlayerRowHeight = 120;

    /// <summary>Distance of the OKAY and CANCEL buttons from the corners, <c>{ 30, 10 }</c> of <c>:530-537</c>.</summary>
    public static readonly Point ButtonOffset = new(30, 10);

    private readonly IReadOnlyList<AutoPlaytestPlayer> _players;
    private readonly AutoPlaytestWindowText _text;
    private readonly AutoPlaytestArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _bodyFont;
    private readonly IGlyphSource _valueFont;
    private readonly IGlyphSource? _disabledFont;
    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly Slider _playthroughSlider;
    private readonly Slider _daySlider;
    private readonly Slider _speedSlider;
    private readonly CheckboxWidget _animationCheckbox;
    private readonly CheckboxWidget _soundsCheckbox;
    private readonly Rect[] _colorIconAreas;
    private readonly Rect[] _classIconAreas;
    private readonly int _positionX;
    private readonly int _inputPositionX;
    private readonly int _valuePositionX;
    private readonly int _playthroughRow;
    private readonly int _dayRow;
    private readonly int _lastDayRow;
    private readonly int _animationRow;
    private readonly int _speedRow;
    private readonly int _soundsRow;
    private readonly int _playerRow;
    private readonly int _hintRow;

    private DialogPopup? _popup;
    private int _maxPlaythroughs;
    private int _maxDays;
    private int _animationSpeed;
    private bool _animationEnabled;
    private bool _environmentSounds;

    private AutoPlaytestWindow(IReadOnlyList<AutoPlaytestPlayer> players, PlaytestSettings settings,
        AutoPlaytestWindowText text, AutoPlaytestArtwork artwork, IGlyphSource titleFont, IGlyphSource bodyFont,
        IGlyphSource valueFont, IGlyphSource? disabledFont, Size screen)
    {
        _players = players;
        _text = text;
        _artwork = artwork;
        _titleFont = titleFont;
        _bodyFont = bodyFont;
        _valueFont = valueFont;
        _disabledFont = disabledFont;

        _maxPlaythroughs = settings.MaxPlaythroughs;
        _maxDays = settings.MaxDays;
        _animationSpeed = settings.AnimationSpeed;
        _animationEnabled = settings.AnimationEnabled;
        _environmentSounds = settings.EnvironmentSounds;

        _window = StandardWindow.CreateCentered(new Size(WindowWidth, WindowHeight), renderBackground: true, screen);

        Rect active = _window.ActiveArea;

        _positionX = active.X + ContentInset;
        _inputPositionX = _positionX + OptionTextMaxWidth;
        _valuePositionX = _inputPositionX + SliderWidth + ValueColumnOffset;

        _playthroughRow = active.Y + FirstRowOffset;
        _dayRow = _playthroughRow + RowSpacing;
        _lastDayRow = _dayRow + CompactRowSpacing;
        _animationRow = _lastDayRow + CompactRowSpacing;
        _speedRow = _animationRow + CompactRowSpacing;
        _soundsRow = _speedRow + CompactRowSpacing;
        _playerRow = _soundsRow + CompactRowSpacing;
        _hintRow = _playerRow + PlayerRowHeight;

        _playthroughSlider = new Slider(
            artwork.SliderFrames, SliderWidth, new Point(_inputPositionX, _playthroughRow),
            PlaytestSettings.MinimumPlaythroughs, PlaytestSettings.PlaythroughLimit, _maxPlaythroughs)
        {
            PopupTitle = text.PlaythroughCountLabel,
            PopupDescription = text.PlaythroughCountHelp,
        };

        _daySlider = new Slider(
            artwork.SliderFrames, SliderWidth, new Point(_inputPositionX, _dayRow),
            MapPlaytest.MinimumDays, MapPlaytest.DayLimit, _maxDays)
        {
            PopupTitle = text.MaxDaysLabel,
            PopupDescription = text.MaxDaysHelp,
        };

        _speedSlider = new Slider(
            artwork.SliderFrames, SliderWidth, new Point(_inputPositionX, _speedRow),
            PlaytestSettings.MinimumAnimationSpeed, PlaytestSettings.AnimationSpeedLimit, _animationSpeed)
        {
            PopupTitle = text.AnimationSpeedLabel,
            PopupDescription = text.AnimationSpeedHelp,
        };

        _animationCheckbox = new CheckboxWidget(
            artwork.CheckboxBox, artwork.CheckboxTick, new Point(_inputPositionX + 3, _animationRow),
            _animationEnabled);

        _soundsCheckbox = new CheckboxWidget(
            artwork.CheckboxBox, artwork.CheckboxTick, new Point(_inputPositionX + 3, _soundsRow),
            _environmentSounds);

        _colorIconAreas = new Rect[players.Count];
        _classIconAreas = new Rect[players.Count];

        Sprite colourIcon = artwork.PlayerIcons.GetIcon(ColorIconFrame(players.Count > 0 ? players[0].Color : PlayerColor.Blue));
        int rowWidth = Math.Max(0, (players.Count - 1) * PlayerStepX) + colourIcon.Width;
        int playerOffsetX = active.X + ((active.Width - rowWidth) / 2);

        for (int index = 0; index < players.Count; ++index)
        {
            Sprite icon = artwork.PlayerIcons.GetIcon(ColorIconFrame(players[index].Color));

            _colorIconAreas[index] = new Rect(playerOffsetX + (index * PlayerStepX), _playerRow, icon.Width,
                icon.Height);

            Sprite classIcon = artwork.PlayerIcons.GetIcon(MapRaces.IcnIndex(players[index].Race, true));

            _classIconAreas[index] = new Rect(_colorIconAreas[index].X, _playerRow + ClassIconOffsetY,
                classIcon.Width, classIcon.Height);
        }

        _buttons = new ButtonGroup();

        DialogButtonArtwork cancel = artwork.Buttons.Get(DialogButton.Cancel);
        Point cancelPosition = _window.RenderPosition(
            ButtonOffset, new Size(cancel.Released.Width, cancel.Released.Height), WindowPadding.BottomRight);

        _buttons.CreateButton(cancelPosition.X, cancelPosition.Y, cancel.Released, cancel.Pressed, DialogButton.Cancel);

        DialogButtonArtwork okay = artwork.Buttons.Get(DialogButton.Ok);
        Point okayPosition = _window.RenderPosition(
            ButtonOffset, new Size(okay.Released.Width, okay.Released.Height), WindowPadding.BottomLeft);

        _buttons.CreateButton(okayPosition.X, okayPosition.Y, okay.Released, okay.Pressed, DialogButton.Ok);

        ApplyAnimationState();
    }

    /// <summary>The window the content is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>Players shown in the bottom row, in the order given.</summary>
    public IReadOnlyList<AutoPlaytestPlayer> Players => _players;

    /// <summary>The playthrough count slider, range 1…100.</summary>
    public Slider PlaythroughSlider => _playthroughSlider;

    /// <summary>The day limit slider, range 1…3360.</summary>
    public Slider DaySlider => _daySlider;

    /// <summary>The animation speed slider, range 1…10.</summary>
    public Slider AnimationSpeedSlider => _speedSlider;

    /// <summary>The animation checkbox.</summary>
    public CheckboxWidget AnimationCheckbox => _animationCheckbox;

    /// <summary>The sound effects checkbox.</summary>
    public CheckboxWidget SoundsCheckbox => _soundsCheckbox;

    /// <summary>The OKAY and CANCEL buttons.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>Where the colour icon of player <paramref name="index"/> is, <c>playerColorRect</c>.</summary>
    /// <param name="index">Index into <see cref="Players"/>.</param>
    /// <returns>The area.</returns>
    public Rect ColorIconArea(int index) => _colorIconAreas[index];

    /// <summary>Where the class icon of player <paramref name="index"/> is, <c>playerRaceRect</c>.</summary>
    /// <param name="index">Index into <see cref="Players"/>.</param>
    /// <returns>The area.</returns>
    public Rect ClassIconArea(int index) => _classIconAreas[index];

    /// <summary>How the window was left; <see cref="AutoPlaytestOutcome.None"/> while it is open.</summary>
    public AutoPlaytestOutcome Result { get; private set; }

    /// <summary>
    /// The settings OKAY accepted, or <see langword="null"/> while the window is open and after CANCEL.
    /// </summary>
    public PlaytestSettings? Settings { get; private set; }

    /// <summary>The message a right click asked for, or <see langword="null"/>.</summary>
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
    /// Builds the window.
    /// </summary>
    /// <param name="players">The players of the map, for the icon row.</param>
    /// <param name="settings">Values the sliders and checkboxes start on.</param>
    /// <param name="text">Labels the window draws; <see cref="AutoPlaytestWindowText.Default"/> when omitted.</param>
    /// <param name="artwork">Sprites of every part of the window.</param>
    /// <param name="titleFont">Font of the title and of the player row, <c>normalWhite()</c> in the reference.</param>
    /// <param name="bodyFont">Font of the labels, <c>normalWhite()</c>.</param>
    /// <param name="valueFont">Font of the values and the hint, <c>normalYellow()</c>.</param>
    /// <param name="disabledFont">Font of an inactive label, <c>FontColor::GRAY</c>; the body font when omitted.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The window, or <see langword="null"/> when a frame it needs is missing.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static AutoPlaytestWindow? TryCreate(IReadOnlyList<AutoPlaytestPlayer> players, PlaytestSettings settings,
        AutoPlaytestWindowText? text, AutoPlaytestArtwork artwork, IGlyphSource titleFont, IGlyphSource bodyFont,
        IGlyphSource valueFont, IGlyphSource? disabledFont = null, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(bodyFont);
        ArgumentNullException.ThrowIfNull(valueFont);

        if (!artwork.IsComplete)
        {
            return null;
        }

        return new AutoPlaytestWindow(players, settings, text ?? AutoPlaytestWindowText.Default, artwork, titleFont,
            bodyFont, valueFont, disabledFont, screen ?? ScreenBuffer.DefaultSize);
    }

    /// <summary>
    /// Processes one frame of input, the body of the <c>while ( eventHandler.HandleEvents() )</c> loop of
    /// <c>:531-658</c>.
    /// </summary>
    /// <remarks>
    /// 顺序照参考：先从滑块开始（轮数、天数、速度，第一个改了就 return），再是动画复选、音效复选，最后是右键说明。
    /// 参考把 OKAY/CANCEL 的判断放在最前面，端口同样如此——否则在按钮上按下的一帧会被滑块吞掉。
    /// </remarks>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="hotKey">Standard-button key pressed this frame, if any.</param>
    /// <returns><see langword="true"/> when the window has closed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events, ButtonHotKey? hotKey = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        _popup = null;

        _buttons.SetCursor(events.CursorPosition);

        DialogButton answer = hotKey is { } key ? _buttons.ProcessHotKey(key) : DialogButton.None;

        if (answer == DialogButton.None)
        {
            answer = _buttons.SetLeftButton(events.IsMouseLeftButtonPressed);
        }

        if (answer == DialogButton.Cancel)
        {
            Result = AutoPlaytestOutcome.Cancelled;
            Settings = null;

            return true;
        }

        if (answer == DialogButton.Ok)
        {
            Result = AutoPlaytestOutcome.Accepted;
            Settings = new PlaytestSettings(_maxPlaythroughs, _maxDays, _animationSpeed, _animationEnabled,
                _environmentSounds);

            return true;
        }

        SliderFrame frame = _playthroughSlider.ProcessEvents(events);

        if (frame.Changed)
        {
            _maxPlaythroughs = _playthroughSlider.CurrentValue;

            return false;
        }

        if (TakeSliderTooltip(frame))
        {
            return false;
        }

        frame = _daySlider.ProcessEvents(events);

        if (frame.Changed)
        {
            _maxDays = _daySlider.CurrentValue;

            return false;
        }

        if (TakeSliderTooltip(frame))
        {
            return false;
        }

        if (_animationEnabled)
        {
            frame = _speedSlider.ProcessEvents(events);

            if (frame.Changed)
            {
                _animationSpeed = _speedSlider.CurrentValue;

                return false;
            }

            if (TakeSliderTooltip(frame))
            {
                return false;
            }
        }

        if (events.MouseClickLeft(_animationCheckbox.Area) && _animationCheckbox.Toggle())
        {
            _animationEnabled = _animationCheckbox.IsChecked;
            ApplyAnimationState();

            return false;
        }

        if (events.MouseClickLeft(_soundsCheckbox.Area) && _soundsCheckbox.Toggle())
        {
            _environmentSounds = _soundsCheckbox.IsChecked;

            return false;
        }

        ShowHelp(events);

        return false;
    }

    /// <summary>Draws the window and everything in it.</summary>
    /// <param name="target">Indexed frame buffer of the display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        DrawTitle(target);
        DrawPlayerRow(target);

        DrawOptionLabel(target, _text.PlaythroughCountLabel, _playthroughRow);

        _playthroughSlider.Draw(target);
        DrawValue(target, _playthroughRow, _maxPlaythroughs, PlaytestSettings.PlaythroughLimit);

        DrawOptionLabel(target, _text.MaxDaysLabel, _dayRow);

        _daySlider.Draw(target);
        DrawValue(target, _dayRow, _maxDays, MapPlaytest.DayLimit);

        DrawOptionLabel(target, _text.LastDayLabel, _lastDayRow);

        GlyphText.DrawLine(_valueFont, target, new Point(_inputPositionX + 5, _lastDayRow),
            GameDate.Describe(_maxDays));

        DrawCheckbox(target, _animationCheckbox, _text.AnimationLabel, _animationRow, isActive: true);

        IGlyphSource speedFont = _animationEnabled ? _bodyFont : _disabledFont ?? _bodyFont;

        DrawOptionLabel(target, _text.AnimationSpeedLabel, _speedRow, speedFont);

        _speedSlider.Draw(target);
        DrawValue(target, _speedRow, _animationSpeed, PlaytestSettings.AnimationSpeedLimit);

        DrawCheckbox(target, _soundsCheckbox, _text.SoundsLabel, _soundsRow, _animationCheckbox.IsChecked);

        GlyphText.DrawLine(_valueFont, target, new Point(_positionX, _hintRow), _text.InterruptHint);

        _buttons.Draw(target);
        _buttons.DrawShadows(target);
    }

    /// <summary>Brings the slider and the sound checkbox in line with the animation switch.</summary>
    private void ApplyAnimationState()
    {
        if (_animationEnabled)
        {
            _speedSlider.Enable();
        }
        else
        {
            _speedSlider.Disable();
        }

        _animationCheckbox.IsChecked = _animationEnabled;
        _soundsCheckbox.IsActive = _animationEnabled;
    }

    /// <summary>Whether <paramref name="frame"/> carried a tooltip, which is turned into <see cref="_popup"/>.</summary>
    /// <param name="frame">The frame of one slider.</param>
    /// <returns><see langword="true"/> when the popup was set.</returns>
    private bool TakeSliderTooltip(in SliderFrame frame)
    {
        if (frame.Tooltip is not { } tooltip)
        {
            return false;
        }

        _popup = new DialogPopup(tooltip.Title, tooltip.Text);

        return true;
    }

    /// <summary>Fills <see cref="_popup"/> for a right click, the <c>showStandardTextMessage()</c> calls of <c>:599-653</c>.</summary>
    /// <param name="events">Input state of the frame.</param>
    private void ShowHelp(LocalEvent events)
    {
        for (int index = 0; index < _buttons.Count; ++index)
        {
            if (!events.IsMouseRightButtonPressedInArea(_buttons[index].Area))
            {
                continue;
            }

            _popup = _buttons.ValueOf(index) == DialogButton.Ok
                ? new DialogPopup(_text.OkayTitle, _text.OkayHelp)
                : new DialogPopup(_text.CancelTitle, _text.CancelHelp);

            return;
        }

        if (events.IsMouseRightButtonPressedInArea(_animationCheckbox.Area))
        {
            _popup = new DialogPopup(_text.AnimationLabel, _text.AnimationHelp);

            return;
        }

        if (events.IsMouseRightButtonPressedInArea(_soundsCheckbox.Area))
        {
            _popup = new DialogPopup(_text.SoundsLabel, _text.SoundsHelp);

            return;
        }

        for (int index = 0; index < _classIconAreas.Length; ++index)
        {
            if (events.IsMouseRightButtonPressedInArea(_classIconAreas[index]))
            {
                _popup = new DialogPopup(_text.ClassTitle, _text.ClassHelp);

                return;
            }
        }

        for (int index = 0; index < _colorIconAreas.Length; ++index)
        {
            if (events.IsMouseRightButtonPressedInArea(_colorIconAreas[index]))
            {
                _popup = new DialogPopup(_text.OpponentsTitle, _text.ColorPlayer(_players[index].Color));

                return;
            }
        }
    }

    /// <summary>Frame of <c>NGEXTRA</c> that shows the colour of <paramref name="color"/>, <c>Color::GetIndex() + 3</c>.</summary>
    /// <param name="color">The player.</param>
    /// <returns>The frame index.</returns>
    private static uint ColorIconFrame(PlayerColor color) => (uint)(PlayerColors.IndexOf(color) + 3);

    private void DrawTitle(PixelBuffer target)
    {
        Sprite box = _artwork.TitleBoxFrames[0];
        Rect active = _window.ActiveArea;
        Rect roi = new(active.X + ((active.Width - box.Width) / 2), active.Y + TitleOffset, box.Width, box.Height);

        ImageOps.BlitSprite(target, roi.Position, box);
        ImageOps.AddGradientShadow(box, target, roi.Position, new Point(-5, 5));

        Rect textRoi = new(roi.X + 6, roi.Y + 5, roi.Width - 12, roi.Height - 11);
        string title = GlyphText.FitToOneRow(_titleFont, _text.Title, textRoi.Width);

        GlyphText.DrawLine(_titleFont, target, new Point(textRoi.X, textRoi.Y + 3), title);
    }

    private void DrawPlayerRow(PixelBuffer target)
    {
        Sprite shadow = _artwork.PlayerIcons.GetIcon(PlayerIconShadowFrame);

        for (int index = 0; index < _players.Count; ++index)
        {
            Rect colorArea = _colorIconAreas[index];

            ImageOps.BlitSprite(target, colorArea.Position,
                _artwork.PlayerIcons.GetIcon(ColorIconFrame(_players[index].Color)));
            ImageOps.AddGradientShadow(shadow, target, new Point(colorArea.X - 5, colorArea.Y + 3), new Point(0, 0));

            Rect classArea = _classIconAreas[index];

            ImageOps.BlitSprite(target, classArea.Position,
                _artwork.PlayerIcons.GetIcon(MapRaces.IcnIndex(_players[index].Race, true)));
            ImageOps.AddGradientShadow(shadow, target, new Point(classArea.X - 5, classArea.Y + 3), new Point(0, 0));
        }
    }

    /// <summary>Frame 61 of <c>NGEXTRA</c>, <c>playerIconShadow</c> of <c>:500</c>.</summary>
    private const uint PlayerIconShadowFrame = 61;

    private void DrawOptionLabel(PixelBuffer target, string label, int row) =>
        DrawOptionLabel(target, label, row, _bodyFont);

    private void DrawOptionLabel(PixelBuffer target, string label, int row, IGlyphSource font)
    {
        int width = GlyphText.Measure(font, label);
        int x = _positionX + OptionTextMaxWidth - width - OptionTitleOffsetX;

        GlyphText.DrawLine(font, target, new Point(x, row + 1), label);
    }

    private void DrawValue(PixelBuffer target, int row, int value, int limit) =>
        GlyphText.DrawLine(_valueFont, target, new Point(_valuePositionX, row + 2), $"{value}/{limit}");

    private void DrawCheckbox(PixelBuffer target, CheckboxWidget checkbox, string label, int row, bool isActive)
    {
        checkbox.IsActive = isActive;
        checkbox.Draw(target);

        IGlyphSource font = isActive ? _bodyFont : _disabledFont ?? _bodyFont;

        GlyphText.DrawLine(font, target, new Point(checkbox.Area.X + checkbox.Area.Width + 5, row + 2), label);
    }
}
