using System.Diagnostics;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Army;

/// <summary>
/// Why a stack of neutral monsters does or does not join the hero, <c>NeutralMonsterJoiningCondition::Reason</c>
/// (<c>army.h:141-159</c>).
/// </summary>
/// <remarks>
/// <see cref="Alliance"/> and <see cref="Bane"/> come from campaign awards: an alliance makes the monster join
/// for free whatever the odds, a bane makes it flee even from a single peasant. Campaigns are not ported yet, so
/// the two values are declared for completeness and <see cref="Army.GetJoinSolution"/> cannot return them today —
/// see the remarks there.
/// </remarks>
public enum NeutralMonsterJoiningReason
{
    /// <summary>They neither join nor flee: the hero has to fight them.</summary>
    None,

    /// <summary>They join for nothing, because the tile says so.</summary>
    Free,

    /// <summary>They can be hired with Diplomacy for the usual price.</summary>
    ForMoney,

    /// <summary>They are too scared to fight and will flee.</summary>
    RunAway,

    /// <summary>Campaign only: they join for nothing because of an alliance award.</summary>
    Alliance,

    /// <summary>Campaign only: they flee whatever the odds, because of a bane award.</summary>
    Bane,
}

/// <summary>
/// The answer to "will these monsters join", <c>NeutralMonsterJoiningCondition</c> (<c>army.h:141-159</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference also carries two <c>const char *</c> messages, which only the campaign reasons fill in
/// (<c>CampaignAwardData::getAllianceJoiningMessage()</c> and the bane's fleeing message). Campaigns are not
/// ported, so the port keeps the two values that every reason needs and leaves the strings to the campaign
/// batch.
/// </para>
/// </remarks>
/// <param name="Reason">What the monsters decided.</param>
/// <param name="MonsterCount">
/// How many would join. Zero for <see cref="NeutralMonsterJoiningReason.RunAway"/> and
/// <see cref="NeutralMonsterJoiningReason.None"/>, the whole stack for <see cref="NeutralMonsterJoiningReason.Free"/>,
/// and only what Diplomacy can pay for in <see cref="NeutralMonsterJoiningReason.ForMoney"/>.
/// </param>
public readonly record struct NeutralMonsterJoiningCondition(NeutralMonsterJoiningReason Reason, uint MonsterCount);

/// <summary>
/// Whether the monsters standing on a tile join the hero, <c>Army::GetJoinSolution()</c>
/// (<c>army.cpp:1641-1731</c>).
/// </summary>
/// <remarks>
/// <para>
/// The four rules, in the order the reference applies them:
/// </para>
/// <list type="number">
/// <item>
/// <b>The odds decide eligibility.</b> The neutrals only consider joining when the hero's army is more than
/// twice as strong as their stack (<c>armyStrengthRatio &gt; 2</c>). The ratio is deliberately computed on raw
/// stack strengths, without the hero's attack and defense, because the original game did the same — a hero with
/// great stats does not scare them off earlier.
/// </item>
/// <item>
/// <b>Three things can still stop a join.</b> The army must be able to accept the stack (a free slot or an
/// existing stack of the same monster), the hero must not carry an artifact that forbids joining armies, and the
/// tile itself must not be flagged to refuse.
/// </item>
/// <item>
/// <b>Joining is free when the tile says so</b> (<c>isMonsterOnTileJoinConditionFree</c>), otherwise only
/// Diplomacy can close the deal, and only for as many creatures as the skill's percentage of the stack's hit
/// points buys — and only if the kingdom can pay for them.
/// </item>
/// <item>
/// <b>Fright beats money.</b> At more than five times the strength and when the hero is not driven by the AI
/// (<c>!hero.isControlAI()</c>, <c>army.cpp:1725</c>), the monsters flee rather than fight — a human hero scares
/// them, a computer one does not. This is checked <i>after</i> the join, so a stack that would have joined still
/// joins.
/// </item>
/// </list>
/// <para>
/// <b>Not ported:</b> the campaign award block (<c>army.cpp:1652-1685</c>), which needs
/// <c>Campaign::CampaignSaveData</c>. When campaigns land, that block goes in front of the strength ratio and
/// can return <see cref="NeutralMonsterJoiningReason.Alliance"/> or <see cref="NeutralMonsterJoiningReason.Bane"/>
/// with the award's message strings, which is why the enum keeps those two values.
/// </para>
/// <para>
/// <b>The kingdom is a parameter.</b> The reference reaches the treasury through
/// <c>hero.GetKingdom()</c>; the port's heroes do not point back at their kingdom (ADR-120), so the caller
/// passes it. <see langword="null"/> means "unknown", and then the Diplomacy branch cannot pay and the monsters
/// do not join for money — the same outcome as a kingdom without the gold.
/// </para>
/// </remarks>
public sealed partial class Army
{
    /// <summary>
    /// Decides what the monsters on a tile do, <c>Army::GetJoinSolution()</c> (<c>army.cpp:1641-1731</c>).
    /// </summary>
    /// <param name="hero">The hero who walked onto the tile; his army's strength is what the monsters weigh.</param>
    /// <param name="tile">The tile the monsters stand on; the join conditions live in its metadata.</param>
    /// <param name="troop">The stack of monsters on the tile.</param>
    /// <param name="world">The world, for the tile metadata.</param>
    /// <param name="kingdom">The hero's kingdom, for the Diplomacy price; <see langword="null"/> if unknown.</param>
    /// <returns>What they decided, and how many would join.</returns>
    public static NeutralMonsterJoiningCondition GetJoinSolution(Hero hero, Tile tile, Troop troop, World world,
        Kingdom.Kingdom? kingdom = null)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(troop);
        ArgumentNullException.ThrowIfNull(world);

