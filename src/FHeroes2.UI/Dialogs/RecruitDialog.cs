using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The recruitment window of a dwelling, <c>Dialog::RecruitMonster()</c> (<c>dialog.h:125</c>,
/// <c>dialog_recruit.cpp:253-665</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference answers with a <c>Troop</c> and lets the default-constructed one mean "nothing was recruited". The
/// port returns <see langword="null"/> for that instead of an empty stack, so a caller cannot read the empty troop as
/// "a stack of zero units was chosen".
/// </para>
/// <para>
/// The reference is one modal loop that keeps the creature, the affordable maximum, the chosen count and the last typed
/// digits in locals. The port kept the inputs and the answer apart first (ADR-163, <see cref="Result"/> and
/// <see cref="Choose"/>); the loop itself now lives here too, so a caller can drive the window without a display and a
/// later view only has to draw <see cref="Monster"/>, <see cref="Count"/> and <see cref="Payment"/> and forward clicks
/// to the mutators below.
/// </para>
/// <para>
/// <b>Faithful quirk</b>: the reference's <c>SwitchMaxMinButtons</c> only touches the two buttons when at least one of
/// them is enabled, and the counts between the maximum and one only clear the label in front of them. A count that is
/// neither the maximum nor one therefore leaves the buttons in the state the last extreme left them: starting at the
/// maximum, MAX stays disabled and MIN keeps showing until the count reaches one, so the player cannot jump straight
/// back to the maximum. The port keeps that, because the label and the two buttons are pinned by tests and a "fixed"
/// version would draw a different dialog.
/// </para>
/// <para>
/// <b>Not ported</b>: buying. The reference returns the chosen <see cref="Troop"/> and leaves the payment to its
/// callers (the castle order pays through <c>OddFundsResource</c>); <see cref="Selected"/> is a request, not a purchase,
/// and the caller pays <see cref="Payment"/>. The creature info window behind a click on the unit (<c>ArmyInfo()</c>) is
/// out of scope for the same reason it is out of every ported dialog: the creature info window is not ported.
/// </para>
/// </remarks>
public sealed class RecruitDialog
{
    /// <summary>Title of the cost box, <c>_( "Cost per troop:" )</c>.</summary>
    public const string CostPerTroopTitle = "Cost per troop:";

    /// <summary>Label in front of the chosen count, <c>_( "Number to buy:" )</c>.</summary>
    public const string NumberToBuyTitle = "Number to buy:";

    /// <summary>Text on the button that jumps to the maximum, <c>_( "Max" )</c>.</summary>
    public const string MaxTitle = "Max";

    /// <summary>Text on the button that jumps to one, <c>_( "Min" )</c>.</summary>
    public const string MinTitle = "Min";

    private readonly GameKingdom? _kingdom;
    private readonly MonsterType _initialMonster;

    private string _typedValue = string.Empty;
    private bool _maxEnabled = true;
    private bool _minEnabled = true;

    private RecruitDialog(MonsterType monster, uint available, GameKingdom? kingdom, bool allowDowngradedMonster)
    {
        if (!MonsterStatistics.IsValid(monster))
        {
            throw new ArgumentOutOfRangeException(nameof(monster), monster, "Not a creature.");
        }

        _kingdom = kingdom;
        _initialMonster = monster;

        Monster = monster;
        Available = available;
        AllowDowngradedMonster = allowDowngradedMonster;
        Maximum = CalculateMaximum();
        Count = Maximum;

        // The reference disables both arrows when it cannot afford a single unit, before it labels them.
        if (Count == 0)
        {
            _maxEnabled = false;
            _minEnabled = false;
        }

        UpdateMaxMinButtons();
    }

    /// <summary>
    /// Builds the window, <c>Dialog::RecruitMonster()</c> (<c>dialog_recruit.cpp:253</c>).
    /// </summary>
    /// <param name="monster">The unit the dwelling offers, <c>monster0</c> of the reference.</param>
    /// <param name="available">How many units are available, <c>available</c>.</param>
    /// <param name="allowDowngradedMonster">
    /// Whether the player may also hire the previous tier of the dwelling, <c>allowDowngradedMonster</c>.
    /// </param>
    /// <returns>The model.</returns>
    /// <remarks>
    /// Without a treasury there is nothing to check the price against, so the stock is the maximum and
    /// <see cref="Funds"/>/<see cref="Remaining"/> read zero. The reference always has a kingdom; this overload is what
    /// the ADR-163 contract tests were written against, and the one that takes a kingdom is the real thing.
    /// </remarks>
    public static RecruitDialog Create(MonsterType monster, uint available, bool allowDowngradedMonster) =>
        new(monster, available, null, allowDowngradedMonster);

    /// <summary>
    /// Builds the window with the treasury that pays, <c>Dialog::RecruitMonster()</c> (<c>dialog_recruit.cpp:253</c>).
    /// </summary>
    /// <param name="monster">The unit the dwelling offers, <c>monster0</c> of the reference.</param>
    /// <param name="available">How many units are available, <c>available</c>.</param>
    /// <param name="kingdom">The kingdom that pays; its treasury is read, never written.</param>
    /// <param name="allowDowngradedMonster">
    /// Whether the player may also hire the previous tier of the dwelling, <c>allowDowngradedMonster</c>.
    /// </param>
    /// <returns>The model, opened at the maximum the treasury can take.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="kingdom"/> is <see langword="null"/>.</exception>
    public static RecruitDialog Create(MonsterType monster, uint available, GameKingdom kingdom,
        bool allowDowngradedMonster = true)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        return new RecruitDialog(monster, available, kingdom, allowDowngradedMonster);
    }

    /// <summary>The unit on offer, <c>monster</c>; the arrows walk its upgrade chain.</summary>
    public MonsterType Monster { get; private set; }

    /// <summary>How many units are available, <c>available</c>.</summary>
    public uint Available { get; }

    /// <summary>Whether the previous tier can be hired as well.</summary>
    public bool AllowDowngradedMonster { get; }

    /// <summary>How many the treasury can take, <c>max</c> of <c>CalculateMax()</c>.</summary>
    public uint Maximum { get; private set; }

    /// <summary>How many will be recruited, <c>result</c>.</summary>
    public uint Count { get; private set; }

    /// <summary>What one unit costs, <c>paymentMonster</c> = <c>monster.GetCost()</c>.</summary>
    public Funds PaymentPerTroop => MonsterStatistics.GetGeneralStats(Monster).GetCost();

    /// <summary>What the chosen count costs, <c>paymentCosts</c>.</summary>
    public Funds Payment => PaymentPerTroop * Count;

    /// <summary>The treasury of the kingdom, <c>funds</c>; empty when the window was built without one.</summary>
    public Funds Funds => _kingdom is null ? new Funds() : _kingdom.Treasury;

    /// <summary>What is left of the treasury once <see cref="Payment"/> is taken, the number in brackets.</summary>
    public Funds Remaining => Funds - Payment;

    /// <summary>Whether the count is worth buying, <c>buttonOk.isEnabled()</c>: zero disables it.</summary>
    public bool IsOkayEnabled => Count != 0;

    /// <summary>Whether the MAX button is drawn and clickable, <c>buttonMax.isEnabled()</c>.</summary>
    public bool IsMaxEnabled => _maxEnabled;

    /// <summary>Whether the MIN button is drawn and clickable, <c>buttonMin.isEnabled()</c>.</summary>
    public bool IsMinEnabled => _minEnabled;

    /// <summary>
    /// The word in front of the count, <c>SwitchMaxMinButtons()</c>: <see cref="MaxTitle"/>, <see cref="MinTitle"/> or
    /// empty.
    /// </summary>
    public string MaxMinLabel { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the arrows that walk the upgrade chain are shown, <c>showDowngradedMonsterSwitchButtons</c>.
    /// </summary>
    /// <remarks>
    /// The reference asks <c>allowDowngradedMonster &amp;&amp; monster0.GetDowngrade() != monster0</c>, so a unit with no
    /// downgrade (a peasant, a dragon) shows no arrows even when the caller allows them.
    /// </remarks>
    public bool CanSwitchMonster =>
        AllowDowngradedMonster && MonsterUpgrades.GetDowngrade(_initialMonster) != _initialMonster;

    /// <summary>The title of the window, <c>_( "Recruit %{name}" )</c> with the plural name filled in.</summary>
    public string Title => $"Recruit {MonsterNames.GetMulti(Monster)}";

    /// <summary>The stock line, <c>_( "Available: %{count}" )</c>.</summary>
    public string AvailableText =>
        $"Available: {Available.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>The request the window returns, <c>{ monster, result }</c>; the caller pays <see cref="Payment"/>.</summary>
    public Troop Selected => new(Monster, Count);

    /// <summary>
    /// The result of the dialog: the hired stack, or <see langword="null"/> when nothing was recruited.
    /// </summary>
    public Troop? Result { get; private set; }

    /// <summary>
    /// Records the stack that was recruited.
    /// </summary>
    /// <param name="troop">The hired stack, or <see langword="null"/> when the dialog was cancelled.</param>
    public void Choose(Troop? troop)
    {
        Result = troop is { Count: > 0 } ? troop : null;
    }

    /// <summary>
    /// Which unit of the upgrade chain is on offer, stepping one <b>down</b> the chain and wrapping around, the left
    /// arrow of the reference.
    /// </summary>
    /// <remarks>
    /// The chain is built downward from the unit the window opened with, so a window opened on an archer offers the
    /// archer and the peasant, and the left arrow always steps away from the upgrade. Switching recomputes the maximum
    /// and jumps the count to it, exactly like the reference.
    /// </remarks>
    public void SwitchToDowngrade() => SwitchMonster(1);

    /// <summary>
    /// Which unit of the upgrade chain is on offer, stepping one <b>up</b> the chain and wrapping around, the right
    /// arrow of the reference.
    /// </summary>
    public void SwitchToUpgrade() => SwitchMonster(-1);

    /// <summary>Sets the count, clamped to the range, and forgets the typed digits.</summary>
    /// <param name="count">The wanted count; anything above <see cref="Maximum"/> is cut down, zero stays zero.</param>
    public void SetCount(uint count)
    {
        Count = Math.Min(count, Maximum);
        _typedValue = string.Empty;
        UpdateMaxMinButtons();
    }

    /// <summary>Steps the count up by one, the up arrow and the mouse wheel, <c>result + 1</c>.</summary>
    /// <returns>Whether the count changed; false at the maximum.</returns>
    public bool Increase()
    {
        if (Count >= Maximum)
        {
            return false;
        }

        SetCount(Count + 1);
        return true;
    }

    /// <summary>Steps the count down by one, the down arrow and the mouse wheel, <c>result - 1</c>.</summary>
    /// <returns>Whether the count changed; false at zero.</returns>
    public bool Decrease()
    {
        if (Count == 0)
        {
            return false;
        }

        SetCount(Count - 1);
        return true;
    }

    /// <summary>Jumps to the maximum, the MAX button; nothing happens while that button is disabled.</summary>
    /// <returns>Whether the count changed.</returns>
    public bool SetToMaximum()
    {
        if (!_maxEnabled || Count == Maximum)
        {
            return false;
        }

        SetCount(Maximum);
        return true;
    }

    /// <summary>Jumps to one, the MIN button; nothing happens while that button is disabled.</summary>
    /// <returns>Whether the count changed.</returns>
    public bool SetToMinimum()
    {
        if (!_minEnabled || Count == 1)
        {
            return false;
        }

        SetCount(1);
        return true;
    }

    /// <summary>
    /// Feeds one typed digit, <c>processIntegerValueTyping( 0, max, typedValueBuf )</c> of the reference.
    /// </summary>
    /// <remarks>
    /// The digits accumulate: typing <c>1</c> then <c>2</c> asks for twelve. A digit that would push the number above
    /// <see cref="Maximum"/> is dropped and the buffer is left as it was, which is what keeps <c>12</c> from turning into
    /// <c>1</c> when the maximum is <c>1</c>. Backspace is a separate call, <see cref="ClearTyped"/>.
    /// </remarks>
    /// <param name="digit">A character from <c>'0'</c> to <c>'9'</c>.</param>
    /// <returns>Whether a number was accepted and <see cref="Count"/> changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="digit"/> is not a decimal digit.</exception>
    public bool TypeDigit(char digit)
    {
        if (digit is < '0' or > '9')
        {
            throw new ArgumentOutOfRangeException(nameof(digit), digit, "Not a decimal digit.");
        }

        string candidate = _typedValue + digit;

        // An overflowing number is as impossible as a too large one.
        if (!uint.TryParse(candidate, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out uint value) || value > Maximum)
        {
            return false;
        }

        _typedValue = candidate;
        Count = value;
        UpdateMaxMinButtons();

        return true;
    }

    /// <summary>
    /// Forgets the typed digits and sets the count to zero, the <c>KEY_BACKSPACE</c> branch of
    /// <c>processIntegerValueTyping</c>.
    /// </summary>
    /// <remarks>
    /// The reference returns <c>clamp( 0, min, max )</c> for this branch, which is zero for the recruit window's
    /// <c>min = 0</c>: pressing backspace empties the count instead of deleting one digit.
    /// </remarks>
    public void ClearTyped()
    {
        _typedValue = string.Empty;
        Count = 0;

        UpdateMaxMinButtons();
    }

    /// <summary>
    /// How many fit in the treasury, <c>CalculateMax()</c> (<c>dialog_recruit.cpp:242-253</c>).
    /// </summary>
    /// <remarks>
    /// The loop asks <c>kingdom.AllowPayment( cost * ( max + 1 ) )</c> and stops at the stock, one unit at a time: a
    /// treasury that can take a thousand peasants costs a thousand checks, exactly like the reference.
    /// </remarks>
    private uint CalculateMaximum()
    {
        Funds perTroop = MonsterStatistics.GetGeneralStats(Monster).GetCost();

        uint max = 0;

        while (max + 1 <= Available && AllowsPayment(perTroop * (max + 1)))
        {
            ++max;
        }

        return max;
    }

    /// <summary>Asks the treasury whether the payment fits; a window without one takes everything.</summary>
    /// <param name="payment">The payment to check.</param>
    /// <returns>Whether the payment fits.</returns>
    private bool AllowsPayment(in Funds payment) => _kingdom is null || _kingdom.AllowPayment(payment);

    /// <summary>The two arrows of the reference, <c>SwitchMaxMinButtons( btnMax, btnMin, max )</c>.</summary>
    /// <remarks>
    /// See the quirk in the remarks of the class: the label is only ever set when one of the buttons is enabled, and the
    /// buttons keep their state in the middle of the range.
    /// </remarks>
    private void UpdateMaxMinButtons()
    {
        if (!_maxEnabled && !_minEnabled)
        {
            MaxMinLabel = string.Empty;
            return;
        }

        if (Count == Maximum)
        {
            _maxEnabled = false;
            _minEnabled = true;
            MaxMinLabel = MaxTitle;
        }
        else if (Count == 1)
        {
            _minEnabled = false;
            _maxEnabled = true;
            MaxMinLabel = MinTitle;
        }
        else
        {
            MaxMinLabel = string.Empty;
        }
    }

    /// <summary>Walks the upgrade chain of the unit the window opened with.</summary>
    /// <param name="step">1 for a downgrade, -1 for an upgrade; both wrap around.</param>
    private void SwitchMonster(int step)
    {
        List<MonsterType> chain = [_initialMonster];

        while (MonsterUpgrades.GetDowngrade(chain[^1]) != chain[^1])
        {
            chain.Add(MonsterUpgrades.GetDowngrade(chain[^1]));
        }

        int index = chain.IndexOf(Monster);

        if (index < 0)
        {
            return;
        }

        index += step;

        if (index < 0)
        {
            index = chain.Count - 1;
        }
        else if (index >= chain.Count)
        {
            index = 0;
        }

        Monster = chain[index];
        Maximum = CalculateMaximum();

        if (Maximum == 0)
        {
            _maxEnabled = false;
            _minEnabled = false;
        }
        else if (!_maxEnabled && !_minEnabled)
        {
            _minEnabled = true;
        }

        _typedValue = string.Empty;
        Count = Maximum;

        UpdateMaxMinButtons();
    }
}
