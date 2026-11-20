using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>What a frame of the gift window asks the caller to do.</summary>
public enum GiftWindowActionKind
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>A recipient box was toggled; the window has already applied it.</summary>
    ToggleRecipient,

    /// <summary>
    /// The player clicked a cell of the planned gift, the window asks for a number: run the count dialog of
    /// <see cref="SelectCountDialog"/> between <see cref="GiftWindowAction.Minimum"/> and
    /// <see cref="GiftWindowAction.Maximum"/> and pass the answer to <see cref="GiftWindowView.ApplySelection"/>.
    /// </summary>
    RequestCount,

    /// <summary>
    /// The window has a sentence to show, <see cref="GiftWindowAction.Message"/>: either "First select recipients!"
    /// or "You cannot select X!". The reference shows it in a <c>Dialog::OK</c> message box.
    /// </summary>
    Message,

    /// <summary>The OKAY button was pressed, the gift may be applied.</summary>
    Confirm,

    /// <summary>The CANCEL button was pressed, the dialog closes without a gift.</summary>
    Cancel,
}

/// <summary>One answer of <see cref="GiftWindowView.ProcessEvents"/>.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Resource">Resource of a count request, one of the <see cref="Resource"/> flags.</param>
/// <param name="Minimum">Lowest number the count dialog accepts; always <c>0</c> in the reference.</param>
/// <param name="Maximum">Highest number the count dialog accepts.</param>
/// <param name="Step">Step of the count dialog, <c>100</c> for gold and <c>1</c> otherwise.</param>
/// <param name="Message">The sentence to show for <see cref="GiftWindowActionKind.Message"/>.</param>
public readonly record struct GiftWindowAction(GiftWindowActionKind Kind, int Resource = 0, int Minimum = 0,
    int Maximum = 0, int Step = 1, string? Message = null)
{
    /// <summary>The answer for "nothing happened".</summary>
    public static GiftWindowAction None => new(GiftWindowActionKind.None);
}

/// <summary>The two fonts of the gift window, <c>FontType::normalWhite()</c> and <c>FontType::smallWhite()</c>.</summary>
/// <param name="NormalWhite">Font of the three captions.</param>
/// <param name="SmallWhite">Font of the resource counts.</param>
public sealed record GiftWindowFonts(IGlyphSource NormalWhite, IGlyphSource SmallWhite);

/// <summary>
/// The gift dialog, <c>Dialog::MakeGiftResource()</c> for the parts that draw and react: the recipient boxes, the two
/// resource rows and the two buttons (<c>dialog_giftresources.cpp:231-346</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps the state in the function: <c>funds1</c> is the kingdom's treasury, <c>funds2</c> the planned
/// gift, <c>selector.recipients</c> the chosen colours and <c>count</c> their number. The port keeps the same three
/// pieces here, so the caller only has to run the message loop and to answer count requests; adding the gift to the
/// world (<c>world.AddEventDate()</c>, <c>kingdom.OddFundsResource()</c>) stays with the caller, because it needs the
/// world and the kingdom and this class only knows the pixels.
/// </para>
/// <para>
/// Two behaviours of the reference are kept on purpose. Toggling a recipient throws the planned gift away and re-reads
/// the treasury (<c>dialog_giftresources.cpp:300-306</c>), since a gift for three players costs three times as much.
/// The OKAY button is disabled while no colour is picked or nothing is planned, which the reference re-evaluates
/// after every event.
/// </para>
/// <para>
/// <b>Not ported</b>: the cursor restorer and the immediate <c>display.render()</c> calls - the scene redraws every
/// frame - and the evil interface switch, which the caller expresses by passing the evil button frames.
/// </para>
/// </remarks>
public sealed class GiftWindowView
{
    /// <summary>Caption of the recipient row, <c>_("Select Recipients")</c>.</summary>
    public const string RecipientsCaption = "Select Recipients";

