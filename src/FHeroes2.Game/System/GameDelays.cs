using System.Diagnostics;

using FHeroes2.Core.Timing;

namespace FHeroes2.Game.System;

/// <summary>
/// The animation delays of <c>enum class DelayType</c> (<c>src/fheroes2/game/game_delays.h:28-67</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把全部动画节拍放在一张表里，每格是一个 <see cref="TimeDelay"/>：界面问「这一格过了没有」，过了就画下一帧并把
/// 格子清零。端口先前是各个控件自己持有 <see cref="TimeDelay"/>，没有这张共享表；移植过来之后两边都能用，界面可以
/// 逐步改成查表。
/// </para>
/// <para>
/// 顺序有意义：表按枚举值下标存放，参考的 <c>delays[static_cast&lt;size_t&gt;( type )]</c> 就是这条约束，新增条目必须排在
/// <see cref="DelayType.LastDelay"/> 之前。
/// </para>
/// </remarks>
public enum DelayType : byte
{
    /// <summary>Scrolling the list one step.</summary>
    ScrollDelay = 0,

    /// <summary>The pause before a scroll starts repeating.</summary>
    ScrollStartDelay,

    /// <summary>Cursor blink in the text fields.</summary>
    CursorBlinkDelay,

    /// <summary>Animation of the main menu background.</summary>
    MainMenuDelay,

    /// <summary>Animation of the map list in the main menu.</summary>
    MapsDelay,

    /// <summary>Animation of the tavern in the castle.</summary>
    CastleTavernDelay,

    /// <summary>Animation of the castle surroundings.</summary>
    CastleAroundDelay,

    /// <summary>How often the castle redraws a changed building.</summary>
    CastleItemUpdateDelay,

    /// <summary>Animation of a dwarf cottage / other dwellings in the castle.</summary>
    CastleUnitDelay,

    /// <summary>Fade of a hero sprite while moving.</summary>
    HeroesFadeDelay,

    /// <summary>Interval between two pickups of a resource by a hero.</summary>
    HeroesPickupDelay,

    /// <summary>Fade of the puzzle map pieces.</summary>
    PuzzleFadeDelay,

    /// <summary>Advance of a battle dialog.</summary>
    BattleDialogDelay,

    /// <summary>Frame of a unit animation on the battlefield.</summary>
    BattleFrameDelay,

    /// <summary>Flight of a projectile.</summary>
    BattleMissileDelay,

    /// <summary>Frame of a spell effect.</summary>
    BattleSpellDelay,

    /// <summary>Frame of the disrupting ray.</summary>
    BattleDisruptingDelay,

    /// <summary>Shot of the catapult.</summary>
    BattleCatapultDelay,

    /// <summary>Flight of the boulder.</summary>
    BattleCatapultBoulderDelay,

    /// <summary>The dust cloud left by the boulder.</summary>
    BattleCatapultCloudDelay,

    /// <summary>The drawbridge going down.</summary>
    BattleBridgeDelay,

    /// <summary>Idle animation of a unit waiting for orders.</summary>
    BattleIdleDelay,

    /// <summary>Pause between two opponents' turns.</summary>
    BattleOpponentsDelay,

    /// <summary>Wave of the flags on the battlefield.</summary>
    BattleFlagsDelay,

    /// <summary>How long a damage popup stays on the screen.</summary>
    BattlePopupDelay,

    /// <summary>Cycle of the colour animation of a selected unit.</summary>
    BattleColorCycleDelay,

    /// <summary>Frame of the marker around the unit whose turn it is.</summary>
    BattleSelectedUnitDelay,

    /// <summary>Frame of a unit movement, set per speed by the player.</summary>
    CustomBattleUnitMovementDelay,

    /// <summary>Step of a human hero on the adventure map.</summary>
    CurrentHeroDelay,

    /// <summary>Step of an AI hero on the adventure map.</summary>
    CurrentAiDelay,

    /// <summary>
    /// The slot the reference reserves for the caller's own delay,
    /// <c>DelayType::CUSTOM_DELAY</c>. Never pass it to the table-driven members.
    /// </summary>
    CustomDelay,

    /// <summary>One past the last slot, <c>LAST_DELAY</c>.</summary>
    LastDelay,
}

