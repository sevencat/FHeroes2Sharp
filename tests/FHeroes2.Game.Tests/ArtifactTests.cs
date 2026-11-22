using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the artifact id numbering (<c>artifact.h:65-189</c>), the validity predicate (<c>artifact.h:186</c>),
/// the generated numbers (<c>artifact_info.cpp:586-982</c>) and the stacking piles
/// (<c>artifact_info.cpp:839-947</c>, plus the fourth <c>[ cumulative per artifact type ]</c> one at
/// <c>artifact_info.cpp:1028</c>).
/// </summary>
public class ArtifactTests
{
    [Theory]
    [InlineData(ArtifactType.Unknown, 0)]
    [InlineData(ArtifactType.UltimateBook, 1)]
    [InlineData(ArtifactType.GoldenBow, 64)]
    [InlineData(ArtifactType.MagicBook, 82)]
    [InlineData(ArtifactType.EditorAnyUltimateArtifact, 83)]
    [InlineData(ArtifactType.Unused84, 84)]
    [InlineData(ArtifactType.Unused86, 86)]
    [InlineData(ArtifactType.SpellScroll, 87)]
    [InlineData(ArtifactType.BattleGarb, 91)]
    [InlineData(ArtifactType.ArtifactCount, 104)]
    public void TheIdMatchesTheReference(ArtifactType artifact, int expected)
    {
        Assert.Equal(expected, (int)artifact);
    }

    [Fact]
    public void TheEnumHasOneMemberPerId()
    {
        Assert.Equal((int)ArtifactType.ArtifactCount + 1, Enum.GetValues<ArtifactType>().Length);
    }

    [Fact]
    public void TheThreeHolesAndTheEditorPlaceholderAreNotArtifacts()
    {
        Assert.False(ArtifactTypes.IsValid(ArtifactType.Unknown));
        Assert.False(ArtifactTypes.IsValid(ArtifactType.EditorAnyUltimateArtifact));
        Assert.False(ArtifactTypes.IsValid(ArtifactType.Unused84));
        Assert.False(ArtifactTypes.IsValid(ArtifactType.Unused85));
        Assert.False(ArtifactTypes.IsValid(ArtifactType.Unused86));
        Assert.False(ArtifactTypes.IsValid(ArtifactType.ArtifactCount));

        Assert.True(ArtifactTypes.IsValid(ArtifactType.UltimateBook));
        Assert.True(ArtifactTypes.IsValid(ArtifactType.GoldenBow));
        Assert.True(ArtifactTypes.IsValid(ArtifactType.MagicBook));
        Assert.True(ArtifactTypes.IsValid(ArtifactType.SpellScroll));
    }

    [Fact]
    public void TheSpriteIndicesFollowTheReference()
    {
        // ARTIFACT is indexed by the id itself and ARTFX by the id minus one (artifact.h:236-249).
        Assert.Equal((uint)ArtifactType.UltimateBook, ArtifactTypes.FullSpriteIndex(ArtifactType.UltimateBook));
        Assert.Equal((uint)ArtifactType.UltimateBook - 1, ArtifactTypes.MiniSpriteIndex(ArtifactType.UltimateBook));

        // The empty slot has no icon at all; the reference points past the end of the frame list.
        Assert.Equal(ArtifactTypes.UnknownMiniSpriteIndex,
            ArtifactTypes.MiniSpriteIndex(ArtifactType.Unknown));
        Assert.Equal(255u, ArtifactTypes.UnknownMiniSpriteIndex);
    }

    [Fact]
    public void TheNumbersOfAKnownArtifactAreTheReferenceOnes()
    {
        Assert.True(ArtifactTable.Get(ArtifactType.UltimateBook).TryGetBonus(ArtifactBonusType.KnowledgeSkill, out ArtifactBonus book));
        Assert.Equal(12, book.Value);

        Assert.True(ArtifactTable.Get(ArtifactType.UltimateSword).TryGetBonus(ArtifactBonusType.AttackSkill, out ArtifactBonus sword));
        Assert.Equal(12, sword.Value);

        // The golden bow cancels the castle wall ranged penalty, game_static.cpp:278 returns 50.
        Assert.True(ArtifactTable.Get(ArtifactType.GoldenBow).TryGetBonus(ArtifactBonusType.NoShootingPenalty, out ArtifactBonus bow));
        Assert.Equal(50, bow.Value);

        // The battle garb is the only artifact that names a spell: Town Portal, spell id 59.
        Assert.True(ArtifactTable.Get(ArtifactType.BattleGarb).TryGetBonus(ArtifactBonusType.AddSpell, out ArtifactBonus garb));
        Assert.Equal(SpellType.TownPortal, garb.Spell);
        Assert.Equal(59, garb.Value);

        // A blank scroll carries no spell id at all.
        Assert.True(ArtifactTable.Get(ArtifactType.SpellScroll).TryGetBonus(ArtifactBonusType.AddSpell, out ArtifactBonus scroll));
        Assert.Equal(SpellType.None, scroll.Spell);
    }

