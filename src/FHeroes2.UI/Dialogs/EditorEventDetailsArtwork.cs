using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork of the editor's event details window, <c>eventDetailsDialog()</c>
/// (<c>editor_event_details_window.cpp:88-240</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考直接建三个元素：<c>ArtifactDialogElement( artifact )</c> 要 <c>RESOURCE</c> 7 的框与 <c>ARTIFACT</c>
/// 对应帧的图标；<c>ExperienceDialogElement( 0 )</c> 要 <c>EXPMRL</c> 4；<c>SecondarySkillDialogElement</c>
/// 要 <c>SECSKILL</c> 15 的框与 <c>SECSKILL</c> 技能帧。删除按钮是 <c>CELLWIN</c> 17/18，「首次访问后取消」的
/// 框与勾是 <c>CELLWIN</c> 1/2，颜色框由 <see cref="FHeroes2.Game.Editor.EditorUiHelper"/> 现取。
/// </para>
/// <para>
/// 图标的帧要按当前事件现取（换神器就换图），所以不预先全部载入：本类留下归档里对应的文件名，由
/// <see cref="GetArtifactIcon"/> / <see cref="GetSecondarySkillIcon"/> 在读档后取一帧。
/// </para>
/// <para>
/// 差异：参考的 <c>ICN::CELLWIN_EVIL</c> 是启动期从 <c>CELLWIN</c> 生成的，那个生成器还没移植
/// （见 <see cref="FHeroes2.Game.Editor.EditorUiHelper.DrawCheckboxWithText"/> 的说明），所以
/// <c>CELLWIN</c> 的框、勾、删除按钮一律用普通版；<paramref name="isEvilInterface"/> 只影响 OKAY/CANCEL。
/// </para>
/// </remarks>
public sealed class EditorEventDetailsArtwork
{
    /// <summary>Frame of the artifact slot in <c>RESOURCE</c>, <c>ICN::RESOURCE</c> 7.</summary>
    public const int ArtifactFrameId = 7;

    /// <summary>Frame of the experience icon in <c>EXPMRL</c>, the <c>4</c> of <c>ExperienceDialogElement</c>.</summary>
    public const int ExperienceFrameId = 4;

    /// <summary>Frame of the secondary skill slot in <c>SECSKILL</c>, the <c>15</c> of <c>SecondarySkillDialogElement</c>.</summary>
    public const int SecondarySkillFrameId = 15;

    /// <summary>Frame of the empty artifact icon, the <c>0</c> the reference falls back to.</summary>
    public const int EmptyArtifactFrameId = 0;

    /// <summary>Frame of the empty secondary skill icon, the <c>0</c> the reference falls back to.</summary>
    public const int EmptySecondarySkillFrameId = 0;

    /// <summary>Frame of the checkbox box in <c>CELLWIN</c>, the <c>1</c> of <c>drawCheckboxWithText</c>.</summary>
    public const int CheckboxFrameId = 1;

    /// <summary>Frame of the check mark in <c>CELLWIN</c>, the <c>2</c> of <c>drawCheckboxWithText</c>.</summary>
    public const int CheckmarkFrameId = 2;

    /// <summary>Frame of the released delete button in <c>CELLWIN</c>, <c>buttonDeleteArtifact</c>.</summary>
    public const int DeleteReleasedFrameId = 17;

    /// <summary>Frame of the pressed delete button in <c>CELLWIN</c>, <c>buttonDeleteArtifact</c>.</summary>
    public const int DeletePressedFrameId = 18;

    private const string ArtifactFile = "ARTIFACT.ICN";

    private const string SecondarySkillFile = "SECSKILL.ICN";

    /// <summary>The <c>RESOURCE</c> 7 frame around the artifact icon.</summary>
    public Sprite ArtifactFrame { get; private init; } = null!;

    /// <summary>The <c>EXPMRL</c> 4 experience icon.</summary>
    public Sprite ExperienceIcon { get; private init; } = null!;

    /// <summary>The <c>SECSKILL</c> 15 frame around the skill icon.</summary>
    public Sprite SecondarySkillFrame { get; private init; } = null!;

    /// <summary>The empty artifact icon, <c>ARTIFACT</c> 0.</summary>
    public Sprite EmptyArtifact { get; private init; } = null!;

    /// <summary>The empty secondary skill icon, <c>SECSKILL</c> 0.</summary>
    public Sprite EmptySecondarySkill { get; private init; } = null!;

