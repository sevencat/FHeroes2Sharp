using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The button fonts, the equivalent of <c>generateButtonAlphabet</c> (<c>ui_font.cpp:6808</c>) and
/// <c>updateButtonFont</c> (<c>ui_font.cpp:170</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这四个面（好/坏 × 按下/未按下）不在归档里，是从 <c>generateGoodButtonFontBaseShape</c> 那张白纸画出来再
/// 各自加工：未按下加两层右下阴影和一圈描边，按下把颜色换深、阴影翻到左上，坏字面只是把绿换成红。
/// 生成一次就够，和归档包无关，所以 <see cref="FontGlyphTable"/> 只缓存一份。
/// </para>
/// <para>
/// 参考的 <c>generateButtonAlphabet</c> 先按代码页补非 ASCII 字形再加工四份；这里只做 ASCII 那份，
/// 所以 <see cref="AsciiCharSetSize"/> 之后没有第二个面需要的字母。
/// </para>
/// </remarks>
internal static partial class ButtonFont
{
    /// <summary>Frames of a button face: the whole ASCII range, indexed by the code itself.</summary>
    public const int AsciiCharSetSize = 128;

    /// <summary>Room left around every letter for the shadows and the contour.</summary>
    private const int Offset = 2;

    /// <summary><c>buttonFontOffset</c> (<c>ui_font.cpp:46</c>).</summary>
    private static readonly Point FontOffset = new(-1, 0);

    /// <summary>Letter colour of the good, not pressed button.</summary>
    private const byte GoodReleasedColour = 56;

    /// <summary>Letter colour of the good, pressed button.</summary>
    private const byte GoodPressedColour = 62;

    /// <summary>Letter colour of the evil, not pressed button.</summary>
    private const byte EvilReleasedColour = 30;

    /// <summary>Letter colour of the evil, pressed button.</summary>
    private const byte EvilPressedColour = 36;

    /// <summary>Contour drawn around the good button letters.</summary>
    private const byte ContourColour = 10;

    /// <summary>The four faces, one per <see cref="FontSize"/> and <see cref="FontColor"/> combination.</summary>
    public readonly record struct Faces(Sprite?[] WhiteReleased, Sprite?[] WhitePressed, Sprite?[] GrayReleased, Sprite?[] GrayPressed);

    /// <summary>
    /// Draws the base shape once and derives the four faces from it, <c>generateButtonAlphabet</c> plus
    /// <c>updateButtonFont</c>.
    /// </summary>
    public static Faces Create()
    {
        Sprite[] baseShape = CreateBaseShape();
        Sprite?[] whiteReleased = new Sprite?[baseShape.Length];
        Sprite?[] whitePressed = new Sprite?[baseShape.Length];
        Sprite?[] grayReleased = new Sprite?[baseShape.Length];
        Sprite?[] grayPressed = new Sprite?[baseShape.Length];

        for (int index = 0; index < baseShape.Length; ++index)
        {
            // 参考先把白面按下复制成 goodPressed，再分别加效果；这里从同一张底稿派生，结果一样。
            Sprite released = ApplyGoodReleasedEffects(baseShape[index].Clone());
            Sprite pressed = ApplyGoodPressedEffects(baseShape[index].Clone());

            whiteReleased[index] = released;
            whitePressed[index] = pressed;

            // 参考在 good 面加完效果之后才复制出 evil 面，所以坏字面带着好字面那圈描边。
            grayReleased[index] = ApplyEvilReleasedEffects(released.Clone());
            grayPressed[index] = ApplyEvilPressedEffects(pressed.Clone());
        }

        return new Faces(whiteReleased, whitePressed, grayReleased, grayPressed);
    }

    /// <summary><c>applyGoodButtonReleasedLetterEffects</c> (<c>ui_font.cpp:119</c>).</summary>
    private static Sprite ApplyGoodReleasedEffects(Sprite letter)
    {
        ImageOps.UpdateShadow(letter, new Point(1, -1), 2, true);
        ImageOps.UpdateShadow(letter, new Point(2, -2), 4, true);

        Sprite contoured = AddContour(letter, new Point(-1, 1), ContourColour);
        ImageOps.UpdateShadow(contoured, new Point(-1, 1), 7, true);

        return contoured;
    }

