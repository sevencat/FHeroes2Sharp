using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// An interactive element inside a message box, a port of <c>fheroes2::DialogElement</c>
/// (<c>src/fheroes2/gui/ui_dialog.h:56-85</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The reference passes a <c>Point</c> and reads the screen; the port passes events and returns a result.</b> The
/// reference's <c>processEvents( const Point &amp; offset )</c> reaches for <c>LocalEvent::Get()</c> and pops a window
/// from inside itself (<c>showStandardTextMessage</c>), which cannot be tested and cannot be replayed. The port keeps
/// the two halves apart: <see cref="ProcessEvents"/> answers «repaint» and «open this popup», the owner of the dialog
/// does the popping.
/// </para>
/// <para>
/// <b>Elements are laid out by <c>showMessage</c>, not by themselves.</b> <see cref="Area"/> is the size the element
/// needs; the row packer decides where it lands and passes that position as the <c>offset</c> of
/// <see cref="Draw"/>. An element therefore never knows its absolute position.
/// </para>
/// </remarks>
public abstract class DialogElement
{
    /// <summary>Size the element needs, <c>_area</c> of the reference.</summary>
    public Size Area { get; protected set; }

    /// <summary>Paints the element, <c>draw( output, offset )</c>.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position the layout gave the element.</param>
    public abstract void Draw(PixelBuffer output, Point offset);

    /// <summary>
    /// Handles one frame of input, <c>processEvents( offset )</c>.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="offset">Position the layout gave the element.</param>
    /// <returns>What the owner has to do, empty when the element does not react.</returns>
    public virtual DialogElementResult ProcessEvents(LocalEvent events, Point offset) => default;

    /// <summary>
    /// Refreshes animated content, <c>update( output, offset )</c>; nothing by default.
    /// </summary>
    /// <remarks>
    /// The reference asks <c>Game::validateCustomAnimationDelay()</c>, a global clock that answers «a
    /// delay has passed» once per frame; the port passes the elapsed time in, so that the owner of the
    /// dialog - which already owns the frame clock - decides how fast time runs and a test can step
    /// the animation frame by frame.
    /// </remarks>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position the layout gave the element.</param>
    /// <param name="elapsed">Time since the previous call.</param>
    /// <returns><see langword="true"/> when something was painted.</returns>
    public virtual bool Update(PixelBuffer output, Point offset, TimeSpan elapsed) => false;
}

/// <summary>
/// A text run that takes the whole width of the message box, <c>TextDialogElement</c>
/// (<c>ui_dialog.h:91-107</c>, <c>ui_dialog.cpp:329-349</c>).
/// </summary>
public sealed class TextDialogElement : DialogElement
{
    private readonly TextBase _text;

    /// <summary>Creates the element and sizes it to the text wrapped at <see cref="FrameBox.AreaWidth"/>.</summary>
    /// <param name="text">The text; the element keeps a reference, the caller must not mutate it afterwards.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public TextDialogElement(TextBase text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _text = text;
        Area = new Size(FrameBox.AreaWidth, text.Height(FrameBox.AreaWidth));
    }

    /// <summary>Draws the text at the offset the layout gave it.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the text.</param>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        _text.DrawInRoi(offset.X, offset.Y, FrameBox.AreaWidth, output, output.Bounds);
    }
}

/// <summary>
/// An arbitrary image, <c>CustomImageDialogElement</c> (<c>ui_dialog.h:109-127</c>, <c>ui_dialog.cpp:351-376</c>).
/// </summary>
/// <remarks>The reference stores the image by value; the port takes the sprite as is and the caller keeps it alive.</remarks>
public sealed class CustomImageDialogElement : DialogElement
{
    private readonly Sprite _image;

    /// <summary>Creates the element and sizes it to the image.</summary>
    /// <param name="image">The image to show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    public CustomImageDialogElement(Sprite image)
    {
        ArgumentNullException.ThrowIfNull(image);

        _image = image;
        Area = new Size(image.Width, image.Height);
    }

