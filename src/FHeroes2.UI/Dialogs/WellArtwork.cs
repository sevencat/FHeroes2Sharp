using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork of the well window: <c>ICN::WELLBKG(_EVIL)</c>, the bottom bar, the two buttons and the dwelling
/// pictures of the town's race (<c>castle_well.cpp:379-447</c>).
/// </summary>
/// <remarks>
/// <para>
/// The dwelling pictures are not their own archive: they are frames 19 to 30 of the race's <c>CSTL*</c> file, the
/// same one the town screen draws its buildings from. Two frames per level for the first five levels — plain and
/// upgraded — and three for the last one, which has the second upgrade on top. <see cref="DwellingFrameCount"/> of
/// them are therefore needed, and <see cref="TryCreate"/> refuses anything shorter.
/// </para>
/// <para>
/// The reference also has no evil variant of this window and says so with a <c>TODO</c>; the port keeps the flag so
/// that the background can be swapped when the assets appear, and nothing else changes.
/// </para>
/// </remarks>
public sealed class WellArtwork
{
    /// <summary>Frame of the well background, <c>ICN::WELLBKG</c> entry 0.</summary>
    public const int BackgroundFrame = 0;

    /// <summary>Frame of the bottom bar, <c>ICN::SMALLBAR</c> entry 0.</summary>
    public const int BarFrame = 0;

    /// <summary>First frame of the dwelling pictures inside the <c>CSTL*</c> archive.</summary>
    public const int FirstDwellingFrame = 19;

    /// <summary>How many dwelling frames the window needs, frames 19 to 30 inclusive.</summary>
    public const int DwellingFrameCount = 12;

    private readonly Sprite[] _dwellingFrames;

    private WellArtwork(
        Sprite background,
        Sprite bar,
        DialogButtonArtwork max,
        DialogButtonArtwork exit,
        Sprite[] dwellingFrames,
        bool evilInterface)
    {
        Background = background;
        Bar = bar;
        Max = max;
        Exit = exit;
        _dwellingFrames = dwellingFrames;
        EvilInterface = evilInterface;
    }

    /// <summary>The whole 640 by 480 background of the window.</summary>
    public Sprite Background { get; }

    /// <summary>The bottom bar, drawn in two pieces around the buttons.</summary>
    public Sprite Bar { get; }

    /// <summary>The MAX button, <c>ICN::BUTTON_WELL_MAX</c>.</summary>
    public DialogButtonArtwork Max { get; }

    /// <summary>The EXIT button, <c>ICN::BUTTON_GUILDWELL_EXIT</c>.</summary>
    public DialogButtonArtwork Exit { get; }

    /// <summary>Whether the background came from the evil interface set.</summary>
    public bool EvilInterface { get; }

    /// <summary>The dwelling pictures, frames <see cref="FirstDwellingFrame"/> and up.</summary>
    public IReadOnlyList<Sprite> DwellingFrames => _dwellingFrames;

    /// <summary>The background archive of an interface, <c>ICN::WELLBKG(_EVIL)</c>.</summary>
    /// <param name="evilInterface">Whether to ask for the evil set.</param>
    /// <returns>The archive to read <see cref="BackgroundFrame"/> from.</returns>
    public static IcnType GetBackgroundIcn(bool evilInterface) => evilInterface ? IcnType.WellbkgEvil : IcnType.Wellbkg;

    /// <summary>
    /// The building archive of a race, <c>ICN::getBuildingIcnId()</c> (<c>icn.cpp</c>).
    /// </summary>
    /// <remarks>
    /// The reference has no entry for a multiple or a random race and answers <c>UNKNOWN</c>; the port says the same
    /// with <see cref="IcnType.Unknown"/>, which the loader will report as a missing archive rather than silently
    /// drawing the knight's town.
    /// </remarks>
    /// <param name="race">The town's race.</param>
    /// <returns>The archive that holds the dwelling frames.</returns>
    public static IcnType GetDwellingIcn(MapRace race) => race switch
    {
        MapRace.Knight => IcnType.Cstlkngt,
        MapRace.Barbarian => IcnType.Cstlbarb,
        MapRace.Sorceress => IcnType.Cstlsorc,
        MapRace.Warlock => IcnType.Cstlwrlk,
        MapRace.Wizard => IcnType.Cstlwzrd,
        MapRace.Necromancer => IcnType.Cstlnecr,
        _ => IcnType.Unknown,
    };

