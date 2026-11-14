using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The font faces the reference builds at start up: <c>game_assets.cpp:736</c>
/// <c>CopyICNWithPalette</c>, <c>game_assets.cpp:3054</c> <c>WHITE_LARGE_FONT</c> and
/// <c>game_assets.cpp:2433</c> <c>generateGradientFont</c>.
/// </summary>
/// <remarks>
/// <para>
/// 归档里只有白色字形（<c>FONT.ICN</c> / <c>SMALFONT.ICN</c>）。参考在
/// <c>game_assets.cpp:2560</c> 用同一张表派生出黄色 <c>YELLOW_FONT</c> / <c>YELLOW_SMALLFONT</c>
/// 与灰色 <c>GRAY_FONT</c> / <c>GRAY_SMALL_FONT</c>，做法是逐帧把像素过一遍
/// <c>PAL::GetPalette( YELLOW_FONT / GRAY_FONT )</c>；大号 <c>WHITE_LARGE_FONT</c>
/// （<c>game_assets.cpp:3054</c>）则是把 <c>FONT.ICN</c> 每帧放大两倍。
/// </para>
/// <para>
/// 两个渐变面走另一条路：每帧先放大 6 像素留出描边位置，再刷一层纵向渐变，最后叠三圈
/// <see cref="ImageOps.CreateContour"/>（内描边、黑、外描边）。
/// </para>
/// <para>
/// 参考是启动时一次性生成、存进 <c>_icnVsSprite</c>；这里是按（归档包, 字号, 颜色）惰性生成整表并存下来，
/// 语义相同：<see cref="Assets.Current"/> 一换，键就变了，旧表自然不再命中。
/// </para>
/// <para>
/// 未搬：<c>BUTTON_*_FONT</c>（<c>generateButtonAlphabet</c> 手工画字形）、小号字体的 CRC 平移修补、
/// CP1251 帧重排、法语资材的字符还原，以及 <c>ui_language</c> 的字符替换。
/// </para>
/// </remarks>
internal static class FontGlyphTable
{
    private static readonly object SyncRoot = new();

    /// <summary>Whole glyph tables, keyed by the bundle they were built from.</summary>
    private static readonly Dictionary<(AssetBundle? Bundle, FontSize Size, FontColor Color), Sprite?[]> Tables = new();

    /// <summary>Repaired white glyph tables, the base every other face of a size is derived from.</summary>
    private static readonly Dictionary<(AssetBundle? Bundle, FontSize Size), Sprite?[]> BaseTables = new();

    /// <summary>The four button faces, drawn on first use; no archive entry backs them.</summary>
    private static ButtonFont.Faces? ButtonFaces;

    /// <summary>One frame of a generated face; <see langword="null"/> when the frame is absent.</summary>
    /// <param name="size">Glyph size.</param>
    /// <param name="color">Glyph colour.</param>
    /// <param name="frame">Zero based frame number inside the archive ICN.</param>
    /// <returns>The converted glyph, or <see langword="null"/>.</returns>
    public static Sprite? Get(FontSize size, FontColor color, int frame)
    {
        Sprite?[] table = GetTable(size, color);

        return frame >= 0 && frame < table.Length ? table[frame] : null;
    }

    /// <summary>Archive entry the face of <paramref name="size"/> is derived from.</summary>
    private static string GetFileName(FontSize size) => size switch
    {
        FontSize.Small => "SMALFONT.ICN",

        // The large face is the normal one enlarged twice.
        FontSize.Normal or FontSize.Large => "FONT.ICN",

        _ => throw new NotSupportedException($"Font size {size} is not backed by an archive entry."),
    };

    /// <summary>Palette the face of <paramref name="color"/> is converted with.</summary>
    /// <remarks>
    /// 参考 <c>ui_text.cpp:61</c> 的 <c>getChar</c> 里，大号只有白色、金色渐变、银色渐变三种，
    /// 没有黄色和灰色大号。渐变面不走调色板，见 <see cref="GetGradientColors"/>。
    /// </remarks>
    private static PaletteId GetPaletteId(FontSize size, FontColor color) => (size, color) switch
    {
        (_, FontColor.White) => PaletteId.Standard,
        (FontSize.Large, FontColor.Yellow or FontColor.Gray) => throw new NotSupportedException(
            $"The reference has no {color} large font."),
        (_, FontColor.Yellow) => PaletteId.YellowFont,
        (_, FontColor.Gray) => PaletteId.GrayFont,
        _ => throw new NotSupportedException(
            $"Font colour {color} is generated at start up; that generator is not ported yet."),
    };

