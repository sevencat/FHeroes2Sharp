using FHeroes2.Assets.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The per-object data a castle carries, <c>Maps::Map_Format::CastleMetadata</c>
/// (<c>src/fheroes2/maps/map_format_info.h:62-107</c>).
/// </summary>
/// <remarks>
/// 颜色、类型与「是城堡还是城堡里的城」都不在这里：它们得跟图形对得上，所以留在物件身上（参考的注释写得很清楚）。
/// 这里只有作者填的东西。名字为空表示让引擎随机起一个；守军里负数是「按阵营默认」，0 是空位。
/// </remarks>
public sealed class CastleMetadata : IEquatable<CastleMetadata>
{
    /// <summary>Name the author gave the castle, empty for a random one, <c>customName</c>.</summary>
    public string CustomName { get; set; } = string.Empty;

    /// <summary>Type of the defending monster of each army slot, <c>defenderMonsterType</c>.</summary>
    public int[] DefenderMonsterType { get; init; } = new int[5];

    /// <summary>How many defenders each slot holds, <c>defenderMonsterCount</c>.</summary>
    public int[] DefenderMonsterCount { get; init; } = new int[5];

    /// <summary>Whether the author picked the buildings, <c>customBuildings</c>.</summary>
    public bool CustomBuildings { get; set; }

    /// <summary>Buildings the castle stands with, <c>builtBuildings</c>.</summary>
    public List<uint> BuiltBuildings { get; init; } = [];

    /// <summary>Buildings that can never be built, <c>bannedBuildings</c>.</summary>
    public List<uint> BannedBuildings { get; init; } = [];

    /// <summary>A mage guild spell that must appear, keyed by its position, <c>mustHaveSpells</c>.</summary>
    /// <remarks>键是十进制两位数：十位是法术等级（0-4），个位是从左数第几个（0-4）。</remarks>
    public SortedDictionary<byte, int> MustHaveSpells { get; init; } = [];

    /// <summary>Spells that must not appear in the mage guild, <c>bannedSpells</c>.</summary>
    public List<int> BannedSpells { get; init; } = [];

    /// <summary>Monsters available to hire in each dwelling, negative meaning "leave it alone", <c>availableToHireMonsterCount</c>.</summary>
    public int[] AvailableToHireMonsterCount { get; init; } = [-1, -1, -1, -1, -1, -1];

    /// <inheritdoc/>
    public bool Equals(CastleMetadata? other)
    {
        return other is not null
               && CustomName == other.CustomName
               && DefenderMonsterType.SequenceEqual(other.DefenderMonsterType)
               && DefenderMonsterCount.SequenceEqual(other.DefenderMonsterCount)
               && CustomBuildings == other.CustomBuildings
               && BuiltBuildings.SequenceEqual(other.BuiltBuildings)
               && BannedBuildings.SequenceEqual(other.BannedBuildings)
               && MustHaveSpells.SequenceEqual(other.MustHaveSpells)
               && BannedSpells.SequenceEqual(other.BannedSpells)
               && AvailableToHireMonsterCount.SequenceEqual(other.AvailableToHireMonsterCount);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CastleMetadata);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(CustomName);
        hash.Add(CustomBuildings);

        foreach (int value in DefenderMonsterType)
        {
            hash.Add(value);
        }

        foreach (int value in DefenderMonsterCount)
        {
            hash.Add(value);
        }

        foreach (uint value in BuiltBuildings)
        {
            hash.Add(value);
        }

        foreach (uint value in BannedBuildings)
        {
            hash.Add(value);
        }

        foreach (KeyValuePair<byte, int> spell in MustHaveSpells)
        {
            hash.Add(spell.Key);
            hash.Add(spell.Value);
        }

        foreach (int value in BannedSpells)
        {
            hash.Add(value);
        }

