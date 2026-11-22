using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// What happened at the pyramid, <c>ActionToPyramid()</c> (<c>heroes_action.cpp:1159</c>).
/// </summary>
public enum PyramidOutcome
{
    /// <summary>The hero chose not to search, <c>heroes_action.cpp:1176-1177</c>.</summary>
    Declined,

    /// <summary>The pyramid held no spell to begin with: a visit record and a luck warning (<c>1228-1239</c>).</summary>
    Empty,

    /// <summary>The guardians won; the hero was dismissed, <c>heroes_action.cpp:1224-1226</c>.</summary>
    Lost,

    /// <summary>The spell was deciphered and written into the book, <c>heroes_action.cpp:1207-1215</c>.</summary>
    Learned,

    /// <summary>The spell was deciphered but the hero owns no book, <c>heroes_action.cpp:1198-1201</c>.</summary>
    NoSpellBook,

    /// <summary>The spell needed expert Wisdom and the hero has less, <c>heroes_action.cpp:1202-1206</c>.</summary>
    NoWisdom,
}

/// <summary>
/// The pyramid's answer: which way it went and which spell it was about.
/// </summary>
/// <remarks>
/// 参考把法术名拼进消息里（<c>1194</c>），而端口成功后就把元数据清了（<c>1221</c>），调用方再也读不到——所以法术
/// 必须跟着结果一起回来。<see cref="PyramidOutcome.Empty"/> 那条没有法术，给
/// <see cref="SpellType.None"/>。
/// </remarks>
/// <param name="Outcome">How the search ended.</param>
/// <param name="Spell">The spell the pyramid held, or <see cref="SpellType.None"/>.</param>
public readonly record struct PyramidResult(PyramidOutcome Outcome, Spell Spell);

/// <summary>
/// What the daemon cave did, <c>ActionToDaemonCave()</c>'s <c>Outcome</c> enum (<c>heroes_action.cpp:3048</c>).
/// </summary>
public enum DaemonCaveOutcome
{
    /// <summary>The hero declined to enter; the cave is untouched, <c>heroes_action.cpp:3073</c>.</summary>
    Ignored,

    /// <summary>Nothing to take: the cave is marked visited, <c>heroes_action.cpp:3076-3080</c>.</summary>
    Empty,

    /// <summary>The servants were beaten for 2500 gold, <c>heroes_action.cpp:3184-3211</c>.</summary>
    BattleWithServants,

    /// <summary>The demon was slain for its 1000 experience, <c>heroes_action.cpp:3213-3216</c>.</summary>
    Experience,

    /// <summary>Experience plus the gold the cave holds, <c>heroes_action.cpp:3217-3222</c>.</summary>
    ExperienceAndGold,

    /// <summary>Experience plus the artifact in the back of the cave, <c>heroes_action.cpp:3223-3230</c>.</summary>
    ExperienceAndArtifact,

    /// <summary>The hero bought his life back, <c>heroes_action.cpp:3231-3235</c>.</summary>
    PaidOff,

    /// <summary>The hero could not or would not pay: the army is lost, <c>heroes_action.cpp:3236-3243</c>.</summary>
    Death,
}

/// <summary>
/// The daemon cave's answer: which way it went, what it paid and what it cost.
/// </summary>
/// <param name="Outcome">How the visit ended.</param>
/// <param name="Experience">Experience the hero earned outright, <c>demonSlayingExperience</c>.</param>
/// <param name="Reward">The gold or artifact taken; empty for the outcomes that give neither.</param>
/// <param name="Paid">The ransom paid, or an empty bag.</param>
public readonly record struct DaemonCaveResult(
    DaemonCaveOutcome Outcome,
    uint Experience,
    ObjectPickupReward Reward,
    Funds Paid);

public sealed partial class HeroActions
{
    /// <summary>
    /// The demon's experience reward, <c>demonSlayingExperience</c> (<c>heroes_action.cpp:3062</c>).
    /// </summary>
    public const uint DemonSlayingExperience = 1000;

    /// <summary>
    /// The gold the daemon's servants leave behind, <c>heroes_action.cpp:3196</c>.
    /// </summary>
    public const uint DaemonServantsGold = 2500;

