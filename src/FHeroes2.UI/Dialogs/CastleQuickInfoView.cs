using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The castle quick info box, <c>showQuickInfo( const Castle &amp; )</c>
/// (<c>src/fheroes2/dialog/dialog_quickinfo.cpp:597-691</c>).
/// </summary>
/// <remarks>
/// <para>
/// 在 <c>QWIKTOWN.ICN</c> 帧 0 上依次画：城名（居中）、城图标（<c>LOCATORS</c> 帧 23 加种族小图标）、左右两面
/// <c>FLAG32</c> 旗、<c>"Defenders:"</c>，最后是把守军缩成两行的小肖像。
/// </para>
/// <para>
/// 守军分支与参考一致：看得清（同阵营或在魔法视野里）或自家有盗贼公会时画军队，否则画 <c>"Unknown"</c>；
/// 看得清但城里没兵时画 <c>"None"</c>。判「看得清」要读 <c>Settings</c> 与 <c>Kingdom</c>，端口把结论作为参数
/// 传进来，与 <c>isDetailedView</c> 的算法分开。
/// </para>
/// <para>
/// <b>未搬</b>：参考的 <c>CursorRestorer</c> / <c>ImageRestorer</c> / <c>RadarUpdater</c> 与「按住右键期间一直显示」
/// 的事件循环——端口每帧整屏重画，弹出时机与停留时间由场景决定。
/// </para>
/// </remarks>
public sealed class CastleQuickInfoView : IQuickInfoPanel
{
    /// <summary>Label above the garrison, <c>_( "Defenders:" )</c>.</summary>
    public const string DefendersLabel = "Defenders:";

    /// <summary>Shown when the viewer may see the garrison and it is empty, <c>_( "None" )</c>.</summary>
    public const string NoneLabel = "None";

    /// <summary>Shown when the viewer may not see the garrison, <c>_( "Unknown" )</c>.</summary>
    public const string UnknownLabel = "Unknown";

    /// <summary>Left inset of the box's content, <c>cur_rt.x + 22</c>.</summary>
    public const int ContentInsetX = 22;

    /// <summary>Top inset of the box's content, <c>cur_rt.y + 9</c>.</summary>
    public const int ContentInsetY = 9;

    /// <summary>Width of the box's content, <c>192</c>.</summary>
    public const int ContentWidth = 192;

    /// <summary>Height of the box's content, <c>154</c>.</summary>
    public const int ContentHeight = 154;

    /// <summary>Room the army lines may use, the <c>192</c> of <c>drawMultipleMonsterLines()</c>.</summary>
    public const int ArmyLineWidth = 192;

    /// <summary>How far the flags sit to the left and right of the icon, <c>flagOffset</c>.</summary>
    public const int FlagOffset = 5;

    /// <summary>How far down the flags sit from the icon's top, the <c>y</c> of <c>flagOffset( 5, 4 )</c>.</summary>
    public const int FlagOffsetY = 4;

    private readonly Castle _castle;
    private readonly bool _detailedView;
    private readonly uint _thievesGuildCount;
    private readonly CastleQuickInfoArtwork _artwork;
    private readonly IGlyphSource _font;
    private readonly IMiniMonsterSprites _sprites;
    private readonly IViewWorldSpriteSource _icons;
    private readonly bool _evilInterface;
    private readonly Rect _box;
    private readonly Rect _content;

    private CastleQuickInfoView(
        Castle castle,
        bool detailedView,
        uint thievesGuildCount,
        CastleQuickInfoArtwork artwork,
        IGlyphSource font,
        IMiniMonsterSprites sprites,
        IViewWorldSpriteSource icons,
        bool evilInterface,
        Rect box)
    {
        _castle = castle;
        _detailedView = detailedView;
        _thievesGuildCount = thievesGuildCount;
        _artwork = artwork;
        _font = font;
        _sprites = sprites;
        _icons = icons;
        _evilInterface = evilInterface;
        _box = box;
        _content = new Rect(box.X + ContentInsetX, box.Y + ContentInsetY, ContentWidth, ContentHeight);
    }

    /// <summary>Bounding box of the box on screen.</summary>
    public Rect Area => _box;

    /// <summary>Area the box draws its contents into, <c>cur_rt</c> of the reference.</summary>
    public Rect Content => _content;

    /// <summary>The castle the box describes.</summary>
    public Castle Subject => _castle;

    /// <summary>Whether the garrison may be shown, <c>isDetailedView</c> of the reference.</summary>
    public bool IsDetailedView => _detailedView;