    [Fact]
    public void TheCursesOfAKnownArtifactAreTheReferenceOnes()
    {
        // Fizbin the Misfortune is the -2 morale artifact and the tax lien takes 250 gold a day.
        Assert.True(ArtifactTable.Get(ArtifactType.FizbinMisfortune).TryGetCurse(ArtifactCurseType.Morale, out ArtifactCurse morale));
        Assert.Equal(2, morale.Value);

        Assert.True(ArtifactTable.Get(ArtifactType.TaxLien).TryGetCurse(ArtifactCurseType.GoldPenalty, out ArtifactCurse tax));
        Assert.Equal(250, tax.Value);

        // The broach of shielding is the +50% elemental resistance that costs 2 spell power.
        Assert.True(ArtifactTable.Get(ArtifactType.BroachShielding).TryGetCurse(ArtifactCurseType.SpellPowerSkill, out ArtifactCurse power));
        Assert.Equal(2, power.Value);
    }

    [Fact]
    public void ThePlaceholdersOnlyCarryTheNoneBonus()
    {
        // The reference gives the magic book, the editor placeholder and the three holes a single NONE row, an
        // artifact that does nothing. The port keeps the row rather than dropping it, so the table stays a
        // faithful copy; the only id without a row at all is UNKNOWN, which is an empty slot rather than an
        // artifact.
        Assert.True(ArtifactTable.Get(ArtifactType.MagicBook).TryGetBonus(ArtifactBonusType.None, out _));
        Assert.Empty(ArtifactTable.Get(ArtifactType.MagicBook).Curses);
        Assert.True(ArtifactTable.Get(ArtifactType.Unused84).TryGetBonus(ArtifactBonusType.None, out _));

        // Id 0 is a row of its own in the reference's table — the "Invalid Artifact" placeholder — so it keeps a
        // name and still carries no numbers. Only the sentinel past the last row falls back to Empty.
        ArtifactData unknown = ArtifactTable.Get(ArtifactType.Unknown);
        Assert.Equal("Invalid Artifact", unknown.Name);
        Assert.Empty(unknown.Bonuses);
        Assert.Empty(unknown.Curses);

        Assert.Same(ArtifactData.Empty, ArtifactTable.Get(ArtifactType.ArtifactCount));
    }

    [Fact]
    public void EveryValidArtifactHasARow()
    {
        // A row that fell off the generator would silently make the artifact worthless.
        int withNumbers = 0;

        foreach (ArtifactType artifact in Enum.GetValues<ArtifactType>().Distinct())
        {
            if (!ArtifactTypes.IsValid(artifact))
            {
                continue;
            }

            if (ArtifactTable.Get(artifact).Bonuses.Count > 0 || ArtifactTable.Get(artifact).Curses.Count > 0)
            {
                ++withNumbers;
            }
        }

        // 99 valid artifacts, every one of them with at least one number (the magic book with NONE).
        Assert.Equal(99, withNumbers);
    }

    [Theory]
    [InlineData(ArtifactBonusType.AttackSkill)]
    [InlineData(ArtifactBonusType.KnowledgeSkill)]
    [InlineData(ArtifactBonusType.GoldIncome)]
    [InlineData(ArtifactBonusType.GemsIncome)]
    public void ThePrimarySkillsAndTheIncomesAddUp(ArtifactBonusType bonus)
    {
        Assert.True(ArtifactBonuses.IsCumulative(bonus));
        Assert.False(ArtifactBonuses.IsMultiplied(bonus));
        Assert.False(ArtifactBonuses.IsUnique(bonus));
    }

