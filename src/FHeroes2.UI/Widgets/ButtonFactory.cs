using System.Text;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.H2D;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Builds a button out of an empty plate, the free functions of <c>ui_button.cpp</c>:
/// <c>getCustomNormalButton()</c>, <c>getTextAdaptedSprite()</c>, <c>makeButtonSprites()</c>,
/// <c>makeSymmetricBackgroundSprites()</c>, <c>renderTextOnButton()</c>, <c>getSupportedText()</c>,
/// <c>makeButtonWithBackground()</c> and <c>makeButtonWithShadow()</c>.
/// </summary>
/// <remarks>
/// <para>
/// The labels are measured and drawn with <see cref="Text"/> and the button font, exactly the way the
/// reference does it. The port used to draw them itself through <c>GlyphText</c>, which is why the
/// text of a generated button could drift away from the one of a dialog label.
/// </para>
/// <para>
/// 参考里这些函数都是 <c>ui_button.cpp</c> 的匿名命名空间函数，唯一的外部使用者是
/// <c>ButtonSprite</c> 的构造与 <c>ButtonGroup</c>；端口把它们集中在这里，返回值用元组/精灵对表示。
/// </para>
/// </remarks>
public static class ButtonFactory
{
    /// <summary><c>EMPTY_GOOD_BUTTON</c>, the plate of the good interface.</summary>
    public const string GoodBlankAssetName = EmptyButtons.GoodFileName;

    /// <summary><c>EMPTY_EVIL_BUTTON</c>, the plate of the evil interface.</summary>
    public const string EvilBlankAssetName = EmptyButtons.EvilFileName;

    /// <summary><c>EMPTY_POL_BUTTON</c>, the short plate of the Price of Loyalty theme.</summary>
    public const string PolBlankAssetName = EmptyButtons.PolFileName;

    /// <summary><c>EMPTY_GUILDWELL_BUTTON</c>, the plate of a dwelling list entry.</summary>
    public const string GuildWellBlankAssetName = EmptyButtons.GuildWellFileName;

    /// <summary><c>EMPTY_MAP_SELECT_BUTTON</c>, the plate of the map size buttons.</summary>
    public const string MapSelectBlankAssetName = EmptyButtons.MapSelectFileName;

    /// <summary><c>EMPTY_VERTICAL_GOOD_BUTTON</c>, the plate of a vertical button.</summary>
    public const string VerticalGoodBlankAssetName = EmptyButtons.VerticalGoodFileName;

    /// <summary>Smallest width <c>getCustomNormalButton()</c> stretches a plate to.</summary>
    public const int MinimumWidth = 16;

    /// <summary>Smallest height <c>getCustomNormalButton()</c> stretches a plate to.</summary>
    public const int MinimumHeight = 25;

    /// <summary>Largest size <c>getCustomNormalButton()</c> stretches a plate to.</summary>
    public const int MaximumSize = 200;

    /// <summary>
    /// Room the label of a generated button gets on top of its measured width,
    /// the <c>+ 10</c> of <c>getCustomNormalButton()</c>.
    /// </summary>
    public const int TextWidthMargin = 10;

    /// <summary>
    /// Builds the MAX shortcut of the count window, the <c>ICN::BUTTON_SMALL_MAX_GOOD</c> of
    /// <c>game_assets.cpp:1261-1283</c> (original resources): <c>RECRUIT</c> frames 4/5 cropped to 60×25 with
    /// their corners made transparent.
    /// </summary>
    /// <returns>The two sprites, released first; <see langword="null"/> when the archive lacks the frames.</returns>
    public static (Sprite Released, Sprite Pressed)? MakeSmallMaxButton()
    {
        Sprite? released = CropRecruitButton(4);
        Sprite? pressed = CropRecruitButton(5);

        if (released is null || pressed is null)
        {
            return null;
        }

        return (released, pressed);
    }

