namespace FHeroes2.Game.Monsters;

/// <summary>
/// The four values the third metadata word of an <c>MP2::OBJ_MONSTER</c> tile may hold,
/// <c>Monster::JOIN_CONDITION_*</c> (<c>monster.h:38-41</c>).
/// </summary>
/// <remarks>
/// The reference stores <see cref="Unset"/> as <c>-1</c> in a <c>uint32_t</c> metadata word, so a tile
/// that was never touched reads back as <c>4294967295</c>. The port keeps the same signed constants and
/// compares the raw word through <c>unchecked( (int)value )</c>; see <c>Tile.IsMonsterJoinConditionSkip</c>.
/// </remarks>
public static class MonsterJoinConditions
{
    /// <summary><c>JOIN_CONDITION_UNSET</c> — no condition was ever written.</summary>
    public const int Unset = -1;

    /// <summary><c>JOIN_CONDITION_SKIP</c> — the player cannot hire the monsters, the tile cannot be entered.</summary>
    public const int Skip = 0;

    /// <summary><c>JOIN_CONDITION_MONEY</c> — the monsters ask for gold.</summary>
    public const int Money = 1;

    /// <summary><c>JOIN_CONDITION_FREE</c> — the monsters join for free.</summary>
    public const int Free = 2;
}
