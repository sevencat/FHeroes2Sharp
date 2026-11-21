using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Builds a button out of an empty plate, the free functions of <c>ui_button.cpp</c>:
/// <c>getCustomNormalButton()</c>, <c>getTextAdaptedSprite()</c>, <c>makeButtonSprites()</c>,
/// <c>makeSymmetricBackgroundSprites()</c> and <c>renderTextOnButton()</c>.
/// </summary>
/// <remarks>
/// <para>
/// The labels are measured and drawn with <see cref="Text"/> and the button font, exactly the way the
/// reference does it. The port used to draw them itself through <c>GlyphText</c>, which is why the
/// text of a generated button could drift away from the one of a dialog label.
/// </para>
/// <para>
/// Not ported yet: <c>getSupportedText()</c> (it asks the localisation subsystem whether the font of
/// the current language holds every glyph) and <c>makeButtonWithBackground()</c> /
/// <c>makeButtonWithShadow()</c> (both return a <c>ButtonSprite</c>, a type no port window uses yet).
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
    public const string MapSelectBlankAssetName = "EMPTY_MAP_SELECT_BUTTON.ICN";

    /// <summary><c>EMPTY_VERTICAL_GOOD_BUTTON</c>, the plate of a vertical button.</summary>
    public const string VerticalGoodBlankAssetName = "EMPTY_VERTICAL_GOOD_BUTTON.ICN";

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
}
