using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>The three text faces of the recruit window: the count, the title and everything else.</summary>
/// <param name="NormalWhite">Font of the chosen count, <c>normalWhite</c>.</param>
/// <param name="NormalYellow">Font of the title, <c>normalYellow</c>.</param>
/// <param name="SmallWhite">Font of the prices, the stock line and the MAX/MIN label, <c>smallWhite</c>.</param>
public sealed record RecruitWindowFonts(IGlyphSource NormalWhite, IGlyphSource NormalYellow, IGlyphSource SmallWhite);

/// <summary>What a frame of the recruit window asks its owner to do.</summary>
public enum RecruitWindowAction
{
    /// <summary>Nothing leaves the window; <see cref="RecruitWindowView.Dialog"/> changed at most.</summary>
    None = 0,

    /// <summary>The player bought the chosen stack; <see cref="RecruitDialog.Result"/> holds it.</summary>
    Okay = 1,

    /// <summary>The player left without buying anything.</summary>
    Cancel = 2,

    /// <summary>The count box was clicked; the owner opens the numpad and calls <see cref="RecruitDialog.SetCount"/>.</summary>
    ChooseCount = 3,

    /// <summary>A right click on the unit; the owner shows its description without buttons, <c>ArmyInfo(…, ZERO)</c>.</summary>
    MonsterInfo = 4,

    /// <summary>A left click on the unit; the owner shows its description with buttons, <c>ArmyInfo(…, BUTTONS)</c>.</summary>
    MonsterInfoWithButtons = 5,

    /// <summary>A right click on a button; <see cref="RecruitWindowView.Tooltip"/> holds the text to show.</summary>
    Tooltip = 6,
}

/// <summary>
/// The recruit window, <c>Dialog::RecruitMonster()</c> (<c>dialog_recruit.cpp:253-668</c>): its drawing and the clicks
/// that move <see cref="RecruitDialog"/>.
/// </summary>
/// <remarks>
/// <para>
/// The reference is one modal loop that keeps the screen up to date by blitting over the parts that changed, keeping a
/// copy of the dialog in <c>background</c> to restore them. The port composes the whole window every frame instead:
/// <see cref="Draw"/> reads <see cref="RecruitDialog"/> and paints it, <see cref="ProcessEvents"/> forwards the frame's
/// input to it. The only state here is the widgets that need one and the last tooltip.
/// </para>
/// <para>
/// What the reference hands to other dialogs stays with the owner, as everywhere else in the port: the count box opens
/// the virtual numpad (<see cref="RecruitWindowAction.ChooseCount"/>), the unit opens the creature window
/// (<see cref="RecruitWindowAction.MonsterInfo"/> and <see cref="RecruitWindowAction.MonsterInfoWithButtons"/>) and the
/// tooltips come back as <see cref="Tooltip"/>. Pressing OKAY and CANCEL does fill in
/// <see cref="RecruitDialog.Result"/>, because no other dialog is involved in that.
/// </para>
/// <para>
/// <b>Not ported</b>: the keyboard shortcuts. The reference also reacts to <c>DEFAULT_UP</c>/<c>DOWN</c>/<c>LEFT</c>/
/// <c>RIGHT</c>/<c>OKAY</c>/<c>CANCEL</c> through <c>Game::HotKeyPressEvent()</c>, and the port has no hot-key handling
/// yet. Typing a count with the number keys is ported, because that needs no hot-key table.
/// </para>
/// <para>
/// <see cref="DrawMonsterInfo"/> is the <c>RedrawMonsterInfo()</c> the dwelling window shares
/// (<c>dialog_recruit.cpp:670-694</c> calls it with <c>showTotalSum = false</c>); it stays private here until that
/// window is ported.
/// </para>
/// </remarks>
public sealed class RecruitWindowView
{
    /// <summary>Header of the OKAY tooltip, <c>_( "Okay" )</c>.</summary>
    public const string OkayTooltipHeader = "Okay";

    /// <summary>Body of the OKAY tooltip, <c>_( "Recruit selected monsters." )</c>.</summary>
    public const string OkayTooltipBody = "Recruit selected monsters.";

