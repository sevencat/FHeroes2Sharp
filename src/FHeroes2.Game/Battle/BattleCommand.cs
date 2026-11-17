using FHeroes2.Core.Random;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Battle;

/// <summary>
/// What a battle command asks for, <c>Battle::CommandType</c> (<c>battle_command.h:38-51</c>). The numbers are the
/// order the reference declares them in.
/// </summary>
public enum BattleCommandType
{
    /// <summary>Move a unit: unit UID, destination cell.</summary>
    Move,

    /// <summary>Attack: attacker UID, defender UID, cell to move to, cell to attack, direction.</summary>
    Attack,

    /// <summary>Cast a spell: see <see cref="BattleCommand.SpellCast(SpellType, int)"/>.</summary>
    Spellcast,

    /// <summary>Morale answer: unit UID, morale.</summary>
    Morale,

    /// <summary>Catapult shot; takes no parameter.</summary>
    Catapult,

    /// <summary>Tower shot: tower type, unit UID.</summary>
    Tower,

    /// <summary>Retreat; takes no parameter.</summary>
    Retreat,

    /// <summary>Surrender; takes no parameter.</summary>
    Surrender,

    /// <summary>Skip the turn: unit UID.</summary>
    Skip,

    /// <summary>Toggle auto combat: colour.</summary>
    ToggleAutoCombat,

    /// <summary>Finish the battle in quick combat; takes no parameter.</summary>
    QuickCombat,
}

/// <summary>
/// One battle command, <c>Battle::Command</c> (<c>battle_command.h:53-144</c>,
/// <c>battle_command.cpp:30-93</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考直接继承 <c>std::vector&lt;int&gt;</c>，构造时把参数包按「从右到左」的赋值顺序压栈，于是**参数是倒着存的**：
/// <c>ATTACK(a, d, move, target, dir)</c> 存成 <c>[dir, target, move, d, a]</c>。这不是笔误，参考的
/// <c>updatePCG32Stream()</c> 就按 <c>at(2)/at(3)/at(4)</c> 取「移动格、防守方、攻击方」。
/// <see cref="GetNextValue"/> 又从尾部弹出，因此它会按调用方写参数的顺序逐个返回。
/// </para>
/// <para>
/// 参考用 <c>std::integral_constant</c> 标签加 <c>static_assert</c> 卡参数个数，C# 没有等价的编译期约束，
/// 于是每种命令给一个静态工厂方法，参数个数与类型在签名上就定死了。
/// </para>
/// </remarks>
public sealed class BattleCommand
{
    private readonly List<int> _parameters;

    /// <summary>Builds a command out of its parameters, which are stored in reverse.</summary>
    /// <param name="type">What the command asks for.</param>
    /// <param name="parameters">The parameters in the order the reference's caller writes them.</param>
    private BattleCommand(BattleCommandType type, params int[] parameters)
    {
        Type = type;
        _parameters = new List<int>(parameters.Length);

        for (int index = parameters.Length - 1; index >= 0; --index)
        {
            _parameters.Add(parameters[index]);
        }
    }

    /// <summary>What the command asks for, <c>Command::GetType()</c>.</summary>
    public BattleCommandType Type { get; }

    /// <summary>The parameters as stored, i.e. in reverse of the order they were given in.</summary>
    public IReadOnlyList<int> Parameters => _parameters;

    /// <summary>How many parameters are left, the reference's <c>size()</c>.</summary>
    public int Count => _parameters.Count;

    /// <summary>One stored parameter, the reference's <c>at()</c> / <c>operator[]</c>.</summary>
    /// <param name="index">Position in the reversed parameter list.</param>
    public int this[int index] => _parameters[index];

    /// <summary>Moves a unit: unit UID, destination cell.</summary>
    /// <param name="unitUid">The unit to move.</param>
    /// <param name="cellIndex">The cell it should end up on.</param>
    public static BattleCommand Move(int unitUid, int cellIndex) => new(BattleCommandType.Move, unitUid, cellIndex);

    /// <summary>Attacks a unit.</summary>
    /// <param name="attackerUid">The attacking unit.</param>
    /// <param name="defenderUid">The unit being attacked.</param>
    /// <param name="cellToMove">The cell the attacker walks to first.</param>
    /// <param name="cellToAttack">The cell of the defender it strikes.</param>
    /// <param name="direction">The side the blow comes from.</param>
    public static BattleCommand Attack(int attackerUid, int defenderUid, int cellToMove, int cellToAttack, AttackDirection direction) =>
        new(BattleCommandType.Attack, attackerUid, defenderUid, cellToMove, cellToAttack, (int)direction);

    /// <summary>Casts a spell that names one target.</summary>
    /// <param name="spell">The spell; a mirror image names a unit UID, anything else a cell.</param>
    /// <param name="target">Unit UID for a mirror image, cell index otherwise.</param>
    public static BattleCommand SpellCast(SpellType spell, int target) => new(BattleCommandType.Spellcast, (int)spell, target);

