using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The eight adventure map buttons, <c>Interface::ButtonsPanel</c> (<c>gui/interface_buttons.h:38-70</c>,
/// <c>gui/interface_buttons.cpp:34-293</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two rows of four 36x36 buttons in a 144x72 block: next hero, hero movement, kingdom, spell on the top row and
/// end turn, adventure, file, system below. Every button owns a released and a pressed frame of
/// <c>ADVBTNS.ICN</c> (or <c>ADVEBTNS.ICN</c> for the evil interface), <c>2 * index</c> and <c>2 * index + 1</c>;
/// the movement button is the only one that swaps its artwork, between the walk (2,3), the action (16,17) and the
/// idle (18,19) pairs.
/// </para>
/// <para>
/// <b>Deviations.</b> The reference keeps an <c>AdventureMap &amp;</c> and runs its <c>Event*()</c> handlers from
/// <see cref="QueueEventProcessing"/>; the port returns a <see cref="ButtonsPanelAction"/> so the orchestrator keeps
/// the events, like <see cref="ControlPanel"/> and <see cref="BorderDrag"/>. <c>_setButtonStatus()</c> asks
/// <c>GetFocusHeroes()</c> and <c>world.GetKingdom()</c>, which the port models in the adventure session; the panel
/// therefore takes the five answers as an <see cref="AdventureButtonStatus"/> through
/// <see cref="SetButtonStatus"/> and never touches the game model. <c>SavePosition()</c> needs the panel offsets of
/// <c>.fh2config</c>, which the port settings do not carry yet.
/// </para>
/// </remarks>
public sealed class ButtonsPanel : BorderWindow
{
    /// <summary>Width of the block, <c>BorderWindow( { 0, 0, 144, 72 } )</c>.</summary>
    public const int PanelWidth = 144;

    /// <summary>Height of the block.</summary>
    public const int PanelHeight = 72;

    /// <summary>Side of one button; the frames of <c>ADVBTNS.ICN</c> are square.</summary>
    public const int ButtonSize = 36;

    /// <summary>Number of buttons, four per row and two rows.</summary>
    public const int ButtonCount = 8;

    /// <summary>Frames the file has to hold: one released and one pressed frame per button.</summary>
    public const int RequiredFrameCount = ButtonCount * 2;

    /// <summary>Buttons of the good interface, <c>ICN::ADVBTNS</c>.</summary>
    public const string GoodInterfaceAssetName = "ADVBTNS.ICN";

    /// <summary>Buttons of the evil interface, <c>ICN::ADVEBTNS</c>.</summary>
    public const string EvilInterfaceAssetName = "ADVEBTNS.ICN";

    /// <summary>Released frame of the walking movement button, <c>setICNIndexes( 2, 3 )</c>.</summary>
    public const int WalkFrame = 2;

    /// <summary>Released frame of the "visit the object under the hero" movement button.</summary>
    public const int ActionFrame = 16;

    /// <summary>Released frame of the idle movement button, also the greyed out one.</summary>
    public const int ResetFrame = 18;

    private readonly IReadOnlyList<Sprite> _frames;
    private readonly ButtonSprite?[] _buttons = new ButtonSprite?[ButtonCount];
    private AdventureButtonStatus _status = AdventureButtonStatus.WithoutHero;

    /// <summary>
    /// Builds the panel from the frames of the button set, <c>ButtonsPanel( AdventureMap &amp; )</c> plus
    /// <c>SetPos()</c>.
    /// </summary>
    /// <param name="display">Frame buffer the floating border is painted into and restored from.</param>
    /// <param name="area">Block the panel occupies, <c>{ 0, 0, 144, 72 }</c> before the interface places it.</param>
    /// <param name="frames">Frames of the button set, in file order.</param>
    /// <param name="artwork">Frame 0 of the stone border, used only when the interface is hidden.</param>
    /// <exception cref="ArgumentException"><paramref name="frames"/> holds fewer than <see cref="RequiredFrameCount"/>.</exception>
    public ButtonsPanel(PixelBuffer display, Rect area, IReadOnlyList<Sprite> frames, Sprite? artwork = null)
        : base(display, area, artwork)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count < RequiredFrameCount)
        {
            throw new ArgumentException(
                $"The button set needs {RequiredFrameCount} frames but holds {frames.Count}.",
                nameof(frames));
        }

        _frames = frames;

        for (int i = 0; i < ButtonCount; ++i)
        {
            _buttons[i] = new ButtonSprite(frames[i * 2], frames[(i * 2) + 1]);
        }

        SetButtonStatus(AdventureButtonStatus.WithoutHero);
        UpdateLayout();
    }

    /// <summary>The eight buttons, indexed by <see cref="AdventureButton"/>.</summary>
    /// <remarks>
    /// The reference keeps one member per button because C++ has no arrays of differently named widgets; the port
    /// keeps the array and exposes the eight areas, which is all the orchestrator ever asked for.
    /// </remarks>
    public IReadOnlyList<ButtonSprite?> Buttons => _buttons;

    /// <summary>State the buttons were last set to, <c>_setButtonStatus()</c>.</summary>
    public AdventureButtonStatus ButtonStatus => _status;

    /// <summary>Area of the next hero button, <c>_nextHeroRect</c>.</summary>
    public Rect NextHeroRect => AreaOf(AdventureButton.NextHero);

    /// <summary>Area of the hero movement button, <c>_heroMovementRect</c>.</summary>
    public Rect HeroMovementRect => AreaOf(AdventureButton.HeroMovement);

    /// <summary>Area of the kingdom button, <c>_kingdomRect</c>.</summary>
    public Rect KingdomRect => AreaOf(AdventureButton.Kingdom);

    /// <summary>Area of the spell button, <c>_spellRect</c>.</summary>
    public Rect SpellRect => AreaOf(AdventureButton.Spell);

    /// <summary>Area of the end turn button, <c>_endTurnRect</c>.</summary>
    public Rect EndTurnRect => AreaOf(AdventureButton.EndTurn);

    /// <summary>Area of the adventure options button, <c>_adventureRect</c>.</summary>
    public Rect AdventureRect => AreaOf(AdventureButton.Adventure);

    /// <summary>Area of the file options button, <c>_fileRect</c>.</summary>
    public Rect FileRect => AreaOf(AdventureButton.File);

    /// <summary>Area of the system options button, <c>_systemRect</c>.</summary>
    public Rect SystemRect => AreaOf(AdventureButton.System);

    /// <summary>
    /// Reads the button set the panel needs, <c>ResetTheme()</c> + <c>Assets::getImage()</c>.
    /// </summary>
    /// <param name="assets">The archive to read from, may be <see langword="null"/>.</param>
    /// <param name="evilInterface">Whether the evil interface is enabled, <c>isEvilInterfaceEnabled()</c>.</param>
    /// <param name="display">Frame buffer the floating border is painted into.</param>
    /// <param name="area">Block the panel occupies.</param>
    /// <param name="artwork">Frame 0 of the stone border, used only when the interface is hidden.</param>
    /// <returns>The panel, or <see langword="null"/> when the archive misses the button set.</returns>
    public static ButtonsPanel? Create(
        AssetBundle? assets,
        bool evilInterface,
        PixelBuffer display,
        Rect area,
        Sprite? artwork = null)
    {
        if (assets is null)
        {
            return null;
        }

        string assetName = evilInterface ? EvilInterfaceAssetName : GoodInterfaceAssetName;

        if (assets.TryReadIcn(assetName) is not { Sprites.Count: >= RequiredFrameCount } buttonSet)
        {
            return null;
        }

        return new ButtonsPanel(display, area, buttonSet.Sprites, artwork);
    }

    /// <summary>Area of <paramref name="button"/> inside the block.</summary>
    /// <param name="button">One of the eight buttons.</param>
    /// <returns>The 36x36 area the button covers.</returns>
    public Rect AreaOf(AdventureButton button)
    {
        Rect block = Area;
        int column = (int)button % 4;
        int row = (int)button / 4;

        return new Rect(
            block.X + (column * ButtonSize),
            block.Y + (row * ButtonSize),
            ButtonSize,
            ButtonSize);
    }

    /// <summary>
    /// Moves the panel and its eight buttons, <c>SetPos()</c>.
    /// </summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    public override void SetPos(int x, int y)
    {
        SetPosition(x, y);
        UpdateLayout();
    }

    /// <summary>
    /// Saves the panel position into the settings file, <c>SavePosition()</c>.
    /// </summary>
    /// <exception cref="NotImplementedException">
    /// Not ported: the reference stores the four panel offsets in <c>.fh2config</c>
    /// (<c>Settings::SetPosButtons()</c>), and <see cref="FHeroes2.Game.System.Settings"/> has no such persistence yet.
    /// </exception>
    public override void SavePosition() =>
        throw new NotImplementedException("ButtonsPanel::SavePosition needs Settings::SetPosButtons.");

    /// <summary>
    /// Enables, disables and re-artworks the buttons, <c>_setButtonStatus()</c>.
    /// </summary>
    /// <param name="status">What the focus hero and his kingdom allow right now.</param>
    /// <remarks>
    /// The movement button shows the walk pair only when the hero can walk his path, the action pair when he stands on
    /// an object he may visit again, and the idle pair otherwise - greyed out when he has no path at all. The next hero
    /// and spell buttons are disabled when nobody can move and when there is no spell to cast.
    /// </remarks>
    public void SetButtonStatus(AdventureButtonStatus status)
    {
        _status = status;

        if (_buttons[(int)AdventureButton.HeroMovement] is { } movement)
        {
            (int released, int pressed) = ResolveMovementFrames(status.Movement);
            movement.SetSprite(_frames[released], _frames[pressed]);
        }

        SetEnabled(AdventureButton.NextHero, status.IsNextHeroEnabled);
        SetEnabled(AdventureButton.HeroMovement, status.IsMovementEnabled);
        SetEnabled(AdventureButton.Spell, status.IsSpellEnabled);
    }

    /// <summary>
    /// Paints the eight buttons, <c>_redraw()</c>.
    /// </summary>
    /// <param name="target">Frame buffer to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// With the interface hidden the reference first redraws the floating stone border of the panel and bails out when
    /// the buttons are switched off; the port draws the border through <see cref="BorderWindow.Redraw"/> and leaves
    /// the "show buttons" switch to the caller, which owns the settings.
    /// </remarks>
    public void Redraw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (HideInterfaceEnabled)
        {
            RedrawBorder();
        }

        foreach (ButtonSprite? button in _buttons)
        {
            button?.Draw(target);
        }
    }

    /// <summary>
    /// Paints the floated stone border of the panel, <c>BorderWindow::Redraw()</c>.
    /// </summary>
    public void RedrawBorder()
    {
        if (HideInterfaceEnabled)
        {
            Redraw();
        }
    }

    /// <summary>
    /// Presses and releases the button the mouse is over, <c>drawOnPressOrRelease</c> of the reference.
    /// </summary>
    /// <param name="events">The frame's events.</param>
    /// <param name="target">Frame buffer the changed button is painted into.</param>
    /// <returns><see langword="true"/> when a button changed and the caller has to present the frame again.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// The reference redraws the whole buttons block and then renders only the rectangle of the button that changed.
    /// The port has no display, so it paints the button straight into <paramref name="target"/> and tells the caller
    /// whether the frame needs presenting.
    /// </remarks>
    public bool UpdatePressState(LocalEvent events, PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(target);

        bool changed = false;

        foreach (AdventureButton button in Enum.GetValues<AdventureButton>())
        {
            if (_buttons[(int)button] is not { } widget)
            {
                continue;
            }

            Rect area = AreaOf(button);

            if (events.IsMouseLeftButtonPressedInArea(area))
            {
                if (!widget.IsPressed && widget.Press())
                {
                    widget.Draw(target);
                    changed = true;
                }
            }
            else if (widget.IsPressed && widget.Release())
            {
                widget.Draw(target);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Reports what the player did with the buttons, <c>queueEventProcessing()</c>.
    /// </summary>
    /// <param name="events">The frame's events.</param>
    /// <returns>The action, or <see langword="null"/> when the click was somewhere else.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The order of the checks is the order of the reference: the five unconditional buttons come first, and the
    /// presses that only open a tooltip are checked afterwards, in the same button order.
    /// </para>
    /// <para>
    /// <b>Hiding.</b> The reference runs <c>BorderWindow::QueueEventProcessing()</c> before this chain and returns
    /// <c>GameMode::CANCEL</c> when the player dragged the floating panel. The port keeps the drag on the base class
    /// (<see cref="BorderWindow.QueueEventProcessing"/>, which hands back a <see cref="BorderDrag"/> the orchestrator
    /// has to own), so the two signatures collide and this one hides the base member with <c>new</c>: call the base
    /// first to get the reference's order.
    /// </para>
    /// </remarks>
    public new ButtonsPanelAction? QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (IsEnabled(AdventureButton.NextHero) && events.MouseClickLeft(NextHeroRect))
        {
            return new ButtonsPanelAction(AdventureButton.NextHero, ButtonsPanelInput.Click);
        }

        if (IsEnabled(AdventureButton.HeroMovement) && events.MouseClickLeft(HeroMovementRect))
        {
            return new ButtonsPanelAction(AdventureButton.HeroMovement, ButtonsPanelInput.Click);
        }

        if (IsEnabled(AdventureButton.HeroMovement)
            && events.IsMouseLeftButtonPressedInArea(HeroMovementRect)
            && _status.HasValidPath
            && events.MouseLongPressLeft(HeroMovementRect))
        {
            return new ButtonsPanelAction(AdventureButton.HeroMovement, ButtonsPanelInput.LongPress);
        }

        if (events.MouseClickLeft(KingdomRect))
        {
            return new ButtonsPanelAction(AdventureButton.Kingdom, ButtonsPanelInput.Click);
        }

        if (IsEnabled(AdventureButton.Spell) && events.MouseClickLeft(SpellRect))
        {
            return new ButtonsPanelAction(AdventureButton.Spell, ButtonsPanelInput.Click);
        }

        if (events.MouseClickLeft(EndTurnRect))
        {
            return new ButtonsPanelAction(AdventureButton.EndTurn, ButtonsPanelInput.Click);
        }

        if (events.MouseClickLeft(AdventureRect))
        {
            return new ButtonsPanelAction(AdventureButton.Adventure, ButtonsPanelInput.Click);
        }

        if (events.MouseClickLeft(FileRect))
        {
            return new ButtonsPanelAction(AdventureButton.File, ButtonsPanelInput.Click);
        }

        if (events.MouseClickLeft(SystemRect))
        {
            return new ButtonsPanelAction(AdventureButton.System, ButtonsPanelInput.Click);
        }

        foreach (AdventureButton button in Enum.GetValues<AdventureButton>())
        {
            if (events.IsMouseRightButtonPressedInArea(AreaOf(button)))
            {
                return new ButtonsPanelAction(button, ButtonsPanelInput.RightClick);
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="button"/> reacts to a click, <c>_button.isEnabled()</c>.</summary>
    /// <param name="button">One of the eight buttons.</param>
    public bool IsEnabled(AdventureButton button) => _buttons[(int)button]?.IsEnabled ?? false;

    /// <summary>
    /// The released and pressed frame of a <see cref="HeroMovementIcon"/>, <c>setICNIndexes()</c>.
    /// </summary>
    /// <param name="icon">What the movement button offers.</param>
    /// <returns>The two frame indices.</returns>
    /// <remarks>
    /// The idle and action pairs live at 16..19, which the original <c>ADVBTNS.ICN</c> of the retail game does not
    /// carry; when they are missing the panel falls back to the walk pair, disabled, so the button keeps its place
    /// instead of drawing nothing.
    /// </remarks>
    private (int Released, int Pressed) ResolveMovementFrames(HeroMovementIcon icon)
    {
        (int released, int pressed) = icon switch
        {
            HeroMovementIcon.Walk => (WalkFrame, WalkFrame + 1),
            HeroMovementIcon.Action => (ActionFrame, ActionFrame + 1),
            _ => (ResetFrame, ResetFrame + 1),
        };

        return pressed < _frames.Count ? (released, pressed) : (WalkFrame, WalkFrame + 1);
    }

    private void SetEnabled(AdventureButton button, bool enabled)
    {
        if (_buttons[(int)button] is not { } widget)
        {
            return;
        }

        if (enabled)
        {
            widget.Enable();
        }
        else
        {
            widget.Disable();
        }
    }

    /// <summary>Spreads the eight buttons over the block, the tail of <c>SetPos()</c>.</summary>
    private void UpdateLayout()
    {
        foreach (AdventureButton button in Enum.GetValues<AdventureButton>())
        {
            if (_buttons[(int)button] is not { } widget)
            {
                continue;
            }

            Rect area = AreaOf(button);

            // ButtonBase measures from the sprite anchor, the reference places the frame's top left corner.
            widget.SetPosition(area.X - widget.Released.OffsetX, area.Y - widget.Released.OffsetY);
        }
    }
}

/// <summary>
/// How one of the eight buttons of the <see cref="ButtonsPanel"/> was used.
/// </summary>
/// <param name="Button">The button.</param>
/// <param name="Kind">What happened to it.</param>
/// <remarks>
/// The reference answers with an <c>fheroes2::GameMode</c> and opens the tooltips itself; the port reports the action
/// so the orchestrator can run the matching <c>Event*()</c> or show its own message box.
/// </remarks>
public readonly record struct ButtonsPanelAction(AdventureButton Button, ButtonsPanelInput Kind);

/// <summary>
/// The three ways a button of the <see cref="ButtonsPanel"/> answers the mouse.
/// </summary>
public enum ButtonsPanelInput
{
    /// <summary>A left click on an enabled button.</summary>
    Click,

    /// <summary>A held left button on the movement button with a valid path, <c>MouseLongPressLeft()</c>.</summary>
    LongPress,

    /// <summary>A right button press that asks for the tooltip of the button.</summary>
    RightClick,
}
