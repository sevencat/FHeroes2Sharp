using System.Globalization;

using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Campaign;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork the campaign reward windows need, <c>gui/ui_campaign.cpp</c>.
/// </summary>
/// <remarks>
/// 参考直接从 <c>Assets::getImage()</c> 取图，端口把取图交给宿主，窗口只负责「哪个分支要哪张图」。
/// </remarks>
public interface ICampaignPopupArtwork
{
    /// <summary>The title font, <c>FontType::normalYellow()</c>.</summary>
    IGlyphSource HeaderFont { get; }

    /// <summary>The body font, <c>FontType::normalWhite()</c>.</summary>
    IGlyphSource BodyFont { get; }

    /// <summary>The plate behind a single monster, <c>ICN::STRIP</c> frame 12.</summary>
    Sprite GetMonsterFrameBackground();

    /// <summary>The monster plates and portraits inside that frame.</summary>
    IMonsterFrameSprites MonsterSprites { get; }

    /// <summary>The frame behind an artifact, <c>ICN::RESOURCE</c> frame 7.</summary>
    Sprite GetArtifactFrame();

    /// <summary>The artifact icon, <c>ICN::ARTIFACT</c>.</summary>
    Sprite GetArtifactIcon(ArtifactType artifact);

    /// <summary>A resource icon, <c>ICN::RESOURCE</c>.</summary>
    Sprite GetResourceIcon(int resource);

    /// <summary>The captain portrait of a race, <c>ICN::getCaptainIcnId( race )</c> frame 1.</summary>
    Sprite GetCaptainPortrait(MapRace race);

    /// <summary>The plate behind a primary skill, <c>ICN::PRIMSKIL</c>.</summary>
    Sprite GetPrimarySkillFrame();

    /// <summary>The icon of a primary skill, <c>ICN::PRIMSKIL</c>.</summary>
    Sprite GetPrimarySkillIcon(PrimarySkill skill);

    /// <summary>The plate behind a secondary skill, <c>ICN::SECSKILL</c>.</summary>
    Sprite GetSecondarySkillFrame();

    /// <summary>The icon of a secondary skill, <c>ICN::SECSKILL</c>.</summary>
    Sprite GetSecondarySkillIcon(SecondarySkill skill);

    /// <summary>A spell icon, <c>ICN::SPELLS</c>.</summary>
    Sprite GetSpellIcon(SpellType spell);

    /// <summary>The backdrop of a hero portrait, <c>ICN::HEROBKG</c> frame 0.</summary>
    Sprite GetHeroBackground();

    /// <summary>A hero portrait, <c>ICN::getHeroPortraitIcnId( heroId )</c> frame 0.</summary>
    Sprite GetHeroPortrait(int heroId);
}

