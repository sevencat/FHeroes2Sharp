using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The input half of <see cref="ArmyBar"/>: the reference's <c>ActionBar*()</c> handlers
/// (<c>army_bar.cpp:317-741</c>) and the <c>interface_itemsbar.h</c> dispatch around them.
/// </summary>
public sealed partial class ArmyBar
{
    /// <summary>
    /// Whether the destination army's treasury can pay for an upgrade, <c>world.GetKingdom( color ).AllowPayment()</c>
    /// (<c>army_bar.cpp:575</c>).
    /// </summary>
    /// <remarks>
    /// The reference reads the kingdom of the army's owner; the port keeps the world out of the widget, so the screen
    /// that owns the kingdom supplies the check. Without it the upgrade flag is always offered, which is what the
    /// reference does for an affordable upgrade.
    /// </remarks>
    public Func<Funds, bool>? CanAffordUpgrade { get; set; }

    /// <summary>
    /// A selection frame of <paramref name="size"/> filled with <see cref="CursorColor"/>.
    /// </summary>
    /// <remarks>
    /// The reference builds <c>spcursor</c> with <c>reset()</c> and <c>DrawBorder( ..., 214 )</c>
    /// (<c>army_bar.cpp:195-198</c>): everything but the frame stays transparent. The port draws four lines because
    /// <see cref="ImageOps.DrawLine"/> also clears the sprite's transform layer along them, which
    /// <see cref="ImageOps.DrawBorder(PixelBuffer, byte, uint)"/> does not do for a sprite's buffer.
    /// </remarks>
    private static Sprite CreateCursor(Size size)
    {
        Sprite cursor = new(size.Width, size.Height);
        cursor.Reset();

        int right = size.Width - 1;
        int bottom = size.Height - 1;

        ImageOps.DrawLine(cursor, new Point(0, 0), new Point(right, 0), CursorColor);
        ImageOps.DrawLine(cursor, new Point(0, bottom), new Point(right, bottom), CursorColor);
        ImageOps.DrawLine(cursor, new Point(0, 1), new Point(0, bottom - 1), CursorColor);
        ImageOps.DrawLine(cursor, new Point(right, 1), new Point(right, bottom - 1), CursorColor);

        return cursor;
    }

    /// <summary>
    /// The creature the editor suggests for an empty slot: the level-one monster of the commander's race
    /// (<c>army_bar.cpp:450-474</c>).
    /// </summary>
    private MonsterType GetDefaultMonster() =>
        _army?.Commander?.Race switch
        {
            MapRace.Knight => MonsterType.Peasant,
            MapRace.Barbarian => MonsterType.Goblin,
            MapRace.Sorceress => MonsterType.Sprite,
            MapRace.Warlock => MonsterType.Centaur,
            MapRace.Wizard => MonsterType.Halfling,
            MapRace.Necromancer => MonsterType.Skeleton,
            _ => MonsterType.Unknown,
        };

    /// <summary>Puts a singular creature name into a cursor message, <c>StringReplaceWithLowercase()</c>.</summary>
    private void SetMessage(string format, Troop troop) =>
        Message = Army.StringReplaceWithLowercase(
            format,
            "%{name}",
            MonsterNames.GetSingular(troop.Monster));

    /// <summary>Puts a singular creature name into a cursor message and returns it.</summary>
    private static string ReplaceName(string text, string placeholder, Troop troop) =>
        Army.StringReplaceWithLowercase(text, placeholder, MonsterNames.GetSingular(troop.Monster));

    private ArmyBarEvent ActionBarCursor(int index, LocalEvent events)
    {
        Troop troop = GetTroop(index)!;

        if (troop.IsValid && !IsReadOnly && events.MouseClickMiddle())
        {
            RedistributeTroopByOne(troop, _army!);
            return new ArmyBarEvent(true);
        }

        if (IsSelected)
        {
            Troop selected = GetTroop(_selectedIndex)!;

            if (ReferenceEquals(troop, selected))
            {
                SetMessage("View %{name}", troop);
            }
            else if (!troop.IsValid)
            {
                if (!IsReadOnly)
                {
                    // The reference's string really ends in a space (army_bar.cpp:335).
                    SetMessage(
                        selected.Count == 1 ? "Move the %{name} " : "Move or right click to redistribute %{name}",
                        selected);
                }
            }
            else if (troop.Monster == selected.Monster)
            {
                if (!IsReadOnly)
                {
                    SetMessage("Combine %{name} armies", troop);
                }
            }
            else if (!IsReadOnly)
            {
                Message = ReplaceName(
                    ReplaceName("Exchange %{name2} with %{name}", "%{name}", troop),
                    "%{name2}",
                    selected);
            }
        }
        else if (troop.IsValid)
        {
            SetMessage("Select %{name}", troop);
        }

        return default;
    }

