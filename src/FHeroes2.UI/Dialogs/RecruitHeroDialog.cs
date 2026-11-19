using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// "Recruit Hero", <c>Castle::DialogBuyHero()</c> (<c>castle_town.cpp:66-160</c>).
/// </summary>
/// <remarks>
/// <para>
/// A portrait, one line about the hero, the price of <c>PaymentConditions::RecruitHero()</c>, and OK/cancel. The line
/// is <c>"%{name} is a level %{value} %{race} "</c> followed by how many artifacts the hero carries — the sentence is
/// built in two halves by the reference, and the trailing space of the first half is part of the text, so it is kept.
/// </para>
/// <para>
/// <b>The spell book does not count as an artifact</b>: the reference asks
/// <c>hero-&gt;GetCountArtifacts()</c> and then subtracts one when <c>hasArtifact( Artifact::MAGIC_BOOK )</c>, because
/// the book is stored in the same bag. A hero with nothing but a book is described as having no artifacts.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): the text, the price and the enabled state, no layout. The enabled state is passed in
/// rather than asked for, because <c>Castle::AllowBuyHero()</c> needs the kingdom and the visiting hero and this model
/// has neither.
/// </para>
/// </remarks>
public sealed class RecruitHeroDialog
{
    /// <summary>The heading of the window, <c>_( "Recruit Hero" )</c>.</summary>
    public const string Title = "Recruit Hero";

    private RecruitHeroDialog(Hero hero, int artifactCount, bool canRecruit, Funds cost)
    {
        Hero = hero;
        ArtifactCount = artifactCount;
        CanRecruit = canRecruit;
        Cost = cost;
    }

    /// <summary>
    /// Builds the window, <c>Castle::DialogBuyHero( hero )</c> (<c>castle_town.cpp:66</c>).
    /// </summary>
    /// <param name="hero">The hero on offer.</param>
    /// <param name="canRecruit">
    /// Whether the town may hire right now, i.e. <c>Castle::AllowBuyHero()</c>; a disabled OK button when false.
    /// </param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public static RecruitHeroDialog Create(Hero hero, bool canRecruit)
    {
        ArgumentNullException.ThrowIfNull(hero);

        int artifacts = hero.BagArtifacts.CountArtifacts();

        // The spell book lives in the artifact bag, and the reference takes it back out of the tally.
        if (hero.HasSpellBook)
        {
            --artifacts;
        }

        return new RecruitHeroDialog(hero, artifacts, canRecruit, PaymentConditions.RecruitHero());
    }

    /// <summary>The hero on offer.</summary>
    public Hero Hero { get; }

    /// <summary>How many artifacts the hero carries, the spell book not included.</summary>
    public int ArtifactCount { get; }

    /// <summary>Whether the OK button works, <c>AllowBuyHero()</c> of the reference.</summary>
    public bool CanRecruit { get; }

    /// <summary>What hiring costs, <c>PaymentConditions::RecruitHero()</c>, shown under the portrait.</summary>
    public Funds Cost { get; }

    /// <summary>The buttons the dialog has.</summary>
    public DialogButton Buttons => DialogButton.Ok | DialogButton.Cancel;

    /// <summary>
    /// The line about the hero, <c>"%{name} is a level %{value} %{race} "</c> plus the artifact clause.
    /// </summary>
    /// <remarks>
    /// Three forms, not a template with a number: the reference picks between "with 1 artifact.",
    /// "with %{count} artifacts." and "without artifacts.".
    /// </remarks>
    public string Description => Describe(Hero.Name, Hero.Level, Hero.Race, ArtifactCount);

    /// <summary>
    /// The line about a hero, split out from <see cref="Description"/> so that the wording can be checked without a
    /// hero whose name has been loaded from a map.
    /// </summary>
    /// <param name="heroName">The hero's name, <c>%{name}</c>.</param>
    /// <param name="level">The hero's level, <c>%{value}</c>.</param>
    /// <param name="race">The hero's race, <c>%{race}</c>.</param>
    /// <param name="artifactCount">How many artifacts the hero carries, the spell book already taken out.</param>
    /// <returns>The sentence, with the reference's trailing space before the artifact clause folded in.</returns>
    public static string Describe(string heroName, int level, MapRace race, int artifactCount)
    {
        string artifacts = artifactCount switch
        {
            <= 0 => "without artifacts.",
            1 => "with 1 artifact.",
            _ => $"with {artifactCount} artifacts.",
        };

        return $"{heroName} is a level {level} {MapRaces.Name(race)} {artifacts}";
    }

    /// <summary>
    /// What right-clicking OK explains, <c>_( "Recruit %{name} the %{race}" )</c> plus a full stop.
    /// </summary>
    public string ConfirmText => DescribeConfirmation(Hero.Name, Hero.Race);

    /// <summary>
    /// The confirmation sentence of <see cref="ConfirmText"/>, split out for the same reason as
    /// <see cref="Describe"/>.
    /// </summary>
    /// <param name="heroName">The hero's name, <c>%{name}</c>.</param>
    /// <param name="race">The hero's race, <c>%{race}</c>.</param>
    /// <returns>The sentence.</returns>
    public static string DescribeConfirmation(string heroName, MapRace race) =>
        $"Recruit {heroName} the {MapRaces.Name(race)}.";

    /// <summary>
    /// The result of pressing a button, <c>buttonGroup.processEvents()</c> at <c>castle_town.cpp:143</c>.
    /// </summary>
    /// <param name="index">Index of the pressed button: 0 OK, 1 cancel.</param>
    /// <returns><see cref="DialogButton.Ok"/> or <see cref="DialogButton.Cancel"/>.</returns>
    public static DialogButton FromButton(int index) =>
        index == 0 ? DialogButton.Ok : DialogButton.Cancel;
}