/// <summary>
/// The animation clock of <c>src/fheroes2/game/game_delays.cpp</c>: one table of
/// <see cref="TimeDelay"/>, the two hero speed multipliers and the battle speed arithmetic.
/// </summary>
/// <remarks>
/// <para>
/// 参考的这张表是进程级静态量，一人一份；端口照旧用静态类，因为节拍本来就是全局的：同一个 <c>BATTLE_FRAME_DELAY</c>
/// 既管进攻方也管防守方，拆成实例反而要把表传遍整个战斗。
/// </para>
/// <para>
/// <b>Read the note on <see cref="ApplyBattleSpeed"/>.</b> 把它和 <see cref="UpdateGameSpeed"/> 里的公式放在一起看，
/// 才知道为什么战斗速度是 10 时还要留 1/3 步：参考注释说那一步是为了不让延迟变成 0。
/// </para>
/// </remarks>
public static class GameDelays
{
    /// <summary>
    /// Milliseconds between two edge scroll steps, the <c>setDelay( DelayType::SCROLL_DELAY, 20 )</c> of
    /// <c>game_delays.cpp:96</c>.
    /// </summary>
    /// <remarks>
    /// 参考的卷屏是按这张表节拍的（<c>Game::validateAnimationDelay( SCROLL_DELAY )</c>），表里那一格就是从这里装进去的。
    /// 谁的界面不按全局节拍走（编辑器按帧的 <c>dt</c> 自己累计，见 <c>EditorScene.EdgeScrollInterval</c>），至少也要用
    /// 同一个数字，别在视图层再抄一个 20。
    /// </remarks>
    public const int ScrollDelayMilliseconds = 20;

    /// <summary>
    /// Milliseconds between two steps of the adventure map object animation, the
    /// <c>setDelay( DelayType::MAPS_DELAY, 250 )</c> of <c>game_delays.cpp:100</c>.
    /// </summary>
    /// <remarks>
    /// 与 <see cref="ScrollDelayMilliseconds"/> 同理：谁的界面不按全局节拍走（编辑器按帧的 <c>dt</c> 自己累计，
    /// 见 <c>EditorScene.AnimationInterval</c>），也要用表里的这个数字，别在视图层再抄一个 250。
    /// </remarks>
    public const int MapsDelayMilliseconds = 250;

    /// <summary>
    /// <c>battleSpeedAdjustment</c> of <c>game_delays.cpp:38</c>: one step of the battle speed scale, chosen so that
    /// speed 1 takes six times as long as speed 10.
    /// </summary>
    private static readonly double BattleSpeedAdjustment = 1.0 / (10 - Settings.DefaultBattleSpeed);

    /// <summary>The delay of every <see cref="DelayType"/>, <c>delays</c>.</summary>
    private static readonly TimeDelay[] Delays = CreateDelays();

    private static int _humanHeroMultiplier = 1;

    private static int _aiHeroMultiplier = 1;

    /// <summary>
    /// Gives the human hero an animation speed multiplier, <c>Game::HumanHeroAnimSpeedMultiplier()</c>
    /// (<c>game_delays.cpp:205-210</c>).
    /// </summary>
    /// <remarks>
    /// 1 到 5 速是每格走一步（倍数 1），6 到 7 速两格并一步（倍数 2），8 速以上四格并一步（倍数 4）——参考用
    /// 「跳过的格数」换速度，而不是把每格的延迟除下去，所以高速度下的动画看起来还是流畅的。
    /// </remarks>
    public static int HumanHeroAnimSpeedMultiplier()
    {
        Debug.Assert(_humanHeroMultiplier > 0);

        return _humanHeroMultiplier;
    }

    /// <summary>
    /// Gives the AI hero an animation speed multiplier, <c>Game::AIHeroAnimSpeedMultiplier()</c>
    /// (<c>game_delays.cpp:212-217</c>).
    /// </summary>
    /// <returns>The multiplier, 1, 2 or 4.</returns>
    /// <remarks>Same scale as the human one, driven by <see cref="Settings.AiMoveSpeed"/>.</remarks>
    public static int AiHeroAnimSpeedMultiplier()
    {
        Debug.Assert(_aiHeroMultiplier > 0);

        return _aiHeroMultiplier;
    }

