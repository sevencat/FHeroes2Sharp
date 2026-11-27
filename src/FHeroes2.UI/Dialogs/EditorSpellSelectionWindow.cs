using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The editor's spell selection window, <c>openEditorSpellSelectionWindow()</c>
/// (<c>editor_spell_selection.cpp:169-435</c>).
/// </summary>
/// <remarks>
/// <para>
/// 满屏标准窗：左上到右上是标题，中间是五列卷轴（<see cref="EditorSpellSelectionGrid"/>），底边是 OKAY/CANCEL、两个
/// Toggle All 和（多等级时）1..5 五个等级按钮。参考里窗口在等级切换时直接关掉重开，所以这个类只表示「一个等级」的
/// 那一次打开：换等级由调用方按 <see cref="EditorSpellSelectionOutcome.LevelChanged"/> 新建一个。
/// </para>
/// <para>
/// 参考靠 <c>ImageRestorer</c> 只重画改动过的一小块；端口每帧整块重画（<see cref="Paint"/>），逻辑上等价，省掉了
/// 一整类「忘了 restore」的 bug。
/// </para>
/// </remarks>
public sealed class EditorSpellSelectionWindow
{
    /// <summary>Vertical distance between two level buttons, <c>levelSelectionStepX</c> (<c>:244</c>).</summary>
    public const int LevelStepX = 62;

    /// <summary>Height of the level buttons inside the active area, <c>levelOffsetY</c> (<c>:245</c>).</summary>
    public const int LevelOffsetY = 410;

    /// <summary>The size of the screen the window fills, <c>Display::DEFAULT_WIDTH</c>/<c>HEIGHT</c> (<c>:213</c>).</summary>
    public const int ScreenWidth = 640;

    /// <summary>The height of the active area of a full screen window.</summary>
    public const int ActiveAreaHeight = 448;

    /// <summary>The lowest spell level, <c>:175</c>.</summary>
    public const int MinimumLevel = 1;

    /// <summary>The highest spell level, <c>:175</c>.</summary>
    public const int MaximumLevel = 5;

    private readonly StandardWindow _window;
    private readonly EditorSpellSelectionArtwork _artwork;
    private readonly IGlyphSource _spellFont;
    private readonly IGlyphSource _titleFont;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;
    private readonly ButtonSprite _toggleAllOn;
    private readonly ButtonSprite _toggleAllOff;
    private readonly ButtonSprite[] _levelButtons;
    private readonly int _minimumEnabledSpells;

    /// <summary>
    /// Lays the window out.
    /// </summary>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="title">The line above the scrolls.</param>
    /// <param name="spellLevel">The level being edited, 1 to 5.</param>
    /// <param name="selectedSpells">The spells the metadata holds; an empty list means every spell.</param>
    /// <param name="isMultiLevelSelectionEnabled">Whether the five level buttons are shown.</param>
    /// <param name="minimumEnabledSpells">How many spells have to stay enabled for OKAY to work.</param>
    /// <param name="pickDisabledSpells">Whether the caller wants the disabled spells instead of the enabled ones.</param>
    /// <param name="titleFont">The normal yellow font.</param>
    /// <param name="spellFont">The small white font.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spellLevel"/> is not 1 to 5.</exception>
    private EditorSpellSelectionWindow(EditorSpellSelectionArtwork artwork, string title, int spellLevel,
        IReadOnlyList<SpellType> selectedSpells, bool isMultiLevelSelectionEnabled, int minimumEnabledSpells,
        bool pickDisabledSpells, IGlyphSource titleFont, IGlyphSource spellFont)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(selectedSpells);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(spellFont);