    /// <summary>Caption of the treasury row, <c>_("Your Funds")</c>.</summary>
    public const string YourFundsCaption = "Your Funds";

    /// <summary>Caption of the gift row, <c>_("Planned Gift")</c>.</summary>
    public const string PlannedGiftCaption = "Planned Gift";

    /// <summary>Sentence shown when a resource is clicked before any recipient is picked.</summary>
    public const string NoRecipientsMessage = "First select recipients!";

    private readonly Func<Funds> _kingdomFunds;
    private readonly IReadOnlyList<PlayerColor> _colors;
    private readonly GiftWindowArtwork _artwork;
    private readonly GiftWindowFonts _fonts;
    private readonly StandardWindow _window;
    private readonly ButtonSprite _okButton;
    private readonly ButtonSprite _cancelButton;
    private readonly IReadOnlyList<Rect> _recipientBoxes;
    private readonly IReadOnlyList<Rect> _giftCells;
    private readonly IReadOnlyList<Rect> _fundsCells;

    private Funds _funds1;
    private Funds _funds2;
    private PlayerColor _recipients;

    private GiftWindowView(Func<Funds> kingdomFunds, IReadOnlyList<PlayerColor> colors, PlayerColor senderColor,
        GiftWindowArtwork artwork, GiftWindowFonts fonts, StandardWindow window, ButtonSprite okButton,
        ButtonSprite cancelButton, IReadOnlyList<Rect> recipientBoxes, IReadOnlyList<Rect> giftCells,
        IReadOnlyList<Rect> fundsCells, in Funds funds)
    {
        _kingdomFunds = kingdomFunds;
        _colors = colors;
        SenderColor = senderColor;
        _artwork = artwork;
        _fonts = fonts;
        _window = window;
        _okButton = okButton;
        _cancelButton = cancelButton;
        _recipientBoxes = recipientBoxes;
        _giftCells = giftCells;
        _fundsCells = fundsCells;
        _funds1 = funds;
        Refresh();
    }

    /// <summary>Area the dialog content goes into.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>Colour of the kingdom that gives, which never appears in the recipient row.</summary>
    public PlayerColor SenderColor { get; }

    /// <summary>The colours that can receive, in map order; one box each.</summary>
    public IReadOnlyList<PlayerColor> RecipientColors => _colors;

    /// <summary>The treasury as shown in the "Your Funds" row; a planned gift is already deducted from it.</summary>
    public Funds YourFunds => _funds1;

    /// <summary>The planned gift as shown in the "Planned Gift" row; the amount is per recipient.</summary>
    public Funds PlannedGift => _funds2;

    /// <summary>The colours picked so far, <c>selector.recipients</c>.</summary>
    public PlayerColor Recipients => _recipients;

    /// <summary>Number of picked colours, the <c>mul</c> the planned gift is multiplied by.</summary>
    public int RecipientCount => PlayerColors.Count(_recipients);

    /// <summary>
    /// Whether OKAY can be pressed: the reference enables it when a colour is picked and something is planned
    /// (<c>dialog_giftresources.cpp:295-298</c>).
    /// </summary>
    public bool CanConfirm => _recipients != PlayerColor.None && _funds2.GetValidItemsCount() > 0;

    /// <summary>Area of the OKAY button.</summary>
    public Rect OkArea => _okButton.Area;

    /// <summary>Area of the CANCEL button.</summary>
    public Rect CancelArea => _cancelButton.Area;

    /// <summary>Where each recipient box sits, in the order of <see cref="RecipientColors"/>.</summary>
    public IReadOnlyList<Rect> RecipientBoxes => _recipientBoxes;

    /// <summary>Where each cell of the "Your Funds" row sits, wood first, gold last.</summary>
    public IReadOnlyList<Rect> FundsCells => _fundsCells;

    /// <summary>Where each cell of the "Planned Gift" row sits, wood first, gold last.</summary>
    public IReadOnlyList<Rect> GiftCells => _giftCells;

