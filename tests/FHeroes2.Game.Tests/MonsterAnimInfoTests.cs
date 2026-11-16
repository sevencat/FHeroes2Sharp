using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the <c>.BIN</c> reader of <c>src/fheroes2/agg/bin_info.cpp</c>: the fixed byte layout
/// (<c>bin_info.cpp:88-248</c>), the corrections the reference applies to the original data
/// (<c>bin_info.cpp:250-490</c>) and the two guards <c>isValid</c> / <c>getProjectileID</c>.
/// </summary>
/// <remarks>
/// 大部分用例自己拼一条 821 字节的记录，这样每个偏移都能单独敲一边；期望值来自参考的偏移算式，
/// 不是从端口读回来的。少数用例用真实游戏数据，数据没装就跳过。
/// </remarks>
public class MonsterAnimInfoTests
{
    [Fact]
    public void ARecordOfTheWrongLengthIsEmpty()
    {
        MonsterAnimInfo info = new(MonsterType.Peasant, new byte[MonsterAnimInfo.CorrectFrameLength - 1]);

        Assert.Empty(info.FrameXOffset);
        Assert.Empty(info.AnimationFrames);
        Assert.Empty(info.ProjectileOffset);
        Assert.Empty(info.ProjectileAngles);
        Assert.Empty(info.IdlePriority);
        Assert.Empty(info.UnusedIdleDelays);
        Assert.Equal(new Point(0, 0), info.EyePosition);
        Assert.Equal(0u, info.IdleAnimationCount);
        Assert.False(info.IsValid());
        Assert.False(info.HasAnim());
    }

    [Fact]
    public void TheDefaultRecordKeepsTheReferenceMoveSpeed()
    {
        // 参考：bin_info.h:73 的 moveSpeed{ 450 }。
        Assert.Equal(450u, new MonsterAnimInfo().MoveSpeed);
    }

    [Fact]
    public void TheHeadOfTheRecordIsReadAtTheReferenceOffsets()
    {
        byte[] record = BuildRecord();

        // 参考：bin_info.cpp:96 的眼睛位置、126-129 的三支速度、231-233 的计数器偏移。
        WriteInt16(record, 1, -5);
        WriteInt16(record, 3, 7);
        WriteUInt32(record, 162, 1000);
        WriteUInt32(record, 166, 2000);
        WriteUInt32(record, 170, 3000);
        WriteInt32(record, 235, -44);
        WriteInt32(record, 239, 44);

        MonsterAnimInfo info = new(MonsterType.Peasant, record);

        Assert.Equal(new Point(-5, 7), info.EyePosition);
        Assert.Equal(1000u, info.MoveSpeed);
        Assert.Equal(2000u, info.ShootSpeed);
        Assert.Equal(3000u, info.FlightSpeed);
        Assert.Equal(-44, info.TroopCountOffsetLeft);
        Assert.Equal(44, info.TroopCountOffsetRight);
    }

    [Fact]
    public void TheIdleAnimationsAreCappedAtFive()
    {
        byte[] record = BuildRecord();
        record[117] = 7;

        // 参考：bin_info.cpp:112 把条数压到 5，权重与延迟都按压过的条数读。
        MonsterAnimInfo info = new(MonsterType.Peasant, record);

        Assert.Equal(5u, info.IdleAnimationCount);
        Assert.Equal(5, info.IdlePriority.Length);
        Assert.Equal(5, info.UnusedIdleDelays.Length);
    }

    [Fact]
    public void TheIdleTimingIsReadAtTheReferenceOffsets()
    {
        byte[] record = BuildRecord();
        record[117] = 2;
        WriteSingle(record, 118, 0.75f);
        WriteSingle(record, 122, 0.25f);
        WriteUInt32(record, 138, 11);
        WriteUInt32(record, 142, 22);
        WriteUInt32(record, 158, 4000);

        MonsterAnimInfo info = new(MonsterType.Peasant, record);

        Assert.Equal(new[] { 0.75f, 0.25f }, info.IdlePriority);
        Assert.Equal(new[] { 11u, 22u }, info.UnusedIdleDelays);
        Assert.Equal(4000u, info.IdleAnimationDelay);
    }

    [Fact]
    public void TheProjectileAnglesAreCappedAtTwelve()
    {
        byte[] record = BuildRecord();
        record[186] = 20;

        // 参考：bin_info.cpp:223 把条数压到 12。
        MonsterAnimInfo info = new(MonsterType.Peasant, record);

        Assert.Equal(12, info.ProjectileAngles.Length);
    }

    [Fact]
    public void TheProjectileIdPicksTheSlotTheAngleFallsInto()
    {
        byte[] record = BuildRecord();
        record[186] = 3;
        WriteSingle(record, 187, 30.0f);
        WriteSingle(record, 191, 20.0f);
        WriteSingle(record, 195, 10.0f);

        MonsterAnimInfo info = new(MonsterType.Peasant, record);

        // 参考：bin_info.cpp:509-523 从上方往下扫，落在哪一段就用哪一段，末段是兜底。
        Assert.Equal(0, info.GetProjectileId(29.0));
        Assert.Equal(0, info.GetProjectileId(25.0));
        Assert.Equal(1, info.GetProjectileId(24.9));
        Assert.Equal(1, info.GetProjectileId(15.0));
        Assert.Equal(2, info.GetProjectileId(14.9));
    }