    /// <summary>
    /// Builds the MIN shortcut of the count window, the <c>ICN::BUTTON_SMALL_MIN_GOOD</c> of
    /// <c>game_assets.cpp:1284-1294</c>: a <c>MIN</c> labelled button whose text area is the MAX button's width
    /// less ten pixels.
    /// </summary>
    /// <param name="maxButtonWidth">Width of the MAX button, <c>60</c> with the original artwork.</param>
    /// <param name="background">
    /// Texture the transparent corners of the pressed plate are filled with, usually <c>STONEBAK</c>.
    /// </param>
    /// <returns>The two sprites, released first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="background"/> is <see langword="null"/>.</exception>
    public static (Sprite Released, Sprite Pressed) MakeSmallMinButton(int maxButtonWidth, Sprite background)
    {
        ArgumentNullException.ThrowIfNull(background);

        return MakeButtonSprites(
            "MIN", new Size(maxButtonWidth - TextWidthMargin, 25), isEvilInterface: false, background);
    }

    /// <summary>
    /// Crops one of the two <c>RECRUIT</c> frames the MAX button is cut from, <c>Crop( image, 5, 0, 60, 25 )</c>
    /// plus the transparent corners of <c>setButtonCornersTransparent()</c>.
    /// </summary>
    /// <param name="frame">Frame of <c>RECRUIT</c>, 4 for released and 5 for pressed.</param>
    /// <returns>The sprite at (0, 0), or <see langword="null"/> when the archive lacks the frame.</returns>
    private static Sprite? CropRecruitButton(int frame)
    {
        const int width = 60;
        const int height = 25;

        if (GameAssets.GetImage(GameAssets.GetFileName(IcnType.Recruit), frame) is not { } original)
        {
            return null;
        }

        Sprite cropped = ImageOps.Crop(original, 5, 0, width, height);

        if (cropped.Image.IsEmpty)
        {
            return null;
        }

        // Crop keeps the (x, y) it was cut at; the reference calls setPosition( 0, 0 ) because the button is
        // blitted by its absolute position.
        var button = new Sprite(cropped.Image);

        EmptyButtons.MakeButtonCornersTransparent(button);

        return button;
    }

    /// <summary>
    /// Generates the released and pressed look of a button labelled with <paramref name="text"/>,
    /// <c>makeButtonSprites()</c>.
    /// </summary>
    /// <param name="text">Label of the button; <c>\n</c> starts a new line.</param>
    /// <param name="textAreaSize">Area the label is laid out in, before the plate is grown.</param>
    /// <param name="isEvilInterface">Whether the gray plates and lettering are used.</param>
    /// <param name="background">
    /// Texture the transparent corners of the pressed plate are filled with, usually <c>STONEBAK</c>;
    /// <see langword="null"/> leaves them transparent.
    /// </param>
    /// <returns>The two sprites of the button, released first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static (Sprite Released, Sprite Pressed) MakeButtonSprites(
        string text,
        Size textAreaSize,
        bool isEvilInterface,
        Sprite? background = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        (Sprite released, Sprite pressed, Point releasedTextOffset, Point pressedTextOffset) =
            GetCustomNormalButton(textAreaSize, isEvilInterface, background);

        RenderTextOnButton(
            released,
            pressed,
            text,
            releasedTextOffset,
            pressedTextOffset,
            textAreaSize,
            isEvilInterface ? FontColor.Gray : FontColor.White);

