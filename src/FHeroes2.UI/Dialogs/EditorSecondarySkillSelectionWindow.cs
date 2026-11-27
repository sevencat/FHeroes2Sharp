using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The editor's secondary skill selection window, <c>openSecondarySkillSelectionWindow()</c>
/// (<c>editor_secondary_skill_selection.cpp:199-394</c>).
/// </summary>
/// <remarks>
/// <para>
/// 满屏标准窗：左上到右上是标题，中间是四列技能格（<see cref="EditorSecondarySkillSelectionGrid"/>），底边是
/// OKAY/CANCEL 与两个全开/全关按钮。技能表是女巫小屋那十二个（<c>:207</c>），等级由调用方给（<c>:201-205</c>）。
/// </para>
/// <para>
/// 两个开关按钮同点重叠、只有启用的那个可见（<c>:260-265</c> 把两个都摆在 <c>BOTTOM_CENTER</c>、<c>{0,7}</c>），
/// 所以 <see cref="Paint"/> 只画启用的那一个——和法术窗一样，参考靠「后画的盖住先画的」达到同一效果。
/// </para>
/// <para>
/// 参考用 <c>ImageRestorer</c> 只重画改过的一格；端口每帧整块重画（<see cref="Paint"/>），逻辑上等价。
/// </para>
/// </remarks>
public sealed class EditorSecondarySkillSelectionWindow
{
    /// <summary>The size of the screen the window fills, <c>Display::DEFAULT_WIDTH</c>/<c>HEIGHT</c> (<c>:238</c>).</summary>
    public const int ScreenWidth = 640;

    /// <summary>The height of the active area of a full screen window.</summary>
    public const int ActiveAreaHeight = 448;

    private readonly StandardWindow _window;
    private readonly EditorSecondarySkillSelectionArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _skillFont;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;
    private readonly ButtonSprite _toggleAllOn;
    private readonly ButtonSprite _toggleAllOff;

    /// <summary>
    /// Lays the window out.
    /// </summary>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="title">The line above the cells.</param>
    /// <param name="level">The level every skill is offered at, basic to expert.</param>
    /// <param name="selectedSkills">The skills the metadata holds; an empty list means every skill.</param>
    /// <param name="titleFont">The normal yellow font.</param>
    /// <param name="skillFont">The small white font.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is <see cref="SkillLevel.None"/>.</exception>
    private EditorSecondarySkillSelectionWindow(EditorSecondarySkillSelectionArtwork artwork, string title,
        SkillLevel level, IReadOnlyList<SecondarySkill> selectedSkills, IGlyphSource titleFont, IGlyphSource skillFont)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(selectedSkills);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(skillFont);

        if (level == SkillLevel.None)
        {
            // 参考 :201-205 把「不是 1..3」当调用方的 bug 断言掉。
            throw new ArgumentOutOfRangeException(nameof(level), level, "Secondary skills are offered at a level.");
        }

        _artwork = artwork;
        _titleFont = titleFont;
        _skillFont = skillFont;
        Title = title;
        Level = level;

        _window = StandardWindow.Create(new Rect(0, 0, ScreenWidth, ActiveAreaHeight), renderBackground: false);

        IReadOnlyList<SecondarySkill> offered = SecondarySkills.WitchsHutSkills;
        SecondarySkillLevel[] skills = new SecondarySkillLevel[offered.Count];
        bool[] flags = new bool[offered.Count];
        bool anySelected = false;

        for (int i = 0; i < skills.Length; ++i)
        {
            skills[i] = new SecondarySkillLevel(offered[i], level);

            flags[i] = selectedSkills.Contains(offered[i]);
            anySelected |= flags[i];
        }

        // 没有选中任何技能时按「全选」处理，参考 :225-230；空表在游戏里表示「按默认」。
        if (!anySelected)
        {
            Array.Fill(flags, true);
        }

        Grid = new EditorSecondarySkillSelectionGrid(skills, flags, _window.ActiveArea.Position,
            new Size(artwork.Frame.Width, artwork.Frame.Height));

        _okay = new ButtonSprite(artwork.Okay.Released, artwork.Okay.Pressed);
        _cancel = new ButtonSprite(artwork.Cancel.Released, artwork.Cancel.Pressed);
        _toggleAllOn = new ButtonSprite(artwork.ToggleAllOn.Released, artwork.ToggleAllOn.Pressed);
        _toggleAllOff = new ButtonSprite(artwork.ToggleAllOff.Released, artwork.ToggleAllOff.Pressed);

