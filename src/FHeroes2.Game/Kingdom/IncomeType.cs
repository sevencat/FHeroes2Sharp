namespace FHeroes2.Game.Kingdom;

/// <summary>
/// Where a kingdom's daily income comes from, the five <c>INCOME_*</c> constants of the reference's
/// <c>Kingdom::GetIncome()</c> argument (<c>kingdom.h:72-79</c>).
/// </summary>
/// <remarks>
/// The reference's <c>INCOME_ALL</c> is <c>0xFF</c>, not the union of the five named bits, so that future bits are
/// already covered by it. Two of the five are unreachable in the port today (see <see cref="Artifacts"/> and
/// <see cref="HeroSkills"/>), and <see cref="CampaignBonus"/> needs the campaign save.
/// </remarks>
[Flags]
public enum IncomeType
{
    /// <summary>Nothing.</summary>
    None = 0x00,

    /// <summary>Captured mines, <c>INCOME_CAPTURED</c>.</summary>
    Captured = 0x01,

    /// <summary>Towns and their castles, <c>INCOME_CASTLES</c>.</summary>
    Castles = 0x02,

    /// <summary>
    /// Income and penalty of the artifacts the heroes carry, <c>INCOME_ARTIFACTS</c>.
    /// </summary>
    /// <remarks>
    /// Unreachable today: a hero has no artifact bag yet (ADR-112), so this bit adds nothing. The table behind it,
    /// <see cref="ProfitConditions.FromArtifact"/>, is already ported and tested.
    /// </remarks>
    Artifacts = 0x04,

    /// <summary>
    /// The estates skill of the heroes, <c>INCOME_HERO_SKILLS</c>.
    /// </summary>
    /// <remarks>Unreachable today: a hero has no secondary skills attached yet (ADR-112).</remarks>
    HeroSkills = 0x08,

    /// <summary>The rewards of the campaign, <c>INCOME_CAMPAIGN_BONUS</c>.</summary>
    /// <remarks>Unreachable today: there is no campaign save (and no campaign module), so the reference's branch has
    /// nothing to read.</remarks>
    CampaignBonus = 0x10,

    /// <summary>Everything, <c>INCOME_ALL</c>.</summary>
    All = 0xFF,
}
