using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Battle;

/// <summary>
/// What the hero options window is closed with, the reference's <c>result</c> of
/// <c>DialogBattleHero()</c> (<c>battle_dialogs.cpp:855-1085</c>).
/// </summary>
public enum BattleHeroOptionsAction
{
    /// <summary>Nothing was chosen yet; the window stays open.</summary>
    None = 0,

    /// <summary>The hero asked to cast a spell, the reference's <c>result = 1</c>.</summary>
    CastSpell = 1,

    /// <summary>The hero asked to retreat, the reference's <c>result = 2</c>.</summary>
    Retreat = 2,

    /// <summary>The hero asked to surrender, the reference's <c>result = 3</c>.</summary>
    Surrender = 3,

    /// <summary>The window is dismissed now: close hotkey, CLOSE button, or the right button released.</summary>
    Close = 4,
}

/// <summary>
/// The hero or captain options of the battle, <c>Battle::Arena::DialogBattleHero()</c>
/// (<c>battle_dialogs.cpp:855-1085</c>).
/// </summary>
/// <remarks>
/// <para>
/// 一个窗口顶三个入口：立绘、四色块按钮（<c>VIEWGEN</c> 9~16），左边四项主属性 + 士气/幸运/法力。
/// <c>readonly</c> 的含义与参考一致——不是自己回合（<see cref="IsReadOnly"/>）或者调用方不要按钮——
/// 此时四个按钮全暗，只剩 CLOSE。
/// </para>
/// <para>
/// 三处与参考的差异：
/// </para>
/// <para>
/// 一、参考是一个模态循环，里面有「等结果」和「右键松开就退」两种走法；端口按本项目的惯例拆成
/// <see cref="ProcessEvents"/>/<see cref="Draw"/>，由调用方驱动，按钮变成 <c>buttons = false</c> 时
/// <see cref="ProcessEvents"/> 只回答「右键松开了没有」。
/// </para>
/// <para>
/// 二、右键四个按钮弹出的帮助说明（<c>showStandardTextMessage</c>）没有接，端口还没有这条通用消息框的
/// 战斗入口；立绘点头像打开的英雄屏也没有接，只把「这一帧点头像了」写成
/// <see cref="PortraitClicked"/> 交给调用方。
/// </para>
/// <para>
/// 三、参考的 <c>ImageRestorer</c> 不需要：端口每帧从底图重画整块窗口。
/// </para>
/// </remarks>
public sealed class BattleHeroOptionsWindow
{
    /// <summary>Width of the reference's <c>dialogShadow</c>, the left side of the frame.</summary>
    public const int DialogShadow = 15;

    /// <summary>Font compensation for the letter shadow, <c>letterShadowCompensation</c>.</summary>
    public const int LetterShadowCompensation = 1;

    /// <summary><c>portraitArea</c> of the reference, inside <see cref="Area"/>.</summary>
    public static readonly Rect PortraitArea = new(7, 35, 113, 108);

    /// <summary>X of the portrait inside <see cref="Area"/>.</summary>
    public const int PortraitOffsetX = 12;

    /// <summary>Y of the portrait inside <see cref="Area"/>.</summary>
    public const int PortraitOffsetY = 42;

    /// <summary>X of the colour block inside <see cref="Area"/>.</summary>
    public const int ColorBlockOffsetX = 133;

    /// <summary>Y of the colour block inside <see cref="Area"/>.</summary>
    public const int ColorBlockOffsetY = 36;

    /// <summary>Y of the title baseline inside <see cref="Area"/>, before the two pixel glyph offset.</summary>
    public const int TitleOffsetY = 11;

    /// <summary>Two pixel glyph offset the reference adds to every text run, <c>tp.y + 2</c>.</summary>
    public const int TextOffsetY = 2;

    /// <summary>X of the stats column inside <see cref="Area"/>, <c>pos_rt.x + 133 + 1</c>.</summary>
    public const int StatsOffsetX = ColorBlockOffsetX + LetterShadowCompensation;

    /// <summary>Y of the stats column inside <see cref="Area"/>.</summary>
    public const int StatsOffsetY = 40;

    /// <summary>Width the stats column is wrapped to, <c>maxStatsTextWidth</c>.</summary>
    public const int StatsWidth = 109;

    /// <summary>Row height of the four primary skills, <c>statsTextRowHeight</c>.</summary>
    public const int StatsRowHeight = 11;

    /// <summary>The distance between the first stat row and the spell points row used by the compensation.</summary>
    public const int SpellPointsCompensationBase = 88;

    /// <summary>Y of the spell points row inside <see cref="Area"/>, relative to <see cref="StatsOffsetY"/>.</summary>
    public const int SpellPointsOffsetY = 79;

