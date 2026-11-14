using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the 1:1 port of <c>src/fheroes2/resource/resource.cpp</c>'s <c>Funds</c>.
/// </summary>
public sealed class FundsTests
{
    [Fact]
    public void SingleResourceConstructorFillsOnlyThatField()
    {
        Assert.Equal(5, new Funds(Resource.Wood, 5).Wood);
        Assert.Equal(5, new Funds(Resource.Mercury, 5).Mercury);
        Assert.Equal(5, new Funds(Resource.Ore, 5).Ore);
        Assert.Equal(5, new Funds(Resource.Sulfur, 5).Sulfur);
        Assert.Equal(5, new Funds(Resource.Crystal, 5).Crystal);
        Assert.Equal(5, new Funds(Resource.Gems, 5).Gems);
        Assert.Equal(5, new Funds(Resource.Gold, 5).Gold);

        // Every other field stays zero.
        Funds wood = new(Resource.Wood, 5);
        Assert.Equal(0, wood.Mercury + wood.Ore + wood.Sulfur + wood.Crystal + wood.Gems + wood.Gold);
    }

    [Fact]
    public void ZeroCountAndUnknownResourceLeaveTheBagEmpty()
    {
        Assert.Equal(default, new Funds(Resource.Wood, 0));
        Assert.Equal(default, new Funds(Resource.Unknown, 5));
    }

    [Fact]
    public void SevenArgumentConstructorPutsOreFirstLikeTheReference()
    {
        Funds funds = new(1, 2, 3, 4, 5, 6, 7);

        Assert.Equal(1, funds.Ore);
        Assert.Equal(2, funds.Wood);
        Assert.Equal(3, funds.Mercury);
        Assert.Equal(4, funds.Sulfur);
        Assert.Equal(5, funds.Crystal);
        Assert.Equal(6, funds.Gems);
        Assert.Equal(7, funds.Gold);
    }

    [Fact]
    public void ArithmeticIsFieldWise()
    {
        Funds left = new(Resource.Wood, 10);
        Funds right = new(Resource.Ore, 4);

        Funds sum = left + right;
        Assert.Equal(10, sum.Wood);
        Assert.Equal(4, sum.Ore);

        Funds difference = left - right;
        Assert.Equal(10, difference.Wood);
        Assert.Equal(-4, difference.Ore);

        Funds doubled = left * 2;
        Assert.Equal(20, doubled.Wood);

        Funds halved = left / 2;
        Assert.Equal(5, halved.Wood);

        Funds inPlace = left;
        inPlace.Add(right);
        Assert.Equal(10, inPlace.Wood);
        Assert.Equal(4, inPlace.Ore);

        inPlace.Subtract(right);
        Assert.Equal(0, inPlace.Ore);

        inPlace.Multiply(3);
        Assert.Equal(30, inPlace.Wood);
    }

    [Fact]
    public void DivisionByZeroIsRejected()
    {
        Funds funds = new(Resource.Wood, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = funds / 0);
    }

    [Fact]
    public void ComparisonChecksEveryField()
    {
        Funds rich = new(10, 10, 10, 10, 10, 10, 10);
        Funds poor = new(1, 1, 1, 1, 1, 1, 1);
        Funds mixed = new(10, 10, 10, 10, 10, 10, 0);

        Assert.True(rich >= poor);
        Assert.False(poor >= rich);
        Assert.False(mixed >= rich);
        Assert.True(rich == new Funds(10, 10, 10, 10, 10, 10, 10));
        Assert.True(rich != poor);
        Assert.True(poor < rich);
        Assert.True(rich > poor);
    }

    [Fact]
    public void GetAndGetPtrReadTheSameField()
    {
        Funds funds = new(1, 2, 3, 4, 5, 6, 7);

        Assert.Equal(funds.Wood, funds.Get(Resource.Wood));
        Assert.Equal(funds.Gold, funds.Get(Resource.Gold));
        Assert.Equal(0, funds.Get(Resource.Unknown));

        Funds.GetPtr(ref funds, Resource.Gems) = 42;
        Assert.Equal(42, funds.Gems);

        // A bad flag throws; a lambda cannot capture a ref local, so the check is spelled out.
        bool thrown = false;
        try
        {
            Funds.GetPtr(ref funds, Resource.Unknown);
        }
        catch (ArgumentOutOfRangeException)
        {
            thrown = true;
        }

        Assert.True(thrown);
    }

