namespace FHeroes2.Game.Army;

/// <summary>
/// The nine size brackets of a stack, <c>ArmySize</c> (<c>army.cpp:74-85</c>).
/// </summary>
/// <remarks>
/// <para>
/// The values are the <b>lower bounds</b> of the brackets, not their indices: a stack of exactly ten creatures is
/// <see cref="Pack"/>, and <c>getArmySize()</c> is a chain of "is the count at least this bracket" comparisons.
/// The reference keeps the enum in an anonymous namespace and the port makes it public because the bounds are
/// what the tests and any caller that wants to word a size itself need.
/// </para>
/// </remarks>
public enum ArmySize : uint
{
    /// <summary><c>ARMY_FEW</c> — 1 to 4 creatures.</summary>
    Few = 1,

    /// <summary><c>ARMY_SEVERAL</c> — 5 to 9.</summary>
    Several = 5,

    /// <summary><c>ARMY_PACK</c> — 10 to 19.</summary>
    Pack = 10,

    /// <summary><c>ARMY_LOTS</c> — 20 to 49.</summary>
    Lots = 20,

    /// <summary><c>ARMY_HORDE</c> — 50 to 99.</summary>
    Horde = 50,

    /// <summary><c>ARMY_THRONG</c> — 100 to 249.</summary>
    Throng = 100,

    /// <summary><c>ARMY_SWARM</c> — 250 to 499.</summary>
    Swarm = 250,

    /// <summary><c>ARMY_ZOUNDS</c> — 500 to 999.</summary>
    Zounds = 500,

    /// <summary><c>ARMY_LEGION</c> — 1000 and up.</summary>
    Legion = 1000,
}

/// <summary>
/// How many creatures a stack holds, in the wording the interface uses, <c>getArmySize()</c>
/// (<c>army.cpp:93-122</c>) and the two strings built from it.
/// </summary>
/// <remarks>
/// <para>
/// This is the map's "thirty peasants" reading: the player sees a size bracket rather than a number until he
/// fights, so every bracket boundary is a gameplay number rather than a cosmetic one — nine is "several", ten is
/// "a pack of".
/// </para>
/// <para>
/// <b>Not here:</b> <c>Army::TroopSizeString()</c> (<c>army.cpp:155-204</c>), which wraps the bracket into
/// two lines together with the monster's <i>plural</i> name. The port has no plural names
/// (<c>Troop::GetMultiName()</c> is not ported) and no translation layer. <c>Army::String()</c> is missing for
/// the same reason.
/// </para>
/// <para>
/// The reference's two strings are chosen between by <c>Settings::isArmyEstimationViewNumeric()</c>. The port
/// has no such setting yet, so the two variants are two methods and the caller decides — that keeps this file
/// free of the settings object and lets the tests check both wordings.
/// </para>
/// </remarks>
public sealed partial class Army
{
    /// <summary>
    /// The bracket a count falls into, <c>getArmySize()</c> (<c>army.cpp:93-122</c>).
    /// </summary>
    /// <param name="count">Number of creatures in a stack.</param>
    /// <returns>The bracket; <see cref="ArmySize.Few"/> for zero, which cannot be shown but must return something.</returns>
    public static ArmySize GetArmySize(uint count) => count switch
    {
        >= (uint)ArmySize.Legion => ArmySize.Legion,
        >= (uint)ArmySize.Zounds => ArmySize.Zounds,
        >= (uint)ArmySize.Swarm => ArmySize.Swarm,
        >= (uint)ArmySize.Throng => ArmySize.Throng,
        >= (uint)ArmySize.Horde => ArmySize.Horde,
        >= (uint)ArmySize.Lots => ArmySize.Lots,
        >= (uint)ArmySize.Pack => ArmySize.Pack,
        >= (uint)ArmySize.Several => ArmySize.Several,
        _ => ArmySize.Few,
    };

    /// <summary>
    /// The numeric wording of a size, <c>troopSizeNumbers</c> (<c>army.cpp:87-91</c>).
    /// </summary>
    /// <remarks>
    /// The ranges are text in the reference too ("1-4", "1000+"), not numbers computed from the enum, because
    /// the open ends cannot be derived. Used when the player asked for numeric army estimates.
    /// </remarks>
    /// <param name="count">Number of creatures in a stack.</param>
    /// <returns>The range, e.g. <c>"10-19"</c>.</returns>
    public static string SizeRangeString(uint count) => GetArmySize(count) switch
    {
        ArmySize.Several => "5-9",
        ArmySize.Pack => "10-19",
        ArmySize.Lots => "20-49",
        ArmySize.Horde => "50-99",
        ArmySize.Throng => "100-249",
        ArmySize.Swarm => "250-499",
        ArmySize.Zounds => "500-999",
        ArmySize.Legion => "1000+",
        _ => "1-4",
    };

    /// <summary>
    /// The verbal wording of a size, <c>Army::SizeString()</c> (<c>army.cpp:206-240</c>) with the estimates
    /// setting off.
    /// </summary>
    /// <remarks>
    /// One word, no monster name — the reference's <c>_( "army|Few" )</c> family, where the prefix is the
    /// translation context. <c>Army::TroopSizeString()</c> is the two-line variant that also needs the plural
    /// monster name; it is not ported.
    /// </remarks>
    /// <param name="count">Number of creatures in a stack.</param>
    /// <returns>The word, e.g. <c>"Pack"</c>.</returns>
    public static string SizeString(uint count) => GetArmySize(count) switch
    {
        ArmySize.Several => "Several",
        ArmySize.Pack => "Pack",
        ArmySize.Lots => "Lots",
        ArmySize.Horde => "Horde",
        ArmySize.Throng => "Throng",
        ArmySize.Swarm => "Swarm",
        ArmySize.Zounds => "Zounds",
        ArmySize.Legion => "Legion",
        _ => "Few",
    };
}
