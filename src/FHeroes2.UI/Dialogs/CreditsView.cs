using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Credits;
using FHeroes2.Game.Gui;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The scrolling credits screen, <c>Game::ShowCredits()</c> (<c>game_credits.cpp:701-820</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把每一页画成一张 <c>CBKGLAVA</c> 背景大小的图（贴文本、贴怪物插图），再按主菜单背景的宽度缩放，最后整块
/// 用 <c>ApplyAlpha</c> 压到屏上；页高之外的余量放一条黑带当标题。端口沿用同一套流程，只是页面在构造时就按屏幕
/// 宽度画好，<see cref="Draw"/> 每帧只做一次「压 alpha」——参考那层 <c>ImageRestorer</c> 不需要，因为端口每帧重画整屏。
/// </para>
/// <para>
/// <b>标题条只淡入一次</b>：<c>fadeInHeader</c> 标志在参考里随第一页的淡入一起关掉（<c>:757-759</c>），之后每次换页
/// 只重画页、不再重画它。端口的 <see cref="CreditsSequence.IsFirstFadeIn"/> 就是那个标志。
/// </para>
/// <para>
/// <b>插图没搬</b>：参考在页面空隙里贴黑龙、牛头人、凤凰等怪物图（<c>ICN::DRAGBLAK</c>、<c>ICN::MINOTAUR</c> …）。
/// 端口只排文本；缺插图不影响名单的可读性，而把十几张图的位置逐一对准是另一件事。
/// </para>
/// </remarks>
public sealed class CreditsView
{
    /// <summary>Frame of <c>CBKGLAVA</c> every page is painted on, <c>ICN::CBKGLAVA, 0</c>.</summary>
    public const int BackgroundFrame = 0;

    /// <summary>Vertical offset of the header caption inside its strip, the <c>17</c> of <c>:161</c>.</summary>
    public const int HeaderTextTop = 17;

    private readonly IReadOnlyList<CreditsPage> _pages;
    private readonly IReadOnlyList<Sprite> _rendered;
    private readonly Sprite? _header;
    private readonly int _headerHeight;
    private readonly int _pageHeight;

    private CreditsView(IReadOnlyList<CreditsPage> pages, IReadOnlyList<Sprite> rendered, Sprite? header,
        int headerHeight, int pageHeight)
    {
        _pages = pages;
        _rendered = rendered;
        _header = header;
        _headerHeight = headerHeight;
        _pageHeight = pageHeight;
    }

    /// <summary>The pages this run shows.</summary>
    public IReadOnlyList<CreditsPage> Pages => _pages;

    /// <summary>Height of the title strip above the pages.</summary>
    public int HeaderHeight => _headerHeight;

    /// <summary>Height of one page on screen.</summary>
    public int PageHeight => _pageHeight;

    /// <summary>
    /// Builds the screen: every page is painted once at <paramref name="screen"/>'s width, the title strip is made
    /// above it.
    /// </summary>
    /// <param name="isPriceOfLoyaltySupported">Whether the expansion pages are included.</param>
    /// <param name="headerTitle">Text of the strip above the pages, <c>fheroes2 engine ( version )</c> in the reference.</param>
    /// <param name="titleFont">Yellow font of the entry headings.</param>
    /// <param name="bodyFont">White font of the entry bodies.</param>
    /// <param name="screen">Screen the pages are sized for, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The view, or <see langword="null"/> with the reason in the log when <c>CBKGLAVA</c> is missing.</returns>
    /// <exception cref="ArgumentNullException">A font or the title is <see langword="null"/>.</exception>
    public static CreditsView? TryCreate(bool isPriceOfLoyaltySupported, string headerTitle,
        IGlyphSource titleFont, IGlyphSource bodyFont, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(headerTitle);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        Size size = screen ?? ScreenBuffer.DefaultSize;

        if (GameAssets.GetImage(GameAssets.GetFileName(IcnType.Cbkglava), BackgroundFrame) is not { } background)
        {
            GameLog.Warn("The credits need CBKGLAVA, and the archive is missing it.");

            return null;
        }

        // 参考按主菜单背景的宽度缩放每一页，页高随宽度等比走，余下的高度留给标题条。
        int pageHeight = size.Width * background.Height / background.Width;

        if (pageHeight <= 0 || pageHeight >= size.Height)
        {
            GameLog.Warn($"The credits background is {background.Width}x{background.Height}, too tall for a {size.Width}x{size.Height} screen.");

            return null;
        }

        IReadOnlyList<CreditsPage> pages = CreditsContent.Create(isPriceOfLoyaltySupported);
        List<Sprite> rendered = [];

        foreach (CreditsPage page in pages)
        {
            rendered.Add(RenderPage(page, background, size.Width, pageHeight, titleFont, bodyFont));
        }

        Sprite? header = RenderHeader(size.Width, size.Height - pageHeight, headerTitle, titleFont);

        return new CreditsView(pages, rendered, header, size.Height - pageHeight, pageHeight);
    }

    /// <summary>
    /// Paints the current page of <paramref name="sequence"/> and, on the first fade, the title strip,
    /// one frame of the reference's loop (<c>:782-806</c>).
    /// </summary>
    /// <param name="target">Screen buffer to paint on.</param>
    /// <param name="palette">Game palette, needed by the alpha blend.</param>
    /// <param name="sequence">The sequence giving the page and its brightness.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, Palette palette, CreditsSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(sequence);

        int pageTop = _headerHeight;
        int pageLeft = (target.Width - _rendered[0].Width) / 2;
        byte alpha = sequence.Alpha;

        // 参考把页与标题分两次压 alpha；alpha 归零时那两块就是纯黑，直接填 0 更省事。
        if (alpha == 0)
        {
            ImageOps.FillRect(target, new Rect(pageLeft, 0, _rendered[0].Width, _pageHeight + _headerHeight), 0);

            return;
        }

        if (sequence.IsFirstFadeIn && _header is { } header)
        {
            ImageOps.ApplyAlpha(header.Image, header.Image.Bounds, target, new Point(pageLeft, 0), alpha, palette);
        }
        else
        {
            ImageOps.FillRect(target, new Rect(pageLeft, 0, _rendered[0].Width, _headerHeight), 0);
        }

        Sprite page = _rendered[sequence.PageId];
        Point pagePosition = new(pageLeft, pageTop);

        if (alpha == CreditsSequence.FullAlpha)
        {
            ImageOps.BlitSprite(target, pagePosition, page);
        }
        else
        {
            ImageOps.ApplyAlpha(page.Image, page.Image.Bounds, target, pagePosition, alpha, palette);
        }
    }

    /// <summary>
    /// Paints one page at the width the screen gives it, the <c>generate*Credits*Page()</c> functions of the
    /// reference plus their <c>Resize</c>.
    /// </summary>
    /// <param name="page">The page to paint.</param>
    /// <param name="background"><c>CBKGLAVA</c> frame 0.</param>
    /// <param name="width">Width the page takes on screen.</param>
    /// <param name="height">Height the page takes on screen.</param>
    /// <param name="titleFont">Font of the headings.</param>
    /// <param name="bodyFont">Font of the bodies.</param>
    /// <returns>The page, ready to be blitted.</returns>
    private static Sprite RenderPage(CreditsPage page, Sprite background, int width, int height,
        IGlyphSource titleFont, IGlyphSource bodyFont)
    {
        var canvas = new Sprite(width, height);

        ImageOps.Resize(background, canvas);

        int columnWidth = page.TextWidth;
        int columnStep = page.IsCentered ? 0 : CreditsPage.ColumnStep;
        int columns = page.ColumnCount;
        int usedHeight = height - CreditsPage.EntryGap;

        int column = 0;
        int y = page.TextTop;

        foreach (CreditsEntry entry in page.Entries)
        {
            Text title = new(entry.Title, FontType.NormalYellow());
            Text body = new(entry.Body, FontType.NormalWhite());

            int entryHeight = title.Height(columnWidth) + body.Height(columnWidth);

            // 一列摆满就换下一列，与参考按列画的手写布局同效。
            if (y + entryHeight > usedHeight && column + 1 < columns)
            {
                ++column;
                y = page.TextTop;
            }

            int columnLeft = page.IsCentered
                ? (width - columnWidth) / 2
                : column * columnStep;

            if (entry.Title.Length > 0)
            {
                title.Draw(columnLeft, y, columnWidth, canvas.Image);
                y += title.Height(columnWidth);
            }

            if (entry.Body.Length > 0)
            {
                body.Draw(columnLeft, y, columnWidth, canvas.Image);
                y += body.Height(columnWidth);
            }

            y += CreditsPage.EntryGap;
        }

        return canvas;
    }

    /// <summary>
    /// Paints the strip above the pages: black with the engine caption centred, <c>generateHeader()</c>
    /// (<c>:152-171</c>).
    /// </summary>
    /// <param name="width">Width of the strip.</param>
    /// <param name="height">Height of the strip.</param>
    /// <param name="title">Caption text.</param>
    /// <param name="font">Yellow font of the caption.</param>
    /// <returns>The strip, or <see langword="null"/> when it would be too short to hold the caption.</returns>
    private static Sprite? RenderHeader(int width, int height, string title, IGlyphSource font)
    {
        if (height <= HeaderTextTop)
        {
            return null;
        }

        var canvas = new Sprite(width, height);

        Text caption = new(title, FontType.NormalYellow());

        caption.Draw((width - caption.Width()) / 2, HeaderTextTop, canvas.Image);

        return canvas;
    }
}
