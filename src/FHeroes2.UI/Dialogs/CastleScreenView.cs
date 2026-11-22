using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The town screen: background, buildings, portraits, army bars, town name, resource panel and status bar,
/// <c>Castle::OpenDialog()</c> (<c>castle_dialog.cpp:325-398</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这一层只画与命中，不跑循环：切城、点建筑、开子窗口都由调用方按 <see cref="CastleDialog.ReturnValue"/> 决定
/// （<c>castle_dialog.cpp:470-859</c> 的循环留给场景层）。
/// </para>
/// <para>
/// 绘制顺序照参考：建筑层 → 底部条与像/军队 → EXIT → 资源面板 → 城名牌 → 状态条。建筑层被裁到背景矩形，
/// 资源面板在它之外，所以两者的先后不影响像素；EXIT 由资源面板画（参考的面板自己盖这一帧），这里只在按下时
/// 换成第二帧。
/// </para>
/// <para>
/// 旗帜的玩家配色已由 <see cref="CastleFlagPalette"/> 接管（<c>getModifiedByColorImage</c>）；未搬的只剩
/// <c>AlphaBlit</c> 的淡入（<see cref="CastleScreenPainter"/>）；状态文字不做滚动，超宽时截断。
/// </para>
/// </remarks>
public sealed class CastleScreenView
{
    private readonly CastleScreenArtwork _artwork;
    private readonly IGlyphSource _font;

    private ArmyBar? _castleBar;
    private ArmyBar? _heroBar;

    /// <summary>
    /// Builds a view on top of already-loaded artwork.
    /// </summary>
    /// <param name="artwork">The frames of the screen.</param>
    /// <param name="smallFont">The small white font the numbers, the town name and the status line use.</param>
    /// <param name="offset">Where the 640×480 screen sits on the target buffer.</param>
    public CastleScreenView(CastleScreenArtwork artwork, IGlyphSource smallFont, Point offset = default)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(smallFont);

        _artwork = artwork;
        _font = smallFont;

