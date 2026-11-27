using System.Buffers.Binary;

namespace FHeroes2.Assets.Maps;

/// <summary>
/// The hero information block of an MP2 file, <c>MP2::mp2hero_t</c> / <c>MP2_HEROES_STRUCTURE_SIZE</c>
/// (76 bytes, <c>mp2.h</c>), as read by <c>Heroes::LoadFromMP2()</c>
/// (<c>heroes.cpp:421</c>).
/// </summary>
/// <remarks>
/// <para>
/// A hero is placed on the map by two things: the tile (position, color and race in the <c>MINIHERO</c>
/// sprite) and this block, which carries everything else — the army, the artifacts, the experience, the
/// secondary skills, the custom name and the role as a patrolling guard. The block is referred to by the
/// 13 bit <c>quantity</c> word of its tile: <see cref="Mp2InfoBlock.ObjectId"/> is that word, so the tile
/// with <c>metadata[0] == ObjectId</c> is the hero the block describes.
/// </para>
/// <para>
/// <b>Raw MP2 values.</b> Monster, artifact and secondary skill ids count from 0 in the file and from 1
/// in the engine, and the portrait byte counts from 0 as well (the reference does the <c>+ 1</c> at the
/// point of use, <c>heroes.cpp:467</c>); this type keeps the file values, like <see cref="Mp2TileInfo"/>
/// does, so that a round trip through the writer stays lossless. <see cref="PortraitId"/> is the raw byte,
/// the engine id is <c>PortraitId + 1</c>.
/// </para>
/// <para>
/// The structure has three bytes nobody reads (<c>0</c>, <c>22</c>, <c>44</c>) and 15 more at the end; they
/// are not exposed. The reference reads them into the struct and ignores them.
/// </para>
/// </remarks>
public sealed class Mp2HeroInfo
{
    /// <summary>Size of the block, <c>MP2::MP2_HEROES_STRUCTURE_SIZE</c> (<c>mp2.h</c>).</summary>
    public const int StructureSize = 76;

    /// <summary>Slots in the army, <c>ARMYMAXSLOT</c>.</summary>
    public const int TroopCount = 5;

    /// <summary>Artifact slots of the block.</summary>
    public const int ArtifactCount = 3;

    /// <summary>Secondary skill slots, <c>HEROESMAXSECONDARYSKILL</c>.</summary>
    public const int SecondarySkillCount = 8;

    /// <summary>Size of the name field, including the terminator.</summary>
    public const int NameFieldSize = 13;

    /// <summary>Offset of the custom army flag.</summary>
    private const int CustomArmyOffset = 1;

    /// <summary>Offset of the monster types.</summary>
    private const int MonsterOffset = 2;

    /// <summary>Offset of the monster counts, five little endian 16 bit values.</summary>
    private const int MonsterCountOffset = 7;

    /// <summary>Offset of the custom portrait flag.</summary>
    private const int CustomPortraitOffset = 17;

    /// <summary>Offset of the portrait id; <c>pblock[18]</c> in the reference.</summary>
    private const int PortraitOffset = 18;

    /// <summary>Offset of the three artifact ids.</summary>
    private const int ArtifactOffset = 19;

    /// <summary>Offset of the experience, a little endian 32 bit value.</summary>
    private const int ExperienceOffset = 23;

    /// <summary>Offset of the custom secondary skill flag.</summary>
    private const int CustomSkillOffset = 27;

    /// <summary>Offset of the secondary skill ids.</summary>
    private const int SkillOffset = 28;

    /// <summary>Offset of the secondary skill levels.</summary>
    private const int SkillLevelOffset = 36;

    /// <summary>Offset of the custom name flag.</summary>
    private const int CustomNameOffset = 45;

    /// <summary>Offset of the name field.</summary>
    private const int NameOffset = 46;

    /// <summary>Offset of the patrol flag.</summary>
    private const int PatrolOffset = 59;

    /// <summary>
    /// Offset of the patrol distance, or of the race of a hero that waits in a jail when
    /// <see cref="IsOnPatrol"/> is not set and the block belongs to a jail.
    /// </summary>
    private const int PatrolDistanceOffset = 60;

