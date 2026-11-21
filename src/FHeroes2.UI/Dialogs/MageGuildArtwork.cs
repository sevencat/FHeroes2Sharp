using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The sprites of the mage guild window: the stone background, the scrolls of a spell row, the bottom bar with
/// its two arrows and the exit button (<c>castle_mageguild.cpp:72-101</c>).
/// </summary>
/// <remarks>
/// <para>
/// The guild <b>building</b> itself is not here: which archive it comes from depends on the town's race and how
/// many levels were built, so <see cref="GetGuildBuildingIcn"/> returns the archive and the caller hands the frame
/// to <see cref="MageGuildDialog.DrawGuildBuilding"/>. Keeping it out means this class has one sprite list per
/// archive instead of six.
/// </para>
/// <para>
/// The two arrows are frames 1-2 and 3-4 of the same <c>SMALLBAR</c> archive as the bar, so they are built from it
/// rather than passed in; the reference constructs them the same way (<c>Button( ..., ICN::SMALLBAR, 1, 2 )</c>).
/// </para>
/// </remarks>
public sealed class MageGuildArtwork
{
    /// <summary>Frame of <c>ICN::STONEBAK</c> and <c>ICN::STONEBAK_EVIL</c> that fills the window.</summary>
    public const int BackgroundFrame = 0;

    /// <summary>Frame of <c>ICN::SMALLBAR</c> that is the status bar's plate.</summary>
    public const int BarFrame = 0;

    /// <summary>Frame of <c>ICN::TOWNWIND</c> that holds a spell.</summary>
    public const int OpenedScrollFrame = 0;

    /// <summary>Frame of <c>ICN::TOWNWIND</c> of an empty slot.</summary>
    public const int FoldedScrollFrame = 1;

    /// <summary>Frames of <c>ICN::SMALLBAR</c>: the previous town arrow.</summary>
    public const int PreviousReleasedFrame = 1;

    /// <summary>Frames of <c>ICN::SMALLBAR</c>: the previous town arrow, pressed.</summary>
    public const int PreviousPressedFrame = 2;

    /// <summary>Frames of <c>ICN::SMALLBAR</c>: the next town arrow.</summary>
    public const int NextReleasedFrame = 3;

    /// <summary>Frames of <c>ICN::SMALLBAR</c>: the next town arrow, pressed.</summary>
    public const int NextPressedFrame = 4;

    /// <summary>How many <c>ICN::SMALLBAR</c> frames the bar and its two arrows need.</summary>
    public const int RequiredBarFrames = 5;

    /// <summary>How many <c>ICN::TOWNWIND</c> frames the two scrolls need.</summary>
    public const int RequiredScrollFrames = 2;

    /// <summary>How many <c>ICN::BUTTON_GUILDWELL_EXIT</c> frames the exit button needs.</summary>
    public const int RequiredExitFrames = 2;

    private MageGuildArtwork(
        Sprite background,
        Sprite openedScroll,
        Sprite foldedScroll,
        Sprite bar,
        DialogButtonArtwork previousTown,
        DialogButtonArtwork nextTown,
        DialogButtonArtwork exit,
        IReadOnlyList<Sprite> spellIcons,
        bool evilInterface)
    {
        Background = background;
        OpenedScroll = openedScroll;
        FoldedScroll = foldedScroll;
        Bar = bar;
        PreviousTown = previousTown;
        NextTown = nextTown;
        Exit = exit;
        SpellIcons = spellIcons;
        EvilInterface = evilInterface;
    }

    /// <summary>The <c>STONEBAK</c> plate that fills the 640 by 480 window.</summary>
    public Sprite Background { get; }

    /// <summary>The scroll of a slot that holds a spell.</summary>
    public Sprite OpenedScroll { get; }

    /// <summary>The scroll of an empty slot.</summary>
    public Sprite FoldedScroll { get; }

    /// <summary>The status bar plate, frame 0 of <c>SMALLBAR</c>.</summary>
    public Sprite Bar { get; }

    /// <summary>The arrow to the previous town, frames 1 and 2 of <c>SMALLBAR</c>.</summary>
    public DialogButtonArtwork PreviousTown { get; }

    /// <summary>The arrow to the next town, frames 3 and 4 of <c>SMALLBAR</c>.</summary>
    public DialogButtonArtwork NextTown { get; }

    /// <summary>The exit button, <c>BUTTON_GUILDWELL_EXIT</c>.</summary>
    /// <remarks>帧来自 <see cref="GetExitButtonIcn"/>，不是与 id 同名的那份档案。</remarks>
    public DialogButtonArtwork Exit { get; }

