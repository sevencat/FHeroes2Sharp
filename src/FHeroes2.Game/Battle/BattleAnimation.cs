using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Core.Timing;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Battle;

/// <summary>
/// A timer whose delay is randomised to 75–125% of the intended value, the reference's
/// <c>RandomizedDelay</c> (<c>battle_animation.h:32</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>battle_animation.cpp:31-46</c>——第一次问的时候才设一个随机延迟（
/// <c>Rand::Get( 0, halfDelay ) + halfDelay * 3 / 2</c>，即 0.75～1.25 倍），过了就重置并标记「下次重新掷」。
/// </para>
/// <para>
/// 差异：参考是 <c>protected</c> 继承 <c>fheroes2::TimeDelay</c>；C# 没有私有继承，端口改成持有
/// （<c>_timer</c>），对外只留 <see cref="CheckDelay"/>/<see cref="Delay"/>，与参考的可见性一致。
/// </para>
/// </remarks>
public sealed class RandomizedDelay
{
    private readonly TimeDelay _timer;
    private readonly uint _halfDelay;
    private bool _timerIsSet;

    /// <summary>Creates the timer; the delay is only fixed on the first <see cref="CheckDelay"/>.</summary>
    /// <param name="delay">Intended delay in milliseconds.</param>
    /// <param name="clockMs">Monotonic clock in milliseconds, for tests; <see langword="null"/> for the real one.</param>
    public RandomizedDelay(uint delay, Func<long>? clockMs = null)
    {
        _timer = new TimeDelay(delay, clockMs);
        _halfDelay = delay / 2;
    }

    /// <summary>The delay currently in force, in milliseconds.</summary>
    public ulong Delay
    {
        get => _timer.Delay;
        set => _timer.Delay = value;
    }

    /// <summary>
    /// Whether the current delay has passed; when it has, the timer is reset and the next delay will be
    /// randomised again.
    /// </summary>
    /// <returns><see langword="true"/> exactly once per elapsed delay.</returns>
    public bool CheckDelay()
    {
        if (!_timerIsSet)
        {
            Delay = (ulong)Rand.Get(0u, _halfDelay) + _halfDelay * 3ul / 2;
            _timerIsSet = true;
        }

        bool passed = _timer.IsPassed();
        if (passed)
        {
            _timer.Reset();
            _timerIsSet = false;
        }

        return passed;
    }
}

/// <summary>
/// The two halves of a flying sequence, the reference's <c>MonsterReturnAnim</c>
/// (<c>battle_animation.h:50</c>).
/// </summary>
public sealed class MonsterReturnAnim
{
    /// <summary>Frames played when the creature takes off.</summary>
    /// <remarks>参考里是公开的 <c>std::vector</c> 成员，端口用字段而不是属性，好让 <c>ref</c> 直接拼帧。</remarks>
    public int[] Start = [];

    /// <summary>Frames played when the creature lands.</summary>
    public int[] End = [];
}

/// <summary>
/// A list of sprite frames with a cursor in it, the reference's <c>AnimationSequence</c>
/// (<c>battle_animation.h:56</c>).
/// </summary>
/// <remarks>参考：<c>battle_animation.cpp:48-91</c>。空序列的每一问都答 0 / 空，与参考的 <c>isValid()</c> 守卫相同。</remarks>
public sealed class AnimationSequence
{
    private int[] _sequence;
    private int _currentFrame;

    /// <summary>Creates the sequence and puts the cursor on the first frame.</summary>
    public AnimationSequence(int[] sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        _sequence = sequence;
    }

    /// <summary>How many frames the sequence has.</summary>
    public int AnimationLength => _sequence.Length;

    /// <summary>Whether the sequence holds any frame at all.</summary>
    public bool IsValid => _sequence.Length != 0;

    /// <summary>Index of the current frame within the sequence.</summary>
    public int CurrentFrameId => _currentFrame;

    /// <summary>The sprite index of the current frame, or 0 for an empty sequence.</summary>
    public int Frame => IsValid ? _sequence[_currentFrame] : 0;

    /// <summary>The sprite index of the first frame, or 0 for an empty sequence.</summary>
    public int FirstFrame => IsValid ? _sequence[0] : 0;

    /// <summary>Whether the cursor stands on the last frame.</summary>
    public bool IsLastFrame => _currentFrame == _sequence.Length - 1;