    /// <summary>The delete button of the artifact and of the skill, <c>CELLWIN</c> 17/18.</summary>
    public (Sprite Released, Sprite Pressed) DeleteButton { get; private init; }

    /// <summary>The box of the recurring-event checkbox, <c>CELLWIN</c> 1.</summary>
    public Sprite CheckboxBox { get; private init; } = null!;

    /// <summary>The check mark of the recurring-event checkbox, <c>CELLWIN</c> 2.</summary>
    public Sprite Checkmark { get; private init; } = null!;

    /// <summary>The OKAY button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Okay { get; private init; }

    /// <summary>The CANCEL button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Cancel { get; private init; }

    /// <summary>
    /// Reads the artwork out of the installed game data.
    /// </summary>
    /// <param name="isEvilInterface">Whether the evil look is used; only the OKAY/CANCEL buttons follow it.</param>
    /// <returns>The artwork, or <see langword="null"/> when a file or frame it needs is missing.</returns>
    /// <remarks>依赖 <see cref="GameAssets.Current"/> 已指向游戏数据。</remarks>
    public static EditorEventDetailsArtwork? TryCreate(bool isEvilInterface)
    {
        Sprite? artifactFrame = Read(IcnType.Resource, ArtifactFrameId);
        Sprite? experienceIcon = Read(IcnType.Expmrl, ExperienceFrameId);
        Sprite? secondarySkillFrame = Read(IcnType.Secskill, SecondarySkillFrameId);
        Sprite? emptyArtifact = Read(IcnType.Artifact, EmptyArtifactFrameId);
        Sprite? emptySecondarySkill = Read(IcnType.Secskill, EmptySecondarySkillFrameId);
        Sprite? deleteReleased = Read(IcnType.Cellwin, DeleteReleasedFrameId);
        Sprite? deletePressed = Read(IcnType.Cellwin, DeletePressedFrameId);
        Sprite? checkboxBox = Read(IcnType.Cellwin, CheckboxFrameId);
        Sprite? checkmark = Read(IcnType.Cellwin, CheckmarkFrameId);

        (Sprite Released, Sprite Pressed)? okay = ReadButtons(
            isEvilInterface ? IcnType.ButtonSmallOkayEvil : IcnType.ButtonSmallOkayGood);

        (Sprite Released, Sprite Pressed)? cancel = ReadButtons(
            isEvilInterface ? IcnType.ButtonSmallCancelEvil : IcnType.ButtonSmallCancelGood);

        if (artifactFrame is null || experienceIcon is null || secondarySkillFrame is null || emptyArtifact is null
            || emptySecondarySkill is null || deleteReleased is null || deletePressed is null || checkboxBox is null
            || checkmark is null || okay is null || cancel is null)
        {
            return null;
        }

        return new EditorEventDetailsArtwork
        {
            ArtifactFrame = artifactFrame,
            ExperienceIcon = experienceIcon,
            SecondarySkillFrame = secondarySkillFrame,
            EmptyArtifact = emptyArtifact,
            EmptySecondarySkill = emptySecondarySkill,
            DeleteButton = (deleteReleased, deletePressed),
            CheckboxBox = checkboxBox,
            Checkmark = checkmark,
            Okay = okay.Value,
            Cancel = cancel.Value
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

    /// <summary>The icon of a secondary skill, <c>SECSKILL</c> at <see cref="SecondarySkills.GetIndexSprite1"/>.</summary>
    /// <param name="skill">The skill; an invalid one gets <see cref="EmptySecondarySkill"/>.</param>
    /// <returns>The 34x34 icon.</returns>
    public Sprite GetSecondarySkillIcon(SecondarySkill skill)
    {
        if (skill == SecondarySkill.Unknown)
        {
            return EmptySecondarySkill;
        }

        return GameAssets.GetImage(SecondarySkillFile, (int)SecondarySkills.GetIndexSprite1(skill)) ??
               EmptySecondarySkill;
    }

    private static Sprite? Read(IcnType type, int frameId) => GameAssets.GetImage(GameAssets.GetFileName(type), frameId);

    private static (Sprite Released, Sprite Pressed)? ReadButtons(IcnType type)
    {
        string file = GameAssets.GetFileName(type);

        Sprite? released = GameAssets.GetImage(file, 0);
        Sprite? pressed = GameAssets.GetImage(file, 1);

        return released is null || pressed is null ? null : (released, pressed);
    }
}
