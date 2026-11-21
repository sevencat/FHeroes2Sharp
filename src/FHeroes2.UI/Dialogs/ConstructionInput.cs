using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The mouse state machine of the construction window: what was pressed, what is held and what a full click
/// asks for, <c>castle_town.cpp:600-768</c>.
/// </summary>
/// <remarks>
/// The reference reacts on release (<c>le.MouseClickLeft( area )</c>), so a press only arms a target and the
/// release has to land on the same one. Arrows and cells can be disabled at press time: the arrows when the
/// kingdom owns a single town, a cell when it cannot be built yet.
/// </remarks>
public sealed class ConstructionInput
{
    private readonly ConstructionView _view;

    private Point? _cursor;
    private bool _leftDown;
    private ConstructionHit _pressed = ConstructionHit.None;
    private ConstructionHit? _action;

    /// <summary>Creates the input state of one construction window.</summary>
    /// <param name="view">The window the input belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> is <see langword="null"/>.</exception>
    public ConstructionInput(ConstructionView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
    }

    /// <summary>The mouse, or <see langword="null"/> when it is outside the window.</summary>
    public Point? Cursor => _cursor;

    /// <summary>Whether the kingdom owns more than one town, <c>CanSwitchCastle</c>.</summary>
    public bool CanSwitchCastle { get; set; }

    /// <summary>Whether EXIT is held down.</summary>
    public bool ExitPressed => IsHeld(ConstructionTarget.Exit);

    /// <summary>Whether the "previous town" arrow is held down.</summary>
    public bool PreviousPressed => CanSwitchCastle && IsHeld(ConstructionTarget.PreviousTown);

    /// <summary>Whether the "next town" arrow is held down.</summary>
    public bool NextPressed => CanSwitchCastle && IsHeld(ConstructionTarget.NextTown);

    /// <summary>Moves the mouse.</summary>
    /// <param name="cursor">The new position, or <see langword="null"/> when it left the window.</param>
    public void SetCursor(Point? cursor) => _cursor = cursor;

    /// <summary>Presses or releases the left button.</summary>
    /// <param name="down"><see langword="true"/> on press, <see langword="false"/> on release.</param>
    public void SetLeftButton(bool down)
    {
        if (down)
        {
            ConstructionHit hit = _view.HitTest(_cursor, forClick: true);

            _leftDown = IsPressable(hit);
            _pressed = _leftDown ? hit : ConstructionHit.None;

            return;
        }

        if (_leftDown)
        {
            ConstructionHit hit = _view.HitTest(_cursor, forClick: true);

            if (hit == _pressed)
            {
                _action = hit;
            }
        }

        _leftDown = false;
        _pressed = ConstructionHit.None;
    }

    /// <summary>
    /// The click that completed since the last call, <c>castle_town.cpp:600-768</c>.
    /// </summary>
    /// <returns>The hit, or <see langword="null"/> when nothing was clicked.</returns>
    public ConstructionHit? TakeAction()
    {
        ConstructionHit? action = _action;
        _action = null;

        return action;
    }

    /// <summary>
    /// Drops the armed target without producing a click, for when the caller consumes the release itself
    /// (a message box opened on top of the window, for instance).
    /// </summary>
    public void Cancel()
    {
        _leftDown = false;
        _pressed = ConstructionHit.None;
    }

    private bool IsPressable(ConstructionHit hit) => hit.Target switch
    {
        ConstructionTarget.None => false,
        ConstructionTarget.PreviousTown or ConstructionTarget.NextTown => CanSwitchCastle,
        ConstructionTarget.Building => _view.CanOpenCell(hit.CellIndex),
        ConstructionTarget.SpreadFormation => !_view.Castle.Army.IsSpreadFormation,
        ConstructionTarget.GroupedFormation => _view.Castle.Army.IsSpreadFormation,
        _ => true,
    };

    private bool IsHeld(ConstructionTarget target) =>
        _leftDown && _view.HitTest(_cursor, forClick: true).Target == target;
}
