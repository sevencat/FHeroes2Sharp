using FHeroes2.Assets.Icn;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the two ICN lookups of <c>monster.cpp</c>: the projectile of a shooter
/// (<c>GetMissileICN</c>, <c>:40-75</c>) and the standing sprite of a creature in its dwelling
/// (<c>ICNMonh</c>, <c>:825-828</c>).
/// </summary>
public class MonsterIcnIdTests
{
    [Theory]
    [InlineData(MonsterType.Archer, IcnType.ArchMsl)]
    [InlineData(MonsterType.Ranger, IcnType.ArchMsl)]
    [InlineData(MonsterType.Orc, IcnType.OrcMsl)]
    [InlineData(MonsterType.OrcChief, IcnType.OrcMsl)]
    [InlineData(MonsterType.Troll, IcnType.Trollmsl)]
    [InlineData(MonsterType.WarTroll, IcnType.Troll2msl)]
    [InlineData(MonsterType.Elf, IcnType.ElfMsl)]
    [InlineData(MonsterType.GrandElf, IcnType.ElfMsl)]
    [InlineData(MonsterType.Druid, IcnType.Druidmsl)]
    [InlineData(MonsterType.GreaterDruid, IcnType.Druidmsl)]
    [InlineData(MonsterType.Halfling, IcnType.Halflmsl)]
    [InlineData(MonsterType.Titan, IcnType.Titanmsl)]
    [InlineData(MonsterType.Lich, IcnType.LichMsl)]
    [InlineData(MonsterType.PowerLich, IcnType.LichMsl)]
    public void EveryShooterThrowsItsOwnProjectile(MonsterType monster, IcnType expected)
    {
        Assert.Equal(expected, MonsterIcnIds.GetMissileIcn(monster));
    }

    [Fact]
    public void TheCentaurBorrowsTheElfsArrows()
    {
        // The centaur has no missile file of its own; the reference names ELF__MSL in a comment.
        Assert.Equal(IcnType.ElfMsl, MonsterIcnIds.GetMissileIcn(MonsterType.Centaur));
    }

    [Fact]
    public void ANonShooterHasNoProjectile()
    {
        Assert.Equal(IcnType.Unknown, MonsterIcnIds.GetMissileIcn(MonsterType.Peasant));
        Assert.Equal(IcnType.Unknown, MonsterIcnIds.GetMissileIcn(MonsterType.GreenDragon));
    }

    [Fact]
    public void TheDwellingSpritesStartAtThePeasant()
    {
        Assert.Equal(IcnType.Monh0000, MonsterIcnIds.GetDwellingIcn(MonsterType.Peasant));
        Assert.Equal(IcnType.Monh0001, MonsterIcnIds.GetDwellingIcn(MonsterType.Archer));
        Assert.Equal(
            IcnType.Monh0000 + ((int)MonsterType.WaterElement - (int)MonsterType.Peasant),
            MonsterIcnIds.GetDwellingIcn(MonsterType.WaterElement));
    }

    [Fact]
    public void TheWaterElementalIsTheLastDwelling()
    {
        // MONH0000 holds the peasant, so the water elemental — the sixty-sixth creature — is MONH0065.
        Assert.Equal(IcnType.Monh0065, MonsterIcnIds.GetDwellingIcn(MonsterType.WaterElement));
    }

    [Fact]
    public void TheMarkersHaveNoDwelling()
    {
        Assert.Equal(IcnType.Unknown, MonsterIcnIds.GetDwellingIcn(MonsterType.Unknown));
        Assert.Equal(IcnType.Unknown, MonsterIcnIds.GetDwellingIcn(MonsterType.RandomMonster));
        Assert.Equal(IcnType.Unknown, MonsterIcnIds.GetDwellingIcn(MonsterType.RandomMonsterLevel4));
    }

    [Theory]
    [InlineData(MonsterType.Ranger, IcnType.Archer2)]
    [InlineData(MonsterType.VeteranPikeman, IcnType.Pikeman2)]
    [InlineData(MonsterType.MasterSwordsman, IcnType.Swordsm2)]
    [InlineData(MonsterType.Champion, IcnType.Cavalryb)]
    [InlineData(MonsterType.Crusader, IcnType.Paladin2)]
    [InlineData(MonsterType.OrcChief, IcnType.Orc2)]
    [InlineData(MonsterType.MinotaurKing, IcnType.Minotau2)]
    [InlineData(MonsterType.Giant, IcnType.Titanblu)]
    [InlineData(MonsterType.Titan, IcnType.Titanbla)]
    [InlineData(MonsterType.Mummy, IcnType.Mummyw)]
    [InlineData(MonsterType.MutantZombie, IcnType.Zombie2)]
    [InlineData(MonsterType.BoneDragon, IcnType.Dragbone)]
    public void AnUpgradeDrawsItsOwnFrames(MonsterType monster, IcnType expected)
    {
        Assert.Equal(expected, MonsterIcnIds.GetSpriteIcn(monster));
    }

    [Fact]
    public void TheChampionRidesTheBlackCavalryFile()
    {
        // The archive calls the black cavalry CAVALRYB, so the champion reads a "black" file while the
        // cavalry reads CAVALRYR — the two names are colours, not upgrades.
        Assert.Equal(IcnType.Cavalryr, MonsterIcnIds.GetSpriteIcn(MonsterType.Cavalry));
        Assert.Equal(IcnType.Cavalryb, MonsterIcnIds.GetSpriteIcn(MonsterType.Champion));
    }

    [Fact]
    public void EveryCreatureHasASprite()
    {
        Assert.Equal(66, (int)MonsterType.WaterElement - (int)MonsterType.Peasant + 1);

        for (MonsterType monster = MonsterType.Peasant; monster <= MonsterType.WaterElement; ++monster)
        {
            Assert.NotEqual(IcnType.Unknown, MonsterIcnIds.GetSpriteIcn(monster));
        }
    }

    [Fact]
    public void TheMarkersHaveNoSprite()
    {
        Assert.Equal(IcnType.Unknown, MonsterIcnIds.GetSpriteIcn(MonsterType.Unknown));
        Assert.Equal(IcnType.Unknown, MonsterIcnIds.GetSpriteIcn(MonsterType.RandomMonster));
        Assert.Equal(IcnType.Unknown, MonsterIcnIds.GetSpriteIcn(MonsterType.RandomMonsterLevel4));
    }
}
