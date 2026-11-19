using FHeroes2.Core.Geometry;
using FHeroes2.Game.Army;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The dispatch half of <see cref="ArmyBar"/>: <c>ItemsActionBar::QueueEventProcessing()</c> and
/// <c>ActionCursorItemIter()</c> from <c>interface_itemsbar.h</c>, narrowed to one army bar.
/// </summary>
public sealed partial class ArmyBar
{
    /// <summary>
    /// Handles one frame of input for a bar on its own, <c>ItemsActionBar::QueueEventProcessing()</c>
    /// (<c>interface_itemsbar.h:425</c>).
    /// </summary>
    /// <param name="events">The frame's input.</param>
    /// <param name="hotKey">Which split or join hot key is down.</param>
    /// <returns>What the frame asked for.</returns>
    public ArmyBarEvent ProcessEvents(LocalEvent events, ArmyBarHotKey hotKey = ArmyBarHotKey.None)
    {
        ArgumentNullException.ThrowIfNull(events);

        Message = string.Empty;

        if (ItemsCount == 0)
        {
            return default;
        }

        return ProcessCursor(events, GetItemIndexAt(events.CursorPosition), hotKey);
    }

    /// <summary>
    /// Handles one frame of input for two bars that exchange troops,
    /// <c>ItemsActionBar::QueueEventProcessing( other )</c> (<c>interface_itemsbar.h:430</c>).
    /// </summary>
    /// <param name="events">The frame's input.</param>
    /// <param name="other">The bar a troop may come from.</param>
    /// <param name="hotKey">Which split or join hot key is down.</param>
    /// <returns>What the frame asked for.</returns>
    public ArmyBarEvent ProcessEvents(LocalEvent events, ArmyBar other, ArmyBarHotKey hotKey = ArmyBarHotKey.None)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(other);

        Message = string.Empty;

        if (ItemsCount == 0 && other.ItemsCount == 0)
        {
            return default;
        }

        // A drag from the other bar that ends here is handled first: the release is this bar's, the press is the
        // other's (interface_itemsbar.h:439-443).
        ArmyBarEvent drag = ActionCrossItemBarDrag(events, other);

        if (drag.Handled)
        {
            return drag;
        }

