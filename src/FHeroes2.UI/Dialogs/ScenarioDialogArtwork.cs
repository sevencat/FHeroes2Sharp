using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork the scenario selection dialog is composed of: the panel of <c>REQSBKG.ICN</c>, the
/// icons and button frames of <c>REQUESTS.ICN</c>, the slider of <c>ESCROLL.ICN</c> and the standard
/// OKAY button.
/// </summary>
/// <remarks>
/// <para>
/// The panel is the whole dialog background: it carries the frame, the groove of the scroll bar and
/// the boxes of the selected scenario, but it also <i>paints the six buttons</i> of the dialog. That
/// is not a quirk of the extraction - the original artwork has them baked in - which is why the
/// reference blits its own button frames over them and why this type needs the same frames: a
/// translated OKAY, a pressed map size filter and a pressed scroll arrow are drawn, not baked.
/// </para>
/// <para>
/// The icons are addressed the way <c>ScenarioListBox</c> addresses them: <c>REQUESTS</c> 20…25
/// count the players of a map, 26…29 are the four map sizes, 30…35 the victory and 36…39 the loss
/// conditions. The map size buttons are the frame pairs 9/10, 11/12, … 17/18 of the same file and
/// the two scroll arrows are 5/6 and 7/8. Every icon of the table is 17x17 pixels, every button
/// frame 56x25. An index the table does not have is reported as <see langword="null"/>, so a
/// trimmed <c>REQUESTS.ICN</c> costs the icons but keeps the dialog.
/// </para>
/// </remarks>
public sealed class ScenarioDialogArtwork
{
    /// <summary>Size of every icon of <c>REQUESTS.ICN</c>, the <c>ICON_SIZE</c> of the reference.</summary>
    public const int IconSize = 17;

    /// <summary>
    /// Index of the icon that stands for zero players; the number of kingdoms of a map is added to
    /// it, so the icons 20…25 count one to six players (<c>19 + Color::Count()</c>).
    /// </summary>
    public const int PlayerCountIconIndex = 19;

    /// <summary>First of the four map size icons (small, medium, large, extra large).</summary>
    public const int MapSizeIconIndex = 26;

    /// <summary>First of the six victory condition icons, <c>30 + condition</c> of the reference.</summary>
    public const int VictoryIconIndex = 30;

    /// <summary>First of the four loss condition icons, <c>36 + condition</c> of the reference.</summary>
    public const int LossIconIndex = 36;

    /// <summary>First released frame of the five map size filter buttons, <c>9</c> of <c>REQUESTS</c>.</summary>
    public const int FilterButtonFirstIndex = 9;

    /// <summary>Number of map size filter buttons: small, medium, large, extra large and all.</summary>
    public const int FilterButtonCount = 5;

    /// <summary>First released frame of the "scroll up" arrow of the list.</summary>
    public const int ScrollUpButtonIndex = 5;

    /// <summary>First released frame of the "scroll down" arrow of the list.</summary>
    public const int ScrollDownButtonIndex = 7;

    private readonly Sprite? _shadow;
    private readonly IReadOnlyList<Sprite> _icons;
    private readonly IReadOnlyList<Sprite>? _mapTypeIcons;

    /// <summary>Composes the artwork from the tables the dialog is drawn with.</summary>
    /// <param name="panel">Frame 0 of <c>REQSBKG.ICN</c>, drawn as the whole dialog.</param>
    /// <param name="shadow">Frame 1 of <c>REQSBKG.ICN</c>, or <see langword="null"/> for no shadow.</param>
    /// <param name="icons">
    /// The sprites of <c>REQUESTS.ICN</c>; an empty table hides every icon and button but keeps the
    /// dialog usable through the hot keys.
    /// </param>
    /// <param name="slider">Frame 3 of <c>ESCROLL.ICN</c>, or <see langword="null"/> for no thumb.</param>
    /// <param name="okayButton">
    /// The OKAY frames, normally 1 and 2 of <c>SYSTEM.ICN</c>; <see langword="null"/> leaves the
    /// baked OKAY of the panel as it is and disables the button.
    /// </param>
    /// <param name="mapTypeIcons">
    /// The three product badges, normally the synthesised <c>MAP_TYPE_ICON</c> icons of the
    /// reference; <see langword="null"/> leaves the map type column empty.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="panel"/> or <paramref name="icons"/> is <see langword="null"/>.
    /// </exception>
    public ScenarioDialogArtwork(
        Sprite panel,
        Sprite? shadow,
        IReadOnlyList<Sprite> icons,
        Sprite? slider = null,
        DialogButtonArtwork? okayButton = null,
        IReadOnlyList<Sprite>? mapTypeIcons = null)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(icons);

