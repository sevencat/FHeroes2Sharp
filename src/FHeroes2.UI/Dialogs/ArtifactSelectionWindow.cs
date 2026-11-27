using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The mini artifact pictures a selection window draws, <c>Assets::getImage( ICN::ARTFX, Artifact::IndexSprite32() )</c>
/// of <c>SelectEnumArtifact::getImage()</c> (<c>dialog_selectitems.cpp:563-570</c>).
/// </summary>
public interface IArtifactIcons
{
    /// <summary>One frame of <c>ARTFX</c>, the 32 pixel icon of an artifact.</summary>
    /// <param name="frame">Frame number, <see cref="ArtifactTypes.MiniSpriteIndex"/>.</param>
    /// <returns>The icon, or <see langword="null"/> when the frame is missing.</returns>
    Sprite? GetMiniArtifact(uint frame);
}

/// <summary>
/// The list of artifacts of <c>Dialog::SelectEnumArtifact</c> (<c>dialog_selectitems.cpp:537-572</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every row is 42 pixels high and shows the <c>ARTFX</c> mini icon centred on the reference's
/// <c>middleImageOffsetX = 45 / 2</c> with the name starting at <c>textOffsetX = 50</c> — the constants of
/// <c>SelectEnumArtifact::RedrawItem()</c>.
/// </para>
/// <para>
/// The reference shows <c>ArtifactDialogElement( artifact ).showPopup( Dialog::ZERO )</c> on a right click, i.e. the
/// artifact's name and description in a window. That window is a modal of its own, so the row only records the
/// request here and the caller runs it, exactly like <see cref="SpellSelectionWindow"/> leaves
/// <c>Dialog::SpellInfo()</c> to its caller.
/// </para>
/// </remarks>
public class ArtifactSelectionWindow : ItemSelectionWindow
{
    /// <summary>The heading of the reference's window, <c>_( "Select Artifact:" )</c>.</summary>
    public const string Heading = "Select Artifact:";

    /// <summary>Height of a row, <c>_offsetY = 42</c>.</summary>
    public const int RowHeight = 42;

    /// <summary>Left edge of the icon inside a row, <c>middleImageOffsetX = 45 / 2</c>.</summary>
    public const int IconOffsetX = 45 / 2;

    /// <summary>Left edge of the name inside a row, <c>textOffsetX = 50</c>.</summary>
    public const int TextOffsetX = 50;

    /// <summary>Vertical middle of a row, <c>_offsetY / 2</c>.</summary>
    public const int RowMiddleOffsetY = RowHeight / 2;

    /// <summary>Room kept free on the right of a row for the scroll bar, <c>- 55</c> of <see cref="ItemSelectionWindow"/>.</summary>
    public const int TextRightMargin = 55;

    private readonly List<ItemSelectionEntry> _artifacts;
    private readonly IArtifactIcons _icons;

    private ArtifactType? _infoRequest;

