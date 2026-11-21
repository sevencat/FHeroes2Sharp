using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The <c>EMPTY_*_BUTTON</c> tables of <c>generateDefaultImages</c>
/// (<c>src/fheroes2/game/game_assets.cpp:4852-4976</c>). They are not archive entries: every one of them
/// is assembled at run time out of the frames of another ICN, which is why the reference loads them
/// through <c>loadICN</c> just like a real file.
/// </summary>
/// <remarks>
/// <para>
/// 项目里这些「文件」只有一个名字、没有归档条目：<see cref="Assets.GetImage"/> 在查归档之前先问
/// <see cref="Build"/>，命中就用现场拼出来的两帧（释放态、按下态）。
/// </para>
/// <para>
/// 未搬：<c>EMPTY_VERTICAL_GOOD_BUTTON</c>（<c>HSBTNS</c> 帧 + <c>ExtractCommonPattern</c>）和
/// <c>EMPTY_MAP_SELECT_BUTTON</c>（<c>NGEXTRA</c> 帧）；用到它们的高分榜与地图选择窗口还没移植，
/// 等搬到那两个窗口时再一起补。
/// </para>
/// </remarks>
public static class EmptyButtons
{
    /// <summary>The name <c>EMPTY_GOOD_BUTTON</c> is cached under, the reference's <c>ICN::EMPTY_GOOD_BUTTON</c>.</summary>
    public const string GoodFileName = "EMPTY_GOOD_BUTTON.ICN";

    /// <summary>The name <c>EMPTY_EVIL_BUTTON</c> is cached under.</summary>
    public const string EvilFileName = "EMPTY_EVIL_BUTTON.ICN";

    /// <summary>The name <c>EMPTY_POL_BUTTON</c> is cached under.</summary>
    public const string PolFileName = "EMPTY_POL_BUTTON.ICN";

    /// <summary>The name <c>EMPTY_GUILDWELL_BUTTON</c> is cached under.</summary>
    public const string GuildWellFileName = "EMPTY_GUILDWELL_BUTTON.ICN";

    /// <summary>Archive entry holding the raw game palette the button colours are resolved against.</summary>
    private const string GamePaletteName = "KB.PAL";

    /// <summary>Size of a raw 256 colour palette in bytes, <c>fheroes2::Palette::sizeInBytes</c>.</summary>
    private const int RawPaletteSizeBytes = 768;

    /// <summary>Released frame of <c>SYSTEM</c> / <c>SYSTEME</c>, <c>_icnVsSprite[originalId][11]</c>.</summary>
    private const int ReleasedFrame = 11;

    /// <summary>Pressed frame of <c>SYSTEM</c> / <c>SYSTEME</c>, <c>Assets::getImage( originalId, 12 )</c>.</summary>
    private const int PressedFrame = 12;

    /// <summary>Released frame of <c>X_CMPBTN</c>.</summary>
    private const int PolReleasedFrame = 4;

    /// <summary>Pressed frame of <c>X_CMPBTN</c>.</summary>
    private const int PolPressedFrame = 5;

    /// <summary>Width of one <c>X_CMPBTN</c> frame, used for the pixel the button colour is read from.</summary>
    private const int PolFrameWidth = 94;

