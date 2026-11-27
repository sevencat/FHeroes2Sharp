using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The spell rows of <c>mageGuildSpellsDialog()</c> (<c>editor_castle_details_window.cpp:318-606</c>): what the sub-window
/// shows for the mage guild of a castle and how its edits go back into the map's metadata.
/// </summary>
/// <remarks>
/// <para>
/// 五排，每排一格一法术，格子数由 <see cref="MageGuild.GetMaxSpellsCount"/> 按 <b>种族有没有图书馆</b> 算出——参考这里用的
/// 是「种族能不能建图书馆」（法师族或随机族），不是「这张地图上图书馆建了没有」（`:333`）。空位放本等级的
/// 随机占位符（<see cref="SpellType.Random"/> 加等级），它不是一个真法术，<see cref="Spell.Level"/> 对它返回 0。
/// </para>
/// <para>
/// 元数据里存的是「必须出现」的表：键是 <c>等级 * 10 + 排内位置</c>（等级从 1 起），值是法术编号。参考在点 OK 时按格子
/// 逐个对齐这张表——格子里是真法术就写进表，是占位符就把表里那条删掉（`:450-479`）。
/// </para>
/// <para>
/// 禁用表是一串法术编号，不按等级分组存；参考在每次打开「禁用」子窗时按等级摊到五个容器里，确认后拼回一串（`:487-529`）。
/// 被禁的法术若正好排在某排里，参考把它换成该等级的占位符（`:532-545`），这里同样做，只是换成整排扫一遍。
/// </para>
/// </remarks>
public sealed class MageGuildSpellTable
{
    /// <summary>Number of spell levels, <c>spellRows.size()</c>.</summary>
    public const int LevelCount = 5;

    private readonly SpellType[][] _rows;
    private readonly List<int> _bannedSpells = [];

    /// <summary>
    /// Builds the empty table of a race, with every slot holding the placeholder of its level.
    /// </summary>
    /// <param name="race">Race of the castle, which decides whether the library adds a slot.</param>
    public MageGuildSpellTable(MapRace race)
    {
        Race = race;
        HasLibraryCapability = race is MapRace.Wizard or MapRace.Random;

        _rows = new SpellType[LevelCount][];

        for (int levelIndex = 0; levelIndex < LevelCount; ++levelIndex)
        {
            int spellLevel = levelIndex + 1;
            SpellType[] row = new SpellType[GetSlotCount(spellLevel)];
            Array.Fill(row, GetPlaceholder(spellLevel));
            _rows[levelIndex] = row;
        }
    }

    /// <summary>Race of the castle.</summary>
    public MapRace Race { get; }

    /// <summary>
    /// Whether the race's library adds a spell to every level, <c>hasLibraryCapability</c> (<c>:333</c>).
    /// </summary>
    public bool HasLibraryCapability { get; }

    /// <summary>The banned spells, in the order the reference keeps them.</summary>
    public IReadOnlyList<int> BannedSpells => _bannedSpells;

    /// <summary>
    /// The number of slots of a spell level, <c>MageGuild::getMaxSpellsCount( level, hasLibraryCapability )</c>
    /// (<c>:342</c>).
    /// </summary>
    /// <param name="spellLevel">Spell level, 1 to 5.</param>
    /// <returns>The number of slots.</returns>
    public int GetSlotCount(int spellLevel) => MageGuild.GetMaxSpellsCount(spellLevel, HasLibraryCapability);

    /// <summary>
    /// The spells of one level as they are shown, <c>spellRows[levelIndex]</c>.
    /// </summary>
    /// <param name="levelIndex">Level index, 0 for the first level.</param>
    /// <returns>The slots of that level.</returns>
    public IReadOnlyList<SpellType> GetRow(int levelIndex) => _rows[levelIndex];

    /// <summary>
    /// The spell of one slot.
    /// </summary>
    /// <param name="levelIndex">Level index, 0 for the first level.</param>
    /// <param name="slot">Slot inside the level.</param>
    /// <returns>The spell, a placeholder when the author did not pin one.</returns>
    public SpellType GetSpell(int levelIndex, int slot) => _rows[levelIndex][slot];