        Layout = CastleDialogLayout.Create(
            offset,
            new Size(artwork.StatusBar.Width, artwork.StatusBar.Height),
            new Size(artwork.Crest.Width, artwork.Crest.Height),
            new Size(artwork.PreviousButton.Width, artwork.PreviousButton.Height));
    }

    /// <summary>Where everything goes, <see cref="CastleDialogLayout"/>.</summary>
    public CastleDialogLayout Layout { get; }

    /// <summary>The EXIT button's rectangle, <c>buttonExit.area()</c>.</summary>
    public Rect ExitButtonArea => Layout.GetExitButtonArea(_artwork.ExitButton);

    /// <summary>The town currently shown, or <see langword="null"/> before <see cref="Show"/>.</summary>
    public Castle? Castle { get; private set; }

    /// <summary>The hero standing in the town, or <see langword="null"/>.</summary>
    public Hero? Hero { get; private set; }

    /// <summary>The garrison bar, whose selection state the caller drives.</summary>
    public ArmyBar? CastleArmyBar => _castleBar;

    /// <summary>The visiting hero's bar, or <see langword="null"/> when no hero stands in the town.</summary>
    public ArmyBar? HeroArmyBar => _heroBar;

    /// <summary>
    /// Points the view at a town, rebuilding the two army bars, <c>castle_dialog.cpp:352-360</c>.
    /// </summary>
    /// <param name="castle">The town to show.</param>
    /// <param name="hero">The hero standing on it, or <see langword="null"/>.</param>
    public void Show(Castle castle, Hero? hero)
    {
        ArgumentNullException.ThrowIfNull(castle);

        Castle = castle;
        Hero = hero;

        _castleBar = CreateBar(castle.Army, Layout.CastleArmyBarOrigin);
        _heroBar = hero is null ? null : CreateBar(hero.Army, Layout.HeroArmyBarOrigin);
    }

    /// <summary>
    /// Paints the whole screen.
    /// </summary>
    /// <param name="target">The frame buffer.</param>
    /// <param name="frame">The dynamic half of the picture: treasury, animation, fade, status line.</param>
    /// <exception cref="InvalidOperationException">No town has been shown yet.</exception>
    public void Draw(PixelBuffer target, CastleScreenFrame frame)
    {
        ArgumentNullException.ThrowIfNull(target);

        Castle castle = Castle ?? throw new InvalidOperationException("Show() must be called before Draw().");

        CastleScreenPainter.Draw(target, castle, Layout.Offset, frame.AnimationIndex, frame.Fade, frame.World);

        // The strip is the plate the portraits and army bars stand on (ICN::STRIP, 0 at +256).
        Point stripPosition = new(Layout.Offset.X, Layout.Offset.Y + 256);
        ImageOps.BlitSprite(target, stripPosition, _artwork.StripBackground);

        DrawCrestOrCaptain(target, castle, frame);
        DrawHeroSlot(target, frame);

        _castleBar?.Redraw(target, _artwork.ArmySprites);

        if (_heroBar is not null && frame.HeroAlpha > 0)
        {
            _heroBar.Redraw(target, _artwork.ArmySprites);
        }

        DrawStatusBar(target, frame);
        DrawTownName(target, castle);
        DrawResourcePanel(target, frame);
    }

    /// <summary>
    /// The building under a point, <c>castle_dialog.cpp:416-435</c>.
    /// </summary>
    /// <param name="cursor">The mouse position.</param>
    /// <returns>The building, or <see cref="BuildingType.Nothing"/>.</returns>
    public BuildingType FindBuildingAt(Point cursor) =>
        Castle is { } castle ? CastleBuildingHitTest.Find(castle, Layout.Offset, cursor) : BuildingType.Nothing;

    /// <summary>
    /// What the status bar says under a point, <c>castle_dialog.cpp:402-451</c>.
    /// </summary>
    /// <remarks>
    /// 优先级照参考：EXIT → 资源面板 → 上一个城 → 下一个城 → 英雄像 → 建筑。一个都没命中时返回空串，调用方据此
    /// 显示日期行（参考 <c>castle_dialog.cpp:455-457</c>）。
    /// </remarks>
    /// <param name="cursor">The mouse position.</param>
    /// <returns>The message, or <see cref="string.Empty"/> when the pointer is over empty ground.</returns>
    public string GetStatusMessage(Point cursor)
    {
        if (Castle is not { } castle)
        {
            return string.Empty;
        }

        if (ExitButtonArea.Contains(cursor))
        {
            return castle.IsBuild(BuildingType.Castle) ? "Exit Castle" : "Exit Town";
        }

        if (Layout.ResourceHitArea.Contains(cursor))
        {
            return "Show Income";
        }

        if (Layout.PreviousCastleButton.Contains(cursor))
        {
            return "Show previous town";
        }

        if (Layout.NextCastleButton.Contains(cursor))
        {
            return "Show next town";
        }

        if (Hero is not null && Layout.HeroPortraitArea.Contains(cursor))
        {
            return "View Hero";
        }

        BuildingType building = FindBuildingAt(cursor);

        return building == BuildingType.Nothing
            ? string.Empty
            : CastleDialog.GetStatusLine(castle.Race, building);
    }

    private ArmyBar CreateBar(FHeroes2.Game.Army.Army army, Point origin)
    {
        ArmyBar bar = new(army, miniSprites: false, readOnly: false);

        // 不用小图标的兵栏靠 STRIP 帧 2 画每个空格子（格子尺寸也由它来定，`army_bar.cpp:198-201`），
        // 帧 1 是被选中时的框（`army_bar.cpp:300`）。不设这一对，格子既没有底板也没有 43×43 的尺寸。
        bar.SetStripBackground(_artwork.ArmySlot, _artwork.ArmyCursor);

        bar.SetTableSize(new Size(CastleDialogLayout.ArmyBarColumns, 1));
        bar.SetInBetweenItemsOffset(new Size(CastleDialogLayout.ArmyBarItemSpacing, 0));
        bar.SetRenderingOffset(origin);

        return bar;
    }

    private void DrawCrestOrCaptain(PixelBuffer target, Castle castle, CastleScreenFrame frame)
    {
        Sprite portrait = castle.IsBuild(BuildingType.Captain) && frame.CaptainPortrait is { } captain
            ? captain
            : _artwork.Crest;

        ImageOps.BlitSprite(target, new Point(Layout.CrestArea.X, Layout.CrestArea.Y), portrait);
    }

    private void DrawHeroSlot(PixelBuffer target, CastleScreenFrame frame)
    {
        if (_heroBar is not null && frame.HeroPortrait is { } portrait && frame.HeroAlpha > 0)
        {
            ImageOps.BlitSprite(target, new Point(Layout.HeroPortraitArea.X, Layout.HeroPortraitArea.Y), portrait);
            return;
        }

        // No hero in town: the reference paints the empty portrait and the empty army plate instead of a bar.
        ImageOps.BlitSprite(
            target,
            new Point(Layout.HeroPortraitArea.X, Layout.HeroPortraitArea.Y),
            _artwork.EmptyHeroPortrait);

        ImageOps.BlitSprite(
            target,
            new Point(Layout.HeroArmyBarOrigin.X, Layout.HeroArmyBarOrigin.Y),
            _artwork.EmptyHeroArmy);
    }

    private void DrawTownName(PixelBuffer target, Castle castle)
    {
        Point platePosition = Layout.GetTownNamePosition(_artwork.NamePlate);
        ImageOps.BlitSprite(target, platePosition, _artwork.NamePlate);

        string name = castle.Name;
        int width = GlyphText.Measure(_font, name);

        Point textPosition = new(
            Layout.Offset.X + ((CastleDialogLayout.ScreenSize.Width - width) / 2),
            platePosition.Y + 2);

        GlyphText.DrawLine(_font, target, textPosition, name);
    }

    private void DrawStatusBar(PixelBuffer target, CastleScreenFrame frame)
    {
        ImageOps.BlitSprite(target, new Point(Layout.PreviousCastleButton.X, Layout.PreviousCastleButton.Y),
            frame.PreviousPressed ? _artwork.PreviousPressed : _artwork.PreviousButton);
        ImageOps.BlitSprite(target, new Point(Layout.StatusBarArea.X, Layout.StatusBarArea.Y), _artwork.StatusBar);
        ImageOps.BlitSprite(target, new Point(Layout.NextCastleButton.X, Layout.NextCastleButton.Y),
            frame.NextPressed ? _artwork.NextPressed : _artwork.NextButton);

        if (frame.StatusText.Length == 0)
        {
            return;
        }

        // 参考把整行文字居中并截到状态条宽度（StatusBar::updateMessage 的 fitToOneRow），这里同样先截再居中。
        string text = FitToWidth(_font, frame.StatusText, Layout.StatusTextArea.Width);
        int width = GlyphText.Measure(_font, text);

        Point position = new(
            Layout.StatusTextArea.X + ((Layout.StatusTextArea.Width - width) / 2),
            Layout.StatusTextArea.Y + ((Layout.StatusTextArea.Height - _font.LineHeight) / 2));

        GlyphText.DrawLine(_font, target, position, text, Layout.StatusTextArea);
    }

    private void DrawResourcePanel(PixelBuffer target, CastleScreenFrame frame)
    {
        Sprite exitFrame = frame.ExitPressed ? _artwork.ExitPressed : _artwork.ExitButton;

        ResourcePanel.Draw(target, frame.Funds, Layout.Offset, _font, _artwork.ResourceIcons, exitFrame);
    }

    /// <summary>
    /// Cuts a line down to the last character that still fits, the way <c>fitToOneRow()</c> does before the status
    /// bar draws it.
    /// </summary>
    private static string FitToWidth(IGlyphSource font, string text, int width)
    {
        if (GlyphText.Measure(font, text) <= width)
        {
            return text;
        }

        int end = text.Length - 1;

        while (end > 0 && GlyphText.Measure(font, text[..end]) > width)
        {
            --end;
        }

        return text[..end];
    }
}