    /// <summary>Colours of a gradient face, the last four arguments of <c>generateGradientFont</c>.</summary>
    /// <param name="Inner">Colour at the vertical centre of a glyph.</param>
    /// <param name="Outer">Colour at the top and the bottom of a glyph.</param>
    /// <param name="ContourInner">Colour of the outline drawn right around the glyph.</param>
    /// <param name="ContourOuter">Colour of the outermost outline.</param>
    private readonly record struct GradientColors(byte Inner, byte Outer, byte ContourInner, byte ContourOuter);

    /// <summary>
    /// Gradient parameters of a face, or <see langword="null"/> when the face is not a gradient,
    /// the arguments of <c>generateGradientFont</c> in <c>game_assets.cpp:2572</c>.
    /// </summary>
    private static GradientColors? GetGradientColors(FontSize size, FontColor color) => (size, color) switch
    {
        (FontSize.Normal, FontColor.GoldenGradient) => new GradientColors(108, 133, 55, 62),
        (FontSize.Normal, FontColor.SilverGradient) => new GradientColors(10, 31, 29, 0),

        // The large faces grow from `WHITE_LARGE_FONT`, so the ramp is slightly shorter.
        (FontSize.Large, FontColor.GoldenGradient) => new GradientColors(108, 127, 55, 62),
        (FontSize.Large, FontColor.SilverGradient) => new GradientColors(10, 26, 29, 0),

        (_, FontColor.GoldenGradient or FontColor.SilverGradient) => throw new NotSupportedException(
            $"The reference has no gradient {size} font."),
        _ => null,
    };

    private static Sprite?[] GetTable(FontSize size, FontColor color)
    {
        if (size is FontSize.ButtonReleased or FontSize.ButtonPressed)
        {
            return GetButtonTable(size, color);
        }

        var key = (Assets.Current, size, color);

        lock (SyncRoot)
        {
            if (Tables.TryGetValue(key, out Sprite?[]? cached))
            {
                return cached;
            }

            Sprite?[] built = Build(size, color);
            Tables[key] = built;

            return built;
        }
    }

    private static Sprite?[] Build(FontSize size, FontColor color)
    {
        Sprite?[] baseTable = GetBaseTable(size);
        GradientColors? gradient = GetGradientColors(size, color);
        PaletteId paletteId = gradient is null ? GetPaletteId(size, color) : PaletteId.Standard;
        bool recolor = paletteId != PaletteId.Standard;

        if (gradient is null && !recolor)
        {
            // 白色面就是修补后的基础表本身。
            return baseTable;
        }

        ReadOnlySpan<byte> palette = recolor ? PaletteTable.Get(paletteId) : default;
        Sprite?[] table = new Sprite?[baseTable.Length];

        for (int index = 0; index < baseTable.Length; ++index)
        {
            if (baseTable[index] is not { } glyph)
            {
                continue;
            }

            if (gradient is { } colors)
            {
                table[index] = CreateGradientGlyph(glyph, colors);

                continue;
            }

            Sprite copy = glyph.Clone();

            if (recolor)
            {
                ImageOps.ApplyPalette(copy.Image, palette);
            }

            table[index] = copy;
        }

        return table;
    }

    /// <summary>
    /// Faces of the button font, <c>generateButtonAlphabet</c> (<c>ui_font.cpp:6808</c>). They are drawn
    /// from scratch, so they depend on no archive and are generated once per process.
    /// </summary>
    /// <exception cref="NotSupportedException">The reference has no such button colour.</exception>
    private static Sprite?[] GetButtonTable(FontSize size, FontColor color)
    {
        ButtonFont.Faces faces;

        lock (SyncRoot)
        {
            ButtonFaces ??= ButtonFont.Create();
            faces = ButtonFaces.Value;
        }

        return (size, color) switch
        {
            (FontSize.ButtonReleased, FontColor.White) => faces.WhiteReleased,
            (FontSize.ButtonPressed, FontColor.White) => faces.WhitePressed,
            (FontSize.ButtonReleased, FontColor.Gray) => faces.GrayReleased,
            (FontSize.ButtonPressed, FontColor.Gray) => faces.GrayPressed,
            _ => throw new NotSupportedException($"The reference has no {color} button font."),
        };
    }