    /// <summary>
    /// Builds the two frames of a synthetic button table.
    /// </summary>
    /// <param name="fileName">Asset name, one of the constants of this class.</param>
    /// <returns>
    /// The released frame followed by the pressed one, or <see langword="null"/> when
    /// <paramref name="fileName"/> is not a synthetic table or the archive is too short for it.
    /// </returns>
    /// <remarks>
    /// 参考在每个 <c>case</c> 里用「帧数不够就 break」跳过，此时 <c>_icnVsSprite[id]</c> 留空，之后 <c>getImage</c>
    /// 返回 <c>errorImage</c>；这里返回 <see langword="null"/>，效果是本项目一贯的「缺失」。
    /// </remarks>
    public static Sprite?[]? Build(string fileName)
    {
        if (string.Equals(fileName, GoodFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildSystemButton(isGoodInterface: true);
        }

        if (string.Equals(fileName, EvilFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildSystemButton(isGoodInterface: false);
        }

        if (string.Equals(fileName, PolFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildPolButton();
        }

        if (string.Equals(fileName, GuildWellFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildGuildWellButton();
        }

        return null;
    }

    /// <summary>
    /// The colour an empty button is filled with, <c>getButtonFillingColor()</c>
    /// (<c>game_assets.cpp:525</c>).
    /// </summary>
    /// <param name="isReleasedState">Whether the released state is asked for.</param>
    /// <param name="isGoodInterface">Whether the button belongs to the good (light) interface.</param>
    /// <returns>The palette index of the button background.</returns>
    public static byte GetButtonFillingColor(bool isReleasedState, bool isGoodInterface = true)
    {
        if (isGoodInterface)
        {
            return isReleasedState ? GetColorId(216, 184, 152) : GetColorId(184, 136, 96);
        }

        return isReleasedState ? GetColorId(180, 180, 180) : GetColorId(144, 144, 144);
    }

    /// <summary>
    /// The palette index closest to an RGB colour, <c>fheroes2::GetColorId()</c>
    /// (<c>src/engine/image.cpp:419</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考预先把 <c>64×64×64</c> 种颜色算成一张索引表（<c>generateColorConversionTable</c>，
    /// <c>image.cpp:355</c>），表里只保留「不会变色循环的、互不重复的」227 个调色板位置，距离用
    /// Redmean 公式，最后再用 15 号变换表把那几个仍然会循环的颜色换成它们的静态副本。这里不建表，
    /// 只在问到某个颜色时把那 227 个位置走一遍——调用方只有按钮底色这四种颜色，建表纯属浪费。
    /// </para>
    /// <para>
    /// 输入是高六位（参考也是 <c>GetColorId( 216, 184, 152 )</c> 这样传八位再右移两位比较），
    /// 调色板本身是 <c>KB.PAL</c> 里的原始六位值，两端同一量纲。
    /// </para>
    /// </remarks>
    /// <param name="red">Red component, 0 to 255.</param>
    /// <param name="green">Green component, 0 to 255.</param>
    /// <param name="blue">Blue component, 0 to 255.</param>
    /// <returns>The palette index to use.</returns>
    /// <exception cref="InvalidOperationException">The archive has no usable <c>KB.PAL</c>.</exception>
    public static byte GetColorId(byte red, byte green, byte blue)
    {
        byte[]? data = Assets.TryReadRaw(GamePaletteName);
        if (data is null || data.Length < RawPaletteSizeBytes)
        {
            throw new InvalidOperationException(
                $"'{GamePaletteName}' is required to resolve an RGB colour to a palette index.");
        }

        byte[] palette = data;

        // 参考: red / 4, green / 4, blue / 4（三色都只在低六位上比较）。
        int r = red >> 2;
        int g = green >> 2;
        int b = blue >> 2;

        int minDistance = int.MaxValue;
        byte bestPosition = 0;

        foreach (byte position in NonCyclingUniqueColorPositions)
        {
            int index = position * 3;
            int paletteRed = palette[index];
            int paletteGreen = palette[index + 1];
            int paletteBlue = palette[index + 2];

            int sumRed = paletteRed + r;
            int offsetRed = paletteRed - r;
            int offsetGreen = paletteGreen - g;
            int offsetBlue = paletteBlue - b;

            // "Redmean" 色差（https://www.compuphase.com/cmetric.htm），与参考逐字一致。
            int distance = ((2 * 2 * 256) + sumRed) * offsetRed * offsetRed
                           + (4 * 2 * 256 * offsetGreen * offsetGreen)
                           + ((2 * ((2 * 256) + 255)) - sumRed) * offsetBlue * offsetBlue;

            if (minDistance > distance)
            {
                minDistance = distance;
                bestPosition = position;

                if (distance == 0)
                {
                    break;
                }
            }
        }

        return PaletteTransformTable.Apply(15, bestPosition);
    }

    /// <summary>
    /// The 227 palette positions the colour table of the reference searches: it skips the first and the
    /// last ten entries of the original palette (undefined), the repeated colours and the cycling sources,
    /// which it links to their static copies at the end of the list.
    /// </summary>
    private static readonly byte[] NonCyclingUniqueColorPositions = BuildNonCyclingPositions();

    private static byte[] BuildNonCyclingPositions()
    {
        List<byte> positions = new(227);

        for (int index = 10; index <= 213; ++index)
        {
            positions.Add((byte)index);
        }

        // 222..230, 236..237, 242..245 是不重复的固定颜色；231..233 / 235 / 214..217 是循环色的源，
        // 参考把它们放在末尾，距离相等时后面的位置不会覆盖前面的（严格小于才替换），和参考一致。
        positions.AddRange(new byte[]
        {
            222, 223, 224, 225, 226, 227, 228, 229, 230, 236, 237, 242, 243, 244, 245, 231, 232, 233, 235,
            214, 215, 216, 217,
        });

        return positions.ToArray();
    }

    /// <summary>
    /// <c>ICN::EMPTY_GOOD_BUTTON</c> / <c>ICN::EMPTY_EVIL_BUTTON</c>: the <c>SYSTEM</c> (or <c>SYSTEME</c>)
    /// frames 11 and 12, with the pressed state's corners made transparent and one pixel of the released
    /// state's border copied onto its left side (<c>game_assets.cpp:4852-4897</c>).
    /// </summary>
    private static Sprite?[]? BuildSystemButton(bool isGoodInterface)
    {
        string originalName = IcnCatalog.GetFileName(isGoodInterface ? IcnType.System : IcnType.Systeme);

        if (Assets.GetImageCount(originalName) < PressedFrame + 1)
        {
            return null;
        }

        if (Assets.GetImage(originalName, ReleasedFrame) is not { } releasedSource ||
            Assets.GetImage(originalName, PressedFrame) is not { } pressedSource)
        {
            return null;
        }

        Sprite released = releasedSource.Clone();
        ImageOps.FillSprite(released, new Rect(8, 7, 1, 1), GetButtonFillingColor(true, isGoodInterface));

        // 参考的 `if` 只用守卫，退化帧就保留一个空精灵；这里当作资材损坏（见 Build 的 remark）。
        if (pressedSource.Width <= 2 || pressedSource.Height <= 2)
        {
            return null;
        }

        Sprite pressed = new(pressedSource.Width, pressedSource.Height);

        // The original pressed button misses the darker left border of the released state, so it is taken from there.
        ImageOps.Copy(released, new Rect(0, 0, 1, released.Height), pressed, Point.Zero);
        ImageOps.Copy(pressedSource, new Rect(0, 0, pressedSource.Width - 1, pressedSource.Height), pressed, new Point(1, 0));

        // Make the background transparent: the top row, the right column and the two corner steps.
        ImageOps.FillTransform(pressed, new Rect(1, 0, pressed.Width - 1, 1), 1);
        ImageOps.FillTransform(pressed, new Rect(pressed.Width - 1, 1, 1, pressed.Height - 1), 1);

        ImageOps.FillTransform(pressed, new Rect(1, 1, 2, 1), 1);
        ImageOps.FillTransform(pressed, new Rect(1, 2, 1, 1), 1);

        ImageOps.FillTransform(pressed, new Rect(pressed.Width - 3, 1, 2, 1), 1);
        ImageOps.FillTransform(pressed, new Rect(pressed.Width - 2, 2, 1, 1), 1);

        ImageOps.FillTransform(pressed, new Rect(pressed.Width - 4, pressed.Height - 1, 3, 1), 1);
        ImageOps.FillTransform(pressed, new Rect(pressed.Width - 3, pressed.Height - 2, 2, 1), 1);
        ImageOps.FillTransform(pressed, new Rect(pressed.Width - 2, pressed.Height - 3, 1, 1), 1);

        ImageOps.FillSprite(pressed, new Rect(90, 5, 1, 1), GetButtonFillingColor(false, isGoodInterface));

        return new Sprite?[] { released, pressed };
    }

    /// <summary>
    /// <c>ICN::EMPTY_POL_BUTTON</c>: <c>X_CMPBTN</c> frames 4 and 5, widened by one pixel and with the dark
    /// border of the pressed state moved onto the released one (<c>game_assets.cpp:4898-4945</c>).
    /// </summary>
    private static Sprite?[]? BuildPolButton()
    {
        string originalName = IcnCatalog.GetFileName(IcnType.XCmpbtn);

        if (Assets.GetImageCount(originalName) < PolPressedFrame + 3)
        {
            return null;
        }

        if (Assets.GetImage(originalName, PolReleasedFrame) is not { } originalReleased ||
            Assets.GetImage(originalName, PolPressedFrame) is not { } originalPressed)
        {
            return null;
        }

        // 参考这条判断是三个 `&&`：真实资材（94 宽）里恒为假，只有明显不是 X_CMPBTN 的资材才会退出。
        if (originalReleased.Width != PolFrameWidth && originalPressed.Width != PolFrameWidth &&
            originalReleased.Height < 5 && originalPressed.Height < 5)
        {
            return null;
        }

        int width = originalReleased.Width + 2;
        int height = originalReleased.Height + 1;

        // Move the dark border of the original pressed state onto the new released state.
        Sprite released = new(width, height);
        released.Reset();

        ImageOps.Copy(originalReleased, new Rect(0, 0, originalReleased.Width, originalReleased.Height), released, new Point(1, 0));
        ImageOps.Copy(originalReleased, new Rect(0, 2, 1, 1), released, new Point(1, 21));
        ImageOps.Copy(originalReleased, new Rect(0, 2, 1, 1), released, new Point(2, 22));
        ImageOps.Copy(originalPressed, new Rect(0, 2, 1, 19), released, new Point(0, 3));
        ImageOps.Copy(originalPressed, new Rect(0, originalPressed.Height - 1, originalPressed.Width, 1), released, new Point(0, originalPressed.Height));
        ImageOps.Copy(originalPressed, new Rect(0, 2, 1, 1), released, new Point(1, 22));

        // The pressed state, made the same size as the released one: the Czech pressed state is one pixel shorter.
        Sprite pressed = new(width, height);
        pressed.Reset();

        ImageOps.Copy(originalPressed, new Rect(0, 0, originalPressed.Width, originalPressed.Height), pressed, new Point(0, 1));

        // Fills the one pixel vertical gap of the Czech pressed state.
        if (originalPressed.Height < 23)
        {
            ImageOps.Copy(
                originalPressed,
                new Rect(0, originalPressed.Height - 10, originalPressed.Width, 10),
                pressed,
                new Point(0, pressed.Height - 10));
        }

        // The empty buttons are one pixel wider than the originals so that resizeButton() can divide them by three.
        ImageOps.Copy(originalReleased, new Rect(originalReleased.Width - 5, 0, 5, originalReleased.Height), released, new Point(released.Width - 5, 0));
        ImageOps.Copy(originalPressed, new Rect(originalPressed.Width - 5, 0, 5, originalReleased.Height), pressed, new Point(pressed.Width - 6, 1));

        // 参考直接按扁平下标取 4 * 94 + 6 处的像素，不管帧的真实宽度——这里照抄，越界时当作资材损坏。
        int pixelPosition = (4 * PolFrameWidth) + 6;
        if (originalReleased.Image.Pixels.Length <= pixelPosition || originalPressed.Image.Pixels.Length <= pixelPosition)
        {
            return null;
        }

        ImageOps.FillSprite(released, new Rect(5, 3, 88, 18), originalReleased.Image.Pixels[pixelPosition]);
        ImageOps.FillSprite(pressed, new Rect(4, 5, 87, 17), originalPressed.Image.Pixels[pixelPosition]);

        return new Sprite?[] { released, pressed };
    }

    /// <summary>
    /// <c>ICN::EMPTY_GUILDWELL_BUTTON</c>: <c>WELLXTRA</c> frames 0 and 1 with the label erased and the
    /// borders redrawn (<c>game_assets.cpp:4946-4976</c>).
    /// </summary>
    private static Sprite?[]? BuildGuildWellButton()
    {
        string originalName = IcnCatalog.GetFileName(IcnType.Wellxtra);

        if (Assets.GetImageCount(originalName) < 3)
        {
            return null;
        }

        Sprite?[] frames = new Sprite?[2];

        for (int state = 0; state < frames.Length; ++state)
        {
            if (Assets.GetImage(originalName, state) is not { } original || original.Width <= 4)
            {
                return null;
            }

            // One pixel narrower so that when resizeButton() divides it by three the result stays integral.
            Sprite output = new(original.Width - 1, original.Height);

            ImageOps.Copy(original, new Rect(0, 0, original.Width - 4, original.Height), output, Point.Zero);
            ImageOps.Copy(original, new Rect(original.Width - 3, 0, 3, original.Height), output, new Point(original.Width - 4, 0));

            // Extra cleaning because some localized assets (Russian, for one) paint text outside the normal border.
            ImageOps.FillSprite(output, new Rect(3 - state, 2 + state, 55, 14), GetButtonFillingColor(state == 0));

            byte borderColor = state == 0 ? (byte)38 : (byte)41;
            ImageOps.DrawLine(output, new Point(3 - state, 3 + state), new Point(4 - state, 2 + state), borderColor);
            ImageOps.SetPixel(output, 5 - (3 * state), 2 + state, borderColor);
            ImageOps.SetPixel(output, 57 - state, 2 + state, borderColor);
            ImageOps.DrawLine(output, new Point(26, 1 + state), new Point(42, 1 + state), borderColor);

            byte secondBorderColor = state == 0 ? (byte)42 : (byte)46;
            ImageOps.DrawLine(output, new Point(26, 0 + state), new Point(42, 0 + state), secondBorderColor);

            frames[state] = output;
        }

        return frames;
    }
}