    /// <summary>
    /// Lays out the window and fills the list, the reference's
    /// <c>SelectEnumArtifact( const fheroes2::Size &amp;, std::string )</c> plus <c>SetListContent()</c>
    /// (<c>:537-556</c>, <c>:1180-1186</c>).
    /// </summary>
    /// <param name="artifactIds">Artifact ids to show, in the order the caller wants them.</param>
    /// <param name="icons">The <c>ARTFX</c> mini icons.</param>
    /// <param name="dialogSize">Size of the active area, normally <see cref="ArtifactSelectDialog.DialogSize"/>.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="artifactIds"/>, <paramref name="icons"/>, <paramref name="artwork"/>,
    /// <paramref name="titleFont"/> or <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public ArtifactSelectionWindow(IReadOnlyList<int> artifactIds, IArtifactIcons icons, Size dialogSize,
        ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font, Size? screen = null)
        : base(dialogSize, Heading, string.Empty, artwork, titleFont, font, screen)
    {
        ArgumentNullException.ThrowIfNull(artifactIds);
        ArgumentNullException.ThrowIfNull(icons);

        _icons = icons;
        _artifacts = new List<ItemSelectionEntry>(artifactIds.Count);

        foreach (int id in artifactIds)
        {
            _artifacts.Add(new ItemSelectionEntry(id));
        }

        SetAreaMaxItems(ItemsArea.Height / RowHeight);
        SetListContent(_artifacts);
        UpdateScrollBarImage();
    }

    /// <summary>The rows, in the order the caller gave them, <c>SetListContent()</c> of the reference.</summary>
    public IReadOnlyList<ItemSelectionEntry> Artifacts => _artifacts;

    /// <summary>
    /// The artifact the selection stands on, or <see cref="ArtifactType.Unknown"/> when the list is empty; the value
    /// <c>selectArtifact()</c> returns after the loop.
    /// </summary>
    public ArtifactType SelectedArtifact =>
        IsValid ? (ArtifactType)GetCurrent().Id : ArtifactType.Unknown;

    /// <summary>
    /// Moves the selection to an artifact, the reference's <c>SetCurrent( artifactId )</c>
    /// (<c>dialog_selectitems.cpp:1184-1186</c>).
    /// </summary>
    /// <param name="artifact">The artifact to select.</param>
    public void SelectArtifact(ArtifactType artifact)
    {
        SetCurrent(new ItemSelectionEntry((int)artifact));
        UpdateScrollBarImage();
    }

    /// <summary>
    /// Takes the description request a right click made, and forgets it;
    /// <c>SelectEnumArtifact::ActionListPressRight()</c> (<c>:558-561</c>).
    /// </summary>
    /// <returns>The artifact whose description the caller has to show, or <see langword="null"/> when none was asked.</returns>
    public ArtifactType? TakeInfoRequest()
    {
        ArtifactType? request = _infoRequest;

        _infoRequest = null;

        return request;
    }

    /// <summary>
    /// Paints a row: the mini icon and the name, the reference's <c>SelectEnumArtifact::RedrawItem()</c> over
    /// <c>ItemSelectionWindow::renderItem()</c> (<c>:543-546</c>, <c>:978-1010</c>).
    /// </summary>
    /// <param name="item">The row, whose id is the artifact.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is the selected one.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public override void RedrawItem(ItemSelectionEntry item, int x, int y, bool current, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(output);

        ArtifactType artifact = (ArtifactType)item.Id;

        if (_icons.GetMiniArtifact(ArtifactTypes.MiniSpriteIndex(artifact)) is { } icon)
        {
            ImageOps.BlitSprite(output,
                new Point(x + IconOffsetX - (icon.Width / 2), y + RowMiddleOffsetY - (icon.Height / 2)),
                DecorateIcon(icon, item));
        }

        RenderText(ArtifactTable.Get(artifact).Name, output, new Point(x, y), current);
    }

    /// <summary>
    /// The picture a row paints for an artifact, so a subclass can grey out the ones it does not pick
    /// (<c>MultiArtifactSelection::getImage()</c>, <c>dialog_selectitems.cpp:257-267</c>).
    /// </summary>
    /// <param name="icon">The mini icon of the artifact.</param>
    /// <param name="item">The row.</param>
    /// <returns>The sprite to paint.</returns>
    protected virtual Sprite DecorateIcon(Sprite icon, ItemSelectionEntry item) => icon;

    /// <summary>
    /// Asks for the description of the clicked artifact, the reference's
    /// <c>SelectEnumArtifact::ActionListPressRight()</c> (<c>:558-561</c>).
    /// </summary>
    /// <param name="item">The clicked row, whose id is the artifact.</param>
    public override void ActionListPressRight(ItemSelectionEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _infoRequest = (ArtifactType)item.Id;
    }

    private void RenderText(string text, PixelBuffer output, Point row, bool selected)
    {
        IGlyphSource font = selected ? HighlightFont : Font;
        Rect area = Window.ActiveArea;
        int maxWidth = area.Width - TextOffsetX - TextRightMargin;

        string fitted = GlyphText.FitToOneRow(font, text, maxWidth);
        int y = row.Y - (font.LineHeight / 2) + RowMiddleOffsetY + 2;

        GlyphText.DrawLine(font, output, new Point(row.X + TextOffsetX, y), fitted,
            new Rect(row.X + TextOffsetX, area.Y, maxWidth, area.Height));
    }
}
