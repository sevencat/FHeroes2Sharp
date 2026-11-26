using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.App.Scenes;

/// <summary>
/// Serves the pictures of the campaign selection screen out of the game data, <c>Assets::getImage()</c>
/// (<c>game_campaign.cpp:1284-1710</c>).
/// </summary>
/// <remarks>
/// <para>
/// 选关屏要的图分散在十几张 ICN 里：背景与关卡轨道按战役、按进度换（<c>CAMPXTRG</c>、<c>CAMPXTRE</c>、
/// <c>X_CMPEXT</c> 与 <c>CTRACK00-06</c>、<c>X_TRACK1-4</c>），按钮是三套固定的表。参考直接问全局的
/// <c>Assets::getImage</c>，端口把归档拆成这个适配器传进对话框，对话框因此不必知道磁盘。
/// </para>
/// <para>
/// 缺图不抛：一个只装了原版、没装资料片的安装照样能开原版战役，缺的那几张按透明占位返回，并在日志里报一次。
/// 这让「缺素材」退化成「画面少一块」而不是「一开局就崩」，与素材层其它地方的降级方式一致。
/// </para>
/// </remarks>
internal sealed class CampaignScenarioArtwork : ICampaignScenarioArtwork
{
    private readonly AssetBundle _assets;
    private readonly Dictionary<IcnType, IcnSpriteFile?> _archives = [];
    private readonly IReadOnlyDictionary<IcnType, Sprite[]> _generated;

    /// <summary>ICNs already reported as missing, so a screen that redraws every frame logs once.</summary>
    private readonly HashSet<IcnType> _reported = [];

    /// <summary>
    /// Wraps an opened archive.
    /// </summary>
    /// <param name="assets">Where the ICNs come from.</param>
    /// <param name="generated">
    /// Tables the game data does not contain and the caller built instead, keyed by their pseudo ICN id,
    /// usually the campaign button set of <see cref="CampaignButtonsArtwork"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public CampaignScenarioArtwork(AssetBundle assets, IReadOnlyDictionary<IcnType, Sprite[]>? generated = null)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _assets = assets;
        _generated = generated ?? new Dictionary<IcnType, Sprite[]>();
    }

    /// <summary>
    /// One frame of an ICN, <c>Assets::getImage( icnId, frame )</c>.
    /// </summary>
    /// <param name="icnId">The ICN.</param>
    /// <param name="frame">The frame inside it.</param>
    /// <returns>The sprite, or a transparent pixel when the frame is not installed.</returns>
    public Sprite GetIcon(IcnType icnId, int frame)
    {
        if (_generated.TryGetValue(icnId, out Sprite[]? generatedFrames))
        {
            return frame >= 0 && frame < generatedFrames.Length ? generatedFrames[frame] : Blank();
        }

        IcnSpriteFile? file = GetArchive(icnId);

        if (file is null || frame < 0 || frame >= file.Count)
        {
            if (_reported.Add(icnId))
            {
                GameLog.Warn(
                    $"The campaign art '{IcnCatalog.GetFileName(icnId)}' is missing frame {frame}; the selection screen draws a blank there.");
            }

            return Blank();
        }

        return file.Sprites[frame];
    }

    private static Sprite Blank()
    {
        Sprite placeholder = new(1, 1);
        placeholder.Reset();

        return placeholder;
    }

    private IcnSpriteFile? GetArchive(IcnType icnId)
    {
        if (_archives.TryGetValue(icnId, out IcnSpriteFile? cached))
        {
            return cached;
        }

        IcnSpriteFile? file = _assets.TryReadIcn(IcnCatalog.GetFileName(icnId));
        _archives[icnId] = file;

        return file;
    }
}
