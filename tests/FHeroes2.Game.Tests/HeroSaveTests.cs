using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using Xunit;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// One hero and the table of them as a save file sees them (<c>heroes.cpp:2560-2697</c>, <c>heroes_base.cpp:618-680</c>).
/// </summary>
/// <remarks>
/// 英雄存档有两个往返自证看不出来的坑：主属性的顺序是 attack、defence、<b>knowledge</b>、<b>power</b>（知识在力量前，
/// 写反了往返也照样通过），以及 1.0.10 之前 id 与头像整体差一（72 号是 UNKNOWN）。两条都用手写字节钉住。
/// </remarks>
public sealed class HeroSaveTests
{
    private const int Width = 10;

    [Fact]
    public void ThePrimarySkillsGoAttackDefenceKnowledgePower()
    {
        Hero hero = new(Hero.LordKilburn, MapRace.Knight);
        hero.SetPrimarySkill(PrimarySkill.Attack, 11);
        hero.SetPrimarySkill(PrimarySkill.Defense, 22);
        hero.SetPrimarySkill(PrimarySkill.Knowledge, 33);
        hero.SetPrimarySkill(PrimarySkill.Power, 44);

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        hero.WriteTo(stream, new MapGeometry(Width, 10));
        stream.Seek(0);

        Assert.Equal(11, stream.ReadInt32());
        Assert.Equal(22, stream.ReadInt32());
        Assert.Equal(33, stream.ReadInt32());
        Assert.Equal(44, stream.ReadInt32());
    }

