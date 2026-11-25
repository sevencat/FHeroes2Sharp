namespace FHeroes2.Game.System;

/// <summary>
/// The pending display fade-in flag, <c>needFadeIn</c> and the three accessors of <c>game.cpp:165-182</c>.
/// </summary>
/// <remarks>
/// 参考把这个 flag 放在 <c>game.cpp</c> 的匿名命名空间里，靠 <c>Game::isFadeInNeeded()</c> /
/// <c>Game::setDisplayFadeIn()</c> / <c>Game::validateDisplayFadeIn()</c> 读写。端口没有全局 <c>Game</c> 类，于是跟
/// <see cref="GameAnimation"/> 一样单独放一个静态类。语义照抄：置位之后由第一个 validate 取走并返回 <see langword="true"/>，
/// 再问就是 <see langword="false"/>。
/// </remarks>
public static class GameDisplayFadeIn
{
    private static bool _needFadeIn;

    /// <summary>Whether a fade-in is pending, <c>Game::isFadeInNeeded()</c>.</summary>
    public static bool IsFadeInNeeded => _needFadeIn;

    /// <summary>Asks for the whole display to fade in, <c>Game::setDisplayFadeIn()</c>.</summary>
    /// <remarks>参考在整屏画面换过之后调它：下一次绘制要把新画面淡入，而不是直接切过去。</remarks>
    public static void SetDisplayFadeIn() => _needFadeIn = true;

    /// <summary>Consumes the pending fade-in, <c>Game::validateDisplayFadeIn()</c>.</summary>
    /// <returns><see langword="true"/> exactly once per <see cref="SetDisplayFadeIn"/>.</returns>
    public static bool ValidateDisplayFadeIn()
    {
        if (!_needFadeIn)
        {
            return false;
        }

        _needFadeIn = false;

        return true;
    }
}