        if (!troop.IsValid)
        {
            Debug.Assert(false, "There are no monsters on the tile to decide about.");

            return new NeutralMonsterJoiningCondition(NeutralMonsterJoiningReason.None, 0);
        }

        // Neutral monsters don't care about hero's stats. Ignoring hero's stats makes hero's army strength be
        // smaller in eyes of neutrals and they won't join so often.
        double armyStrengthRatio = hero.Army.GetTroopsStrength() / troop.GetStrength();

        bool canJoin = armyStrengthRatio > 2
            && hero.Army.CanJoinTroop(troop.Monster)
            && !hero.BagArtifacts.IsCursePresent(ArtifactCurseType.NoJoiningArmies)
            && !tile.IsMonsterJoinConditionSkip(world);

        if (canJoin)
        {
            if (tile.IsMonsterJoinConditionFree(world))
            {
                return new NeutralMonsterJoiningCondition(NeutralMonsterJoiningReason.Free, troop.Count);
            }

            if (hero.HasSecondarySkill(SecondarySkill.Diplomacy))
            {
                uint amountToJoin = Troop.GetCountFromHitPoints(troop.Monster,
                    troop.GetHitPoints() * hero.GetSecondarySkillValue(SecondarySkill.Diplomacy) / 100);

                // The ability to immediately hire the entire stack of monsters is a mandatory condition for
                // their joining due to hero's Diplomacy skill in accordance with the mechanics of the original
                // game: what the skill's percentage of the stack's hit points buys must be payable in gold.
                if (amountToJoin > 0
                    && kingdom is not null
                    && kingdom.AllowPayment(new Funds(Resource.Gold, (uint)troop.GetTotalCost().Gold)))
                {
                    return new NeutralMonsterJoiningCondition(NeutralMonsterJoiningReason.ForMoney, amountToJoin);
                }
            }
        }

        // 参考问的是 <c>!hero.isControlAI()</c>：除电脑以外的英雄（人、远端、无主）都能把怪吓跑，电脑英雄不会。
        // 端口此前写成 <c>!hero.IsControlHuman</c>——正好反了，人类英雄永远看不到「要不要追」这一问。
        if (armyStrengthRatio > 5 && !hero.GetControl().IsAi())
        {
            // ... will surely flee before us
            return new NeutralMonsterJoiningCondition(NeutralMonsterJoiningReason.RunAway, 0);
        }

        return new NeutralMonsterJoiningCondition(NeutralMonsterJoiningReason.None, 0);
    }
}
