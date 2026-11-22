using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The archive-backed lookups of the two skill bars: <c>PRIMSKIL</c> plates, the <c>SWAPWIN</c> window the mini
/// primary icons are cut from, <c>SECSKILL</c> plates and <c>MINISS</c> icons
/// (<c>gui/skill_bar.cpp:65-360</c>).
/// </summary>
/// <remarks>
/// <para>
/// 帧号来自参考：主技能底板是 <c>PRIMSKIL</c> 的 <c>skill - 1</c>（<c>skill_bar.cpp:137</c>），次技能底板与
/// 小图标分别是 <c>SECSKILL</c> 的第 <see cref="SecondarySkills.GetIndexSprite1"/> 帧与 <c>MINISS</c> 的第
/// <see cref="SecondarySkills.GetIndexSprite2"/> 帧（<c>skill_bar.cpp:351</c>），小图标窗口是
/// <c>SWAPWIN</c> 第 0 帧（<c>skill_bar.cpp:93</c>），空底板是 <c>SECSKILL</c> 第 0 帧。
/// </para>
/// <para>
/// 两个「34×34 空槽」与神器袋共用 <see cref="SkillBarArtwork.CreateMiniPlate"/>：参考里
/// <c>ArtifactsBar</c> 与 <c>SecondarySkillsBar</c> 造的是同一块板。
/// </para>
/// </remarks>
public sealed class SkillBarSpriteSet : ISkillBarSprites
{
    private readonly Dictionary<PrimarySkill, Sprite?> _primaryFrames = [];
    private readonly Dictionary<SecondarySkill, Sprite?> _secondaryFrames = [];
    private readonly Dictionary<SecondarySkill, Sprite?> _miniSecondaryFrames = [];
    private Sprite? _swapWindow;
    private bool _swapWindowLoaded;

    /// <summary>Reads the two empty plates, <c>SECSKILL</c> frame 0 and the shared mini plate.</summary>
    public SkillBarSpriteSet()
    {
        SecondarySkillPlate = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Secskill), 0);
        MiniPlate = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsicons), 0) is { } frame
            ? SkillBarArtwork.CreateMiniPlate(frame, EmptyButtons.GetColorId(0xD0, 0xC0, 0x48))
            : null;
    }

    /// <inheritdoc/>
    public Sprite? SecondarySkillPlate { get; }

    /// <inheritdoc/>
    public Sprite? MiniPlate { get; }

    /// <inheritdoc/>
    public Sprite? GetPrimarySkillFrame(PrimarySkill skill)
    {
        if (_primaryFrames.TryGetValue(skill, out Sprite? cached))
        {
            return cached;
        }

        Sprite? sprite = GameAssets.GetImage(
            IcnCatalog.GetFileName(IcnType.Primskil), (int)skill - 1);
        _primaryFrames[skill] = sprite;

        return sprite;
    }

    /// <inheritdoc/>
    public Sprite? GetSwapWindowFrame()
    {
        if (!_swapWindowLoaded)
        {
            _swapWindowLoaded = true;
            _swapWindow = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Swapwin), 0);
        }

        return _swapWindow;
    }

    /// <inheritdoc/>
    public Sprite? GetSecondarySkillFrame(SecondarySkill skill)
    {
        if (_secondaryFrames.TryGetValue(skill, out Sprite? cached))
        {
            return cached;
        }

        Sprite? sprite = GameAssets.GetImage(
            IcnCatalog.GetFileName(IcnType.Secskill), SecondarySkills.GetIndexSprite1(skill));
        _secondaryFrames[skill] = sprite;

        return sprite;
    }

    /// <inheritdoc/>
    public Sprite? GetMiniSecondarySkillFrame(SecondarySkill skill)
    {
        if (_miniSecondaryFrames.TryGetValue(skill, out Sprite? cached))
        {
            return cached;
        }

        Sprite? sprite = GameAssets.GetImage(
            IcnCatalog.GetFileName(IcnType.Miniss), SecondarySkills.GetIndexSprite2(skill));
        _miniSecondaryFrames[skill] = sprite;

        return sprite;
    }
}
