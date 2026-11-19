using FHeroes2.Core.Random;

namespace FHeroes2.Game;

/// <summary>
/// The rumor pool of the world, <c>World::getCurrentRumor()</c> (<c>world.cpp:632-703</c>).
/// </summary>
/// <remarks>
/// <para>
/// The pool is nine built-in rumors plus whatever the map shipped (already in the port: <see cref="Rumors"/>,
/// <c>_customRumors</c> of <c>world.h:558</c>). Nine is the count of the <c>switch</c> in the reference, not a limit the
/// map has to respect: a map may add any number of its own, and every id from <see cref="StandardRumorCount"/> upwards
/// indexes a custom one.
/// </para>
/// <para>
/// The id is drawn from <c>Rand::GetWithSeed( 0, total - 1, GetWeekSeed() )</c>, so the same rumor is told all week and
/// another one next week; <see cref="WeekSeed"/> carries the map seed and the week (<c>world.cpp:1263-1270</c>).
/// </para>
/// <para>
/// The sentence itself is built by <see cref="Describe"/>, which is static and takes the two variable parts as
/// arguments, so all nine branches can be checked without a map. <see cref="GetCurrentRumor"/> only decides where
/// those parts come from: the artifact's name, the direction of its tile, and the map's own rumors.
/// </para>
/// <para>
/// <b>A custom rumor carries no language tag.</b> The reference attaches
/// <c>Settings::Get().getCurrentMapInfo().getSupportedLanguage()</c>; for a classic MP2 map that is always empty —
/// <c>maps_fileinfo.h:148</c> only knows a language for Resurrection maps — and the port reads nothing else, so
/// <see langword="null"/> is the right answer today rather than a stand-in. The <see cref="LocalizedString"/> is in
/// place for the day <c>.fh2m</c> maps arrive. See <c>docs/DECISIONS.md</c>, ADR-170.
/// </para>
/// </remarks>
public sealed partial class World
{
    /// <summary>
    /// How many rumors the reference ships, the <c>standardRumorCount</c> of <c>getCurrentRumor()</c>.
    /// </summary>
    /// <remarks>
    /// Ids <c>0</c> to <c>8</c> are built in; an id of <see cref="StandardRumorCount"/> or more is
    /// <c>_customRumors[id - StandardRumorCount]</c>.
    /// </remarks>
    public const int StandardRumorCount = 9;

    /// <summary>
    /// Rumor <c>0</c>: the ultimate artifact and its name, <c>"The ultimate artifact is really the %{name}."</c>.
    /// </summary>
    /// <remarks><c>%{name}</c> is the artifact's own name, <c>_ultimateArtifact.GetName()</c>.</remarks>
    public const string UltimateArtifactNameRumor = "The ultimate artifact is really the %{name}.";

    /// <summary>
    /// Rumor <c>1</c>: where the ultimate artifact lies, <c>"The ultimate artifact may be found in the %{name} regions
    /// of the world."</c>.
    /// </summary>
    /// <remarks><c>%{name}</c> is a direction word, see <see cref="GetRegionName"/>.</remarks>
    public const string UltimateArtifactRegionRumor =
        "The ultimate artifact may be found in the %{name} regions of the world.";

    /// <summary>Rumor <c>2</c>: <c>"The truth is out there."</c>.</summary>
    public const string TruthRumor = "The truth is out there.";

    /// <summary>Rumor <c>3</c>: <c>"The dark side is stronger."</c>.</summary>
    public const string DarkSideRumor = "The dark side is stronger.";

    /// <summary>Rumor <c>4</c>: <c>"The end of the world is near."</c>.</summary>
    public const string EndOfWorldRumor = "The end of the world is near.";

    /// <summary>
    /// Rumor <c>5</c>: <c>"The bones of Lord Slayer are buried in the foundation of the arena."</c>.
    /// </summary>
    public const string LordSlayerRumor = "The bones of Lord Slayer are buried in the foundation of the arena.";

    /// <summary>
    /// Rumor <c>6</c>: <c>"A Black Dragon will take out a Titan any day of the week."</c>.
    /// </summary>
    public const string BlackDragonRumor = "A Black Dragon will take out a Titan any day of the week.";

    /// <summary>
    /// Rumor <c>7</c>: <c>"He told her: Yada yada yada... and then she said: Blah, blah, blah..."</c>.
    /// </summary>
    public const string SmallTalkRumor =
        "He told her: Yada yada yada... and then she said: Blah, blah, blah...";

    /// <summary>Rumor <c>8</c>: <c>"An unknown force is being resurrected..."</c>.</summary>
    public const string UnknownForceRumor = "An unknown force is being resurrected...";

    /// <summary>Direction word of <see cref="UltimateArtifactRegionRumor"/>, <c>_( "north-west" )</c>.</summary>
    public const string RegionNorthWest = "north-west";

