using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Battle;

/// <summary>
/// The cursor and the status hint of one aim, <c>Battle::Interface::GetBattleSpellCursor()</c> returning the pair.
/// </summary>
/// <param name="Theme">Which cursor image the aim shows.</param>
/// <param name="Status">The lower status line while the spell is being aimed.</param>
public readonly record struct BattleSpellHover(CursorTheme Theme, string Status);

/// <summary>
/// The cursor an aimed spell shows and the hint under it, <c>Battle::Interface::GetBattleSpellCursor()</c>
/// (<c>battle_interface.cpp:3066-3117</c>) and <c>getCursorForSpell()</c> (<c>:550-635</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么单独一层</b>：参考把这段写在界面里，但判定只用到竞技场、当前单位和鼠标下的格子，不碰图片，
/// 于是搬出来做成纯函数，好让 <c>tests/FHeroes2.UI.Tests</c> 直接对着一个竞技场问答案。
/// </para>
/// <para>
/// <b>与参考的三处差异</b>：文案不经过翻译层（<c>_( "…" )</c>），与端口其余界面一致；群体法术在参考里也没有
/// 对应光标图（<c>getCursorForSpell</c> 只列了单体法术），所以仍然返回 <see cref="CursorTheme.CombatNone"/>；
/// 「施法失败」类的提示框在端口里改成状态行文字，见 <see cref="Describe"/>。
/// </para>
/// </remarks>
public static class BattleSpellCursor
{
    /// <summary>The hint when nothing under the pointer can take the spell, <c>_( "Select spell target" )</c>.</summary>
    public const string SelectTargetStatus = "Select spell target";

    /// <summary>The hint of a teleport whose destination is free, <c>_( "Teleport here" )</c>.</summary>
    public const string TeleportHereStatus = "Teleport here";

    /// <summary>The hint of a teleport onto an occupied or blocked cell, <c>_( "Invalid teleport destination" )</c>.</summary>
    public const string InvalidTeleportStatus = "Invalid teleport destination";

    /// <summary>
    /// The cursor image of a spell, <c>getCursorForSpell()</c> (<c>battle_interface.cpp:550-635</c>).
    /// </summary>
    /// <param name="spell">The spell.</param>
    /// <returns>
    /// The <c>SPELCO</c> frame of the spell, or <see cref="CursorTheme.CombatNone"/> for the mass spells and the
    /// summons, which the reference's table does not list either.
    /// </returns>
    public static CursorTheme ThemeFor(SpellType spell) => spell switch
    {
        SpellType.Slow => CursorTheme.SpellSlow,
        SpellType.Curse => CursorTheme.SpellCurse,
        SpellType.Cure => CursorTheme.SpellCure,
        SpellType.Bless => CursorTheme.SpellBless,
        SpellType.Fireball => CursorTheme.SpellFireball,
        SpellType.FireBlast => CursorTheme.SpellFireblast,
        SpellType.Teleport => CursorTheme.SpellTeleport,
        SpellType.Resurrect => CursorTheme.SpellResurrect,
        SpellType.Haste => CursorTheme.SpellHaste,
        SpellType.Shield => CursorTheme.SpellShield,
        SpellType.Armageddon => CursorTheme.SpellArmageddon,
        SpellType.AntiMagic => CursorTheme.SpellAntiMagic,
        SpellType.Berserker => CursorTheme.SpellBerserker,
        SpellType.Paralyze => CursorTheme.SpellParalyze,
        SpellType.Blind => CursorTheme.SpellBlind,
        SpellType.LightningBolt => CursorTheme.SpellLightningBolt,
        SpellType.ChainLightning => CursorTheme.SpellChainLightning,
        SpellType.ElementalStorm => CursorTheme.SpellElementalStorm,
        SpellType.ResurrectTrue => CursorTheme.SpellResurrectTrue,
        SpellType.Dispel => CursorTheme.SpellDispel,
        SpellType.HolyWord => CursorTheme.SpellHolyWord,
        SpellType.HolyShout => CursorTheme.SpellHolyShout,
        SpellType.MeteorShower => CursorTheme.SpellMeteorShower,
        SpellType.AnimateDead => CursorTheme.SpellAnimateDead,
        SpellType.MirrorImage => CursorTheme.SpellMirrorImage,
        SpellType.BloodLust => CursorTheme.SpellBloodLust,
        SpellType.DeathRipple => CursorTheme.SpellDeathRipple,
        SpellType.DeathWave => CursorTheme.SpellDeathWave,
        SpellType.SteelSkin => CursorTheme.SpellSteelSkin,
        SpellType.StoneSkin => CursorTheme.SpellStoneSkin,
        SpellType.DragonSlayer => CursorTheme.SpellDragonSlayer,
        SpellType.Earthquake => CursorTheme.SpellEarthquake,
        SpellType.DisruptingRay => CursorTheme.SpellDisruptingRay,
        SpellType.ColdRing => CursorTheme.SpellColdRing,
        SpellType.ColdRay => CursorTheme.SpellColdRay,
        SpellType.Hypnotize => CursorTheme.SpellHypnotize,
        SpellType.Arrow => CursorTheme.SpellArrow,
        _ => CursorTheme.CombatNone,
    };