/// <summary>
/// The two campaign reward windows, <c>showScenarioBonusDataPopupWindow()</c> and <c>showAwardDataPopupWindow()</c>
/// (<c>gui/ui_campaign.cpp:71-217</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的两个函数都是「按奖励种类拼一组对话框元素，再调 <c>showStandardTextMessage</c>」。端口照抄这段分支，
/// 只把绘制结果交回调用方：返回的 <see cref="MessageBox"/> 由场景渲染。
/// </para>
/// <para>
/// 参考从全局 <c>Campaign::CampaignSaveData::Get()</c> 取带入下一关的部队；端口允许显式传入，默认仍读那份存档。
/// </para>
/// </remarks>
public static class CampaignPopupWindows
{
    /// <summary>
    /// The window describing a scenario bonus, <c>showScenarioBonusDataPopupWindow()</c>
    /// (<c>ui_campaign.cpp:71-137</c>).
    /// </summary>
    /// <param name="bonusData">The bonus.</param>
    /// <param name="artwork">The artwork.</param>
    /// <param name="startY">Where to start the window, -1 to centre it.</param>
    /// <param name="screen">The screen, for the vertical clamp.</param>
    /// <returns>The window; the caller draws it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bonusData"/> or <paramref name="artwork"/> is null.</exception>
    public static MessageBox ShowScenarioBonus(ScenarioBonusData bonusData, ICampaignPopupArtwork artwork,
        int startY = -1, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(bonusData);
        ArgumentNullException.ThrowIfNull(artwork);

        List<DialogElement> elements;
        string header = bonusData.GetName();
        string body = bonusData.GetDescription();

        switch (bonusData.Type)
        {
            case ScenarioBonusData.BonusType.Artifact:
            {
                ArtifactType artifact = (ArtifactType)bonusData.SubType;
                ArtifactData data = ArtifactTable.Get(artifact);

                // A spell scroll carries its spell in its own field, like the reference's Artifact::SetSpell().
                string artifactDescription = artifact == ArtifactType.SpellScroll
                    ? data.GetDescription(bonusData.ArtifactSpellId)
                    : data.GetDescription();

                elements =
                [
                    new ArtifactDialogElement(artwork.GetArtifactFrame(), artwork.GetArtifactIcon(artifact),
                        data.Name, artifactDescription),
                    new TextDialogElement(new Text(artifactDescription, FontType.NormalWhite())),
                ];
                break;
            }

            case ScenarioBonusData.BonusType.Resources:
            {
                string resourceAmount = bonusData.Amount.ToString(CultureInfo.InvariantCulture);

                elements =
                [
                    new ResourceDialogElement(artwork.GetResourceIcon(bonusData.SubType),
                        bonusData.Amount > 0 ? "+" + resourceAmount : resourceAmount,
                        ResourceText.Of(bonusData.SubType), ResourceText.Description),
                ];
                break;
            }

            case ScenarioBonusData.BonusType.Troop:
            {
                elements =
                [
                    new CustomImageDialogElement(
                        BuildMonsterFrame((MonsterType)bonusData.SubType, bonusData.Amount, artwork)),
                ];
                break;
            }

            case ScenarioBonusData.BonusType.Spell:
            {
                Spell spell = new((SpellType)bonusData.SubType);
                string spellDescription = SpellInfo.GetDescription(spell);

                elements =
                [
                    new SpellDialogElement(artwork.GetSpellIcon(spell.Type), spell, 0, artwork.BodyFont),
                    new TextDialogElement(new Text(spellDescription, FontType.NormalWhite())),
                ];
                break;
            }

            case ScenarioBonusData.BonusType.StartingRace:
            case ScenarioBonusData.BonusType.StartingRaceAndArmy:
            {
                elements =
                [
                    new CustomImageDialogElement(artwork.GetCaptainPortrait((MapRace)bonusData.SubType)),
                ];
                break;
            }

            case ScenarioBonusData.BonusType.SkillPrimary:
            {
                PrimarySkill skill = (PrimarySkill)bonusData.SubType;

                elements =
                [
                    new PrimarySkillDialogElement(skill, artwork.GetPrimarySkillFrame(),
                        artwork.GetPrimarySkillIcon(skill),
                        "+" + bonusData.Amount.ToString(CultureInfo.InvariantCulture),
                        PrimarySkillNames.Get(skill), PrimarySkillNames.GetDescription(skill)),
                    new TextDialogElement(new Text(PrimarySkillNames.GetDescription(skill), FontType.NormalWhite())),
                ];
                break;
            }

            case ScenarioBonusData.BonusType.SkillSecondary:
            {
                SecondarySkill skill = (SecondarySkill)bonusData.SubType;
                SkillLevel level = (SkillLevel)bonusData.Amount;
                string description = SecondarySkills.GetDescription(skill, level);

                elements =
                [
                    new SecondarySkillDialogElement(artwork.GetSecondarySkillFrame(),
                        artwork.GetSecondarySkillIcon(skill), SecondarySkills.GetName(skill),
                        SecondarySkills.GetLevelName(level), SecondarySkills.GetFullName(skill, level),
                        description),
                    new TextDialogElement(new Text(description, FontType.NormalWhite())),
                ];
                break;
            }

            default:
                // The reference asserts here: a bonus kind that has no window.
                throw new ArgumentOutOfRangeException(nameof(bonusData), bonusData.Type,
                    "A scenario bonus of this kind has no window.");
        }

        return MessageBox.Create(header, body, artwork.HeaderFont, artwork.BodyFont, elements, startY, screen);
    }

