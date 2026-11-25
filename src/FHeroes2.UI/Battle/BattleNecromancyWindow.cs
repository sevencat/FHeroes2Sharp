using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Battle;

/// <summary>
/// How many skeletons necromancy raised, <c>Battle::Arena::DialogBattleNecromancy()</c>
/// (<c>battle_dialogs.cpp:786-853</c>).
/// </summary>
/// <remarks>
/// <para>
/// 一块 <c>WINLOSE</c> 动画边框、一行黄字标题、一段说明、一只骷髅图标与数量，底下一个 OK。动画是循环的
/// <c>WINCMBT</c>，边框本身从 <c>WINLOSE</c> 的 0 号帧拷贝。
/// </para>
/// <para>
/// <b>与参考的差异</b>：参考在开窗时播一次拾取音效（<c>Game::PlayPickupSound()</c>），端口还没有音效层，
/// 这一步留空；参考按界面风格挑 <c>WINLOSE</c>/<c>WINLOSEE</c>，端口的窗口装饰由 <see cref="StandardWindow"/>
/// 统一负责，这里只用 GOOD 那一套图。
/// </para>
/// <para>
/// 窗口不开自己的循环：场景每帧把输入喂给 <see cref="ProcessEvents"/>，动画由 <see cref="Update"/> 推进，
/// 参考的 <c>while ( le.HandleEvents() )</c> 由场景的帧循环顶替。这与本目录的
/// <see cref="BattleSurrenderWindow"/> 是同一套约定。
/// </para>
/// </remarks>
public sealed class BattleNecromancyWindow
{
    /// <summary>Width of the dialog's active area, <c>StandardWindow( 287, 424 )</c>.</summary>
    public const int Width = 287;

    /// <summary>Height of the dialog's active area.</summary>
    public const int Height = 424;

    /// <summary>Left edge of the border art inside <c>WINLOSE</c>, <c>animationBorderRoi.x</c> = 43.</summary>
    private const int BorderSourceX = 43;

    /// <summary>Top edge of the border art inside <c>WINLOSE</c>, <c>animationBorderRoi.y</c> = 32.</summary>
    private const int BorderSourceY = 32;

    /// <summary>Width of the animation border, <c>animationBorderRoi.width</c> = 231.</summary>
    private const int BorderWidth = 231;

    /// <summary>Height of the animation border, <c>animationBorderRoi.height</c> = 133.</summary>
    private const int BorderHeight = 133;

    /// <summary>Horizontal shift of the border inside the active area, <c>( roi.width - 231 ) / 2</c> = 28.</summary>
    private const int BorderOffsetX = (Width - BorderWidth) / 2;

    /// <summary>Vertical offset of the border, <c>roi.y + 20</c>.</summary>
    private const int BorderOffsetY = 20;

    /// <summary>Gap between the animation and the title, <c>+ 8</c>.</summary>
    private const int TitleGap = 8;

    /// <summary>Distance between the title and the message, <c>bsTextIndent</c> = 30.</summary>
    private const int TextIndent = 30;

    /// <summary>Horizontal inset of the message, <c>roi.width - 22</c> split in two.</summary>
    private const int MessageInset = 11;

    /// <summary>Gap between the skull and the count, <c>+ 30</c>.</summary>
    private const int CountGap = 30;

    /// <summary>Offset of the count text below its anchor, <c>+ 2</c>.</summary>
    private const int CountOffsetY = 2;

    /// <summary>Offset of the OK button from the bottom edge, <c>{ 0, 5 }</c>.</summary>
    private static readonly Point OkayOffset = new(0, 5);

    /// <summary>The creature necromancy raises, <c>Monster( Monster::SKELETON )</c>.</summary>
    public const MonsterType RaisedMonster = MonsterType.Skeleton;

    private readonly StandardWindow _window;
    private readonly IViewWorldSpriteSource _sprites;
    private readonly Sprite? _border;
    private readonly Sprite? _monsterSprite;
    private readonly BattleDialogAnimationSequence _sequence = new();
    private readonly uint _raiseCount;
    private readonly ButtonSprite _okay;

    private BattleNecromancyWindow(StandardWindow window, IViewWorldSpriteSource sprites, Sprite? border,
        Sprite? monsterSprite, Rect animationArea, uint raiseCount, ButtonSprite okay)
    {
        _window = window;
        _sprites = sprites;
        _border = border;
        _monsterSprite = monsterSprite;
        _okay = okay;
        _raiseCount = raiseCount;
        AnimationArea = animationArea;
        AnimationFrame = 1;

        _sequence.Push(IcnType.Wincmbt, true);
    }

    /// <summary>The dialog's active area, <c>background.activeArea()</c>.</summary>
    public Rect Area => _window.ActiveArea;

    /// <summary>Where the <c>WINCMBT</c> frames go, <c>animationRoi</c>.</summary>
    public Rect AnimationArea { get; }

    /// <summary>The number of skeletons raised, <c>raiseCount</c>.</summary>
    public uint RaiseCount => _raiseCount;

    /// <summary>The headline, <c>_( "Necromancy!" )</c>.</summary>
    public string Title => "Necromancy!";