    /// <summary>
    /// Searching the pyramid of an ancient king, <c>ActionToPyramid()</c> (<c>heroes_action.cpp:1159</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1159-1241</c>。金字塔里只有<b>一本书上记着的法术</b>：有法术就有守军，要打；
    /// 没有法术就是空金字塔，只掉运气（<c>1228-1239</c>），<b>不清元数据</b>，和坟场一样可以反复搜。
    /// </para>
    /// <para>
    /// <b>学会法术要两个条件，缺哪个都只给提示不给法术</b>：身上得有魔法书，以及 Wisdom 要到专家
    /// （<c>1198-1206</c>）——不是「法术等级 ≤ 当前 Wisdom 等级」那种宽松判定，这是金字塔特有的硬门槛，
    /// 照抄。学到之后书里怎么写，交给 <see cref="Hero.AppendSpellToBook"/>。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object, without its action bit.</param>
    /// <param name="tileIndex">The tile it stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the search ended and which spell it was about.</returns>
    public PyramidResult ActionToPyramid(Mp2ObjectType objectType, int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_host.Ask(HeroActionQuestion.SearchPyramid))
        {
            return new PyramidResult(PyramidOutcome.Declined, default);
        }

        Tile tile = world.GetTile(tileIndex);
        Spell spell = tile.GetSpellFromTile(world);

        if (spell.Type == SpellType.None)
        {
            // 空金字塔：本地与全局各记一笔，运气警告归界面（参考只画图标，没改运气值）。
            _hero.SetVisited(tileIndex, objectType);
            Kingdom.SetVisited(tileIndex, objectType);

            return new PyramidResult(PyramidOutcome.Empty, default);
        }

        ArmyStack defenders = new();
        defenders.SetFromTile(world, tile);

        BattleResult result = Fight(world, defenders, tileIndex, castle: null);

        if (!result.IsAttackerWin)
        {
            BattleLose(world, _hero, result, attacker: true);

            return new PyramidResult(PyramidOutcome.Lost, spell);
        }

        _hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

        PyramidOutcome outcome;

        if (!_hero.HasSpellBook)
        {
            outcome = PyramidOutcome.NoSpellBook;
        }
        else if (_hero.GetLevelSkill(SecondarySkill.Wisdom) < SkillLevel.Expert)
        {
            outcome = PyramidOutcome.NoWisdom;
        }
        else
        {
            _hero.AppendSpellToBook(spell);
            outcome = PyramidOutcome.Learned;
        }

        tile.ResetObjectMetadata();
        Kingdom.SetVisited(tileIndex, objectType);

