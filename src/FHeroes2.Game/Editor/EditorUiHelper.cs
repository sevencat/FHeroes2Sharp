using System.Globalization;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The small drawing helpers of the editor windows, a function level port of
/// <c>src/fheroes2/editor/editor_ui_helper.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/editor/editor_ui_helper.h:41</c> namespace Editor（<c>Checkbox</c> 类在 42 行），
/// 实现 <c>src/fheroes2/editor/editor_ui_helper.cpp:34</c> namespace Editor。
/// </para>
/// <para>
/// 已搬：<c>Checkbox</c>（构造 <c>cpp:36</c>、<c>toggle</c> <c>cpp:57</c>）、
/// <c>createColorCheckboxes</c>（<c>cpp:66</c>）、<c>drawCheckboxWithText</c>（<c>cpp:77</c>）、
/// <c>renderResources</c>（<c>cpp:99</c>）。
/// </para>
/// <para>
/// 映射：参考的 <c>fheroes2::Image &amp; output</c> 是 <see cref="PixelBuffer"/>（单层表面，写完由场景呈现），
/// <c>Assets::getImage</c> 是 <see cref="Assets.GetImage"/>，<c>ICN::CELLWIN</c> / <c>ICN::RESOURCE</c> 是归档条目名
/// <c>"CELLWIN.ICN"</c> / <c>"RESOURCE.ICN"</c>（<c>ICN::</c> 枚举表还没移植）。
/// </para>
/// </remarks>
public static class EditorUiHelper
{
    /// <summary>
    /// <c>Editor::createColorCheckboxes</c>: appends one <see cref="Checkbox"/> per available colour,
    /// 32 pixels apart, and paints the boxes into <paramref name="output"/>.
    /// </summary>
    /// <param name="list">List the checkboxes are added to.</param>
    /// <param name="availableColors">Colours the map allows, as a bit set of <see cref="PlayerColor"/>.</param>
    /// <param name="selectedColors">Colours that start checked.</param>
    /// <param name="boxOffsetX">Left edge of the first box.</param>
    /// <param name="boxOffsetY">Top edge of the boxes.</param>
    /// <param name="output">Frame buffer the boxes are painted into.</param>
    /// <remarks>
    /// 参考：<c>src/fheroes2/editor/editor_ui_helper.cpp:66</c> createColorCheckboxes（声明
    /// <c>src/fheroes2/editor/editor_ui_helper.h:70</c>）。参考的列表是
    /// <c>std::vector&lt;std::unique_ptr&lt;Checkbox&gt;&gt;</c>，本项目是 <c>List&lt;Checkbox&gt;</c>；
    /// 参考的空值由调用方保证，这里保留 <see cref="ArgumentNullException"/> 检查。
    /// </remarks>
    public static void CreateColorCheckboxes(List<Checkbox> list, PlayerColor availableColors, PlayerColor selectedColors,
        int boxOffsetX, int boxOffsetY, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(output);

        int colorsAdded = 0;

        foreach (PlayerColor color in PlayerColors.ToList(availableColors))
        {
            list.Add(new Checkbox(boxOffsetX + (colorsAdded * 32), boxOffsetY, color, (selectedColors & color) != 0, output));
            ++colorsAdded;
        }
    }