    /// <summary>
    /// How far along its way to the next cell the creature is, 0.5/length for the first frame and
    /// (length-0.5)/length for the last one.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_animation.cpp:80-91</c>——加 0.5 是为了让起步与收尾都落在两格中间，而不是压在格心上。
    /// </remarks>
    public double MovementProgress =>
        IsValid ? (_currentFrame + 0.5) / _sequence.Length : 0;

    /// <summary>Replaces the frames and rewinds the cursor, the reference's <c>operator=</c>.</summary>
    public void SetSequence(int[] sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        _sequence = sequence;
        _currentFrame = 0;
    }

    /// <summary>Advances one frame (or rewinds when looping) and returns the new frame.</summary>
    /// <param name="loop">Whether to start over after the last frame.</param>
    public int PlayAnimation(bool loop = false)
    {
        if (!IsValid)
        {
            return 0;
        }

        if (IsLastFrame)
        {
            if (loop)
            {
                RestartAnimation();
            }
        }
        else
        {
            ++_currentFrame;
        }

        return _sequence[_currentFrame];
    }

    /// <summary>Puts the cursor back on the first frame and returns it.</summary>
    public int RestartAnimation()
    {
        _currentFrame = 0;
        return Frame;
    }
}

/// <summary>
/// Every animation sequence a creature owns, cut out of its <c>.BIN</c> record, the reference's
/// <c>AnimationReference</c> (<c>battle_animation.h:112</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>battle_animation.cpp:93-370</c>——把 <c>.BIN</c> 的 33 支动画拼成战斗要用的十几条序列：
/// 站立、挨打、死亡、若干待机、第一格/中间格/最后一格/单格移动、飞起与落下、三个方向的近战与远程。
/// </para>
/// <para>
/// 差异：参考在构造函数里从全局 <c>Bin_Info::GetMonsterInfo</c> 取记录；端口要求调用方给出
/// <see cref="MonsterAnimInfoCache"/>（它需要 <c>AssetBundle</c>），这样 <c>Battle</c> 不必知道归档在哪，
/// 测试也能喂一份自造数据。取不到时与参考一样退化成一条 <c>[1]</c> 的静态序列，而不是崩溃。
/// </para>
/// </remarks>
public class AnimationReference
{
    private readonly MonsterType _monsterId;
    private readonly MonsterAnimInfo _monsterInfo;

    private int[] _static = [];
    private int[] _winceUp = [];
    private int[] _winceDown = [];
    private int[] _wince = [];
    private int[] _death = [];
    private int[] _moveFirstTile = [];
    private int[] _moving = [];
    private int[] _moveLastTile = [];
    private int[] _moveOneTile = [];
    private readonly MonsterReturnAnim _flying = new();
    private readonly MonsterReturnAnim[] _melee = [new(), new(), new()];
    private readonly MonsterReturnAnim[] _ranged = [new(), new(), new()];
    private readonly List<int[]> _idle = [];
    private int[][] _offsetX = [];

    /// <summary>Builds every sequence of <paramref name="monsterId"/> from its animation record.</summary>
    /// <param name="monsterId">Creature whose sequences are wanted.</param>
    /// <param name="monsterInfoCache">Where the <c>.BIN</c> records come from.</param>
    public AnimationReference(MonsterType monsterId, MonsterAnimInfoCache monsterInfoCache)
        : this(monsterId, ResolveMonsterInfo(monsterId, monsterInfoCache))
    {
    }