    /// <summary>Draws the image without its own offset, the <c>Blit( image, 0, 0, ... )</c> of the reference.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the image.</param>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, offset, _image);
    }
}

/// <summary>
/// A looping animation drawn over one or more background frames, <c>AnimationDialogElement</c>
/// (<c>ui_dialog.h:287-313</c>, <c>ui_dialog.cpp:798-856</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference takes the id of the ICN file and the indices of the background frames, and asks
/// <c>ICN::getAnimatedIcnIndex()</c> which sprite a frame number stands for. The port does not port
/// that table - see <see cref="IcnAnimation"/> - so the caller names the range of the animation and the
/// element keeps the background frames.
/// </para>
/// <para>
/// <b>The background is painted on every <see cref="Draw"/>, not only on the first one.</b> The
/// reference paints it once, at <c>_currentIndex == 0</c>, and then only repaints the animated frame,
/// because its dialog loop leaves the rest of the screen alone. A ported scene repaints the whole frame
/// every time, so the background has to come back with it.
/// </para>
/// <para>
/// The centring quirk of the reference is kept: every background frame is centred inside
/// <see cref="DialogElement.Area"/> on its own, but the animated frame is placed by the centring of the
/// <i>last</i> background frame, because the reference overwrites <c>_internalOffset</c> on every
/// iteration of that loop (<c>ui_dialog.cpp:806-813</c>). <c>TAVWIN</c> depends on it: 188x100 and
/// 182x94, so the hand of the barkeep sits at <c>(3, 3)</c> plus its own anchor.
/// </para>
/// <para>
/// The element never handles input and has nothing to pop up, like the reference.
/// </para>
/// </remarks>
public sealed class AnimationDialogElement : DialogElement
{
    private readonly IReadOnlyList<Sprite> _backgrounds;
    private readonly IcnAnimation _animation;
    private readonly Point _internalOffset;

    /// <summary>
    /// Creates the element and sizes it to the widest and tallest background frame,
    /// <c>AnimationDialogElement( icnId, backgroundIndices, animationIndexOffset, delay )</c>.
    /// </summary>
    /// <param name="sprites">Sprites of the ICN file, in file order.</param>
    /// <param name="backgroundIndices">
    /// Frames drawn under the animation, in drawing order; the last one decides where the animation sits.
    /// </param>
    /// <param name="firstFrame">Index of the first animated sprite inside <paramref name="sprites"/>.</param>
    /// <param name="frameCount">Number of sprites the animation cycles through.</param>
    /// <param name="frameDuration">Time one frame stays on screen, <c>CASTLE_TAVERN_DELAY</c> is 75 ms.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sprites"/> or <paramref name="backgroundIndices"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="backgroundIndices"/> is empty or names a frame outside <paramref name="sprites"/>,
    /// or the animation range does not fit into <paramref name="sprites"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="firstFrame"/> is negative, <paramref name="frameCount"/> is not positive or
    /// <paramref name="frameDuration"/> is zero or negative.
    /// </exception>
    public AnimationDialogElement(
        IReadOnlyList<Sprite> sprites, IReadOnlyList<int> backgroundIndices, int firstFrame, int frameCount,
        TimeSpan frameDuration)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(backgroundIndices);

        if (backgroundIndices.Count == 0)
        {
            throw new ArgumentException("An animation needs at least one background frame.",
                nameof(backgroundIndices));
        }

        List<Sprite> backgrounds = new(backgroundIndices.Count);
        Size area = default;

        foreach (int index in backgroundIndices)
        {
            if (index < 0 || index >= sprites.Count)
            {
                throw new ArgumentException(
                    $"Frame {index} is not one of the {sprites.Count} sprites of the ICN file.",
                    nameof(backgroundIndices));
            }

            Sprite background = sprites[index];

            backgrounds.Add(background);
            area = new Size(Math.Max(area.Width, background.Width), Math.Max(area.Height, background.Height));
        }