    /// <summary>
    /// Lays the dialog out and composes its buttons, <c>Dialog::MakeGiftResource()</c> up to its message loop.
    /// </summary>
    /// <param name="funds">Treasury of the giving kingdom, <c>kingdom.GetFunds()</c>.</param>
    /// <param name="senderColor">Colour of the giving kingdom, <c>kingdom.GetColor()</c>.</param>
    /// <param name="recipientColors">Colours taking part, sender already removed.</param>
    /// <param name="chrome">Border and background artwork of the stone window.</param>
    /// <param name="artwork">Sprites of the dialog.</param>
    /// <param name="fonts">Fonts of the captions and of the counts.</param>
    /// <param name="kingdomFunds">
    /// Re-reads the treasury; the reference calls it whenever the number of recipients changes, because that throws
    /// the planned gift away and starts the split again from the full treasury.
    /// </param>
    /// <param name="screen">Screen to centre on; 640x480 when omitted.</param>
    /// <returns><see langword="null"/> when an argument is missing.</returns>
    public static GiftWindowView? TryCreate(in Funds funds, PlayerColor senderColor,
        IReadOnlyList<PlayerColor>? recipientColors, StandardWindowChrome? chrome, GiftWindowArtwork? artwork,
        GiftWindowFonts? fonts, Func<Funds>? kingdomFunds = null, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(recipientColors);

        if (chrome is null || artwork is null || fonts is null)
        {
            return null;
        }

        PlayerColor seen = PlayerColor.None;

        foreach (PlayerColor color in recipientColors)
        {
            // The reference builds the list as "actual colours without the sender", which cannot hold a duplicate.
            if (color == PlayerColor.None || color == senderColor || PlayerColors.Count(color) != 1
                || (seen & color) != 0)
            {
                throw new ArgumentException(
                    "The recipient list must hold single colours and must not repeat the sender.", nameof(recipientColors));
            }

            seen |= color;
        }

        StandardWindow window = StandardWindow.CreateCentered(chrome, GiftWindowLayout.WindowSize, true, screen);

        ButtonSprite okButton = new(artwork.OkButton.Released, artwork.OkButton.Pressed);
        Point okPosition = GiftWindowLayout.BottomLeftButton(window.ActiveArea,
            new Size(okButton.Area.Width, okButton.Area.Height));
        okButton.SetPosition(okPosition.X, okPosition.Y);

        ButtonSprite cancelButton = new(artwork.CancelButton.Released, artwork.CancelButton.Pressed);
        Point cancelPosition = GiftWindowLayout.BottomRightButton(window.ActiveArea,
            new Size(cancelButton.Area.Width, cancelButton.Area.Height));
        cancelButton.SetPosition(cancelPosition.X, cancelPosition.Y);

        Size cellSize = artwork.CellSize;
        Size boxSize = artwork.BoxSize;

        // The reference re-reads kingdom.GetFunds(); a caller that does not need that can leave the reader out.
        Funds initial = funds;
        Func<Funds> readFunds = kingdomFunds ?? new Func<Funds>(() => initial);

        return new GiftWindowView(
            readFunds,
            recipientColors,
            senderColor,
            artwork,
            fonts,
            window,
            okButton,
            cancelButton,
            GiftWindowLayout.RecipientBoxes(window.ActiveArea, boxSize, recipientColors.Count),
            GiftWindowLayout.ResourceCells(window.ActiveArea, GiftWindowLayout.PlannedGiftY, cellSize),
            GiftWindowLayout.ResourceCells(window.ActiveArea, GiftWindowLayout.YourFundsY, cellSize),
            funds);
    }

    /// <summary>
    /// Reads a frame of input, <c>dialog_giftresources.cpp:289-323</c>: the two buttons, then the recipient boxes, then
    /// the planned gift row.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>What the caller has to do next.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public GiftWindowAction ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Refresh();

        _okButton.UpdatePressState(events);
        _cancelButton.UpdatePressState(events);