    /// <summary>
    /// The cursor and the hint of an aim, <c>Interface::GetBattleSpellCursor()</c>
    /// (<c>battle_interface.cpp:3066-3117</c>).
    /// </summary>
    /// <param name="arena">The battle, asked about corpses the spell could raise.</param>
    /// <param name="currentUnit">The unit whose turn it is; the reference calls it <c>_currentUnit</c>.</param>
    /// <param name="spell">The spell being aimed.</param>
    /// <param name="hoveredCell">The cell under the pointer.</param>
    /// <param name="teleportSourceCell">
    /// The cell picked as the source of a teleport, <c>-1</c> while no source has been picked.
    /// </param>
    /// <returns>The cursor to show and the lower status line to write.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arena"/> or <paramref name="currentUnit"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 顺序照参考：先看传送选过起点没有（这时不看格子上的兵），再看格子上的兵能不能吃这个法术，最后看
    /// 法术是不是不需要目标单位。墓地里能被复活的兵算作「格子上的兵」，和参考一样。
    /// </remarks>
    public static BattleSpellHover Describe(
        BattleArena arena,
        BattleUnit currentUnit,
        Spell spell,
        int hoveredCell,
        int teleportSourceCell)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        if (spell.Type == SpellType.None || arena.Board.GetCell(hoveredCell) is not { } cell)
        {
            return new BattleSpellHover(CursorTheme.CombatNone, SelectTargetStatus);
        }

        BattleUnit? unitOnCell = cell.Unit;

        // 指针下没有兵，但墓地里压着一具这个法术能救活的尸体时，按参考把它当成目标。
        if (unitOnCell is null && arena.IsAbleToResurrectFromGraveyard(hoveredCell, spell))
        {
            unitOnCell = arena.GetLastResurrectableUnitFromGraveyard(hoveredCell, spell);
        }

        if (BattleBoard.IsValidIndex(teleportSourceCell))
        {
            BattleUnit? unitToTeleport = arena.GetTroopBoard(teleportSourceCell);

            if (unitOnCell is null && unitToTeleport is not null && cell.IsPassableForUnit(unitToTeleport))
            {
                return new BattleSpellHover(CursorTheme.SpellTeleport, TeleportHereStatus);
            }

            return new BattleSpellHover(CursorTheme.CombatNone, InvalidTeleportStatus);
        }

        if (unitOnCell is not null && unitOnCell.AllowApplySpell(spell, currentUnit.GetCurrentOrArmyCommander()))
        {
            return new BattleSpellHover(ThemeFor(spell.Type), CastOn(spell, unitOnCell));
        }

        if (!spell.IsApplyToFriends() && !spell.IsApplyToEnemies() && !spell.IsApplyToAnyTroops())
        {
            return new BattleSpellHover(ThemeFor(spell.Type), $"Cast {spell.GetName()}");
        }

        return new BattleSpellHover(CursorTheme.CombatNone, SelectTargetStatus);
    }

    /// <summary>The hint of a spell aimed at a stack, <c>_( "Cast %{spell} on %{monster}" )</c>.</summary>
    /// <param name="spell">The spell.</param>
    /// <param name="unit">The stack under the pointer.</param>
    /// <returns>The sentence with both names spelled in; the creature stays lower case like the reference.</returns>
    public static string CastOn(Spell spell, BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return Army.StringReplaceWithLowercase(
            $"Cast {spell.GetName()} on %{{monster}}",
            "%{monster}",
            MonsterNames.GetSingular(unit.Monster));
    }
}