        // 参考在循环里每次都覆盖 _internalOffset，所以留下的是最后一帧的居中量。
        Sprite last = backgrounds[^1];

        _backgrounds = backgrounds;
        _internalOffset = new Point((area.Width - last.Width) / 2, (area.Height - last.Height) / 2);
        _animation = new IcnAnimation(sprites, firstFrame, frameCount, frameDuration);
        Area = area;
    }

    /// <summary>Current frame of the cycle, <c>_currentIndex % frame count</c> of the reference.</summary>
    public int FrameNumber => _animation.FrameNumber;

    /// <summary>Draws the background frames and the current frame of the animation on top of them.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the element.</param>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        foreach (Sprite background in _backgrounds)
        {
            ImageOps.BlitSprite(
                output,
                new Point(
                    offset.X + ((Area.Width - background.Width) / 2),
                    offset.Y + ((Area.Height - background.Height) / 2)),
                background);
        }

        Sprite frame = _animation.Current;

        ImageOps.BlitSprite(
            output,
            new Point(
                offset.X + _internalOffset.X + frame.OffsetX,
                offset.Y + _internalOffset.Y + frame.OffsetY),
            frame);
    }

    /// <summary>Advances the clock and repaints when the frame of the animation changed.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the element.</param>
    /// <param name="elapsed">Time since the previous call.</param>
    /// <returns><see langword="true"/> when the animation moved on and was painted.</returns>
    public override bool Update(PixelBuffer output, Point offset, TimeSpan elapsed)
    {
        int before = _animation.FrameNumber;

        _animation.Advance(elapsed);

        if (_animation.FrameNumber == before)
        {
            return false;
        }

        Draw(output, offset);

        return true;
    }
}

/// <summary>
/// An artifact icon in a frame, <c>ArtifactDialogElement</c> (<c>ui_dialog.h:129-144</c>,
/// <c>ui_dialog.cpp:378-402</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference blits frame 7 of <c>RESOURCE.ICN</c> as the frame and the artifact's own 64 by 64 frame from
/// <c>ARTIFACT.ICN</c> six pixels into it, and the element is exactly the frame's size. Both sprites come from the
/// caller, like the icon of <see cref="ResourceDialogElement"/>.
/// </para>
/// <para>
/// The right click popup is the artifact name and its ability text. The reference takes them from
/// <c>Artifact::GetName()</c> and <c>Artifact::GetDescription()</c>, which are part of the artifact text table the port
/// has not built yet, so the caller passes both strings here.
/// </para>
/// </remarks>
public sealed class ArtifactDialogElement : DialogElement
{
    /// <summary>Distance between the frame and the icon, the <c>6</c> of the reference's blit.</summary>
    public const int IconOffsetFromFrame = 6;

    private readonly Sprite _frame;
    private readonly Sprite _icon;
    private readonly string _popupHeader;
    private readonly string _popupBody;

    /// <summary>Creates the element and sizes it to the frame.</summary>
    /// <param name="frame">The frame the icon sits in, frame 7 of <c>RESOURCE.ICN</c>.</param>
    /// <param name="icon">The artifact icon, a 64 by 64 frame of <c>ARTIFACT.ICN</c>.</param>
    /// <param name="popupHeader">Header of the right click popup, the artifact name.</param>
    /// <param name="popupBody">Body of the right click popup, the artifact description.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public ArtifactDialogElement(Sprite frame, Sprite icon, string popupHeader, string popupBody)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(popupHeader);
        ArgumentNullException.ThrowIfNull(popupBody);

        _frame = frame;
        _icon = icon;
        _popupHeader = popupHeader;
        _popupBody = popupBody;
        Area = new Size(frame.Width, frame.Height);
    }

    /// <summary>Draws the frame and the icon six pixels inside it.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the element.</param>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, offset, _frame);
        ImageOps.BlitSprite(
            output,
            new Point(offset.X + IconOffsetFromFrame, offset.Y + IconOffsetFromFrame),
            _icon);
    }

    /// <summary>Asks for the artifact popup on a right click, the only event the reference handles.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="offset">Position of the element.</param>
    /// <returns>The popup, or an empty result.</returns>
    public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressedInArea(new Rect(offset, Area)))
        {
            return default;
        }

        return new DialogElementResult(false, new DialogPopup(_popupHeader, _popupBody));
    }
}

