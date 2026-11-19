namespace FHeroes2.Game.Kingdom;

/// <summary>
/// <c>Luck::</c> of <c>src/fheroes2/kingdom/luck.h</c>: the luck of a hero's army, a seven step scale plus
/// one "unknown" value.
/// </summary>
/// <remarks>
/// <para>
/// The scale is a plain <see cref="int"/> in the reference as well (<c>namespace Luck { enum { ... }; }</c>
/// is an unnamed enum), because luck values are added and clamped rather than stored: +1 from the luck
/// skill, +1 from a visited fountain, −1 from a pyramid, and <see cref="Normalize"/> clamps the sum into
/// <see cref="Cursed"/>…<see cref="Irish"/>.
/// </para>
/// <para>
/// <c>Luck::String()</c> returns the reference's English (<see cref="GetName"/>); <c>Luck::Description()</c>
/// is a paragraph of translated text (<c>translations.cpp</c>) and is not ported.
/// </para>
/// </remarks>
public static class Luck
{
    /// <summary><c>Luck::UNKNOWN</c>, the value of a hero who is not in the game yet.</summary>
    public const int Unknown = -4;

    /// <summary><c>Luck::CURSED</c>: half damage attacks.</summary>
    public const int Cursed = -3;

    /// <summary><c>Luck::AWFUL</c>.</summary>
    public const int Awful = -2;

    /// <summary><c>Luck::BAD</c>.</summary>
    public const int Bad = -1;

    /// <summary><c>Luck::NORMAL</c>, the value of an army without any luck modifier.</summary>
    public const int Normal = 0;

    /// <summary><c>Luck::GOOD</c>.</summary>
    public const int Good = 1;

    /// <summary><c>Luck::GREAT</c>.</summary>
    public const int Great = 2;

    /// <summary><c>Luck::IRISH</c>: double damage attacks.</summary>
    public const int Irish = 3;

    /// <summary>
    /// Clamps a luck sum into the scale, <c>Luck::Normalize()</c> (<c>luck.cpp:81</c>).
    /// </summary>
    /// <param name="luck">The raw sum of the modifiers, which can be outside the scale.</param>
    /// <returns>The value clamped into <see cref="Cursed"/>…<see cref="Irish"/>.</returns>
    public static int Normalize(int luck) => Math.Clamp(luck, Cursed, Irish);

    /// <summary>
    /// The name of a luck value, <c>Luck::String()</c> (<c>luck.cpp:33-58</c>).
    /// </summary>
    /// <remarks>
    /// The reference's English with the <c>luck|</c> gettext context prefix dropped, and <c>"Unknown"</c> for
    /// <see cref="Unknown"/> or anything else off the scale.
    /// </remarks>
    /// <param name="luck">The luck value.</param>
    /// <returns>The word the info screen prints.</returns>
    public static string GetName(int luck) => luck switch
    {
        Cursed => "Cursed",
        Awful => "Awful",
        Bad => "Bad",
        Normal => "Normal",
        Good => "Good",
        Great => "Great",
        Irish => "Irish",
        _ => "Unknown",
    };

    /// <summary>
    /// The paragraph the info screen prints for a luck value, <c>Luck::Description()</c>
    /// (<c>luck.cpp:60-84</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考用 <c>%{luck-type}</c> 占位，再把 <see cref="GetName"/> 填回去；端口没有翻译层，所以填的是英文名。
    /// </para>
    /// <para>
    /// 三段文案与三个区间一一对应：负数「有一半概率打出半伤」，零「既不会幸运也不会不幸」，正数「有一半概率
    /// 打出双倍伤害」。<see cref="Unknown"/> 与任何表外值都只有 <c>"Unknown"</c> 一个词。
    /// </para>
    /// </remarks>
    /// <param name="luck">The luck value.</param>
    /// <returns>The sentence for the info screen.</returns>
    public static string Description(int luck)
    {
        string message = luck switch
        {
            Cursed or Awful or Bad =>
                "%{luck-type} luck sometimes falls on the hero's units in combat, causing their attacks to only do half damage.",
            Normal => "%{luck-type} luck means the hero's units will never get lucky or unlucky attacks on the enemy.",
            Good or Great or Irish =>
                "%{luck-type} luck sometimes lets the hero's units get lucky attacks (double strength) in combat.",
            _ => "Unknown",
        };

        return message.Replace("%{luck-type}", GetName(luck));
    }
}