    /// <summary>Casts a teleport, which names both the source and the destination cell.</summary>
    /// <param name="spell">Must be <see cref="SpellType.Teleport"/>.</param>
    /// <param name="sourceIndex">The cell the unit leaves.</param>
    /// <param name="destinationIndex">The cell the unit lands on.</param>
    /// <remarks>参考在 <c>SPELLCAST</c> 里只对 <c>TELEPORT</c> 允许三个参数。</remarks>
    /// <exception cref="ArgumentException"><paramref name="spell"/> is not a teleport.</exception>
    public static BattleCommand SpellCast(SpellType spell, int sourceIndex, int destinationIndex)
    {
        if (spell != SpellType.Teleport)
        {
            throw new ArgumentException("Only teleport takes a source and a destination cell.", nameof(spell));
        }

        return new BattleCommand(BattleCommandType.Spellcast, (int)spell, sourceIndex, destinationIndex);
    }

    /// <summary>Answers the morale question of a unit.</summary>
    /// <param name="unitUid">The unit.</param>
    /// <param name="morale">The morale it acts with.</param>
    public static BattleCommand Morale(int unitUid, int morale) => new(BattleCommandType.Morale, unitUid, morale);

    /// <summary>Fires the catapult, which takes no parameter.</summary>
    public static BattleCommand Catapult() => new(BattleCommandType.Catapult);

    /// <summary>Fires a tower.</summary>
    /// <param name="towerType">Which tower shoots.</param>
    /// <param name="unitUid">The unit it aims at.</param>
    public static BattleCommand Tower(int towerType, int unitUid) => new(BattleCommandType.Tower, towerType, unitUid);

    /// <summary>Retreats, which takes no parameter.</summary>
    public static BattleCommand Retreat() => new(BattleCommandType.Retreat);

    /// <summary>Surrenders, which takes no parameter.</summary>
    public static BattleCommand Surrender() => new(BattleCommandType.Surrender);

    /// <summary>Skips the turn of a unit.</summary>
    /// <param name="unitUid">The unit.</param>
    public static BattleCommand Skip(int unitUid) => new(BattleCommandType.Skip, unitUid);

    /// <summary>Toggles auto combat for a colour.</summary>
    /// <param name="color">The colour whose auto combat flips.</param>
    public static BattleCommand ToggleAutoCombat(int color) => new(BattleCommandType.ToggleAutoCombat, color);

    /// <summary>Finishes the battle in quick combat, which takes no parameter.</summary>
    public static BattleCommand QuickCombat() => new(BattleCommandType.QuickCombat);

    /// <summary>Pops the next parameter in the order the caller wrote them, <c>Command::GetNextValue()</c>.</summary>
    /// <returns>The parameter, or <c>0</c> when none is left.</returns>
    /// <remarks>参考：<c>battle_command.cpp:30-37</c>——从尾部弹出，空命令答 <c>0</c> 而不报错。</remarks>
    public int GetNextValue()
    {
        if (_parameters.Count == 0)
        {
            return 0;
        }

        int value = _parameters[^1];
        _parameters.RemoveAt(_parameters.Count - 1);

        return value;
    }

    /// <summary>
    /// Mixes the command into a PCG32 stream, <c>Command::updatePCG32Stream()</c>.
    /// </summary>
    /// <param name="stream">The stream to mix into.</param>
    /// <returns>The mixed stream, or <paramref name="stream"/> unchanged for the commands that must not affect it.</returns>
    /// <remarks>
    /// 参考：<c>battle_command.cpp:39-76</c>。攻击只混入「移动格、防守方、攻击方」，因为「攻击格」与「方向」会随
    /// 人类玩家或 AI 而不同，混进去会让同一场战斗在两种控制方式下掷出不同的随机序列。
    /// </remarks>
    public ulong UpdatePcg32Stream(ulong stream)
    {
        switch (Type)
        {
            case BattleCommandType.Attack:
                stream = Rand.CombineSeedWithValueHash(stream, (int)Type);
                stream = Rand.CombineSeedWithValueHash(stream, _parameters[2]);
                stream = Rand.CombineSeedWithValueHash(stream, _parameters[3]);
                stream = Rand.CombineSeedWithValueHash(stream, _parameters[4]);
                break;

            case BattleCommandType.Move:
            case BattleCommandType.Spellcast:
            case BattleCommandType.Morale:
            case BattleCommandType.Catapult:
            case BattleCommandType.Tower:
            case BattleCommandType.Retreat:
            case BattleCommandType.Surrender:
            case BattleCommandType.Skip:
                stream = Rand.CombineSeedWithValueHash(stream, (int)Type);

                // 参考按 vector 里存着的顺序混，也就是参数的反序。
                foreach (int parameter in _parameters)
                {
                    stream = Rand.CombineSeedWithValueHash(stream, parameter);
                }

                break;

            case BattleCommandType.ToggleAutoCombat:
            case BattleCommandType.QuickCombat:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(Type), Type, "Unknown battle command type.");
        }

        return stream;
    }
}
