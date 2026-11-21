using FHeroes2.Game.Spells;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Picks one spell out of a scrolling list, <c>Dialog::selectSpell()</c> (<c>dialog_selectitems.cpp:1116-1160</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这里是**先立接口**：调用方已经存在（法术书的「学法术」、宝物窗口给「法术卷轴」挑法术、编辑器），
/// 但它自己的目录（<c>dialog</c>，planv4 的 S13）还没轮到。按 v4 的约定，被依赖的接口先写签名与文档，
/// 方法体抛 <see cref="NotImplementedException"/>，等 S13 一次做实。
/// </para>
/// <para>
/// 参数逐项对应参考：<paramref name="spellId"/> 是打开时落在哪一项（<c>Spell::RANDOM</c> 表示「随机」也进列表），
/// <paramref name="includeRandomSpells"/> 决定列表里放不放「随机」那一项，
/// <paramref name="excludeSpellsList"/> 是已经点过、不再露出的法术，
/// <paramref name="spellsLevel"/> 为负表示不限等级。
/// </para>
/// </remarks>
public static class SpellSelectDialog
{
    /// <summary>
    /// Asks the player for a spell, <c>Dialog::selectSpell()</c> (<c>dialog_selectitems.cpp:1116-1160</c>).
    /// </summary>
    /// <param name="spellId">The spell the list starts on, or the random-spell id.</param>
    /// <param name="includeRandomSpells">Whether the random entry is part of the list.</param>
    /// <param name="excludeSpellsList">Spells the player has already used up.</param>
    /// <param name="spellsLevel">Only this level, or a negative number for all levels.</param>
    /// <returns>The chosen spell, or <see cref="SpellType.None"/> when the player cancelled.</returns>
    /// <exception cref="NotImplementedException">The list window is not ported yet (S13).</exception>
    public static SpellType SelectSpell(int spellId, bool includeRandomSpells, ISet<int>? excludeSpellsList = null,
        int spellsLevel = -1)
    {
        throw new NotImplementedException("Dialog::selectSpell() 未移植（planv4 S13）。");
    }
}
