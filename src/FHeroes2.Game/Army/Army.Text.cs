using System.Globalization;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Army;

/// <summary>
/// The wording of a count and of a stack, <c>Game::formatMonsterCount()</c> (<c>game.cpp:461</c>) and
/// <c>Army::TroopSizeString()</c> (<c>army.cpp:155</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>Game::formatMonsterCount()</c> belongs to <c>game.cpp</c> in the reference, but it has one subject — how many
/// creatures a stack holds — and both of its branches are already here (<see cref="SizeRangeString"/> and
/// <see cref="SizeString"/>). The port keeps it next to them instead of opening a <c>Game</c> facade for four lines.
/// </para>
/// <para>
/// <b>Settings are parameters</b>, as everywhere else in this file: the reference reads
/// <c>Settings::isArmyEstimationViewNumeric()</c> and the port cannot, so the caller passes the flag. Both wordings
/// therefore stay testable without a settings object.
/// </para>
/// <para>
/// <b>No translation layer.</b> The reference's <c>_( "A few\n%{monster}" )</c> is the English string with a
/// placeholder, and <c>StringReplaceWithLowercase()</c> (<c>translations.cpp:711</c>) decides whether the creature's
/// name keeps its capital letter by looking at the character before it: at the start of a sentence the name keeps its
/// capital on the first word only, in the middle of one it is lowercased whole. That rule is about the German and
/// Russian translations as much as the English one, but it is cheap and invisible when it is right, so
/// <see cref="TroopSizeString"/> follows it.
/// </para>
/// </remarks>
public sealed partial class Army
{
    /// <summary>
    /// How a stack's count is written, <c>Game::formatMonsterCount()</c> (<c>game.cpp:461</c>).
    /// </summary>
    /// <param name="count">Number of creatures in the stack.</param>
    /// <param name="detailed">Whether the player may see the number; false gives the size bracket.</param>
    /// <param name="abbreviate">Whether a large number is shortened to <c>1K</c>/<c>2M</c>.</param>
    /// <returns>The number as text, or <see cref="SizeString"/> when <paramref name="detailed"/> is false.</returns>
    public static string FormatMonsterCount(uint count, bool detailed, bool abbreviate = false) =>
        detailed ? (abbreviate ? AbbreviateNumber(count) : count.ToString(CultureInfo.InvariantCulture)) : SizeString(count);

