using FHeroes2.Client.Desktop.Fonts;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Rendering.Text;
using Microsoft.Xna.Framework.Graphics;

namespace FHeroes2.Client.Desktop.Scenes;

/// <summary>
/// Milestone M0 screen: exercises the whole pipeline (palette, indexed frame buffer, text) and
/// shows the state of the port. It is replaced by the real main menu in M2.
/// </summary>
public sealed class DiagnosticsScene
{
    private const int LineHeight = 14;
    private const int OverlayLeft = 12;
    private const int OverlayTop = 62;
    private const int PaletteLeft = 10;
    private const int PaletteTop = 8;
    private const int PaletteBlockHeight = 18;
    private const int MarkerWidth = 28;
    private const int MarkerTop = 240;
    private const int BottomHintTop = 452;

    private static readonly RgbaColor LabelColor = new(255, 226, 120);

    private readonly ScreenBuffer _frame;
    private readonly FontStashTextRenderer? _text;
    private readonly bool _supportsCjk;
    private readonly byte _backgroundIndex;
    private readonly byte _highlightIndex;

    private double _elapsedSeconds;

    public DiagnosticsScene(ScreenBuffer frame, FontStashTextRenderer? text, bool supportsCjk)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _frame = frame;
        _text = text;
        _supportsCjk = supportsCjk;
        _backgroundIndex = FindPaletteIndex(frame.Palette, brightest: false);
        _highlightIndex = FindPaletteIndex(frame.Palette, brightest: true);
    }

    public void Update(double elapsedSeconds)
    {
        if (elapsedSeconds > 0)
        {
            _elapsedSeconds += elapsedSeconds;
        }
    }

    /// <summary>Draws the indexed content of the scene into the frame buffer.</summary>
    public void DrawFrame()
    {
        PixelBuffer pixels = _frame.Pixels;

        // Background and a two row palette strip: proof that the palette reached the screen.
        ImageOps.FillRect(pixels, _frame.Bounds, _backgroundIndex);

        for (int index = 0; index < Palette.ColorCount; ++index)
        {
            int x = PaletteLeft + ((index % 128) * 4);
            int y = PaletteTop + ((index / 128) * (PaletteBlockHeight + 4));
            ImageOps.FillRect(pixels, new Rect(x, y, 4, PaletteBlockHeight), (byte)index);
        }

        ImageOps.DrawRect(
            pixels,
            new Rect(PaletteLeft - 2, PaletteTop - 2, (128 * 4) + 3, (PaletteBlockHeight * 2) + 10),
            _highlightIndex);

        // A moving marker makes dropped frames and vsync problems visible at a glance.
        int span = _frame.Size.Width - MarkerWidth - 16;
        int markerX = 8 + (int)((_elapsedSeconds * 120.0) % span);
        ImageOps.FillRect(pixels, new Rect(markerX, MarkerTop, MarkerWidth, MarkerWidth), _highlightIndex);
        ImageOps.DrawRect(pixels, new Rect(markerX - 2, MarkerTop - 2, MarkerWidth + 4, MarkerWidth + 4), _backgroundIndex);

        // M0 redraws everything; the dirty rectangle machinery is used from M1 on.
        _frame.InvalidateAll();
    }

    /// <summary>Draws the diagnostic text in logical (640x480) pixel space.</summary>
    public void DrawOverlay(SpriteBatch batch, in DiagnosticsState state)
    {
        if (_text is null)
        {
            return;
        }

        TextStyle label = new(12, LabelColor);
        TextStyle value = new(12, new RgbaColor(232, 232, 232));

        int y = OverlayTop;
        WriteLine(batch, BuildInfo.DisplayName, y, label);
        WriteLine(batch, "参考实现 " + BuildInfo.ReferenceImplementation, y += LineHeight, value);

        string dataLine = state.DataDirectory is null
            ? (_supportsCjk ? "游戏数据: 未找到 (使用灰度调色板)" : "Game data: not found (grayscale palette)")
            : (_supportsCjk ? "游戏数据: " : "Game data: ") + state.DataDirectory;
        WriteLine(batch, dataLine, y += LineHeight, value);

        WriteLine(
            batch,
            $"heroes2.agg: {state.BaseArchiveEntries}   heroes2x.agg: {state.ExpansionArchiveEntries}",
            y += LineHeight,
            value);

        WriteLine(batch, (_supportsCjk ? "文本后端: " : "Text backend: ") + state.TextBackend, y += LineHeight, value);
        WriteLine(
            batch,
            $"640x480 → {state.OutputSize.Width}x{state.OutputSize.Height} (x{state.Scale:0.##})",
            y += LineHeight,
            value);
        WriteLine(
            batch,
            $"FPS {state.FramesPerSecond,6:0.0}   frame {state.FrameCount}",
            y += LineHeight,
            value);

        if (_supportsCjk)
        {
            WriteLine(batch, "按键: ESC 退出 | F11 全屏 | F12 截图", BottomHintTop, label);
        }
        else
        {
            WriteLine(batch, "Keys: ESC quits | F11 fullscreen | F12 screenshot", BottomHintTop, label);
        }
    }

    private static byte FindPaletteIndex(Palette palette, bool brightest)
    {
        ArgumentNullException.ThrowIfNull(palette);

        byte bestIndex = 0;
        int bestScore = brightest ? -1 : int.MaxValue;

        for (int index = 0; index < Palette.ColorCount; ++index)
        {
            RgbaColor color = palette[index];
            int score = (color.R * 299) + (color.G * 587) + (color.B * 114);

            if (brightest ? score > bestScore : score < bestScore)
            {
                bestScore = score;
                bestIndex = (byte)index;
            }
        }

        return bestIndex;
    }

    private void WriteLine(SpriteBatch batch, string text, int y, in TextStyle style) =>
        _text!.Draw(batch, text, new Point(OverlayLeft, y), style);
}
