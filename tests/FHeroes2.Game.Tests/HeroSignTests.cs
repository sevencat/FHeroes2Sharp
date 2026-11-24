using System.Text;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers reading a sign, <c>ActionToSign</c> (<c>heroes_action.cpp:1243</c>), and reading a bottle, the
/// <c>OBJ_BOTTLE</c> branch of <c>ActionToPickupResource</c> (<c>heroes_action.cpp:758</c>).
/// </summary>
/// <remarks>
/// 两者共用同一份 <c>MapSign</c> 数据，差别只在「读完之后世界变不变」：牌子留在原处可以反复读，瓶子被捡走、
/// 元数据清空。
/// </remarks>
public sealed class HeroSignTests
{
    private const int SignTile = 0;

    private const int BottleTile = 1;

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

    private static HeroActions SignActions(out World world, params MapSign[] signs) =>
        SignActions(out world, out _, signs);

    private static HeroActions SignActions(out World world, out PlayerKingdom kingdom, params MapSign[] signs)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);
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

    [Fact]
    public void ABottleGivesItsMessageAndIsCleared()
    {
        HeroActions actions = SignActions(out World world, out PlayerKingdom kingdom,
            Sign("The treasure is north.", BottleTile));

        Tile tile = world.GetTile(BottleTile);

        tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        tile.SetMainObjectType(Mp2ObjectType.ActionBottle);
        tile.Metadata[0] = 7;

        int gold = kingdom.Treasury.Gold;

        string? message = actions.ActionToBottle(BottleTile, world);

        Assert.Equal("The treasure is north.", message);

        // 瓶子被捡走：元数据清零（参考在函数末尾统一 resetObjectMetadata），国库一分不动，名册不记。
        Assert.Equal(0u, tile.Metadata[0]);
        Assert.Equal(gold, kingdom.Treasury.Gold);
        Assert.False(actions.Hero.IsObjectTypeVisited(Mp2ObjectType.Bottle));
    }

    [Fact]
    public void ABottleIsDispatchedAsItsOwnKind()
    {
        HeroActions actions = SignActions(out World world, Sign("Land ho!", BottleTile));

        Tile tile = world.GetTile(BottleTile);

        tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        tile.SetMainObjectType(Mp2ObjectType.ActionBottle);

        HeroActionResult result = actions.Action(BottleTile, world);

        Assert.Equal(HeroActionKind.Bottle, result.Kind);
        Assert.Equal(Mp2ObjectType.Bottle, result.ObjectType);
        Assert.True(result.Changed);
        Assert.True(result.TryGetPayload(out string? message));
        Assert.Equal("Land ho!", message);
    }
}
