using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What a left press of the town screen landed on, in the order the reference tests the areas
/// (<c>castle_dialog.cpp:499-776</c>).
/// </summary>
public enum CastleScreenTarget
{
    /// <summary>Nothing of the screen, <c>DoNothing</c>.</summary>
    None,

    /// <summary>The EXIT button, <c>buttonExit</c>.</summary>
    Exit,

    /// <summary>The "previous town" arrow, <c>buttonPrevCastle</c>.</summary>
    PreviousCastle,

    /// <summary>The "next town" arrow, <c>buttonNextCastle</c>.</summary>
    NextCastle,

    /// <summary>The resource panel, <c>resActiveArea</c>, which opens the kingdom's income.</summary>
    Income,

    /// <summary>The visiting hero's portrait, <c>rectSign2</c>.</summary>
    Hero,

    /// <summary>One of the town's buildings, an entry of <c>cacheBuildings</c>.</summary>
    Building,
}

/// <summary>
/// One completed click of the town screen, <c>MouseClickLeft()</c> of the reference's loop.
/// </summary>
/// <remarks>
/// 这一层只说明「点了什么」，不决定「打开哪个窗口」：魔法塔、怪物住所与建造窗口的区分要用城堡规则
/// （<c>GetActualDwelling</c>、<c>BUILD_MAGEGUILD</c> 掩码），留给场景层。
/// </remarks>
/// <param name="Target">The area that was clicked.</param>
/// <param name="Building">The building, when <paramref name="Target"/> is <see cref="CastleScreenTarget.Building"/>.</param>
public readonly record struct CastleScreenAction(CastleScreenTarget Target, BuildingType Building)
{
    /// <summary>A click on <paramref name="building"/>.</summary>
    /// <param name="building">The building under the pointer.</param>
    /// <returns>The action.</returns>
    public static CastleScreenAction OnBuilding(BuildingType building) => new(CastleScreenTarget.Building, building);

    /// <summary>A click on one of the fixed areas, no building involved.</summary>
    /// <param name="target">The area.</param>
    /// <returns>The action.</returns>
    public static CastleScreenAction OnArea(CastleScreenTarget target) => new(target, BuildingType.Nothing);
}

/// <summary>
/// The click handling of the town screen: which area a point hits, which button is drawn pressed and
/// which action a completed click produces, <c>castle_dialog.cpp:493-776</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>MouseClickLeft( area )</c> 要求按下与松开落在同一处，所以这里记住按下时命中的目标，松手时再比对：
/// 按下 EXIT 再拖出去松手什么都不会发生。按下帧则照 <c>isMouseLeftButtonPressedAndHeldInArea</c>，只看「按住
/// 且指针现在在区域里」，指针移出按下帧就消失、移回又出现。
/// </para>
/// <para>
/// 箭头是否响应由 <see cref="CanSwitchCastle"/> 决定，参考的 <c>buttonPrevCastle.isEnabled()</c> 只在王国有
/// 不止一座城时为真；场景层按王国的城数设置它。
/// </para>
/// <para>
/// 循环本身（淡入、动画、子窗口的返回值）不在这里：这一层只吃指针与左键状态，吐出一次
/// <see cref="CastleScreenAction"/>，由场景层翻译成开窗口、切城或关屏。
/// </para>
/// </remarks>
public sealed class CastleScreenInput
{
    private readonly CastleScreenView _view;

    private Point? _cursor;
    private bool _down;
    private CastleScreenTarget _pressed;
    private BuildingType _pressedBuilding;
    private CastleScreenAction? _action;

    /// <summary>
    /// Builds the handler of one town screen.
    /// </summary>
    /// <param name="view">The screen whose areas and buildings are hit.</param>
    public CastleScreenInput(CastleScreenView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
    }

    /// <summary>
    /// Whether the two arrows react, the <c>buttonPrevCastle.isEnabled()</c> of the reference.
    /// </summary>
    /// <remarks>单城王国里两个箭头是灰的：点击不产生切城动作。</remarks>
    public bool CanSwitchCastle { get; set; } = true;