    /// <summary>
    /// Sets every delay to its starting value, <c>Game::initAnimateDelays()</c> (<c>game_delays.cpp:94-132</c>).
    /// </summary>
    /// <remarks>
    /// 只有战斗相关的格子依赖玩家速度，其余是固定节拍；装完表之后统一清零，免得第一节就一口气画完好几帧。
    /// </remarks>
    public static void InitAnimateDelays()
    {
        SetDelay(DelayType.ScrollDelay, ScrollDelayMilliseconds);
        SetDelay(DelayType.ScrollStartDelay, ScrollDelayMilliseconds);
        SetDelay(DelayType.CursorBlinkDelay, 440);
        SetDelay(DelayType.MainMenuDelay, 250);
        SetDelay(DelayType.MapsDelay, MapsDelayMilliseconds);
        SetDelay(DelayType.CastleTavernDelay, 75);
        SetDelay(DelayType.CastleAroundDelay, 200);
        SetDelay(DelayType.CastleItemUpdateDelay, 60);
        SetDelay(DelayType.CastleUnitDelay, 150);
        SetDelay(DelayType.HeroesFadeDelay, 32);
        SetDelay(DelayType.HeroesPickupDelay, 40);
        SetDelay(DelayType.PuzzleFadeDelay, 50);
        SetDelay(DelayType.BattleDialogDelay, 75);
        SetDelay(DelayType.BattleFrameDelay, 120);
        SetDelay(DelayType.BattleMissileDelay, 40);
        SetDelay(DelayType.BattleSpellDelay, 75);
        SetDelay(DelayType.BattleDisruptingDelay, 25);
        SetDelay(DelayType.BattleCatapultDelay, 90);
        SetDelay(DelayType.BattleCatapultBoulderDelay, 40);
        SetDelay(DelayType.BattleCatapultCloudDelay, 40);
        SetDelay(DelayType.BattleBridgeDelay, 90);
        SetDelay(DelayType.BattleIdleDelay, 150);
        SetDelay(DelayType.BattleOpponentsDelay, 75);
        SetDelay(DelayType.BattleFlagsDelay, 250);
        SetDelay(DelayType.BattlePopupDelay, 800);
        SetDelay(DelayType.BattleColorCycleDelay, 220);
        SetDelay(DelayType.BattleSelectedUnitDelay, 160);
        SetDelay(DelayType.CustomBattleUnitMovementDelay, 10);
        SetDelay(DelayType.CurrentHeroDelay, 10);
        SetDelay(DelayType.CurrentAiDelay, 10);

        UpdateGameSpeed();

        foreach (TimeDelay delay in Delays)
        {
            delay.Reset();
        }
    }

    /// <summary>
    /// Restarts one delay, <c>Game::AnimateResetDelay()</c> (<c>game_delays.cpp:134-137</c>).
    /// </summary>
    /// <param name="delayType">The delay to restart.</param>
    public static void AnimateResetDelay(DelayType delayType) => Delays[(int)delayType].Reset();

    /// <summary>
    /// Whether <paramref name="delayMs"/> has passed since the caller's own delay was last reset, resetting it when it
    /// has, <c>Game::validateCustomAnimationDelay()</c> (<c>game_delays.cpp:139-147</c>).
    /// </summary>
    /// <param name="delayMs">The length the caller asks for.</param>
    /// <returns>True once, on the frame the delay expires.</returns>
    public static bool ValidateCustomAnimationDelay(ulong delayMs)
    {
        TimeDelay custom = Delays[(int)DelayType.CustomDelay];
        if (!custom.IsPassed(delayMs))
        {
            return false;
        }

        custom.Reset();

        return true;
    }

    /// <summary>
    /// Whether one delay has passed, resetting it when it has, <c>Game::validateAnimationDelay()</c>
    /// (<c>game_delays.cpp:149-159</c>).
    /// </summary>
    /// <param name="delayType">The delay to test; <see cref="DelayType.CustomDelay"/> is not allowed.</param>
    /// <returns>True once, on the frame the delay expires.</returns>
    public static bool ValidateAnimationDelay(DelayType delayType)
    {
        Debug.Assert(delayType != DelayType.CustomDelay);

        TimeDelay delay = Delays[(int)delayType];
        if (!delay.IsPassed())
        {
            return false;
        }

        delay.Reset();

        return true;
    }

    /// <summary>
    /// Makes the next <see cref="ValidateAnimationDelay"/> of <paramref name="delayType"/> answer true,
    /// <c>Game::passAnimationDelay()</c> (<c>game_delays.cpp:161-166</c>).
    /// </summary>
    /// <param name="delayType">The delay to pass; <see cref="DelayType.CustomDelay"/> is not allowed.</param>
    /// <remarks>参考用它让循环的第一轮必定画出来，否则第一帧要等满一个延迟。</remarks>
    public static void PassAnimationDelay(DelayType delayType)
    {
        Debug.Assert(delayType != DelayType.CustomDelay);

        Delays[(int)delayType].Pass();
    }

    /// <summary>
    /// Makes the next <see cref="ValidateCustomAnimationDelay"/> of <paramref name="delayMs"/> answer true,
    /// <c>Game::passCustomAnimationDelay()</c> (<c>game_delays.cpp:168-172</c>).
    /// </summary>
    /// <param name="delayMs">The length the caller asks for.</param>
    public static void PassCustomAnimationDelay(ulong delayMs)
    {
        SetDelay(DelayType.CustomDelay, delayMs);
        Delays[(int)DelayType.CustomDelay].Pass();
    }