    /// <summary>
    /// Builds the box either at a fixed position or next to the cursor.
    /// </summary>
    /// <param name="castle">The castle to describe.</param>
    /// <param name="detailedView">Whether the viewer may see the garrison, <c>isDetailedView</c> of the reference.</param>
    /// <param name="thievesGuildCount">Power of the viewer's thieves' guild, <c>GetCountThievesGuild()</c>.</param>
    /// <param name="artwork">Frames of the box.</param>
    /// <param name="font">Font of every string, <c>FontType::smallWhite()</c> in the reference.</param>
    /// <param name="sprites">Where the army line takes its portraits from.</param>
    /// <param name="icons">Where the castle icon takes <c>LOCATORS</c> from.</param>
    /// <param name="evilInterface">Whether the evil interface is in effect.</param>
    /// <param name="cursor">Cursor of the mouse, which the box is placed next to.</param>
    /// <param name="interfaceArea">Area the box has to stay inside.</param>
    /// <param name="position">
    /// Top left corner of the box when it is pinned, the <c>position</c> argument of <c>makeRectQuickInfo()</c>;
    /// when either coordinate is zero or negative the box is placed next to <paramref name="cursor"/> instead.
    /// </param>
    /// <returns>The box, or <see langword="null"/> when the box frame cannot fit the interface.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static CastleQuickInfoView? TryCreate(
        Castle castle,
        bool detailedView,
        uint thievesGuildCount,
        CastleQuickInfoArtwork artwork,
        IGlyphSource font,
        IMiniMonsterSprites sprites,
        IViewWorldSpriteSource icons,
        bool evilInterface,
        Point cursor,
        Rect interfaceArea,
        Point? position = null)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(icons);

        Size size = new(artwork.Box.Width, artwork.Box.Height);
        Rect box;

        if (position is { } pinned && pinned.X > 0 && pinned.Y > 0)
        {
            box = new Rect(pinned.X - size.Width, pinned.Y, size.Width, size.Height);
        }
        else
        {
            if (size.Width > interfaceArea.Width || size.Height > interfaceArea.Height)
            {
                return null;
            }

            box = StandardPopupWindow.PositionOf(cursor, interfaceArea, size);
        }

        return new CastleQuickInfoView(
            castle, detailedView, thievesGuildCount, artwork, font, sprites, icons, evilInterface, box);
    }

    /// <summary>Draws the box.</summary>
    /// <param name="target">The frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, _box.Position, _artwork.Box);

        DrawCentered(target, _castle.Name, _content.Y + 3);

        // The icon row is the anchor of everything below it: the flags sit beside it and the labels under it.
        Point icon = new(_content.X + ((_content.Width - _artwork.CastleIcon.Width) / 2), _content.Y + 13);
        ImageOps.BlitSprite(target, icon, _artwork.CastleIcon);
        CastleIconPainter.Draw(_castle, _icons, new Point(icon.X + 4, icon.Y + 4), target, _evilInterface);
        DrawFlags(target, icon);

        int defenders = icon.Y + _artwork.CastleIcon.Height + 2;
        DrawCentered(target, DefendersLabel, defenders);
        DrawGarrison(target, defenders);
    }

    private void DrawFlags(PixelBuffer target, Point icon)
    {
        if (HeroQuickInfoArtwork.GetLeftFlagIndex(_castle.Color) is not { } index)
        {
            return;
        }

        Sprite left = _artwork.Flags[index];
        ImageOps.BlitSprite(target, new Point(icon.X - FlagOffset - left.Width, icon.Y + FlagOffsetY), left);

        Sprite right = _artwork.Flags[index + 1];
        ImageOps.BlitSprite(
            target, new Point(icon.X + FlagOffset + _artwork.CastleIcon.Width, icon.Y + FlagOffsetY), right);
    }

    private void DrawGarrison(PixelBuffer target, int defenders)
    {
        if (!_detailedView && _thievesGuildCount == 0)
        {
            DrawCentered(target, UnknownLabel, defenders + 47);

            return;
        }

        if (!_castle.Army.IsValid)
        {
            DrawCentered(target, NoneLabel, defenders + 47);

            return;
        }

        Army.DrawMultipleMonsterLines(
            _castle.Army,
            _content.X - 1,
            defenders + 21,
            ArmyLineWidth,
            false,
            _detailedView,
            true,
            _thievesGuildCount,
            target,
            _sprites);
    }

    private void DrawCentered(PixelBuffer target, string text, int y)
    {
        int width = GlyphText.Measure(_font, text);

        GlyphText.DrawLine(_font, target, new Point(_content.X + ((_content.Width - width) / 2), y), text);
    }
}