/// <summary>
/// A resource icon with an optional amount under it, <c>ResourceDialogElement</c>
/// (<c>ui_dialog.h:146-163</c>, <c>ui_dialog.cpp:404-454</c>).
/// </summary>
/// <remarks>
/// The icon is centred over the quantity when the quantity is wider than the icon, so a narrow resource with a five
/// digit amount keeps a straight column. The right click popup is the resource name and its description, which the
/// reference takes from <c>Resource::String</c>/<c>Resource::getDescription</c>; the port takes both as text because
/// the translation layer is deliberately out of scope (ADR-139).
/// </remarks>
public sealed class ResourceDialogElement : DialogElement
{
    /// <summary>Gap between the icon and the quantity, <c>textOffsetFromElement</c>.</summary>
    public const int TextOffsetFromElement = 2;

    private readonly Sprite _icon;
    private readonly string _quantity;
    private readonly string _popupHeader;
    private readonly string _popupBody;

    /// <summary>Creates the element, <c>ResourceDialogElement( resourceType, text )</c>.</summary>
    /// <param name="icon">The resource icon, <c>RESOURCE.ICN</c> frame of the type.</param>
    /// <param name="quantity">
    /// Amount printed under the icon; an empty string makes the element exactly the size of the icon.
    /// </param>
    /// <param name="popupHeader">Header of the right click popup, usually the resource name.</param>
    /// <param name="popupBody">Body of the right click popup, usually the resource description.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public ResourceDialogElement(Sprite icon, string quantity, string popupHeader, string popupBody)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(popupHeader);
        ArgumentNullException.ThrowIfNull(popupBody);

        _icon = icon;
        _quantity = quantity;
        _popupHeader = popupHeader;
        _popupBody = popupBody;

        if (quantity.Length == 0)
        {
            Area = new Size(icon.Width, icon.Height);
        }
        else
        {
            var quantityText = new Text(quantity, FontType.SmallWhite());

            Area = new Size(
                Math.Max(icon.Width, quantityText.Width()),
                icon.Height + TextOffsetFromElement + quantityText.Height());
        }
    }

    /// <summary>Draws the icon and, when there is one, the amount under it.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the element.</param>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (_quantity.Length == 0)
        {
            ImageOps.BlitSprite(output, offset, _icon);

            return;
        }

        var quantityText = new Text(_quantity, FontType.SmallWhite());

        int maxWidth = Math.Max(_icon.Width, quantityText.Width());

        ImageOps.BlitSprite(output, new Point(offset.X + ((maxWidth - _icon.Width) / 2), offset.Y), _icon);

        quantityText.DrawInRoi(
            offset.X + ((maxWidth - quantityText.Width()) / 2),
            offset.Y + _icon.Height + TextOffsetFromElement,
            maxWidth,
            output,
            output.Bounds);
    }

    /// <summary>Asks for the resource popup on a right click, the only event the reference handles.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="offset">Position of the element.</param>
    /// <returns>The popup, or an empty result.</returns>
    public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressedInArea(new Rect(offset, Area)))
        {
            return default;
        }

        return new DialogElementResult(false, new DialogPopup(_popupHeader, _popupBody));
    }
}

