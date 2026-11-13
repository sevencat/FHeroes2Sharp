using FHeroes2.Assets.Fonts;
using FHeroes2.App.Fonts;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Rendering.Text;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The engine diagnostics screen: palette strip, moving marker, font samples and the runtime state
/// overlay. It is still reachable with <c>--scene diagnostics</c> and is the fallback of the client
/// when the game data or the fonts are missing.
/// </summary>
/// <remarks>
/// Besides the M0 diagnostics it draws the game fonts into the indexed frame buffer
/// (<see cref="DrawFontSample"/>), which is the path the main menu uses: <c>FONT.ICN</c> for Latin
/// text, a rasterized CJK face for everything else and a palette for the colour.
/// </remarks>
public sealed class DiagnosticsScene : IScene
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
    private const int FontSampleLeft = 12;
    private const int FontSampleTop = 286;

    /// <summary>The line that has to look identical in both colours.</summary>
    private const string FontSampleLine = "英雄无敌 II  Hero Edition 12345";

    private static readonly RgbaColor LabelColor = new(255, 226, 120);

    private readonly ScreenBuffer _frame;
    private readonly SdlTextRenderer? _text;
    private readonly GameFonts? _fonts;
    private readonly bool _supportsCjk;
    private readonly byte _backgroundIndex;
    private readonly byte _highlightIndex;

    private double _elapsedSeconds;

    public DiagnosticsScene(ScreenBuffer frame, SdlTextRenderer? text, bool supportsCjk, GameFonts? fonts = null)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _frame = frame;
        _text = text;
        _supportsCjk = supportsCjk;
        _fonts = fonts;
        _backgroundIndex = frame.Palette.FindDarkestIndex();
        _highlightIndex = frame.Palette.FindBrightestIndex();
    }

    public void Update(double elapsedSeconds)
    {
        if (elapsedSeconds > 0)
        {
            _elapsedSeconds += elapsedSeconds;
        }
    }

    /// <summary>Draws the indexed content of the scene into the frame buffer.</summary>
    public void DrawFrame(in DiagnosticsState state)
    {
        _ = state;

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

        // The game fonts are drawn into the indexed buffer, so the palette decides their colour.
        DrawFontSample(pixels);

        // M0 redraws everything; the dirty rectangle machinery is used from M1 on.
        _frame.InvalidateAll();
    }

    /// <summary>
    /// Draws the two text providers into the frame buffer: the same mixed Chinese/Latin line once in
    /// the yellow of <c>YELLOW_FONT</c> and once in the grey of the untouched table, then the small
    /// font with the characters the ICN tables cannot express.
    /// </summary>
    private void DrawFontSample(PixelBuffer pixels)
    {
        if (_fonts is null)
        {
            return;
        }

        int y = FontSampleTop;

        GlyphText.DrawLine(_fonts.Small, pixels, new Point(FontSampleLeft, y), "索引文本 (FONT.ICN + TrueType 补位)");
        y += _fonts.Small.LineHeight + 4;

        GlyphText.DrawLine(_fonts.Yellow, pixels, new Point(FontSampleLeft, y), FontSampleLine);
        y += _fonts.Yellow.LineHeight;

        GlyphText.DrawLine(_fonts.Normal, pixels, new Point(FontSampleLeft, y), FontSampleLine);
        y += _fonts.Normal.LineHeight + 4;

        GlyphText.DrawLine(
            _fonts.Small, pixels, new Point(FontSampleLeft, y), "SMALFONT.ICN: 8 px 字形，中英混排 mixed text");
    }

    /// <summary>Draws the diagnostic text in logical (640x480) pixel space.</summary>
    public void DrawOverlay(in DiagnosticsState state)
    {
        if (_text is null)
        {
            return;
        }

        TextStyle label = new(12, LabelColor);
        TextStyle value = new(12, new RgbaColor(232, 232, 232));

        int y = OverlayTop;
        WriteLine(BuildInfo.DisplayName, y, label);
        WriteLine("参考实现 " + BuildInfo.ReferenceImplementation, y += LineHeight, value);

        string dataLine = state.DataDirectory is null
            ? (_supportsCjk ? "游戏数据: 未找到 (使用灰度调色板)" : "Game data: not found (grayscale palette)")
            : (_supportsCjk ? "游戏数据: " : "Game data: ") + state.DataDirectory;
        WriteLine(dataLine, y += LineHeight, value);

        WriteLine(
            $"heroes2.agg: {state.BaseArchiveEntries}   heroes2x.agg: {state.ExpansionArchiveEntries}",
            y += LineHeight,
            value);

        WriteLine((_supportsCjk ? "文本后端: " : "Text backend: ") + state.TextBackend, y += LineHeight, value);
        WriteLine((_supportsCjk ? "游戏字体: " : "Game fonts: ") + state.FontBackend, y += LineHeight, value);
        WriteLine(
            $"640x480 → {state.OutputSize.Width}x{state.OutputSize.Height} (x{state.Scale:0.##})",
            y += LineHeight,
            value);
        WriteLine(
            $"FPS {state.FramesPerSecond,6:0.0}   frame {state.FrameCount}",
            y += LineHeight,
            value);

        if (_supportsCjk)
        {
            WriteLine("按键: ESC 退出 | F11 全屏 | F12 截图", BottomHintTop, label);
        }
        else
        {
            WriteLine("Keys: ESC quits | F11 fullscreen | F12 screenshot", BottomHintTop, label);
        }
    }

    private void WriteLine(string text, int y, in TextStyle style) =>
        _text!.Draw(text, new Point(OverlayLeft, y), style);
}
