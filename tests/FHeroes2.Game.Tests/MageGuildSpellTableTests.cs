using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The spell table of the mage guild sub-window of the castle details dialog
/// (<c>editor_castle_details_window.cpp:318-606</c>).
/// </summary>
/// <remarks>
/// 用例盯三处：格子数只看种族能力、元数据那张「必须出现」的表进出行是什么样、禁用一个法术时它是不是从排里被换成了占位符。
/// </remarks>
public sealed class MageGuildSpellTableTests
{
    [Fact]
    public void SlotsFollowTheRaceLibraryCapability()
    {
        MageGuildSpellTable knight = new(MapRace.Knight);
        MageGuildSpellTable wizard = new(MapRace.Wizard);
        MageGuildSpellTable random = new(MapRace.Random);

        // 参考只问「这个种族能不能建图书馆」，所以随机族与法师族一样多一格。
        Assert.False(knight.HasLibraryCapability);
        Assert.True(wizard.HasLibraryCapability);
        Assert.True(random.HasLibraryCapability);

        Assert.Equal(3, knight.GetSlotCount(1));
        Assert.Equal(4, wizard.GetSlotCount(1));
        Assert.Equal(1, knight.GetSlotCount(5));
        Assert.Equal(2, wizard.GetSlotCount(5));
    }

    [Fact]
    public void EveryRowHasTheSlotsOfItsLevel()
    {
        MageGuildSpellTable table = new(MapRace.Knight);

        for (int levelIndex = 0; levelIndex < MageGuildSpellTable.LevelCount; ++levelIndex)
        {
            Assert.Equal(table.GetSlotCount(levelIndex + 1), table.GetRow(levelIndex).Count);
        }
    }

    [Fact]
    public void AnEmptyTableShowsThePlaceholderOfEachLevel()
    {
        MageGuildSpellTable table = new(MapRace.Knight);

        for (int levelIndex = 0; levelIndex < MageGuildSpellTable.LevelCount; ++levelIndex)
        {
            foreach (SpellType spell in table.GetRow(levelIndex))
            {
                Assert.Equal(MageGuildSpellTable.GetPlaceholder(levelIndex + 1), spell);

                // 占位符不是真法术：等级为 0，写回时会被当成「表里不该有这条」。
                Assert.Equal(0, new Spell(spell).Level());
            }
        }
    }

    [Fact]
    public void LoadingPinsTheSpellsOfTheMetadata()
    {
        CastleMetadata metadata = new();
        metadata.MustHaveSpells[0] = (int)SpellType.Bless;
        metadata.MustHaveSpells[12] = (int)SpellType.LightningBolt;

        MageGuildSpellTable table = new(MapRace.Knight);
        table.Load(metadata);

        Assert.Equal(SpellType.Bless, table.GetSpell(0, 0));
        Assert.Equal(SpellType.LightningBolt, table.GetSpell(1, 2));
        Assert.Equal(SpellType.Random1, table.GetSpell(0, 1));
    }

    [Fact]
    public void SavingWritesThePinnedSpellsAndDropsThePlaceholders()
    {
        CastleMetadata metadata = new();
        MageGuildSpellTable table = new(MapRace.Knight);
        table.Load(metadata);

        table.SetSpell(0, 0, SpellType.Bless);

        Assert.True(table.Save(metadata));
        Assert.Single(metadata.MustHaveSpells);
        Assert.Equal((int)SpellType.Bless, metadata.MustHaveSpells[0]);

        // 再存一次什么都没变。
        Assert.False(table.Save(metadata));
    }

    [Fact]
    public void AnUnpinnedSlotRemovesItsEntry()
    {
        CastleMetadata metadata = new();
        metadata.MustHaveSpells[0] = (int)SpellType.Bless;

        MageGuildSpellTable table = new(MapRace.Knight);
        table.Load(metadata);
        table.SetSpell(0, 0, MageGuildSpellTable.GetPlaceholder(1));

        Assert.True(table.Save(metadata));
        Assert.Empty(metadata.MustHaveSpells);
    }

    [Fact]
    public void BanningASpellSwapsItOutOfItsRow()
    {
        CastleMetadata metadata = new();
        MageGuildSpellTable table = new(MapRace.Knight);
        table.Load(metadata);
        table.SetSpell(1, 0, SpellType.LightningBolt);

        Assert.True(table.SetBannedSpells([(int)SpellType.LightningBolt]));

        Assert.Equal(SpellType.Random2, table.GetSpell(1, 0));
        Assert.Equal([(int)SpellType.LightningBolt], table.BannedSpells);

        Assert.True(table.Save(metadata));
        Assert.Empty(metadata.MustHaveSpells);
        Assert.Equal([(int)SpellType.LightningBolt], metadata.BannedSpells);
    }

    [Fact]
    public void TheSameBannedListIsNoChange()
    {
        MageGuildSpellTable table = new(MapRace.Knight);
        table.Load(new CastleMetadata());

        Assert.True(table.SetBannedSpells([(int)SpellType.Bless]));
        Assert.False(table.SetBannedSpells([(int)SpellType.Bless]));
    }

    [Fact]
    public void BanningKeepsTheOtherLevelsAlone()
    {
        CastleMetadata metadata = new();
        MageGuildSpellTable table = new(MapRace.Knight);
        table.Load(metadata);
        table.SetSpell(0, 0, SpellType.Bless);
        table.SetSpell(1, 0, SpellType.LightningBolt);

        table.SetBannedSpells([(int)SpellType.Bless]);

        Assert.Equal(SpellType.Random1, table.GetSpell(0, 0));
        Assert.Equal(SpellType.LightningBolt, table.GetSpell(1, 0));
    }

    [Fact]
    public void GroupingAndFlatteningRoundTrips()
    {
        List<int> banned = [(int)SpellType.Bless, (int)SpellType.LightningBolt, (int)SpellType.Fireball];

        List<int>[] grouped = MageGuildSpellTable.GroupByLevel(banned);

        Assert.Single(grouped[0]);
        Assert.Single(grouped[1]);
        Assert.Single(grouped[2]);
        Assert.Empty(grouped[3]);
        Assert.Empty(grouped[4]);
        Assert.Equal(banned, MageGuildSpellTable.FlattenGrouped(grouped));
    }

    [Fact]
    public void SpellsOutsideTheGuildAreIgnoredWhenGrouping()
    {
        List<int>[] grouped = MageGuildSpellTable.GroupByLevel([(int)SpellType.None, (int)SpellType.Random1]);

        Assert.All(grouped, level => Assert.Empty(level));
    }

    [Fact]
    public void TheNameBoxFallsBackToThePlaceholder()
    {
        Assert.Equal("Dragons' Peak", CastleDetailsLabels.GetCastleName("Dragons' Peak", true));
        Assert.Equal("Random Town Name", CastleDetailsLabels.GetCastleName(string.Empty, true));
        Assert.Equal("Random Castle Name", CastleDetailsLabels.GetCastleName(string.Empty, false));
    }

    [Fact]
    public void TheGuildTitleIsTheNameOfThatGuildLevel()
    {
        string first = CastleDetailsLabels.GetMageGuildLevelTitle(1);
        string fifth = CastleDetailsLabels.GetMageGuildLevelTitle(5);

        Assert.Equal(CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.MageGuild1), first);
        Assert.Equal(CastleBuildingInfo.GetName(MapRace.Knight, BuildingType.MageGuild5), fifth);
        Assert.NotEqual(first, fifth);
        Assert.Throws<ArgumentOutOfRangeException>(() => CastleDetailsLabels.GetMageGuildLevelTitle(6));
    }
}