    [Fact]
    public void TheFrameNumbersAreReadPerAnimation()
    {
        byte[] record = BuildRecord();
        record[243 + (int)MonsterAnimId.Static] = 2;
        record[277 + ((int)MonsterAnimId.Static * 16) + 0] = 3;
        record[277 + ((int)MonsterAnimId.Static * 16) + 1] = 9;

        MonsterAnimInfo info = new(MonsterType.Peasant, record);

        Assert.Equal(new[] { 3, 9 }, info.AnimationFrames[(int)MonsterAnimId.Static]);
        Assert.True(info.HasAnim(MonsterAnimId.Static));
        Assert.False(info.HasAnim(MonsterAnimId.Death));
    }

    [Fact]
    public void TheFrameCountIsCappedAtSixteen()
    {
        byte[] record = BuildRecord();
        record[243 + (int)MonsterAnimId.MoveMain] = 40;

        // 参考：bin_info.cpp:239 把帧数压到 16；未被写过的帧号是 0。
        MonsterAnimInfo info = new(MonsterType.Peasant, record);

        Assert.Equal(16, info.AnimationFrames[(int)MonsterAnimId.MoveMain].Length);
    }

    [Fact]
    public void TheXOffsetsAreSignedBytes()
    {
        byte[] record = BuildRecord();
        record[5 + 1] = unchecked((byte)-7);

        // 偏移行会被裁到该动画的帧数（bin_info.cpp:315-317），所以先给 MOVE_START 十六帧。
        record[243 + (int)MonsterAnimId.MoveStart] = 16;

        MonsterAnimInfo info = new(MonsterType.Peasant, record);

        Assert.Equal(-7, info.FrameXOffset[(int)MonsterAnimId.MoveStart][1]);
    }

    [Fact]
    public void ARecordWithoutTheMandatoryAnimationsIsInvalid()
    {
        MonsterAnimInfo info = new(MonsterType.Peasant, BuildRecord());

        // 参考：bin_info.cpp:493-507 要求移动、站立、死亡、受击与三个方向的攻击。
        Assert.False(info.IsValid());
    }

    [Fact]
    public void ARecordWithTheMandatoryAnimationsIsValid()
    {
        byte[] record = BuildRecord();
        record[117] = 0;

        foreach (MonsterAnimId animId in (ReadOnlySpan<MonsterAnimId>)
                 [
                     MonsterAnimId.MoveMain,
                     MonsterAnimId.Static,
                     MonsterAnimId.Death,
                     MonsterAnimId.WinceUp,
                     MonsterAnimId.Attack1,
                     MonsterAnimId.Attack2,
                     MonsterAnimId.Attack3,
                 ])
        {
            record[243 + (int)animId] = 1;
            record[277 + ((int)animId * 16)] = 0;
        }

        // MOVE_MAIN 与 MOVE_ONE 的偏移行会被对齐到帧数，落地动画的那一支才有值可推。
        record[277 + ((int)MonsterAnimId.MoveMain * 16)] = 5;

        MonsterAnimInfo info = new(MonsterType.Peasant, record);

        Assert.True(info.IsValid());
    }

    [Fact]
    public void TheWolfAttackFrameIsCorrected()
    {
        byte[] record = BuildRecord();
        record[243 + (int)MonsterAnimId.Attack3] = 3;
        record[277 + ((int)MonsterAnimId.Attack3 * 16)] = 16;
        record[243 + (int)MonsterAnimId.Attack3End] = 3;
        record[277 + ((int)MonsterAnimId.Attack3End * 16) + 2] = 16;

        MonsterAnimInfo info = new(MonsterType.Wolf, record);

        // 参考：bin_info.cpp:250-257。
        Assert.Equal(2, info.AnimationFrames[(int)MonsterAnimId.Attack3][0]);
        Assert.Equal(2, info.AnimationFrames[(int)MonsterAnimId.Attack3End][2]);
    }

    [Fact]
    public void TheDwarfDeathAnimationIsReplaced()
    {
        byte[] record = BuildRecord();
        record[243 + (int)MonsterAnimId.Death] = 8;
        for (int frame = 0; frame < 8; ++frame)
        {
            record[277 + ((int)MonsterAnimId.Death * 16) + frame] = (byte)frame;
        }

        MonsterAnimInfo info = new(MonsterType.Dwarf, record);

        // 参考：bin_info.cpp:259-266，49…55 共七帧。
        Assert.Equal(new[] { 49, 50, 51, 52, 53, 54, 55 }, info.AnimationFrames[(int)MonsterAnimId.Death]);
    }

