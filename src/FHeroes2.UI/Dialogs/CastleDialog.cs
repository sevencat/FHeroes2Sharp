using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The town screen's contract, <c>Castle::OpenDialog()</c> (<c>castle_dialog.cpp:196-859</c>).
/// </summary>
/// <remarks>
/// <para>
/// Everything the window itself draws — the strip, the buildings, the armies, the cursor — is left for the renderer
/// batch. What is here are the three answers the screen gives the game loop: which town window to open next, the date
/// line at the top, and the status line under the pointer.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163).
/// </para>
/// </remarks>
public sealed class CastleDialog
{
    /// <summary>
    /// How the screen ends, <c>Castle::CastleDialogReturnValue</c> (<c>castle.h:138-149</c>).
    /// </summary>
    /// <remarks>
    /// The four "next" answers exist because the screen owns the arrows that walk through the kingdom's towns: a
    /// closed screen with a direction is not the same as a closed screen, since the caller reopens on another town.
    /// </remarks>
    public enum Result
    {
        /// <summary>Nothing happened, <c>DoNothing</c> — the screen stays open.</summary>
        DoNothing,

        /// <summary>The screen is closed, <c>Close</c>.</summary>
        Close,

        /// <summary>Close and open the next town's screen, <c>NextCastle</c>.</summary>
        NextCastle,

        /// <summary>Close and open the previous town's screen, <c>PreviousCastle</c>.</summary>
        PreviousCastle,

        /// <summary>Close and open the next town's construction window, <c>NextConstructionWindow</c>.</summary>
        NextConstructionWindow,

        /// <summary>Close and open the previous town's construction window, <c>PreviousConstructionWindow</c>.</summary>
        PreviousConstructionWindow,

        /// <summary>Close and open the next town's mage guild, <c>NextMageGuildWindow</c>.</summary>
        NextMageGuildWindow,

        /// <summary>Close and open the previous town's mage guild, <c>PreviousMageGuildWindow</c>.</summary>
        PreviousMageGuildWindow,
    }

    /// <summary>
    /// The date line, <c>getDateString()</c> (<c>castle_dialog.cpp:158-166</c>).
    /// </summary>
    /// <remarks>
    /// Capitalised and spelled out, unlike <c>World.DateString()</c> which is the lower-case form used in logs. The
    /// reference reads the numbers from the world; the port takes them as arguments so that the formatting can be
    /// checked without a running game.
    /// </remarks>
    /// <param name="month">The month, <c>world.GetMonth()</c>.</param>
    /// <param name="week">The week, <c>world.GetWeek()</c>.</param>
    /// <param name="day">The day, <c>world.GetDay()</c>.</param>
    /// <returns>The line, for example <c>"Month: 1, Week: 3, Day: 5"</c>.</returns>
    public static string GetDateString(int month, int week, int day) =>
        $"Month: {month}, Week: {week}, Day: {day}";

    /// <summary>
    /// The status line under the pointer, <c>buildingStatusMessage()</c> (<c>castle_dialog.cpp:118-131</c>).
    /// </summary>
    /// <param name="race">The town's race.</param>
    /// <param name="building">The building under the pointer.</param>
    /// <returns><c>"Recruit peasants"</c> for a dwelling, the building's name otherwise.</returns>
    public static string GetStatusLine(MapRace race, BuildingType building) =>
        BuildingInfo.GetStatusLine(race, building);
}