    /// <summary>
    /// The white glyphs of <paramref name="size"/> after the start up repairs, the equivalent of the
    /// reference's <c>ICN::SMALFONT</c> / <c>ICN::FONT</c> / <c>ICN::WHITE_LARGE_FONT</c> tables
    /// (<c>game_assets.cpp:2455</c>, <c>3068</c>).
    /// </summary>
    /// <remarks>
    /// 修补（<see cref="FontRepairs"/>）只做一次，黄/灰/渐变面都从这份结果派生；大号是普通号的逐帧放大。
    /// 修补时逐帧克隆过，所以帧之间不再共享对象，派生面不必再按引用去重。
    /// </remarks>
    private static Sprite?[] GetBaseTable(FontSize size)
    {
        var key = (Assets.Current, size);

        lock (SyncRoot)
        {
            if (BaseTables.TryGetValue(key, out Sprite?[]? cached))
            {
                return cached;
            }

            Sprite?[] built;

            if (size == FontSize.Large)
            {
                Sprite?[] normal = GetBaseTable(FontSize.Normal);
                built = new Sprite?[normal.Length];

                for (int index = 0; index < normal.Length; ++index)
                {
                    built[index] = normal[index] is { } glyph ? Enlarge(glyph) : null;
                }
            }
            else
            {
                string fileName = GetFileName(size);
                uint fingerprint = FontAssetFixes.GetFingerprint(fileName);
                int count = Assets.GetImageCount(fileName);
                Sprite?[] frames = new Sprite?[count];

                for (int index = 0; index < count; ++index)
                {
                    frames[index] = Assets.GetImage(fileName, index);
                }

                if (size == FontSize.Small)
                {
                    // 波兰 GoG 版的小号字体整体低了一像素，得在修补（它会重算阴影）之前挪回去。
                    if (FontAssetFixes.IsPolishSmallFont(fingerprint))
                    {
                        frames = FontAssetFixes.MoveUp(frames);
                    }
                }
                else
                {
                    // 波兰资材的 '*' 是个点，换回英文资材的那张。
                    FontAssetFixes.RestoreAsterisk(fileName, frames);
                }

                built = FontRepairs.Apply(size, frames, ResourceLanguage.Get());

                // 译版资材的帧序和 ASCII 对不上，重排一步放在修补之后、派生之前。
                built = FontAssetFixes.Arrange(built, fingerprint, size);
            }

            BaseTables[key] = built;

            return built;
        }
    }

    /// <summary>
    /// Highest character the face of <paramref name="size"/> covers, <c>fheroes2::getCharacterLimit</c>
    /// (<c>ui_text.cpp:143</c>).
    /// </summary>
    /// <remarks>
    /// 参考返回的是数组长度：位图字体加载时前面补了 32 个空帧（<c>game_assets.cpp:2555</c>），所以长度 =
    /// 32 + 帧数；译版资材重排后帧数会变，所以必须问已经排好的表。按钮字体没有前置空帧，长度就是 128。
    /// </remarks>
    /// <param name="size">Font size.</param>
    /// <returns>The highest character code the font covers, a space included.</returns>
    public static uint GetCharacterLimit(FontSize size) =>
        size is FontSize.ButtonReleased or FontSize.ButtonPressed
            ? (uint)ButtonFont.AsciiCharSetSize
            : FontCharHandler.GlyphFrameOffset + (uint)GetBaseTable(size).Length;