    /// <summary>Header of the CANCEL tooltip.</summary>
    public const string CancelTooltipHeader = "Cancel";

    /// <summary>Body of the CANCEL tooltip.</summary>
    public const string CancelTooltipBody = "Exit this menu without doing anything.";

    /// <summary>Header of the MAX tooltip, which the reference spells in capitals.</summary>
    public const string MaxTooltipHeader = "MAX";

    /// <summary>Body of the MAX tooltip.</summary>
    public const string MaxTooltipBody = "Select maximum monsters to be recruited.";

    /// <summary>Header of the MIN tooltip, which the reference spells in capitals.</summary>
    public const string MinTooltipHeader = "MIN";

    /// <summary>Body of the MIN tooltip.</summary>
    public const string MinTooltipBody = "Select only 1 monster to be recruited.";

    /// <summary>Offset of the shadow every element of the window casts, <c>{ -5, 5 }</c>.</summary>
    public static readonly Point ShadowOffset = new(-5, 5);

    private readonly RecruitDialog _dialog;
    private readonly RecruitWindowLayout _layout;
    private readonly RecruitWindowArtwork _artwork;
    private readonly RecruitWindowFonts _fonts;
    private readonly StandardWindow _window;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;
    private readonly ButtonSprite _max;
    private readonly ButtonSprite _min;
    private readonly ButtonSprite _up;
    private readonly ButtonSprite _down;
    private readonly ButtonSprite _switchLeft;
    private readonly ButtonSprite _switchRight;
    private readonly TimedEventValidator _upRepeat = new();
    private readonly TimedEventValidator _downRepeat = new();

    private RecruitWindowView(RecruitDialog dialog, RecruitWindowArtwork artwork, RecruitWindowFonts fonts,
        StandardWindow window, RecruitWindowLayout layout)
    {
        _dialog = dialog;
        _artwork = artwork;
        _fonts = fonts;
        _window = window;
        _layout = layout;

        _okay = new ButtonSprite(artwork.OkayButton.Released, artwork.OkayButton.Pressed);
        _cancel = new ButtonSprite(artwork.CancelButton.Released, artwork.CancelButton.Pressed);
        _max = new ButtonSprite(artwork.MaxButton.Released, artwork.MaxButton.Pressed);
        _min = new ButtonSprite(artwork.MinButton.Released, artwork.MinButton.Pressed);
        _up = new ButtonSprite(artwork.UpReleased, artwork.UpPressed);
        _down = new ButtonSprite(artwork.DownReleased, artwork.DownPressed);
        _switchLeft = new ButtonSprite(artwork.SwitchLeft[0], artwork.SwitchLeft[1]);
        _switchRight = new ButtonSprite(artwork.SwitchRight[0], artwork.SwitchRight[1]);

        Point okay = layout.OkayButtonPosition;
        _okay.SetPosition(okay.X, okay.Y);

        Point cancel = layout.CancelButtonPosition(artwork.CancelButton.Released.Width);
        _cancel.SetPosition(cancel.X, cancel.Y);

        // MAX and MIN share one place; only one of them is ever drawn.
        Point maxMin = layout.MaxMinButtonPosition(artwork.MaxButton.Released.Width);
        _max.SetPosition(maxMin.X, maxMin.Y);
        _min.SetPosition(maxMin.X, maxMin.Y);

        Point up = layout.UpButtonPosition;
        _up.SetPosition(up.X, up.Y);

        Point down = layout.DownButtonPosition;
        _down.SetPosition(down.X, down.Y);

        Point left = layout.SwitchLeftPosition;
        _switchLeft.SetPosition(left.X, left.Y);

        Point right = layout.SwitchRightPosition;
        _switchRight.SetPosition(right.X, right.Y);

        Refresh();
    }

    /// <summary>The state this window shows and that its clicks change.</summary>
    public RecruitDialog Dialog => _dialog;

    /// <summary>Where the parts of the window go.</summary>
    public RecruitWindowLayout Layout => _layout;

    /// <summary>The window itself; <see cref="StandardWindow.TotalArea"/> is what a full redraw covers.</summary>
    public StandardWindow Window => _window;

    /// <summary>The OKAY button.</summary>
    public ButtonSprite OkayButton => _okay;

