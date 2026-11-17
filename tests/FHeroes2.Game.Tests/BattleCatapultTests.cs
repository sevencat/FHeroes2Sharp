using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Battle::Catapult</c> (<c>battle_catapult.h/.cpp</c>): the shots a hero's ballistics and artifacts buy,
/// how much a shot breaks, what it aims at and whether it connects.
/// </summary>
public class BattleCatapultTests
{
    private static Hero Hero(SkillLevel ballistics = SkillLevel.None)
    {
        Hero hero = new(1, MapRace.Knight);

        if (ballistics != SkillLevel.None)
        {
            hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Ballistics, ballistics));
        }

        return hero;
    }

    [Fact]
    public void WithoutBallisticsTheCatapultIsOneShotThatCanMiss()
    {
        BattleCatapult catapult = new(Hero());

        Assert.Equal(1u, catapult.Shots);
        Assert.Equal(25u, catapult.DoubleDamageChance);
        Assert.True(catapult.CanMiss);
    }

    [Fact]
    public void BasicBallisticsStopsTheMisses()
    {
        BattleCatapult catapult = new(Hero(SkillLevel.Basic));

        Assert.Equal(1u, catapult.Shots);
        Assert.Equal(50u, catapult.DoubleDamageChance);
        Assert.False(catapult.CanMiss);
    }

    [Fact]
    public void AdvancedBallisticsBuysASecondShot()
    {
        BattleCatapult catapult = new(Hero(SkillLevel.Advanced));

        Assert.Equal(2u, catapult.Shots);
        Assert.Equal(50u, catapult.DoubleDamageChance);
        Assert.False(catapult.CanMiss);
    }

    [Fact]
    public void ExpertBallisticsAlwaysBreaksTwoSections()
    {
        BattleCatapult catapult = new(Hero(SkillLevel.Expert));
        Pcg32 randomGenerator = new(7);

        Assert.Equal(2u, catapult.Shots);
        Assert.Equal(100u, catapult.DoubleDamageChance);

        for (int i = 0; i < 20; ++i)
        {
            Assert.Equal(2, catapult.GetDamage(randomGenerator));
        }
    }

    [Fact]
    public void TheBallistaAddsAShotOnTopOfBallistics()
    {
        Hero hero = Hero(SkillLevel.Expert);
        hero.BagArtifacts.Push(ArtifactType.Ballista);

        Assert.Equal(3u, new BattleCatapult(hero).Shots);
    }

    [Fact]
    public void AShotEitherBreaksOneSectionOrTwo()
    {
        BattleCatapult catapult = new(Hero(SkillLevel.Basic));
        Pcg32 randomGenerator = new(11);

        int doubles = 0;

        for (int i = 0; i < 400; ++i)
        {
            int damage = catapult.GetDamage(randomGenerator);

            Assert.InRange(damage, 1, 2);

            if (damage == 2)
            {
                ++doubles;
            }
        }

        // 五成的双倍伤害：400 次里落在很宽的范围里都算对，这条测的是「会掷骰子」而不是分布。
        Assert.InRange(doubles, 100, 300);
    }

    [Fact]
    public void AShotThatCannotMissNeverMisses()
    {
        BattleCatapult catapult = new(Hero(SkillLevel.Advanced));
        Pcg32 randomGenerator = new(3);

        for (int i = 0; i < 100; ++i)
        {
            Assert.True(catapult.IsNextShotHit(randomGenerator));
        }
    }

    [Fact]
    public void AnUntrainedCatapultMissesAboutAQuarterOfTheTime()
    {
        BattleCatapult catapult = new(Hero());
        Pcg32 randomGenerator = new(5);

        int hits = 0;

        for (int i = 0; i < 1000; ++i)
        {
            if (catapult.IsNextShotHit(randomGenerator))
            {
                ++hits;
            }
        }

        // 掷 1..20 需要至少 6 才算命中，也就是 3/4 命中。
        Assert.InRange(hits, 650, 850);
    }

    [Fact]
    public void TheWallsComeFirstAndTheKeepLast()
    {
        Pcg32 randomGenerator = new(9);

        Dictionary<CastleDefenseStructure, int> state = new()
        {
            [CastleDefenseStructure.Wall1] = 2,
            [CastleDefenseStructure.Wall2] = 2,
            [CastleDefenseStructure.Wall3] = 2,
            [CastleDefenseStructure.Wall4] = 2,
            [CastleDefenseStructure.Tower1] = 1,
            [CastleDefenseStructure.Tower2] = 1,
            [CastleDefenseStructure.Bridge] = 1,
            [CastleDefenseStructure.CentralTower] = 1
        };

        Assert.Contains(
            BattleCatapult.GetTarget(state, randomGenerator),
            new[]
            {
                CastleDefenseStructure.Wall1,
                CastleDefenseStructure.Wall2,
                CastleDefenseStructure.Wall3,
                CastleDefenseStructure.Wall4
            });

        state[CastleDefenseStructure.Wall1] = 0;
        state[CastleDefenseStructure.Wall2] = 0;
        state[CastleDefenseStructure.Wall3] = 0;

        // 只剩一堵墙时没有选择余地。
        Assert.Equal(CastleDefenseStructure.Wall4, BattleCatapult.GetTarget(state, randomGenerator));

        state[CastleDefenseStructure.Wall4] = 0;

        Assert.Contains(
            BattleCatapult.GetTarget(state, randomGenerator),
            new[] { CastleDefenseStructure.Tower1, CastleDefenseStructure.Tower2 });

        state[CastleDefenseStructure.Tower1] = 0;
        state[CastleDefenseStructure.Tower2] = 0;

        Assert.Equal(CastleDefenseStructure.Bridge, BattleCatapult.GetTarget(state, randomGenerator));

        state[CastleDefenseStructure.Bridge] = 0;

        Assert.Equal(CastleDefenseStructure.CentralTower, BattleCatapult.GetTarget(state, randomGenerator));

        state[CastleDefenseStructure.CentralTower] = 0;

        Assert.Equal(CastleDefenseStructure.None, BattleCatapult.GetTarget(state, randomGenerator));
    }

    [Fact]
    public void TheBridgeTowersAreNotTargets()
    {
        CastleDefenseStructure[] targets = BattleCatapult.GetAllowedTargets();

        Assert.Equal(8, targets.Length);
        Assert.DoesNotContain(CastleDefenseStructure.TopBridgeTower, targets);
        Assert.DoesNotContain(CastleDefenseStructure.BottomBridgeTower, targets);
    }

    [Fact]
    public void EveryTargetHasAHitAndAMissPosition()
    {
        foreach (CastleDefenseStructure target in BattleCatapult.GetAllowedTargets())
        {
            Point hit = BattleCatapult.GetTargetPosition(target, true);
            Point miss = BattleCatapult.GetTargetPosition(target, false);

            Assert.NotEqual(default, hit);
            Assert.NotEqual(default, miss);
            Assert.NotEqual(hit, miss);
        }

        Assert.Equal(default, BattleCatapult.GetTargetPosition(CastleDefenseStructure.None, true));
    }
}