    /// <summary>The explanation with the count and the creature's plural name substituted in.</summary>
    public string Message => "Practicing the dark arts of necromancy, you are able to raise "
        + $"{_raiseCount} of the enemy's dead to return under your service as "
        + $"{MonsterNames.GetPluralName(RaisedMonster, _raiseCount)}.";

    /// <summary>The OK button, exposed for the caller's hit tests and for tests.</summary>
    public ButtonSprite OkayButton => _okay;

    /// <summary>The current animation frame; starts at 1, the frame <c>setupAnimation()</c> blits at once.</summary>
    public uint AnimationFrame { get; private set; }

    /// <summary>
    /// Builds the window, or returns <see langword="null"/> when the border is missing from the data.
    /// </summary>
    /// <param name="sprites">Where <c>WINLOSE</c>, <c>WINCMBT</c> and <c>MONS32</c> come from.</param>
    /// <param name="raiseCount">How many skeletons were raised.</param>
    /// <param name="okayReleased">Released OK artwork — <c>SYSTEM.ICN</c> frame 1 in this data set.</param>
    /// <param name="okayPressed">Pressed OK artwork.</param>
    /// <param name="screen">Screen the window is centered on.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sprites"/> or the button art is <see langword="null"/>.</exception>
    public static BattleNecromancyWindow TryCreate(IViewWorldSpriteSource sprites, uint raiseCount,
        Sprite okayReleased, Sprite okayPressed, Size screen)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(okayReleased);
        ArgumentNullException.ThrowIfNull(okayPressed);

        StandardWindow window = StandardWindow.CreateCentered(new Size(Width, Height), renderBackground: true, screen);
        Rect area = window.ActiveArea;

        Rect animationArea = new(
            area.X + BorderOffsetX + 4,
            area.Y + BorderOffsetY,
            BorderWidth,
            BorderHeight);

        ButtonSprite okay = new(okayReleased, okayPressed);
        Point okayPosition = window.RenderPosition(OkayOffset, new Size(okayReleased.Width, okayReleased.Height),
            WindowPadding.BottomCenter);
        okay.SetPosition(okayPosition.X, okayPosition.Y);

        int monsterFrame = (int)RaisedMonster - (int)MonsterType.Peasant;

        return new BattleNecromancyWindow(window, sprites, sprites.GetSprite(IcnType.Winlose, 0),
            sprites.GetSprite(IcnType.Mons32, monsterFrame), animationArea, raiseCount, okay);
    }

    /// <summary>Advances the backdrop when the dialog delay has elapsed, <c>updateAnimation()</c>.</summary>
    /// <returns><see langword="true"/> when the animation advanced and the window should be repainted.</returns>
    public bool Update()
    {
        if (!GameDelays.ValidateAnimationDelay(DelayType.BattleDialogDelay) || _sequence.NextFrame())
        {
            return false;
        }

        AnimationFrame = _sequence.AdvanceFrame();

        return true;
    }

    /// <summary>Paints the window: stone frame, animation, texts, skull and count, then the button.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);
        DrawAnimation(target);

        Rect area = Area;
        int y = AnimationArea.Y + AnimationArea.Height + TitleGap;

        new Text(Title, FontType.NormalYellow()).Draw(area.X, y, target);

        Text message = new(Message, FontType.NormalWhite());
        int messageWidth = area.Width - (MessageInset * 2);
        y += TextIndent;
        message.Draw(area.X + MessageInset, y, messageWidth, target);

        int messageHeight = message.Height(messageWidth);
        int spriteHeight = 0;

        if (_monsterSprite is { } monster)
        {
            spriteHeight = monster.Image.Height;

            // 参考：横向按整个屏幕居中，纵向落在说明文字下面、让图标底边压在数量行的上沿。
            ImageOps.Blit(target,
                new Point((target.Width - monster.Image.Width) / 2, y + messageHeight + spriteHeight - 2),
                monster.Image, monster.Image.Bounds);
        }

        y += messageHeight + spriteHeight - 2 + CountGap;

        Text count = new(_raiseCount.ToString(), FontType.SmallWhite());
        count.FitToOneRow(area.Width);

        // 参考按整个屏幕居中；窗口本身居中，所以两者等价。
        count.Draw((target.Width - count.Width()) / 2, y + CountOffsetY, target);

        _okay.Draw(target);
    }

    /// <summary>
    /// Consumes one frame of input, the body of the reference's <c>while ( le.HandleEvents() )</c> loop.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns><see langword="true"/> when the window should close.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _okay.UpdatePressState(events);

        return GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultOkay)
            || GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel)
            || events.MouseClickLeft(_okay.Area);
    }

    /// <summary>Copies the border art and blits the current animation frame over it.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawAnimation(PixelBuffer target)
    {
        if (_border is { } border)
        {
            // 参考的 Copy：只拷图像区域，不认偏移。
            ImageOps.Blit(target, new Point(AnimationArea.X - 4, AnimationArea.Y - 4), border.Image,
                new Rect(BorderSourceX, BorderSourceY, BorderWidth, BorderHeight));
        }

        if (_sprites.GetSprite(IcnType.Wincmbt, (int)AnimationFrame) is { } frame)
        {
            ImageOps.BlitSprite(target, new Point(AnimationArea.X, AnimationArea.Y), frame);
        }
    }
}
