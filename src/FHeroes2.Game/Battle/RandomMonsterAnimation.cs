using FHeroes2.Assets.Icn;
using FHeroes2.Core.Random;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The endless loop of random animations a creature plays while nothing happens,
/// <c>RandomMonsterAnimation</c> (<c>monster_anim.cpp:41-189</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考给怪物信息屏、城镇里的住宅窗口和战场待机共用：把几支动画压成一个「帧队列」，每次
/// <see cref="Increment"/> 取一帧，队列见底再按权重掷下一段。偏移和帧一起排队，因为移动与飞行要横向平移。
/// </para>
/// <para>
/// 构造里 <see cref="MonsterAnimationType.Static"/> 与 <see cref="MonsterAnimationType.Moving"/> 各入表两次，是
/// **故意的**：参考靠重复入表给这两段加倍权重（<c>monster_anim.cpp:48-58</c>），不是笔误。
/// </para>
/// <para>
/// 参考的文件在 <c>monster_anim.cpp</c>，端口落在 Battle 层：它整个依赖 <see cref="AnimationReference"/>，
/// 而那个类是 <c>battle_animation.cpp</c> 的移植，也在本层。
/// </para>
/// </remarks>
public sealed class RandomMonsterAnimation
{
    private readonly AnimationReference _reference;
    private readonly IcnType _icn;
    private readonly bool _isFlyer;
    private readonly List<MonsterAnimationType> _validMoves = [];
    private readonly Queue<int> _frameSet = new();
    private readonly Queue<int> _offsetSet = new();
    private int _frameId;
    private int _frameOffset;

    /// <summary>
    /// Builds the sequence of a creature, <c>RandomMonsterAnimation( monster, includeDeathAnimation )</c>
    /// (<c>monster_anim.cpp:41-66</c>).
    /// </summary>
    /// <param name="monsterId">The creature to animate.</param>
    /// <param name="monsterInfoCache">The <c>.BIN</c> records to read the animations from.</param>
    /// <param name="includeDeathAnimation">
    /// Whether the wince and death tracks take part. False for the places that must not show a corpse.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="monsterInfoCache"/> is null.</exception>
    public RandomMonsterAnimation(MonsterType monsterId, MonsterAnimInfoCache monsterInfoCache, bool includeDeathAnimation)
    {
        ArgumentNullException.ThrowIfNull(monsterInfoCache);

        _reference = new AnimationReference(monsterId, monsterInfoCache);
        _icn = MonsterIcnIds.GetSpriteIcn(monsterId);
        _isFlyer = MonsterAbilities.IsFlying(monsterId);

        AddValidMove(MonsterAnimationType.Static);
        AddValidMove(MonsterAnimationType.Static);
        AddValidMove(MonsterAnimationType.Idle);
        AddValidMove(MonsterAnimationType.MeleeTop);
        AddValidMove(MonsterAnimationType.MeleeFront);
        AddValidMove(MonsterAnimationType.MeleeBot);
        AddValidMove(MonsterAnimationType.RangTop);
        AddValidMove(MonsterAnimationType.RangFront);
        AddValidMove(MonsterAnimationType.RangBot);
        AddValidMove(MonsterAnimationType.Moving);
        AddValidMove(MonsterAnimationType.Moving);

        if (includeDeathAnimation)
        {
            // 被打一下和解体的两段：只给「允许露尸体」的场合。
            AddValidMove(MonsterAnimationType.Wnce);
            AddValidMove(MonsterAnimationType.Kill);
        }

        Increment();
    }

    /// <summary>The sprite file of the creature, <c>icnFile()</c> (<c>monster_anim.cpp:130-133</c>).</summary>
    public IcnType Icn => _icn;

    /// <summary>The frame to draw now, <c>frameId()</c> (<c>monster_anim.cpp:135-138</c>).</summary>
    public int FrameId => _frameId;

    /// <summary>The horizontal offset of the current frame, <c>offset()</c> (<c>monster_anim.cpp:140-143</c>).</summary>
    public int Offset => _frameOffset;