/// <summary>
/// A spell icon with its name and cost under it, <c>SpellDialogElement</c> (<c>ui_dialog.h:165-182</c>,
/// <c>ui_dialog.cpp:486-521</c>).
/// </summary>
/// <remarks>
/// <para>
/// The label is the spell name and, when the spell costs anything, the cost in brackets: <c>Bless [6]</c>. The icon is
/// centred over the label when the label is wider, exactly like <see cref="ResourceDialogElement"/>.
/// </para>
/// <para>
/// The reference takes the cost from <c>spell.spellPoints( hero )</c>, which the artifact bonuses can scale down; the
/// port takes the number instead, because <see cref="Spell.SpellPoints(ArtifactBag?)"/> already answers it and the
/// element has no hero to give it. The font comes in as a glyph source for the same reason the message box takes one:
/// the element measures and draws the label itself, so a test can hand it a fake font.
/// </para>
/// <para>
/// The right click popup shows the name and the description template. <c>%{count}</c> stays in the text because the
/// reference fills it with the hero's power (<c>updateSpellDescription</c>, <c>spell_info.cpp</c>), and the port has no
/// damage tables for that yet.
/// </para>
/// </remarks>
public sealed class SpellDialogElement : DialogElement
{
    /// <summary>Gap between the icon and the label, <c>textOffsetFromElement</c>.</summary>
    public const int TextOffsetFromElement = 2;

    private readonly Sprite _icon;
    private readonly string _header;
    private readonly string _body;
    private readonly IGlyphSource _font;

    /// <summary>Creates the element and sizes it to the icon and the label, <c>SpellDialogElement( spell, hero )</c>.</summary>
    /// <param name="icon">The spell icon, a frame of <c>ICN::SPELLS</c>.</param>
    /// <param name="spell">The spell the element shows.</param>
    /// <param name="spellPoints">What the spell costs; zero hides the number in brackets.</param>
    /// <param name="font">Font of the label, <c>FontType::smallWhite()</c> in the reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="icon"/> or <paramref name="font"/> is <see langword="null"/>.</exception>
    public SpellDialogElement(Sprite icon, Spell spell, uint spellPoints, IGlyphSource font)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(font);

        _icon = icon;
        _font = font;
        _header = spell.GetName();
        _body = spell.GetDescription();

        Label = spellPoints > 0
            ? $"{_header} [{spellPoints.ToString(System.Globalization.CultureInfo.InvariantCulture)}]"
            : _header;

        Area = new Size(
            Math.Max(icon.Width, GlyphText.Measure(font, Label)),
            icon.Height + TextOffsetFromElement + font.LineHeight);
    }

    /// <summary>The text under the icon, the name and the cost.</summary>
    public string Label { get; }

    /// <summary>Draws the icon and the label under it.</summary>
    /// <param name="output">Frame buffer of the message box.</param>
    /// <param name="offset">Position of the element.</param>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        int labelWidth = GlyphText.Measure(_font, Label);
        int maxWidth = Math.Max(_icon.Width, labelWidth);

        ImageOps.BlitSprite(output, new Point(offset.X + ((maxWidth - _icon.Width) / 2), offset.Y), _icon);

        GlyphText.DrawLine(
            _font,
            output,
            new Point(offset.X + ((maxWidth - labelWidth) / 2), offset.Y + _icon.Height + TextOffsetFromElement),
            Label);
    }

    /// <summary>Asks for the spell popup on a right click, the only event the reference handles.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="offset">Position of the element.</param>
    /// <returns>The popup, or an empty result.</returns>
    public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressedInArea(new Rect(offset, Area)))
        {
            return default;
        }

        return new DialogElementResult(false, new DialogPopup(_header, _body));
    }
}

/// <summary>
/// A luck icon on its own, <c>LuckDialogElement</c> (<c>ui_dialog.h:427-448; ui_dialog.cpp:548-573</c>).
/// </summary>
/// <remarks>
/// 参考从 <c>EXPMRL.ICN</c> 取第 0 或第 1 帧，帧号由 <c>goodLuck</c> 决定；两帧都是自带底框的完整图标，元素只
/// 把它贴到自己的左上角。端口把帧交给调用方，于是元素不再需要知道幸运值——「第几帧」在调用点就确定下来。
/// </remarks>
public sealed class LuckDialogElement : DialogElement
{
    private const int TextOffsetFromElement = 2;

    private readonly Sprite _icon;
    private readonly string _popupHeader;
    private readonly string _popupBody;

