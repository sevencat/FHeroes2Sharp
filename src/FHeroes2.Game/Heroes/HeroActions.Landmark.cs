using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// What happened at the sphinx, <c>ActionToSphinx()</c> (<c>heroes_action.cpp:3522</c>).
/// </summary>
public enum SphinxOutcome
{
    /// <summary>The action was dispatched to something that is not a sphinx.</summary>
    NotASphinx,

    /// <summary>Already answered, or never had a riddle: the sphinx stays quiet (<c>heroes_action.cpp:3541-3545</c>).</summary>
    Empty,

    /// <summary>The hero did not take the challenge, <c>heroes_action.cpp:3547-3554</c> — not even a visit is recorded.</summary>
    Declined,

    /// <summary>Wrong answer: the hero is eaten, <c>heroes_action.cpp:3566-3573</c>.</summary>
    WrongAnswer,

    /// <summary>Right answer: the reward was paid, <c>heroes_action.cpp:3615-3634</c>.</summary>
    Answered,
}

/// <summary>
/// The sphinx's answer: whether the hero walked away and what the answer was worth.
/// </summary>
/// <param name="Outcome">How the encounter ended.</param>
/// <param name="Riddle">The riddle that was asked, empty when none was.</param>
/// <param name="Reward">What the hero was given, empty unless <see cref="SphinxOutcome.Answered"/>.</param>
/// <param name="Artifact">The artifact given, <see cref="ArtifactType.Unknown"/> for none.</param>
public readonly record struct SphinxResult(
    SphinxOutcome Outcome,
    string Riddle,
    Funds Reward,
    ArtifactType Artifact);

/// <summary>
/// What happened on a ground event, <c>ActionToEvent()</c> (<c>heroes_action.cpp:2811</c>).
/// </summary>
public enum EventTileOutcome
{
    /// <summary>
    /// The tile claims to be an event but carries no block — a hacked map (<c>heroes_action.cpp:2816-2824</c>).
    /// </summary>
    MissingData,

    /// <summary>The event does not fire for this color, <c>heroes_action.cpp:2826-2828</c>.</summary>
    NotForThisPlayer,

    /// <summary>The event fired, <c>heroes_action.cpp:2830-2921</c>.</summary>
    Fired,
}

/// <summary>
/// The event's answer: whether it fired, what it said and what it gave.
/// </summary>
/// <remarks>
/// <see cref="Granted"/> 是按钱袋上限<b>截过</b>的那一份（<see cref="Resource.CalculateEventResourceUpdate"/>），
/// 也就是界面该画出来的量；负值同样被截在「最多扣到 0」。
/// </remarks>
/// <param name="Outcome">How the step ended.</param>
/// <param name="Message">The text of the event, empty when the block carried none.</param>
/// <param name="Granted">Resources actually added to the treasury.</param>
/// <param name="Artifact">The artifact given, <see cref="ArtifactType.Unknown"/> for none.</param>
/// <param name="Skill">The skill taught, invalid when the event teaches none.</param>
/// <param name="Experience">Experience granted, zero when none.</param>
public readonly record struct EventTileResult(
    EventTileOutcome Outcome,
    string Message,
    Funds Granted,
    ArtifactType Artifact,
    SecondarySkillLevel Skill,
    int Experience);

public sealed partial class HeroActions
{
    /// <summary>
    /// The sphinx and its riddle, <c>ActionToSphinx()</c> (<c>heroes_action.cpp:3522</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3522-3648</c>。五条出路：没有谜语（或已经答过）→ 只记访问；拒答 → <b>什么都不记</b>，
    /// 可以回来再答；答错 → 英雄被吞（<c>BattleLose</c>，全军覆没）；答对 → 给奖、<b>清空奖品</b>、记访问。
    /// </para>
    /// <para>
    /// 答对之后 `valid` 就变成假了（<see cref="MapSphinx.Reset"/>），所以第二个人来只会听到「狮身人面像安静地待着」。
    /// 谜语与答案本身留在瓦片上，参考也不擦。
    /// </para>
    /// <para>
    /// 参考在「答对了但没有奖」那一支直接 <c>assert(0)</c> 什么都不做；端口按「答对」处理（清奖品 + 记访问），
    /// 因为那种状态只可能来自存档里半个谜题，不处理会让英雄反复被问同一道题。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object, without its action bit.</param>
    /// <param name="tileIndex">The tile it stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the encounter ended and what it was worth.</returns>
    public SphinxResult ActionToSphinx(Mp2ObjectType objectType, int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (objectType != Mp2ObjectType.Sphinx)
        {
            Debug.Assert(false, $"A sphinx action was dispatched to {objectType}.");

            return new SphinxResult(SphinxOutcome.NotASphinx, string.Empty, default, ArtifactType.Unknown);
        }

        if (world.MapObjects.Get<MapSphinx>(tileIndex) is not { IsValid: true } sphinx)
        {
            Kingdom.SetVisited(tileIndex, objectType);

            return new SphinxResult(SphinxOutcome.Empty, string.Empty, default, ArtifactType.Unknown);
        }

        if (!_host.Ask(HeroActionQuestion.AcceptSphinxChallenge))
        {
            return new SphinxResult(SphinxOutcome.Declined, sphinx.Riddle, default, ArtifactType.Unknown);
        }

        string answer = _host.AskRiddle(sphinx.Riddle);

        if (!sphinx.IsCorrectAnswer(answer))
        {
            BattleResult loss = new() { Attacker = BattleOutcome.Loss };
            BattleLose(world, _hero, loss, attacker: true);

            return new SphinxResult(SphinxOutcome.WrongAnswer, sphinx.Riddle, default, ArtifactType.Unknown);
        }

        Funds reward = sphinx.Resources;
        ArtifactType artifact = sphinx.Artifact;

        if (reward.GetValidItemsCount() > 0)
        {
            Kingdom.AddFundsResource(reward);
        }

        if (artifact != ArtifactType.Unknown)
        {
            _hero.PickupArtifact(artifact, world);
        }

        sphinx.Reset();
        Kingdom.SetVisited(tileIndex, objectType);

        return new SphinxResult(SphinxOutcome.Answered, sphinx.Riddle, reward, artifact);
    }