    private ArmyBarEvent ActionBarCursor(int index, ArmyBar other)
    {
        Troop dest = GetTroop(index)!;
        Troop selected = other.GetTroop(other._selectedIndex)!;

        // Whether the move would take the other army's last stack (army_bar.cpp:366): the destination bar's
        // _saveLastTroop combined with the source army's own answer.
        bool saveLastTroop = SaveLastTroop && other._army is { } sourceArmy && sourceArmy.GetCount() <= 1 &&
                             sourceArmy.SaveLastTroop();

        if (dest.IsValid)
        {
            if (dest.Monster != selected.Monster)
            {
                Message = ReplaceName(
                    ReplaceName("Exchange %{name2} with %{name}", "%{name}", dest),
                    "%{name2}",
                    selected);
            }
            else if (saveLastTroop)
            {
                Message = "Cannot move last troop";
            }
            else
            {
                SetMessage("Combine %{name} armies", dest);
            }
        }
        else if (saveLastTroop)
        {
            Message = "Cannot move last troop";
        }
        else
        {
            SetMessage(
                selected.Count == 1 ? "Move the %{name}" : "Move or right click to redistribute %{name}",
                selected);
        }

        return default;
    }

    private ArmyBarEvent ActionBarLeftMouseSingleClick(int index, ArmyBarHotKey hotKey)
    {
        if (IsSelected)
        {
            if (IsReadOnly)
            {
                return default; // reset cursor
            }

            Troop selected = GetTroop(_selectedIndex)!;
            Troop troop = GetTroop(index)!;

            // Shift is decided first: clicking a different creature with shift down must not open the split dialog,
            // which would be ambiguous (army_bar.cpp:411-415).
            if (hotKey == ArmyBarHotKey.SplitStackByHalf)
            {
                if (!troop.IsValid || troop.Monster == selected.Monster)
                {
                    return new ArmyBarEvent(false, RedistributeArmy(selected, _army!, troop, _army!));
                }

                return default;
            }

            if (troop.IsValid)
            {
                if (IsSplitHotkeyUsed(troop, _army!, hotKey))
                {
                    return default;
                }

                if (troop.Monster != selected.Monster)
                {
                    Army.SwapTroops(troop, selected);
                }
                else if (SaveLastTroop && _army!.SaveLastTroop())
                {
                    // The army's only stack: all but one creature moves over (army_bar.cpp:420-423).
                    troop.Count += selected.Count - 1;
                    selected.Count = 1;
                }
                else
                {
                    troop.Count += selected.Count;
                    selected.Reset();
                }
            }
            else if (SaveLastTroop && _army!.SaveLastTroop())
            {
                // An empty destination, and the source is the army's only stack (army_bar.cpp:426-434).
                troop.Set(selected.Monster, selected.Count - 1);
                selected.Count = 1;
            }
            else
            {
                Army.SwapTroops(troop, selected);
            }

            return default; // reset cursor
        }

        Troop target = GetTroop(index)!;

        if (target.IsValid)
        {
            if (!IsReadOnly && IsSplitHotkeyUsed(target, _army!, hotKey))
            {
                return default;
            }

            return new ArmyBarEvent(true); // select
        }

        if (CanChange)
        {
            // Dialog::selectMonster() and Dialog::SelectCount() move out of the bar (army_bar.cpp:448-489).
            return new ArmyBarEvent(false, null, null, new ArmyBarAddTroop(target, GetDefaultMonster()));
        }

        return default;
    }

    private ArmyBarEvent ActionBarLeftMouseSingleClick(int index, ArmyBar other, ArmyBarHotKey hotKey)
    {
        Troop dest = GetTroop(index)!;
        Troop selected = other.GetTroop(other._selectedIndex)!;

        bool sameTroopType = dest.IsValid && dest.Monster == selected.Monster;

        // Shift, again before anything else (army_bar.cpp:501-509).
        if (hotKey == ArmyBarHotKey.SplitStackByHalf)
        {
            if (dest.IsEmpty || sameTroopType)
            {
                ResetSelected();
                return new ArmyBarEvent(false, RedistributeArmy(selected, other._army!, dest, _army!));
            }

            return default;
        }

        if (!dest.IsEmpty)
        {
            if (IsSplitHotkeyUsed(dest, _army!, hotKey))
            {
                return default;
            }

            if (!sameTroopType)
            {
                Army.SwapTroops(dest, selected);
            }
            else if (SaveLastTroop && other._army!.SaveLastTroop())
            {
                dest.Count += selected.Count - 1;
                selected.Count = 1;
            }
            else
            {
                dest.Count += selected.Count;
                selected.Reset();
            }
        }
        else if (SaveLastTroop && other._army!.SaveLastTroop())
        {
            dest.Set(selected.Monster, selected.Count - 1);
            selected.Count = 1;
        }
        else
        {
            Army.SwapTroops(dest, selected);
        }

        return default; // reset cursor
    }

