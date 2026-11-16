using System.Text;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers reading a sign, <c>ActionToSign</c> (<c>heroes_action.cpp:1243</c>).
/// </summary>
public sealed class HeroSignTests
{
    private const int SignTile = 0;

    /// <summary>
    /// A sign information block: marker, eight unused bytes and the message, see
    /// <c>MapSign.TryRead</c>.
    /// </summary>
    private static MapSign Sign(string message, int tileIndex = SignTile)
    {
        byte[] text = Encoding.ASCII.GetBytes(message);
        byte[] data = new byte[10 + text.Length];

        data[0] = 0x01;
        text.CopyTo(data, 9);

        Assert.True(MapSign.TryRead(data, tileIndex, out MapSign? sign));

        return sign!;
    }

    private static HeroActions SignActions(out World world, params MapSign[] signs)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        PlayerKingdom kingdom = kingdoms.GetKingdom(PlayerColor.Blue);
        Hero hero = new Hero(1, MapRace.Knight);
        kingdom.AddHero(hero);

        world = new World();
        world.GenerateUninitializedMap(2);

        foreach (MapSign sign in signs)
        {
            world.MapObjects.Add(sign);
        }

        return new HeroActions(hero, kingdoms);
    }

    [Fact]
    public void ASignGivesItsMessage()
    {
        HeroActions actions = SignActions(out World world, Sign("Beware of the dragon."));

        Assert.Equal("Beware of the dragon.", actions.ActionToSign(SignTile, world));
    }

    [Fact]
    public void ATileWithoutASignGivesNothing()
    {
        HeroActions actions = SignActions(out World world);

        Assert.Null(actions.ActionToSign(SignTile, world));
    }

    [Fact]
    public void ASignCanBeReadAgain()
    {
        HeroActions actions = SignActions(out World world, Sign("No treasure here."));

        actions.ActionToSign(SignTile, world);

        // Reading changes no state at all: no visit record, no metadata reset.
        Assert.Equal("No treasure here.", actions.ActionToSign(SignTile, world));
    }

    [Fact]
    public void AnEmptySignStaysEmpty()
    {
        HeroActions actions = SignActions(out World world, Sign(string.Empty));

        Assert.Equal(string.Empty, actions.ActionToSign(SignTile, world));
    }
}
