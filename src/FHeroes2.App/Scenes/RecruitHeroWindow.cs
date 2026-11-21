using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// What the recruit hero dialog asks the scene to do next.
/// </summary>
public enum RecruitHeroAction
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>Close the dialog without hiring.</summary>
    Exit,

    /// <summary>The player pressed OKAY on a hero they can afford.</summary>
    Hire,
}

/// <summary>
/// The recruit hero dialog of a town, <c>Castle::DialogBuyHero()</c>
/// (<c>src/fheroes2/castle/castle_town.cpp:66-160</c>).
/// </summary>
/// <remarks>
/// <para>
/// 和建造说明框一样，这一层只做「窗口本体」：素材取自 AGG，布局与绘制交给
/// <see cref="RecruitHeroView"/>，真正把人招进王国由场景（<see cref="CastleScene"/>）执行——窗口不碰城堡，
/// 也不扣钱。
/// </para>
/// <para>
/// 右键在两个按钮上会要一段说明（<c>castle_town.cpp:150-156</c>）：OKAY 上是 <c>Recruit X the Y.</c>，
/// CANCEL 上是 <c>Exit this menu without doing anything.</c>；窗口只回答「右键落在哪个按钮上」，说明框由
/// 场景摆放与关闭。相框上的右键是英雄速览（<c>Dialog::QuickInfo</c>），窗口只上报。
/// </para>
/// </remarks>
public sealed class RecruitHeroWindow
{
    private readonly RecruitHeroView _view;

    private Point? _cursor;
    private DialogButton _lastButton = DialogButton.None;
    private bool _closePressed;
    private bool _confirmPressed;

    private RecruitHeroWindow(RecruitHeroView view) => _view = view;

    /// <summary>Builds the dialog for one hero.</summary>
    /// <param name="assets">The archives the sprites come from.</param>
    /// <param name="dialog">The hero, the sentence and the price.</param>
    /// <param name="fonts">The game fonts: yellow heading, white sentence, small amounts.</param>
    /// <returns>The window, or <see langword="null"/> when an asset is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dialog"/> or <paramref name="fonts"/> is <see langword="null"/>.</exception>
    public static RecruitHeroWindow? TryCreate(AssetBundle? assets, RecruitHeroDialog dialog, GameFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(fonts);

        if (assets is null)
        {
            GameLog.Warn("The recruit hero dialog needs the archives, and they were not loaded.");

            return null;
        }

        if (SystemButtonArtwork.TryCreate(assets) is not { } buttons)
        {
            return null;
        }

        if (RecruitHeroArtwork.TryCreate(assets) is not { } artwork)
        {
            GameLog.Warn("The artwork of the recruit hero dialog is missing.");

            return null;
        }

        if (RecruitHeroView.TryCreate(
                dialog, artwork, LoadPortrait(dialog.Hero), fonts.Yellow, fonts.Normal, fonts.Small, buttons)
            is not { } view)
        {
            GameLog.Warn($"The recruit hero dialog does not fit its frame for '{dialog.Hero.Name}'.");

            return null;
        }

        return new RecruitHeroWindow(view);
    }

    /// <summary>The hero on offer and what the dialog says about them.</summary>
    public RecruitHeroDialog Dialog => _view.Dialog;

    /// <summary>Bounding box of the dialog, used to redraw the town screen around it.</summary>
    public Rect FrameArea => _view.FrameArea;

    /// <summary>Whether OKAY is enabled, <c>AllowBuyHero()</c>.</summary>
    public bool OkayEnabled => _view.OkayEnabled;

    /// <summary>Records the pointer over the dialog.</summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor)
    {
        _cursor = cursor;
        _view.SetCursor(cursor);
    }

    /// <summary>Records the left button; the answer is read by <see cref="Update"/>.</summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down) => _lastButton = _view.SetLeftButton(down);

    /// <summary>
    /// Records the right button and answers which button's help it asks for,
    /// <c>le.isMouseRightButtonPressedInArea( button.area() )</c> (<c>castle_town.cpp:150-156</c>).
    /// </summary>
    /// <param name="down">Whether the right button is now held.</param>
    /// <returns>The button under the pointer on the press, <see cref="DialogButton.None"/> otherwise.</returns>
    public DialogButton SetRightButton(bool down)
    {
        if (!down || _cursor is not { } cursor)
        {
            return DialogButton.None;
        }

        if (_view.Buttons[0].Area.Contains(cursor))
        {
            return DialogButton.Ok;
        }

        return _view.Buttons[1].Area.Contains(cursor) ? DialogButton.Cancel : DialogButton.None;
    }

    /// <summary>Whether the pointer rests on the portrait frame, <c>isMouseRightButtonPressedInArea( portrait_frame )</c>.</summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the hero's quick info is being asked for.</returns>
    public bool IsPortraitUnder(Point? cursor) => cursor is { } point && _view.Layout.FrameArea.Contains(point);

    /// <summary>Marks the close hot key, ESCAPE or CANCEL.</summary>
    public void PressClose() => _closePressed = true;

    /// <summary>Marks the confirm hot key, RETURN or OKAY.</summary>
    public void PressConfirm() => _confirmPressed = true;

    /// <summary>Consumes the events of this frame.</summary>
    /// <returns>What the caller should do next; the window does not close itself.</returns>
    public RecruitHeroAction Update()
    {
        RecruitHeroAction action;

        if (_confirmPressed)
        {
            action = _view.OkayEnabled ? RecruitHeroAction.Hire : RecruitHeroAction.None;
        }
        else if (_closePressed)
        {
            action = RecruitHeroAction.Exit;
        }
        else
        {
            action = _lastButton switch
            {
                DialogButton.Ok => _view.OkayEnabled ? RecruitHeroAction.Hire : RecruitHeroAction.None,
                DialogButton.Cancel => RecruitHeroAction.Exit,
                _ => RecruitHeroAction.None,
            };
        }

        _closePressed = false;
        _confirmPressed = false;
        _lastButton = DialogButton.None;

        return action;
    }

    /// <summary>Paints the dialog over the town screen.</summary>
    /// <param name="target">The indexed frame buffer of the scene.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _view.Draw(target);
    }

    /// <summary>
    /// The hero's 102x93 portrait, <c>GetPortrait( hero, PORT_BIG )</c> (<c>heroes.cpp:2103</c>).
    /// </summary>
    /// <param name="hero">The hero on offer.</param>
    /// <returns>The portrait, or <see langword="null"/> when the archive has no such frame.</returns>
    private static Sprite? LoadPortrait(Hero hero)
    {
        IcnType icn = HeroPortraitIcn.Get(hero.PortraitId);

        return icn == IcnType.Unknown ? null : GameAssets.GetImage(IcnCatalog.GetFileName(icn), 0);
    }
}
