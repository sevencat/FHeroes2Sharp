using FHeroes2.Assets.Icn;

namespace FHeroes2.Game.Battle;

/// <summary>
/// One animated ICN in a battle dialog's backdrop, the reference's anonymous <c>LoopedAnimation</c>
/// (<c>battle_dialogs.cpp:83-138</c>).
/// </summary>
/// <remarks>
/// <para>
/// 动画不是「按时间取帧」，而是「每调用一次往前挪一帧」：<see cref="FrameId"/> 返回当前该画的那一帧，
/// 同时把内部计数器推进到下一帧。这样调用方只要在每次「动画延迟到点」时调用它一次，节奏就由调用方掌握
/// （参考用 <c>Game::validateAnimationDelay( BATTLE_DIALOG_DELAY )</c>），这个类不读时钟。
/// </para>
/// <para>
/// 参考里 <c>frameId()</c> 的返回值与内部状态差一帧：它先把计数器推进、算出下一帧，再 <c>std::swap</c>
/// 换成「返回刚画过的那一帧、内部指向下一帧」。端口逐字照抄这段交换，因为 <c>LoopedAnimationSequence</c>
/// 的结束判断（<c>nextId &lt; frameId</c> 即回绕）依赖它。
/// </para>
/// </remarks>
public sealed class BattleDialogAnimation
{
    private readonly IcnType _icn;
    private readonly bool _loop;

    private uint _counter;
    private uint _frameId;
    private bool _finished;

    /// <summary>Starts an animation, <c>LoopedAnimation( icnId, loop )</c>.</summary>
    /// <param name="icn">The animated ICN.</param>
    /// <param name="loop">Whether it starts over instead of stopping on the last frame.</param>
    public BattleDialogAnimation(IcnType icn, bool loop)
    {
        _icn = icn;
        _loop = loop;

        _frameId = IcnCatalog.GetAnimatedIndex(icn, 1, _counter);
    }

    /// <summary>The ICN being played, <c>id()</c>.</summary>
    public IcnType Icn => _icn;

    /// <summary>The frame that should be on screen now, <c>frameId()</c>'s returned value.</summary>
    public uint FrameId => _frameId;

    /// <summary>Whether the last frame has been reached and the animation stopped there, <c>isFinished()</c>.</summary>
    public bool IsFinished => _finished;

    /// <summary>
    /// Advances by one frame and returns the frame that was on screen before the advance, <c>frameId()</c>.
    /// </summary>
    /// <returns>The frame to draw now.</returns>
    /// <remarks>
    /// 参考的 <c>frameId()</c>（<c>battle_dialogs.cpp:96-120</c>）：计数器先加一，算出下一帧；下一帧比当前帧
    /// 小说明回绕了——非循环动画就此标记结束并停在最后一帧，循环动画把计数器清零重来。没结束就把两帧互换
    /// 并返回刚画过的那一帧。结束之后再调用只重复返回最后一帧，不再推进。
    /// </remarks>
    public uint Advance()
    {
        if (_finished)
        {
            return _frameId;
        }

        ++_counter;
        uint nextId = IcnCatalog.GetAnimatedIndex(_icn, 1, _counter);

        if (nextId < _frameId)
        {
            if (!_loop)
            {
                _finished = true;

                return _frameId;
            }

            _counter = 0;
            nextId = IcnCatalog.GetAnimatedIndex(_icn, 1, _counter);

            uint shown = _frameId;
            _frameId = nextId;

            return shown;
        }

        uint current = _frameId;
        _frameId = nextId;

        return current;
    }
}

/// <summary>
/// The queue of animations behind a battle dialog, the reference's anonymous <c>LoopedAnimationSequence</c>
/// (<c>battle_dialogs.cpp:140-179</c>).
/// </summary>
/// <remarks>
/// 参考把「被打败的三段动画 + 最后循环的胜利画面」排成一条队列，队头播完就弹出播下一段。端口保留这个结构，
/// 包括 <see cref="NextFrame"/> 那个与名字相反的返回值（空队列返回 <see langword="true"/>），因为调用点
/// 写成 <c>!sequence.NextFrame()</c> 时才读得懂。
/// </remarks>
public sealed class BattleDialogAnimationSequence
{
    private readonly Queue<BattleDialogAnimation> _queue = new();

    /// <summary>Appends an animation, <c>push()</c>.</summary>
    /// <param name="icn">The animated ICN.</param>
    /// <param name="loop">Whether it loops.</param>
    public void Push(IcnType icn, bool loop) => _queue.Enqueue(new BattleDialogAnimation(icn, loop));

    /// <summary>Whether every animation has been played, <c>isFinished()</c>.</summary>
    public bool IsFinished => _queue.Count == 0;

    /// <summary>The ICN at the head of the queue, <c>id()</c>; <see cref="IcnType.Unknown"/> when the queue is empty.</summary>
    public IcnType Icn => _queue.Count == 0 ? IcnType.Unknown : _queue.Peek().Icn;

    /// <summary>The frame the head animation should draw now, <c>frameId()</c>; 0 when the queue is empty.</summary>
    public uint FrameId => _queue.Count == 0 ? 0 : _queue.Peek().FrameId;

    /// <summary>
    /// Advances the head animation by one frame, dropping it when it has played out, <c>nextFrame()</c>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when no frames are left — the reference returns <c>_queue.empty()</c> despite the
    /// comment saying "returns true only if there is some frames left".
    /// </returns>
    public bool NextFrame()
    {
        if (_queue.Count > 0 && _queue.Peek().IsFinished)
        {
            _queue.Dequeue();
        }

        return _queue.Count == 0;
    }

    /// <summary>Advances the head animation one step and returns the frame that should be drawn, <c>frameId()</c>.</summary>
    /// <returns>The frame to draw.</returns>
    public uint AdvanceFrame()
    {
        if (_queue.Count == 0)
        {
            return 0;
        }

        return _queue.Peek().Advance();
    }
}