    /// <summary>
    /// Sets the length of a unit's movement frame, <c>Game::setCustomUnitMovementDelay()</c>
    /// (<c>game_delays.cpp:174-177</c>).
    /// </summary>
    /// <param name="delayMs">The length; zero is raised to one millisecond.</param>
    /// <remarks>参考不接受 0，因为延迟为 0 的格子每一帧都算「已过」，动画会瞬间播完。</remarks>
    public static void SetCustomUnitMovementDelay(ulong delayMs) =>
        SetDelay(DelayType.CustomBattleUnitMovementDelay, delayMs > 0 ? delayMs : 1);

    /// <summary>
    /// Recomputes the delays that follow the player's speed settings, <c>Game::UpdateGameSpeed()</c>
    /// (<c>game_delays.cpp:179-203</c>).
    /// </summary>
    public static void UpdateGameSpeed()
    {
        Settings conf = Settings.Get();

        SetupHeroMovement(conf.HeroesMoveSpeed, Delays[(int)DelayType.CurrentHeroDelay], ref _humanHeroMultiplier);
        SetupHeroMovement(conf.AiMoveSpeed, Delays[(int)DelayType.CurrentAiDelay], ref _aiHeroMultiplier);

        int battleSpeed = conf.BattleSpeed;

        // 速度 10 时公式给 0，参考留了三分之一格的步长，只为让延迟不为 0。
        double adjustedBattleSpeed = battleSpeed < 10
            ? (10 - battleSpeed) * BattleSpeedAdjustment
            : BattleSpeedAdjustment / 3;

        // 待机动画的缩放比其他战斗动画温和：速度 1 是 1.2 倍，速度 10 是 0.8 倍。
        double adjustedIdleAnimationSpeed = (28 - battleSpeed) / 22.5;

        SetDelay(DelayType.BattleFrameDelay, (ulong)(120 * adjustedBattleSpeed));
        SetDelay(DelayType.BattleMissileDelay, (ulong)(40 * adjustedBattleSpeed));
        SetDelay(DelayType.BattleSpellDelay, (ulong)(75 * adjustedBattleSpeed));
        SetDelay(DelayType.BattleDisruptingDelay, (ulong)(25 * adjustedBattleSpeed));
        SetDelay(DelayType.BattleCatapultDelay, (ulong)(90 * adjustedBattleSpeed));
        SetDelay(DelayType.BattleCatapultBoulderDelay, (ulong)(40 * adjustedBattleSpeed));
        SetDelay(DelayType.BattleCatapultCloudDelay, (ulong)(40 * adjustedBattleSpeed));
        SetDelay(DelayType.BattleBridgeDelay, (ulong)(90 * adjustedBattleSpeed));
        SetDelay(DelayType.BattleIdleDelay, (ulong)(150 * adjustedIdleAnimationSpeed));
        SetDelay(DelayType.BattleOpponentsDelay, (ulong)(75 * adjustedIdleAnimationSpeed));
        SetDelay(DelayType.BattleFlagsDelay, (ulong)(250 * adjustedIdleAnimationSpeed));
    }

    /// <summary>
    /// Scales a hand-written delay by the battle speed, <c>Game::ApplyBattleSpeed()</c>
    /// (<c>game_delays.cpp:219-223</c>).
    /// </summary>
    /// <param name="delay">The delay at speed 1.</param>
    /// <returns>The delay at the current speed, never zero.</returns>
    /// <remarks>
    /// 用的是 <c>1 / (10 - defaultBattleSpeed)</c> 这个步长，即「速度 10 只有速度 1 的六分之一」；和
    /// <see cref="UpdateGameSpeed"/> 的乘法是同一套缩放，只是这里给调用方留着自定义的基数。
    /// </remarks>
    public static uint ApplyBattleSpeed(uint delay)
    {
        double scaled = BattleSpeedAdjustment * (10 - Settings.Get().BattleSpeed) * delay;
        uint result = (uint)scaled;

        return result == 0 ? 1 : result;
    }