        Point okayPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_okay.Area.Width, _okay.Area.Height), WindowPadding.BottomLeft);
        Point cancelPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_cancel.Area.Width, _cancel.Area.Height), WindowPadding.BottomRight);
        Point togglePosition = _window.RenderPosition(Point.Zero,
            new Size(_toggleAllOn.Area.Width, _toggleAllOn.Area.Height), WindowPadding.BottomCenter);

        _okay.SetPosition(okayPosition.X, okayPosition.Y);
        _cancel.SetPosition(cancelPosition.X, cancelPosition.Y);
        _toggleAllOn.SetPosition(togglePosition.X, togglePosition.Y);
        _toggleAllOff.SetPosition(togglePosition.X, togglePosition.Y);

        // 一开始「全选」是灰的：所有技能本来就都是选中的，参考 :262。
        _toggleAllOn.Disable();
    }

    /// <summary>The line above the cells.</summary>
    public string Title { get; }

    /// <summary>The level every skill is offered at.</summary>
    public SkillLevel Level { get; }

    /// <summary>The cells.</summary>
    public EditorSecondarySkillSelectionGrid Grid { get; }

    /// <summary>The active area, <c>background.activeArea()</c> (<c>:239</c>).</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The window the cells and the buttons sit in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The OKAY button; disabled while every skill is off (<c>:353-373</c>).</summary>
    public ButtonSprite Okay => _okay;

    /// <summary>The CANCEL button.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>The Enable All Secondary Skills button, disabled until something is disabled (<c>:262</c>).</summary>
    public ButtonSprite ToggleAllOn => _toggleAllOn;

    /// <summary>The Disable All Secondary Skills button.</summary>
    public ButtonSprite ToggleAllOff => _toggleAllOff;

    /// <summary>Whether the OKAY button is enabled.</summary>
    public bool IsOkayEnabled => _okay.IsEnabled;

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="title">The line above the cells.</param>
    /// <param name="level">The level every skill is offered at, basic to expert.</param>
    /// <param name="selectedSkills">The skills the metadata holds; an empty list means every skill.</param>
    /// <param name="titleFont">The normal yellow font.</param>
    /// <param name="skillFont">The small white font.</param>
    /// <returns>The window.</returns>
    public static EditorSecondarySkillSelectionWindow Create(EditorSecondarySkillSelectionArtwork artwork,
        string title, SkillLevel level, IReadOnlyList<SecondarySkill> selectedSkills, IGlyphSource titleFont,
        IGlyphSource skillFont) =>
        new(artwork, title, level, selectedSkills, titleFont, skillFont);

    /// <summary>
    /// The skills the window hands back, <c>:379-390</c>: the enabled ones; everything at once turns into nothing,
    /// because the game reads an empty list as "use the default".
    /// </summary>
    /// <returns>The result of the selection.</returns>
    public IReadOnlyList<SecondarySkill> GetSelectedSkills()
    {
        List<SecondarySkill> result = new(Grid.Count);

        for (int i = 0; i < Grid.Count; ++i)
        {
            if (Grid.IsSelected(i))
            {
                result.Add(Grid.Skills[i].Skill);
            }
        }

        if (result.Count == Grid.Count)
        {
            result.Clear();
        }

        return result;
    }

    /// <summary>
    /// Turns every skill on, the click on Enable All Secondary Skills (<c>:311-320</c>).
    /// </summary>
    public void EnableAllSkills()
    {
        Grid.SetAllSelected(true);
        _toggleAllOn.Disable();
        _toggleAllOff.Enable();
        RefreshOkayEnabled();
    }

    /// <summary>
    /// Turns every skill off, the click on Disable All Secondary Skills (<c>:321-330</c>).
    /// </summary>
    public void DisableAllSkills()
    {
        Grid.SetAllSelected(false);
        _toggleAllOff.Disable();
        _toggleAllOn.Enable();
        RefreshOkayEnabled();
    }

    /// <summary>
    /// Turns one skill on or off, the click on a cell (<c>:172-177</c>), then updates the OKAY button
    /// (<c>:353-373</c>).
    /// </summary>
    /// <param name="index">Zero based cell number.</param>
    public void ToggleSkill(int index)
    {
        Grid.Toggle(index);
        RefreshOkayEnabled();
    }

    /// <summary>
    /// Draws the whole window, the <c>display.render()</c> calls of <c>:271</c> and <c>:375</c>.
    /// </summary>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame, for the pressed look of the buttons.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Paint(PixelBuffer target, LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(events);

        // 参考只在满屏时把 STONEBAK 抄进活动区（:243-246），端口只有这一个分辨率，所以总是抄。
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

        Grid.Draw(target, _artwork.Frame, _artwork.InactiveFrame, _artwork.GetSkillIcon, _skillFont);

        // 两个开关按钮同一个位置，画在下面的那个永远是看不见的，所以只画启用的那个。
        if (_toggleAllOn.IsEnabled)
        {
            PaintButton(_toggleAllOn, target, events);
        }
        else
        {
            PaintButton(_toggleAllOff, target, events);
        }

        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
    }

    /// <summary>
    /// Disables OKAY while every skill is off, the check of <c>:353-373</c>.
    /// </summary>
    private void RefreshOkayEnabled()
    {
        if (Grid.SelectedCount == 0)
        {
            _okay.Disable();
        }
        else
        {
            _okay.Enable();
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