    private ArmyBarEvent ActionBarLeftMouseDoubleClick(int index, ArmyBarHotKey hotKey)
    {
        Troop troop = GetTroop(index)!;

        if (troop.IsValid && !IsReadOnly && hotKey != ArmyBarHotKey.None && IsSplitHotkeyUsed(troop, _army!, hotKey))
        {
            ResetSelected();
            return new ArmyBarEvent(true);
        }

        Troop? selected = GetTroop(_selectedIndex);

        if (selected is null || troop.Monster != selected.Monster)
        {
            return new ArmyBarEvent(true);
        }

        DialogButton flags = InfoButtons;

        if (!IsReadOnly)
        {
            if (!SaveLastTroop || !_army!.SaveLastTroop())
            {
                flags |= DialogButton.Dismiss;
            }

            if (troop.IsAllowUpgrade && _army!.Commander?.InCastle is { } castle &&
                castle.Race == troop.GetRace() &&
                castle.IsBuild(DwellingMonsters.GetDwelling(troop.GetUpgrade())))
            {
                flags |= DialogButton.Upgrade;

                if (CanAffordUpgrade is { } canAfford && !canAfford(troop.GetTotalUpgradeCost()))
                {
                    flags |= DialogButton.Max;
                }
            }
        }

        ResetSelected();

        // Dialog::ArmyInfo() is a model the caller drives; its answer goes to ApplyInfoAnswer().
        return new ArmyBarEvent(true, null, ArmyInfoDialog.Create(troop, flags, false, 0, 0));
    }

    private ArmyBarEvent ActionBarLeftMouseRelease(LocalEvent events, int index)
    {
        if (IsReadOnly)
        {
            return new ArmyBarEvent(true);
        }

        int sourceIndex = GetItemIndexAt(events.LeftButtonPressPosition);

        if (sourceIndex < 0)
        {
            return new ArmyBarEvent(true);
        }

        Troop source = GetTroop(sourceIndex)!;

        if (!source.IsValid)
        {
            return new ArmyBarEvent(true);
        }

        Troop dest = GetTroop(index)!;

        if (ReferenceEquals(source, dest))
        {
            return new ArmyBarEvent(true);
        }

        if (dest.IsValid && dest.Monster != source.Monster)
        {
            return new ArmyBarEvent(true);
        }

        ArmyBarSplit? split = RedistributeArmy(source, _army!, dest, _army!);

        if (IsSelected)
        {
            ResetSelected();
        }

        return new ArmyBarEvent(true, split);
    }

    private ArmyBarEvent ActionBarLeftMouseRelease(int index, ArmyBar other, int otherIndex)
    {
        if (IsSelected)
        {
            ResetSelected();
        }

        if (IsReadOnly)
        {
            return default;
        }

        Troop selected = other.GetTroop(otherIndex)!;

        if (!selected.IsValid)
        {
            return default;
        }

        Troop dest = GetTroop(index)!;

        if (dest.IsValid && dest.Monster != selected.Monster)
        {
            return default;
        }

        return new ArmyBarEvent(true, RedistributeArmy(selected, other._army!, dest, _army!));
    }

    private ArmyBarEvent ActionBarRightMouseHold(int index)
    {
        // A right click that is going to redistribute must not also open the information window
        // (army_bar.cpp:658-661).
        if (AbleToRedistributeArmyOnRightMouseSingleClick(index))
        {
            return default;
        }

        Troop troop = GetTroop(index)!;

        if (troop.IsValid)
        {
            ResetSelected();

            if (CanChange && (!SaveLastTroop || !_army!.SaveLastTroop()))
            {
                troop.Reset();
            }
            else
            {
                return new ArmyBarEvent(true, null, ArmyInfoDialog.Create(troop));
            }
        }

        return new ArmyBarEvent(true);
    }

    private ArmyBarEvent ActionBarRightMouseSingleClick(int index)
    {
        if (!AbleToRedistributeArmyOnRightMouseSingleClick(index))
        {
            return default;
        }

        Troop selected = GetTroop(_selectedIndex)!;
        ArmyBarSplit? split = RedistributeArmy(selected, _army!, GetTroop(index)!, _army!);
        ResetSelected();

        return new ArmyBarEvent(true, split);
    }

    private ArmyBarEvent ActionBarRightMouseSingleClick(int index, ArmyBar other)
    {
        Troop dest = GetTroop(index)!;
        Troop selected = other.GetTroop(other._selectedIndex)!;

        if (!dest.IsValid || dest.Monster == selected.Monster)
        {
            ArmyBarSplit? split = RedistributeArmy(selected, other._army!, dest, _army!);
            ResetSelected();

            return new ArmyBarEvent(true, split);
        }

        return new ArmyBarEvent(true);
    }

    private bool AbleToRedistributeArmyOnRightMouseSingleClick(int index)
    {
        if (IsReadOnly || !IsSelected)
        {
            return false;
        }

        Troop selected = GetTroop(_selectedIndex)!;
        Troop troop = GetTroop(index)!;

        // A stack cannot be redistributed into itself, which the reference checks on the pointers
        // (army_bar.cpp:734-737).
        if (ReferenceEquals(troop, selected))
        {
            return false;
        }

        return !troop.IsValid || troop.Monster == selected.Monster;
    }
}
