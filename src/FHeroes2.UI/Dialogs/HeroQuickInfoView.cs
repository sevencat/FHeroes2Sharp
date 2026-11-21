using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The hero quick info box, <c>showQuickInfo( const HeroBase &amp; )</c>
/// (<c>src/fheroes2/dialog/dialog_quickinfo.cpp:693-911</c>).
/// </summary>
/// <remarks>
/// <para>
/// 在 <c>QWIKHERO.ICN</c> 帧 0 上依次画：名字（完整信息时后缀 <c>" (Level N)"</c>，名字本身先被截到
/// <c>236 - 39 - 等级宽度</c>）、小肖像相框与小肖像、左士气右幸运两列 <c>MINILKMR</c> 图标（数量为 1 时下移 7 像素）、
/// 左右两面 <c>FLAG32</c> 旗子（中立英雄不画）、四维 + 魔法点 + 移动力六行、最后一整行军队。
/// </para>
/// <para>
/// 亡灵军团的士气图标先转灰再压暗（参考 <c>ApplyPalette</c> 两次），表示「没有士气」。不全的信息只画名字、相框与
/// 军队，其余整块省略——参考的 <c>isFullInfo</c> 分支就是这样。
/// </para>
/// <para>
/// 框里描述的东西是 <see cref="HeroQuickInfoSubject"/>：冒险英雄或者城镇船长。船长的等级与移动力为空，所以名字后面
/// 没有等级后缀、四维只有五行（<c>isActiveHero == false</c> 的那一支）。
/// </para>
/// <para>
/// <b>未搬</b>：参考的事件循环（按住右键期间一直显示）由场景负责；<c>CursorRestorer</c>/<c>ImageRestorer</c> 是
/// 参考的重绘优化，端口每帧整屏重画，不需要。
/// </para>
/// </remarks>
public sealed class HeroQuickInfoView
{
    /// <summary>Label of the attack row, <c>_( "Attack:" )</c>.</summary>
    public const string AttackLabel = "Attack:";

    /// <summary>Label of the defense row, <c>_( "Defense:" )</c>.</summary>
    public const string DefenseLabel = "Defense:";

    /// <summary>Label of the spell power row, <c>_( "Spell Power:" )</c>.</summary>
    public const string SpellPowerLabel = "Spell Power:";

    /// <summary>Label of the knowledge row, <c>_( "Knowledge:" )</c>.</summary>
    public const string KnowledgeLabel = "Knowledge:";

    /// <summary>Label of the spell points row, <c>_( "Spell Points:" )</c>.</summary>
    public const string SpellPointsLabel = "Spell Points:";

    /// <summary>Label of the move points row, <c>_( "Move Points:" )</c>.</summary>
    public const string MovePointsLabel = "Move Points:";

    /// <summary>Room the name loses to the box's shadow and border, <c>boxShadowAndBorder</c> (39).</summary>
    public const int NameInset = 39;

    private readonly HeroQuickInfoSubject _subject;
    private readonly bool _fullInfo;
    private readonly HeroQuickInfoArtwork _artwork;
    private readonly IGlyphSource _font;
    private readonly IMiniMonsterSprites _sprites;
    private readonly HeroQuickInfoLayout _layout;

    private HeroQuickInfoView(
        HeroQuickInfoSubject subject,
        bool fullInfo,
        HeroQuickInfoArtwork artwork,
        IGlyphSource font,
        IMiniMonsterSprites sprites,
        HeroQuickInfoLayout layout)
    {
        _subject = subject;
        _fullInfo = fullInfo;
        _artwork = artwork;
        _font = font;
        _sprites = sprites;
        _layout = layout;
    }

    /// <summary>Bounding box of the box on screen.</summary>
    public Rect Area => _layout.Box;

    /// <summary>Where each part of the box was placed.</summary>
    public HeroQuickInfoLayout Layout => _layout;