    /// <summary>How far the spell points move down when morale and luck take four lines together.</summary>
    public const int SpellPointsCompensation = 11;

    /// <summary>Y of the four buttons inside <see cref="Area"/>.</summary>
    public const int ButtonOffsetY = 148;

    private readonly Rect _frame;
    private readonly Rect _area;
    private readonly Sprite _background;
    private readonly Sprite _colorBlock;
    private readonly Sprite? _portrait;
    private readonly IArmyCommander _commander;
    private readonly string _name;
    private readonly string _title;
    private readonly int _knowledge;
    private readonly bool _isCaptain;
    private readonly bool _isControllable;
    private readonly bool _readOnly;
    private readonly bool _buttons;
    private readonly ButtonSprite _cast;
    private readonly ButtonSprite _retreat;
    private readonly ButtonSprite _surrender;
    private readonly ButtonSprite _close;
    private string _statusMessage;

    private BattleHeroOptionsWindow(Rect frame, Sprite background, Sprite colorBlock, Sprite? portrait,
        IArmyCommander commander, bool buttons, bool canRetreat, bool canSurrender, PlayerColor currentColor,
        ButtonSprite cast, ButtonSprite retreat, ButtonSprite surrender, ButtonSprite close)
    {
        _frame = frame;
        _area = ActiveAreaFor(frame);
        _background = background;
        _colorBlock = colorBlock;
        _portrait = portrait;
        _commander = commander;
        _name = GetName(commander);
        _title = TitleFor(commander);
        _knowledge = GetKnowledge(commander);
        _isCaptain = !commander.IsHeroes;
        _isControllable = currentColor == commander.GetColor();
        _readOnly = !_isControllable || !buttons;
        _buttons = buttons;
        _cast = cast;
        _retreat = retreat;
        _surrender = surrender;
        _close = close;
        _statusMessage = _isCaptain ? "Captain's Options" : "Hero's Options";

        if (_readOnly || !commander.HasSpellBook || (commander.Modes & HeroModes.Spellcasted) != 0)
        {
            _cast.Disable();
        }

        if (_readOnly || !canRetreat)
        {
            _retreat.Disable();
        }

        if (_readOnly || !canSurrender)
        {
            _surrender.Disable();
        }

        if (!buttons)
        {
            _close.Disable();
        }
    }

    /// <summary>The whole dialog image including its shadow, the reference's <c>pos_rt</c> before the shift.</summary>
    public Rect Frame => _frame;

    /// <summary>
    /// The part of the frame the content is laid out in: the reference shifts <c>pos_rt</c> 15 pixels right after
    /// blitting the backdrop, because the first 15 columns of the artwork are shadow.
    /// </summary>
    public Rect Area => _area;

    /// <summary>Whether the window only shows what it was handed with and waits for the right button to go up.</summary>
    public bool IsReadOnly => _readOnly;

    /// <summary>Whether the commander is a battle-only captain, <c>isCaptain()</c>.</summary>
    public bool IsCaptain => _isCaptain;

    /// <summary>Whether the commander belongs to the side whose turn it is.</summary>
    public bool IsControllable => _isControllable;

    /// <summary>The name the title is built from, <c>hero.GetName()</c>.</summary>
    public string Name => _name;

    /// <summary>The title line, <c>"%{name} the %{race}"</c> or <c>"Captain of %{name}"</c>.</summary>
    public string Title => _title;

    /// <summary>The advice line of the battle status, <c>status.setMessage()</c> of the reference.</summary>
    public string StatusMessage => _statusMessage;

    /// <summary>The CAST SPELL button.</summary>
    public ButtonSprite CastButton => _cast;

    /// <summary>The RETREAT button.</summary>
    public ButtonSprite RetreatButton => _retreat;

    /// <summary>The SURRENDER button.</summary>
    public ButtonSprite SurrenderButton => _surrender;

    /// <summary>The CLOSE button.</summary>
    public ButtonSprite CloseButton => _close;

    /// <summary>
    /// Whether the portrait was clicked in the last <see cref="ProcessEvents"/> call with the Hero Screen available.
    /// The caller opens the hero dialog; the port's battle does not have that dialog wired yet.
    /// </summary>
    public bool PortraitClicked { get; private set; }

    /// <summary>Whether the reference would offer the Hero Screen for this commander.</summary>
    public bool CanOpenHeroScreen => _isControllable && !_isCaptain;