    /// <summary>Builds a luck icon element.</summary>
    /// <param name="icon">The 34x34 luck frame the caller picked.</param>
    /// <param name="popupHeader">Header of the right-click popup.</param>
    /// <param name="popupBody">Body of the right-click popup.</param>
    public LuckDialogElement(Sprite icon, string popupHeader, string popupBody)
    {
        ArgumentNullException.ThrowIfNull(icon);

        _icon = icon;
        _popupHeader = popupHeader ?? string.Empty;
        _popupBody = popupBody ?? string.Empty;

        Area = new Size(icon.Width, icon.Height);
    }

    /// <inheritdoc/>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, offset, _icon);
    }

    /// <inheritdoc/>
    public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressedInArea(new Rect(offset, Area)))
        {
            return default;
        }

        return new DialogElementResult(false, new DialogPopup(_popupHeader, _popupBody));
    }
}

/// <summary>
/// A morale icon on its own, <c>MoraleDialogElement</c> (<c>ui_dialog.h:450-471; ui_dialog.cpp:575-600</c>).
/// </summary>
/// <remarks>
/// 与 <see cref="LuckDialogElement"/> 同构，只是参考的帧是 <c>EXPMRL.ICN</c> 的第 2、3 帧。两个类分开是为了让
/// 调用点读起来是「士气」而不是「某个图标」。
/// </remarks>
public sealed class MoraleDialogElement : DialogElement
{
    private readonly Sprite _icon;
    private readonly string _popupHeader;
    private readonly string _popupBody;

    /// <summary>Builds a morale icon element.</summary>
    /// <param name="icon">The 34x34 morale frame the caller picked.</param>
    /// <param name="popupHeader">Header of the right-click popup.</param>
    /// <param name="popupBody">Body of the right-click popup.</param>
    public MoraleDialogElement(Sprite icon, string popupHeader, string popupBody)
    {
        ArgumentNullException.ThrowIfNull(icon);

        _icon = icon;
        _popupHeader = popupHeader ?? string.Empty;
        _popupBody = popupBody ?? string.Empty;

        Area = new Size(icon.Width, icon.Height);
    }

    /// <inheritdoc/>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, offset, _icon);
    }

    /// <inheritdoc/>
    public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressedInArea(new Rect(offset, Area)))
        {
            return default;
        }

        return new DialogElementResult(false, new DialogPopup(_popupHeader, _popupBody));
    }
}

/// <summary>
/// An experience icon with the number under it, <c>ExperienceDialogElement</c>
/// (<c>ui_dialog.h:473-494; ui_dialog.cpp:602-640</c>).
/// </summary>
/// <remarks>
/// 参考把经验值格式化成小号白字放在图标下面（间隔 <c>textOffsetFromElement</c>）；数字为空时元素就是图标本身。
/// 端口让调用方先格式化——数字的千位分隔符属于界面语言，元素不该自己决定。
/// </remarks>
public sealed class ExperienceDialogElement : DialogElement
{
    private const int TextOffsetFromElement = 2;

    private readonly Sprite _icon;
    private readonly Sprite? _background;
    private readonly string _experience;
    private readonly string _popupHeader;
    private readonly string _popupBody;

    /// <summary>Builds an experience element.</summary>
    /// <param name="icon">The 34x34 experience frame.</param>
    /// <param name="experience">The number under it; empty for none.</param>
    /// <param name="popupHeader">Header of the right-click popup.</param>
    /// <param name="popupBody">Body of the right-click popup.</param>
    /// <param name="background">Frame drawn behind the icon, or <see langword="null"/>.</param>
    public ExperienceDialogElement(Sprite icon, string experience, string popupHeader, string popupBody, Sprite? background = null)
    {
        ArgumentNullException.ThrowIfNull(icon);

        _icon = icon;
        _background = background;
        _experience = experience ?? string.Empty;
        _popupHeader = popupHeader ?? string.Empty;
        _popupBody = popupBody ?? string.Empty;

        int width = icon.Width;
        int height = icon.Height;

        if (_experience.Length != 0)
        {
            Text text = new(_experience, FontType.SmallWhite());
            width = Math.Max(width, text.Width());
            height += TextOffsetFromElement + text.Height();
        }

        if (background is not null)
        {
            width = Math.Max(width, background.Width);
            height = Math.Max(height, background.Height);
        }

        Area = new Size(width, height);
    }