    /// <summary>The hero or captain the box describes.</summary>
    public HeroQuickInfoSubject Subject => _subject;

    /// <summary>Whether the box shows the stats and the army, <c>isFullInfo</c> of the reference.</summary>
    public bool IsFullInfo => _fullInfo;

    /// <summary>
    /// Builds the box next to the cursor.
    /// </summary>
    /// <param name="subject">The hero or captain to describe.</param>
    /// <param name="fullInfo">Whether the viewer may see the stats, <c>isFullInfo</c> of the reference.</param>
    /// <param name="artwork">Frames of the box.</param>
    /// <param name="font">Font of every string, <c>FontType::smallWhite()</c> in the reference.</param>
    /// <param name="sprites">Where the army line takes its portraits from.</param>
    /// <param name="cursor">Cursor of the mouse, which the box is placed next to.</param>
    /// <param name="interfaceArea">Area the box has to stay inside.</param>
    /// <returns>The box, or <see langword="null"/> when the box frame cannot fit the interface.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static HeroQuickInfoView? TryCreate(
        HeroQuickInfoSubject subject,
        bool fullInfo,
        HeroQuickInfoArtwork artwork,
        IGlyphSource font,
        IMiniMonsterSprites sprites,
        Point cursor,
        Rect interfaceArea)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(sprites);

        Size size = new(artwork.Box.Width, artwork.Box.Height);
        if (size.Width > interfaceArea.Width || size.Height > interfaceArea.Height)
        {
            return null;
        }

        Rect box = StandardPopupWindow.PositionOf(cursor, interfaceArea, size);

        // The left flag's width decides how far the left flag reaches; the neutral frame is the fallback for
        // a color the reference cannot paint.
        Sprite leftFlag = artwork.Flags[HeroQuickInfoArtwork.GetLeftFlagIndex(subject.Color)
            ?? HeroQuickInfoArtwork.NeutralFlagIndex];

        HeroQuickInfoLayout layout = HeroQuickInfoLayout.Create(
            box,
            new Size(artwork.PortraitFrame.Width, artwork.PortraitFrame.Height),
            new Size(leftFlag.Width, leftFlag.Height));

        return new HeroQuickInfoView(subject, fullInfo, artwork, font, sprites, layout);
    }

    /// <summary>Draws the box.</summary>
    /// <param name="target">The frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, _layout.Box.Position, _artwork.Box);
        DrawName(target);

        if (_subject.Portrait is { } portrait)
        {
            ImageOps.BlitSprite(target, _layout.PortraitFrame, _artwork.PortraitFrame);
            ImageOps.BlitSprite(target, _layout.GetPortrait(new Size(portrait.Width, portrait.Height)), portrait);
        }

        if (!_fullInfo)
        {
            Army.DrawMultipleMonsterLines(
                _subject.Army,
                _layout.Content.X - 6,
                _layout.Content.Y + 60,
                HeroQuickInfoLayout.ArmyLineWidth,
                false,
                false,
                false,
                0,
                target,
                _sprites);

            return;
        }

        DrawLuck(target);
        DrawMorale(target);
        DrawFlags(target);
        DrawStats(target);

        Army.DrawSingleDetailedMonsterLine(
            _subject.Army, _layout.Army.X, _layout.Army.Y, HeroQuickInfoLayout.ArmyLineWidth, target, _sprites);
    }

    /// <summary>
    /// How many icons a luck or morale value asks for, <c>count = ( 0 == value ? 1 : std::abs( value ) )</c>.
    /// </summary>
    /// <param name="value">Luck or morale of the hero.</param>
    /// <returns>The icon count, never zero.</returns>
    public static int GetIconCount(int value) => value == 0 ? 1 : Math.Abs(value);

    private void DrawName(PixelBuffer target)
    {
        string name = _subject.Name;

        if (_fullInfo && _subject.Level is { } level)
        {
            string suffix = $" (Level {level})";
            int room = _artwork.Box.Width - NameInset - GlyphText.Measure(_font, suffix);

            name = FitToOneRow(name, room) + suffix;
        }

        int width = GlyphText.Measure(_font, name);
        Point position = new(_layout.Content.X + ((_layout.Content.Width - width) / 2), _layout.Name.Y);

        GlyphText.DrawLine(_font, target, position, name);
    }

    private void DrawLuck(PixelBuffer target)
    {
        int luck = _subject.Luck;
        Sprite icon = _artwork.Icons[luck < 0 ? 0 : (luck > 0 ? 1 : 2)];
        Point position = _layout.GetLuck(GetIconCount(luck));

        for (int count = GetIconCount(luck); count > 0; --count)
        {
            ImageOps.BlitSprite(target, position, icon);
            position = new Point(position.X, position.Y + icon.Height - 1);
        }
    }

    private void DrawMorale(PixelBuffer target)
    {
        int morale = _subject.Morale;
        Sprite icon = _artwork.Icons[morale < 0 ? 3 : (morale > 0 ? 4 : 5)];

        // Undead armies have no morale: the icon is flattened twice, gray then darkened.
        PixelBuffer? grayed = null;
        if (_subject.Army.AllTroopsAreUndead())
        {
            grayed = icon.Image.Clone();
            ImageOps.ApplyPalette(grayed, PaletteId.Gray);
            ImageOps.ApplyPalette(grayed, PaletteId.Darkening);
        }

        Point position = _layout.GetMorale(GetIconCount(morale));
        for (int count = GetIconCount(morale); count > 0; --count)
        {
            if (grayed is { } buffer)
            {
                ImageOps.Blit(target, position, buffer, buffer.Bounds);
            }
            else
            {
                ImageOps.BlitSprite(target, position, icon);
            }

            position = new Point(position.X, position.Y + icon.Height - 1);
        }
    }

    private void DrawFlags(PixelBuffer target)
    {
        if (HeroQuickInfoArtwork.GetLeftFlagIndex(_subject.Color) is not { } index)
        {
            return;
        }

        ImageOps.BlitSprite(target, _layout.LeftFlag, _artwork.Flags[index]);
        ImageOps.BlitSprite(target, _layout.RightFlag, _artwork.Flags[index + 1]);
    }

    private void DrawStats(PixelBuffer target)
    {
        DrawStat(target, 0, AttackLabel, _subject.Attack.ToString());
        DrawStat(target, 1, DefenseLabel, _subject.Defense.ToString());
        DrawStat(target, 2, SpellPowerLabel, _subject.Power.ToString());
        DrawStat(target, 3, KnowledgeLabel, _subject.Knowledge.ToString());
        DrawStat(target, 4, SpellPointsLabel, $"{_subject.SpellPoints}/{_subject.MaxSpellPoints}");

        // A captain has no move points: the reference skips the whole row, leaving the space empty.
        if (_subject.MovePoints is { } movePoints && _subject.MaxMovePoints is { } maxMovePoints)
        {
            DrawStat(target, 5, MovePointsLabel, $"{movePoints}/{maxMovePoints}");
        }
    }

    private void DrawStat(PixelBuffer target, int row, string label, string value)
    {
        GlyphText.DrawLine(_font, target, _layout.GetStatLabel(row), FitToOneRow(label, HeroQuickInfoLayout.StatNumberColumn));
        GlyphText.DrawLine(_font, target, _layout.GetStatValue(row), value);
    }

    /// <summary>
    /// Cuts a string down until it fits a width, <c>fheroes2::Text::fitToOneRow()</c>.
    /// </summary>
    private string FitToOneRow(string text, int width)
    {
        if (GlyphText.Measure(_font, text) <= width)
        {
            return text;
        }

        int length = text.Length;
        while (length > 0 && GlyphText.Measure(_font, text[..length]) > width)
        {
            --length;
        }

        return text[..length];
    }
}