        foreach (int value in AvailableToHireMonsterCount)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Copies the castle, arrays and lists included, <c>CastleMetadata( const CastleMetadata &amp; )</c>.
    /// </summary>
    /// <remarks>
    /// 参考的拷贝构造对 <c>std::vector</c> 与 <c>std::map</c> 都是逐元素拷贝，所以这里也得深拷贝：浅拷一份拿去做
    /// 「打开窗口时的快照」，之后作者改了建筑，快照会跟着一起变，退出时的「有没有未保存改动」就永远答否。
    /// </remarks>
    /// <returns>An independent copy.</returns>
    public CastleMetadata Clone() => new()
    {
        CustomName = CustomName,
        DefenderMonsterType = (int[])DefenderMonsterType.Clone(),
        DefenderMonsterCount = (int[])DefenderMonsterCount.Clone(),
        CustomBuildings = CustomBuildings,
        BuiltBuildings = [.. BuiltBuildings],
        BannedBuildings = [.. BannedBuildings],
        MustHaveSpells = new(MustHaveSpells),
        BannedSpells = [.. BannedSpells],
        AvailableToHireMonsterCount = (int[])AvailableToHireMonsterCount.Clone()
    };
}

/// <summary>
/// The per-object data a hero carries, <c>Maps::Map_Format::HeroMetadata</c>
/// (<c>src/fheroes2/maps/map_format_info.h:109-174</c>).
/// </summary>
/// <remarks>
/// 颜色与职业不在里面，理由同城堡：得跟图形对得上。<c>-1</c> 是「没填」，让引擎按默认值算；等级与经验互斥，
/// 只填一个。名字为空表示随机起名。
/// </remarks>
public sealed class HeroMetadata : IEquatable<HeroMetadata>
{
    /// <summary>Name the author gave the hero, empty for a random one, <c>customName</c>.</summary>
    public string CustomName { get; set; } = string.Empty;

    /// <summary>Portrait to draw instead of the default one, <c>customPortrait</c>: zero or negative means none.</summary>
    public int CustomPortrait { get; set; }

    /// <summary>Type of the monster in each army slot, zero meaning "not set", <c>armyMonsterType</c>.</summary>
    public int[] ArmyMonsterType { get; init; } = new int[5];

    /// <summary>How many monsters each army slot holds, <c>armyMonsterCount</c>.</summary>
    public int[] ArmyMonsterCount { get; init; } = new int[5];

    /// <summary>Artifact in each inventory slot, zero meaning "not set", <c>artifact</c>.</summary>
    public int[] Artifact { get; init; } = new int[14];

    /// <summary>Extra data of the artifacts, <c>artifactMetadata</c>.</summary>
    public int[] ArtifactMetadata { get; init; } = new int[14];

    /// <summary>Spells the hero's spell book holds; empty for the default set, <c>availableSpells</c>.</summary>
    public List<int> AvailableSpells { get; init; } = [];

    /// <summary>Whether an AI hero patrols instead of wandering, <c>isOnPatrol</c>.</summary>
    public bool IsOnPatrol { get; set; }

    /// <summary>Radius the patrol covers, <c>patrolRadius</c>.</summary>
    public byte PatrolRadius { get; set; }

    /// <summary>Secondary skill of each slot, zero meaning "not set", <c>secondarySkill</c>.</summary>
    public sbyte[] SecondarySkill { get; init; } = new sbyte[8];

    /// <summary>Level of the skill in the matching slot, <c>secondarySkillLevel</c>.</summary>
    public byte[] SecondarySkillLevel { get; init; } = new byte[8];

    /// <summary>Level the author gave the hero, <c>-1</c> when it is not set, <c>customLevel</c>.</summary>
    public short CustomLevel { get; set; } = -1;

    /// <summary>Experience the author gave the hero, <c>-1</c> when it is not set, <c>customExperience</c>.</summary>
    public int CustomExperience { get; set; } = -1;

    /// <summary>Attack the author gave the hero, <c>-1</c> for the default, <c>customAttack</c>.</summary>
    public short CustomAttack { get; set; } = -1;

    /// <summary>Defense the author gave the hero, <c>-1</c> for the default, <c>customDefense</c>.</summary>
    public short CustomDefense { get; set; } = -1;

    /// <summary>Knowledge the author gave the hero, <c>-1</c> for the default, <c>customKnowledge</c>.</summary>
    public short CustomKnowledge { get; set; } = -1;

    /// <summary>Spell power the author gave the hero, <c>-1</c> for the default, <c>customSpellPower</c>.</summary>
    public short CustomSpellPower { get; set; } = -1;