    /// <summary>
    /// Whether every delay of <paramref name="delayTypes"/> has passed, <c>Game::hasEveryDelayPassed()</c>
    /// (<c>game_delays.cpp:225-234</c>).
    /// </summary>
    /// <param name="delayTypes">The delays to test.</param>
    /// <returns>True when all of them expired.</returns>
    /// <remarks>不复位，只是问一句。</remarks>
    public static bool HasEveryDelayPassed(IReadOnlyList<DelayType> delayTypes)
    {
        ArgumentNullException.ThrowIfNull(delayTypes);

        foreach (DelayType type in delayTypes)
        {
            if (!Delays[(int)type].IsPassed())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether at least one delay of <paramref name="delayTypes"/> still has to run, <c>Game::isDelayNeeded()</c>
    /// (<c>game_delays.cpp:236-247</c>).
    /// </summary>
    /// <param name="delayTypes">The delays to test; <see cref="DelayType.CustomDelay"/> is not allowed.</param>
    /// <returns>True when at least one delay has not expired yet.</returns>
    /// <remarks>参考的实现是「任何一个还没过就返回 true 让调用方跳过这一帧」，语义上等于取反的全体已过。</remarks>
    public static bool IsDelayNeeded(IReadOnlyList<DelayType> delayTypes)
    {
        ArgumentNullException.ThrowIfNull(delayTypes);

        foreach (DelayType type in delayTypes)
        {
            Debug.Assert(type != DelayType.CustomDelay);

            if (Delays[(int)type].IsPassed())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a custom delay of <paramref name="delayMs"/> still has to run, <c>Game::isCustomDelayNeeded()</c>
    /// (<c>game_delays.cpp:249-252</c>).
    /// </summary>
    /// <param name="delayMs">The length the caller asks for.</param>
    /// <returns>True while the delay has not expired.</returns>
    public static bool IsCustomDelayNeeded(ulong delayMs) => !Delays[(int)DelayType.CustomDelay].IsPassed(delayMs);

    /// <summary>
    /// The length of one delay, <c>Game::getAnimationDelayValue()</c> (<c>game_delays.cpp:254-258</c>).
    /// </summary>
    /// <param name="delayType">The delay to ask about; <see cref="DelayType.CustomDelay"/> is not allowed.</param>
    /// <returns>The length in milliseconds.</returns>
    public static ulong GetAnimationDelayValue(DelayType delayType)
    {
        Debug.Assert(delayType != DelayType.CustomDelay);

        return Delays[(int)delayType].Delay;
    }

    /// <summary>
    /// Gives a hero the delay and the multiplier of <paramref name="speed"/>, <c>SetupHeroMovement()</c>
    /// (<c>game_delays.cpp:43-86</c>).
    /// </summary>
    /// <param name="speed">The speed setting, 0 to 10.</param>
    /// <param name="delay">The delay to change.</param>
    /// <param name="multiplier">The multiplier to change.</param>
    /// <remarks>
    /// 1 到 5 速每格越长越短（18 到 10 毫秒），6 速开始反而把格子拉长成 16 毫秒同时一次跳过 2 到 4 格——不是笔误，
    /// 参考就是这么写的：低速度看的是「一格一格走」，高速度看的是「一帧一大步」。
    /// </remarks>
    private static void SetupHeroMovement(int speed, TimeDelay delay, ref int multiplier)
    {
        switch (speed)
        {
            case 1:
                delay.Delay = 18;
                multiplier = 1;
                break;
            case 2:
                delay.Delay = 16;
                multiplier = 1;
                break;
            case 3:
                delay.Delay = 14;
                multiplier = 1;
                break;
            case 4:
                delay.Delay = 12;
                multiplier = 1;
                break;
            case 5:
                delay.Delay = 10;
                multiplier = 1;
                break;
            case 6:
                delay.Delay = 16;
                multiplier = 2;
                break;
            case 7:
                delay.Delay = 12;
                multiplier = 2;
                break;
            case 8:
                delay.Delay = 16;
                multiplier = 4;
                break;
            case 9:
                delay.Delay = 8;
                multiplier = 4;
                break;
            default:
                delay.Delay = 1;
                multiplier = 4;
                break;
        }
    }

    /// <summary>Sets the length of one delay, the anonymous <c>setDelay()</c> of <c>game_delays.cpp:88-91</c>.</summary>
    /// <param name="type">The delay to change.</param>
    /// <param name="delayMs">The new length.</param>
    private static void SetDelay(DelayType type, ulong delayMs) => Delays[(int)type].Delay = delayMs;

    /// <summary>Builds the table, one zero-length delay per slot.</summary>
    /// <returns>The table, ready for <see cref="InitAnimateDelays"/>.</returns>
    private static TimeDelay[] CreateDelays()
    {
        TimeDelay[] delays = new TimeDelay[(int)DelayType.LastDelay];
        for (int i = 0; i < delays.Length; ++i)
        {
            delays[i] = new TimeDelay(0);
        }

        return delays;
    }
}