    /// <summary>
    /// The window describing a campaign award, <c>showAwardDataPopupWindow()</c> (<c>ui_campaign.cpp:139-217</c>).
    /// </summary>
    /// <param name="awardData">The award.</param>
    /// <param name="artwork">The artwork.</param>
    /// <param name="carryOverTroops">
    /// The troops carried into the current scenario; <see langword="null"/> reads the campaign save.
    /// </param>
    /// <param name="startY">Where to start the window, -1 to centre it.</param>
    /// <param name="screen">The screen, for the vertical clamp.</param>
    /// <returns>The window; the caller draws it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="awardData"/> or <paramref name="artwork"/> is null.</exception>
    public static MessageBox ShowAward(CampaignAwardData awardData, ICampaignPopupArtwork artwork,
        IReadOnlyList<Troop>? carryOverTroops = null, int startY = -1, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(awardData);
        ArgumentNullException.ThrowIfNull(artwork);

        List<DialogElement> elements;
        string header = awardData.GetName();
        string body = awardData.GetDescription();

        switch (awardData.Type)
        {
            case CampaignAwardData.AwardType.CreatureCurse:
            case CampaignAwardData.AwardType.CreatureAlliance:
            {
                elements = [];

                // The whole upgrade chain, ending where GetUpgrade() returns the creature itself.
                MonsterType monster = (MonsterType)awardData.SubType;

                while (true)
                {
                    elements.Add(new CustomImageDialogElement(BuildMonsterFrame(monster, 0, artwork)));

                    MonsterType upgrade = MonsterUpgrades.GetUpgrade(monster);

                    if (upgrade == monster)
                    {
                        break;
                    }

                    monster = upgrade;
                }

                break;
            }

            case CampaignAwardData.AwardType.GetArtifact:
            {
                ArtifactType artifact = (ArtifactType)awardData.SubType;
                ArtifactData data = ArtifactTable.Get(artifact);
                string artifactDescription = data.GetDescription();

                elements =
                [
                    new ArtifactDialogElement(artwork.GetArtifactFrame(), artwork.GetArtifactIcon(artifact),
                        data.Name, artifactDescription),
                    new TextDialogElement(new Text(artifactDescription, FontType.NormalWhite())),
                ];
                break;
            }

            case CampaignAwardData.AwardType.CarryOverForces:
            {
                elements = [];

                foreach (Troop troop in carryOverTroops ?? CampaignSaveData.Get().CarryOverTroops)
                {
                    if (!troop.IsValid)
                    {
                        continue;
                    }

                    elements.Add(new CustomImageDialogElement(
                        BuildMonsterFrame(troop.Monster, (int)troop.Count, artwork)));
                }

                break;
            }

            case CampaignAwardData.AwardType.ResourceBonus:
            {
                string resourceAmount = awardData.Amount.ToString(CultureInfo.InvariantCulture);

                elements =
                [
                    new ResourceDialogElement(artwork.GetResourceIcon(awardData.SubType),
                        awardData.Amount > 0 ? "+" + resourceAmount : resourceAmount,
                        ResourceText.Of(awardData.SubType), ResourceText.Description),
                ];
                break;
            }

            case CampaignAwardData.AwardType.GetSpell:
            {
                Spell spell = new((SpellType)awardData.SubType);
                string spellDescription = SpellInfo.GetDescription(spell);

                elements =
                [
                    new SpellDialogElement(artwork.GetSpellIcon(spell.Type), spell, 0, artwork.BodyFont),
                    new TextDialogElement(new Text(spellDescription, FontType.NormalWhite())),
                ];
                break;
            }

            case CampaignAwardData.AwardType.HireableHero:
            case CampaignAwardData.AwardType.DefeatEnemyHero:
            {
                // The portrait window: the backdrop cut to the portrait frame, then the portrait blitted into it.
                Sprite output = ImageOps.Crop(artwork.GetHeroBackground(), 47, 29, 105, 98);
                Sprite portrait = artwork.GetHeroPortrait(awardData.SubType);

                ImageOps.BlitSprite(output.Image, new Point(2, 2), portrait);

                elements = [new CustomImageDialogElement(output)];
                break;
            }

            default:
                // The reference asserts here: an award kind that has no window.
                throw new ArgumentOutOfRangeException(nameof(awardData), awardData.Type,
                    "A campaign award of this kind has no window.");
        }

        return MessageBox.Create(header, body, artwork.HeaderFont, artwork.BodyFont, elements, startY, screen);
    }

    /// <summary>
    /// One creature on a plate with an optional "+count", <c>getMonsterFrame()</c> (<c>ui_campaign.cpp:52-66</c>).
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <param name="count">The number to print, 0 to print none.</param>
    /// <param name="artwork">The artwork.</param>
    /// <returns>The composed sprite.</returns>
    private static Sprite BuildMonsterFrame(MonsterType monster, int count, ICampaignPopupArtwork artwork)
    {
        Point frameOffset = new(6, 6);
        Sprite output = artwork.GetMonsterFrameBackground().Clone();

        MonsterFrameRenderer.Render(monster, output.Image, frameOffset, artwork.MonsterSprites);

        if (count > 0)
        {
            Text monsterCountText = new("+" + count.ToString(CultureInfo.InvariantCulture), FontType.NormalWhite());

            monsterCountText.Draw(
                output.Width - frameOffset.X - monsterCountText.Width() - 3,
                output.Height - frameOffset.Y - monsterCountText.Height() + 2,
                output.Image);
        }

        return output;
    }
}