    /// <summary>Magic points of the hero, negative when not set, <c>magicPoints</c>.</summary>
    public short MagicPoints { get; set; } = -1;

    /// <summary>Race of the hero, <c>race</c>.</summary>
    public byte Race { get; set; }

    /// <inheritdoc/>
    public bool Equals(HeroMetadata? other)
    {
        return other is not null
               && CustomName == other.CustomName
               && CustomPortrait == other.CustomPortrait
               && IsOnPatrol == other.IsOnPatrol
               && PatrolRadius == other.PatrolRadius
               && CustomLevel == other.CustomLevel
               && CustomExperience == other.CustomExperience
               && CustomAttack == other.CustomAttack
               && CustomDefense == other.CustomDefense
               && CustomKnowledge == other.CustomKnowledge
               && CustomSpellPower == other.CustomSpellPower
               && MagicPoints == other.MagicPoints
               && Race == other.Race
               && ArmyMonsterType.SequenceEqual(other.ArmyMonsterType)
               && ArmyMonsterCount.SequenceEqual(other.ArmyMonsterCount)
               && Artifact.SequenceEqual(other.Artifact)
               && ArtifactMetadata.SequenceEqual(other.ArtifactMetadata)
               && AvailableSpells.SequenceEqual(other.AvailableSpells)
               && SecondarySkill.SequenceEqual(other.SecondarySkill)
               && SecondarySkillLevel.SequenceEqual(other.SecondarySkillLevel);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as HeroMetadata);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(CustomName);
        hash.Add(CustomPortrait);
        hash.Add(IsOnPatrol);
        hash.Add(PatrolRadius);
        hash.Add(CustomLevel);
        hash.Add(CustomExperience);
        hash.Add(CustomAttack);
        hash.Add(CustomDefense);
        hash.Add(CustomKnowledge);
        hash.Add(CustomSpellPower);
        hash.Add(MagicPoints);
        hash.Add(Race);

        foreach (int value in ArmyMonsterType)
        {
            hash.Add(value);
        }

        foreach (int value in ArmyMonsterCount)
        {
            hash.Add(value);
        }

        foreach (int value in Artifact)
        {
            hash.Add(value);
        }

        foreach (int value in ArtifactMetadata)
        {
            hash.Add(value);
        }

        foreach (int value in AvailableSpells)
        {
            hash.Add(value);
        }

        foreach (sbyte value in SecondarySkill)
        {
            hash.Add(value);
        }

        foreach (byte value in SecondarySkillLevel)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// The riddle, its answers and the reward of a sphinx, <c>Maps::Map_Format::SphinxMetadata</c>
/// (<c>src/fheroes2/maps/map_format_info.h:176-200</c>).
/// </summary>
public sealed class SphinxMetadata : IEquatable<SphinxMetadata>
{
    /// <summary>The riddle asked, <c>riddle</c>.</summary>
    public string Riddle { get; set; } = string.Empty;

    /// <summary>The answers that pass, <c>answers</c>.</summary>
    public List<string> Answers { get; init; } = [];

    /// <summary>Artifact given as a reward, zero for none, <c>artifact</c>.</summary>
    public int Artifact { get; set; }

    /// <summary>Extra data of the rewarded artifact, <c>artifactMetadata</c>.</summary>
    public int ArtifactMetadata { get; set; }

    /// <summary>Resources given as a reward, <c>resources</c>.</summary>
    public Funds Resources { get; set; }

    /// <inheritdoc/>
    public bool Equals(SphinxMetadata? other)
    {
        return other is not null
               && Riddle == other.Riddle
               && Answers.SequenceEqual(other.Answers)
               && Artifact == other.Artifact
               && ArtifactMetadata == other.ArtifactMetadata
               && Resources == other.Resources;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SphinxMetadata);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(Riddle);
        hash.Add(Artifact);
        hash.Add(ArtifactMetadata);
        hash.Add(Resources);