    /// <summary>The CANCEL button.</summary>
    public ButtonSprite CancelButton => _cancel;

    /// <summary>The MAX button, drawn while <see cref="ShowsMaxButton"/> holds.</summary>
    public ButtonSprite MaxButton => _max;

    /// <summary>The MIN button, drawn while <see cref="ShowsMaxButton"/> does not.</summary>
    public ButtonSprite MinButton => _min;

    /// <summary>The up arrow right of the count.</summary>
    public ButtonSprite UpButton => _up;

    /// <summary>The down arrow right of the count.</summary>
    public ButtonSprite DownButton => _down;

    /// <summary>The arrow that steps down the upgrade chain.</summary>
    public ButtonSprite SwitchLeftButton => _switchLeft;

    /// <summary>The arrow that steps up the upgrade chain.</summary>
    public ButtonSprite SwitchRightButton => _switchRight;

    /// <summary>Whether the upgrade arrows are shown, <c>showDowngradedMonsterSwitchButtons</c>.</summary>
    public bool ShowsSwitchArrows => _dialog.CanSwitchMonster;

    /// <summary>
    /// Whether the MAX look of the third button is drawn instead of the MIN one, <c>:400-405</c>: the reference draws
    /// MAX while it is enabled or while nothing at all can be bought, and MIN otherwise.
    /// </summary>
    public bool ShowsMaxButton => _dialog.IsMaxEnabled || _dialog.Maximum == 0;

    /// <summary>The digits in the count box, <c>std::to_string( result )</c>.</summary>
    public string CountText => _dialog.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>What the price line under the count says, <c>"cost (rest)"</c> of the gold.</summary>
    public string GoldLine =>
        $"{_dialog.Payment.Gold.ToString(System.Globalization.CultureInfo.InvariantCulture)} ({_dialog.Remaining.Gold.ToString(System.Globalization.CultureInfo.InvariantCulture)})";

    /// <summary>
    /// What the second price line says when the unit also costs a resource, or <see langword="null"/> when gold is all
    /// it takes.
    /// </summary>
    public string? ExtraResourceLine
    {
        get
        {
            int resource = ExtraResource;

            return resource == Resource.Unknown
                ? null
                : $"{_dialog.Payment.Get(resource).ToString(System.Globalization.CultureInfo.InvariantCulture)} ({_dialog.Remaining.Get(resource).ToString(System.Globalization.CultureInfo.InvariantCulture)})";
        }
    }

    /// <summary>The text of the tooltip the last <see cref="ProcessEvents"/> asked for, if any.</summary>
    public DialogPopup? Tooltip { get; private set; }