    [Fact]
    public void AnUpgradeScalesTheMoveSpeedByTheSpeedDifference()
    {
        MonsterAnimInfo info = new(MonsterType.Ranger, BuildRecord(moveSpeed: 1000, shootSpeed: 1000));

        // 参考：bin_info.cpp:300-302。游侠比弓箭手快 2 点，移动速度乘 (1 - 0.12 * 2)。
        int speedDiff = (int)MonsterStatistics.GetBattleStats(MonsterType.Ranger).Speed
                        - (int)MonsterStatistics.GetBattleStats(MonsterType.Archer).Speed;

        Assert.Equal(2, speedDiff);
        Assert.Equal((uint)((1 - (0.12 * 2)) * 1000), info.MoveSpeed);
    }

    [Fact]
    public void TheRangerShootSpeedUsesItsOwnFactor()
    {
        MonsterAnimInfo info = new(MonsterType.Ranger, BuildRecord(moveSpeed: 1000, shootSpeed: 1000));

        // 参考：bin_info.cpp:304-306，游侠的射击速度乘 0.78。
        Assert.Equal((uint)(1000 * 0.78), info.ShootSpeed);
    }

    [Fact]
    public void APeasantKeepsItsSpeeds()
    {
        MonsterAnimInfo info = new(MonsterType.Peasant, BuildRecord(moveSpeed: 1000, shootSpeed: 1000));

        Assert.Equal(1000u, info.MoveSpeed);
        Assert.Equal(1000u, info.ShootSpeed);
    }

    [Fact]
    public void TheGolemMovementSequenceIsRearranged()
    {
        byte[] record = BuildRecord();

        // 参考：bin_info.cpp:331-360 只在 MOVE_START 有 4 帧、MOVE_MAIN 有 7 帧时动手。
        for (int frame = 0; frame < 4; ++frame)
        {
            record[5 + frame] = (byte)(10 + frame);
        }

        record[243 + (int)MonsterAnimId.MoveStart] = 4;
        record[243 + (int)MonsterAnimId.MoveMain] = 7;
        for (int frame = 0; frame < 7; ++frame)
        {
            record[5 + (int)(2 * 16) + frame] = (byte)(20 + frame);
            record[277 + ((int)MonsterAnimId.MoveMain * 16) + frame] = (byte)(30 + frame);
        }

        MonsterAnimInfo info = new(MonsterType.IronGolem, record);

        // 原先 MOVE_MAIN 的前六帧被搬进 MOVE_START，MOVE_MAIN 只剩最后一帧。
        Assert.Equal(10, info.AnimationFrames[(int)MonsterAnimId.MoveStart].Length);
        Assert.Equal(new[] { 36 }, info.AnimationFrames[(int)MonsterAnimId.MoveMain]);

        // MOVE_TILE_START 换成新造的帧号 40…45，共六帧。
        Assert.Equal(6, info.AnimationFrames[(int)MonsterAnimId.MoveTileStart].Length);
        Assert.Equal(new[] { 40, 41, 42, 43, 44, 45 }, info.AnimationFrames[(int)MonsterAnimId.MoveTileStart]);
    }

    [Fact]
    public void TheRealArchivesHoldUsableRecords()
    {
        using AssetBundle? bundle = GameData.TryOpen();
        if (bundle is null)
        {
            return;
        }

        MonsterAnimInfoCache cache = new(bundle);

        foreach (MonsterType monster in (ReadOnlySpan<MonsterType>)
                 [
                     MonsterType.Peasant,
                     MonsterType.Archer,
                     MonsterType.Ranger,
                     MonsterType.IronGolem,
                     MonsterType.Swordsman,
                     MonsterType.Titan,
                     MonsterType.BoneDragon,
                 ])
        {
            MonsterAnimInfo info = cache.Get(monster);

            Assert.True(info.IsValid(), $"{monster} should have a usable animation record");
            Assert.Equal(MonsterAnimInfo.AnimCount, info.AnimationFrames.Length);
            Assert.Equal(3, info.ProjectileOffset.Length);
        }
    }

    [Fact]
    public void AnArchiveWithoutTheRecordYieldsAnEmptyOne()
    {
        using AssetBundle? bundle = GameData.TryOpen();
        if (bundle is null)
        {
            return;
        }

        MonsterAnimInfoCache cache = new(bundle);

        // 参考的 binFileName 里随机的怪物都是 UNKNOWN，归档里没有这个条目。
        MonsterAnimInfo info = cache.Get(MonsterType.RandomMonsterLevel4);

        Assert.False(info.IsValid());
        Assert.Empty(info.AnimationFrames);
        Assert.Equal(0, cache.Count);
    }

    /// <summary>A zeroed record with everything the reference needs to get past its first guard.</summary>
    private static byte[] BuildRecord(uint moveSpeed = 0, uint shootSpeed = 0)
    {
        byte[] record = new byte[MonsterAnimInfo.CorrectFrameLength];

        WriteUInt32(record, 162, moveSpeed);
        WriteUInt32(record, 166, shootSpeed);

        return record;
    }

    private static void WriteInt16(byte[] record, int offset, short value) =>
        global::System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(offset), value);

    private static void WriteInt32(byte[] record, int offset, int value) =>
        global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(offset), value);

    private static void WriteUInt32(byte[] record, int offset, uint value) =>
        global::System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(offset), value);

    private static void WriteSingle(byte[] record, int offset, float value) =>
        global::System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(record.AsSpan(offset), value);
}
