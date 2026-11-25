using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The two portraits of a town's captain, <c>Captain::GetPortrait()</c> (<c>castle/captain.cpp:200-250</c>).
/// </summary>
/// <remarks>
/// <para>
/// 大头像（<c>PORT_BIG</c>）是每个种族一张 <c>PORT0090</c>~<c>PORT0095</c>，再把该色的 <c>HEROFL</c> 旗子
/// （<c>ICN::getFlagIcnId</c> 帧 0）贴到种族自己的偏移上——骑士与女巫是 <c>(43,9)</c>，野蛮人是 <c>(42,8)</c>，
/// 术士 <c>(41,9)</c>，巫师 <c>(42,10)</c>，亡灵 <c>(42,9)</c>。城镇屏左侧立的那张就是它（<c>castle_dialog.cpp:139-140</c>）。
/// </para>
/// <para>
/// 小头像（<c>PORT_SMALL</c>）是 <c>MINICAPT</c> 的 0~5 帧，顺序是骑士、野蛮人、女巫、术士、巫师、亡灵。
/// 速览框里贴的就是它（<c>dialog_quickinfo.cpp:756</c>）。
/// </para>
/// <para>
/// 与 <c>ICN::getCaptainIcnId</c> 不是一回事：那支取的是船长住处那栋建筑（<c>CSTLCAP*</c>），这里要的是人脸。
/// </para>
/// </remarks>
public static class CaptainPortrait
{
    /// <summary>Number of <c>MINICAPT</c> frames, one per playable race.</summary>
    public const int SmallFrameCount = 6;

    /// <summary>
    /// The ICN that holds a race's big captain portrait, <c>GetPortraitIcnId()</c> (<c>captain.cpp:44-62</c>).
    /// </summary>
    /// <param name="race">The race of the town.</param>
    /// <returns>The ICN, or <see langword="null"/> for a race without a captain.</returns>
    public static IcnType? GetBigIcn(MapRace race) => race switch
    {
        MapRace.Knight => IcnType.Port0090,
        MapRace.Barbarian => IcnType.Port0091,
        MapRace.Sorceress => IcnType.Port0092,
        MapRace.Warlock => IcnType.Port0093,
        MapRace.Wizard => IcnType.Port0094,
        MapRace.Necromancer => IcnType.Port0095,
        _ => null,
    };

    /// <summary>
    /// The <c>MINICAPT</c> frame of a race, <c>Captain::GetPortrait( PORT_SMALL )</c>.
    /// </summary>
    /// <param name="race">The race of the town.</param>
    /// <returns>The frame, or <see langword="null"/> for a race without a captain.</returns>
    public static int? GetSmallFrameIndex(MapRace race) => race switch
    {
        MapRace.Knight => 0,
        MapRace.Barbarian => 1,
        MapRace.Sorceress => 2,
        MapRace.Warlock => 3,
        MapRace.Wizard => 4,
        MapRace.Necromancer => 5,
        _ => null,
    };

    /// <summary>
    /// Where the color flag is glued onto the big portrait, <c>GetFlagOffset()</c> (<c>captain.cpp:64-84</c>).
    /// </summary>
    /// <param name="race">The race of the town.</param>
    /// <returns>The offset inside the portrait; the origin for a race without a captain.</returns>
    public static Point GetFlagOffset(MapRace race) => race switch
    {
        MapRace.Knight => new Point(43, 9),
        MapRace.Barbarian => new Point(42, 8),
        MapRace.Sorceress => new Point(43, 9),
        MapRace.Warlock => new Point(41, 9),
        MapRace.Wizard => new Point(42, 10),
        MapRace.Necromancer => new Point(42, 9),
        _ => default,
    };

    /// <summary>
    /// Builds the captain's big portrait with the town's flag already on it.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <param name="captain">The captain whose race and color the portrait shows.</param>
    /// <returns>The portrait, or <see langword="null"/> when the race's portrait is not in the archives.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>参考把旗子贴到底图的一份拷贝上（<c>Blit( flag, portrait, ... )</c>），原件不动；这里同样先克隆。</remarks>
    public static Sprite? TryCreateBig(AssetBundle assets, Captain captain)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(captain);

        if (GetBigIcn(captain.Race) is not { } icn ||
            assets.TryReadIcn(IcnCatalog.GetFileName(icn)) is not { } file ||
            file.Count == 0)
        {
            return null;
        }

        Sprite portrait = file.Sprites[0];
        PixelBuffer image = portrait.Image.Clone();

        IcnType flagIcn = IcnCatalog.GetFlagIcnId(captain.Color);
        if (assets.TryReadIcn(IcnCatalog.GetFileName(flagIcn)) is { } flags && flags.Count > 0)
        {
            ImageOps.BlitSprite(image, GetFlagOffset(captain.Race), flags.Sprites[0]);
        }

        return new Sprite(image);
    }

    /// <summary>
    /// Builds the captain's big portrait through a sprite source instead of the archives, the same portrait as
    /// <see cref="TryCreateBig(AssetBundle, Captain)"/> for the windows that receive their artwork as a
    /// <see cref="IViewWorldSpriteSource"/>.
    /// </summary>
    /// <param name="sprites">The source of the race portrait and the <c>HEROFL</c> flag.</param>
    /// <param name="captain">The captain whose race and color the portrait shows.</param>
    /// <returns>The portrait, or <see langword="null"/> when the race's portrait is not in the source.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Sprite? TryCreateBig(IViewWorldSpriteSource sprites, Captain captain)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(captain);

        if (GetBigIcn(captain.Race) is not { } icn || sprites.GetSprite(icn, 0) is not { } portrait)
        {
            return null;
        }

        PixelBuffer image = portrait.Image.Clone();

        if (sprites.GetSprite(IcnCatalog.GetFlagIcnId(captain.Color), 0) is { } flag)
        {
            ImageOps.BlitSprite(image, GetFlagOffset(captain.Race), flag);
        }

        return new Sprite(image);
    }

    /// <summary>
    /// Reads the captain's small portrait of a race.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <param name="race">The race of the town.</param>
    /// <returns>The portrait, or <see langword="null"/> when the frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static Sprite? TryCreateSmall(AssetBundle assets, MapRace race)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (GetSmallFrameIndex(race) is not { } index ||
            assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Minicapt)) is not { } file ||
            file.Count <= index)
        {
            return null;
        }

        return file.Sprites[index];
    }
}
