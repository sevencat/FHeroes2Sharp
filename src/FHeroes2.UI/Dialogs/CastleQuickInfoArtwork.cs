using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork of the castle quick info box, <c>showQuickInfo( const Castle &amp; )</c>
/// (<c>src/fheroes2/dialog/dialog_quickinfo.cpp:597-691</c>).
/// </summary>
/// <remarks>
/// <para>
/// 底板是 <c>QWIKTOWN.ICN</c> 帧 0（236x160），城图标是 <c>LOCATORS.ICN</c> 帧 23（邪恶界面换 <c>LOCATORE.ICN</c>），
/// 真图标由 <see cref="Interface.CastleIconPainter"/> 按种族盖在它上面的 <c>(4, 4)</c> 处；两面旗与英雄框共用
/// <c>FLAG32.ICN</c>，偶数帧左旗、奇数帧右旗，六色占 0~11，中立占 12~13。
/// </para>
/// <para>
/// 与英雄框不同，这里的城图标是「空位图」（一个通用的城轮廓），换种族时只换盖上去的小图标，所以它放在美术里可以复用。
/// </para>
/// </remarks>
public sealed class CastleQuickInfoArtwork
{
    /// <summary>Frame of <c>LOCATORS.ICN</c> that holds the castle icon, <c>ICN::LOCATORS, 23</c>.</summary>
    public const int CastleIconIndex = 23;

    /// <summary>Number of <c>FLAG32.ICN</c> frames: seven colors, two flags each.</summary>
    public const int FlagCount = HeroQuickInfoArtwork.FlagCount;

    private CastleQuickInfoArtwork(Sprite box, Sprite castleIcon, IReadOnlyList<Sprite> flags)
    {
        Box = box;
        CastleIcon = castleIcon;
        Flags = flags;
    }

    /// <summary>Frame 0 of <c>QWIKTOWN.ICN</c>.</summary>
    public Sprite Box { get; }

    /// <summary>Frame 23 of <c>LOCATORS.ICN</c> (or <c>LOCATORE.ICN</c>).</summary>
    public Sprite CastleIcon { get; }

    /// <summary>Frames 0~13 of <c>FLAG32.ICN</c>.</summary>
    public IReadOnlyList<Sprite> Flags { get; }

    /// <summary>
    /// Builds the artwork from already loaded frames, for callers that keep the frames themselves and for tests.
    /// </summary>
    /// <param name="box">Frame 0 of <c>QWIKTOWN.ICN</c>.</param>
    /// <param name="castleIcon">Frame 23 of <c>LOCATORS.ICN</c> (or <c>LOCATORE.ICN</c>).</param>
    /// <param name="flags">Frames 0~13 of <c>FLAG32.ICN</c>.</param>
    /// <returns>The artwork.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="flags"/> has fewer than <see cref="FlagCount"/> frames.</exception>
    public static CastleQuickInfoArtwork Create(Sprite box, Sprite castleIcon, IReadOnlyList<Sprite> flags)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(castleIcon);
        ArgumentNullException.ThrowIfNull(flags);
        ArgumentOutOfRangeException.ThrowIfLessThan(flags.Count, FlagCount);

        return new CastleQuickInfoArtwork(box, castleIcon, flags);
    }

    /// <summary>
    /// Loads the artwork.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <param name="evilInterface">Whether the evil interface is in effect, which switches the icon to <c>LOCATORE</c>.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static CastleQuickInfoArtwork? TryCreate(AssetBundle assets, bool evilInterface)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Qwiktown)) is not { } boxFile || boxFile.Count == 0)
        {
            return null;
        }

        IcnType iconIcn = evilInterface ? IcnType.Locatore : IcnType.Locators;
        if (assets.TryReadIcn(IcnCatalog.GetFileName(iconIcn)) is not { } iconFile ||
            iconFile.Count <= CastleIconIndex)
        {
            return null;
        }

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Flag32)) is not { } flagFile ||
            flagFile.Count < FlagCount)
        {
            return null;
        }

        return new CastleQuickInfoArtwork(boxFile.Sprites[0], iconFile.Sprites[CastleIconIndex], flagFile.Sprites);
    }
}