    /// <summary>
    /// <c>Editor::drawCheckboxWithText</c>: paints the <c>CELLWIN</c> checkbox with its gradient shadow,
    /// draws the label next to it and positions <paramref name="checkSprite"/> on the box.
    /// </summary>
    /// <param name="checkSprite">Movable check mark, re-pointed at <c>CELLWIN</c> frame 2 and moved to the box.</param>
    /// <param name="text">Label.</param>
    /// <param name="output">Frame buffer.</param>
    /// <param name="posX">Left edge of the box.</param>
    /// <param name="posY">Top edge of the box.</param>
    /// <param name="isEvil">Whether to use the evil variant of the box.</param>
    /// <param name="maxWidth">Maximum width of the label, the label is cut with "..." if longer.</param>
    /// <returns>Area taken by the box and the label.</returns>
    /// <exception cref="NotSupportedException"><paramref name="isEvil"/> is set and the evil box generator is not ported.</exception>
    /// <remarks>
    /// <para>
    /// 参考：<c>src/fheroes2/editor/editor_ui_helper.cpp:77</c> drawCheckboxWithText（声明
    /// <c>src/fheroes2/editor/editor_ui_helper.h:64</c>）：贴 <c>CELLWIN</c> 帧 1（82）、加 <c>(-4, 4)</c>
    /// 渐变阴影（85）、文字左移 23 像素、下移 4 像素（87–91）、勾选标记落在 <c>+2,+2</c>（93–94）。
    /// </para>
    /// <para>
    /// 差异：参考的 <c>ICN::CELLWIN_EVIL</c> 是启动期从 <c>CELLWIN</c> 生成的 ICN，那个生成器还没移植，
    /// 所以 <paramref name="isEvil"/> 走 <see cref="NotSupportedException"/>。参考用 <c>assert</c> 保证
    /// <paramref name="maxWidth"/> 为正，这里不加检查，<c>FitToOneRow</c> 自己会忽略非正值。
    /// </para>
    /// </remarks>
    public static Rect DrawCheckboxWithText(MovableSprite checkSprite, string text, PixelBuffer output, int posX, int posY, bool isEvil,
        int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(checkSprite);
        ArgumentNullException.ThrowIfNull(output);

        if (isEvil)
        {
            throw new NotSupportedException(
                "CELLWIN_EVIL is generated at start up; that generator is not ported yet.");
        }

        Sprite checkboxBackground = Assets.GetImage("CELLWIN.ICN", 1)
            ?? throw new InvalidOperationException("CELLWIN.ICN frame 1 is missing from the archives.");

        ImageOps.Blit(output, new Point(posX, posY), checkboxBackground.Image,
            new Rect(0, 0, checkboxBackground.Width, checkboxBackground.Height));

        ImageOps.AddGradientShadow(checkboxBackground, output, new Point(posX, posY), new Point(-4, 4));

        const int TextOffsetX = 23;

        Text checkboxText = new(text, FontType.NormalWhite());
        checkboxText.FitToOneRow(maxWidth - TextOffsetX);
        checkboxText.Draw(posX + TextOffsetX, posY + 4, output);

        checkSprite.Set(Assets.GetImage("CELLWIN.ICN", 2)
            ?? throw new InvalidOperationException("CELLWIN.ICN frame 2 is missing from the archives."));
        checkSprite.SetPosition(posX + 2, posY + 2);

        return new Rect(posX, posY, TextOffsetX + checkboxText.Width(), checkboxBackground.Height);
    }

    /// <summary>
    /// <c>Editor::renderResources</c>: paints the seven resource icons with their amounts into a panel.
    /// </summary>
    /// <param name="resources">Amounts to paint.</param>
    /// <param name="roi">Panel rectangle the icons are laid out in.</param>
    /// <param name="output">Frame buffer.</param>
    /// <param name="resourceRoi">Receives the area of every icon, in the reference's order: wood, sulfur, crystal, mercury, ore, gems, gold.</param>
    /// <exception cref="InvalidOperationException">The archive has no <c>RESOURCE.ICN</c> frame.</exception>
    /// <remarks>
    /// <para>
    /// 参考：<c>src/fheroes2/editor/editor_ui_helper.cpp:99</c> renderResources（声明
    /// <c>src/fheroes2/editor/editor_ui_helper.h:71</c>）：边缘 7（101）、格 39×32（104–105）、两行、第二行
    /// 起点下移 32 + 字体高 + 2（118）、金子在第二行横跨两格（134/164）。
    /// </para>
    /// <para>
    /// 差异：<c>std::array&lt;Rect, 7&gt; &amp; resourceRoi</c> 变成 <see cref="Span{T}"/>，调用方负责给出
    /// 至少 7 个元素；<c>Assets::getImage</c> 缺帧时参考给 <c>errorImage</c>，这里抛
    /// <see cref="InvalidOperationException"/>。
    /// </para>
    /// </remarks>
    public static void RenderResources(in Funds resources, in Rect roi, PixelBuffer output, Span<Rect> resourceRoi)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (resourceRoi.Length < 7)
        {
            throw new ArgumentException("resourceRoi must hold 7 areas.", nameof(resourceRoi));
        }