    /// <summary>
    /// Creates an empty block: no custom army, portrait, artifacts, skills or name. The reference reads a zeroed
    /// structure when a hero tile has no block behind it, which is the same thing.
    /// </summary>
    public Mp2HeroInfo()
    {
    }

    /// <summary>
    /// Whether the block carries an army of its own, <c>pblock[1]</c>. When it does not, the hero gets the
    /// army of his race at the default strength.
    /// </summary>
    public bool HasCustomArmy { get; init; }

    /// <summary>Monster ids of the five army slots, raw MP2 values (engine id is <c>+ 1</c>).</summary>
    public IReadOnlyList<int> MonsterIds { get; init; } = [];

    /// <summary>Counts of the five army slots; zero when the slot is empty.</summary>
    public IReadOnlyList<int> MonsterCounts { get; init; } = [];

    /// <summary>Whether the block carries a portrait, <c>pblock[17]</c>.</summary>
    public bool HasCustomPortrait { get; init; }

    /// <summary>Portrait id, raw MP2 byte; the engine id is <c>PortraitId + 1</c>.</summary>
    public int PortraitId { get; init; }

    /// <summary>Artifact ids of the three slots, raw MP2 values (engine id is <c>+ 1</c>).</summary>
    public IReadOnlyList<int> ArtifactIds { get; init; } = [];

    /// <summary>Experience of the hero, <c>pblock[23]</c>.</summary>
    public uint Experience { get; init; }

    /// <summary>Whether the block carries secondary skills, <c>pblock[27]</c>.</summary>
    public bool HasCustomSecondarySkills { get; init; }

    /// <summary>Secondary skill ids of the eight slots, raw MP2 values (engine id is <c>+ 1</c>).</summary>
    public IReadOnlyList<int> SecondarySkillIds { get; init; } = [];

    /// <summary>Levels the eight secondary skills start at.</summary>
    public IReadOnlyList<int> SecondarySkillLevels { get; init; } = [];

    /// <summary>Whether the block carries a name, <c>pblock[45]</c>.</summary>
    public bool HasCustomName { get; init; }

    /// <summary>The custom name, empty when there is none.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether the hero patrols, <c>pblock[59]</c>. A patrolling hero is a guard: he does not leave the
    /// area around his position.
    /// </summary>
    public bool IsOnPatrol { get; init; }

    /// <summary>
    /// The patrol distance, or the race of the hero of a jail when the block describes a jail rather than
    /// a hero on the map, <c>pblock[60]</c>.
    /// </summary>
    public int PatrolDistanceOrJailRace { get; init; }

    /// <summary>
    /// Reads a hero block, <c>Heroes::LoadFromMP2()</c> (<c>heroes.cpp:421</c>).
    /// </summary>
    /// <remarks>
    /// A block of the wrong size is not a hero block. The reference compares the size and logs a warning
    /// before it reads anything (<c>world_loadmap.cpp:584</c>), so a caller that gets
    /// <see langword="false"/> should skip the object rather than guess.
    /// </remarks>
    /// <param name="data">Payload of the information block, without its size prefix.</param>
    /// <param name="info">The block when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="data"/> is a hero block.</returns>
    public static bool TryRead(ReadOnlySpan<byte> data, out Mp2HeroInfo info)
    {
        info = Empty;

        if (data.Length != StructureSize)
        {
            return false;
        }

        int[] monsters = new int[TroopCount];
        int[] counts = new int[TroopCount];

        for (int slot = 0; slot < TroopCount; ++slot)
        {
            monsters[slot] = data[MonsterOffset + slot];
            counts[slot] = BinaryPrimitives.ReadUInt16LittleEndian(data[(MonsterCountOffset + (slot * 2))..]);
        }

        int[] artifacts = new int[ArtifactCount];
        for (int slot = 0; slot < ArtifactCount; ++slot)
        {
            artifacts[slot] = data[ArtifactOffset + slot];
        }

        int[] skillIds = new int[SecondarySkillCount];
        int[] skillLevels = new int[SecondarySkillCount];
        for (int slot = 0; slot < SecondarySkillCount; ++slot)
        {
            skillIds[slot] = data[SkillOffset + slot];
            skillLevels[slot] = data[SkillLevelOffset + slot];
        }

        info = new Mp2HeroInfo
        {
            HasCustomArmy = data[CustomArmyOffset] != 0,
            MonsterIds = monsters,
            MonsterCounts = counts,
            HasCustomPortrait = data[CustomPortraitOffset] != 0,
            PortraitId = data[PortraitOffset],
            ArtifactIds = artifacts,
            Experience = BinaryPrimitives.ReadUInt32LittleEndian(data[ExperienceOffset..]),
            HasCustomSecondarySkills = data[CustomSkillOffset] != 0,
            SecondarySkillIds = skillIds,
            SecondarySkillLevels = skillLevels,
            HasCustomName = data[CustomNameOffset] != 0,
            Name = MapTextCodec.Decode(data.Slice(NameOffset, NameFieldSize)),
            IsOnPatrol = data[PatrolOffset] != 0,
            PatrolDistanceOrJailRace = data[PatrolDistanceOffset],
        };

        return true;
    }