    /// <summary>Builds every sequence of <paramref name="monsterId"/> from an already read record.</summary>
    /// <param name="monsterId">Creature whose sequences are wanted.</param>
    /// <param name="monsterInfo">Its <c>.BIN</c> record; an empty one yields the degraded sequences.</param>
    public AnimationReference(MonsterType monsterId, MonsterAnimInfo monsterInfo)
    {
        ArgumentNullException.ThrowIfNull(monsterInfo);

        _monsterId = monsterId;
        _monsterInfo = monsterInfo;

        // 参考：不是真怪物（marker、UNKNOWN）时直接收工，所有序列都是空的。
        if (monsterId < MonsterType.Peasant || monsterId > MonsterType.WaterElement)
        {
            return;
        }

        // 参考：没有 STATIC 时塞一个第 1 号图，免得整条序列为空、渲染时崩。
        if (!AppendFrames(ref _static, MonsterAnimId.Static))
        {
            _static = [1];
        }

        AppendFrames(ref _wince, MonsterAnimId.WinceUp);
        AppendFrames(ref _wince, MonsterAnimId.WinceEnd);
        AppendFrames(ref _winceUp, MonsterAnimId.WinceUp);
        AppendFrames(ref _winceDown, MonsterAnimId.WinceEnd);
        AppendFrames(ref _death, MonsterAnimId.Death);

        // 参考：最多五个待机循环，逐个拼。
        for (uint index = 0; index < _monsterInfo.IdleAnimationCount; ++index)
        {
            int[] idle = [];
            MonsterAnimId idleId = (MonsterAnimId)((int)MonsterAnimId.Idle1 + (int)index);
            if (AppendFrames(ref idle, idleId))
            {
                _idle.Add(idle);
            }
        }
        _offsetX = _monsterInfo.FrameXOffset;

        // 参考：每个单位都有 MOVE_MAIN，以它为中段。
        AppendFrames(ref _moving, MonsterAnimId.MoveTileStart);
        AppendFrames(ref _moving, MonsterAnimId.MoveMain);
        AppendFrames(ref _moving, MonsterAnimId.MoveTileEnd);

        if (!AppendFrames(ref _moveOneTile, MonsterAnimId.MoveOne))
        {
            // 参考：巫妖没有单格移动动画，用「起步 + 中段 + 收尾」拼。
            AppendFrames(ref _moveOneTile, MonsterAnimId.MoveStart);
            AppendFrames(ref _moveOneTile, MonsterAnimId.MoveMain);
            AppendFrames(ref _moveOneTile, MonsterAnimId.MoveStop);
        }

        AppendFrames(ref _moveFirstTile, MonsterAnimId.MoveStart);
        AppendFrames(ref _moveFirstTile, MonsterAnimId.MoveMain);
        AppendFrames(ref _moveFirstTile, MonsterAnimId.MoveTileEnd);

        AppendFrames(ref _moveLastTile, MonsterAnimId.MoveTileStart);
        AppendFrames(ref _moveLastTile, MonsterAnimId.MoveMain);
        AppendFrames(ref _moveLastTile, MonsterAnimId.MoveStop);

        AppendFrames(ref _flying.Start, MonsterAnimId.MoveStart);
        AppendFrames(ref _flying.End, MonsterAnimId.MoveStop);

        AppendFrames(ref _melee[(int)MonsterAttackDirection.Top].Start, MonsterAnimId.Attack1);
        AppendFrames(ref _melee[(int)MonsterAttackDirection.Top].End, MonsterAnimId.Attack1End);
        AppendFrames(ref _melee[(int)MonsterAttackDirection.Front].Start, MonsterAnimId.Attack2);
        AppendFrames(ref _melee[(int)MonsterAttackDirection.Front].End, MonsterAnimId.Attack2End);
        AppendFrames(ref _melee[(int)MonsterAttackDirection.Bottom].Start, MonsterAnimId.Attack3);
        AppendFrames(ref _melee[(int)MonsterAttackDirection.Bottom].End, MonsterAnimId.Attack3End);

        // 参考：有 SHOOT2 的用三段射击，否则有 DOUBLEHEX2 的（原版里只有六只）用吐息当远程。
        if (_monsterInfo.HasAnim(MonsterAnimId.Shoot2))
        {
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Top].Start, MonsterAnimId.Shoot1);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Top].End, MonsterAnimId.Shoot1End);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Front].Start, MonsterAnimId.Shoot2);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Front].End, MonsterAnimId.Shoot2End);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Bottom].Start, MonsterAnimId.Shoot3);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Bottom].End, MonsterAnimId.Shoot3End);
        }
        else if (_monsterInfo.HasAnim(MonsterAnimId.DoubleHex2))
        {
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Top].Start, MonsterAnimId.DoubleHex1);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Top].End, MonsterAnimId.DoubleHex1End);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Front].Start, MonsterAnimId.DoubleHex2);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Front].End, MonsterAnimId.DoubleHex2End);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Bottom].Start, MonsterAnimId.DoubleHex3);
            AppendFrames(ref _ranged[(int)MonsterAttackDirection.Bottom].End, MonsterAnimId.DoubleHex3End);
        }
    }

    /// <summary>The creature these sequences belong to.</summary>
    public MonsterType MonsterId => _monsterId;

    /// <summary>Reads the record of <paramref name="monsterId"/>, or an empty one for anything that is not a creature.</summary>
    protected static MonsterAnimInfo ResolveMonsterInfo(MonsterType monsterId, MonsterAnimInfoCache monsterInfoCache)
    {
        ArgumentNullException.ThrowIfNull(monsterInfoCache);

        return monsterId >= MonsterType.Peasant && monsterId <= MonsterType.WaterElement
            ? monsterInfoCache.Get(monsterId)
            : new MonsterAnimInfo();
    }

    /// <summary>How long one step of a move takes, from the <c>.BIN</c> record.</summary>
    public uint MoveSpeed => _monsterInfo.MoveSpeed;

    /// <summary>How long one step of a flight takes.</summary>
    public uint FlightSpeed => _monsterInfo.FlightSpeed;

    /// <summary>How long a shot takes.</summary>
    public uint ShootSpeed => _monsterInfo.ShootSpeed;

    /// <summary>The delay between two idle cycles.</summary>
    public uint IdleDelay => _monsterInfo.IdleAnimationDelay;

    /// <summary>Where the "Blind" overlay is drawn, in sprite coordinates.</summary>
    public Point BlindOffset => _monsterInfo.EyePosition;

    /// <summary>The horizontal offset of the creature count, which depends on the sprite's facing.</summary>
    public int GetTroopCountOffset(bool isReflect) =>
        isReflect ? _monsterInfo.TroopCountOffsetRight : _monsterInfo.TroopCountOffsetLeft;

    /// <summary>The offset a projectile starts from, for the given shooting direction.</summary>
    public Point GetProjectileOffset(int direction) =>
        direction >= 0 && direction < _monsterInfo.ProjectileOffset.Length
            ? _monsterInfo.ProjectileOffset[direction]
            : default;

    /// <summary>The frames of one animation state, empty when the creature has none.</summary>
    /// <remarks>参考：<c>battle_animation.cpp:202-269</c>；待机态在这里按权重掷一支。</remarks>
    public int[] GetAnimationVector(MonsterAnimationType animState) => animState switch
    {
        MonsterAnimationType.StandStill or MonsterAnimationType.Static => _static,
        MonsterAnimationType.Idle => PickIdleAnimation(),
        MonsterAnimationType.MoveStart => _moveFirstTile,
        MonsterAnimationType.Moving => _moving,
        MonsterAnimationType.MoveEnd => _moveLastTile,
        MonsterAnimationType.MoveQuick => _moveOneTile,
        MonsterAnimationType.FlyUp => _flying.Start,
        MonsterAnimationType.FlyLand => _flying.End,
        MonsterAnimationType.MeleeTop => _melee[(int)MonsterAttackDirection.Top].Start,
        MonsterAnimationType.MeleeTopEnd => _melee[(int)MonsterAttackDirection.Top].End,
        MonsterAnimationType.MeleeFront => _melee[(int)MonsterAttackDirection.Front].Start,
        MonsterAnimationType.MeleeFrontEnd => _melee[(int)MonsterAttackDirection.Front].End,
        MonsterAnimationType.MeleeBot => _melee[(int)MonsterAttackDirection.Bottom].Start,
        MonsterAnimationType.MeleeBotEnd => _melee[(int)MonsterAttackDirection.Bottom].End,
        MonsterAnimationType.RangTop => _ranged[(int)MonsterAttackDirection.Top].Start,
        MonsterAnimationType.RangTopEnd => _ranged[(int)MonsterAttackDirection.Top].End,
        MonsterAnimationType.RangFront => _ranged[(int)MonsterAttackDirection.Front].Start,
        MonsterAnimationType.RangFrontEnd => _ranged[(int)MonsterAttackDirection.Front].End,
        MonsterAnimationType.RangBot => _ranged[(int)MonsterAttackDirection.Bottom].Start,
        MonsterAnimationType.RangBotEnd => _ranged[(int)MonsterAttackDirection.Bottom].End,
        MonsterAnimationType.WnceUp => _winceUp,
        MonsterAnimationType.WnceDown => _winceDown,
        MonsterAnimationType.Wnce => _wince,
        MonsterAnimationType.Kill => _death,

        // 参考的 default：连 NONE/STAND_STILL 之外的东西都回落到静态图。
        _ => _static,
    };

    /// <summary>
    /// The per-frame horizontal offsets of one animation state, zero-filled for the states the record does
    /// not describe.
    /// </summary>
    /// <remarks>
    /// 参考：<c>battle_animation.cpp:271-361</c>。只有四条移动序列有横向偏移（来自 <c>.BIN</c> 的前六行），
    /// 其余一律补 0。
    /// </remarks>
    public int[] GetAnimationOffset(MonsterAnimationType animState)
    {
        switch (animState)
        {
            case MonsterAnimationType.MoveStart:
                return [.. Row(MonsterAnimId.MoveStart), .. Row(MonsterAnimId.MoveMain), .. Row(MonsterAnimId.MoveTileEnd)];
            case MonsterAnimationType.Moving:
                return [.. Row(MonsterAnimId.MoveTileStart), .. Row(MonsterAnimId.MoveMain), .. Row(MonsterAnimId.MoveTileEnd)];
            case MonsterAnimationType.MoveEnd:
                return [.. Row(MonsterAnimId.MoveTileStart), .. Row(MonsterAnimId.MoveMain), .. Row(MonsterAnimId.MoveStop)];
            case MonsterAnimationType.MoveQuick:
                return [.. Row(MonsterAnimId.MoveStart), .. Row(MonsterAnimId.MoveMain), .. Row(MonsterAnimId.MoveStop)];
            default:
                return new int[GetAnimationVector(animState).Length];
        }
    }

    /// <summary>Appends the frames of <paramref name="animId"/> to <paramref name="target"/>.</summary>
    /// <returns><see langword="false"/> when the record has no such animation.</returns>
    protected bool AppendFrames(ref int[] target, MonsterAnimId animId)
    {
        if (!_monsterInfo.HasAnim(animId))
        {
            return false;
        }

        target = [.. target, .. _monsterInfo.AnimationFrames[(int)animId]];
        return true;
    }

    /// <summary>One row of the record's horizontal offsets; empty when the row does not exist.</summary>
    protected int[] Row(MonsterAnimId animId) =>
        (int)animId < _offsetX.Length ? _offsetX[(int)animId] : [];

    /// <summary>The idle cycle to play, weighted by the record's priorities.</summary>
    private int[] PickIdleAnimation()
    {
        if (_idle.Count == 0 || _idle.Count != _monsterInfo.IdlePriority.Length)
        {
            return _static;
        }

        Rand.Queue picker = new();

        for (int index = 0; index < _idle.Count; ++index)
        {
            picker.Push(index, (uint)(_monsterInfo.IdlePriority[index] * 100));
        }

        return _idle[picker.Get()];
    }
}