    /// <summary>
    /// Advances one frame, refilling the queue when it runs out, <c>increment()</c>
    /// (<c>monster_anim.cpp:68-128</c>).
    /// </summary>
    public void Increment()
    {
        if (_frameSet.Count == 0)
        {
            // 参考先清偏移队列：帧与偏移必须同长，清掉旧的一半才不会错位。
            _offsetSet.Clear();

            MonsterAnimationType moveId = _validMoves.Count > 0 ? Rand.Get(_validMoves) : MonsterAnimationType.Static;

            switch (moveId)
            {
                case MonsterAnimationType.Static:
                {
                    int counter = Rand.Get(10, 20);

                    for (int i = 0; i < counter; ++i)
                    {
                        PushFrames(MonsterAnimationType.Static);
                    }

                    break;
                }

                case MonsterAnimationType.Idle:
                    PushFrames(MonsterAnimationType.Idle);
                    break;

                case MonsterAnimationType.Moving:
                {
                    PushFrames(_isFlyer ? MonsterAnimationType.FlyUp : MonsterAnimationType.MoveStart);

                    int counter = Rand.Get(3, 5);

                    for (int i = 0; i < counter; ++i)
                    {
                        PushFrames(MonsterAnimationType.Moving);
                    }

                    PushFrames(_isFlyer ? MonsterAnimationType.FlyLand : MonsterAnimationType.MoveEnd);

                    break;
                }

                case MonsterAnimationType.MeleeTop:
                    PushFrames(MonsterAnimationType.MeleeTop);
                    PushFrames(MonsterAnimationType.MeleeTopEnd);
                    break;

                case MonsterAnimationType.MeleeFront:
                    PushFrames(MonsterAnimationType.MeleeFront);
                    PushFrames(MonsterAnimationType.MeleeFrontEnd);
                    break;

                case MonsterAnimationType.MeleeBot:
                    PushFrames(MonsterAnimationType.MeleeBot);
                    PushFrames(MonsterAnimationType.MeleeBotEnd);
                    break;

                case MonsterAnimationType.RangTop:
                    PushFrames(MonsterAnimationType.RangTop);
                    PushFrames(MonsterAnimationType.RangTopEnd);
                    break;

                case MonsterAnimationType.RangFront:
                    PushFrames(MonsterAnimationType.RangFront);
                    PushFrames(MonsterAnimationType.RangFrontEnd);
                    break;

                case MonsterAnimationType.RangBot:
                    PushFrames(MonsterAnimationType.RangBot);
                    PushFrames(MonsterAnimationType.RangBotEnd);
                    break;

                case MonsterAnimationType.Wnce:
                    PushFrames(MonsterAnimationType.Wnce);
                    break;

                case MonsterAnimationType.Kill:
                    PushFrames(MonsterAnimationType.Kill);
                    break;

                default:
                    break;
            }

            PushFrames(MonsterAnimationType.Static);
        }

        UpdateFrameInfo();
    }

    /// <summary>Drops the queue and starts over from the static pose, <c>reset()</c> (<c>monster_anim.cpp:145-152</c>).</summary>
    public void Reset()
    {
        _frameSet.Clear();
        _offsetSet.Clear();

        PushFrames(MonsterAnimationType.Static);
        UpdateFrameInfo();
    }

    /// <summary>Queues the frames and offsets of one track, <c>_pushFrames()</c> (<c>monster_anim.cpp:154-169</c>).</summary>
    private void PushFrames(MonsterAnimationType type)
    {
        int[] sequence = _reference.GetAnimationVector(type);

        foreach (int frame in sequence)
        {
            _frameSet.Enqueue(frame);
        }

        if (type == MonsterAnimationType.Idle)
        {
            // 参考对 IDLE 不查偏移表（那张表里没有这一列），直接补同等数量的 0。
            for (int i = 0; i < sequence.Length; ++i)
            {
                _offsetSet.Enqueue(0);
            }
        }
        else
        {
            int[] offset = _reference.GetAnimationOffset(type);

            foreach (int value in offset)
            {
                _offsetSet.Enqueue(value);
            }
        }

        if (_offsetSet.Count != _frameSet.Count)
        {
            // 两边不等长说明这一支动画的偏移表与帧表不一致：补 0 而不是崩掉。
            while (_offsetSet.Count > _frameSet.Count)
            {
                _offsetSet.Dequeue();
            }

            while (_offsetSet.Count < _frameSet.Count)
            {
                _offsetSet.Enqueue(0);
            }
        }
    }

    /// <summary>Adds a track to the draw bag, <c>_addValidMove()</c> (<c>monster_anim.cpp:171-175</c>).</summary>
    private void AddValidMove(MonsterAnimationType type)
    {
        if (_reference.GetAnimationVector(type).Length > 0)
        {
            _validMoves.Add(type);
        }
    }

    /// <summary>Takes one frame and one offset off the queues, <c>_updateFrameInfo()</c> (<c>monster_anim.cpp:177-189</c>).</summary>
    private void UpdateFrameInfo()
    {
        if (_frameSet.Count == 0)
        {
            return;
        }

        _frameId = _frameSet.Dequeue();

        if (_offsetSet.Count > 0)
        {
            _frameOffset = _offsetSet.Dequeue();
        }
    }
}
