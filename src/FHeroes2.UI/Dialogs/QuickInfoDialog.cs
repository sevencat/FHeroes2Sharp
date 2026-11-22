using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>What a <see cref="QuickInfoDialog"/> describes.</summary>
public enum QuickInfoTargetKind
{
    /// <summary>An adventure map tile.</summary>
    Tile = 0,

    /// <summary>One of the castles of a kingdom.</summary>
    Castle = 1,

    /// <summary>A hero or a captain.</summary>
    Hero = 2,
}

/// <summary>
/// The quick information panel, the seven <c>Dialog::QuickInfo*()</c> entry points of <c>dialog.h:92-101</c>
/// (<c>dialog_quickinfo.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference has a separate function per way the panel is opened - a tile, a castle, a hero, each also with a
/// marker on the radar or at an arbitrary screen position - because each chooses its own anchor. The port keeps one
/// class: what differs between the variants is which field is filled and where the panel goes, not what it says.
/// </para>
/// <para>
/// The model only carries the target and the anchor; the boxes themselves are <see cref="CastleQuickInfoView"/>
/// (castle, done) and <see cref="HeroQuickInfoView"/> (hero, done). The tile box - the big text panel that describes
/// a map object - is <b>not ported yet</b>: it is the <c>getQuickInfoText()</c> table of
/// <c>dialog_quickinfo.cpp:455-577</c> plus the <c>QWIKINFO.ICN</c> frame, and it needs the object descriptions of
/// the map objects, which live in <c>Game/Objects</c>. Until then <see cref="CreateForTile"/> has no view.
/// </para>
/// </remarks>
public sealed class QuickInfoDialog
{
    private QuickInfoDialog(
        QuickInfoTargetKind targetKind,
        Tile? tile,
        CastleTown? castle,
        Hero? hero,
        Rect? indicationArea,
        Point? position,
        bool ignoreCustomBottomInfo)
    {
        TargetKind = targetKind;
        Tile = tile;
        Castle = castle;
        Hero = hero;
        IndicationArea = indicationArea;
        Position = position;
        IgnoreCustomBottomInfo = ignoreCustomBottomInfo;
    }

    /// <summary>
    /// Builds the panel for a tile, <c>Dialog::QuickInfo( const Maps::Tile &amp; )</c>.
    /// </summary>
    /// <param name="tile">The tile to describe.</param>
    /// <returns>The model.</returns>
    public static QuickInfoDialog CreateForTile(Tile tile) =>
        new(QuickInfoTargetKind.Tile, tile, null, null, null, null, false);

    /// <summary>
    /// Builds the panel for a castle, <c>Dialog::QuickInfo( const Castle &amp; )</c>, and its two variants
    /// <c>QuickInfoWithIndicationOnRadar()</c> and <c>QuickInfoAtPosition()</c>.
    /// </summary>
    /// <param name="castle">The castle to describe.</param>
    /// <param name="indicationArea">Area to mark on the radar, <c>indicationRoi</c> of the reference, if any.</param>
    /// <param name="position">Screen position for the panel, <c>position</c> of the reference, if it is fixed.</param>
    /// <returns>The model.</returns>
    public static QuickInfoDialog CreateForCastle(CastleTown castle, Rect? indicationArea = null, Point? position = null) =>
        new(QuickInfoTargetKind.Castle, null, castle, null, indicationArea, position, false);

    /// <summary>
    /// Builds the panel for a hero, <c>Dialog::QuickInfo( const HeroBase &amp;, const std::optional&lt;bool&gt; )</c>,
    /// and its two variants <c>QuickInfoWithIndicationOnRadar()</c> and <c>QuickInfoAtPosition()</c>.
    /// </summary>
    /// <param name="hero">The hero to describe.</param>
    /// <param name="ignoreCustomBottomInfo">
    /// Whether the custom status line of the hero is ignored, the <c>ignoreCustomBottomInfo</c> optional of the
    /// reference: <see langword="null"/> keeps the reference default, <see langword="false"/>.
    /// </param>
    /// <param name="indicationArea">Area to mark on the radar, if any.</param>
    /// <param name="position">Screen position for the panel, if it is fixed.</param>
    /// <returns>The model.</returns>
    public static QuickInfoDialog CreateForHero(
        Hero hero,
        bool? ignoreCustomBottomInfo = null,
        Rect? indicationArea = null,
        Point? position = null) =>
        new(QuickInfoTargetKind.Hero, null, null, hero, indicationArea, position, ignoreCustomBottomInfo ?? false);

    /// <summary>What the panel describes.</summary>
    public QuickInfoTargetKind TargetKind { get; }

    /// <summary>The tile, when <see cref="TargetKind"/> is <see cref="QuickInfoTargetKind.Tile"/>.</summary>
    public Tile? Tile { get; }

    /// <summary>The castle, when <see cref="TargetKind"/> is <see cref="QuickInfoTargetKind.Castle"/>.</summary>
    public CastleTown? Castle { get; }

    /// <summary>The hero, when <see cref="TargetKind"/> is <see cref="QuickInfoTargetKind.Hero"/>.</summary>
    public Hero? Hero { get; }

    /// <summary>Area to mark on the radar, if the panel was opened from the radar.</summary>
    public Rect? IndicationArea { get; }

    /// <summary>Fixed screen position of the panel, if it was opened at one.</summary>
    public Point? Position { get; }

    /// <summary>Whether the custom status line of the hero is ignored.</summary>
    public bool IgnoreCustomBottomInfo { get; }

    /// <summary>
    /// Builds the drawable box of a castle panel, the <c>showQuickInfo( const Castle &amp; )</c> of the reference.
    /// </summary>
    /// <param name="artwork">Frames of the box.</param>
    /// <param name="font">Font of every string, <c>FontType::smallWhite()</c> in the reference.</param>
    /// <param name="sprites">Where the army line takes its portraits from.</param>
    /// <param name="icons">Where the castle icon takes <c>LOCATORS</c> from.</param>
    /// <param name="evilInterface">Whether the evil interface is in effect.</param>
    /// <param name="detailedView">Whether the viewer may see the garrison, <c>isDetailedView</c> of the reference.</param>
    /// <param name="thievesGuildCount">Power of the viewer's thieves' guild, <c>GetCountThievesGuild()</c>.</param>
    /// <param name="cursor">Cursor of the mouse, which the box is placed next to when <see cref="Position"/> is unset.</param>
    /// <param name="interfaceArea">Area the box has to stay inside.</param>
    /// <returns>The box, or <see langword="null"/> for another target kind or when the frame cannot fit.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public CastleQuickInfoView? TryCreateCastleView(
        CastleQuickInfoArtwork artwork,
        IGlyphSource font,
        IMiniMonsterSprites sprites,
        IViewWorldSpriteSource icons,
        bool evilInterface,
        bool detailedView,
        uint thievesGuildCount,
        Point cursor,
        Rect interfaceArea)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(icons);

        return Castle is not { } castle
            ? null
            : CastleQuickInfoView.TryCreate(
                castle,
                detailedView,
                thievesGuildCount,
                artwork,
                font,
                sprites,
                icons,
                evilInterface,
                cursor,
                interfaceArea,
                Position);
    }
}