/// <summary>
/// The animation currently playing on a unit, the reference's <c>AnimationState</c>
/// (<c>battle_animation.h:182</c>).
/// </summary>
/// <remarks>参考：<c>battle_animation.cpp:372-475</c>。</remarks>
public sealed class AnimationState : AnimationReference
{
    private MonsterAnimationType _animState;

    /// <summary>Builds the state machine and starts it on the static pose.</summary>
    /// <param name="monsterId">Creature whose animations are played.</param>
    /// <param name="monsterInfoCache">Where the <c>.BIN</c> records come from.</param>
    public AnimationState(MonsterType monsterId, MonsterAnimInfoCache monsterInfoCache)
        : this(monsterId, ResolveMonsterInfo(monsterId, monsterInfoCache))
    {
    }

    /// <summary>Builds the state machine from an already read record and starts it on the static pose.</summary>
    /// <param name="monsterId">Creature whose animations are played.</param>
    /// <param name="monsterInfo">Its <c>.BIN</c> record.</param>
    public AnimationState(MonsterType monsterId, MonsterAnimInfo monsterInfo)
        : base(monsterId, monsterInfo)
    {
        _animState = MonsterAnimationType.Static;
        _currentSequence = new AnimationSequence(GetAnimationVector(MonsterAnimationType.Static));
    }

