using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Shows the screen with a foreign palette (a video, an intro) and puts the game palette back
/// afterwards, a port of <c>ScreenPaletteRestorer</c>
/// (<c>src/fheroes2/gui/ui_tool.cpp:349-367</c>, declared at <c>ui_tool.h:261-277</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考用它把屏幕调色板换成视频自带的那一份，作用域结束时（析构）没换过就不动，换过就切回游戏调色板并把
/// 色彩循环重新打开。端口同样用 <c>using</c> 表达作用域：构造时记下游戏调色板，<see cref="ChangePalette"/>
/// 每帧换一份，<see cref="Dispose"/> 时切回。
/// </para>
/// <para>
/// 差异：参考是全进程单例（<c>Display::instance()</c>），端口把屏幕与游戏调色板作为构造参数注入。
/// </para>
/// <para>
/// 差异：参考的构造与析构还调用 <c>RenderProcessor::instance().stopColorCycling()/startColorCycling()</c>，
/// 让视频播放期间调色板不被循环动画改写。端口还没有色彩循环（<c>engine/render_processor.cpp</c> 未移植，
/// 所有画面都按固定调色板出图），这一步在端口里恒等于「什么都不用做」，等色彩循环移植时在这里接上。
/// </para>
/// </remarks>
public sealed class ScreenPaletteRestorer : IDisposable
{
    private readonly ScreenBuffer _display;
    private readonly Palette _gamePalette;
    private bool _paletteChanged;
    private bool _disposed;

    /// <summary>Takes over the screen palette and remembers the game palette to return to.</summary>
    /// <param name="display">Frame whose palette is swapped.</param>
    /// <param name="gamePalette">
    /// The palette to restore on <see cref="Dispose"/>, <c>getGamePalette()</c> of the reference.
    /// </param>
    /// <remarks>参考：<c>ui_tool.cpp:349-352</c>（在那里只做停循环，屏幕调色板在第一次 changePalette 时才换）。</remarks>
    public ScreenPaletteRestorer(ScreenBuffer display, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(gamePalette);

        _display = display;
        _gamePalette = gamePalette;
    }

    /// <summary>Whether <see cref="ChangePalette"/> has been called, <c>_paletteChanged</c>.</summary>
    public bool PaletteChanged => _paletteChanged;

    /// <summary>Shows the screen with <paramref name="palette"/>.</summary>
    /// <param name="palette">Palette of the video or animation being played.</param>
    /// <remarks>参考：<c>ui_tool.cpp:363-367</c>。</remarks>
    public void ChangePalette(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        _display.ChangePalette(palette);
        _paletteChanged = true;
    }

    /// <summary>Restores the game palette when it was swapped.</summary>
    /// <remarks>参考：<c>ui_tool.cpp:354-361</c>。</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_paletteChanged)
        {
            _display.ChangePalette(_gamePalette);
        }
    }
}