    /// <inheritdoc/>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (_background is not null)
        {
            ImageOps.BlitSprite(output, offset, _background);
        }

        ImageOps.BlitSprite(output, offset, _icon);

        if (_experience.Length == 0)
        {
            return;
        }

        Text text = new(_experience, FontType.SmallWhite());
        int x = offset.X + ((Area.Width - text.Width()) / 2);
        int y = offset.Y + _icon.Height + TextOffsetFromElement;

        text.DrawInRoi(x, y, Area.Width, output, new Rect(offset, Area));
    }

    /// <inheritdoc/>
    public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressedInArea(new Rect(offset, Area)))
        {
            return default;
        }

        return new DialogElementResult(false, new DialogPopup(_popupHeader, _popupBody));
    }
}

/// <summary>
/// A primary skill with its value, <c>PrimarySkillDialogElement</c>
/// (<c>ui_dialog.h:496-527; ui_dialog.cpp:642-706</c>).
/// </summary>
/// <remarks>
/// 参考在 34x34 的图标右边写「技能名」（小号黄字，<c>y + 10</c>）与「数值」（大号白字，<c>y + 82</c>）。技能名由
/// <see cref="PrimarySkillDefaults.GetName"/> 给出，数值由调用方格式化——那是界面语言的事。
/// </remarks>
public sealed class PrimarySkillDialogElement : DialogElement
{
    private const int SkillTextHeight = 10;
    private const int ValueTextHeight = 82;

    private readonly Sprite _background;
    private readonly Sprite _icon;
    private readonly string _skillName;
    private readonly string _value;
    private readonly string _popupHeader;
    private readonly string _popupBody;

    /// <summary>Builds a primary skill element.</summary>
    /// <param name="skill">The skill the icon stands for.</param>
    /// <param name="background">Frame drawn behind the icon, or <see langword="null"/>.</param>
    /// <param name="icon">The 34x34 skill frame.</param>
    /// <param name="value">The value under the name; empty for none.</param>
    /// <param name="popupHeader">Header of the right-click popup.</param>
    /// <param name="popupBody">Body of the right-click popup.</param>
    public PrimarySkillDialogElement(PrimarySkill skill, Sprite background, Sprite icon, string value, string popupHeader, string popupBody)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(icon);

        _background = background;
        _icon = icon;
        _skillName = PrimarySkillDefaults.GetName(skill);
        _value = value ?? string.Empty;
        _popupHeader = popupHeader ?? string.Empty;
        _popupBody = popupBody ?? string.Empty;

        Area = new Size(background.Width, background.Height);
    }

    /// <inheritdoc/>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, offset, _background);

        int iconX = offset.X + ((Area.Width - _icon.Width) / 2);
        int iconY = offset.Y + ((Area.Height - _icon.Height) / 2);

        ImageOps.BlitSprite(output, new Point(iconX, iconY), _icon);

        Text skillName = new(_skillName, FontType.SmallWhite());
        int nameX = offset.X + ((Area.Width - skillName.Width()) / 2);

        skillName.DrawInRoi(nameX, offset.Y + SkillTextHeight, Area.Width, output, new Rect(offset, Area));

        if (_value.Length == 0)
        {
            return;
        }

        Text value = new(_value, FontType.NormalWhite());
        int valueX = offset.X + ((Area.Width - value.Width()) / 2);

        value.DrawInRoi(valueX, offset.Y + ValueTextHeight, Area.Width, output, new Rect(offset, Area));
    }

    /// <inheritdoc/>
    public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressedInArea(new Rect(offset, Area)))
        {
            return default;
        }

        return new DialogElementResult(false, new DialogPopup(_popupHeader, _popupBody));
    }
}