    /// <summary>
    /// Pins a spell into a slot, which is what picking one in the row does (<c>:429-432</c>).
    /// </summary>
    /// <param name="levelIndex">Level index, 0 for the first level.</param>
    /// <param name="slot">Slot inside the level.</param>
    /// <param name="spell">The spell to show.</param>
    public void SetSpell(int levelIndex, int slot, SpellType spell) => _rows[levelIndex][slot] = spell;

    /// <summary>
    /// The placeholder of a level, <c>Spell::RANDOM + spellLevel</c> (<c>:353</c>).
    /// </summary>
    /// <param name="spellLevel">Spell level, 1 to 5.</param>
    /// <returns>The placeholder spell type.</returns>
    public static SpellType GetPlaceholder(int spellLevel) => SpellType.Random + spellLevel;

    /// <summary>
    /// Whether a spell type is one of the five placeholders rather than a real spell.
    /// </summary>
    /// <param name="spell">The spell type.</param>
    /// <returns><see langword="true"/> for <c>Spell::RANDOM + level</c>.</returns>
    public static bool IsPlaceholder(SpellType spell)
    {
        int id = (int)spell;

        return id > (int)SpellType.Random && id <= (int)SpellType.Random + LevelCount;
    }

    /// <summary>
    /// The spells the spell picker must not offer for a slot, <c>:413-425</c>: the other spells of the row,
    /// every placeholder, and the banned spells of that level. The slot's own spell stays selectable unless it
    /// is a placeholder.
    /// </summary>
    /// <param name="levelIndex">Level index, 0 for the first level.</param>
    /// <param name="slot">Slot inside the level.</param>
    /// <returns>The spell ids to exclude, in the order the reference inserts them.</returns>
    public IReadOnlyList<int> GetExcludedSpells(int levelIndex, int slot)
    {
        SpellType currentSpell = GetSpell(levelIndex, slot);
        int spellLevel = levelIndex + 1;

        List<int> excluded = [];

        foreach (SpellType spell in _rows[levelIndex])
        {
            // 当前那一格的法术留着可选，占位符则永远不给选（它不是真法术）。
            if ((spell != currentSpell || IsPlaceholder(spell)) && !excluded.Contains((int)spell))
            {
                excluded.Add((int)spell);
            }
        }

        foreach (int spellId in _bannedSpells)
        {
            if (new Spell(spellId).Level() == spellLevel && !excluded.Contains(spellId))
            {
                excluded.Add(spellId);
            }
        }

        return excluded;
    }

    /// <summary>
    /// Fills the rows from the metadata, <c>:340-361</c>.
    /// </summary>
    /// <param name="metadata">The castle.</param>
    public void Load(CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        for (int levelIndex = 0; levelIndex < LevelCount; ++levelIndex)
        {
            SpellType[] row = _rows[levelIndex];

            for (int slot = 0; slot < row.Length; ++slot)
            {
                byte key = (byte)(levelIndex * 10 + slot);
                row[slot] = metadata.MustHaveSpells.TryGetValue(key, out int spellId)
                    ? (SpellType)spellId
                    : GetPlaceholder(levelIndex + 1);
            }
        }

        _bannedSpells.Clear();
        _bannedSpells.AddRange(metadata.BannedSpells);
    }

