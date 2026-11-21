namespace FHeroes2.Game.Heroes;

/// <summary>
/// The default names of the 73 heroes, <c>defaultHeroNames</c> of <c>heroes.cpp:84-121</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考把这 73 个名字放在一张 <c>gettext_noop</c> 表里，<c>Heroes::GetName()</c> 在英雄没有自定义名字时回落到
/// 它（<c>heroes.cpp:2100-2116</c>）。端口没有翻译层，所以这里直接放英文原文——顺序与
/// <see cref="Hero"/> 的编号一一对应：0 是未初始化，1~9 骑士，10~18 野蛮人，19~27 女巫，28~36 术士，
/// 37~45 法师，46~54 亡灵巫师，55~60 战役英雄，61~71 <c>The Price of Loyalty</c> 的新英雄，72 是开发用英雄。
/// </para>
/// <para>
/// 表是不可变的；名字只读，改名的入口是 <see cref="Hero.Name"/> 的自定义名字分支。
/// </para>
/// </remarks>
public static class HeroNames
{
    private static readonly string[] Names =
    [
        // Unknown / uninitialized hero.
        "Unknown",

        // Knight heroes from The Succession Wars.
        "Lord Kilburn", "Sir Gallant", "Ector", "Gwenneth", "Tyro", "Ambrose", "Ruby", "Maximus", "Dimitry",

        // Barbarian heroes from The Succession Wars.
        "Thundax", "Fineous", "Jojosh", "Crag Hack", "Jezebel", "Jaclyn", "Ergon", "Tsabu", "Atlas",

        // Sorceress heroes from The Succession Wars.
        "Astra", "Natasha", "Troyan", "Vatawna", "Rebecca", "Gem", "Ariel", "Carlawn", "Luna",

        // Warlock heroes from The Succession Wars.
        "Arie", "Alamar", "Vesper", "Crodo", "Barok", "Kastore", "Agar", "Falagar", "Wrathmont",

        // Wizard heroes from The Succession Wars.
        "Myra", "Flint", "Dawn", "Halon", "Myrini", "Wilfrey", "Sarakin", "Kalindra", "Mandigal",

        // Necromancer heroes from The Succession Wars.
        "Zom", "Darlana", "Zam", "Ranloo", "Charity", "Rialdo", "Roxana", "Sandro", "Celia",

        // The Succession Wars campaign heroes.
        "Roland", "Lord Corlagon", "Sister Eliza", "Archibald", "Lord Halton", "Brother Brax",

        // The Price of Loyalty expansion heroes.
        "Solmyr", "Dainwin", "Mog", "Uncle Ivan", "Joseph", "Gallavant", "Elderian", "Ceallach",
        "Drakonia", "Martine", "Jarkonas",

        // Debug hero. Should not be used anywhere outside the development!
        "Debug Hero",
    ];

    /// <summary>
    /// The default name of a hero, <c>_( defaultHeroNames[id] )</c>.
    /// </summary>
    /// <param name="id">Hero id, from 0 to <see cref="Hero.DebugHero"/>.</param>
    /// <returns>The English name; <c>Unknown</c> for id 0, the empty string for an id out of range.</returns>
    public static string Get(int id) => id >= 0 && id < Names.Length ? Names[id] : string.Empty;

    /// <summary>How many names the table holds, <see cref="Hero.IdCount"/>.</summary>
    public static int Count => Names.Length;
}