/// <summary>
/// A secondary skill with its level, <c>SecondarySkillDialogElement</c>
/// (<c>ui_dialog.h:529-559; ui_dialog.cpp:708-770</c>).
/// </summary>
/// <remarks>
/// 参考在图标上方写「技能名」（小号黄字，<c>y + 8</c>）、下方写「等级名」（小号白字，<c>y + 56</c>），两行都以
/// 图标宽度为上限裁剪。端口把两行文本都交给调用方：等级名要不要带招魂加成，由调用点按英雄算
/// （<see cref="SecondarySkills.GetLevelNameWithBonus"/>）。
/// </remarks>
public sealed class SecondarySkillDialogElement : DialogElement
{
    private const int SkillTextHeight = 8;
    private const int LevelTextHeight = 56;

    private readonly Sprite _background;
    private readonly Sprite _icon;
    private readonly string _skillName;
    private readonly string _levelName;
    private readonly string _popupHeader;
    private readonly string _popupBody;

    /// <summary>Builds a secondary skill element.</summary>
    /// <param name="background">Frame drawn behind the icon.</param>
    /// <param name="icon">The 34x34 skill frame.</param>
    /// <param name="skillName">The skill's name.</param>
    /// <param name="levelName">The hero's level in it.</param>
    /// <param name="popupHeader">Header of the right-click popup.</param>
    /// <param name="popupBody">Body of the right-click popup.</param>
    public SecondarySkillDialogElement(Sprite background, Sprite icon, string skillName, string levelName, string popupHeader, string popupBody)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(icon);

        _background = background;
        _icon = icon;
        _skillName = skillName ?? string.Empty;
        _levelName = levelName ?? string.Empty;
        _popupHeader = popupHeader ?? string.Empty;
        _popupBody = popupBody ?? string.Empty;

        Area = new Size(background.Width, background.Height);
    }

    /// <inheritdoc/>
    public override void Draw(PixelBuffer output, Point offset)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, offset, _background);

        int iconX = offset.X + ((Area.Width - _icon.Width) / 2);
        int iconY = offset.Y + ((Area.Height - _icon.Height) / 2);

        ImageOps.BlitSprite(output, new Point(iconX, iconY), _icon);

        Rect iconArea = new Rect(iconX, iconY, _icon.Width, _icon.Height);

        Text skillName = new(_skillName, FontType.SmallWhite());
        skillName.FitToOneRow(_icon.Width);
        skillName.DrawInRoi(iconX + ((_icon.Width - skillName.Width()) / 2), iconY - SkillTextHeight, _icon.Width, output, iconArea);

        Text levelName = new(_levelName, FontType.SmallWhite());
        levelName.FitToOneRow(_icon.Width);
        levelName.DrawInRoi(iconX + ((_icon.Width - levelName.Width()) / 2), iconY + _icon.Height + LevelTextHeight, _icon.Width, output, iconArea);
    }

    /// <inheritdoc/>
    public override DialogElementResult ProcessEvents(LocalEvent events, Point offset)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressedInArea(new Rect(offset, Area)))
        {
            return default;
        }

        return new DialogElementResult(false, new DialogPopup(_popupHeader, _popupBody));
    }
}

/// <summary>A standard message the owner of a dialog has to open, the <c>showStandardTextMessage</c> of the reference.</summary>
/// <param name="Header">Yellow header line.</param>
/// <param name="Body">White body text.</param>
public readonly record struct DialogPopup(string Header, string Body);



/// <summary>What one frame of <see cref="DialogElement.ProcessEvents"/> asks its owner to do.</summary>
/// <param name="RequiresRedraw">Whether the element painted something and the dialog has to be repainted.</param>
/// <param name="Popup">A popup the element asked for, or <see langword="null"/>.</param>
public readonly record struct DialogElementResult(bool RequiresRedraw, DialogPopup? Popup = null);
