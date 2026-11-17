using System.Buffers.Binary;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>battle_animation.cpp</c>: the frame sequence, the animation set cut out of a creature's
/// <c>.BIN</c> record, and the state machine that switches between them.
/// </summary>
public class BattleAnimationTests
{
    [Fact]
    public void AnEmptySequenceStaysAtZero()
    {
        AnimationSequence sequence = new([]);

        Assert.False(sequence.IsValid);
        Assert.Equal(0, sequence.Frame);
        Assert.Equal(0, sequence.FirstFrame);
        Assert.Equal(0, sequence.AnimationLength);
        Assert.Equal(0, sequence.PlayAnimation(loop: true));
        Assert.Equal(0, sequence.RestartAnimation());
        Assert.Equal(0, sequence.MovementProgress);
    }

    [Fact]
    public void ASequenceStepsThroughItsFramesAndLoopsOnDemand()
    {
        AnimationSequence sequence = new([10, 20, 30]);

        Assert.True(sequence.IsValid);
        Assert.Equal(3, sequence.AnimationLength);
        Assert.Equal(10, sequence.Frame);
        Assert.Equal(10, sequence.FirstFrame);
        Assert.False(sequence.IsLastFrame);

        Assert.Equal(20, sequence.PlayAnimation());
        Assert.Equal(30, sequence.PlayAnimation());
        Assert.True(sequence.IsLastFrame);

        // 走到最后一帧就停在最后一帧，除非要求循环。
        Assert.Equal(30, sequence.PlayAnimation());
        Assert.Equal(10, sequence.PlayAnimation(loop: true));
        Assert.Equal(0, sequence.CurrentFrameId);
    }

    [Fact]
    public void TheMovementProgressSitsHalfwayBetweenFrames()
    {
        AnimationSequence sequence = new([1, 2, 3]);

        // 参考：首帧 0.5/3、次帧 1.5/3、末帧 2.5/3，让起步与收尾落在格心之间。
        Assert.Equal(0.5 / 3, sequence.MovementProgress);

        sequence.PlayAnimation();
        Assert.Equal(1.5 / 3, sequence.MovementProgress);

        sequence.PlayAnimation();
        Assert.Equal(2.5 / 3, sequence.MovementProgress);
    }

    [Fact]
    public void ReplacingTheSequenceRewindsIt()
    {
        AnimationSequence sequence = new([1, 2, 3]);

        sequence.PlayAnimation();
        sequence.PlayAnimation();

        sequence.SetSequence([7, 8]);

        Assert.Equal(0, sequence.CurrentFrameId);
        Assert.Equal(7, sequence.Frame);
        Assert.Equal(2, sequence.AnimationLength);
    }