    private AnimationSequence _currentSequence;

    /// <summary>The state the machine is in.</summary>
    public MonsterAnimationType CurrentState => _animState;

    /// <summary>How many frames the current sequence has.</summary>
    public int AnimationLength => _currentSequence.AnimationLength;

    /// <summary>Whether the current sequence has any frame.</summary>
    public bool IsValid => _currentSequence.IsValid;

    /// <summary>Whether the cursor stands on the last frame of the current sequence.</summary>
    public bool IsLastFrame => _currentSequence.IsLastFrame;

    /// <summary>The sprite index of the current frame.</summary>
    public int Frame => _currentSequence.Frame;

    /// <summary>The sprite index of the first frame of the current sequence.</summary>
    public int FirstFrame => _currentSequence.FirstFrame;

    /// <summary>How far along its way the creature is, see <see cref="AnimationSequence.MovementProgress"/>.</summary>
    public double MovementProgress => _currentSequence.MovementProgress;

    /// <summary>Advances the current sequence by one frame.</summary>
    public int PlayAnimation(bool loop = false) => _currentSequence.PlayAnimation(loop);

    /// <summary>Rewinds the current sequence.</summary>
    public int RestartAnimation() => _currentSequence.RestartAnimation();

    /// <summary>Switches to <paramref name="animState"/>.</summary>
    /// <param name="animState">The state to play.</param>
    /// <param name="reverse">Whether to play it backwards.</param>
    /// <returns><see langword="false"/> when the creature has no frames for that state.</returns>
    public bool SwitchAnimation(MonsterAnimationType animState, bool reverse = false)
    {
        int[] sequence = [.. GetAnimationVector(animState)];
        if (sequence.Length == 0)
        {
            return false;
        }

        _animState = animState;

        if (reverse)
        {
            Array.Reverse(sequence);
        }

        _currentSequence.SetSequence(sequence);
        _currentSequence.RestartAnimation();

        return true;
    }