    /// <summary>Draws <paramref name="glyph"/> at twice its size, the way <c>WHITE_LARGE_FONT</c> is grown.</summary>
    /// <remarks>
    /// 参考（<c>game_assets.cpp:3068</c>）逐帧 <c>out.resize( w * 2, h * 2 )</c> + <c>Resize</c>，
    /// 再把位置设成 <c>in.x() * 2, in.y() * 2</c>。
    /// 参考在这里会按画面缩放设置选 <c>SubpixelResize</c>（平滑）或 <c>Resize</c>（最近邻）；
    /// C# 侧没有那个设置，画面本身也一律最近邻采样，所以固定用 <see cref="ImageOps.Resize(Sprite,Sprite)"/>。
    /// </remarks>
    private static Sprite Enlarge(Sprite glyph)
    {
        Sprite scaled = new(glyph.Width * 2, glyph.Height * 2, glyph.OffsetX * 2, glyph.OffsetY * 2);
        ImageOps.Resize(glyph, scaled);

        return scaled;
    }

    /// <summary>Builds one gradient frame, the body of <c>generateGradientFont</c> (<c>game_assets.cpp:2433</c>).</summary>
    /// <remarks>
    /// 参考留出 3 像素的边给两圈描边（<c>out.resize( w + 6, h + 6 )</c>），把字形贴在 <c>( 3, 3 )</c>，
    /// 位置再左移上移 2 像素补偿那圈边框；渐变只作用在字形本体上，描边是之后另画的三圈。
    /// </remarks>
    private static Sprite CreateGradientGlyph(Sprite glyph, GradientColors colors)
    {
        Sprite output = new(glyph.Width + 6, glyph.Height + 6, glyph.OffsetX - 2, glyph.OffsetY - 2);
        output.Reset();

        ImageOps.Copy(glyph, glyph.Image.Bounds, output, new Point(3, 3));

        ApplyVerticalGradient(output, colors.Inner, colors.Outer);

        // 顺序不能换：后画的一圈盖掉先画的一圈，最外面那圈才留得下来。
        ImageOps.BlitSprite(output, ImageOps.CreateContour(output, colors.ContourInner));
        ImageOps.BlitSprite(output, ImageOps.CreateContour(output, 0));
        ImageOps.BlitSprite(output, ImageOps.CreateContour(output, colors.ContourOuter));

        return output;
    }

    /// <summary>
    /// Paints <paramref name="glyph"/> with a vertical ramp, <c>applyFontVerticalGradient</c> of
    /// <c>ui_font.cpp:7151</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 颜色只取决于行：行离竖直中线的距离越大，越接近 <paramref name="outsideColor"/>，
    /// 中线两侧对称，中线取 <c>max( 1, height / 2 - height % 2 )</c>。
    /// </para>
    /// <para>
    /// 只处理可见像素（transform <c>0</c>）；其中索引小于 21 的是字身，染成渐变色的当前行；
    /// 大等于 21 的是白色基础字形自带的阴影，直接改成透明（transform <c>1</c>）——渐变面不要那层灰边，
    /// 它的立体感来自后面画的描边。
    /// </para>
    /// </remarks>
    private static void ApplyVerticalGradient(Sprite glyph, byte insideColor, byte outsideColor)
    {
        if (glyph.Transform is not { } transform)
        {
            throw new ArgumentException("A vertical gradient needs the transform layer.", nameof(glyph));
        }

        int width = glyph.Width;
        int height = glyph.Height;

        if (width < 2 || height < 2)
        {
            return;
        }

        byte[] image = glyph.Image.Pixels;
        int centerY = Math.Max(1, (height / 2) - (height % 2));

        // 参考用 uint8 减法，外色比内色小时会绕回一个大数；照抄以免台阶方向反了。
        byte delta = unchecked((byte)(outsideColor - insideColor));

        for (int row = 0; row < height; ++row)
        {
            int heightScale = (delta * Math.Abs(centerY - row)) / centerY;
            byte color = unchecked((byte)Math.Abs(insideColor + heightScale));
            int rowStart = row * width;

            for (int x = 0; x < width; ++x)
            {
                int index = rowStart + x;

                if (transform[index] != 0)
                {
                    continue;
                }

                if (image[index] < 21)
                {
                    image[index] = color;
                }
                else
                {
                    transform[index] = 1;
                }
            }
        }
    }
}
