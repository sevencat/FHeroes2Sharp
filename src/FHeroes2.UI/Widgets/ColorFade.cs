using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Fades the screen to grayscale, a port of <c>fheroes2::colorFade()</c> (<c>src/fheroes2/gui/ui_tool.cpp:395-483</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考做的事：把当前调色板逐项换成「游戏调色板里最接近的灰阶色」（在 10..36 号这一段里找），
/// 然后把调色板按 <c>durationMs / delay</c> 步线性插值到灰阶，最后把画面像素按「原索引 → 灰阶索引」
/// 的表重映射一次，于是画面变成原调色板下的灰阶图。搜索距离、插值公式
/// （<c>(start * (steps - id) + end * id) / steps</c>，整数除）、「最后一步不渲染」都照抄。
/// </para>
/// <para>
/// 差异：参考在函数内部自己跑事件循环（<c>LocalEvent::HandleEvents</c> 与 <c>Display::render</c> 一起阻塞
/// 到动画结束），端口把循环交给宿主——每帧调一次 <see cref="TryAdvance"/>，动画结束后调
/// <see cref="Complete"/> 落回游戏调色板。步数、延时与每帧调色板的值与参考完全一致。
/// </para>
/// <para>
/// 差异：<c>delay = round(1000 / fps)</c> 为 0 时参考会整除零，端口把它夹到 1 毫秒，
/// 于是 <c>StepCount</c> 退化为 0、动画立即结束（画面只会走最后一次重映射）。
/// </para>
/// </remarks>
public sealed class ColorFade
{
    /// <summary>First grayscale entry of the game palette searched by the reference, <c>10</c>.</summary>
    public const int StartGrayScaleColorId = 10;

    /// <summary>Last grayscale entry of the game palette searched by the reference, <c>36</c>.</summary>
    public const int EndGrayScaleColorId = 36;

    private readonly byte[] _startPalette;
    private readonly byte[] _endPalette;
    private readonly byte[] _colorMap = new byte[Palette.ColorCount];
    private readonly byte[] _gradingPalette = new byte[Palette.RawPaletteSize];

    private int _gradingId = 1;

    /// <summary>
    /// Prepares the fade, everything <c>colorFade()</c> does before its loop.
    /// </summary>
    /// <param name="currentPalette">The palette the screen is drawn with right now.</param>
    /// <param name="normalizedGamePalette">
    /// The game palette in video values, <c>getNormalizedRGBGamePalette()</c> of the reference
    /// (the 6 bit <c>KB.PAL</c> entries shifted left by two bits).
    /// </param>
    /// <param name="frameRoi">Region of the screen that fades and gets remapped.</param>
    /// <param name="durationMs">Length of the fade in milliseconds.</param>
    /// <param name="fps">Frames per second the reference would render at.</param>
    /// <remarks>参考：<c>ui_tool.cpp:395-452</c>。</remarks>
    public ColorFade(
        Palette currentPalette,
        Palette normalizedGamePalette,
        in Rect frameRoi,
        uint durationMs,
        double fps)
    {
        ArgumentNullException.ThrowIfNull(currentPalette);
        ArgumentNullException.ThrowIfNull(normalizedGamePalette);

        if (fps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fps), fps, "A fade needs a positive frame rate.");
        }

        FrameRoi = frameRoi;
        Fps = fps;
        DelayMs = Math.Max(1, (int)Math.Round(1000.0 / fps));
        StepCount = (int)(durationMs / (uint)DelayMs);

        _startPalette = currentPalette.ToRaw();
        byte[] normalized = normalizedGamePalette.ToRaw();
        _endPalette = new byte[_startPalette.Length];

        // The palette in use can differ from the game palette, so every entry is mapped onto the
        // nearest grayscale entry of the game palette before anything is blended.
        for (int id = 0; id < Palette.ColorCount; ++id)
        {
            int nearestDistance = int.MaxValue;

            for (int colorId = StartGrayScaleColorId; colorId <= EndGrayScaleColorId; ++colorId)
            {
                int redDiff = _startPalette[id * 3] - normalized[colorId * 3];
                int greenDiff = _startPalette[(id * 3) + 1] - normalized[(colorId * 3) + 1];
                int blueDiff = _startPalette[(id * 3) + 2] - normalized[(colorId * 3) + 2];

                int distance = (redDiff * redDiff) + (greenDiff * greenDiff) + (blueDiff * blueDiff);
                if (nearestDistance > distance)
                {
                    nearestDistance = distance;
                    _colorMap[id] = (byte)colorId;

                    if (distance == 0)
                    {
                        break;
                    }
                }
            }
        }

        for (int id = 0; id < Palette.ColorCount; ++id)
        {
            int source = _colorMap[id] * 3;
            _endPalette[id * 3] = normalized[source];
            _endPalette[(id * 3) + 1] = normalized[source + 1];
            _endPalette[(id * 3) + 2] = normalized[source + 2];
        }
    }

    /// <summary>Region of the screen the fade covers, <c>frameRoi</c> of the reference.</summary>
    public Rect FrameRoi { get; }

    /// <summary>Frame rate the reference would have rendered the fade at.</summary>
    public double Fps { get; }

    /// <summary>Delay between two grading steps in milliseconds, <c>round( 1000 / fps )</c>.</summary>
    public int DelayMs { get; }

    /// <summary>Number of grading steps, <c>durationMs / delay</c>.</summary>
    public int StepCount { get; }

    /// <summary>Whether every grading step has been handed out.</summary>
    /// <remarks>参考的循环在 <c>gradingId == gradingSteps</c> 时退出，最后一步不渲染。</remarks>
    public bool IsFinished => _gradingId >= StepCount;

    /// <summary>
    /// The remap table the reference applies after the fade, index of the original palette entry
    /// onto the grayscale entry of the game palette.
    /// </summary>
    public ReadOnlySpan<byte> ColorMap => _colorMap;

    /// <summary>Moves the fade to the next grading step.</summary>
    /// <param name="gradingPalette">The palette to draw the next frame with.</param>
    /// <returns><see langword="false"/> once the fade is over and <see cref="Complete"/> should run.</returns>
    /// <remarks>参考：<c>ui_tool.cpp:462-478</c>（一步一帧，调色板逐字节整数插值）。</remarks>
    public bool TryAdvance(out Palette gradingPalette)
    {
        gradingPalette = null!;

        if (IsFinished)
        {
            return false;
        }

        int startWeight = StepCount - _gradingId;

        for (int i = 0; i < _gradingPalette.Length; ++i)
        {
            _gradingPalette[i] = (byte)(((_startPalette[i] * startWeight) + (_endPalette[i] * _gradingId)) / StepCount);
        }

        ++_gradingId;
        gradingPalette = Palette.FromRaw(_gradingPalette);

        return true;
    }

    /// <summary>Recolours the faded frame into the grayscale entries of the game palette.</summary>
    /// <param name="target">Frame the fade was drawn into.</param>
    /// <remarks>参考：<c>ui_tool.cpp:482</c> 的 <c>ApplyPalette( display, ..., assignedValue )</c>。</remarks>
    public void Complete(PixelBuffer target) => ImageOps.ApplyPalette(target, FrameRoi, _colorMap);
}