        foreach (string answer in Answers)
        {
            hash.Add(answer);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// The message a sign shows, <c>Maps::Map_Format::SignMetadata</c>
/// (<c>src/fheroes2/maps/map_format_info.h:202-205</c>).
/// </summary>
public sealed class SignMetadata
{
    /// <summary>The text on the sign, <c>message</c>.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// What happens when a hero steps on an adventure map event,
/// <c>Maps::Map_Format::AdventureMapEventMetadata</c> (<c>src/fheroes2/maps/map_format_info.h:207-254</c>).
/// </summary>
/// <remarks>
/// 颜色是 <c>PlayerColorsSet</c> 的位掩码，端口按参考的底层类型存一个字节。
/// </remarks>
public sealed class AdventureMapEventMetadata : IEquatable<AdventureMapEventMetadata>
{
    /// <summary>The message shown when the event fires, <c>message</c>.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Human players the event applies to, <c>humanPlayerColors</c>.</summary>
    public byte HumanPlayerColors { get; set; }

    /// <summary>Computer players the event applies to, <c>computerPlayerColors</c>.</summary>
    public byte ComputerPlayerColors { get; set; }

    /// <summary>Whether the event fires every time a hero steps on it, <c>isRecurringEvent</c>.</summary>
    public bool IsRecurringEvent { get; set; }

    /// <summary>Artifact given as a reward, zero for none, <c>artifact</c>.</summary>
    public int Artifact { get; set; }

    /// <summary>Extra data of the rewarded artifact, <c>artifactMetadata</c>.</summary>
    public int ArtifactMetadata { get; set; }

    /// <summary>Resources given as a reward, <c>resources</c>.</summary>
    public Funds Resources { get; set; }

    /// <summary>Attack the hero gains, <c>attack</c>.</summary>
    public short Attack { get; set; }

    /// <summary>Defense the hero gains, <c>defense</c>.</summary>
    public short Defense { get; set; }

    /// <summary>Knowledge the hero gains, <c>knowledge</c>.</summary>
    public short Knowledge { get; set; }

    /// <summary>Spell power the hero gains, <c>spellPower</c>.</summary>
    public short SpellPower { get; set; }

    /// <summary>Experience the hero gains, <c>experience</c>.</summary>
    public int Experience { get; set; }

    /// <summary>Secondary skill the hero gains, zero for none, <c>secondarySkill</c>.</summary>
    public byte SecondarySkill { get; set; }

    /// <summary>Level of the gained skill, <c>secondarySkillLevel</c>.</summary>
    public byte SecondarySkillLevel { get; set; }

    /// <summary>Monster type the event spawns, zero for none, <c>monsterType</c>.</summary>
    public int MonsterType { get; set; }

    /// <summary>How many monsters the event spawns, <c>monsterCount</c>.</summary>
    public int MonsterCount { get; set; }

    /// <inheritdoc/>
    public bool Equals(AdventureMapEventMetadata? other)
    {
        return other is not null
               && Message == other.Message
               && HumanPlayerColors == other.HumanPlayerColors
               && ComputerPlayerColors == other.ComputerPlayerColors
               && IsRecurringEvent == other.IsRecurringEvent
               && Artifact == other.Artifact
               && ArtifactMetadata == other.ArtifactMetadata
               && Resources == other.Resources
               && Attack == other.Attack
               && Defense == other.Defense
               && Knowledge == other.Knowledge
               && SpellPower == other.SpellPower
               && Experience == other.Experience
               && SecondarySkill == other.SecondarySkill
               && SecondarySkillLevel == other.SecondarySkillLevel
               && MonsterType == other.MonsterType
               && MonsterCount == other.MonsterCount;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as AdventureMapEventMetadata);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(Message);
        hash.Add(HumanPlayerColors);
        hash.Add(ComputerPlayerColors);
        hash.Add(IsRecurringEvent);
        hash.Add(Artifact);
        hash.Add(ArtifactMetadata);
        hash.Add(Resources);
        hash.Add(Attack);
        hash.Add(Defense);
        hash.Add(Knowledge);
        hash.Add(SpellPower);
        hash.Add(Experience);
        hash.Add(SecondarySkill);
        hash.Add(SecondarySkillLevel);
        hash.Add(MonsterType);
        hash.Add(MonsterCount);

        return hash.ToHashCode();
    }

    /// <summary>
    /// Makes a copy; the details window edits its own copy and hands it back when the author accepts.
    /// </summary>
    /// <returns>An independent copy of every field.</returns>
    public AdventureMapEventMetadata Clone() => new()
    {
        Message = Message,
        HumanPlayerColors = HumanPlayerColors,
        ComputerPlayerColors = ComputerPlayerColors,
        IsRecurringEvent = IsRecurringEvent,
        Artifact = Artifact,
        ArtifactMetadata = ArtifactMetadata,
        Resources = Resources,
        Attack = Attack,
        Defense = Defense,
        Knowledge = Knowledge,
        SpellPower = SpellPower,
        Experience = Experience,
        SecondarySkill = SecondarySkill,
        SecondarySkillLevel = SecondarySkillLevel,
        MonsterType = MonsterType,
        MonsterCount = MonsterCount
    };
}

/// <summary>
/// Which items a "pick one of these" object offers,
/// <c>Maps::Map_Format::SelectionObjectMetadata</c> (<c>src/fheroes2/maps/map_format_info.h:256-259</c>).
/// </summary>
public sealed class SelectionObjectMetadata
{
    /// <summary>The items the author picked, <c>selectedItems</c>.</summary>
    public List<int> SelectedItems { get; init; } = [];
}

/// <summary>
/// How many monsters a monster stack holds, <c>Maps::Map_Format::MonsterMetadata</c>
/// (<c>src/fheroes2/maps/map_format_info.h:266-278</c>).
/// </summary>
/// <remarks>后两个字段参考明确写着「先留着，还没用」。</remarks>
public sealed class MonsterMetadata
{
    /// <summary>How many monsters stand on the tile, <c>count</c>.</summary>
    public int Count { get; set; }

    /// <summary>Reserved by the reference, <c>joinCondition</c>.</summary>
    public int JoinCondition { get; set; }

    /// <summary>Reserved by the reference, <c>isWeeklyGrowthDisabled</c>.</summary>
    public bool IsWeeklyGrowthDisabled { get; set; }

    /// <summary>Monsters a random monster stack may become, <c>selected</c>.</summary>
    public List<int> Selected { get; init; } = [];
}

/// <summary>
/// The extra data of an artifact on the map, <c>Maps::Map_Format::ArtifactMetadata</c>
/// (<c>src/fheroes2/maps/map_format_info.h:280-290</c>).
/// </summary>
public sealed class ArtifactMetadata
{
    /// <summary>Radius the ultimate artifact is buried in, <c>radius</c>.</summary>
    public int Radius { get; set; }

    /// <summary>Reserved by the reference, <c>captureCondition</c>.</summary>
    public int CaptureCondition { get; set; }

    /// <summary>Artifacts a random artifact or a spell scroll may hold, <c>selected</c>.</summary>
    public List<int> Selected { get; init; } = [];
}

/// <summary>
/// How much of a resource a pile holds, <c>Maps::Map_Format::ResourceMetadata</c>
/// (<c>src/fheroes2/maps/map_format_info.h:292-295</c>).
/// </summary>
public sealed class ResourceMetadata
{
    /// <summary>Amount of the resource, <c>count</c>.</summary>
    public int Count { get; set; }
}

/// <summary>
/// Who owns a capturable object, <c>Maps::Map_Format::CapturableObjectMetadata</c>
/// (<c>src/fheroes2/maps/map_format_info.h:297-300</c>).
/// </summary>
/// <remarks>
/// 矿洞、炼金实验室、锯木厂、灯塔共用这一本表：它们的区别在图形上，归属只在作者改过之后才记一笔。
/// </remarks>
public sealed class CapturableObjectMetadata
{
    /// <summary>Player the object belongs to, <c>ownerColor</c>.</summary>
    public PlayerColor OwnerColor { get; set; }
}

/// <summary>
/// The per-object metadata tables of a map, the ten maps of
/// <c>Maps::Map_Format::MapFormat</c> (<c>src/fheroes2/maps/map_format_info.h:400-419</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把「图上一件物件除此之外还记着什么」按物件编号存成九本表，撤销时只换表不存整图，所以改一个城堡的名字不必
/// 复制整张地图。端口的地图是 MP2 记录，没有这套表，这里补上：键是物件 UID，与 <c>Mp2Map.TakeObjectUid</c> 同源。
/// </para>
/// <para>
/// 表用有序字典，跟参考的 <c>std::map</c> 一样按键遍历：存档要按编号顺序写，撤销也要能稳定地比出前后两份。
/// </para>
/// </remarks>
public sealed class MapObjectMetadataStore
{
    /// <summary>The metadata of castles by object UID, <c>castleMetadata</c>.</summary>
    public SortedDictionary<uint, CastleMetadata> Castles { get; } = [];

    /// <summary>The metadata of heroes by object UID, <c>heroMetadata</c>.</summary>
    public SortedDictionary<uint, HeroMetadata> Heroes { get; } = [];

    /// <summary>The metadata of sphinxes by object UID, <c>sphinxMetadata</c>.</summary>
    public SortedDictionary<uint, SphinxMetadata> Sphinxes { get; } = [];

    /// <summary>The metadata of signs by object UID, <c>signMetadata</c>.</summary>
    public SortedDictionary<uint, SignMetadata> Signs { get; } = [];

    /// <summary>The metadata of adventure map events by object UID, <c>adventureMapEventMetadata</c>.</summary>
    public SortedDictionary<uint, AdventureMapEventMetadata> AdventureMapEvents { get; } = [];

    /// <summary>The metadata of "pick one of these" objects by object UID, <c>selectionObjectMetadata</c>.</summary>
    public SortedDictionary<uint, SelectionObjectMetadata> Selections { get; } = [];

    /// <summary>The metadata of monster stacks by object UID, <c>monsterMetadata</c>.</summary>
    public SortedDictionary<uint, MonsterMetadata> Monsters { get; } = [];

    /// <summary>The metadata of artifacts by object UID, <c>artifactMetadata</c>.</summary>
    public SortedDictionary<uint, ArtifactMetadata> Artifacts { get; } = [];

    /// <summary>The metadata of resource piles by object UID, <c>resourceMetadata</c>.</summary>
    public SortedDictionary<uint, ResourceMetadata> Resources { get; } = [];

    /// <summary>The owner of capturable objects by object UID, <c>capturableObjectsMetadata</c>.</summary>
    /// <remarks>
    /// 这本表故意没有动作类型：参考的注释说得很清楚（<c>history_manager.h:80-82</c>），改归属要连地图上的旗子一起
    /// 重画，所以走整图快照那一档，而不是像另外九本那样只换一张表。
    /// </remarks>
    public SortedDictionary<uint, CapturableObjectMetadata> CapturableObjects { get; } = [];

    /// <summary>
    /// An action that tracks one of the tables, the <c>switch</c> of the reference's
    /// <c>ActionCreator</c> constructor (<c>src/fheroes2/editor/history_manager.cpp:167-202</c>).
    /// </summary>
    /// <param name="actionType">One of <see cref="ActionCreator.ActionType"/> except <c>Generic</c>.</param>
    /// <returns>The action, bound to the table of that type.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="actionType"/> is not a metadata type.</exception>
    public MapAction CreateAction(int actionType)
    {
        return actionType switch
        {
            ActionCreator.ActionType.HeroMetadata => new MetadataMapAction<HeroMetadata>(Heroes),
            ActionCreator.ActionType.CastleMetadata => new MetadataMapAction<CastleMetadata>(Castles),
            ActionCreator.ActionType.SphinxMetadata => new MetadataMapAction<SphinxMetadata>(Sphinxes),
            ActionCreator.ActionType.SignMetadata => new MetadataMapAction<SignMetadata>(Signs),
            ActionCreator.ActionType.AdventureMapEventMetadata => new MetadataMapAction<AdventureMapEventMetadata>(AdventureMapEvents),
            ActionCreator.ActionType.SelectionMetadata => new MetadataMapAction<SelectionObjectMetadata>(Selections),
            ActionCreator.ActionType.MonsterMetadata => new MetadataMapAction<MonsterMetadata>(Monsters),
            ActionCreator.ActionType.ArtifactMetadata => new MetadataMapAction<ArtifactMetadata>(Artifacts),
            ActionCreator.ActionType.ResourceMetadata => new MetadataMapAction<ResourceMetadata>(Resources),
            _ => throw new ArgumentOutOfRangeException(
                nameof(actionType), actionType, "No metadata table of the map belongs to this action type."),
        };
    }
}