    [Theory]
    [InlineData(ArtifactBonusType.SurrenderCostReductionPercent)]
    [InlineData(ArtifactBonusType.CurseSpellCostReductionPercent)]
    [InlineData(ArtifactBonusType.ColdSpellDamageReductionPercent)]
    public void ThePercentagesMultiply(ArtifactBonusType bonus)
    {
        Assert.True(ArtifactBonuses.IsMultiplied(bonus));
        Assert.False(ArtifactBonuses.IsCumulative(bonus));
        Assert.False(ArtifactBonuses.IsUnique(bonus));
    }

    [Theory]
    [InlineData(ArtifactBonusType.NoShootingPenalty)]
    [InlineData(ArtifactBonusType.CurseSpellImmunity)]
    [InlineData(ArtifactBonusType.ViewMonsterInformation)]
    public void TheFlagsCountOnce(ArtifactBonusType bonus)
    {
        Assert.True(ArtifactBonuses.IsUnique(bonus));
        Assert.False(ArtifactBonuses.IsCumulative(bonus));
        Assert.False(ArtifactBonuses.IsMultiplied(bonus));
    }

    [Fact]
    public void TheCursesFallInAtMostOnePileAndTheLeftoverIsMorale()
    {
        // MORALE is the curse twin of the fourth pile: "unique per artifact type but cumulative across
        // multiple artifact types" (artifact_info.h:104-105). Two misfortunes still cost two morale, and a
        // misfortune plus another morale curse would add up, which is why it is not in the unique pile.
        int leftovers = 0;

        foreach (ArtifactCurseType curse in Enum.GetValues<ArtifactCurseType>())
        {
            int piles = (ArtifactBonuses.IsCurseCumulative(curse) ? 1 : 0)
                + (ArtifactBonuses.IsCurseMultiplied(curse) ? 1 : 0)
                + (ArtifactBonuses.IsCurseUnique(curse) ? 1 : 0);

            Assert.True(piles <= 1, $"{curse} is in {piles} piles.");

            if (piles == 0)
            {
                ++leftovers;
                Assert.Equal(ArtifactCurseType.Morale, curse);
            }
        }

        Assert.Equal(1, leftovers);
    }

    [Fact]
    public void TheBonusesFallInAtMostOnePileAndTheLeftoversAreTheFourth()
    {
        // The three switches are not a partition of the bonus types. A type that is neither cumulative, nor
        // multiplied, nor unique is the reference's fourth pile, "[ cumulative per artifact type ]"
        // (artifact_info.cpp:1028): morale, luck, mobility, the spell point ring, ADD_SPELL and friends.
        var fourthPile = new HashSet<ArtifactBonusType>
        {
            ArtifactBonusType.None,
            ArtifactBonusType.Morale,
            ArtifactBonusType.Luck,
            ArtifactBonusType.SeaBattleMoraleBoost,
            ArtifactBonusType.SeaBattleLuckBoost,
            ArtifactBonusType.LandMobility,
            ArtifactBonusType.SeaMobility,
            ArtifactBonusType.SpellPointsDailyGeneration,
            ArtifactBonusType.EveryCombatSpellDuration,
            ArtifactBonusType.ExtraCatapultShots,
            ArtifactBonusType.AreaRevealDistance,
            ArtifactBonusType.AddSpell,
            ArtifactBonusType.NecromancySkill,
            ArtifactBonusType.MaximumMorale,
            ArtifactBonusType.MaximumLuck,
        };

        int leftovers = 0;

        foreach (ArtifactBonusType bonus in Enum.GetValues<ArtifactBonusType>())
        {
            int piles = (ArtifactBonuses.IsCumulative(bonus) ? 1 : 0)
                + (ArtifactBonuses.IsMultiplied(bonus) ? 1 : 0)
                + (ArtifactBonuses.IsUnique(bonus) ? 1 : 0);

            // No type may be in two piles at once, that would make the summation ambiguous.
            Assert.True(piles <= 1, $"{bonus} is in {piles} piles.");

            if (piles == 0)
            {
                ++leftovers;
            }

            Assert.Equal(fourthPile.Contains(bonus), piles == 0);
        }

        // The fourth pile is a real pile, not a hole: fifteen of the fifty-three bonus types land in it.
        Assert.Equal(15, leftovers);
        Assert.Equal(15, fourthPile.Count);
    }
}