    [Fact]
    public void AWholeHeroSurvivesARoundTrip()
    {
        Hero hero = new(Hero.LordKilburn, MapRace.Knight);
        hero.SetPrimarySkill(PrimarySkill.Attack, 2);
        hero.SetPrimarySkill(PrimarySkill.Defense, 3);
        hero.SetPrimarySkill(PrimarySkill.Knowledge, 4);
        hero.SetPrimarySkill(PrimarySkill.Power, 5);
        hero.SetExperience(4_321u);
        hero.SetSpellPoints(37u);
        hero.SetMovePoints(1_234u);
        hero.SetColor(PlayerColor.Blue);
        hero.SetMapIndex(24);
        hero.Direction = Direction.Right;
        hero.SpriteIndex = 9;
        hero.SetPatrolCenter(32);
        hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Logistics, SkillLevel.Expert));
        hero.BagArtifacts.Push(ArtifactType.MagicBook);
        hero.MagicBook.Append(new Spell(SpellType.Fireball));
        hero.Path.SetPath([new RouteStep(25, 24, Direction.Right, 100u)]);
        hero.SetVisited(40, Mp2ObjectType.Mine);
        hero.SetVisited(12, Mp2ObjectType.Sawmill);

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        hero.WriteTo(stream, new MapGeometry(Width, 10));

        byte[] expected = stream.Written.ToArray();
        stream.Seek(0);

        Hero restored = new(Hero.LordKilburn, MapRace.Knight);
        restored.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(2, restored.BaseAttack);
        Assert.Equal(3, restored.BaseDefense);
        Assert.Equal(4, restored.BaseKnowledge);
        Assert.Equal(5, restored.BasePower);
        Assert.Equal(4_321u, restored.Experience);
        Assert.Equal(37u, restored.SpellPoints);
        Assert.Equal(1_234u, restored.MovePoints);
        Assert.Equal(PlayerColor.Blue, restored.Color);
        Assert.Equal(24, restored.MapIndex);
        Assert.Equal(Direction.Right, restored.Direction);
        Assert.Equal(9, restored.SpriteIndex);
        Assert.Equal(32, restored.PatrolCenterIndex);
        Assert.Equal(Hero.LordKilburn, restored.Id);
        Assert.Equal(MapRace.Knight, restored.Race);
        Assert.Equal(SkillLevel.Expert, restored.GetLevelSkill(SecondarySkill.Logistics));
        Assert.True(restored.BagArtifacts.IsPresent(ArtifactType.MagicBook));
        Assert.True(restored.MagicBook.IsPresent(new Spell(SpellType.Fireball)));
        Assert.Equal(1, restored.Path.Count);
        Assert.Equal(25, restored.Path.FrontIndex);
        Assert.Equal(24, restored.Path.FrontFrom);
        Assert.Equal(100u, restored.Path.FrontPenalty);
        Assert.Equal([12, 40], restored.VisitedObjects.Select(static visited => visited.Index));

        StreamBuf again = new();
        again.SetBigEndian(true);
        restored.WriteTo(again, new MapGeometry(Width, 10));

        Assert.Equal(expected, again.Written.ToArray());
    }

    [Fact]
    public void TheVisitsAreWrittenInIndexOfOrder()
    {
        // 参考的 visitedObjects 是 std::set<IndexObject>，按 index 升序写；端口存的是插入序（最新在前），写时排序。
        Hero hero = new(Hero.LordKilburn, MapRace.Knight);
        hero.SetVisited(10, Mp2ObjectType.Sawmill);
        hero.SetVisited(50, Mp2ObjectType.Mine);

        Assert.Equal([50, 10], hero.VisitedObjects.Select(static visited => visited.Index));

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        hero.WriteTo(stream, new MapGeometry(Width, 10));
        stream.Seek(0);

        Hero restored = new(Hero.LordKilburn, MapRace.Knight);
        restored.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal([10, 50], restored.VisitedObjects.Select(static visited => visited.Index));
    }

    [Fact]
    public void AnOldHeroShiftsItsIdAndUsesTheOldFieldWidths()
    {
        // 1.0.05：位置是「空字 + UID + 两个 int16」，路线前多一个空字，objectUnderHero 是 int32，
        // 巡逻中心是两个 int16，访问表的类型也是 int32，id 与头像整体 +1（heroes.cpp:2560-2646）。
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        WriteOldHero(stream, rawId: 5);
        stream.Seek(0);

        Hero hero = new(Hero.LordKilburn, MapRace.Knight);
        hero.ReadFrom(stream, SaveFileFormat.Release1_0_05, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(7, hero.BaseAttack);
        Assert.Equal(8, hero.BaseDefense);
        Assert.Equal(9, hero.BaseKnowledge);
        Assert.Equal(10, hero.BasePower);
        Assert.Equal(6, hero.Id);
        Assert.Equal(6, hero.PortraitId);
        Assert.Equal(24, hero.MapIndex);
        Assert.Equal(5u, hero.SpellPoints);
        Assert.Equal(6u, hero.MovePoints);
        Assert.Equal(1_000u, hero.Experience);
        Assert.Equal(PlayerColor.Red, hero.Color);
        Assert.False(string.IsNullOrEmpty(hero.Name));
        Assert.True(hero.MagicBook.IsPresent(new Spell(SpellType.Fireball)));
        Assert.True(hero.BagArtifacts.IsPresent(ArtifactType.MagicBook));
        Assert.Equal(SkillLevel.Expert, hero.GetLevelSkill(SecondarySkill.Logistics));
        Assert.Equal(Mp2ObjectType.Mine, hero.ObjectTypeUnderHero);
        Assert.Equal(21, hero.Path.FrontIndex);
        Assert.Equal(20, hero.Path.FrontFrom);
        Assert.Equal(Direction.Right, hero.Direction);
        Assert.Equal(7, hero.SpriteIndex);
        Assert.Equal(32, hero.PatrolCenterIndex);
        Assert.Equal(4, hero.PatrolDistance);
        Assert.Equal([30], hero.VisitedObjects.Select(static visited => visited.Index));
        Assert.Equal(Mp2ObjectType.Mine, hero.VisitedObjects[0].Object);
    }

    [Fact]
    public void TheOldUnknownHeroBecomesTheEmptyId()
    {
        // 1.0.10 之前 UNKNOWN 是 72；读进来必须是 0，而不是 73（heroes.cpp:2593-2604）。
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        WriteOldHero(stream, rawId: 72);
        stream.Seek(0);

        Hero hero = new(Hero.LordKilburn, MapRace.Knight);
        hero.ReadFrom(stream, SaveFileFormat.Release1_0_05, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(Hero.UnknownId, hero.Id);
        Assert.Equal(Hero.UnknownId, hero.PortraitId);
    }

    [Fact]
    public void TheWholeTableOfHeroesSurvivesARoundTrip()
    {
        AllHeroes written = new();
        written.Init(MapVersion.SuccessionWars);
        written[Hero.LordKilburn]!.SetMapIndex(24);
        written[Hero.LordKilburn]!.SetColor(PlayerColor.Red);

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        written.WriteTo(stream, new MapGeometry(Width, 10));

        byte[] expected = stream.Written.ToArray();
        stream.Seek(0);

        AllHeroes restored = new();
        restored.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(written.Count, restored.Count);
        Assert.Equal(24, restored[Hero.LordKilburn]!.MapIndex);
        Assert.Equal(PlayerColor.Red, restored[Hero.LordKilburn]!.Color);
        Assert.Null(restored[Hero.UnknownId]);
        Assert.Null(restored[Hero.Solmyr]);

        StreamBuf again = new();
        again.SetBigEndian(true);
        restored.WriteTo(again, new MapGeometry(Width, 10));

        Assert.Equal(expected, again.Written.ToArray());
    }

    [Fact]
    public void ATableOfAnotherWidthFailsTheStream()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        stream.Write((uint)(Hero.IdCount - 1));
        stream.Seek(0);

        AllHeroes heroes = new();
        heroes.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.True(stream.Fail);
        Assert.Empty(heroes);
    }

    /// <summary>
    /// Writes a hero in the 1.0.05 layout (<c>heroes.cpp:2560-2646</c>, <c>heroes_base.cpp:618-680</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="rawId">The id as the old file spells it, before the shift.</param>
    private static void WriteOldHero(StreamBase stream, int rawId)
    {
        stream.Write(7);
        stream.Write(8);
        stream.Write(9);
        stream.Write(10);

        // The old position: a word of nothing, the UID, then two int16 (maps_objects.cpp:332-336).
        stream.Write(0);
        stream.Write(0u);
        stream.Write((short)4);
        stream.Write((short)2);

        stream.Write(0u);
        stream.Write(5u);
        stream.Write(6u);
        stream.Write(1u);
        stream.Write((int)SpellType.Fireball);

        stream.Write((uint)ArtifactBag.MaxCapacity);
        stream.Write((int)ArtifactType.MagicBook);
        stream.Write(0u);

        for (int i = 1; i < ArtifactBag.MaxCapacity; ++i)
        {
            stream.Write((int)ArtifactType.Unknown);
            stream.Write(0u);
        }

        stream.Write(string.Empty);
        stream.Write((byte)PlayerColor.Red);
        stream.Write(1_000u);

        stream.Write(1u);
        stream.Write((int)SecondarySkill.Logistics);
        stream.Write((int)SkillLevel.Expert);

        stream.Write((uint)ArmyStack.MaximumTroopCount);

        for (int i = 0; i < ArmyStack.MaximumTroopCount; ++i)
        {
            stream.Write(0);
            stream.Write(0u);
        }

        stream.Write(false);
        stream.Write((int)PlayerColor.None);

        stream.Write(rawId);
        stream.Write(rawId);
        stream.Write((int)MapRace.Knight);
        stream.Write((int)Mp2ObjectType.Mine);

        // The route: the word of nothing before 1.0.7, then the flag, the count and the steps (route.cpp:397-409).
        stream.Write(0);
        stream.Write(true);
        stream.Write(1u);
        stream.Write(20);
        stream.Write((int)Direction.Right);
        stream.Write(100u);

        stream.Write((int)Direction.Right);
        stream.Write(7);

        // The patrol centre as two int16 before 1.0.10.
        stream.Write((short)2);
        stream.Write((short)3);
        stream.Write(4);

        stream.Write(1u);
        stream.Write(30);
        stream.Write((int)Mp2ObjectType.Mine);
        stream.Write(0u);
    }
}
