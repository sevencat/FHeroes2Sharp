using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork of the hero quick info box, <c>showQuickInfo( const HeroBase &amp; )</c>
/// (<c>src/fheroes2/dialog/dialog_quickinfo.cpp:693-911</c>).
/// </summary>
/// <remarks>
/// <para>
/// 底板是 <c>QWIKHERO.ICN</c> 帧 0（236x160），小肖像的相框复用 <c>LOCATORS.ICN</c> 帧 22
/// （邪恶界面换 <c>LOCATORE.ICN</c>），士气与幸运共用 <c>MINILKMR.ICN</c> 的 0~5 帧（0~2 幸运、3~5 士气，各是
/// 差/平/好），阵营颜色借用城堡的 <c>FLAG32.ICN</c>——偶数帧是左旗、奇数帧是右旗，六色占 0~11，中立占 12~13。
/// </para>
/// <para>
/// 小肖像本身不在这里：它是每个英雄的 <c>MINIPORT.ICN</c> 帧 <c>portraitId - 1</c>，由调用方按英雄取。
/// </para>
/// </remarks>
public sealed class HeroQuickInfoArtwork
{
    /// <summary>Frame of <c>LOCATORS.ICN</c> that holds the small portrait's frame.</summary>
    public const int PortraitFrameIndex = 22;

    /// <summary>Number of <c>MINILKMR.ICN</c> frames the box uses, <c>0..5</c>.</summary>
    public const int IconCount = 6;

    /// <summary>Number of <c>FLAG32.ICN</c> frames: seven colors, two flags each.</summary>
    public const int FlagCount = 14;

    /// <summary>Frame of the neutral flags, <c>getCastleLeftFlagIcnIndex( PlayerColor::NONE )</c>.</summary>
    public const int NeutralFlagIndex = 12;

    private HeroQuickInfoArtwork(
        Sprite box, Sprite portraitFrame, IReadOnlyList<Sprite> icons, IReadOnlyList<Sprite> flags)
    {
        Box = box;
        PortraitFrame = portraitFrame;
        Icons = icons;
        Flags = flags;
    }

    /// <summary>Frame 0 of <c>QWIKHERO.ICN</c>.</summary>
    public Sprite Box { get; }

    /// <summary>Frame 22 of <c>LOCATORS.ICN</c> (or <c>LOCATORE.ICN</c>).</summary>
    public Sprite PortraitFrame { get; }

    /// <summary>Frames 0~5 of <c>MINILKMR.ICN</c>: bad, neutral and good luck followed by the same for morale.</summary>
    public IReadOnlyList<Sprite> Icons { get; }

    /// <summary>Frames 0~13 of <c>FLAG32.ICN</c>.</summary>
    public IReadOnlyList<Sprite> Flags { get; }

    /// <summary>
    /// Loads the artwork.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <param name="evilInterface">Whether the evil interface is in effect, which switches the frame to <c>LOCATORE</c>.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static HeroQuickInfoArtwork? TryCreate(AssetBundle assets, bool evilInterface)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Qwikhero)) is not { } boxFile || boxFile.Count == 0)
        {
            return null;
        }

        IcnType frameIcn = evilInterface ? IcnType.Locatore : IcnType.Locators;
        if (assets.TryReadIcn(IcnCatalog.GetFileName(frameIcn)) is not { } frameFile ||
            frameFile.Count <= PortraitFrameIndex)
        {
            return null;
        }

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Minilkmr)) is not { } iconFile ||
            iconFile.Count < IconCount)
        {
            return null;
        }

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Flag32)) is not { } flagFile ||
            flagFile.Count < FlagCount)
        {
            return null;
        }

        return new HeroQuickInfoArtwork(
            boxFile.Sprites[0], frameFile.Sprites[PortraitFrameIndex], iconFile.Sprites, flagFile.Sprites);
    }

    /// <summary>
    /// The <c>FLAG32.ICN</c> frame of a color's left flag, <c>getCastleLeftFlagIcnIndex()</c>
    /// (<c>ui_castle.cpp:196-220</c>).
    /// </summary>
    /// <param name="color">The hero's color; <see cref="PlayerColor.None"/> asks for the neutral flag.</param>
    /// <returns>The frame of the left flag, or <see langword="null"/> for a color the reference cannot paint.</returns>
    public static int? GetLeftFlagIndex(PlayerColor color)
    {
        if (color == PlayerColor.None)
        {
            return NeutralFlagIndex;
        }

        foreach (PlayerColor known in PlayerColors.Ordered)
        {
            if (color == known)
            {
                return PlayerColors.GetIndex(known) * 2;
            }
        }

        return null;
    }
}
