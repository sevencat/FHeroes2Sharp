using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Cursor;

/// <summary>
/// Sets the cursor for the lifetime of the scope and puts the previous one back on <see cref="Dispose"/>, the port of
/// <c>CursorRestorer</c> (<c>src/fheroes2/gui/cursor.h:223-240</c>, <c>cursor.cpp:239-271</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考里这是唯一被大量使用的光标入口（<c>CursorRestorer cursorRestorer( false );</c> 几乎每个对话框都有），所以语义要
/// 照抄：构造时先记下当前的可见性与主题，两参数版本还会立刻切换主题；析构时先恢复可见性、再恢复主题。
/// </para>
/// <para>
/// 偏差：析构末尾「软件光标且主题未变、刚由隐藏变显示时，强制重绘指针下那一像素」的那一步没搬
/// （<c>cursor.cpp:265-270</c> 的 <c>Display::instance().render()</c>）。端口没有软件光标，也没有按矩形重绘的入口；
/// 等 App 层有软件光标时再补，届时用 <see cref="CursorState.UpdateCursorPosition"/> 的返回值即可。
/// </para>
/// </remarks>
public sealed class CursorRestorer : IDisposable
{
    private readonly CursorTheme _theme;
    private readonly bool _visible;

    private bool _disposed;

    /// <summary>Shows or hides the cursor for the lifetime of the scope, keeping the current theme.</summary>
    /// <param name="visible">Whether the cursor should be visible.</param>
    /// <exception cref="InvalidOperationException"><see cref="CursorState.Current"/> is not set.</exception>
    public CursorRestorer(bool visible)
    {
        CursorState state = GetState();

        _theme = state.Theme;
        _visible = state.IsVisible;

        state.Show(visible);
    }

    /// <summary>Shows or hides the cursor and switches it to <paramref name="theme"/> for the lifetime of the scope.</summary>
    /// <param name="visible">Whether the cursor should be visible.</param>
    /// <param name="theme">The theme to show until the scope ends.</param>
    /// <exception cref="InvalidOperationException"><see cref="CursorState.Current"/> is not set.</exception>
    public CursorRestorer(bool visible, CursorTheme theme)
    {
        CursorState state = GetState();

        _theme = state.Theme;
        _visible = state.IsVisible;

        state.SetThemes(theme);
        state.Show(visible);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (CursorState.Current is not { } state)
        {
            return;
        }

        state.Show(_visible);
        state.SetThemes(_theme);
    }

    private static CursorState GetState() => CursorState.Current
        ?? throw new InvalidOperationException("CursorState.Current is not set; the composition root has to create it.");
}
