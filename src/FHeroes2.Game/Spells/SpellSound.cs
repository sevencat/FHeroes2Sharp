using FHeroes2.Assets.Audio;

namespace FHeroes2.Game.Spells;

/// <summary>
/// The battle sound of a spell, the reference's <c>M82::FromSpell</c> (<c>src/fheroes2/agg/m82.cpp:105</c>).
/// </summary>
/// <remarks>
/// 参考把这张表放在 <c>agg</c> 目录里，但它一边是 <c>M82::SoundType</c>（资源层）、一边是 <c>Spell::</c>
/// （游戏层），所以端口按依赖方向拆开：音效 id 在 <c>FHeroes2.Assets.Audio</c>，这张对照表在游戏层。
/// 几处不是一对一的照搬：
/// <list type="bullet">
/// <item><c>FIREBALL</c> 与 <c>FIREBLAST</c> 共用 <c>FIREBALL</c>；</item>
/// <item><c>DISPEL</c> 与 <c>MASSDISPEL</c> 共用 <c>DIPMAGK</c>；</item>
/// <item><c>DEATHRIPPLE</c> 与 <c>DEATHWAVE</c> 共用 <c>MNRDEATH</c>；</item>
/// <item><c>ANIMATEDEAD</c> 借的是复活术的 <c>RESURECT</c>；</item>
/// <item><c>PETRIFY</c> 借的是麻痹的 <c>PARALIZE</c>，因为原版没有为石化单独录一条。</item>
/// </list>
/// 没有音效的法术（治疗、召唤船、视野类，以及表里没列的）返回 <see cref="M82Sound.Unknown"/>。
/// </remarks>
public static class SpellSound
{
    /// <summary>
    /// The sound that is played when <paramref name="spell"/> is cast, the reference's <c>M82::FromSpell</c>.
    /// </summary>
    /// <param name="spell">The spell being cast.</param>
    /// <returns>The sound, or <see cref="M82Sound.Unknown"/> when the spell has none.</returns>
    /// <remarks>参考：<c>m82.cpp:105-203</c>，全部四十六条 <c>case</c> 照搬。</remarks>
    public static M82Sound FromSpell(SpellType spell) => spell switch
    {
        SpellType.Fireball or SpellType.FireBlast => M82Sound.Fireball,
        SpellType.LightningBolt => M82Sound.Lightblt,
        SpellType.ChainLightning => M82Sound.Chainlte,
        SpellType.Teleport => M82Sound.Telein,
        SpellType.Cure => M82Sound.Cure,
        SpellType.MassCure => M82Sound.Masscure,
        SpellType.Resurrect => M82Sound.Resurect,
        SpellType.ResurrectTrue => M82Sound.Resurtru,
        SpellType.Haste => M82Sound.Haste,
        SpellType.MassHaste => M82Sound.Masshast,
        SpellType.Slow => M82Sound.Slow,
        SpellType.MassSlow => M82Sound.Massslow,
        SpellType.Blind => M82Sound.Blind,
        SpellType.Bless => M82Sound.Bless,
        SpellType.MassBless => M82Sound.Massbles,
        SpellType.StoneSkin => M82Sound.Stonskin,
        SpellType.SteelSkin => M82Sound.Stelskin,
        SpellType.Curse => M82Sound.Curse,
        SpellType.MassCurse => M82Sound.Masscurs,
        SpellType.AntiMagic => M82Sound.Antimagk,
        SpellType.Dispel or SpellType.MassDispel => M82Sound.Dipmagk,
        SpellType.Arrow => M82Sound.Magcarow,
        SpellType.Berserker => M82Sound.Berzerk,
        SpellType.Armageddon => M82Sound.Armgedn,
        SpellType.ElementalStorm => M82Sound.Storm,
        SpellType.MeteorShower => M82Sound.Meteor,
        SpellType.Paralyze => M82Sound.Paralize,
        SpellType.Hypnotize => M82Sound.Hypnotiz,
        SpellType.ColdRay => M82Sound.Coldray,
        SpellType.ColdRing => M82Sound.Coldring,
        SpellType.DisruptingRay => M82Sound.Disruptr,
        SpellType.DeathRipple or SpellType.DeathWave => M82Sound.Mnrdeath,
        SpellType.DragonSlayer => M82Sound.Drgnslay,
        SpellType.BloodLust => M82Sound.Bloodlus,
        SpellType.AnimateDead => M82Sound.Resurect,
        SpellType.MirrorImage => M82Sound.Mirrorim,
        SpellType.Shield => M82Sound.Shield,
        SpellType.MassShield => M82Sound.Massshie,
        SpellType.SummonEElement or SpellType.SummonAElement or SpellType.SummonFElement or SpellType.SummonWElement => M82Sound.Sumnelm,
        SpellType.Earthquake => M82Sound.Erthquak,
        SpellType.Haunt => M82Sound.H2mine,
        SpellType.Petrify => M82Sound.Paralize,
        _ => M82Sound.Unknown,
    };
}