        if (events.MouseClickLeft(_cancelButton.Area))
        {
            return new GiftWindowAction(GiftWindowActionKind.Cancel);
        }

        if (events.MouseClickLeft(_okButton.Area))
        {
            // A disabled button draws darkened and swallows its clicks, the Dialog::ZERO the reference keeps until
            // a colour and a gift exist.
            return _okButton.IsEnabled
                ? new GiftWindowAction(GiftWindowActionKind.Confirm)
                : GiftWindowAction.None;
        }

        for (int index = 0; index < _recipientBoxes.Count; ++index)
        {
            if (!events.MouseClickLeft(_recipientBoxes[index]))
            {
                continue;
            }

            SelectRecipient(_colors[index]);

            return new GiftWindowAction(GiftWindowActionKind.ToggleRecipient);
        }

        for (int index = 0; index < _giftCells.Count; ++index)
        {
            if (events.MouseClickLeft(_giftCells[index]))
            {
                return PlannedGiftClicked(ResourceIconIndex.ToResource(index));
            }
        }

        return GiftWindowAction.None;
    }

    /// <summary>
    /// Applies the number the count dialog returned, the tail of <c>ResourceBar::QueueEventProcessing()</c>
    /// (<c>dialog_giftresources.cpp:210-222</c>).
    /// </summary>
    /// <param name="resource">The resource whose cell was clicked.</param>
    /// <param name="count">The number of the planned gift for that resource, per recipient.</param>
    public void ApplySelection(int resource, int count)
    {
        int current = _funds2.Get(resource);
        int difference = count - current;

        if (difference == 0)
        {
            return;
        }

        int multiplier = Math.Max(RecipientCount, 1);
        int cost = multiplier > 1 ? difference * multiplier : difference;

        SetFunds(ref _funds1, resource, _funds1.Get(resource) - cost);
        SetFunds(ref _funds2, resource, current + difference);

        Refresh();
    }

    /// <summary>
    /// Picks or unpicks a recipient, <c>SelectRecipientsColors::QueueEventProcessing()</c>
    /// (<c>dialog_giftresources.cpp:121-139</c>) with the reset behind it (<c>:292-306</c>).
    /// </summary>
    /// <param name="color">The colour of the clicked box.</param>
    public void SelectRecipient(PlayerColor color)
    {
        _recipients = (_recipients & color) != 0 ? _recipients & ~color : _recipients | color;

        _funds1 = _kingdomFunds();
        _funds2.Reset();

        Refresh();
    }

    /// <summary>
    /// Highest number of <paramref name="resource"/> a recipient may get, the <c>max</c> of the reference
    /// (<c>dialog_giftresources.cpp:197</c>): the treasury plus what is already planned, split over the recipients
    /// once more than one is picked.
    /// </summary>
    /// <param name="resource">One of the <see cref="Resource"/> flags.</param>
    /// <returns>The maximum the count dialog may offer.</returns>
    public int MaximumFor(int resource)
    {
        int multiplier = RecipientCount;
        int total = _funds1.Get(resource) + _funds2.Get(resource);

        return multiplier > 1 ? total / multiplier : total;
    }

    /// <summary>Step of the count dialog, <c>100</c> gold and <c>1</c> otherwise (<c>dialog_giftresources.cpp:193</c>).</summary>
    /// <param name="resource">One of the <see cref="Resource"/> flags.</param>
    /// <returns>The step the count dialog counts by.</returns>
    public static int StepFor(int resource) => resource == Resource.Gold ? 100 : 1;

    /// <summary>
    /// Draws the window, the three captions, both resource rows, the recipient boxes and the two buttons
    /// (<c>dialog_giftresources.cpp:239-283</c>).
    /// </summary>
    /// <param name="target">Frame to draw into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        Rect area = ActiveArea;
        DrawCaption(target, area, GiftWindowLayout.RecipientsCaptionY, RecipientsCaption);

        for (int index = 0; index < _recipientBoxes.Count; ++index)
        {
            Rect box = _recipientBoxes[index];
            Sprite sprite = _artwork.Box(_colors[index]);

            ImageOps.BlitSprite(target, new Point(box.X, box.Y), sprite);

            if ((_recipients & _colors[index]) != 0)
            {
                ImageOps.BlitSprite(target, new Point(box.X + 2, box.Y + 2), _artwork.Checkmark);
            }
        }

        DrawCaption(target, area, GiftWindowLayout.YourFundsCaptionY, YourFundsCaption);
        DrawResourceRow(target, _fundsCells, _funds1);

        DrawCaption(target, area, GiftWindowLayout.PlannedGiftCaptionY, PlannedGiftCaption);
        DrawResourceRow(target, _giftCells, _funds2);

        _okButton.DrawShadow(target);
        _cancelButton.DrawShadow(target);
        _okButton.Draw(target);
        _cancelButton.Draw(target);
    }

    /// <summary>Keeps the OKAY button in step with the state, the <c>btnGroup.button( 0 )</c> calls of the reference.</summary>
    private void Refresh()
    {
        if (CanConfirm)
        {
            _okButton.Enable();
        }
        else
        {
            _okButton.Disable();
        }
    }

    /// <summary>Turns a click on the planned row into a count request, <c>ResourceBar::QueueEventProcessing()</c>.</summary>
    private GiftWindowAction PlannedGiftClicked(int resource)
    {
        if (RecipientCount == 0)
        {
            return new GiftWindowAction(GiftWindowActionKind.Message, Message: NoRecipientsMessage);
        }

        int maximum = MaximumFor(resource);

        if (maximum == 0)
        {
            return new GiftWindowAction(
                GiftWindowActionKind.Message,
                Message: $"You cannot select {ResourceText.Of(resource)}!");
        }

        return new GiftWindowAction(GiftWindowActionKind.RequestCount, resource, 0, maximum, StepFor(resource));
    }

    /// <summary>Draws a cell with its count, <c>ResourceBar::RedrawResource()</c>.</summary>
    private void DrawResourceRow(PixelBuffer target, IReadOnlyList<Rect> cells, in Funds funds)
    {
        for (int index = 0; index < cells.Count; ++index)
        {
            Rect cell = cells[index];
            Sprite sprite = _artwork.Cell(index);

            ImageOps.BlitSprite(target, new Point(cell.X, cell.Y), sprite);

            string count = funds.Get(ResourceIconIndex.ToResource(index))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);

            GlyphText.DrawLine(
                _fonts.SmallWhite,
                target,
                new Point(
                    cell.X + ((sprite.Width - GlyphText.Measure(_fonts.SmallWhite, count)) / 2),
                    cell.Y + sprite.Height - 10),
                count);
        }
    }

    /// <summary>Draws a caption centred in the active area, the <c>text.draw()</c> calls of the reference.</summary>
    private void DrawCaption(PixelBuffer target, Rect area, int offsetY, string text) =>
        GlyphText.DrawLine(
            _fonts.NormalWhite,
            target,
            new Point(area.X + ((area.Width - GlyphText.Measure(_fonts.NormalWhite, text)) / 2), area.Y + offsetY),
            text);

    /// <summary>Writes one resource of a funds record, the <c>GetPtr()[type]</c> assignment of the reference.</summary>
    private static void SetFunds(ref Funds funds, int resource, int value)
    {
        switch (resource)
        {
            case Resource.Wood:
                funds.Wood = value;
                break;
            case Resource.Mercury:
                funds.Mercury = value;
                break;
            case Resource.Ore:
                funds.Ore = value;
                break;
            case Resource.Sulfur:
                funds.Sulfur = value;
                break;
            case Resource.Crystal:
                funds.Crystal = value;
                break;
            case Resource.Gems:
                funds.Gems = value;
                break;
            case Resource.Gold:
                funds.Gold = value;
                break;
            default:
                break;
        }
    }
}
