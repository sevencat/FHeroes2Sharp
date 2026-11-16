using FHeroes2.Assets.Audio;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins <c>M82::FromSpell</c> (<c>src/fheroes2/agg/m82.cpp:105-203</c>), including the five spells that
/// borrow another spell's sound because the original game never recorded one for them.
/// </summary>
public class SpellSoundTests
{
    [Theory]
    [InlineData(SpellType.Fireball, M82Sound.Fireball)]
    [InlineData(SpellType.FireBlast, M82Sound.Fireball)]
    [InlineData(SpellType.LightningBolt, M82Sound.Lightblt)]
    [InlineData(SpellType.Haste, M82Sound.Haste)]
    [InlineData(SpellType.Bless, M82Sound.Bless)]
    [InlineData(SpellType.Armageddon, M82Sound.Armgedn)]
    [InlineData(SpellType.MeteorShower, M82Sound.Meteor)]
    [InlineData(SpellType.DragonSlayer, M82Sound.Drgnslay)]
    [InlineData(SpellType.Earthquake, M82Sound.Erthquak)]
    [InlineData(SpellType.Haunt, M82Sound.H2mine)]
    public void ASpellPlaysItsOwnSound(SpellType spell, M82Sound expected)
    {
        Assert.Equal(expected, SpellSound.FromSpell(spell));
    }

    [Fact]
    public void TheSharedSoundsAreShared()
    {
        // 参考：m82.cpp:108-110、149-151、172-174、179-180、196-197。
        Assert.Equal(SpellSound.FromSpell(SpellType.Fireball), SpellSound.FromSpell(SpellType.FireBlast));
        Assert.Equal(SpellSound.FromSpell(SpellType.Dispel), SpellSound.FromSpell(SpellType.MassDispel));
        Assert.Equal(SpellSound.FromSpell(SpellType.DeathRipple), SpellSound.FromSpell(SpellType.DeathWave));
        Assert.Equal(M82Sound.Resurect, SpellSound.FromSpell(SpellType.AnimateDead));
        Assert.Equal(M82Sound.Paralize, SpellSound.FromSpell(SpellType.Petrify));
    }

    [Theory]
    [InlineData(SpellType.None)]
    [InlineData(SpellType.HolyWord)]
    [InlineData(SpellType.HolyShout)]
    [InlineData(SpellType.ViewMines)]
    [InlineData(SpellType.SummonBoat)]
    [InlineData(SpellType.DimensionDoor)]
    [InlineData(SpellType.TownGate)]
    [InlineData(SpellType.Visions)]
    [InlineData(SpellType.SetEGuardian)]
    [InlineData(SpellType.Count)]
    public void TheSpellsWithoutASoundAreSilent(SpellType spell)
    {
        Assert.Equal(M82Sound.Unknown, SpellSound.FromSpell(spell));
    }

    [Fact]
    public void OnlyTheSpellsWithASoundAreMapped()
    {
        // 参考的表里有四十六条 case，去掉三对共用与两处借用之后，剩下的法术都应该沉默。
        int mapped = 0;
        int silent = 0;

        foreach (SpellType spell in Enum.GetValues<SpellType>())
        {
            if (SpellSound.FromSpell(spell) == M82Sound.Unknown)
            {
                ++silent;
            }
            else
            {
                ++mapped;
            }
        }

        // 参考的四十八条 case 一条对一个法术；枚举一共七十四项一级法术。
        Assert.Equal(48, mapped);
        Assert.Equal(26, silent);
        Assert.Equal(Enum.GetValues<SpellType>().Length, mapped + silent);
    }
}