        return new PyramidResult(outcome, spell);
    }

    /// <summary>
    /// The daemon cave, <c>ActionToDaemonCave()</c> (<c>heroes_action.cpp:3042</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3042-3256</c>。「进不进去」之后分两条路：<b>打仆从</b>就是普通战斗，赢了必给
    /// <see cref="DaemonServantsGold"/> 金；<b>不打仆从</b>就落到瓦片元数据写好的那支赏赐上——纯经验、经验加金币、
    /// 经验加神器，或者「拿钱赎命」（钱不够或不肯给就是全军覆没）。
    /// </para>
    /// <para>
    /// <b>背包满会把神器折成金币再判定</b>（<c>3093-3096</c>）：折过之后走「经验加金币」那一支，读的是瓦片上的
    /// 金币而不是补偿值——地图作者摆这个洞窟时若把金币写成 0，这条分支会给出 0 金，参考如此。
    /// </para>
    /// <para>
    /// <b>除了「不进去」之外都要清元数据并记全局访问</b>（<c>3249-3254</c>），包括<b>打输了</b>与<b>英雄战死</b>——
    /// 后者连王国都可能已经没有了，但瓦片照样记在前主人的账上，参考特意写了注释。端口保留这个顺序。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object, without its action bit.</param>
    /// <param name="tileIndex">The tile it stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the visit ended and what it paid or cost.</returns>
    public DaemonCaveResult ActionToDaemonCave(Mp2ObjectType objectType, int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_host.Ask(HeroActionQuestion.EnterDaemonCave))
        {
            return new DaemonCaveResult(DaemonCaveOutcome.Ignored, 0, default, default);
        }

        Tile tile = world.GetTile(tileIndex);

        if (!tile.DoesTileContainValuableItems(world))
        {
            Kingdom.SetVisited(tileIndex, objectType);

            return new DaemonCaveResult(DaemonCaveOutcome.Empty, 0, default, default);
        }

        DaemonCaveOutcome outcome;
        uint experience = 0;
        ObjectPickupReward reward = default;
        Funds paid = default;

        if (_host.Ask(HeroActionQuestion.FightDaemonServants))
        {
            outcome = DaemonCaveOutcome.BattleWithServants;
        }
        else
        {
            DaemonCaveCaptureBonus bonus = tile.GetDaemonCaveBonusType();

            if (bonus == DaemonCaveCaptureBonus.Get1000ExperienceAndArtifact && _hero.BagArtifacts.IsFull)
            {
                bonus = DaemonCaveCaptureBonus.Get1000ExperienceAnd2500Gold;
            }

            switch (bonus)
            {
                case DaemonCaveCaptureBonus.Get1000Experience:
                    outcome = DaemonCaveOutcome.Experience;
                    experience = DemonSlayingExperience;
                    break;

                case DaemonCaveCaptureBonus.Get1000ExperienceAnd2500Gold:
                {
                    Funds funds = tile.GetFundsFromTile(world);

                    Debug.Assert(funds.Gold > 0 || funds.GetValidItemsCount() == 0, "The cave's gold lane must hold only gold.");

                    outcome = DaemonCaveOutcome.ExperienceAndGold;
                    experience = DemonSlayingExperience;
                    reward = new ObjectPickupReward(funds, ArtifactType.Unknown);
                    break;
                }

                case DaemonCaveCaptureBonus.Get1000ExperienceAndArtifact:
                {
                    ArtifactType artifact = tile.GetArtifactFromTile(world);

                    if (artifact == ArtifactType.Unknown)
                    {
                        Debug.Assert(false, "The cave promised an artifact but its lane is empty.");

                        outcome = DaemonCaveOutcome.Empty;
                        break;
                    }

                    outcome = DaemonCaveOutcome.ExperienceAndArtifact;
                    experience = DemonSlayingExperience;
                    reward = new ObjectPickupReward(default, artifact);
                    break;
                }

                case DaemonCaveCaptureBonus.Pay2500Gold:
                {
                    Funds payment = tile.GetDaemonPaymentCondition();

                    // 钱不够就没什么好问的：恶魔直接动手（3152），与「不肯给」同一个下场。
                    if (!Kingdom.AllowPayment(payment) || !_host.Ask(HeroActionQuestion.PayDaemonRansom, (uint)payment.Gold))
                    {
                        outcome = DaemonCaveOutcome.Death;
                        break;
                    }

                    outcome = DaemonCaveOutcome.PaidOff;
                    paid = payment;
                    break;
                }

                case DaemonCaveCaptureBonus.Empty:
                default:
                    Debug.Assert(false, $"A cave with valuables must name a reward, not {bonus}.");

                    outcome = DaemonCaveOutcome.Empty;
                    break;
            }
        }

        if (outcome == DaemonCaveOutcome.Empty)
        {
            Kingdom.SetVisited(tileIndex, objectType);

            return new DaemonCaveResult(outcome, 0, default, default);
        }

        switch (outcome)
        {
            case DaemonCaveOutcome.BattleWithServants:
            {
                ArmyStack defenders = new();
                defenders.SetFromTile(world, tile);

                BattleResult result = Fight(world, defenders, tileIndex, castle: null);

                if (!result.IsAttackerWin)
                {
                    BattleLose(world, _hero, result, attacker: true);

                    // 打输了洞窟也照样被清空并记访问（3249-3254），参考如此。
                    break;
                }

                _hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

                experience = result.AttackerExperience;

                Funds gold = new(Resource.Gold, DaemonServantsGold);
                Kingdom.AddFundsResource(gold);

                reward = new ObjectPickupReward(gold, ArtifactType.Unknown);
                break;
            }

            case DaemonCaveOutcome.Experience:
                _hero.IncreaseExperience(DemonSlayingExperience, world.MapSeed);
                break;

            case DaemonCaveOutcome.ExperienceAndGold:
            {
                Funds funds = tile.GetFundsFromTile(world);

                _hero.IncreaseExperience(DemonSlayingExperience, world.MapSeed);

                if (funds.GetValidItemsCount() > 0)
                {
                    Kingdom.AddFundsResource(funds);
                }

                break;
            }

            case DaemonCaveOutcome.ExperienceAndArtifact:
            {
                ArtifactType artifact = tile.GetArtifactFromTile(world);

                _hero.IncreaseExperience(DemonSlayingExperience, world.MapSeed);

                if (artifact != ArtifactType.Unknown && _hero.BagArtifacts.Push(artifact))
                {
                    reward = new ObjectPickupReward(default, artifact);
                }

                break;
            }

            case DaemonCaveOutcome.PaidOff:
                Kingdom.OddFundsResource(tile.GetDaemonPaymentCondition());
                break;

            case DaemonCaveOutcome.Death:
            {
                BattleResult loss = new() { Attacker = BattleOutcome.Loss };

                BattleLose(world, _hero, loss, attacker: true);
                break;
            }

            default:
                Debug.Assert(false, $"A cave outcome of {outcome} should have been handled above.");
                break;
        }

        tile.ResetObjectMetadata();
        Kingdom.SetVisited(tileIndex, objectType);

        return new DaemonCaveResult(outcome, experience, reward, paid);
    }
}