    /// <summary>
    /// Writes the block back into the 76 bytes <see cref="TryRead"/> reads, the mirror of
    /// <c>Heroes::LoadFromMP2()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考没有写端（fheroes2 只写自家的 <c>.fh2m</c>），所以这是从读端逐偏移反推出来的：每个字节要么由某个字段决定，
    /// 要么是读端明确忽略的三处（0、22、44）与尾部 15 字节，后者一律写 0。
    /// </para>
    /// <para>
    /// <b>名字字段</b>用 <see cref="MapTextCodec.Encode"/>，与地图头的文本同一套编码与截断规则；<see cref="HasCustomName"/>
    /// 为假时整个字段留 0（读端只在那面旗帜非零时才解名字）。
    /// </para>
    /// </remarks>
    /// <returns>The 76 bytes of the block, without its size prefix.</returns>
    public byte[] ToBlock()
    {
        byte[] block = new byte[StructureSize];

        block[CustomArmyOffset] = HasCustomArmy ? (byte)1 : (byte)0;

        for (int slot = 0; slot < TroopCount; ++slot)
        {
            block[MonsterOffset + slot] = (byte)(slot < MonsterIds.Count ? MonsterIds[slot] : 0);

            int count = slot < MonsterCounts.Count ? MonsterCounts[slot] : 0;

            BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(MonsterCountOffset + (slot * 2)), (ushort)count);
        }

        block[CustomPortraitOffset] = HasCustomPortrait ? (byte)1 : (byte)0;
        block[PortraitOffset] = (byte)PortraitId;

        for (int slot = 0; slot < ArtifactCount; ++slot)
        {
            block[ArtifactOffset + slot] = (byte)(slot < ArtifactIds.Count ? ArtifactIds[slot] : 0);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(ExperienceOffset), Experience);

        block[CustomSkillOffset] = HasCustomSecondarySkills ? (byte)1 : (byte)0;

        for (int slot = 0; slot < SecondarySkillCount; ++slot)
        {
            block[SkillOffset + slot] = (byte)(slot < SecondarySkillIds.Count ? SecondarySkillIds[slot] : 0);
            block[SkillLevelOffset + slot] = (byte)(slot < SecondarySkillLevels.Count ? SecondarySkillLevels[slot] : 0);
        }

        block[CustomNameOffset] = HasCustomName ? (byte)1 : (byte)0;

        if (HasCustomName)
        {
            MapTextCodec.Encode(Name, NameFieldSize).CopyTo(block, NameOffset);
        }

        block[PatrolOffset] = IsOnPatrol ? (byte)1 : (byte)0;
        block[PatrolDistanceOffset] = (byte)PatrolDistanceOrJailRace;

        return block;
    }

    /// <summary>
    /// The value <see cref="TryRead"/> hands back on failure: a block that carries nothing, mirroring the
    /// zeroed structure the reference reads into.
    /// </summary>
    private static Mp2HeroInfo Empty { get; } = new();
}