    /// <summary>
    /// A ground event, <c>ActionToEvent()</c> (<c>heroes_action.cpp:2811</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2811-2922</c>。顺序是硬的：判颜色 → <b>走完这一步的路</b>（<c>SetMove(false)</c>，
    /// 事件不消耗移动力）→ 给资源（先按钱袋上限截一次，界面画的是截过的那份）→ 给技能（学不会就悄悄不给）→
    /// 给神器（背包满时 <c>PickupArtifact</c> 自己处理）→ 给经验 → 标用尽。
    /// </para>
    /// <para>
    /// <b>一次性事件要连瓦片一起擦</b>：清掉英雄脚下的对象类型再删块，否则「透过英雄看」还是那个事件。
    /// 常驻事件（收过路费那种）只把数量交出去，对象留在原地等下一个颜色。
    /// </para>
    /// <para>
    /// 数据缺失（被改过的地图）时参考只把脚下的对象类型清成 <see cref="Mp2ObjectType.None"/> 就走人——
    /// 也照抄，否则英雄会一站上去就被反复触发。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the hero stepped on.</param>
    /// <param name="world">The world.</param>
    /// <returns>Whether the event fired and what it gave.</returns>
    public EventTileResult ActionToEvent(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (world.GetMapEvent(tileIndex) is not MapEvent mapEvent)
        {
            _hero.SetObjectTypeUnderHero(Mp2ObjectType.None);

            return new EventTileResult(
                EventTileOutcome.MissingData,
                string.Empty,
                default,
                ArtifactType.Unknown,
                default,
                0);
        }

        if (!mapEvent.IsAllow(_hero.Color))
        {
            return new EventTileResult(
                EventTileOutcome.NotForThisPlayer,
                mapEvent.Message,
                default,
                ArtifactType.Unknown,
                default,
                0);
        }

        _hero.SetMove(false);

        Funds granted = Resource.CalculateEventResourceUpdate(Kingdom.GetFunds(), mapEvent.Resources);

        if (mapEvent.Resources.GetValidItemsCount() > 0)
        {
            Kingdom.AddFundsResource(mapEvent.Resources);
        }

        SecondarySkillLevel taught = mapEvent.SecondarySkill;

        if (taught.IsValid)
        {
            bool learns = _hero.HasSecondarySkill(taught.Skill)
                ? (int)_hero.SecondarySkills.GetLevel(taught.Skill) < (int)taught.Level
                : !_hero.HasMaxSecondarySkill();

            if (learns)
            {
                _hero.LearnSkill(taught);

                // 学会侦察要立刻把新视野揭开（参考同时重画雷达，那是界面的事）。
                if (taught.Skill == SecondarySkill.Scouting)
                {
                    _hero.Scout(world, tileIndex);
                }
            }
            else
            {
                taught = default;
            }
        }

        if (mapEvent.Artifact != ArtifactType.Unknown)
        {
            _hero.PickupArtifact(mapEvent.Artifact, world);
        }

        if (mapEvent.Experience > 0)
        {
            _hero.IncreaseExperience((uint)mapEvent.Experience, world.MapSeed);
        }

        mapEvent.SetVisited();

        if (mapEvent.IsSingleTimeEvent)
        {
            _hero.SetObjectTypeUnderHero(Mp2ObjectType.None);
            world.MapObjects.Remove(tileIndex);
        }

        return new EventTileResult(
            EventTileOutcome.Fired,
            mapEvent.Message,
            granted,
            mapEvent.Artifact,
            taught,
            mapEvent.Experience);
    }
}