/// <summary>
/// The part of the town screen that changes from frame to frame, as opposed to the static
/// <see cref="CastleScreenArtwork"/>.
/// </summary>
public sealed class CastleScreenFrame
{
    /// <summary>The kingdom's treasury, drawn in the resource panel.</summary>
    public Funds Funds { get; init; }

    /// <summary>The town's animation counter, <c>castle_dialog.cpp:573</c>.</summary>
    public uint AnimationIndex { get; init; }

    /// <summary>The building fading in, or a default <see cref="FadeBuilding"/> when none is.</summary>
    public FadeBuilding Fade { get; init; } = new();

    /// <summary>The world, only used to ask whether a boat lies near the shipyard.</summary>
    public World? World { get; init; }

    /// <summary>The status line; the caller passes the date line when nothing is hovered.</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>The hero portrait of the visiting hero, or <see langword="null"/>.</summary>
    public Sprite? HeroPortrait { get; init; }

    /// <summary>The captain portrait while the captain's quarters stand, or <see langword="null"/>.</summary>
    public Sprite? CaptainPortrait { get; init; }

    /// <summary>How opaque the visiting hero is while he fades out, <c>255</c> when fully visible.</summary>
    public int HeroAlpha { get; init; } = 255;

    /// <summary>Whether EXIT is held down.</summary>
    public bool ExitPressed { get; init; }

    /// <summary>Whether the "previous town" arrow is held down.</summary>
    public bool PreviousPressed { get; init; }

    /// <summary>Whether the "next town" arrow is held down.</summary>
    public bool NextPressed { get; init; }
}