    /// <summary>
    /// The dialog rect of a screen, <c>( ( display.width() - w - 15 ) / 2, ( display.height() - h - 15 ) / 2, w, h )</c>
    /// (<c>battle_dialogs.cpp:869</c>).
    /// </summary>
    /// <param name="screen">Screen the dialog is centered on.</param>
    /// <param name="dialog">Size of the <c>VGENBKG</c> frame.</param>
    /// <returns>The dialog rect, shadow included.</returns>
    public static Rect FrameFor(Size screen, Size dialog) => new(
        (screen.Width - dialog.Width - DialogShadow) / 2,
        (screen.Height - dialog.Height - DialogShadow) / 2,
        dialog.Width,
        dialog.Height);

    /// <summary>The content rect inside <paramref name="frame"/>, the reference's shifted <c>pos_rt</c>.</summary>
    /// <param name="frame">The dialog rect.</param>
    /// <returns>The content rect.</returns>
    public static Rect ActiveAreaFor(Rect frame) =>
        new(frame.X + DialogShadow, frame.Y, frame.Width - DialogShadow, frame.Height);

    /// <summary>The portrait hit box of a dialog, <c>portraitArea</c>.</summary>
    /// <param name="area">The content rect, <see cref="Area"/>.</param>
    /// <returns>The hit box.</returns>
    public static Rect PortraitAreaFor(Rect area) => new(area.X + PortraitArea.X, area.Y + PortraitArea.Y,
        PortraitArea.Width, PortraitArea.Height);

    /// <summary>X offset of a button inside the content rect, <c>pos_rt.x + 15/74/133/192</c>.</summary>
    /// <param name="button">Zero for cast, one for retreat, two for surrender, three for close.</param>
    /// <returns>The offset; the close button's for an index outside the range.</returns>
    public static int ButtonOffsetX(int button) => button switch
    {
        0 => 15,
        1 => 74,
        2 => 133,
        _ => 192,
    };

    /// <summary>The title of a commander, <c>"Captain of %{name}"</c> or <c>"%{name} the %{race}"</c>.</summary>
    /// <param name="commander">The hero or captain.</param>
    /// <returns>The title line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="commander"/> is <see langword="null"/>.</exception>
    public static string TitleFor(IArmyCommander commander)
    {
        ArgumentNullException.ThrowIfNull(commander);

        return commander.IsHeroes
            ? $"{GetName(commander)} the {RaceNames.Get(commander.Race)}"
            : $"Captain of {GetName(commander)}";
    }

    /// <summary>
    /// The frame of the colour block in <c>VIEWGEN</c>, <c>( color == NONE ? 1 : Color::GetIndex( color ) + 1 )</c>.
    /// </summary>
    /// <param name="color">The commander's colour.</param>
    /// <returns>The frame index.</returns>
    public static int ColorBlockFrame(PlayerColor color) =>
        color == PlayerColor.None ? 1 : PlayerColors.GetIndex(color) + 1;

    /// <summary>
    /// Builds the dialog, <c>DialogBattleHero()</c> (<c>battle_dialogs.cpp:855-1085</c>).
    /// </summary>
    /// <param name="sprites">Source of the <c>VGENBKG</c>/<c>VIEWGEN</c>/portrait frames.</param>
    /// <param name="commander">The hero or the captain the dialog is about.</param>
    /// <param name="buttons">
    /// Whether the four buttons are wanted; <see langword="false"/> is the reference's right-click invocation that
    /// only waits for the right button to be released.
    /// </param>
    /// <param name="canRetreat"><c>CanRetreatOpponent( hero.GetColor() )</c> of the arena.</param>
    /// <param name="canSurrender"><c>CanSurrenderOpponent( hero.GetColor() )</c> of the arena.</param>
    /// <param name="currentColor"><c>GetCurrentColor()</c> of the arena.</param>
    /// <param name="screen">Screen the dialog is centered on.</param>
    /// <returns>The dialog, or <see langword="null"/> when the artwork is not in the data.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sprites"/> or <paramref name="commander"/> is <see langword="null"/>.
    /// </exception>
    public static BattleHeroOptionsWindow? TryCreate(IViewWorldSpriteSource sprites, IArmyCommander commander,
        bool buttons, bool canRetreat, bool canSurrender, PlayerColor currentColor, Size screen)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(commander);

        if (sprites.GetSprite(IcnType.Vgenbkg, 0) is not { } background ||
            sprites.GetSprite(IcnType.Viewgen, ColorBlockFrame(commander.GetColor())) is not { } colorBlock)
        {
            return null;
        }

        Sprite?[] frames = new Sprite?[8];

        for (int index = 0; index < frames.Length; ++index)
        {
            frames[index] = sprites.GetSprite(IcnType.Viewgen, 9 + index);

            if (frames[index] is null)
            {
                return null;
            }
        }

