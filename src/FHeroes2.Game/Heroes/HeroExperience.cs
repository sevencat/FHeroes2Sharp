namespace FHeroes2.Game.Heroes;

/// <summary>
/// The experience thresholds of a hero — <c>Heroes::GetExperienceFromLevel</c>,
/// <c>Heroes::GetLevelFromExperience</c> and <c>Heroes::getExperienceMaxValue</c>
/// (<c>heroes.cpp:1501-1607</c>).
/// </summary>
/// <remarks>
/// <para>
/// The table is the original game's, not a formula: the first thirty-nine levels are hardcoded in
/// <c>heroes.cpp</c> and only the levels above them grow by a rule — 1.2% of the previous step, rounded to
/// hundreds. The numbers are kept verbatim because the game's balance lives in them: level 2 costs 1000
/// experience, level 10 costs 15500, and the last level of the table costs 2990600, which is also the
/// experience cap the original game uses.
/// </para>
/// <para>
/// <b>The cap is 39.</b> <see cref="MaxValue"/> equals <see cref="FromLevel"/>(39), and no hero of the original
/// game can hold more than that; the geometric tail exists because the reference can still be asked for a
/// higher level and the level-up message of a save file with a hacked level would otherwise read zero.
/// </para>
/// <para>
/// <b>Levels above 254 do not exist.</b> <see cref="LevelFrom"/> looks for a level whose threshold is above the
/// given experience and gives up after 254, returning zero — the reference returns zero there too, and the
/// callers treat it as "no level" rather than as an error.
/// </para>
/// </remarks>
public static class HeroExperience
{
    /// <summary>
    /// The highest experience a hero can hold — <c>Heroes::getExperienceMaxValue()</c>, the threshold of
    /// level 39.
    /// </summary>
    public const uint MaxValue = 2990600;

    /// <summary>
    /// The highest level the table, and therefore the game, knows — the last hardcoded row of
    /// <c>GetExperienceFromLevel</c>.
    /// </summary>
    public const int MaxTableLevel = 39;

    /// <summary>
    /// The experience at which a hero of a level reaches the <b>next</b> one —
    /// <c>Heroes::GetExperienceFromLevel</c>.
    /// </summary>
    /// <param name="level">The level, zero for a hero that has not been levelled yet.</param>
    /// <returns>
    /// The threshold of that level: a hero with exactly <c>FromLevel( 1 )</c> = 1000 experience is level 2,
    /// which is the sense in which <see cref="LevelFrom"/> turns the number back into a level.
    /// </returns>
    /// <remarks>
    /// From level 40 on the value is grown from the two levels below it: the step from the previous level is
    /// multiplied by 1.2, rounded to the nearest whole number of hundreds, and added. The reference does this
    /// in floating point (<c>round( ( l1 - GetExperienceFromLevel( level - 2 ) ) * 1.2 / 100 ) * 100</c>), so the
    /// port does too — replacing it with integer arithmetic rounds the intermediate value differently and
    /// drifts by hundreds of points after a few dozen levels.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is negative.</exception>
    public static uint FromLevel(int level)
    {
        if (level < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "A level cannot be negative.");
        }

        if (level <= MaxTableLevel)
        {
            return Thresholds[level];
        }

        uint previous = FromLevel(level - 1);
        uint stepBefore = previous - FromLevel(level - 2);
        uint growth = (uint)(Math.Round(stepBefore * 1.2 / 100.0, MidpointRounding.AwayFromZero) * 100);

        return previous + growth;
    }

    /// <summary>
    /// The level a hero with the given experience has — <c>Heroes::GetLevelFromExperience</c>.
    /// </summary>
    /// <param name="experience">The experience of the hero.</param>
    /// <returns>
    /// The lowest level whose threshold is still above <paramref name="experience"/> — 1000 experience is level
    /// 2, 999 is level 1 — and zero when no threshold of the 254 levels the reference tries is above it.
    /// </returns>
    public static int LevelFrom(uint experience)
    {
        for (int level = 1; level < 255; ++level)
        {
            if (experience < FromLevel(level))
            {
                return level;
            }
        }

        return 0;
    }

    /// <summary>
    /// <c>Heroes::GetExperienceFromLevel</c> (<c>heroes.cpp:1512-1602</c>), levels 0 to 39 verbatim.
    /// </summary>
    private static readonly uint[] Thresholds =
    [
        0, // 0
        1000, // 1
        2000, // 2
        3200, // 3
        4500, // 4
        6000, // 5
        7700, // 6
        9000, // 7
        11000, // 8
        13200, // 9
        15500, // 10
        18500, // 11
        22100, // 12
        26400, // 13
        31600, // 14
        37800, // 15
        45300, // 16
        54200, // 17
        65000, // 18
        78000, // 19
        93600, // 20
        112300, // 21
        134700, // 22
        161600, // 23
        193900, // 24
        232700, // 25
        279300, // 26
        335200, // 27
        402300, // 28
        482800, // 29
        579400, // 30
        695300, // 31
        834400, // 32
        1001300, // 33
        1201600, // 34
        1442000, // 35
        1730500, // 36
        2076700, // 37
        2492100, // 38
        2990600, // 39
    ];
}
