using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The empty button artwork a text button is generated from, the <c>emptyButtonIcnID</c> parameter
/// of <c>fheroes2::getTextAdaptedSprite()</c>.
/// </summary>
/// <remarks>
/// The reference passes archive ids around; the port names the templates so the UI layer never
/// depends on numeric ICN ids (see <see cref="ButtonArtwork"/> for the same reasoning).
/// </remarks>
public enum DialogButtonTemplate
{
    /// <summary><c>EMPTY_GOOD_BUTTON</c>, the white lettered button of the good interface.</summary>
    Good,

    /// <summary><c>EMPTY_EVIL_BUTTON</c>, the gray lettered button of the evil interface.</summary>
    Evil,

    /// <summary><c>EMPTY_POL_BUTTON</c>, the short campaign button of the Price of Loyalty theme.</summary>
    Pol,

    /// <summary><c>EMPTY_GUILDWELL_BUTTON</c>, the 25 pixel tall list entry button of a dwelling.</summary>
    GuildWell,

    /// <summary><c>EMPTY_MAP_SELECT_BUTTON</c>, the map size button of the scenario setup.</summary>
    MapSelect,

    /// <summary><c>EMPTY_VERTICAL_GOOD_BUTTON</c>, a button whose text runs down the sprite.</summary>
    VerticalGood,
}

/// <summary>
/// Metrics of one <see cref="DialogButtonTemplate"/>, a direct port of
/// <c>fheroes2::getButtonSpecificValues()</c> (<c>src/fheroes2/gui/ui_button.cpp</c>).
/// </summary>
/// <param name="Template">Template these metrics belong to.</param>
/// <param name="TextPalette">
/// Colouring palette of the label: <see cref="PaletteId.WhiteFont"/> for a white template and
/// <see cref="PaletteId.GrayFont"/> for the gray ones.
/// </param>
/// <param name="TextMargins">
/// Extra room around the measured text, <c>textAreaBorders</c>. It is added to the text width and
/// height before the minimum and maximum are applied.
/// </param>
/// <param name="MinimumTextArea">Smallest text area the template is stretched to.</param>
/// <param name="MaximumTextArea">Largest text area; the text is wrapped to its width.</param>
/// <param name="BackgroundBorders">
/// Padding between the text area and the button edge, <c>backgroundBorders</c>. Added to the text
/// area to get the button size.
/// </param>
/// <param name="ReleasedTextOffset">Top left corner of the label on the released sprite.</param>
/// <param name="PressedTextOffset">Top left corner of the label on the pressed sprite.</param>
public readonly record struct ButtonTemplateMetrics(
    DialogButtonTemplate Template,
    PaletteId TextPalette,
    Size TextMargins,
    Size MinimumTextArea,
    Size MaximumTextArea,
    Size BackgroundBorders,
    Point ReleasedTextOffset,
    Point PressedTextOffset)
{
    /// <summary>
    /// Smallest button the template can produce: the minimum text area plus the background borders.
    /// </summary>
    public Size MinimumButtonSize => new(
        MinimumTextArea.Width + BackgroundBorders.Width,
        MinimumTextArea.Height + BackgroundBorders.Height);

    /// <summary>
    /// Multi lined labels on a good or evil button need a taller minimum area, because the second
    /// line of the button font starts below the plate: the reference adds 16 pixels to the vertical
    /// text margin once the text is taller than 17 pixels.
    /// </summary>
    public bool NeedsExtraHeightForMultilineText => Template is DialogButtonTemplate.Good or DialogButtonTemplate.Evil;

    /// <summary>Returns the metrics of <paramref name="template"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The template is not known.</exception>
    public static ButtonTemplateMetrics Get(DialogButtonTemplate template) => template switch
    {
        DialogButtonTemplate.Good => new ButtonTemplateMetrics(
            template,
            PaletteId.WhiteFont,
            TextMargins: new Size(4 + 4, 1 + 1),
            MinimumTextArea: new Size(86, 15),
            MaximumTextArea: new Size(200, 50),
            BackgroundBorders: new Size(6 + 4, 4 + 4),
            ReleasedTextOffset: new Point(6, 4),
            PressedTextOffset: new Point(5, 5)),
        DialogButtonTemplate.Evil => new ButtonTemplateMetrics(
            template,
            PaletteId.GrayFont,
            TextMargins: new Size(4 + 4, 1 + 1),
            MinimumTextArea: new Size(86, 15),
            MaximumTextArea: new Size(200, 50),
            BackgroundBorders: new Size(6 + 4, 4 + 4),
            ReleasedTextOffset: new Point(6, 4),
            PressedTextOffset: new Point(5, 5)),
        // The empty POL button is 24 pixels tall: (2 + 1) + 15 = 18 and 18 + (3 + 3) = 24.
        DialogButtonTemplate.Pol => new ButtonTemplateMetrics(
            template,
            PaletteId.GrayFont,
            TextMargins: new Size(4 + 4, 2 + 1),
            MinimumTextArea: new Size(87, 18),
            MaximumTextArea: new Size(200, 18),
            BackgroundBorders: new Size(4 + 3, 3 + 3),
            ReleasedTextOffset: new Point(4, 4),
            PressedTextOffset: new Point(3, 5)),
        DialogButtonTemplate.GuildWell => new ButtonTemplateMetrics(
            template,
            PaletteId.WhiteFont,
            TextMargins: new Size(2 + 2, 0),
            MinimumTextArea: new Size(53, 13),
            MaximumTextArea: new Size(200, 14),
            BackgroundBorders: new Size(5 + 3, 2 + 3),
            ReleasedTextOffset: new Point(4, 2),
            PressedTextOffset: new Point(3, 3)),
        DialogButtonTemplate.MapSelect => new ButtonTemplateMetrics(
            template,
            PaletteId.WhiteFont,
            TextMargins: new Size(2 + 2, 1),
            MinimumTextArea: new Size(60, 13),
            MaximumTextArea: new Size(200, 14),
            BackgroundBorders: new Size(6 + 3, 2 + 3),
            ReleasedTextOffset: new Point(6, 3),
            PressedTextOffset: new Point(5, 4)),
        DialogButtonTemplate.VerticalGood => new ButtonTemplateMetrics(
            template,
            PaletteId.WhiteFont,
            TextMargins: new Size(0, 2 + 2),
            MinimumTextArea: new Size(19, 110),
            MaximumTextArea: new Size(19, 200),
            BackgroundBorders: new Size(5 + 4, 4 + 6),
            ReleasedTextOffset: new Point(5, 5),
            PressedTextOffset: new Point(4, 6)),
        _ => throw new ArgumentOutOfRangeException(nameof(template), template, "Unknown button template."),
    };
}