        const int OffsetFromEdge = 7;

        // Maximum width is 39 pixels (except gold), maximum height is 32 pixels.
        const int MaxWidth = 39;
        const int MaxHeight = 32;

        int midElementOffsetX = ((roi.Width - (2 * OffsetFromEdge)) - (MaxWidth * 4)) / 3;

        int firstColumnOffset = roi.X + OffsetFromEdge;
        int columnStep = MaxWidth + midElementOffsetX;
        int secondColumnOffset = firstColumnOffset + columnStep;
        int thirdColumnOffset = secondColumnOffset + columnStep;
        int forthColumnOffset = thirdColumnOffset + columnStep;

        FontType fontType = new(FontSize.Small, FontColor.White);
        int fontHeight = new Text(string.Empty, fontType).Height();

        int[] offsetY = [roi.Y + OffsetFromEdge + MaxHeight, roi.Y + OffsetFromEdge + (2 * MaxHeight) + fontHeight + 2];

        Sprite woodImage = RequireResource(0);
        Sprite mercuryImage = RequireResource(1);
        Sprite oreImage = RequireResource(2);
        Sprite sulfurImage = RequireResource(3);
        Sprite crystalImage = RequireResource(4);
        Sprite gemsImage = RequireResource(5);
        Sprite goldImage = RequireResource(6);

        resourceRoi[0] = new Rect(firstColumnOffset + ((MaxWidth - woodImage.Width) / 2), offsetY[0] - woodImage.Height,
            woodImage.Width, woodImage.Height);
        resourceRoi[1] = new Rect(secondColumnOffset + ((MaxWidth - sulfurImage.Width) / 2), offsetY[0] - sulfurImage.Height,
            sulfurImage.Width, sulfurImage.Height);
        resourceRoi[2] = new Rect(thirdColumnOffset + ((MaxWidth - crystalImage.Width) / 2), offsetY[0] - crystalImage.Height,
            crystalImage.Width, crystalImage.Height);
        resourceRoi[3] = new Rect(forthColumnOffset + ((MaxWidth - mercuryImage.Width) / 2), offsetY[0] - mercuryImage.Height,
            mercuryImage.Width, mercuryImage.Height);
        resourceRoi[4] = new Rect(firstColumnOffset + ((MaxWidth - oreImage.Width) / 2), offsetY[1] - oreImage.Height,
            oreImage.Width, oreImage.Height);
        resourceRoi[5] = new Rect(secondColumnOffset + ((MaxWidth - gemsImage.Width) / 2), offsetY[1] - gemsImage.Height,
            gemsImage.Width, gemsImage.Height);
        resourceRoi[6] = new Rect(thirdColumnOffset + (((MaxWidth * 2) + midElementOffsetX - goldImage.Width) / 2),
            offsetY[1] - goldImage.Height, goldImage.Width, goldImage.Height);

        ImageOps.BlitSprite(output, new Point(resourceRoi[0].X, resourceRoi[0].Y), woodImage);
        ImageOps.BlitSprite(output, new Point(resourceRoi[1].X, resourceRoi[1].Y), sulfurImage);
        ImageOps.BlitSprite(output, new Point(resourceRoi[2].X, resourceRoi[2].Y), crystalImage);
        ImageOps.BlitSprite(output, new Point(resourceRoi[3].X, resourceRoi[3].Y), mercuryImage);
        ImageOps.BlitSprite(output, new Point(resourceRoi[4].X, resourceRoi[4].Y), oreImage);
        ImageOps.BlitSprite(output, new Point(resourceRoi[5].X, resourceRoi[5].Y), gemsImage);
        ImageOps.BlitSprite(output, new Point(resourceRoi[6].X, resourceRoi[6].Y), goldImage);