    /// <summary><c>applyGoodButtonPressedLetterEffects</c> (<c>ui_font.cpp:127</c>).</summary>
    private static Sprite ApplyGoodPressedEffects(Sprite letter)
    {
        ImageOps.ReplaceColorId(letter, GoodReleasedColour, GoodPressedColour);

        ImageOps.UpdateShadow(letter, new Point(1, -1), 2, true);
        ImageOps.UpdateShadow(letter, new Point(-1, 1), 7, true);
        ImageOps.UpdateShadow(letter, new Point(-2, 2), 8, true);

        return letter;
    }

    /// <summary><c>applyEvilButtonReleasedLetterEffects</c> (<c>ui_font.cpp:136</c>).</summary>
    private static Sprite ApplyEvilReleasedEffects(Sprite letter)
    {
        ImageOps.ReplaceColorId(letter, GoodReleasedColour, EvilReleasedColour);

        return letter;
    }

    /// <summary><c>applyEvilButtonPressedLetterEffects</c> (<c>ui_font.cpp:141</c>).</summary>
    private static Sprite ApplyEvilPressedEffects(Sprite letter)
    {
        ImageOps.ReplaceColorId(letter, GoodPressedColour, EvilPressedColour);

        return letter;
    }

    /// <summary>
    /// The anonymous namespace <c>addContour</c> of <c>ui_font.cpp:96</c>: a contour that does not grow the
    /// image, the room for it is already reserved by <see cref="Offset"/>.
    /// </summary>
    /// <remarks>
    /// 参考只擦「离笔画一格」的透明像素，并跳过两个方向都没笔画的斜角，所以描边不会把阴影框住。
    /// 指针算法的等价写法是 <c>input</c> 的下标比 <c>output</c> 少 <c>contourOffset.X + width * contourOffset.Y</c>，
    /// 循环上界保证不会越界（<c>contourOffset</c> 的符号已被守卫限死）。
    /// </remarks>
    private static Sprite AddContour(Sprite input, Point contourOffset, byte colorId)
    {
        if (input.Width <= 0 || input.Height <= 0 || input.Transform is not { } inputTransform
            || contourOffset.X > 0 || contourOffset.Y < 0
            || -contourOffset.X >= input.Width || contourOffset.Y >= input.Height)
        {
            return input;
        }

        Sprite output = input.Clone();
        byte[] outputPixels = output.Image.Pixels;
        byte[] outputTransform = output.Transform!;

        int imageWidth = output.Width;
        int width = imageWidth + contourOffset.X;
        int height = output.Height - contourOffset.Y;
        int rowOffset = imageWidth * contourOffset.Y;

        for (int y = 0; y < height; ++y)
        {
            int outputRow = y * imageWidth + rowOffset;
            int inputRow = y * imageWidth - contourOffset.X;

            for (int x = 0; x < width; ++x)
            {
                int outputIndex = outputRow + x;

                if (outputTransform[outputIndex] != 1)
                {
                    continue;
                }

                int inputIndex = inputRow + x;
                int horizontalNeighbour = inputIndex + contourOffset.X;
                int verticalNeighbour = inputIndex + rowOffset;

                if (inputTransform[inputIndex] == 0
                    || (inputTransform[horizontalNeighbour] == 0 && inputTransform[verticalNeighbour] == 0))
                {
                    outputPixels[outputIndex] = colorId;
                    outputTransform[outputIndex] = 0;
                }
            }
        }

        return output;
    }

    /// <summary>
    /// <c>resize()</c> followed by <c>reset()</c>: a fresh frame that keeps the anchor of the one it replaces.
    /// </summary>
    /// <remarks>
    /// C# 的 <see cref="Sprite"/> 尺寸不可变，所以参考里「就地放大再重置」的两步在这里合成一个新建。
    /// 注意构造函数不会把变换层置 1，必须 <see cref="Sprite.Reset"/>，否则整张图都是可见的黑色。
    /// </remarks>
    private static Sprite Create(int width, int height, Sprite previous)
    {
        Sprite letter = new(width, height, previous.OffsetX, previous.OffsetY);
        letter.Reset();

        return letter;
    }

    /// <summary>Same content, different anchor, the reference's <c>setPosition</c>.</summary>
    private static Sprite Moved(Sprite letter, int offsetX, int offsetY) =>
        new(letter.Image, offsetX, offsetY, letter.Transform);
}