    /// <summary>
    /// The frame of a dwelling's picture, <c>castle_well.cpp:407-433</c>.
    /// </summary>
    /// <remarks>
    /// The upgraded frames are only asked for when the upgrade stands: a town that owns the plain dwelling shows the
    /// plain picture even if the upgrade is on its way. The last level has three pictures — plain, the first upgrade
    /// and the second one — because its second upgrade changes the creature as well.
    /// </remarks>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <param name="actualDwelling">The dwelling as built.</param>
    /// <returns>The frame inside the race's <c>CSTL*</c> archive.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not one to six.</exception>
    public static int GetDwellingFrame(int level, BuildingType actualDwelling) => level switch
    {
        1 => FirstDwellingFrame,
        2 => actualDwelling == BuildingType.DwellingUpgrade2 ? 25 : 20,
        3 => actualDwelling == BuildingType.DwellingUpgrade3 ? 26 : 21,
        4 => actualDwelling == BuildingType.DwellingUpgrade4 ? 27 : 22,
        5 => actualDwelling == BuildingType.DwellingUpgrade5 ? 28 : 23,
        6 => actualDwelling switch
        {
            BuildingType.DwellingUpgrade7 => 30,
            BuildingType.DwellingUpgrade6 => 29,
            _ => 24,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "A town has six dwellings."),
    };

    /// <summary>
    /// Takes the sprites of the window, or <see langword="null"/> when one of them is missing or too small.
    /// </summary>
    /// <param name="background">The 640 by 480 background.</param>
    /// <param name="bar">The bottom bar, <c>ICN::SMALLBAR</c> frame 0.</param>
    /// <param name="max">The MAX button, frames 0 and 1 of <c>ICN::BUTTON_WELL_MAX</c>.</param>
    /// <param name="exit">The EXIT button, frames 0 and 1 of <c>ICN::BUTTON_GUILDWELL_EXIT</c>.</param>
    /// <param name="dwellingFrames">Frames 19 to 30 of the race's <c>CSTL*</c> archive.</param>
    /// <param name="evilInterface">Whether the background is the evil one.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive is short.</returns>
    public static WellArtwork? TryCreate(
        Sprite background,
        Sprite bar,
        DialogButtonArtwork max,
        DialogButtonArtwork exit,
        IReadOnlyList<Sprite> dwellingFrames,
        bool evilInterface = false)
    {
        ArgumentNullException.ThrowIfNull(dwellingFrames);

        if (IsMissing(background) || IsMissing(bar))
        {
            return null;
        }

        if (IsMissing(max.Released) || IsMissing(max.Pressed) || IsMissing(exit.Released) || IsMissing(exit.Pressed))
        {
            return null;
        }

        if (dwellingFrames.Count < DwellingFrameCount)
        {
            return null;
        }

        Sprite[] frames = new Sprite[DwellingFrameCount];
        for (int index = 0; index < frames.Length; ++index)
        {
            if (IsMissing(dwellingFrames[index]))
            {
                return null;
            }

            frames[index] = dwellingFrames[index];
        }

        return new WellArtwork(background, bar, max, exit, frames, evilInterface);
    }

    /// <summary>The picture of a dwelling, frames 19 to 30 of the race's archive.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <param name="actualDwelling">The dwelling as built.</param>
    /// <returns>The sprite to draw at the cell's picture anchor.</returns>
    public Sprite GetDwellingSprite(int level, BuildingType actualDwelling) =>
        _dwellingFrames[GetDwellingFrame(level, actualDwelling) - FirstDwellingFrame];

    /// <summary>
    /// Whether a sprite carries nothing to draw; an unloaded frame of a button is <see langword="null"/> here,
    /// because <see cref="DialogButtonArtwork"/> keeps plain sprite references.
    /// </summary>
    private static bool IsMissing(Sprite? sprite) => sprite is null || sprite.Image.IsEmpty;
}