        Panel = panel;
        _shadow = shadow;
        _icons = icons;
        Slider = slider;
        OkayButton = okayButton;
        _mapTypeIcons = mapTypeIcons;
    }

    /// <summary>The 377x458 panel that carries the frame, the buttons and the boxes of the dialog.</summary>
    public Sprite Panel { get; }

    /// <summary>The shadow that is blitted below the panel, or <see langword="null"/>.</summary>
    public Sprite? Shadow => _shadow;

    /// <summary>The slider of the scroll bar, or <see langword="null"/> when the list has no thumb.</summary>
    public Sprite? Slider { get; }

    /// <summary>The two looks of the OKAY button, or <see langword="null"/> when it is not available.</summary>
    public DialogButtonArtwork? OkayButton { get; }

    /// <summary>Icon of a map played by <paramref name="count"/> kingdoms (1…6).</summary>
    /// <param name="count">Number of colors that have a kingdom on the map.</param>
    /// <returns>The icon, or <see langword="null"/> when the table does not have it.</returns>
    public Sprite? PlayerCountIcon(int count) => count is >= 1 and <= 6 ? Icon(PlayerCountIconIndex + count) : null;

    /// <summary>Icon of <paramref name="size"/>, or <see langword="null"/> for an unknown size.</summary>
    /// <param name="size">Size class of the map.</param>
    public Sprite? MapSizeIcon(MapSize size) => size switch
    {
        MapSize.Small => Icon(MapSizeIconIndex),
        MapSize.Medium => Icon(MapSizeIconIndex + 1),
        MapSize.Large => Icon(MapSizeIconIndex + 2),
        MapSize.ExtraLarge => Icon(MapSizeIconIndex + 3),
        _ => null,
    };

    /// <summary>Icon of <paramref name="condition"/>, or <see langword="null"/> when it is unknown.</summary>
    /// <param name="condition">Victory condition of the map.</param>
    public Sprite? VictoryIcon(MapVictoryCondition condition) => Icon(VictoryIconIndex + (int)condition);

    /// <summary>Icon of <paramref name="condition"/>, or <see langword="null"/> when it is unknown.</summary>
    /// <param name="condition">Loss condition of the map.</param>
    public Sprite? LossIcon(MapLossCondition condition) => Icon(LossIconIndex + (int)condition);

    /// <summary>Icon of the product <paramref name="version"/> belongs to, or <see langword="null"/>.</summary>
    /// <param name="version">Product the map file belongs to.</param>
    public Sprite? MapTypeIcon(MapVersion version) =>
        _mapTypeIcons is { } icons && (int)version >= 0 && (int)version < icons.Count
            ? icons[(int)version]
            : null;

    /// <summary>Frames of the map size filter button <paramref name="index"/>, or <see langword="null"/>.</summary>
    /// <param name="index">
    /// Zero based filter index in the order of the dialog: small, medium, large, extra large, all.
    /// </param>
    public DialogButtonArtwork? FilterButton(int index) =>
        index >= 0 && index < FilterButtonCount
            ? FramePair(FilterButtonFirstIndex + (2 * index))
            : null;

    /// <summary>Frames of the "scroll up" arrow of the list, or <see langword="null"/>.</summary>
    public DialogButtonArtwork? ScrollUpButton => FramePair(ScrollUpButtonIndex);

    /// <summary>Frames of the "scroll down" arrow of the list, or <see langword="null"/>.</summary>
    public DialogButtonArtwork? ScrollDownButton => FramePair(ScrollDownButtonIndex);

    private Sprite? Icon(int index) => index >= 0 && index < _icons.Count ? _icons[index] : null;

    /// <summary>Released and pressed frame of a button, or <see langword="null"/> when the pair is missing.</summary>
    private DialogButtonArtwork? FramePair(int releasedIndex)
    {
        if (releasedIndex < 0 || releasedIndex + 1 >= _icons.Count)
        {
            return null;
        }

        return new DialogButtonArtwork(_icons[releasedIndex], _icons[releasedIndex + 1]);
    }
}
