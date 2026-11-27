using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The artifact row of the artifact victory condition, a port of the two blits of
/// <c>VICTORY_OBTAIN_ARTIFACT</c> (<c>editor_map_specs_window.cpp:1106-1121</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考先贴 <c>RESOURCE</c> 帧 7 当底座（<c>:1106-1109</c>，位置由 <c>_selectConditionRoi</c> 给定，
/// 是面板水平居中、<c>roi.y + 4</c> 的框），再把 <c>ARTIFACT</c> 里
/// <c>Artifact::IndexSprite64()</c> 的 64x64 图 <c>Copy</c> 到底座 <c>+6,+6</c>（<c>:1119-1121</c>）。
/// </para>
/// <para>
/// 差异：参考 <c>Assets::getImage</c> 缺帧给 <c>errorImage</c>；端口在底座缺失时返回
/// <see langword="false"/> 让调用方降级，神器图标缺失时只画空底座。
/// </para>
/// </remarks>
public static class MapSpecsConditionArtifactRow
{
    /// <summary>Frame of <c>RESOURCE</c> that is the empty artifact slot, <c>ICN::RESOURCE, 7</c> (<c>:1106</c>).</summary>
    public const int FrameId = 7;

    /// <summary>Horizontal offset of the artifact inside the slot, <c>:1121</c>.</summary>
    public const int ArtifactOffsetX = 6;

    /// <summary>Vertical offset of the artifact inside the slot, <c>:1121</c>.</summary>
    public const int ArtifactOffsetY = 6;

    /// <summary>
    /// Paints the empty slot at <paramref name="frameRoi"/> and the artifact icon inside it.
    /// </summary>
    /// <param name="artifact">
    /// The artifact the player has to obtain; <see cref="ArtifactType.None"/> leaves the slot empty, which is what the
    /// reference does for an unset artifact id.
    /// </param>
    /// <param name="frameRoi">Area of the slot, the layout's <see cref="MapSpecsConditionPanelLayout.SelectRowRoi"/>.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <returns><see langword="false"/> when <c>RESOURCE</c> frame 7 is missing from the archives and nothing was drawn.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static bool TryDraw(ArtifactType artifact, in Rect frameRoi, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        Sprite? slot = Assets.GetImage("RESOURCE.ICN", FrameId);
        if (slot is null)
        {
            return false;
        }

        ImageOps.BlitSprite(output, new Point(frameRoi.X, frameRoi.Y), slot);

        if (artifact == ArtifactType.Unknown)
        {
            return true;
        }

        uint frame = ArtifactTypes.FullSpriteIndex(artifact);
        Sprite? icon = Assets.GetImage("ARTIFACT.ICN", (int)frame);
        if (icon is not null)
        {
            ImageOps.BlitSprite(
                output, new Point(frameRoi.X + ArtifactOffsetX, frameRoi.Y + ArtifactOffsetY), icon);
        }

        return true;
    }
}
