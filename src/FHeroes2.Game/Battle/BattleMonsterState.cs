namespace FHeroes2.Game.Battle;

/// <summary>
/// The flag word <c>Battle::Unit::modes</c> is made of, <c>Battle::MonsterState</c>
/// (<c>battle.h:89-133</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把它写成 <c>enum MonsterState : uint32_t</c>，但每一位都是**掩码**：一个单位可以同时被祝福、加速、
/// 护盾，所以取值都是单比特，三个 <c>IS_*_MAGIC</c> 是它们的或。C# 的 <c>[Flags] enum</c> 会把
/// <c>IS_GOOD_MAGIC</c> 这样的大组合当成一个成员，位运算读起来反而更绕，因此这里保留参考的裸
/// <c>uint</c> 常量写法，和 <c>BitModes</c> 的 <c>uint</c> 直接对接。
/// </para>
/// <para>
/// <c>TR_</c> 前缀是回合内的三种「已做过」标记，<c>CAP_</c> 是镜像与塔这类「身份」，<c>SP_</c> 是法术效果。
/// 中间那个 <c>UNUSED = 0x00010000</c> 参考留了个洞，这里也留着，好让十六进制对得上。
/// </para>
/// </remarks>
public static class BattleMonsterState
{
    /// <summary>Has already retaliated this turn, <c>TR_RETALIATED</c>.</summary>
    public const uint Retaliated = 0x00000001;

    /// <summary>Has already acted this turn, <c>TR_MOVED</c>.</summary>
    public const uint Moved = 0x00000002;

    /// <summary>Skipped its turn, <c>TR_SKIP</c>.</summary>
    public const uint Skip = 0x00000004;

    /// <summary>Can teleport instead of walking, <c>TELEPORT_ABILITY</c>.</summary>
    public const uint TeleportAbility = 0x00000008;

    /// <summary>Good luck on the next blow, <c>LUCK_GOOD</c>.</summary>
    public const uint LuckGood = 0x00000100;

    /// <summary>Bad luck on the next blow, <c>LUCK_BAD</c>.</summary>
    public const uint LuckBad = 0x00000200;

    /// <summary>Extra turn, <c>MORALE_GOOD</c>.</summary>
    public const uint MoraleGood = 0x00000400;

    /// <summary>Freezes for a turn, <c>MORALE_BAD</c>.</summary>
    public const uint MoraleBad = 0x00000800;

    /// <summary>One of the castle towers, <c>CAP_TOWER</c>.</summary>
    public const uint CapTower = 0x00001000;

    /// <summary>A summoned elemental, <c>CAP_SUMMONELEM</c>.</summary>
    public const uint CapSummonElement = 0x00002000;

    /// <summary>The unit the mirror image belongs to, <c>CAP_MIRROROWNER</c>.</summary>
    public const uint CapMirrorOwner = 0x00004000;

    /// <summary>A mirror image, <c>CAP_MIRRORIMAGE</c>.</summary>
    public const uint CapMirrorImage = 0x00008000;

    /// <summary>Bloodlust, <c>SP_BLOODLUST</c>.</summary>
    public const uint SpellBloodlust = 0x00020000;

    /// <summary>Bless, <c>SP_BLESS</c>.</summary>
    public const uint SpellBless = 0x00040000;

    /// <summary>Haste, <c>SP_HASTE</c>.</summary>
    public const uint SpellHaste = 0x00080000;

    /// <summary>Shield, <c>SP_SHIELD</c>.</summary>
    public const uint SpellShield = 0x00100000;

    /// <summary>Stone skin, <c>SP_STONESKIN</c>.</summary>
    public const uint SpellStoneSkin = 0x00200000;

    /// <summary>Dragon slayer, <c>SP_DRAGONSLAYER</c>.</summary>
    public const uint SpellDragonSlayer = 0x00400000;

    /// <summary>Steel skin, <c>SP_STEELSKIN</c>.</summary>
    public const uint SpellSteelSkin = 0x00800000;

    /// <summary>Anti-magic, <c>SP_ANTIMAGIC</c>.</summary>
    public const uint SpellAntiMagic = 0x01000000;

    /// <summary>Curse, <c>SP_CURSE</c>.</summary>
    public const uint SpellCurse = 0x02000000;

    /// <summary>Slow, <c>SP_SLOW</c>.</summary>
    public const uint SpellSlow = 0x04000000;

    /// <summary>Berserker, <c>SP_BERSERKER</c>.</summary>
    public const uint SpellBerserker = 0x08000000;

    /// <summary>Hypnotize, <c>SP_HYPNOTIZE</c>.</summary>
    public const uint SpellHypnotize = 0x10000000;

    /// <summary>Blind, <c>SP_BLIND</c>.</summary>
    public const uint SpellBlind = 0x20000000;

    /// <summary>Paralyze, <c>SP_PARALYZE</c>.</summary>
    public const uint SpellParalyze = 0x40000000;

    /// <summary>Petrify, <c>SP_STONE</c>.</summary>
    public const uint SpellStone = 0x80000000;

    /// <summary>Every beneficial spell, <c>IS_GOOD_MAGIC</c>.</summary>
    public const uint GoodMagic = SpellBloodlust | SpellBless | SpellHaste | SpellShield | SpellStoneSkin | SpellDragonSlayer | SpellSteelSkin;

    /// <summary>Every harmful spell, <c>IS_BAD_MAGIC</c>.</summary>
    public const uint BadMagic = SpellCurse | SpellSlow | SpellBerserker | SpellHypnotize | SpellBlind | SpellParalyze | SpellStone;

    /// <summary>Everything a dispel would take away, <c>IS_MAGIC</c>.</summary>
    public const uint Magic = GoodMagic | BadMagic | SpellAntiMagic;

    /// <summary>The two spells that freeze a unit, <c>IS_PARALYZE_MAGIC</c>.</summary>
    public const uint ParalyzeMagic = SpellParalyze | SpellStone;

    /// <summary>The spells that a mind spell immunity covers, <c>IS_MIND_MAGIC</c>.</summary>
    public const uint MindMagic = SpellBerserker | SpellHypnotize | SpellBlind | SpellParalyze;
}