    /// <summary>Direction word of <see cref="UltimateArtifactRegionRumor"/>, <c>_( "north" )</c>.</summary>
    public const string RegionNorth = "north";

    /// <summary>Direction word of <see cref="UltimateArtifactRegionRumor"/>, <c>_( "north-east" )</c>.</summary>
    public const string RegionNorthEast = "north-east";

    /// <summary>Direction word of <see cref="UltimateArtifactRegionRumor"/>, <c>_( "west" )</c>.</summary>
    public const string RegionWest = "west";

    /// <summary>Direction word of <see cref="UltimateArtifactRegionRumor"/>, <c>_( "center" )</c>.</summary>
    public const string RegionCenter = "center";

    /// <summary>Direction word of <see cref="UltimateArtifactRegionRumor"/>, <c>_( "east" )</c>.</summary>
    public const string RegionEast = "east";

    /// <summary>Direction word of <see cref="UltimateArtifactRegionRumor"/>, <c>_( "south-west" )</c>.</summary>
    public const string RegionSouthWest = "south-west";

    /// <summary>Direction word of <see cref="UltimateArtifactRegionRumor"/>, <c>_( "south" )</c>.</summary>
    public const string RegionSouth = "south";

    /// <summary>Direction word of <see cref="UltimateArtifactRegionRumor"/>, <c>_( "south-east" )</c>.</summary>
    public const string RegionSouthEast = "south-east";

    /// <summary>
    /// How many rumors the tavern can tell, <c>_customRumors.size() + standardRumorCount</c>.
    /// </summary>
    /// <remarks>
    /// The map's own rumors come last, so the pool only grows at the end and the built-in ids stay put.
    /// </remarks>
    public int TotalRumorCount => StandardRumorCount + _rumors.Count;

    /// <summary>
    /// The rumor of this week, <c>World::getCurrentRumor()</c> (<c>world.cpp:632-703</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference picks an id and switches on it; the port does the same, with the switch moved into
    /// <see cref="Describe"/> so that only the two inputs of the sentence have to be gathered here.
    /// </para>
    /// <para>
    /// The direction is computed <b>only for id <c>1</c></b>: it needs the map's width to turn the artifact's tile
    /// index into a point, and a world with no map loaded would divide by zero for an answer it did not need.
    /// </para>
    /// <para>
    /// <b>Both artifact rumors assume the map placed the artifact</b> (<c>Position</c> &gt;= 0). The reference assumes
    /// the same — it feeds the position straight to <c>Maps::GetPoint()</c>, and a map without a puzzle never asks.
    /// </para>
    /// </remarks>
    /// <returns>The sentence and, for a custom rumor, the language it is written in.</returns>
    public LocalizedString GetCurrentRumor()
    {
        int rumorId = ChooseRumorId(WeekSeed, TotalRumorCount);

        // The artifact's region is asked for only when the sentence uses it.
        string region = rumorId == 1 ? GetUltimateArtifactRegion() : string.Empty;

        return Describe(rumorId, _ultimateArtifact.Name, region, _rumors);
    }

    /// <summary>
    /// The direction word of the tile the ultimate artifact lies on, the <c>Maps::GetPoint()</c> call of
    /// <c>world.cpp:644</c> followed by <see cref="GetRegionName"/>.
    /// </summary>
    /// <remarks>
    /// A tile index is <c>y * width + x</c>, so the two coordinates are the remainder and the quotient. The artifact
    /// has no position before a map is loaded, and the reference would quietly answer for tile <c>-1</c>; the port
    /// passes it through and lets <see cref="GetRegionName"/> decide, which lands in the first band.
    /// </remarks>
    /// <returns>One of the nine <c>Region*</c> constants.</returns>
    private string GetUltimateArtifactRegion()
    {
        int position = _ultimateArtifact.Position;

        return GetRegionName(position % Width, position / Width, Width, Height);
    }