    /// <summary>The pointer as last recorded, or <see langword="null"/> outside the window.</summary>
    public Point? Cursor => _cursor;

    /// <summary>Whether EXIT is drawn pressed, <c>buttonExit.drawOnState()</c>.</summary>
    public bool ExitPressed => IsHeld(CastleScreenTarget.Exit);

    /// <summary>Whether the "previous town" arrow is drawn pressed.</summary>
    public bool PreviousPressed => IsHeld(CastleScreenTarget.PreviousCastle);

    /// <summary>Whether the "next town" arrow is drawn pressed.</summary>
    public bool NextPressed => IsHeld(CastleScreenTarget.NextCastle);

    /// <summary>Records the pointer.</summary>
    /// <param name="cursor">Pointer in screen pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor) => _cursor = cursor;

    /// <summary>
    /// Records the left button. A release completes the click when it lands on the same target the press
    /// landed on; anything else is dropped.
    /// </summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down)
    {
        if (down)
        {
            if (_down)
            {
                return;
            }

            _down = true;
            (_pressed, _pressedBuilding) = Hit(_cursor);

            return;
        }

        if (!_down)
        {
            return;
        }

        _down = false;

        (CastleScreenTarget target, BuildingType building) = Hit(_cursor);

        if (target != CastleScreenTarget.None && target == _pressed && building == _pressedBuilding)
        {
            _action = new CastleScreenAction(target, building);
        }

        _pressed = CastleScreenTarget.None;
        _pressedBuilding = BuildingType.Nothing;
    }

    /// <summary>
    /// Takes the action of the last completed click, if any; the action is consumed.
    /// </summary>
    /// <returns>The action, or <see langword="null"/> when nothing was clicked since the last call.</returns>
    public CastleScreenAction? TakeAction()
    {
        CastleScreenAction? action = _action;
        _action = null;

        return action;
    }

    /// <summary>
    /// What a point hits, in the reference's order: EXIT, the arrows, the income panel, the hero's
    /// portrait, then the buildings from the top of the render queue down.
    /// </summary>
    /// <param name="cursor">Pointer in screen pixels, or <see langword="null"/> outside the window.</param>
    /// <returns>The area under the pointer, <see cref="CastleScreenTarget.None"/> when there is none.</returns>
    public CastleScreenTarget HitTest(Point? cursor) => Hit(cursor).Target;

    private (CastleScreenTarget Target, BuildingType Building) Hit(Point? cursor)
    {
        if (cursor is not { } point || _view.Castle is null)
        {
            return (CastleScreenTarget.None, BuildingType.Nothing);
        }

        CastleDialogLayout layout = _view.Layout;

        if (_view.ExitButtonArea.Contains(point))
        {
            return (CastleScreenTarget.Exit, BuildingType.Nothing);
        }

        if (CanSwitchCastle && layout.PreviousCastleButton.Contains(point))
        {
            return (CastleScreenTarget.PreviousCastle, BuildingType.Nothing);
        }

        if (CanSwitchCastle && layout.NextCastleButton.Contains(point))
        {
            return (CastleScreenTarget.NextCastle, BuildingType.Nothing);
        }

        if (layout.ResourceHitArea.Contains(point))
        {
            return (CastleScreenTarget.Income, BuildingType.Nothing);
        }

        if (_view.Hero is not null && layout.HeroPortraitArea.Contains(point))
        {
            return (CastleScreenTarget.Hero, BuildingType.Nothing);
        }

        BuildingType building = _view.FindBuildingAt(point);

        return building == BuildingType.Nothing
            ? (CastleScreenTarget.None, BuildingType.Nothing)
            : (CastleScreenTarget.Building, building);
    }

    private bool IsHeld(CastleScreenTarget target) =>
        _down && _cursor is { } point && HitTest(point) == target;
}