        return other.IsSelected
            ? ProcessCursor(events, other, hotKey)
            : ProcessCursor(events, GetItemIndexAt(events.CursorPosition), hotKey);
    }

    /// <summary>
    /// Which of this bar's slots contains <paramref name="position"/>, the port's <c>GetItemIterPos()</c>
    /// (<c>interface_itemsbar.h:233</c>).
    /// </summary>
    /// <param name="position">A screen position.</param>
    /// <returns>The slot index, or <c>-1</c> outside the bar or past the last slot.</returns>
    public int GetItemIndexAt(Point position)
    {
        int index = _layout.IndexAt(position);

        return index < ItemsCount ? index : -1;
    }

    /// <summary>The troop of a slot, or <see langword="null"/> outside the bar.</summary>
    /// <param name="index">The slot.</param>
    /// <returns>The slot's troop.</returns>
    public Troop? GetTroop(int index) =>
        index >= 0 && index < ItemsCount ? _army!.GetTroop(index) : null;

    /// <summary>
    /// Applies the answer of the unit information window, the <c>switch ( Dialog::ArmyInfo( ... ) )</c> of the double
    /// click (<c>army_bar.cpp:582-597</c>).
    /// </summary>
    /// <remarks>
    /// A dismissal empties the slot; an upgrade first charges the treasury through <paramref name="payForUpgrade"/>
    /// and then turns the stack into the upgraded monster. The reference reads the treasury from the world
    /// (<c>world.GetKingdom( color ).OddFundsResource()</c>), which the port keeps out of the widget, so the caller
    /// that owns the kingdom passes the payment in. Passing <see langword="null"/> upgrades for free, as the
    /// battle-only cases do.
    /// </remarks>
    /// <param name="troop">The troop the window showed.</param>
    /// <param name="answer">The window's answer.</param>
    /// <param name="payForUpgrade">Charges a treasury; may be <see langword="null"/>.</param>
    public static void ApplyInfoAnswer(Troop troop, DialogButton answer, Action<Funds>? payForUpgrade = null)
    {
        ArgumentNullException.ThrowIfNull(troop);

        switch (answer)
        {
            case DialogButton.Upgrade:
                payForUpgrade?.Invoke(troop.GetTotalUpgradeCost());
                troop.Upgrade();
                break;

            case DialogButton.Dismiss:
                troop.Reset();
                break;
        }
    }

    private ArmyBarEvent ProcessCursor(LocalEvent events, int index, ArmyBarHotKey hotKey)
    {
        if (index < 0)
        {
            return default;
        }

        Rect area = _layout.GetItemRect(index);

        ArmyBarEvent cursor = ActionBarCursor(index, events);

        if (cursor.Handled)
        {
            return cursor;
        }

        if (events.MouseClickLeft(area))
        {
            if (index == _selectedIndex)
            {
                return ActionBarLeftMouseDoubleClick(index, hotKey);
            }

            ArmyBarEvent click = ActionBarLeftMouseSingleClick(index, hotKey);

            if (click.Handled)
            {
                _selectedIndex = index;
            }
            else
            {
                ResetSelected();
            }

            return new ArmyBarEvent(true, click.Split, click.Info, click.AddTroop);
        }

        if (events.IsMouseLeftButtonPressedInArea(area))
        {
            // ActionBarLeftMouseHold() does nothing; the call stays so the order matches the reference.
            return default;
        }

        if (events.IsMouseLeftButtonReleasedInArea(area))
        {
            return ActionBarLeftMouseRelease(events, index);
        }

        if (events.MouseClickRight(area))
        {
            return ActionBarRightMouseSingleClick(index);
        }

        if (events.IsMouseRightButtonPressedInArea(area))
        {
            return ActionBarRightMouseHold(index);
        }

        return default;
    }

    private ArmyBarEvent ProcessCursor(LocalEvent events, ArmyBar other, ArmyBarHotKey hotKey)
    {
        int index = GetItemIndexAt(events.CursorPosition);

        if (index < 0)
        {
            return default;
        }

        Rect area = _layout.GetItemRect(index);

        ArmyBarEvent cursor = ActionBarCursor(index, other);

        if (cursor.Handled)
        {
            return cursor;
        }

        if (events.MouseClickLeft(area))
        {
            ArmyBarEvent click = ActionBarLeftMouseSingleClick(index, other, hotKey);

            if (click.Handled)
            {
                _selectedIndex = index;
            }
            else
            {
                ResetSelected();
            }

            other.ResetSelected();

            return new ArmyBarEvent(true, click.Split);
        }

        if (events.IsMouseLeftButtonPressedInArea(area))
        {
            return default;
        }

        // The release belongs to ActionCrossItemBarDrag() (interface_itemsbar.h:563).

        if (events.MouseClickRight(area))
        {
            ArmyBarEvent click = ActionBarRightMouseSingleClick(index, other);
            other.ResetSelected();

            return new ArmyBarEvent(true, click.Split);
        }

        if (events.IsMouseRightButtonPressedInArea(area))
        {
            return default;
        }

        return default;
    }

    private ArmyBarEvent ActionCrossItemBarDrag(LocalEvent events, ArmyBar other)
    {
        int otherIndex = other.GetItemIndexAt(events.LeftButtonPressPosition);

        if (otherIndex < 0)
        {
            return default;
        }

        int index = GetItemIndexAt(events.CursorPosition);

        if (index < 0)
        {
            return default;
        }

        Rect area = _layout.GetItemRect(index);

        if (events.IsMouseLeftButtonPressedInArea(area))
        {
            return default;
        }

        if (events.IsMouseLeftButtonReleasedInArea(area))
        {
            ArmyBarEvent release = ActionBarLeftMouseRelease(index, other, otherIndex);

            if (release.Handled)
            {
                other.ResetSelected();
            }

            // The reference answers true whether or not the release moved anything: the drag belonged to the pair of
            // bars, so the frame's input is spent (interface_itemsbar.h:480-486).
            return new ArmyBarEvent(true, release.Split);
        }

        return default;
    }
}