    /// <summary>
    /// A large number in the shortest form that keeps its order of magnitude, <c>fheroes2::abbreviateNumber()</c>
    /// (<c>engine/tools.cpp:155</c>).
    /// </summary>
    /// <remarks>
    /// The reference rounds <i>down</i> rather than to nearest — 1999 is <c>1K</c>, not <c>2K</c> — because the
    /// number is a hint about the size of a stack, not an inventory. Counts are unsigned here, so the reference's
    /// <c>std::abs()</c> has nothing to do; it exists for the other callers that pass deltas.
    /// </remarks>
    /// <param name="number">The number to shorten.</param>
    /// <returns>The number as text, with <c>K</c> or <c>M</c> appended when it is large enough.</returns>
    public static string AbbreviateNumber(uint number) => number switch
    {
        >= 1_000_000 => (number / 1_000_000).ToString(CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (number / 1_000).ToString(CultureInfo.InvariantCulture) + "K",
        _ => number.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// The two line wording of a stack, <c>Army::TroopSizeString()</c> (<c>army.cpp:155</c>): how many, then what.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first line is the size bracket and the second the creature's plural name — "A pack of\nwolves" — with
    /// the name lowercased because it never starts the sentence; the numeric variant puts the name first and the
    /// range under it, where the name <i>does</i> start the sentence and keeps its capital.
    /// </para>
    /// <para>
    /// The reference's <c>StringReplaceWithLowercase()</c> treats "Power Liches" specially: at the start of a
    /// sentence only the first word keeps its capital, so the answer is "Power liches". That is what
    /// <see cref="ReplaceMonsterName"/> does too.
    /// </para>
    /// </remarks>
    /// <param name="troop">The stack to describe.</param>
    /// <param name="numericEstimates">Whether the player asked for numeric army estimates.</param>
    /// <returns>The text, with a newline between its two lines.</returns>
    public static string TroopSizeString(Troop troop, bool numericEstimates = false)
    {
        ArgumentNullException.ThrowIfNull(troop);

        if (numericEstimates)
        {
            return ReplaceMonsterName($"{MonsterPlaceholder}\n{SizeRangeString(troop.Count)}", troop.Monster);
        }

        string sizeWord = GetArmySize(troop.Count) switch
        {
            ArmySize.Several => "Several",
            ArmySize.Pack => "A pack of",
            ArmySize.Lots => "Lots of",
            ArmySize.Horde => "A horde of",
            ArmySize.Throng => "A throng of",
            ArmySize.Swarm => "A swarm of",
            ArmySize.Zounds => "Zounds...",
            ArmySize.Legion => "A legion of",
            _ => "A few",
        };

        return ReplaceMonsterName($"{sizeWord}\n{MonsterPlaceholder}", troop.Monster);
    }

    /// <summary>The placeholder the reference's translated strings spell the creature's name with.</summary>
    private const string MonsterPlaceholder = "%{monster}";

    /// <summary>
    /// Puts a stack's plural name into a sentence, <c>StringReplaceWithLowercase()</c>
    /// (<c>translations.cpp:711</c>).
    /// </summary>
    /// <param name="text">The text with one placeholder in it.</param>
    /// <param name="monster">The creature whose name replaces the placeholder.</param>
    /// <returns>The text with the name spelled in.</returns>
    private static string ReplaceMonsterName(string text, MonsterType monster) =>
        StringReplaceWithLowercase(text, MonsterPlaceholder, MonsterNames.GetMulti(monster));

    /// <summary>
    /// Puts a name into a sentence, <c>StringReplaceWithLowercase()</c> (<c>translations.cpp:711</c>).
    /// </summary>
    /// <remarks>
    /// The name keeps its capital only when it starts the sentence — at position zero, or after a full stop,
    /// question mark or exclamation mark with nothing but whitespace in between — and then only on its first word.
    /// Everywhere else the whole name is lowercased, which is what "A pack of\nwolves" needs.
    /// <para>
    /// The army bar spells singular names into its cursor messages with the same rule ("Select peasant"), which is
    /// why this is public: the wording belongs to the army, not to the bar.
    /// </para>
    /// </remarks>
    /// <param name="text">The text with one placeholder in it.</param>
    /// <param name="placeholder">The placeholder to replace, for example <c>"%{name}"</c>.</param>
    /// <param name="name">The name to spell in, capitalized as the reference's tables hold it.</param>
    /// <returns>The text with the name spelled in, or <paramref name="text"/> untouched when it has no placeholder.</returns>
    public static string StringReplaceWithLowercase(string text, string placeholder, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(placeholder);
        ArgumentNullException.ThrowIfNull(name);

        int position = text.IndexOf(placeholder, StringComparison.Ordinal);

        if (position < 0)
        {
            return text;
        }

        // Walk backwards over whitespace: what is left is the last character of the word before the placeholder.
        int before = position - 1;

        while (before >= 0 && char.IsWhiteSpace(text[before]))
        {
            --before;
        }

        bool startsSentence = before < 0 || text[before] is '.' or '?' or '!';

        // Only the first word keeps its capital, so "Power Liches" becomes "Power liches".
        int space = name.IndexOf(' ', StringComparison.Ordinal);

        string replacement = startsSentence && space >= 0
            ? name[..space] + name[space..].ToLower(CultureInfo.InvariantCulture)
            : (startsSentence ? name : name.ToLower(CultureInfo.InvariantCulture));

        return string.Concat(text.AsSpan(0, position), replacement, text.AsSpan(position + placeholder.Length));
    }
}
