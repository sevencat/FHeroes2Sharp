using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="RecruitHeroDialog"/>: the "Recruit Hero" window of a town.
/// </summary>
public sealed class RecruitHeroDialogTests
{
    private static Hero CreateHero()
    {
        Hero hero = new(Hero.LordKilburn, MapRace.Knight);

        Assert.Equal(MapRace.Knight, hero.Race);

        return hero;
    }

    [Fact]
    public void AHerowithoutArtifactsSaysSo()
    {
        Hero hero = CreateHero();

        RecruitHeroDialog dialog = RecruitHeroDialog.Create(hero, true);

        Assert.Equal(0, dialog.ArtifactCount);
        Assert.Equal("Lord Kilburn is a level 1 Knight without artifacts.",
            RecruitHeroDialog.Describe("Lord Kilburn", 1, MapRace.Knight, 0));
        Assert.Contains("is a level 1 Knight without artifacts.", dialog.Description);
        Assert.Equal(RecruitHeroDialog.Title, "Recruit Hero");
    }

    [Fact]
    public void OneArtifactUsesTheSingular()
    {
        Hero hero = CreateHero();
        Assert.True(hero.BagArtifacts.Push(ArtifactType.UltimateSword));

        RecruitHeroDialog dialog = RecruitHeroDialog.Create(hero, true);

        Assert.Equal(1, dialog.ArtifactCount);
        Assert.Equal("Lord Kilburn is a level 1 Knight with 1 artifact.",
            RecruitHeroDialog.Describe("Lord Kilburn", 1, MapRace.Knight, 1));
    }

    [Fact]
    public void SeveralArtifactsUseThePlural()
    {
        Hero hero = CreateHero();
        Assert.True(hero.BagArtifacts.Push(ArtifactType.UltimateSword));
        Assert.True(hero.BagArtifacts.Push(ArtifactType.UltimateShield));

        RecruitHeroDialog dialog = RecruitHeroDialog.Create(hero, true);

        Assert.Equal(2, dialog.ArtifactCount);
        Assert.Equal("Lord Kilburn is a level 1 Knight with 2 artifacts.",
            RecruitHeroDialog.Describe("Lord Kilburn", 1, MapRace.Knight, 2));
    }

    [Fact]
    public void ASpellBookIsNotCountedAsAnArtifact()
    {
        Hero hero = CreateHero();
        Assert.True(hero.BagArtifacts.Push(ArtifactType.MagicBook));

        // The book sits in the bag, so counting the bag alone would say "with 1 artifact".
        Assert.True(hero.HasSpellBook);
        Assert.Equal(1, hero.BagArtifacts.CountArtifacts());

        RecruitHeroDialog dialog = RecruitHeroDialog.Create(hero, true);

        Assert.Equal(0, dialog.ArtifactCount);
        Assert.Contains("without artifacts.", dialog.Description);
    }

    [Fact]
    public void TheBookIsTakenOutAndTheRestStillCounts()
    {
        Hero hero = CreateHero();
        Assert.True(hero.BagArtifacts.Push(ArtifactType.MagicBook));
        Assert.True(hero.BagArtifacts.Push(ArtifactType.UltimateSword));

        RecruitHeroDialog dialog = RecruitHeroDialog.Create(hero, true);

        Assert.Equal(1, dialog.ArtifactCount);
    }

    [Fact]
    public void TheConfirmationNamesTheHeroAndTheRace()
    {
        Assert.Equal("Recruit Lord Kilburn the Knight.",
            RecruitHeroDialog.DescribeConfirmation("Lord Kilburn", MapRace.Knight));
        Assert.Equal("Recruit Lord Kilburn the Necromancer.",
            RecruitHeroDialog.DescribeConfirmation("Lord Kilburn", MapRace.Necromancer));
    }

    [Fact]
    public void ThePriceIsTheRecruitHeroCondition()
    {
        RecruitHeroDialog dialog = RecruitHeroDialog.Create(CreateHero(), true);

        Assert.True(dialog.Cost == PaymentConditions.RecruitHero());
    }

    [Fact]
    public void TheEnabledStateIsTheCallers()
    {
        // AllowBuyHero() needs the kingdom and the visiting hero, so the caller decides.
        Assert.True(RecruitHeroDialog.Create(CreateHero(), true).CanRecruit);
        Assert.False(RecruitHeroDialog.Create(CreateHero(), false).CanRecruit);
    }

    [Fact]
    public void TheButtonsAreOkayAndCancel()
    {
        RecruitHeroDialog dialog = RecruitHeroDialog.Create(CreateHero(), true);

        Assert.Equal(DialogButton.Ok | DialogButton.Cancel, dialog.Buttons);
        Assert.Equal(DialogButton.Ok, RecruitHeroDialog.FromButton(0));
        Assert.Equal(DialogButton.Cancel, RecruitHeroDialog.FromButton(1));
    }
}