        Rect frame = FrameFor(screen, new Size(background.Width, background.Height));
        Rect area = ActiveAreaFor(frame);

        ButtonSprite cast = CreateButton(frames, 0, area, 0);
        ButtonSprite retreat = CreateButton(frames, 2, area, 1);
        ButtonSprite surrender = CreateButton(frames, 4, area, 2);
        ButtonSprite close = CreateButton(frames, 6, area, 3);

        return new BattleHeroOptionsWindow(frame, background, colorBlock,
            TryCreatePortrait(sprites, commander), commander, buttons, canRetreat, canSurrender, currentColor,
            cast, retreat, surrender, close);
    }

    /// <summary>
    /// Reads one frame of input and reports what the window was closed with, the body of the reference's modal loop.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>The action of this frame; <see cref="BattleHeroOptionsAction.Close"/> means the caller stops drawing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public BattleHeroOptionsAction ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        PortraitClicked = false;

        if (!_buttons)
        {
            // "This is a case when this dialog was called by the right mouse button press."
            return events.IsMouseRightButtonPressed ? BattleHeroOptionsAction.None : BattleHeroOptionsAction.Close;
        }

        _cast.UpdatePressState(events);
        _retreat.UpdatePressState(events);
        _surrender.UpdatePressState(events);
        _close.UpdatePressState(events);

        UpdateStatusMessage(events);

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultOkay) ||
            GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel) ||
            events.MouseClickLeft(_close.Area))
        {
            return BattleHeroOptionsAction.Close;
        }

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.BattleCastSpell) ||
            (_cast.IsEnabled && events.MouseClickLeft(_cast.Area)))
        {
            return BattleHeroOptionsAction.CastSpell;
        }

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.BattleRetreat) ||
            (_retreat.IsEnabled && events.MouseClickLeft(_retreat.Area)))
        {
            return BattleHeroOptionsAction.Retreat;
        }

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.BattleSurrender) ||
            (_surrender.IsEnabled && events.MouseClickLeft(_surrender.Area)))
        {
            return BattleHeroOptionsAction.Surrender;
        }

        PortraitClicked = CanOpenHeroScreen && events.MouseClickLeft(PortraitAreaFor(_area));

        return BattleHeroOptionsAction.None;
    }

    /// <summary>
    /// Draws the whole dialog, the reference's backdrop blit plus the first <c>display.render( pos_rt )</c>.
    /// </summary>
    /// <param name="target">Frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, _frame.Position, _background);

        if (_portrait is { } portrait)
        {
            ImageOps.BlitSprite(target, new Point(_area.X + PortraitOffsetX, _area.Y + PortraitOffsetY), portrait);
        }

        ImageOps.BlitSprite(target, new Point(_area.X + ColorBlockOffsetX, _area.Y + ColorBlockOffsetY), _colorBlock);

        Text title = new(_title, FontType.SmallWhite());
        title.Draw(_area.X + ((_area.Width - title.Width()) / 2), _area.Y + TitleOffsetY + TextOffsetY, target);

        DrawStats(target);

        _cast.Draw(target);
        _retreat.Draw(target);
        _surrender.Draw(target);
        _close.Draw(target);
    }

    /// <summary>The name of a commander, <c>HeroBase::GetName()</c>.</summary>
    /// <param name="commander">The hero or captain.</param>
    /// <returns>The name, empty for a commander type the port does not know.</returns>
    private static string GetName(IArmyCommander commander) => commander switch
    {
        Hero hero => hero.Name,
        Captain captain => captain.Name,
        _ => string.Empty,
    };

    /// <summary>
    /// The knowledge of a commander, <c>HeroBase::GetKnowledge()</c>. It is not on <see cref="IArmyCommander"/>
    /// because only the two dialogs read it.
    /// </summary>
    /// <param name="commander">The hero or captain.</param>
    /// <returns>The knowledge, zero for a commander type the port does not know.</returns>
    private static int GetKnowledge(IArmyCommander commander) => commander switch
    {
        Hero hero => hero.GetKnowledge(),
        Captain captain => captain.GetKnowledge(),
        _ => 0,
    };

    /// <summary>Builds the big portrait, <c>HeroBase::PortraitRedraw( PORT_BIG )</c>.</summary>
    /// <param name="sprites">Source of the portrait frames.</param>
    /// <param name="commander">The hero or captain.</param>
    /// <returns>The portrait, or <see langword="null"/> when the artwork is missing.</returns>
    private static Sprite? TryCreatePortrait(IViewWorldSpriteSource sprites, IArmyCommander commander) =>
        commander switch
        {
            Hero hero => sprites.GetSprite(HeroPortraitIcn.Get(hero.PortraitId), 0),
            Captain captain => CaptainPortrait.TryCreateBig(sprites, captain),
            _ => null,
        };

    /// <summary>Creates one <c>VIEWGEN</c> button at the reference's offset.</summary>
    /// <param name="frames">The eight frames from <c>VIEWGEN</c> 9 to 16.</param>
    /// <param name="releasedIndex">Frame offset of the released look.</param>
    /// <param name="area">The content rect.</param>
    /// <param name="button">The button index, see <see cref="ButtonOffsetX"/>.</param>
    /// <returns>The button.</returns>
    private static ButtonSprite CreateButton(Sprite?[] frames, int releasedIndex, Rect area, int button)
    {
        ButtonSprite sprite = new(frames[releasedIndex]!, frames[releasedIndex + 1]!);
        sprite.SetPosition(area.X + ButtonOffsetX(button), area.Y + ButtonOffsetY);

        return sprite;
    }

    /// <summary>Draws the four primary skills and the three wrapped lines below them.</summary>
    /// <param name="target">Frame buffer of the scene.</param>
    private void DrawStats(PixelBuffer target)
    {
        int x = _area.X + StatsOffsetX;
        int y = _area.Y + StatsOffsetY;

        DrawCenteredStat($"Attack: {_commander.GetAttack()}", x, y, target);
        DrawCenteredStat($"Defense: {_commander.GetDefense()}", x, y + StatsRowHeight, target);
        DrawCenteredStat($"Spell Power: {_commander.GetPower()}", x, y + (StatsRowHeight * 2), target);
        DrawCenteredStat($"Knowledge: {_knowledge}", x, y + (StatsRowHeight * 3), target);

        y += StatsRowHeight * 4;

        // The reference reuses one Text object for the three wrapped lines, so the "no uniform alignment" switch it
        // sets for morale stays in effect for luck and spell points too.
        Text text = new($"Morale: {Morale.GetName(_commander.GetMorale())}", FontType.SmallWhite());
        text.SetUniformVerticalAlignment(false);
        text.Draw(x, y + TextOffsetY, StatsWidth, target);
        y += text.Height(StatsWidth);

        text.Set($"Luck: {Luck.GetName(_commander.GetLuck())}", FontType.SmallWhite());
        text.Draw(x, y + TextOffsetY, StatsWidth, target);
        y += text.Height(StatsWidth);

        text.Set($"Spell Points: {_commander.SpellPoints}/{_commander.GetMaxSpellPoints()}", FontType.SmallWhite());

        // "By default the spell points should have one line of space between it and the Luck, but if there isn't any
        // space due to morale and luck taking up four lines, then move it down to the lowest line."
        int compensation = y - (_area.Y + StatsOffsetY + SpellPointsCompensationBase) == 0
            ? SpellPointsCompensation
            : 0;
        int spellPointsY = _area.Y + StatsOffsetY + SpellPointsOffsetY + compensation;

        text.Draw(x, spellPointsY, StatsWidth, target);
    }

    /// <summary>Draws one primary skill row centered in the stats column.</summary>
    /// <param name="line">The text.</param>
    /// <param name="x">Left edge of the stats column.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="target">Frame buffer of the scene.</param>
    private static void DrawCenteredStat(string line, int x, int y, PixelBuffer target)
    {
        Text text = new(line, FontType.SmallWhite());
        text.Draw(x + ((StatsWidth - text.Width()) / 2), y + TextOffsetY, target);
    }

    /// <summary>Updates <see cref="StatusMessage"/> from the cursor, the reference's status block.</summary>
    /// <param name="events">Input state of the frame.</param>
    private void UpdateStatusMessage(LocalEvent events)
    {
        if (_isControllable && events.IsMouseCursorPosInArea(_cast.Area))
        {
            _statusMessage = "Cast Spell";
        }
        else if (_isControllable && !_isCaptain && events.IsMouseCursorPosInArea(_retreat.Area))
        {
            _statusMessage = "Retreat";
        }
        else if (_isControllable && !_isCaptain && events.IsMouseCursorPosInArea(_surrender.Area))
        {
            _statusMessage = "Surrender";
        }
        else if (events.IsMouseCursorPosInArea(_close.Area))
        {
            _statusMessage = "Cancel";
        }
        else if (CanOpenHeroScreen && events.IsMouseCursorPosInArea(PortraitAreaFor(_area)))
        {
            _statusMessage = "Hero Screen";
        }
        else
        {
            _statusMessage = _isCaptain ? "Captain's Options" : "Hero's Options";
        }
    }
}