        Text text = new(Money(resources.Wood), fontType);
        text.Draw(firstColumnOffset + ((MaxWidth - text.Width()) / 2), offsetY[0] + 1, output);

        text.Set(Money(resources.Sulfur), fontType);
        text.Draw(secondColumnOffset + ((MaxWidth - text.Width()) / 2), offsetY[0] + 1, output);

        text.Set(Money(resources.Crystal), fontType);
        text.Draw(thirdColumnOffset + ((MaxWidth - text.Width()) / 2), offsetY[0] + 1, output);

        text.Set(Money(resources.Mercury), fontType);
        text.Draw(forthColumnOffset + ((MaxWidth - text.Width()) / 2), offsetY[0] + 1, output);

        text.Set(Money(resources.Ore), fontType);
        text.Draw(firstColumnOffset + ((MaxWidth - text.Width()) / 2), offsetY[1] + 1, output);

        text.Set(Money(resources.Gems), fontType);
        text.Draw(secondColumnOffset + ((MaxWidth - text.Width()) / 2), offsetY[1] + 1, output);

        text.Set(Money(resources.Gold), fontType);
        text.Draw(thirdColumnOffset + (((MaxWidth * 2) + midElementOffsetX - text.Width()) / 2), offsetY[1] + 1, output);
    }

    /// <summary><c>std::to_string</c> for a resource amount: locale independent, like the reference.</summary>
    /// <param name="value">The amount.</param>
    /// <returns>Its decimal representation.</returns>
    private static string Money(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Reads one <c>RESOURCE.ICN</c> frame or fails loudly.</summary>
    /// <param name="frameId">Frame number, 0 is wood and 6 is gold.</param>
    /// <returns>The icon.</returns>
    private static Sprite RequireResource(int frameId) =>
        Assets.GetImage("RESOURCE.ICN", frameId)
        ?? throw new InvalidOperationException($"RESOURCE.ICN frame {frameId.ToString(CultureInfo.InvariantCulture)} is missing from the archives.");
}

/// <summary>
/// <c>Editor::Checkbox</c>: a player colour box painted into a frame buffer plus the check mark that
/// is toggled on top of it.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/editor/editor_ui_helper.h:42</c> class Checkbox（<c>getRect</c> 52、
/// <c>getColor</c> 57、<c>toggle</c> 62、私有成员 <c>_color</c>/<c>_area</c>/<c>_checkmark</c> 在 65–67）；
/// 实现 <c>src/fheroes2/editor/editor_ui_helper.cpp:36</c> 构造、<c>:57</c> toggle。
/// </para>
/// <para>
/// 未搬：参考的 <c>toggle</c> 末尾还有 <c>Display::instance().render( _area )</c>（<c>cpp:61</c>）立即刷新
/// 那块矩形，<c>MovableSprite::show</c> 也会立刻 <c>Blit</c> 到 Display。本项目还没有 Display 层，
/// 所以状态翻在这个类里，画面由场景下一帧整体重画。
/// </para>
/// </remarks>
public sealed class Checkbox
{
    private readonly PlayerColor _color;
    private readonly MovableSprite _checkmark;