        return (released, pressed);
    }

    /// <summary>
    /// Size of the "enable all"/"disable all" buttons, <c>spellSelectionButton</c> of <c>game_assets.cpp</c>.
    /// </summary>
    public static readonly Size ToggleAllButtonSize = new(86, 25);

    /// <summary>
    /// Builds the "enable all"/"disable all" button of the selection windows, the <c>BUTTON_TOGGLE_ALL_*</c>
    /// entries of <c>game_assets.cpp</c> that hold no <c>.ICN</c> of their own: an empty 86x25 plate with the two
    /// arrows of the h2d resources on it.
    /// </summary>
    /// <param name="background">Texture the transparent corners are filled with, usually <c>STONEBAK</c>.</param>
    /// <param name="isEvilInterface">Whether the gray plate is used.</param>
    /// <param name="enableAll">Whether the "enable all" arrows are used instead of the "disable all" ones.</param>
    /// <returns>The two sprites of the button, released first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="background"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// h2d 图标缺席时（没有装 <c>res</c> 那份资源）按钮退化成没有图案的空板子，尺寸与位置都不变。
    /// </remarks>
    public static (Sprite Released, Sprite Pressed) MakeToggleAllButton(Sprite background, bool isEvilInterface,
        bool enableAll)
    {
        ArgumentNullException.ThrowIfNull(background);

        (Sprite released, Sprite pressed) = MakeButtonSprites(string.Empty, ToggleAllButtonSize, isEvilInterface,
            background);

        string name = enableAll ? "toggle-all-on" : "toggle-all-off";

        if (H2DResources.TryReadCurrentImage($"{name}-released.image", out Sprite? icon))
        {
            ImageOps.BlitSprite(
                released.Image,
                new Point((released.Width - icon.Width) / 2, (released.Height - icon.Height) / 2),
                icon);
        }

        if (H2DResources.TryReadCurrentImage($"{name}-pressed.image", out Sprite? pressedIcon))
        {
            ImageOps.BlitSprite(
                pressed.Image,
                new Point((pressed.Width - pressedIcon.Width) / 2, ((pressed.Height - pressedIcon.Height) / 2) + 1),
                pressedIcon);
        }

        return (released, pressed);
    }

    /// <summary>
    /// Stretches the empty plate of <paramref name="textAreaSize"/> and returns the offsets its label
    /// has to be drawn at, <c>getCustomNormalButton()</c>.
    /// </summary>
    /// <param name="textAreaSize">Area the label will occupy.</param>
    /// <param name="isEvilInterface">Whether the gray plate is used.</param>
    /// <param name="background">Texture for the transparent corners of the pressed plate.</param>
    /// <returns>The sprites and the label offsets of both states.</returns>
    public static (Sprite Released, Sprite Pressed, Point ReleasedTextOffset, Point PressedTextOffset)
        GetCustomNormalButton(Size textAreaSize, bool isEvilInterface, Sprite? background = null)
    {
        DialogButtonTemplate template = isEvilInterface
            ? DialogButtonTemplate.Evil
            : DialogButtonTemplate.Good;

        return GetCustomNormalButton(
            RequireBlank(template, 0), RequireBlank(template, 1), template, textAreaSize, background);
    }

    /// <summary>
    /// Stretches the given plates, <c>getCustomNormalButton()</c> with the artwork handed in.
    /// </summary>
    /// <param name="emptyReleased">Released plate, frame 0 of the template.</param>
    /// <param name="emptyPressed">Pressed plate, frame 1 of the template.</param>
    /// <param name="template">Template the plates belong to.</param>
    /// <param name="textAreaSize">Area the label will occupy.</param>
    /// <param name="background">Texture for the transparent corners of the pressed plate.</param>
    /// <returns>The sprites and the label offsets of both states.</returns>
    /// <exception cref="ArgumentNullException">A plate is missing.</exception>
    public static (Sprite Released, Sprite Pressed, Point ReleasedTextOffset, Point PressedTextOffset)
        GetCustomNormalButton(
            Sprite emptyReleased,
            Sprite emptyPressed,
            DialogButtonTemplate template,
            Size textAreaSize,
            Sprite? background = null)
    {
        ArgumentNullException.ThrowIfNull(emptyReleased);
        ArgumentNullException.ThrowIfNull(emptyPressed);

        Point releasedTextOffset = new(7, 5);
        Point pressedTextOffset = new(6, 6);

        Size size = new(
            Math.Clamp(textAreaSize.Width + TextWidthMargin, MinimumWidth, MaximumSize),
            Math.Clamp(textAreaSize.Height, MinimumHeight, MaximumSize));

        Sprite released = ButtonArtwork.Resize(emptyReleased, size);
        Sprite pressed = ButtonArtwork.Resize(emptyPressed, size);

        if (template is DialogButtonTemplate.Good or DialogButtonTemplate.Evil)
        {
            ButtonArtwork.AddShine(released, template);
        }

        if (background is not null)
        {
            ButtonArtwork.MakeTransparentBackground(released, pressed, background);
        }

        return (released, pressed, releasedTextOffset, pressedTextOffset);
    }

    /// <summary>
    /// Builds a button sized to its label, <c>getTextAdaptedSprite()</c>.
    /// </summary>
    /// <param name="text">Label of the button; <c>\n</c> starts a new line.</param>
    /// <param name="template">Template whose plate and metrics are used.</param>
    /// <param name="background">Texture for the transparent corners of the pressed plate.</param>
    /// <returns>The two sprites of the button, released first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static (Sprite Released, Sprite Pressed) GetTextAdaptedSprite(
        string text,
        DialogButtonTemplate template,
        Sprite? background = null)
    {
        return GetTextAdaptedSprite(
            text, RequireBlank(template, 0), RequireBlank(template, 1), template, background);
    }

    /// <summary>
    /// Builds a button sized to its label out of the given plates, <c>getTextAdaptedSprite()</c> with
    /// the artwork handed in.
    /// </summary>
    /// <param name="text">Label of the button; <c>\n</c> starts a new line.</param>
    /// <param name="emptyReleased">Released plate, frame 0 of the template.</param>
    /// <param name="emptyPressed">Pressed plate, frame 1 of the template.</param>
    /// <param name="template">Template whose metrics are used.</param>
    /// <param name="background">Texture for the transparent corners of the pressed plate.</param>
    /// <returns>The two sprites of the button, released first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or a plate is missing.</exception>
    public static (Sprite Released, Sprite Pressed) GetTextAdaptedSprite(
        string text,
        Sprite emptyReleased,
        Sprite emptyPressed,
        DialogButtonTemplate template,
        Sprite? background = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(emptyReleased);
        ArgumentNullException.ThrowIfNull(emptyPressed);

        ButtonTemplateMetrics metrics = ButtonTemplateMetrics.Get(template);
        FontColor color = metrics.TextPalette == PaletteId.WhiteFont ? FontColor.White : FontColor.Gray;

        Text releasedText = new(text, new FontType(FontSize.ButtonReleased, color));
        Text pressedText = new(text, new FontType(FontSize.ButtonPressed, color));

        int textWidth = releasedText.Width(metrics.MaximumTextArea.Width);

        int textAreaWidth = Math.Clamp(
            textWidth + metrics.TextMargins.Width,
            metrics.MinimumTextArea.Width,
            metrics.MaximumTextArea.Width);

        int textHeight = releasedText.Height(textAreaWidth);

        int textMargins = metrics.TextMargins.Height;
        if (metrics.NeedsExtraHeightForMultilineText && textHeight > 17)
        {
            // The button font hangs below the plate on the second line: the reference makes room for it.
            textMargins += 16;
        }

        int textAreaHeight = Math.Clamp(
            textHeight + textMargins,
            metrics.MinimumTextArea.Height,
            metrics.MaximumTextArea.Height);

        Size size = new(
            textAreaWidth + metrics.BackgroundBorders.Width,
            textAreaHeight + metrics.BackgroundBorders.Height);

        Sprite released = ButtonArtwork.Resize(emptyReleased, size);
        Sprite pressed = ButtonArtwork.Resize(emptyPressed, size);

        if (background is not null)
        {
            ButtonArtwork.MakeTransparentBackground(released, pressed, background);
        }

        if (template is DialogButtonTemplate.Good or DialogButtonTemplate.Evil)
        {
            ButtonArtwork.AddShine(released, template);
        }

        int textTop = (textAreaHeight - textHeight) / 2;

        releasedText.Draw(
            metrics.ReleasedTextOffset.X + 1,
            metrics.ReleasedTextOffset.Y + textTop,
            textAreaWidth,
            released.Image);

        pressedText.Draw(
            metrics.PressedTextOffset.X + 1,
            metrics.PressedTextOffset.Y + textTop,
            textAreaWidth,
            pressed.Image);

        return (released, pressed);
    }

    /// <summary>
    /// Generates the sprites of several buttons that share one width and height,
    /// <c>makeSymmetricBackgroundSprites()</c>.
    /// </summary>
    /// <param name="texts">Labels of the buttons, in the order the sprites are wanted.</param>
    /// <param name="isEvilInterface">Whether the gray plates and lettering are used.</param>
    /// <param name="minimumWidth">Smallest width the common plate is allowed to take.</param>
    /// <param name="background">Texture for the transparent corners of the pressed plates.</param>
    /// <returns>
    /// Two sprites per label, released then pressed, in the order of <paramref name="texts"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Fewer than two labels were given.</exception>
    public static List<Sprite> MakeSymmetricBackgroundSprites(
        IReadOnlyList<string> texts,
        bool isEvilInterface,
        int minimumWidth,
        Sprite? background = null)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count < 2)
        {
            throw new ArgumentException("Symmetric buttons need at least two labels.", nameof(texts));
        }

        FontType font = new(
            FontSize.ButtonReleased,
            isEvilInterface ? FontColor.Gray : FontColor.White);

        List<Text> labels = new(texts.Count);
        foreach (string text in texts)
        {
            labels.Add(new Text(text, font));
        }

        int widestLabel = 0;
        foreach (Text label in labels)
        {
            widestLabel = Math.Max(widestLabel, label.Width(MaximumSize));
        }

        int width = Math.Clamp(widestLabel + 6, minimumWidth, MaximumSize);

        int tallestLabel = 0;
        foreach (Text label in labels)
        {
            tallestLabel = Math.Max(tallestLabel, label.Height(width));
        }

        int fontHeight = FontCharHandler.GetFontHeight(font.Size);
        int height = tallestLabel > fontHeight
            ? Math.Clamp(tallestLabel, 56, MaximumSize)
            : tallestLabel + 10;

        List<Sprite> sprites = new(labels.Count * 2);
        foreach (Text label in labels)
        {
            (Sprite released, Sprite pressed) = MakeButtonSprites(
                label.Content, new Size(width, height), isEvilInterface, background);

            sprites.Add(released);
            sprites.Add(pressed);
        }

        return sprites;
    }

    /// <summary>
    /// Draws the label of a generated button onto its two sprites, <c>renderTextOnButton()</c>.
    /// </summary>
    /// <param name="released">Released sprite to draw on.</param>
    /// <param name="pressed">Pressed sprite to draw on.</param>
    /// <param name="text">Label of the button; <c>\n</c> starts a new line.</param>
    /// <param name="releasedTextOffset">Top left corner of the label on the released sprite.</param>
    /// <param name="pressedTextOffset">Top left corner of the label on the pressed sprite.</param>
    /// <param name="buttonSize">
    /// Text area of the button, not the size of the sprite: it is the width the label is wrapped to
    /// and the height it is centred in.
    /// </param>
    /// <param name="color">Lettering colour of both states.</param>
    /// <exception cref="ArgumentNullException">A sprite or the label is missing.</exception>
    public static void RenderTextOnButton(
        Sprite released,
        Sprite pressed,
        string text,
        Point releasedTextOffset,
        Point pressedTextOffset,
        Size buttonSize,
        FontColor color)
    {
        ArgumentNullException.ThrowIfNull(released);
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(text);

        Text releasedText = new(text, new FontType(FontSize.ButtonReleased, color));
        Text pressedText = new(text, new FontType(FontSize.ButtonPressed, color));

        releasedText.Draw(
            releasedTextOffset.X,
            (buttonSize.Height - releasedText.Height(buttonSize.Width)) / 2,
            buttonSize.Width,
            released.Image);

        // The pressed font already sits one pixel lower, the reference moves it down another one.
        pressedText.Draw(
            pressedTextOffset.X,
            (buttonSize.Height - pressedText.Height(buttonSize.Width)) / 2 + 1,
            buttonSize.Width,
            pressed.Image);
    }

    /// <summary>
    /// Room the keyboard icon of the virtual keyboard button gets around it inside its plate,
    /// the <c>extraWidth</c> of <c>game_assets.cpp:5489</c>.
    /// </summary>
    public const int VirtualKeyboardExtraWidth = 19;

    /// <summary>
    /// Builds the button that opens the virtual keyboard, i.e. the generated
    /// <c>ICN::BUTTON_VIRTUAL_KEYBOARD_GOOD</c> / <c>ICN::BUTTON_VIRTUAL_KEYBOARD_EVIL</c>
    /// (<c>game_assets.cpp:5466-5511</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考把 h2d 里的键盘小图贴到放大过的空按钮上，再拿 <c>UNIFORMBAK</c> 补掉按下态多出来的四个角；返回的
    /// 释放态与「贴在底纹上的按下态」就是它生成的三张图里的第 0 和第 2 张，也正是对话框取用的那两张
    /// （<c>Button( …, 0, 2 )</c>）。
    /// </para>
    /// <para>
    /// 端口读到的档案里没有 <c>UNIFORMBAK.ICN</c>（它不在 AGG 里），底纹一律用 <c>STONEBAK</c> 的第 0 帧，
    /// 与 <see cref="VirtualKeyboardArtwork"/>、各对话框的按下态一致。
    /// </para>
    /// <para>
    /// h2d 里的释放态小图比按下态低一个像素（参考在 <c>game_assets.cpp:5493</c> 的注释里说明了这一点），
    /// 所以贴释放态时往上挪一格。没有 h2d 或没有底纹时返回 <see langword="null"/>，对话框据此不画这个键。
    /// </para>
    /// </remarks>
    /// <param name="isEvilInterface">Whether the gray plate and the evil icon are used.</param>
    /// <returns>The released look and the pressed look on the uniform background, or <see langword="null"/>.</returns>
    public static (Sprite Released, Sprite Pressed)? MakeVirtualKeyboardButton(bool isEvilInterface)
    {
        string iconName = isEvilInterface
            ? "keyboard_button_released_evil.image"
            : "keyboard_button_released_good.image";
        string pressedIconName = isEvilInterface
            ? "keyboard_button_pressed_evil.image"
            : "keyboard_button_pressed_good.image";

        if (!H2DResources.TryReadCurrentImage(iconName, out Sprite? icon)
            || !H2DResources.TryReadCurrentImage(pressedIconName, out Sprite? pressedIcon))
        {
            return null;
        }

        (Sprite released, Sprite pressed, Point releasedTextOffset, Point pressedTextOffset) =
            GetCustomNormalButton(new Size(icon.Width + VirtualKeyboardExtraWidth, icon.Height), isEvilInterface);

        ImageOps.BlitSprite(
            released,
            new Point((VirtualKeyboardExtraWidth / 2) + releasedTextOffset.X, releasedTextOffset.Y - 1),
            icon);

        ImageOps.BlitSprite(
            pressed,
            new Point((VirtualKeyboardExtraWidth / 2) + pressedTextOffset.X, pressedTextOffset.Y),
            pressedIcon);

        Sprite? background = GameAssets.GetImage(
            IcnCatalog.GetFileName(isEvilInterface ? IcnType.StonebakEvil : IcnType.Stonebak), 0);

        if (background is null)
        {
            return null;
        }

        ButtonArtwork.MakeTransparentBackground(released, pressed, background);

        return (released, pressed);
    }

    /// <summary>Names the asset an <see cref="DialogButtonTemplate"/> is cut from.</summary>
    /// <param name="template">Template to name.</param>
    /// <returns>The file name of the empty plate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The template is not known.</exception>
    public static string GetBlankAssetName(DialogButtonTemplate template) => template switch
    {
        DialogButtonTemplate.Good => GoodBlankAssetName,
        DialogButtonTemplate.Evil => EvilBlankAssetName,
        DialogButtonTemplate.Pol => PolBlankAssetName,
        DialogButtonTemplate.GuildWell => GuildWellBlankAssetName,
        DialogButtonTemplate.MapSelect => MapSelectBlankAssetName,
        DialogButtonTemplate.VerticalGood => VerticalGoodBlankAssetName,
        _ => throw new ArgumentOutOfRangeException(nameof(template), template, "Unknown button template."),
    };

    /// <summary>Reads one frame of the empty plate of <paramref name="template"/>.</summary>
    private static Sprite RequireBlank(DialogButtonTemplate template, int frameId)
    {
        string name = GetBlankAssetName(template);

        Sprite? sprite = GameAssets.GetImage(name, frameId);
        if (sprite is null)
        {
            throw new InvalidOperationException(
                $"Asset '{name}' has no frame {frameId}; the archive is too short or the plate cannot be assembled.");
        }

        return sprite;
    }

    /// <summary>
    /// The label a button is drawn with when the current language may not cover every glyph,
    /// <c>getSupportedText()</c> (<c>ui_button.cpp:959</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的签名只有一个 <c>untranslatedText</c>，翻译在函数里做（<c>_()</c>）。端口的翻译层还没搬，
    /// 所以把「译好的」和「原文」都交给调用方传进来，这里只保留参考的判断：译文里只要有一个字在按钮
    /// 字体里没有字形，就退回原文——这是参考对付半翻译语言（比如俄文版里的英文按钮）的办法。
    /// </para>
    /// <para>
    /// 参考检查的是译文/原文各自的 UTF-8 字节序列，端口用同样的编码（<see cref="Text"/> 也是 UTF-8）。
    /// </para>
    /// </remarks>
    /// <param name="translatedText">Label in the language of the current build.</param>
    /// <param name="untranslatedText">Original label, used when the font cannot draw the translation.</param>
    /// <param name="fontType">Font the label will be drawn with.</param>
    /// <returns>
    /// <paramref name="translatedText"/> when every glyph of it is available, otherwise
    /// <paramref name="untranslatedText"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">A text is <see langword="null"/>.</exception>
    public static string GetSupportedText(string translatedText, string untranslatedText, in FontType fontType)
    {
        ArgumentNullException.ThrowIfNull(translatedText);
        ArgumentNullException.ThrowIfNull(untranslatedText);

        return FontCharHandler.IsFontAvailable(Encoding.UTF8.GetBytes(translatedText), fontType)
            ? translatedText
            : untranslatedText;
    }

    /// <summary>
    /// Combines the two states of a button with the piece of a background texture they sit on,
    /// <c>makeButtonWithBackground()</c> (<c>ui_button.cpp:739</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 结果三张图都是「背景 + 按钮」：释放态、按下态，以及把释放态压暗得到的禁用态。参考总是给三张图
    /// 带上与背景相同的层数（背景是单层图时按钮也退化成单层），这样贴图时不至于把背景的变换值盖掉。
    /// </para>
    /// <para>
    /// 裁剪位置就是按钮在屏幕上的位置：<paramref name="offsetX"/> / <paramref name="offsetY"/> 会被原样
    /// 交给返回的 <see cref="ButtonSprite"/>，而三张合成图的锚点是 <c>(0, 0)</c>。
    /// </para>
    /// </remarks>
    /// <param name="offsetX">Horizontal position of the button on the background.</param>
    /// <param name="offsetY">Vertical position of the button on the background.</param>
    /// <param name="released">Released look of the button.</param>
    /// <param name="pressed">Pressed look of the button.</param>
    /// <param name="background">Texture the button is pasted on, usually the dialog background.</param>
    /// <returns>The button, anchored at <c>(offsetX, offsetY)</c>.</returns>
    /// <exception cref="ArgumentNullException">A sprite is <see langword="null"/>.</exception>
    public static ButtonSprite MakeButtonWithBackground(
        int offsetX,
        int offsetY,
        Sprite released,
        Sprite pressed,
        Sprite background)
    {
        ArgumentNullException.ThrowIfNull(released);
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(background);

        Sprite croppedBackground = ImageOps.Crop(background, offsetX, offsetY, released.Width, released.Height);

        Sprite releasedWithBackground = CopyLayers(croppedBackground);
        ImageOps.BlitSprite(releasedWithBackground, new Point(released.OffsetX, released.OffsetY), released);

        Sprite pressedWithBackground = CopyLayers(croppedBackground);
        ImageOps.BlitSprite(pressedWithBackground, new Point(pressed.OffsetX, pressed.OffsetY), pressed);

        // `Sprite disabled( released )` is a copy constructor: the darkened look keeps the anchor of
        // the released sprite, so it lands on the same pixels of the composite as the button itself.
        Sprite disabled = released.Clone();
        ImageOps.ApplyPalette(disabled.Image, PaletteId.Darkening);

        // The reference moves the crop into the disabled state and resets its anchor to (0, 0): no copy.
        Sprite disabledWithBackground = new(croppedBackground.Image, 0, 0, croppedBackground.Transform);
        ImageOps.BlitSprite(disabledWithBackground, new Point(disabled.OffsetX, disabled.OffsetY), disabled);

        return new ButtonSprite(releasedWithBackground, pressedWithBackground, disabledWithBackground, offsetX, offsetY);
    }

    /// <summary>
    /// Combines the two states of a button with the piece of a background texture they sit on, shifted by
    /// the shadow the button casts, <c>makeButtonWithShadow()</c> (<c>ui_button.cpp:770</c>).
    /// </summary>
    /// <remarks>
    /// 与 <see cref="MakeButtonWithBackground"/> 的区别只有裁剪原点：这里多出阴影的位置，裁剪出的背景
    /// 先被阴影盖一层，按钮再按 <c>-shadow</c> 的偏移贴上去，于是按钮的三张图都比背景的左上角更靠下。
    /// </remarks>
    /// <param name="offsetX">Horizontal position of the button on the background.</param>
    /// <param name="offsetY">Vertical position of the button on the background.</param>
    /// <param name="released">Released look of the button.</param>
    /// <param name="pressed">Pressed look of the button.</param>
    /// <param name="background">Texture the button is pasted on, usually the dialog background.</param>
    /// <param name="shadowOffset">Displacement of the shadow, relative to the button.</param>
    /// <returns>The button, anchored at the shadow's corner.</returns>
    /// <exception cref="ArgumentNullException">A sprite is <see langword="null"/>.</exception>
    public static ButtonSprite MakeButtonWithShadow(
        int offsetX,
        int offsetY,
        Sprite released,
        Sprite pressed,
        Sprite background,
        Point shadowOffset)
    {
        ArgumentNullException.ThrowIfNull(released);
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(background);

        Sprite shadow = ImageOps.MakeShadow(released, shadowOffset, ShadowTransformId);

        Sprite croppedBackground = ImageOps.Crop(
            background,
            offsetX + shadow.OffsetX,
            offsetY + shadow.OffsetY,
            shadow.Width,
            shadow.Height);

        ImageOps.BlitSprite(croppedBackground, Point.Zero, shadow);

        Sprite releasedWithBackground = CopyLayers(croppedBackground);
        ImageOps.BlitSprite(
            releasedWithBackground,
            new Point(released.OffsetX - shadow.OffsetX, released.OffsetY - shadow.OffsetY),
            released);

        Sprite pressedWithBackground = CopyLayers(croppedBackground);
        ImageOps.BlitSprite(
            pressedWithBackground,
            new Point(pressed.OffsetX - shadow.OffsetX, pressed.OffsetY - shadow.OffsetY),
            pressed);

        // `Sprite disabled( released )` is a copy constructor: the darkened look keeps the anchor of
        // the released sprite, so it lands on the same pixels of the composite as the button itself.
        Sprite disabled = released.Clone();
        ImageOps.ApplyPalette(disabled.Image, PaletteId.Darkening);

        // The reference moves the crop into the disabled state and resets its anchor to (0, 0): no copy.
        Sprite disabledWithBackground = new(croppedBackground.Image, 0, 0, croppedBackground.Transform);
        ImageOps.BlitSprite(
            disabledWithBackground,
            new Point(disabled.OffsetX - shadow.OffsetX, disabled.OffsetY - shadow.OffsetY),
            disabled);

        return new ButtonSprite(
            releasedWithBackground,
            pressedWithBackground,
            disabledWithBackground,
            offsetX + shadow.OffsetX,
            offsetY + shadow.OffsetY);
    }

    /// <summary>Transform id the button shadow is drawn with, <c>makeShadow( released, offset, 3 )</c>.</summary>
    private const byte ShadowTransformId = 3;

    /// <summary>
    /// Copies both layers of a sprite into a fresh one anchored at <c>(0, 0)</c>,
    /// <c>Copy( const Image &amp;, Image &amp; )</c>: the reference relies on the destination being a default
    /// constructed sprite, so the composites carry no anchor and only the <see cref="ButtonSprite"/>
    /// offset positions them.
    /// </summary>
    private static Sprite CopyLayers(Sprite source) => new(
        source.Image.Clone(),
        0,
        0,
        source.Transform is null ? null : (byte[])source.Transform.Clone());
}