    [Fact]
    public void MaxTakesTheLargerFieldOfEach()
    {
        Funds left = new(1, 2, 3, 4, 5, 6, 7);
        Funds right = new(7, 6, 5, 4, 3, 2, 1);
        Funds max = left.Max(right);

        Assert.Equal(7, max.Ore);
        Assert.Equal(6, max.Wood);
        Assert.Equal(5, max.Mercury);
        Assert.Equal(4, max.Sulfur);
        Assert.Equal(5, max.Crystal);
        Assert.Equal(6, max.Gems);
        Assert.Equal(7, max.Gold);
    }

    [Fact]
    public void TrimClampsEveryFieldToZero()
    {
        Funds funds = new(-1, -2, -3, -4, -5, -6, -7);
        funds.Trim();

        Assert.Equal(default, funds);
    }

    [Fact]
    public void ResetZeroesTheBag()
    {
        Funds funds = new(1, 2, 3, 4, 5, 6, 7);
        funds.Reset();

        Assert.Equal(default, funds);
    }

    [Fact]
    public void ValidItemsFollowTheFlagValues()
    {
        Funds funds = new(Resource.Wood, 3);
        funds.Gold = 1;

        Assert.Equal(Resource.Wood | Resource.Gold, funds.GetValidItems());
        Assert.Equal(2u, funds.GetValidItemsCount());

        Funds empty = default;
        Assert.Equal(0, empty.GetValidItems());
        Assert.Equal(0u, empty.GetValidItemsCount());
    }

    [Fact]
    public void FirstValidResourceFollowsTheReferenceOrder()
    {
        Funds funds = new(0, 0, 0, 0, 0, 0, 0);
        funds.Gold = 5;
        funds.Gems = 3;
        funds.Wood = 1;

        Assert.Equal((Resource.Wood, 1), funds.GetFirstValidResource());

        funds.Wood = 0;
        Assert.Equal((Resource.Gems, 3), funds.GetFirstValidResource());

        funds.Gems = 0;
        Assert.Equal((Resource.Gold, 5), funds.GetFirstValidResource());

        funds.Gold = 0;
        Assert.Equal((Resource.Unknown, 0), funds.GetFirstValidResource());
    }

    [Fact]
    public void LowestQuotientIsTheTightestResource()
    {
        Funds funds = new(10, 10, 10, 10, 10, 10, 10);
        Funds divisor = new(5, 5, 5, 5, 5, 5, 5);

        Assert.Equal(2, funds.GetLowestQuotient(divisor));

        // A zero divisor field does not constrain the result.
        Funds partial = new(0, 0, 0, 0, 0, 0, 4);
        Assert.Equal(2, funds.GetLowestQuotient(partial));
    }

    [Fact]
    public void CostCopiesIntoTheBag()
    {
        Cost cost = new() { Gold = 300, Wood = 5, Mercury = 1, Ore = 2, Sulfur = 3, Crystal = 4, Gems = 6 };
        Funds funds = new(cost);

        Assert.Equal(300, funds.Gold);
        Assert.Equal(5, funds.Wood);
        Assert.Equal(6, funds.Gems);

        Funds target = default;
        target.SetFrom(cost);
        Assert.Equal(funds, target);
    }

    [Fact]
    public void EventResourceUpdateIsClampedToWhatStillFits()
    {
        Funds current = new(0, 0, 0, 0, 0, 0, 10);
        Funds granted = new(0, 0, 0, 0, 0, 0, 20);

        // Negative event funds are the "take away" case.
        Funds update = Resource.CalculateEventResourceUpdate(current, new Funds(0, 0, 0, 0, 0, 0, -6));
        Assert.Equal(-6, update.Gold);

        // A negative result is clamped at zero, so the update cannot go below what the player has.
        Funds overdraw = Resource.CalculateEventResourceUpdate(current, new Funds(0, 0, 0, 0, 0, 0, -20));
        Assert.Equal(-10, overdraw.Gold);

        Assert.Equal(20, granted.Gold);
    }

    [Fact]
    public void ToStringListsTheReferenceFields()
    {
        Funds funds = new(1, 2, 3, 4, 5, 6, 7);

        Assert.Equal("ore: 1, wood: 2, mercury: 3, sulfur: 4, crystal: 5, gems: 6, gold: 7", funds.ToString());
    }
}