    /// <summary>Plays several states as one sequence, skipping the ones the creature does not have.</summary>
    /// <param name="animationList">States to glue together, in order.</param>
    /// <param name="reverse">Whether to play the glued sequence backwards.</param>
    /// <returns><see langword="false"/> when none of the states has frames.</returns>
    /// <remarks>参考：<c>battle_animation.cpp:401-427</c>——状态取最后一个非空的，序列按顺序拼接。</remarks>
    public bool SwitchAnimation(ReadOnlySpan<MonsterAnimationType> animationList, bool reverse = false)
    {
        List<int> combined = [];

        foreach (MonsterAnimationType animState in animationList)
        {
            int[] sequence = GetAnimationVector(animState);
            if (sequence.Length != 0)
            {
                _animState = animState;
                combined.AddRange(sequence);
            }
        }

        if (combined.Count == 0)
        {
            return false;
        }

        if (reverse)
        {
            combined.Reverse();
        }

        _currentSequence.SetSequence([.. combined]);
        _currentSequence.RestartAnimation();

        return true;
    }

    /// <summary>The horizontal offset of the current frame, 0 for the states that carry none.</summary>
    /// <remarks>参考：<c>battle_animation.cpp:429-475</c>。</remarks>
    public int GetCurrentFrameXOffset()
    {
        MonsterAnimId[] subsequences = _animState switch
        {
            MonsterAnimationType.MoveStart => [MonsterAnimId.MoveStart, MonsterAnimId.MoveMain, MonsterAnimId.MoveTileEnd],
            MonsterAnimationType.Moving => [MonsterAnimId.MoveTileStart, MonsterAnimId.MoveMain, MonsterAnimId.MoveTileEnd],
            MonsterAnimationType.MoveEnd => [MonsterAnimId.MoveTileStart, MonsterAnimId.MoveMain, MonsterAnimId.MoveStop],
            MonsterAnimationType.MoveQuick => [MonsterAnimId.MoveStart, MonsterAnimId.MoveMain, MonsterAnimId.MoveStop],
            _ => [],
        };

        if (subsequences.Length == 0)
        {
            return 0;
        }

        int subsequenceStart = 0;
        int currentFrame = _currentSequence.CurrentFrameId;

        foreach (MonsterAnimId subsequence in subsequences)
        {
            int[] offsets = Row(subsequence);
            int subsequenceEnd = offsets.Length + subsequenceStart;

            if (currentFrame < subsequenceEnd)
            {
                return offsets[currentFrame - subsequenceStart];
            }

            subsequenceStart = subsequenceEnd;
        }

        return 0;
    }
}
