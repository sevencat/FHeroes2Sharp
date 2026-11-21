using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Blocks the window close request for the duration of a scope, a port of
/// <c>ApplicationClosureRestorer</c> (<c>src/fheroes2/gui/ui_tool.cpp:369-377</c>, declared at
/// <c>ui_tool.h:279-288</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在构造时把 <c>LocalEvent</c> 的「退出事件处理钩子」换成一个恒返回 <see langword="false"/> 的 lambda，
/// 于是作用域内点窗口关闭按钮不会退出游戏；析构时换回标准的
/// <c>Game::processExitEvent() == QUIT_GAME</c>。
/// </para>
/// <para>
/// 差异：参考的析构直接写死标准钩子，端口把标准钩子作为构造参数传进来——宿主的退出流程是自己的方法，
/// 这里不去猜它叫什么，只负责原样还回去。
/// </para>
/// </remarks>
public sealed class ApplicationClosureRestorer : IDisposable
{
    private readonly LocalEvent _localEvent;
    private readonly Func<bool> _standardHook;
    private bool _disposed;

    /// <summary>Takes over the close request handling of <paramref name="localEvent"/>.</summary>
    /// <param name="localEvent">Event queue whose quit hook is replaced.</param>
    /// <param name="standardHook">
    /// The hook to put back on <see cref="Dispose"/>, the reference's
    /// <c>() { return Game::processExitEvent() == fheroes2::GameMode::QUIT_GAME; }</c>.
    /// </param>
    /// <remarks>参考：<c>ui_tool.cpp:369-372</c>。</remarks>
    public ApplicationClosureRestorer(LocalEvent localEvent, Func<bool> standardHook)
    {
        ArgumentNullException.ThrowIfNull(localEvent);
        ArgumentNullException.ThrowIfNull(standardHook);

        _localEvent = localEvent;
        _standardHook = standardHook;

        _localEvent.QuitEventHook = IgnoreClose;
    }

    /// <summary>Puts the standard close request handling back.</summary>
    /// <remarks>参考：<c>ui_tool.cpp:374-377</c>。</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localEvent.QuitEventHook = _standardHook;
    }

    /// <summary>The hook installed while the scope is alive: a close request never quits.</summary>
    private static bool IgnoreClose() => false;
}