    /// <summary>
    /// The sentence of a rumor id, the <c>switch</c> of <c>world.cpp:638-702</c> with its variable parts passed in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Static and argument-driven so that all nine built-in branches and the custom fallback can be checked without a
    /// map, a week or an artifact — the same reason <c>RecruitHeroDialog.Describe</c> is static.
    /// </para>
    /// <para>
    /// Ids <c>0</c> to <c>8</c> are the <c>%{name}</c> templates and the seven fixed sentences; everything from
    /// <see cref="StandardRumorCount"/> up is <c>_customRumors[id - 9]</c>, returned as-is with no language (see the
    /// type's remarks).
    /// </para>
    /// </remarks>
    /// <param name="rumorId">The id, <see cref="ChooseRumorId"/>'s answer.</param>
    /// <param name="ultimateArtifactName">The name of the artifact the puzzle hides, <c>_ultimateArtifact.Name</c>.</param>
    /// <param name="region">
    /// The direction word for <paramref name="rumorId"/> <c>1</c>, <see cref="GetRegionName"/>'s answer.
    /// </param>
    /// <param name="customRumors">The map's own rumors, <see cref="Rumors"/>.</param>
    /// <returns>The sentence, never empty.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rumorId"/> is negative or beyond <paramref name="customRumors"/>.
    /// </exception>
    public static LocalizedString Describe(int rumorId,
        string ultimateArtifactName,
        string region,
        IReadOnlyList<string> customRumors)
    {
        switch (rumorId)
        {
            case 0:
                return new LocalizedString(UltimateArtifactNameRumor.Replace("%{name}", ultimateArtifactName,
                    StringComparison.Ordinal));
            case 1:
                return new LocalizedString(UltimateArtifactRegionRumor.Replace("%{name}", region,
                    StringComparison.Ordinal));
            case 2:
                return new LocalizedString(TruthRumor);
            case 3:
                return new LocalizedString(DarkSideRumor);
            case 4:
                return new LocalizedString(EndOfWorldRumor);
            case 5:
                return new LocalizedString(LordSlayerRumor);
            case 6:
                return new LocalizedString(BlackDragonRumor);
            case 7:
                return new LocalizedString(SmallTalkRumor);
            case 8:
                return new LocalizedString(UnknownForceRumor);
            default:
                break;
        }

        int customRumor = rumorId - StandardRumorCount;

        // The reference asserts that the id is in range and then indexes; a bad id is a bug in the caller, and the
        // port says so instead of reading whatever the list happens to hold.
        if (customRumor < 0 || customRumor >= customRumors.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rumorId), rumorId,
                $"The pool has {StandardRumorCount} built-in rumors and {customRumors.Count} of the map's own.");
        }

        return new LocalizedString(customRumors[customRumor]);
    }

    /// <summary>
    /// Which rumor of the pool this week gets, <c>Rand::GetWithSeed( 0, totalRumorCount - 1, GetWeekSeed() )</c>
    /// (<c>world.cpp:636</c>).
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="GetCurrentRumor"/> so that the choice can be checked without a world: the reference
    /// hashes nothing itself, it hands the week seed to the generator, and the seed already carries the map seed and
    /// the week (<see cref="WeekSeed"/>). A <paramref name="totalRumorCount"/> of zero cannot happen — the nine
    /// built-in rumors are always there — but the port will have to decide what to answer rather than divide by zero.
    /// </remarks>
    /// <param name="weekSeed">The seed of the week, <see cref="WeekSeed"/>.</param>
    /// <param name="totalRumorCount">Size of the pool, <see cref="TotalRumorCount"/>.</param>
    /// <returns>The id, <c>0</c> to <paramref name="totalRumorCount"/> - 1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="totalRumorCount"/> is not positive. The nine built-in rumors make that impossible from
    /// <see cref="GetCurrentRumor"/>, but a caller with a hand-made pool can still ask, and the reference would hand a
    /// <c>-1</c> to an unsigned generator.
    /// </exception>
    public static int ChooseRumorId(uint weekSeed, int totalRumorCount)
    {
        if (totalRumorCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRumorCount), totalRumorCount,
                $"A rumor pool always holds the {StandardRumorCount} built-in rumors.");
        }

        return (int)Rand.GetWithSeed(0, (uint)(totalRumorCount - 1), weekSeed);
    }

    /// <summary>
    /// The direction word that goes into <see cref="UltimateArtifactRegionRumor"/>, the <c>if</c> chain of
    /// <c>world.cpp:643-679</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The map is cut into thirds both ways and the artefact's tile picks one of nine words. The comparisons are strict
    /// (<c>height / 3 &gt; y</c>, not <c>&gt;=</c>), so a tile exactly on a third falls into the band beyond it.
    /// </para>
    /// <para>
    /// The row is decided first — north, then the middle band, then south — and the column inside it second, which is
    /// why the middle band answers <c>west</c>/<c>center</c>/<c>east</c> rather than a ninth of its own.
    /// </para>
    /// </remarks>
    /// <param name="x">Column of the artefact's tile.</param>
    /// <param name="y">Row of the artefact's tile.</param>
    /// <param name="width">Width of the map.</param>
    /// <param name="height">Height of the map.</param>
    /// <returns>One of the nine <c>Region*</c> constants.</returns>
    public static string GetRegionName(int x, int y, int width, int height)
    {
        int thirdWidth = width / 3;
        int twoThirdsWidth = 2 * width / 3;
        int thirdHeight = height / 3;
        int twoThirdsHeight = 2 * height / 3;

        if (y < thirdHeight)
        {
            return x < thirdWidth ? RegionNorthWest : x < twoThirdsWidth ? RegionNorth : RegionNorthEast;
        }

        if (y < twoThirdsHeight)
        {
            return x < thirdWidth ? RegionWest : x < twoThirdsWidth ? RegionCenter : RegionEast;
        }

        return x < thirdWidth ? RegionSouthWest : x < twoThirdsWidth ? RegionSouth : RegionSouthEast;
    }
}
