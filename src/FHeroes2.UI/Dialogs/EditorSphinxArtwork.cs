using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork of the editor's sphinx window, <c>openSphinxWindow()</c> (<c>editor_sphinx_window.cpp:151-471</c>).
/// </summary>
/// <remarks>
/// <para>
/// 窗口要的东西几乎和两个列表窗一样——滚动条（<c>SCROLL</c>/<c>SCROLLE</c> 0..4 帧）、滚动条槽（<c>ADVBORD</c> 0）、
/// 标准 OKAY/CANCEL（<c>SYSTEM</c> 1..4）、增/改/删三个小按钮（<c>CELLWIN</c> 13..18），这些都直接借
/// <see cref="EditorListArtwork"/>；本类只多一样：奖励里神器的槽（<c>RESOURCE</c> 7）。
/// </para>
/// <para>
/// 神器图标要按当前奖励现取，所以只留下 <c>ARTIFACT</c> 的文件名，由 <see cref="GetArtifactIcon"/> 读档后取帧。
/// </para>
/// </remarks>
public sealed class EditorSphinxArtwork
{
    /// <summary>Frame of the artifact slot in <c>RESOURCE</c>, <c>ICN::RESOURCE</c> 7 (<c>:230</c>).</summary>
    public const int ArtifactFrameId = 7;

    /// <summary>Frame of the empty artifact icon, the <c>Artifact( 0 ).IndexSprite64()</c> of <c>:237</c>.</summary>
    public const int EmptyArtifactFrameId = 0;

    private const string ArtifactFile = "ARTIFACT.ICN";

    /// <summary>The scroll bar, the buttons and the three small buttons, as the list windows use them.</summary>
    public EditorListArtwork List { get; private init; } = null!;

    /// <summary>The <c>RESOURCE</c> 7 frame around the artifact icon.</summary>
    public Sprite ArtifactFrame { get; private init; } = null!;

    /// <summary>The empty artifact icon, <c>ARTIFACT</c> 0.</summary>
    public Sprite EmptyArtifact { get; private init; } = null!;

    /// <summary>
    /// Reads the artwork out of the installed game data.
    /// </summary>
    /// <param name="isEvilInterface">Whether the evil look is used.</param>
    /// <returns>The artwork, or <see langword="null"/> when a file or frame it needs is missing.</returns>
    /// <remarks>依赖 <see cref="GameAssets.Current"/> 已指向游戏数据。</remarks>
    public static EditorSphinxArtwork? TryCreate(bool isEvilInterface)
    {
        EditorListArtwork? list = EditorListArtwork.TryCreate(isEvilInterface);
        Sprite? artifactFrame = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Resource), ArtifactFrameId);
        Sprite? emptyArtifact = GameAssets.GetImage(ArtifactFile, EmptyArtifactFrameId);

        if (list is null || artifactFrame is null || emptyArtifact is null)
        {
            return null;
        }

        return new EditorSphinxArtwork
        {
            List = list,
            ArtifactFrame = artifactFrame,
            EmptyArtifact = emptyArtifact
        };
    }

    /// <summary>The icon of an artifact, <c>ARTIFACT</c> at <see cref="ArtifactTypes.FullSpriteIndex"/>.</summary>
    /// <param name="artifact">The artifact; an invalid one gets <see cref="EmptyArtifact"/>.</param>
    /// <returns>The 64x64 icon.</returns>
    public Sprite GetArtifactIcon(ArtifactType artifact)
    {
        if (!ArtifactTypes.IsValid(artifact))
        {
            return EmptyArtifact;
        }

        return GameAssets.GetImage(ArtifactFile, (int)ArtifactTypes.FullSpriteIndex(artifact)) ?? EmptyArtifact;
    }
}