    /// <summary>
    /// Paints the box of <paramref name="boxColor"/> at (<paramref name="x"/>, <paramref name="y"/>)
    /// and positions the check mark at its top left corner plus two pixels.
    /// </summary>
    /// <param name="x">Left edge of the box.</param>
    /// <param name="y">Top edge of the box.</param>
    /// <param name="boxColor">Colour of the box, <see cref="PlayerColor.None"/> for the neutral one.</param>
    /// <param name="checkedState">Whether the check mark starts visible.</param>
    /// <param name="output">Frame buffer the box is painted into.</param>
    /// <exception cref="InvalidOperationException">The archive has no <c>CELLWIN.ICN</c> frame.</exception>
    /// <remarks>
    /// 参考：<c>src/fheroes2/editor/editor_ui_helper.cpp:36</c> Checkbox::Checkbox
    /// （帧号 40、<c>Copy</c> 45、<c>setPosition</c> 47、show/hide 49–54）。
    /// 差异：参考的 <c>Assets::getImage</c> 缺帧时给 <c>errorImage</c>，这里抛
    /// <see cref="InvalidOperationException"/>，避免把错误图画进窗口。
    /// </remarks>
    public Checkbox(int x, int y, PlayerColor boxColor, bool checkedState, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _color = boxColor;
        _checkmark = new MovableSprite(RequireImage(2));

        // 参考 40 行：const int32_t icnIndex = ( _color == PlayerColor::NONE ) ? 1 : Color::GetIndex( _color ) + 43;
        // Color::GetIndex（src/fheroes2/kingdom/color.h:127）在这里是 PlayerColors.IndexOf。
        int icnIndex = (boxColor == PlayerColor.None) ? 1 : PlayerColors.IndexOf(boxColor) + 43;
        Sprite playerIcon = RequireImage(icnIndex);

        Area = new Rect(x, y, playerIcon.Width, playerIcon.Height);

        // 参考 45 行：fheroes2::Copy( playerIcon, 0, 0, output, _area )，整帧贴到框的位置。
        ImageOps.BlitSprite(output, new Point(Area.X, Area.Y), playerIcon);

        _checkmark.SetPosition(Area.X + 2, Area.Y + 2);

        if (checkedState)
        {
            _checkmark.Show();
        }
        else
        {
            _checkmark.Hide();
        }
    }

    /// <summary>Rectangle the box occupies, the reference's <c>_area</c>.</summary>
    /// <remarks>参考：<c>src/fheroes2/editor/editor_ui_helper.h:52</c> getRect()。</remarks>
    public Rect Area { get; }

    /// <summary>Colour of the box.</summary>
    /// <remarks>参考：<c>src/fheroes2/editor/editor_ui_helper.h:57</c> getColor()。</remarks>
    public PlayerColor Color => _color;

    /// <summary>The check mark sprite, so that a renderer without a Display layer can blit it after the box.</summary>
    /// <remarks>
    /// 参考：<c>src/fheroes2/editor/editor_ui_helper.h:67</c> 私有成员 <c>_checkmark</c>。
    /// 差异：参考没有这个访问器——勾选标记由 <c>MovableSprite::show</c> 直接画到 Display，外部看不见它。
    /// 本项目没有 Display，只能把它交给调用方，由调用方在框之后贴图。Display 移植后这个访问器可以去掉。
    /// </remarks>
    public MovableSprite Checkmark => _checkmark;

    /// <summary>
    /// <c>Editor::Checkbox::toggle</c>: flips the check mark and returns the new state.
    /// </summary>
    /// <returns><see langword="true"/> when the box is checked afterwards.</returns>
    /// <remarks>
    /// 参考：<c>src/fheroes2/editor/editor_ui_helper.cpp:57</c> Checkbox::toggle（声明
    /// <c>src/fheroes2/editor/editor_ui_helper.h:62</c>）：<c>isHidden() ? show() : hide();</c>，
    /// 然后 <c>return !isHidden();</c>。
    /// </remarks>
    public bool Toggle()
    {
        if (_checkmark.IsHidden)
        {
            _checkmark.Show();
        }
        else
        {
            _checkmark.Hide();
        }

        return !_checkmark.IsHidden;
    }

    private static Sprite RequireImage(int frameId) =>
        Assets.GetImage("CELLWIN.ICN", frameId)
        ?? throw new InvalidOperationException($"CELLWIN.ICN frame {frameId} is missing from the archives.");
}