    /// <summary>
    /// Builds the window, or returns <see langword="null"/> when the artwork is incomplete.
    /// </summary>
    /// <param name="dialog">The state to show and to change.</param>
    /// <param name="chrome">Pieces of the standard window, <c>SURDRBKG</c>/<c>WINLOSE</c>/<c>STONEBAK</c>.</param>
    /// <param name="artwork">The sprites; <see langword="null"/> is the same as incomplete.</param>
    /// <param name="fonts">The three text faces.</param>
    /// <param name="screen">Screen the window is centred on.</param>
    /// <param name="windowOffsetY">
    /// Pixels the window is pushed down, <c>windowOffsetY</c>; the dwelling room uses it to keep the title visible.
    /// </param>
    /// <returns>The window, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static RecruitWindowView? TryCreate(RecruitDialog dialog, StandardWindowChrome chrome,
        RecruitWindowArtwork? artwork, RecruitWindowFonts fonts, Size? screen = null, int windowOffsetY = 0)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(chrome);
        ArgumentNullException.ThrowIfNull(fonts);

        if (artwork is null)
        {
            return null;
        }

        Size bounds = screen ?? ScreenBuffer.DefaultSize;

        Rect activeArea = new(
            (bounds.Width - RecruitWindowLayout.WindowSize.Width) / 2,
            ((bounds.Height - RecruitWindowLayout.WindowSize.Height) / 2) + windowOffsetY,
            RecruitWindowLayout.WindowSize.Width,
            RecruitWindowLayout.WindowSize.Height);

        StandardWindow window = StandardWindow.Create(chrome, activeArea);
        RecruitWindowLayout layout = RecruitWindowLayout.Create(activeArea);

        return new RecruitWindowView(dialog, artwork, fonts, window, layout);
    }

    /// <summary>
    /// Paints the whole window, the sum of what the reference's constructor draws plus every redraw of its loop
    /// (<c>dialog_recruit.cpp:276-405</c>, <c>:637-661</c>).
    /// </summary>
    /// <param name="target">The buffer to draw on, usually the display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        Refresh();

        _window.Draw(target);

        // The count box and the cost frame are the two elements the reference composes and shadows itself.
        Point countBox = _layout.CountBoxPosition;
        ImageOps.BlitSprite(target, countBox, _artwork.CountBox);
        ImageOps.AddGradientShadow(_artwork.CountBox, target, countBox, ShadowOffset);

        Point costFrame = _layout.CostFramePosition;
        ImageOps.BlitSprite(target, costFrame, _artwork.CostFrame);
        ImageOps.AddGradientShadow(_artwork.CostFrame, target, costFrame, ShadowOffset);

        _okay.DrawShadow(target);
        _cancel.DrawShadow(target);
        _max.DrawShadow(target);
        _up.DrawShadow(target);
        _down.DrawShadow(target);

        if (ShowsSwitchArrows)
        {
            _switchLeft.DrawShadow(target);
            _switchRight.DrawShadow(target);
            _switchLeft.Draw(target);
            _switchRight.Draw(target);
        }

        _up.Draw(target);
        _down.Draw(target);

        DrawMonsterInfo(target);
        DrawCurrentInfo(target);

        _okay.Draw(target);
        _cancel.Draw(target);

        if (ShowsMaxButton)
        {
            _max.Draw(target);
        }
        else if (_min.IsEnabled)
        {
            _min.Draw(target);
        }
    }

    /// <summary>
    /// Reads the frame's input, <c>dialog_recruit.cpp:455-663</c>, and forwards it to <see cref="Dialog"/>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>The action that leaves the window, if any.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public RecruitWindowAction ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Tooltip = null;

        _okay.UpdatePressState(events);
        _cancel.UpdatePressState(events);
        _max.UpdatePressState(events);
        _min.UpdatePressState(events);
        _up.UpdatePressState(events);
        _down.UpdatePressState(events);

        if (ShowsSwitchArrows)
        {
            _switchLeft.UpdatePressState(events);
            _switchRight.UpdatePressState(events);

            if (events.MouseClickLeft(_switchLeft.Area))
            {
                _dialog.SwitchToDowngrade();
            }
            else if (events.MouseClickLeft(_switchRight.Area))
            {
                _dialog.SwitchToUpgrade();
            }
        }

        if (events.IsMouseRightButtonPressedInArea(_layout.MonsterArea))
        {
            return RecruitWindowAction.MonsterInfo;
        }

        if (events.MouseClickLeft(_layout.MonsterArea))
        {
            return RecruitWindowAction.MonsterInfoWithButtons;
        }

        bool heldUp = events.IsMouseLeftButtonPressedAndHeldInArea(_up.Area);
        bool heldDown = events.IsMouseLeftButtonPressedAndHeldInArea(_down.Area);

        // The reference is one else-if chain: a typed digit wins over a click of the same frame.
        if (TryTypeDigit(events))
        {
        }
        else if (events.MouseClickLeft(_layout.CountBoxArea))
        {
            Refresh();

            return RecruitWindowAction.ChooseCount;
        }
        else if ((events.IsMouseWheelUpInArea(_layout.WheelArea) || events.MouseClickLeft(_up.Area)
                     || _upRepeat.IsDelayPassed(heldUp)) && _dialog.Increase())
        {
        }
        else if ((events.IsMouseWheelDownInArea(_layout.WheelArea) || events.MouseClickLeft(_down.Area)
                     || _downRepeat.IsDelayPassed(heldDown)) && _dialog.Decrease())
        {
        }
        else if (_dialog.IsMaxEnabled && events.MouseClickLeft(_max.Area) && _dialog.SetToMaximum())
        {
        }
        else if (_dialog.IsMinEnabled && events.MouseClickLeft(_min.Area) && _dialog.SetToMinimum())
        {
        }
        else if (_dialog.IsOkayEnabled && events.MouseClickLeft(_okay.Area))
        {
            _dialog.Choose(_dialog.Selected);
            Refresh();

            return RecruitWindowAction.Okay;
        }
        else if (events.MouseClickLeft(_cancel.Area))
        {
            _dialog.Choose(null);
            Refresh();

            return RecruitWindowAction.Cancel;
        }
        else if (events.IsMouseRightButtonPressedInArea(_okay.Area))
        {
            Tooltip = new DialogPopup(OkayTooltipHeader, OkayTooltipBody);

            return RecruitWindowAction.Tooltip;
        }
        else if (events.IsMouseRightButtonPressedInArea(_cancel.Area))
        {
            Tooltip = new DialogPopup(CancelTooltipHeader, CancelTooltipBody);

            return RecruitWindowAction.Tooltip;
        }
        else if (_dialog.IsMaxEnabled && events.IsMouseRightButtonPressedInArea(_max.Area))
        {
            Tooltip = new DialogPopup(MaxTooltipHeader, MaxTooltipBody);

            return RecruitWindowAction.Tooltip;
        }
        else if (_dialog.IsMinEnabled && events.IsMouseRightButtonPressedInArea(_min.Area))
        {
            Tooltip = new DialogPopup(MinTooltipHeader, MinTooltipBody);

            return RecruitWindowAction.Tooltip;
        }

        Refresh();

        return RecruitWindowAction.None;
    }

    /// <summary>
    /// Brings the buttons in line with <see cref="Dialog"/>: OKAY greys out at a count of zero, MAX and MIN follow the
    /// model, and the upgrade arrows disappear for a unit that has no downgrade.
    /// </summary>
    /// <remarks>
    /// <see cref="Draw"/> and <see cref="ProcessEvents"/> call this themselves, so a frame loop never needs it.
    /// </remarks>
    public void Refresh()
    {
        Toggle(_okay, _dialog.IsOkayEnabled);
        Toggle(_max, _dialog.IsMaxEnabled);
        Toggle(_min, _dialog.IsMinEnabled);

        bool arrows = ShowsSwitchArrows;

        Toggle(_switchLeft, arrows);
        Toggle(_switchRight, arrows);
    }

    /// <summary>
    /// The resource the unit costs besides gold, or <see cref="Resource.Unknown"/> when gold is all it takes.
    /// </summary>
    private int ExtraResource => _dialog.PaymentPerTroop.GetValidItems() & ~Resource.Gold;

    private void DrawMonsterInfo(PixelBuffer target)
    {
        Sprite sprite = _artwork.Monster;

        int titleWidth = GlyphText.Measure(_fonts.NormalYellow, _dialog.Title);
        GlyphText.DrawLine(_fonts.NormalYellow, target, _layout.TitlePosition(titleWidth), _dialog.Title);

        bool isChampion = _dialog.Monster == MonsterType.Champion;

        ImageOps.BlitSprite(target,
            _layout.MonsterSpritePosition(sprite, MonsterAbilities.IsWide(_dialog.Monster), isChampion), sprite);

        int extraResource = ExtraResource;

        if (extraResource == Resource.Unknown)
        {
            DrawResourcePrice(target, Resource.Gold, _dialog.PaymentPerTroop.Gold, 159, 59, 189, 89);
        }
        else
        {
            DrawResourcePrice(target, Resource.Gold, _dialog.PaymentPerTroop.Gold, 134, 59, 167, 89);

            (int iconX, int iconY) = extraResource switch
            {
                Resource.Crystal => (206, 53),
                Resource.Mercury => (209, 56),
                Resource.Wood => (209, 56),
                Resource.Ore => (209, 56),
                Resource.Sulfur => (209, 59),
                Resource.Gems => (209, 59),
                _ => (209, 59),
            };

            DrawResourcePrice(target, extraResource, _dialog.PaymentPerTroop.Get(extraResource), iconX, iconY, 224, 89);
        }

        Point available = _layout.AvailableCentre(sprite.Height);
        GlyphText.DrawLine(_fonts.SmallWhite, target,
            new Point(available.X - (GlyphText.Measure(_fonts.SmallWhite, _dialog.AvailableText) / 2), available.Y),
            _dialog.AvailableText);

        int captionWidth = GlyphText.Measure(_fonts.SmallWhite, RecruitDialog.NumberToBuyTitle);
        GlyphText.DrawLine(_fonts.SmallWhite, target, _layout.NumberToBuyPosition(captionWidth),
            RecruitDialog.NumberToBuyTitle);
    }

    /// <summary>
    /// One price of the unit: the icon, the amount under it and, for the alternate resource, the icon of the total
    /// sum, <c>RedrawResourceInfo()</c> (<c>dialog_recruit.cpp:136-153</c>).
    /// </summary>
    private void DrawResourcePrice(PixelBuffer target, int resource, int value, int iconX, int iconY, int textX,
        int textY)
    {
        Sprite icon = _artwork.ResourceIcons[ResourceIconIndex.Of(resource)];
        bool showTotalSum = resource != Resource.Gold;

        ImageOps.BlitSprite(target, _layout.ResourceIconPosition(iconX, iconY, showTotalSum), icon);

        string amount = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Point centre = _layout.ResourceTextCentre(textX, textY, showTotalSum);

        GlyphText.DrawLine(_fonts.SmallWhite, target,
            new Point(centre.X - (GlyphText.Measure(_fonts.SmallWhite, amount) / 2), centre.Y), amount);

        if (showTotalSum)
        {
            ImageOps.BlitSprite(target, _layout.TotalSumIconPosition(iconX, iconY), icon);
        }
    }

    /// <summary>
    /// The count and the two price lines under it, <c>RedrawCurrentInfo()</c> (<c>dialog_recruit.cpp:96-134</c>).
    /// </summary>
    /// <remarks>
    /// The reference restores the background under each line before drawing it because it paints over the same pixels
    /// again; a full redraw has nothing to restore.
    /// </remarks>
    private void DrawCurrentInfo(PixelBuffer target)
    {
        Point count = _layout.CountTextCentre;
        GlyphText.DrawLine(_fonts.NormalWhite, target,
            new Point(count.X - (GlyphText.Measure(_fonts.NormalWhite, CountText) / 2), count.Y), CountText);

        bool withSecondResource = ExtraResource != Resource.Unknown;
        Point gold = _layout.GoldLineCentre(withSecondResource);
        GlyphText.DrawLine(_fonts.SmallWhite, target,
            new Point(gold.X - (GlyphText.Measure(_fonts.SmallWhite, GoldLine) / 2), gold.Y), GoldLine);

        if (ExtraResourceLine is { } extra)
        {
            Point line = _layout.ExtraLineCentre;
            GlyphText.DrawLine(_fonts.SmallWhite, target,
                new Point(line.X - (GlyphText.Measure(_fonts.SmallWhite, extra) / 2), line.Y), extra);
        }

        if (_dialog.MaxMinLabel.Length == 0)
        {
            return;
        }

        Point label = _layout.LabelCentre;
        GlyphText.DrawLine(_fonts.SmallWhite, target,
            new Point(label.X - (GlyphText.Measure(_fonts.SmallWhite, _dialog.MaxMinLabel) / 2), label.Y),
            _dialog.MaxMinLabel);
    }

    /// <summary>
    /// Feeds this frame's keystroke to <see cref="RecruitDialog.TypeKey"/>, <c>processIntegerValueTyping()</c>
    /// (<c>dialog_recruit.cpp:571-577</c>, <c>ui_tool.cpp:766-839</c>).
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>Whether a number was accepted, in which case the mouse branches of the frame are skipped.</returns>
    private bool TryTypeDigit(LocalEvent events)
    {
        return events.IsAnyKeyPressed && _dialog.TypeKey(events.PressedKeyValue);
    }

    private static void Toggle(ButtonSprite button, bool show)
    {
        if (show)
        {
            button.Show();
            button.Enable();
        }
        else
        {
            button.Hide();
            button.Disable();
        }
    }
}