    [Fact]
    public void ACreatureMarkerHasNoAnimationsAtAll()
    {
        // 参考在怪物 id 越界时直接返回，连静态图的兜底都不给。
        AnimationReference reference = new(MonsterType.RandomMonster, new MonsterAnimInfo());

        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.Static));
        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.Kill));
        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.MoveQuick));

        AnimationState state = new(MonsterType.RandomMonster, new MonsterAnimInfo());

        Assert.False(state.IsValid);
        Assert.False(state.SwitchAnimation(MonsterAnimationType.Static));
    }

    [Fact]
    public void ARecordWithoutAStaticPoseFallsBackToTheFirstFrame()
    {
        AnimationReference reference = new(MonsterType.Peasant, new MonsterAnimInfo());

        // 参考：`if ( !appendFrames( _static, STATIC ) ) _static.push_back( 1 );`
        Assert.Equal([1], reference.GetAnimationVector(MonsterAnimationType.Static));
        Assert.Equal([1], reference.GetAnimationVector(MonsterAnimationType.StandStill));
        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.Kill));
    }

    [Fact]
    public void TheFiveMovementSequencesAreCutFromTheRecord()
    {
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.MoveStart, 1, 2);
            SetAnimation(r, MonsterAnimId.MoveMain, 3);
            SetAnimation(r, MonsterAnimId.MoveStop, 4);
        });

        AnimationReference reference = new(MonsterType.Peasant, Record(record));

        // 参考：第一格 = 起步 + 中段 + 收尾，中间 = 中段单独一段，最后一格 = 中段 + 减速。
        Assert.Equal([1, 2, 3], reference.GetAnimationVector(MonsterAnimationType.MoveStart));
        Assert.Equal([3], reference.GetAnimationVector(MonsterAnimationType.Moving));
        Assert.Equal([3, 4], reference.GetAnimationVector(MonsterAnimationType.MoveEnd));

        // 没有 MOVE_ONE 的兵种用「起步 + 中段 + 减速」拼单格移动（参考举的例子是巫妖）。
        Assert.Equal([1, 2, 3, 4], reference.GetAnimationVector(MonsterAnimationType.MoveQuick));

        // 飞起只播起步，降落只播收尾。
        Assert.Equal([1, 2], reference.GetAnimationVector(MonsterAnimationType.FlyUp));
        Assert.Equal([4], reference.GetAnimationVector(MonsterAnimationType.FlyLand));
    }

    [Fact]
    public void ASingleTileMoveUsesTheDedicatedFramesWhenTheRecordHasThem()
    {
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.MoveStart, 1);
            SetAnimation(r, MonsterAnimId.MoveMain, 2);
            SetAnimation(r, MonsterAnimId.MoveOne, 9);
        });

        AnimationReference reference = new(MonsterType.Peasant, Record(record));

        Assert.Equal([9], reference.GetAnimationVector(MonsterAnimationType.MoveQuick));
    }

    [Fact]
    public void TheThreeMeleeDirectionsKeepTheirOwnFrames()
    {
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.Attack1, 10);
            SetAnimation(r, MonsterAnimId.Attack1End, 11);
            SetAnimation(r, MonsterAnimId.Attack2, 20);
            SetAnimation(r, MonsterAnimId.Attack3End, 31);
        });

        AnimationReference reference = new(MonsterType.Peasant, Record(record));

        Assert.Equal([10], reference.GetAnimationVector(MonsterAnimationType.MeleeTop));
        Assert.Equal([11], reference.GetAnimationVector(MonsterAnimationType.MeleeTopEnd));
        Assert.Equal([20], reference.GetAnimationVector(MonsterAnimationType.MeleeFront));
        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.MeleeFrontEnd));
        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.MeleeBot));
        Assert.Equal([31], reference.GetAnimationVector(MonsterAnimationType.MeleeBotEnd));
    }

    [Fact]
    public void ARangedCreatureUsesItsShootingFrames()
    {
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.Shoot1, 1);
            SetAnimation(r, MonsterAnimId.Shoot2, 2);
            SetAnimation(r, MonsterAnimId.Shoot3End, 6);
        });

        AnimationReference reference = new(MonsterType.Peasant, Record(record));

        Assert.Equal([1], reference.GetAnimationVector(MonsterAnimationType.RangTop));
        Assert.Equal([2], reference.GetAnimationVector(MonsterAnimationType.RangFront));
        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.RangBot));
        Assert.Equal([6], reference.GetAnimationVector(MonsterAnimationType.RangBotEnd));
    }

    [Fact]
    public void ACreatureWithoutShootingFramesBreathesInstead()
    {
        // 参考：没有 SHOOT2 但有 DOUBLEHEX2 的（原版里只有六只）把吐息当远程动画。
        // 判据是 DOUBLEHEX2 本身，因此三段吐息各自取自己的起止帧。
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.DoubleHex1, 3);
            SetAnimation(r, MonsterAnimId.DoubleHex2, 4);
            SetAnimation(r, MonsterAnimId.DoubleHex2End, 5);
        });

        AnimationReference reference = new(MonsterType.Peasant, Record(record));

        Assert.Equal([3], reference.GetAnimationVector(MonsterAnimationType.RangTop));
        Assert.Equal([4], reference.GetAnimationVector(MonsterAnimationType.RangFront));
        Assert.Equal([5], reference.GetAnimationVector(MonsterAnimationType.RangFrontEnd));
        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.RangBot));
    }

    [Fact]
    public void ACreatureWithoutDoubleHexFramesHasNoRangedAnimationAtAll()
    {
        // 既没有 SHOOT2 也没有 DOUBLEHEX2 时两段回退都不成立，远程方向全空。
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.DoubleHex1, 3);
            SetAnimation(r, MonsterAnimId.Shoot3End, 4);
        });

        AnimationReference reference = new(MonsterType.Peasant, Record(record));

        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.RangTop));
        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.RangFrontEnd));
        Assert.Empty(reference.GetAnimationVector(MonsterAnimationType.RangBotEnd));
    }

    [Fact]
    public void TheWinceIsAvailableWholeAndInHalves()
    {
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.WinceUp, 5);
            SetAnimation(r, MonsterAnimId.WinceEnd, 6);
            SetAnimation(r, MonsterAnimId.Death, 7);
        });

        AnimationReference reference = new(MonsterType.Peasant, Record(record));

        Assert.Equal([5], reference.GetAnimationVector(MonsterAnimationType.WnceUp));
        Assert.Equal([6], reference.GetAnimationVector(MonsterAnimationType.WnceDown));
        Assert.Equal([5, 6], reference.GetAnimationVector(MonsterAnimationType.Wnce));
        Assert.Equal([7], reference.GetAnimationVector(MonsterAnimationType.Kill));
    }

    [Fact]
    public void TheIdleAnimationWithZeroWeightIsNeverPlayed()
    {
        byte[] record = BuildRecord(r =>
        {
            r[117] = 2;
            WriteSingle(r, 118, 1.0f);
            WriteSingle(r, 122, 0.0f);
            SetAnimation(r, MonsterAnimId.Idle1, 7);
            SetAnimation(r, MonsterAnimId.Idle2, 8);
        });

        AnimationReference reference = new(MonsterType.Peasant, Record(record));

        // 权重 0 的那支永远掷不到，因此每次都是第一支。
        for (int attempt = 0; attempt < 16; ++attempt)
        {
            Assert.Equal([7], reference.GetAnimationVector(MonsterAnimationType.Idle));
        }
    }

    [Fact]
    public void TheStateMachineSwitchesAndRefusesMissingAnimations()
    {
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.Static, 1);
            SetAnimation(r, MonsterAnimId.Death, 2, 3);
        });

        AnimationState state = new(MonsterType.Peasant, Record(record));

        Assert.Equal(MonsterAnimationType.Static, state.CurrentState);
        Assert.True(state.IsValid);
        Assert.Equal(1, state.Frame);

        // 参考的 getAnimationVector 在 IDLE 取不到待机时回落到静态图，所以这并不算「缺失」。
        Assert.True(state.SwitchAnimation(MonsterAnimationType.Idle));
        Assert.Equal(MonsterAnimationType.Idle, state.CurrentState);
        Assert.Equal(1, state.Frame);

        // 真正缺失的动画（近战没有任何帧）被拒绝，状态保持不变。
        Assert.False(state.SwitchAnimation(MonsterAnimationType.MeleeTop));
        Assert.Equal(MonsterAnimationType.Idle, state.CurrentState);
        Assert.Equal(1, state.Frame);

        Assert.True(state.SwitchAnimation(MonsterAnimationType.Kill));
        Assert.Equal(MonsterAnimationType.Kill, state.CurrentState);
        Assert.Equal(2, state.Frame);
        Assert.Equal(2, state.AnimationLength);
        Assert.False(state.IsLastFrame);

        Assert.Equal(3, state.PlayAnimation());
        Assert.True(state.IsLastFrame);
        Assert.Equal(2, state.RestartAnimation());
    }

    [Fact]
    public void ReversingPlaysTheSequenceBackwards()
    {
        byte[] record = BuildRecord(r => SetAnimation(r, MonsterAnimId.Death, 2, 3, 4));

        AnimationState state = new(MonsterType.Peasant, Record(record));

        Assert.True(state.SwitchAnimation(MonsterAnimationType.Kill, reverse: true));

        Assert.Equal(4, state.Frame);
        Assert.Equal(3, state.PlayAnimation());
        Assert.Equal(2, state.PlayAnimation());
        Assert.True(state.IsLastFrame);
    }

    [Fact]
    public void GluingSeveralStatesKeepsTheLastOneThatExists()
    {
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.MoveStart, 1);
            SetAnimation(r, MonsterAnimId.MoveStop, 4);
        });

        AnimationState state = new(MonsterType.Peasant, Record(record));

        // FLY_UP 只有 [1]、FLY_LAND 只有 [4]；中间那个不存在的状态被跳过，状态记最后的 FLY_LAND。
        bool switched = state.SwitchAnimation(
            [MonsterAnimationType.FlyUp, MonsterAnimationType.Moving, MonsterAnimationType.FlyLand]);

        Assert.True(switched);
        Assert.Equal(MonsterAnimationType.FlyLand, state.CurrentState);
        Assert.Equal(2, state.AnimationLength);
        Assert.Equal(1, state.Frame);
        Assert.Equal(4, state.PlayAnimation());
    }

    [Fact]
    public void GluingStatesThatAllMissFails()
    {
        AnimationState state = new(MonsterType.Peasant, Record(BuildRecord()));

        Assert.False(state.SwitchAnimation([MonsterAnimationType.Kill, MonsterAnimationType.MeleeTop]));
    }

    [Fact]
    public void TheFrameOffsetFollowsTheSubsequenceTheCursorIsIn()
    {
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.MoveStart, 1, 2);
            SetAnimation(r, MonsterAnimId.MoveMain, 3);
            SetHorizontalOffset(r, MonsterAnimId.MoveStart, -7, 3);
            SetHorizontalOffset(r, MonsterAnimId.MoveMain, 5);
        });

        AnimationState state = new(MonsterType.Peasant, Record(record));

        Assert.True(state.SwitchAnimation(MonsterAnimationType.MoveStart));

        // 第一段（起步）两帧、第二段（中段）一帧，光标走到第三帧时读的是中段那一行。
        Assert.Equal(-7, state.GetCurrentFrameXOffset());

        state.PlayAnimation();
        Assert.Equal(3, state.GetCurrentFrameXOffset());

        state.PlayAnimation();
        Assert.Equal(5, state.GetCurrentFrameXOffset());
    }

    [Fact]
    public void TheStatesWithoutMovementCarryNoOffset()
    {
        byte[] record = BuildRecord(r =>
        {
            SetAnimation(r, MonsterAnimId.Static, 1);
            SetAnimation(r, MonsterAnimId.MoveStart, 1, 2);
            SetHorizontalOffset(r, MonsterAnimId.MoveStart, -7, 3);
        });

        AnimationState state = new(MonsterType.Peasant, Record(record));

        Assert.Equal(0, state.GetCurrentFrameXOffset());
        Assert.Equal([0], state.GetAnimationOffset(MonsterAnimationType.Static));

        // 参考在 bin_info.cpp:315-317 已把偏移行裁到对应动画的帧数，所以起步只给出它自己那两帧；
        // 中段与结尾没有帧，移动序列里的偏移便整体为空。
        Assert.Equal([-7, 3], state.GetAnimationOffset(MonsterAnimationType.MoveStart));
        Assert.Empty(state.GetAnimationOffset(MonsterAnimationType.Moving));
        Assert.Empty(state.GetAnimationOffset(MonsterAnimationType.MoveEnd));
    }

    [Fact]
    public void TheRecordSuppliesTheDrawnPoints()
    {
        byte[] record = BuildRecord(r =>
        {
            WriteInt16(r, 1, 12);
            WriteInt16(r, 3, -4);
            WriteInt16(r, 174, 6);
            WriteInt16(r, 176, 8);
            WriteInt16(r, 178, 10);
            WriteInt16(r, 180, 12);
            WriteInt32(r, 235, -11);
            WriteInt32(r, 239, 13);
            WriteUInt32(r, 158, 250);
            WriteUInt32(r, 162, 450);
            WriteUInt32(r, 166, 600);
            WriteUInt32(r, 170, 800);
        });

        AnimationReference reference = new(MonsterType.Peasant, Record(record));

        Assert.Equal(new Point(12, -4), reference.BlindOffset);
        Assert.Equal(new Point(6, 8), reference.GetProjectileOffset(0));
        Assert.Equal(new Point(10, 12), reference.GetProjectileOffset(1));
        Assert.Equal(default, reference.GetProjectileOffset(7));
        Assert.Equal(-11, reference.GetTroopCountOffset(isReflect: false));
        Assert.Equal(13, reference.GetTroopCountOffset(isReflect: true));
        Assert.Equal(250u, reference.IdleDelay);
        Assert.Equal(450u, reference.MoveSpeed);
        Assert.Equal(600u, reference.ShootSpeed);
        Assert.Equal(800u, reference.FlightSpeed);
    }

    [Fact]
    public void ARandomizedDelayFiresOnceThenPicksANewValue()
    {
        long now = 0;
        RandomizedDelay delay = new(100, () => now);

        // 参考：延迟是 0.75～1.25 倍，第一次问的时候才定下来。
        Assert.False(delay.CheckDelay());

        ulong chosen = delay.Delay;

        Assert.InRange(chosen, 75ul, 125ul);

        now = (long)chosen;
        Assert.True(delay.CheckDelay());

        // 过了以后再问一次：说明它重置过，并且会重新掷一个值。
        Assert.False(delay.CheckDelay());
        Assert.InRange(delay.Delay, 75ul, 125ul);
    }

    [Fact]
    public void AZeroDelayFiresAtOnce()
    {
        long now = 0;
        RandomizedDelay delay = new(0, () => now);

        Assert.True(delay.CheckDelay());
        Assert.Equal(0ul, delay.Delay);
    }

    /// <summary>A zeroed record; <c>MonsterAnimInfo</c> only reads one that is exactly the right length.</summary>
    private static byte[] BuildRecord(Action<byte[]>? edit = null)
    {
        byte[] record = new byte[MonsterAnimInfo.CorrectFrameLength];
        edit?.Invoke(record);

        return record;
    }

    /// <summary>The peasant is the creature the reference applies no per-creature corrections to.</summary>
    private static MonsterAnimInfo Record(byte[] record) => new(MonsterType.Peasant, record);

    private static void SetAnimation(byte[] record, MonsterAnimId animId, params int[] frames)
    {
        record[243 + (int)animId] = (byte)frames.Length;

        for (int frame = 0; frame < frames.Length; ++frame)
        {
            record[277 + ((int)animId * 16) + frame] = (byte)frames[frame];
        }
    }

    private static void SetHorizontalOffset(byte[] record, MonsterAnimId animId, params int[] offsets)
    {
        for (int frame = 0; frame < offsets.Length; ++frame)
        {
            record[5 + ((int)animId * 16) + frame] = (byte)(sbyte)offsets[frame];
        }
    }

    private static void WriteInt16(byte[] record, int offset, short value) =>
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(offset), value);

    private static void WriteInt32(byte[] record, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(offset), value);

    private static void WriteUInt32(byte[] record, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(offset), value);

    private static void WriteSingle(byte[] record, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(record.AsSpan(offset), value);
}