        if (spellLevel is < MinimumLevel or > MaximumLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(spellLevel), spellLevel, "Spell levels are 1 to 5.");
        }

        _artwork = artwork;
        _titleFont = titleFont;
        _spellFont = spellFont;
        _minimumEnabledSpells = minimumEnabledSpells;
        Title = title;
        Level = spellLevel;
        IsMultiLevelSelectionEnabled = isMultiLevelSelectionEnabled;
        PickDisabledSpells = pickDisabledSpells;

        _window = StandardWindow.Create(new Rect(0, 0, ScreenWidth, ActiveAreaHeight), renderBackground: false);

        IReadOnlyList<SpellType> levelSpells = Spell.SuitableForSpellBook(spellLevel);
        bool[] flags = new bool[levelSpells.Count];
        bool anySelected = false;

        // 参考 :191 用 `!= pickDisabledSpells` 取反：要「禁用法术」的调用方给的正是那张禁用清单。
        for (int i = 0; i < flags.Length; ++i)
        {
            flags[i] = selectedSpells.Contains(levelSpells[i]) != pickDisabledSpells;
            anySelected |= flags[i];
        }

        // 没有选中任何法术时按「全选」处理，参考 :200-205；空容器在游戏里表示「按默认」。
        if (!anySelected)
        {
            Array.Fill(flags, true);
        }

        Grid = new EditorSpellSelectionGrid(levelSpells, flags, _window.ActiveArea.Position, new Size(artwork.Scroll.Width,
            artwork.Scroll.Height));

        _okay = new ButtonSprite(artwork.Okay.Released, artwork.Okay.Pressed);
        _cancel = new ButtonSprite(artwork.Cancel.Released, artwork.Cancel.Pressed);
        _toggleAllOn = new ButtonSprite(artwork.ToggleAllOn.Released, artwork.ToggleAllOn.Pressed);
        _toggleAllOff = new ButtonSprite(artwork.ToggleAllOff.Released, artwork.ToggleAllOff.Pressed);

        Point okayPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_okay.Area.Width, _okay.Area.Height), WindowPadding.BottomLeft);
        Point cancelPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_cancel.Area.Width, _cancel.Area.Height), WindowPadding.BottomRight);
        Point togglePosition = _window.RenderPosition(Point.Zero, EditorSpellSelectionArtwork.ToggleButtonSize,
            WindowPadding.BottomCenter);

        _okay.SetPosition(okayPosition.X, okayPosition.Y);
        _cancel.SetPosition(cancelPosition.X, cancelPosition.Y);
        _toggleAllOn.SetPosition(togglePosition.X, togglePosition.Y);
        _toggleAllOff.SetPosition(togglePosition.X, togglePosition.Y);

        // 一开始「全选」是灰的：所有法术本来就都是选中的，参考 :236。
        _toggleAllOn.Disable();

        _levelButtons = new ButtonSprite[5];

        for (int i = 0; i < _levelButtons.Length; ++i)
        {
            (Sprite released, Sprite pressed) = artwork.LevelButtons[i];

            _levelButtons[i] = new ButtonSprite(released, pressed);
        }

        PlaceLevelButtons();
    }

    /// <summary>The line above the scrolls.</summary>
    public string Title { get; }

    /// <summary>The level being edited.</summary>
    public int Level { get; private set; }

    /// <summary>Whether the five level buttons are shown.</summary>
    public bool IsMultiLevelSelectionEnabled { get; }

    /// <summary>Whether the caller asked for the disabled spells.</summary>
    public bool PickDisabledSpells { get; }

    /// <summary>The scrolls.</summary>
    public EditorSpellSelectionGrid Grid { get; }

    /// <summary>The active area, <c>background.activeArea()</c> (<c>:214</c>).</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The window the scrolls and the buttons sit in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The OKAY button; disabled while too few spells are enabled (<c>:391-396</c>).</summary>
    public ButtonSprite Okay => _okay;

    /// <summary>The CANCEL button.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>The Enable All Spells button, disabled until something is disabled (<c>:236</c>).</summary>
    public ButtonSprite ToggleAllOn => _toggleAllOn;

    /// <summary>The Disable All Spells button.</summary>
    public ButtonSprite ToggleAllOff => _toggleAllOff;

    /// <summary>The five level buttons, level 1 first; only existing when <see cref="IsMultiLevelSelectionEnabled"/>.</summary>
    public IReadOnlyList<ButtonSprite> LevelButtons => _levelButtons;

    /// <summary>Whether the OKAY button is enabled.</summary>
    public bool IsOkayEnabled => _okay.IsEnabled;

    /// <summary>
    /// Builds the window out of the artwork of the installed game data.
    /// </summary>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="title">The line above the scrolls.</param>
    /// <param name="spellLevel">The level being edited, 1 to 5.</param>
    /// <param name="selectedSpells">The spells the metadata holds; an empty list means every spell.</param>
    /// <param name="isMultiLevelSelectionEnabled">Whether the five level buttons are shown.</param>
    /// <param name="minimumEnabledSpells">How many spells have to stay enabled for OKAY to work.</param>
    /// <param name="pickDisabledSpells">Whether the caller wants the disabled spells instead of the enabled ones.</param>
    /// <param name="titleFont">The normal yellow font.</param>
    /// <param name="spellFont">The small white font.</param>
    /// <returns>The window.</returns>
    public static EditorSpellSelectionWindow Create(EditorSpellSelectionArtwork artwork, string title, int spellLevel,
        IReadOnlyList<SpellType> selectedSpells, bool isMultiLevelSelectionEnabled, int minimumEnabledSpells,
        bool pickDisabledSpells, IGlyphSource titleFont, IGlyphSource spellFont) =>
        new(artwork, title, spellLevel, selectedSpells, isMultiLevelSelectionEnabled, minimumEnabledSpells,
            pickDisabledSpells, titleFont, spellFont);

    /// <summary>
    /// The spells the window hands back, <c>:418-429</c>: the enabled ones, or the disabled ones when
    /// <see cref="PickDisabledSpells"/> is set; everything at once turns into nothing, because the game reads an
    /// empty list as "use the default".
    /// </summary>
    /// <returns>The result of the selection.</returns>
    public IReadOnlyList<SpellType> GetSelectedSpells()
    {
        List<SpellType> result = new(Grid.Count);

        for (int i = 0; i < Grid.Count; ++i)
        {
            if (Grid.IsSelected(i) != PickDisabledSpells)
            {
                result.Add(Grid.Spells[i]);
            }
        }

        if (result.Count == Grid.Count)
        {
            result.Clear();
        }

        return result;
    }

    /// <summary>
    /// Turns every spell on, the click on Enable All Spells (<c>:344-353</c>).
    /// </summary>
    public void EnableAllSpells()
    {
        Grid.SetAllSelected(true);
        _toggleAllOn.Disable();
        _toggleAllOff.Enable();
        RefreshOkayEnabled();
    }

    /// <summary>
    /// Turns every spell off, the click on Disable All Spells (<c>:354-363</c>).
    /// </summary>
    public void DisableAllSpells()
    {
        Grid.SetAllSelected(false);
        _toggleAllOff.Disable();
        _toggleAllOn.Enable();
        RefreshOkayEnabled();
    }

    /// <summary>
    /// Turns one spell on or off, the click on a scroll (<c>:151</c>), then updates the OKAY button and the level
    /// buttons (<c>:377-412</c>).
    /// </summary>
    /// <param name="index">Zero based scroll number.</param>
    public void ToggleSpell(int index)
    {
        Grid.Toggle(index);
        RefreshOkayEnabled();
    }

    /// <summary>Moves to another level, the level buttons (<c>:315-319</c>).</summary>
    /// <param name="level">The new level, 1 to 5.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not 1 to 5.</exception>
    public void ChangeLevel(int level)
    {
        if (level is < MinimumLevel or > MaximumLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Spell levels are 1 to 5.");
        }

        Level = level;
    }

    /// <summary>
    /// Draws the whole window, the <c>display.render()</c> calls of <c>:276</c> and <c>:414</c>.
    /// </summary>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame, for the pressed look of the buttons.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Paint(PixelBuffer target, LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(events);

        // 参考只在满屏时把 STONEBAK 抄进活动区（:218-221），端口只有这一个分辨率，所以总是抄。
        ImageOps.BlitSprite(
            target,
            ActiveArea.Position,
            _artwork.StoneBackground,
            new Rect(0, 0, ActiveArea.Width, ActiveArea.Height));

        _window.Draw(target);

        int titleWidth = GlyphText.Measure(_titleFont, Title);
        GlyphText.DrawLine(
            _titleFont,
            target,
            new Point(ActiveArea.X + ((ActiveArea.Width - titleWidth) / 2), ActiveArea.Y + 10),
            Title);

        PaintLevelButtons(target, events);
        Grid.Draw(target, _artwork.Scroll, _artwork.InactiveScroll, _artwork.GetSpellIcon, _spellFont);

        PaintButton(_toggleAllOn, target, events);
        PaintButton(_toggleAllOff, target, events);
        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
    }

    /// <summary>
    /// Recomputes whether OKAY and the other level buttons are usable, <c>:377-412</c>.
    /// </summary>
    private void RefreshOkayEnabled()
    {
        bool enabled = Grid.SelectedCount >= _minimumEnabledSpells;

        if (enabled)
        {
            _okay.Enable();
        }
        else
        {
            _okay.Disable();
        }

        for (int i = 0; i < _levelButtons.Length; ++i)
        {
            if (i + 1 == Level)
            {
                continue;
            }

            if (enabled)
            {
                _levelButtons[i].Enable();
            }
            else
            {
                _levelButtons[i].Disable();
            }
        }
    }

    private void PlaceLevelButtons()
    {
        Rect okay = _okay.Area;
        Rect cancel = _cancel.Area;
        int widthInBetween = cancel.X - okay.X - okay.Width;
        int x = okay.X + okay.Width + ((widthInBetween - (LevelStepX * _levelButtons.Length)) / 2);

        for (int i = 0; i < _levelButtons.Length; ++i)
        {
            _levelButtons[i].SetPosition(x + (LevelStepX * i), ActiveArea.Y + LevelOffsetY);

            if (!IsMultiLevelSelectionEnabled)
            {
                _levelButtons[i].Disable();
            }
        }
    }

    private void PaintLevelButtons(PixelBuffer target, LocalEvent events)
    {
        if (!IsMultiLevelSelectionEnabled)
        {
            return;
        }

        foreach (ButtonSprite button in _levelButtons)
        {
            ImageOps.AddGradientShadow(button.Released, target, button.Area.Position, new Point(-5, 5));
        }

        for (int i = 0; i < _levelButtons.Length; ++i)
        {
            ButtonSprite button = _levelButtons[i];

            // 当前等级是「按下去」的样子，参考 :261-267；其余跟着鼠标按住与否。
            if (i + 1 == Level)
            {
                button.DrawOnState(true, target);
            }
            else if (button.IsEnabled)
            {
                button.DrawOnState(events.IsMouseLeftButtonPressedAndHeldInArea(button.Area), target);
            }
            else
            {
                button.Draw(target);
            }
        }
    }

    private static void PaintButton(ButtonSprite button, PixelBuffer target, LocalEvent events)
    {
        ImageOps.AddGradientShadow(button.Released, target, button.Area.Position, new Point(-5, 5));

        if (button.IsEnabled)
        {
            button.DrawOnState(events.IsMouseLeftButtonPressedAndHeldInArea(button.Area), target);
        }
        else
        {
            button.Draw(target);
        }
    }
}
