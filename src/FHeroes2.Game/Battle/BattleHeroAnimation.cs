using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The battle poses of a hero or captain, the reference's <c>Battle::HeroAnimation</c>
/// (<c>battle_interface.h:85-99</c>).
/// </summary>
/// <remarks>
/// 枚举顺序就是 <c>heroTypeAnim</c> 的列顺序，<see cref="BattleHeroSprites.GetFrames"/> 直接拿序号当下的标。
/// </remarks>
public enum BattleHeroAnimation
{
    /// <summary>Cheers over a won fight, <c>OP_JOY</c>.</summary>
    Joy,

    /// <summary>Starts a mass spell, <c>OP_CAST_MASS</c>.</summary>
    CastMass,

    /// <summary>Comes back from a mass spell, <c>OP_CAST_MASS_RETURN</c>.</summary>
    CastMassReturn,

    /// <summary>Starts an upward spell, <c>OP_CAST_UP</c>.</summary>
    CastUp,

    /// <summary>Comes back from an upward spell, <c>OP_CAST_UP_RETURN</c>.</summary>
    CastUpReturn,

    /// <summary>Starts a downward spell, <c>OP_CAST_DOWN</c>.</summary>
    CastDown,

    /// <summary>Comes back from a downward spell, <c>OP_CAST_DOWN_RETURN</c>.</summary>
    CastDownReturn,

    /// <summary>First idle pose, <c>OP_IDLE</c>.</summary>
    Idle,

    /// <summary>Second idle pose, <c>OP_IDLE2</c>.</summary>
    Idle2,

    /// <summary>The standing pose every animation ends on, <c>OP_STATIC</c>.</summary>
    Static,

    /// <summary>Sighs over a fallen stack, <c>OP_SORROW</c>.</summary>
    Sorrow,
}

/// <summary>
/// The animation state of one hero or captain, the behaviour half of <c>Battle::OpponentSprite</c>
/// (<c>battle_interface.cpp:1056-1140</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考一个类里既放状态又放绘制；端口把状态（当前姿势、当前帧、站起来多久该动一下）拿出来，
/// 好让无资产的分支也能断言。绘制见 <c>FHeroes2.UI.Battle.BattleOpponentSprite</c>。
/// </para>
/// <para>
/// 与参考一致：施法的「去」阶段（<see cref="BattleHeroAnimation.CastMass"/> /
/// <see cref="BattleHeroAnimation.CastUp"/> / <see cref="BattleHeroAnimation.CastDown"/>）播到最后一帧
/// 就停在那里等外部切到「回」阶段，不会自己回到 <see cref="BattleHeroAnimation.Static"/>。
/// </para>
/// </remarks>
public sealed class BattleOpponentAnimation
{
    /// <summary>How long a standing hero waits before it idles, <c>_idleTimer{ 8000 }</c>.</summary>
    private const uint IdleDelay = 8000;

    private readonly MapRace _race;
    private readonly bool _isCaptain;
    private readonly RandomizedDelay _idleTimer;
    private readonly Func<int, int, int> _random;

    private AnimationSequence _frames;
    private BattleHeroAnimation _type;

    /// <summary>Starts on the standing pose, as the reference's constructor does.</summary>
    /// <param name="race">The race of the commander.</param>
    /// <param name="isCaptain">Whether the commander is a captain.</param>
    /// <param name="clockMs">Monotonic clock in milliseconds, for tests; <see langword="null"/> for the real one.</param>
    /// <param name="random">The generator behind <c>Rand::Get</c>, for tests; <see langword="null"/> for the real one.</param>
    public BattleOpponentAnimation(MapRace race, bool isCaptain, Func<long>? clockMs = null, Func<int, int, int>? random = null)
    {
        _race = race;
        _isCaptain = isCaptain;
        _idleTimer = new RandomizedDelay(IdleDelay, clockMs);
        _random = random ?? Rand.Get;
        _type = BattleHeroAnimation.Static;
        _frames = new AnimationSequence(BattleHeroSprites.GetFrames(race, isCaptain, BattleHeroAnimation.Static));
    }

    /// <summary>The pose being played, <c>OpponentSprite::_animationType</c>.</summary>
    public BattleHeroAnimation Type => _type;

    /// <summary>Whether the pose has reached its last frame, <c>OpponentSprite::isFinishFrame()</c>.</summary>
    public bool IsFinishFrame => _frames.IsLastFrame;

    /// <summary>The frame of the archive to draw right now, <c>_currentAnim.getFrame()</c>.</summary>
    public int Frame => _frames.Frame;

    /// <summary>Switches to another pose, <c>OpponentSprite::SetAnimation()</c>.</summary>
    /// <param name="rule">The pose to play from its first frame.</param>
    public void SetAnimation(BattleHeroAnimation rule)
    {
        _type = rule;
        _frames = new AnimationSequence(BattleHeroSprites.GetFrames(_race, _isCaptain, rule));
    }

    /// <summary>Steps one frame, <c>OpponentSprite::IncreaseAnimFrame()</c>.</summary>
    public void IncreaseAnimFrame() => _frames.PlayAnimation(false);

    /// <summary>
    /// The per-tick step of the reference's <c>updateAnimationState()</c>.
    /// </summary>
    /// <returns>Whether the pose or the frame changed; the caller redraws the hero then.</returns>
    public bool UpdateAnimationState()
    {
        if (IsFinishFrame)
        {
            if (_type != BattleHeroAnimation.Static)
            {
                // Still on the way to the target of a cast: hold the last frame until the caller returns.
                if (_type is not (BattleHeroAnimation.CastMass or BattleHeroAnimation.CastUp or BattleHeroAnimation.CastDown))
                {
                    SetAnimation(BattleHeroAnimation.Static);

                    return true;
                }
            }
            else if (_idleTimer.CheckDelay())
            {
                // 1 in 3, the reference's `Rand::Get( 1, 3 ) < 2`.
                SetAnimation(_random(1, 3) < 2 ? BattleHeroAnimation.Idle2 : BattleHeroAnimation.Idle);

                return true;
            }

            return false;
        }

        IncreaseAnimFrame();

        return true;
    }
}