    /// <summary>
    /// The archive the EXIT button is read from, <c>game_assets.cpp:904-911</c>.
    /// </summary>
    /// <remarks>
    /// <c>ICN::BUTTON_GUILDWELL_EXIT</c> 本身没有档案：用原始资源时它的两帧就是 <c>WELLXTRA</c> 的 0 和 1
    /// （fheroes2 只在「不用原始资源」时才现画这个按钮）。
    /// </remarks>
    /// <returns>The archive to read the released and pressed frames from.</returns>
    public static IcnType GetExitButtonIcn() => IcnType.Wellxtra;

    /// <summary>Every frame of <c>ICN::SPELLS</c>, which the rows index by <c>Spell::IndexSprite()</c>.</summary>
    public IReadOnlyList<Sprite> SpellIcons { get; }

    /// <summary>Whether these frames are the evil interface's.</summary>
    public bool EvilInterface { get; }

    /// <summary>The background archive, <c>isEvilInterface ? STONEBAK_EVIL : STONEBAK</c>.</summary>
    /// <param name="evilInterface">Whether the evil interface is on.</param>
    /// <returns>The archive to read frame 0 from.</returns>
    public static IcnType GetBackgroundIcn(bool evilInterface) =>
        evilInterface ? IcnType.StonebakEvil : IcnType.Stonebak;

    /// <summary>
    /// The archive of the guild's own picture, <c>renderMageGuildBuilding</c>'s race switch
    /// (<c>castle_mageguild.cpp:33-52</c> off <c>Race::MAGEGLD*</c>).
    /// </summary>
    /// <param name="race">The town's race.</param>
    /// <returns>The archive; the wizard's for a random race, which is what the reference's <c>default</c> does.</returns>
    public static IcnType GetGuildBuildingIcn(MapRace race) => race switch
    {
        MapRace.Knight => IcnType.Magegldk,
        MapRace.Barbarian => IcnType.Magegldb,
        MapRace.Sorceress => IcnType.Mageglds,
        MapRace.Warlock => IcnType.Magegldw,
        MapRace.Wizard => IcnType.Magegldz,
        MapRace.Necromancer => IcnType.Magegldn,
        _ => IcnType.Magegldz,
    };

    /// <summary>
    /// The icon of a spell, <c>Assets::getImage( ICN::SPELLS, spell.IndexSprite() )</c>.
    /// </summary>
    /// <param name="spell">The spell; anything but <see cref="SpellType.None"/>.</param>
    /// <returns>The icon, or <see langword="null"/> when the archive is shorter than the table.</returns>
    public Sprite? GetSpellIcon(SpellType spell)
    {
        uint index = new Spell(spell).IndexSprite();
        return index < (uint)SpellIcons.Count ? SpellIcons[(int)index] : null;
    }

    /// <summary>
    /// Collects the window's sprites, the counterpart of <c>StandardWindowChrome.TryCreate</c>.
    /// </summary>
    /// <param name="background">Frames of <see cref="GetBackgroundIcn"/>; only frame 0 is used.</param>
    /// <param name="bar">Frames of <c>SMALLBAR</c>; five of them.</param>
    /// <param name="townWind">Frames of <c>TOWNWIND</c>; the open and the folded scroll.</param>
    /// <param name="spellIcons">Frames of <c>SPELLS</c>.</param>
    /// <param name="exitButton">Frames of <c>BUTTON_GUILDWELL_EXIT</c>; the released and the pressed look.</param>
    /// <param name="evilInterface">Whether the frames are the evil interface's.</param>
    /// <returns>
    /// The artwork, or <see langword="null"/> when an archive is missing frames — the reference would read past the
    /// end of the sprite list instead.
    /// </returns>
    public static MageGuildArtwork? TryCreate(
        IReadOnlyList<Sprite>? background,
        IReadOnlyList<Sprite>? bar,
        IReadOnlyList<Sprite>? townWind,
        IReadOnlyList<Sprite>? spellIcons,
        IReadOnlyList<Sprite>? exitButton,
        bool evilInterface = false)
    {
        if (background is null || background.Count <= BackgroundFrame
            || bar is null || bar.Count < RequiredBarFrames
            || townWind is null || townWind.Count < RequiredScrollFrames
            || spellIcons is null || spellIcons.Count == 0
            || exitButton is null || exitButton.Count < RequiredExitFrames)
        {
            return null;
        }

        return new MageGuildArtwork(
            background[BackgroundFrame],
            townWind[OpenedScrollFrame],
            townWind[FoldedScrollFrame],
            bar[BarFrame],
            new DialogButtonArtwork(bar[PreviousReleasedFrame], bar[PreviousPressedFrame]),
            new DialogButtonArtwork(bar[NextReleasedFrame], bar[NextPressedFrame]),
            new DialogButtonArtwork(exitButton[0], exitButton[1]),
            spellIcons,
            evilInterface);
    }

    /// <summary>Size of the bar plate, which is also the height of both arrows' row.</summary>
    public Size BarSize => new(Bar.Width, Bar.Height);
}