    /// <summary>
    /// Writes the rows back into the metadata, <c>:449-483</c>.
    /// </summary>
    /// <remarks>
    /// 占位符（<see cref="Spell.Level"/> 为 0）不是真法术，写回时是「从表里删掉」而不是「存一个占位符」——否则地图文件里
    /// 会留下一条永远不该出现的「必须出现第 N 级随机法术」。
    /// </remarks>
    /// <param name="metadata">The castle.</param>
    /// <returns>Whether anything changed.</returns>
    public bool Save(CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        bool hasChanges = false;

        for (int levelIndex = 0; levelIndex < LevelCount; ++levelIndex)
        {
            SpellType[] row = _rows[levelIndex];
            int spellLevel = levelIndex + 1;

            for (int slot = 0; slot < row.Length; ++slot)
            {
                byte key = (byte)(levelIndex * 10 + slot);
                bool isInTable = metadata.MustHaveSpells.TryGetValue(key, out int storedSpellId);

                if (new Spell(row[slot]).Level() != spellLevel)
                {
                    if (isInTable)
                    {
                        metadata.MustHaveSpells.Remove(key);
                        hasChanges = true;
                    }

                    continue;
                }

                if (isInTable)
                {
                    if (storedSpellId != (int)row[slot])
                    {
                        metadata.MustHaveSpells[key] = (int)row[slot];
                        hasChanges = true;
                    }
                }
                else
                {
                    metadata.MustHaveSpells[key] = (int)row[slot];
                    hasChanges = true;
                }
            }
        }

        if (!_bannedSpells.SequenceEqual(metadata.BannedSpells))
        {
            metadata.BannedSpells.Clear();
            metadata.BannedSpells.AddRange(_bannedSpells);
            hasChanges = true;
        }

        return hasChanges;
    }

    /// <summary>
    /// Replaces the banned spells, and swaps any of them that sits in a row for the placeholder of its level,
    /// <c>:528-548</c>.
    /// </summary>
    /// <param name="bannedSpells">The new banned spells.</param>
    /// <returns>Whether the list differs from the current one; the reference reports that as a change even when no row moved.</returns>
    public bool SetBannedSpells(IReadOnlyList<int> bannedSpells)
    {
        ArgumentNullException.ThrowIfNull(bannedSpells);

        if (_bannedSpells.SequenceEqual(bannedSpells))
        {
            return false;
        }

        _bannedSpells.Clear();
        _bannedSpells.AddRange(bannedSpells);

        foreach (int spellId in _bannedSpells)
        {
            int spellLevel = new Spell((SpellType)spellId).Level();

            // 禁用表里的都该是真法术；万一不是，没有哪一排配得上它，跳过。
            if (spellLevel < 1 || spellLevel > LevelCount)
            {
                continue;
            }

            SpellType[] row = _rows[spellLevel - 1];
            SpellType placeholder = GetPlaceholder(spellLevel);

            for (int slot = 0; slot < row.Length; ++slot)
            {
                if (row[slot] == (SpellType)spellId)
                {
                    row[slot] = placeholder;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Spreads the banned spells over five containers by level, <c>bannedSpellsContainer</c> (<c>:487-494</c>).
    /// </summary>
    /// <param name="bannedSpells">The banned spells.</param>
    /// <returns>Five lists, index <c>0</c> being the first level.</returns>
    public static List<int>[] GroupByLevel(IReadOnlyList<int> bannedSpells)
    {
        ArgumentNullException.ThrowIfNull(bannedSpells);

        List<int>[] grouped = new List<int>[LevelCount];

        for (int i = 0; i < grouped.Length; ++i)
        {
            grouped[i] = [];
        }

        foreach (int spellId in bannedSpells)
        {
            int spellLevel = new Spell((SpellType)spellId).Level();

            if (spellLevel >= 1 && spellLevel <= LevelCount)
            {
                grouped[spellLevel - 1].Add(spellId);
            }
        }

        return grouped;
    }

    /// <summary>
    /// Puts the five containers back into one list, <c>:520-526</c>.
    /// </summary>
    /// <param name="groupedByLevel">Five lists, index <c>0</c> being the first level.</param>
    /// <returns>The banned spells, first level first.</returns>
    public static List<int> FlattenGrouped(IReadOnlyList<List<int>> groupedByLevel)
    {
        ArgumentNullException.ThrowIfNull(groupedByLevel);

        List<int> result = [];

        foreach (List<int> levelSpells in groupedByLevel)
        {
            result.AddRange(levelSpells);
        }

        return result;
    }
}
