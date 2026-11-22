using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The behaviour of <see cref="MonsterFrameRenderer"/> against <c>ui_monster.cpp:31-59</c>.
/// </summary>
public sealed class MonsterFrameRendererTests
{
    private const byte PlateColour = 7;
    private const byte PortraitColour = 9;

    [Fact]
    public void Render_PaintsThePlateAtTheSlotCorner()
    {
        PixelBuffer output = new(40, 40);
        Sprite plate = Filled(10, 10, PlateColour);

        MonsterFrameRenderer.Render(MonsterType.Peasant, output, new Point(5, 6), new StubSprites(plate, null));

        Assert.Equal(PlateColour, output[5, 6]);
        Assert.Equal(PlateColour, output[14, 15]);
        Assert.Equal(0, output[15, 6]);
    }

    [Fact]
    public void Render_PaintsThePortraitAtItsOwnSourceOffset()
    {
        PixelBuffer output = new(40, 40);

        // A portrait whose source rectangle starts two pixels right and three pixels down of its nominal corner.
        Sprite portrait = Filled(4, 4, PortraitColour, offsetX: 2, offsetY: 3);

        MonsterFrameRenderer.Render(MonsterType.Peasant, output, new Point(5, 6), new StubSprites(null, portrait));

        // offset + the source offset, not the plain offset.
        Assert.Equal(PortraitColour, output[7, 9]);
        Assert.Equal(0, output[5, 6]);
    }

    [Fact]
    public void Render_DrawsThePortraitOverThePlate()
    {
        PixelBuffer output = new(40, 40);
        Sprite plate = Filled(10, 10, PlateColour);
        Sprite portrait = Filled(4, 4, PortraitColour);

        MonsterFrameRenderer.Render(MonsterType.Peasant, output, new Point(5, 6), new StubSprites(plate, portrait));

        Assert.Equal(PortraitColour, output[5, 6]);
        Assert.Equal(PlateColour, output[9, 10]);
    }

    [Fact]
    public void Render_WithoutArtwork_DrawsNothing()
    {
        PixelBuffer output = new(20, 20);

        MonsterFrameRenderer.Render(MonsterType.Peasant, output, new Point(0, 0), new StubSprites(null, null));

        Assert.All(output.Pixels, pixel => Assert.Equal(0, pixel));
    }

    [Fact]
    public void Render_AsksForTheRaceOfTheCreature()
    {
        PixelBuffer output = new(20, 20);
        StubSprites sprites = new(null, null);

        MonsterFrameRenderer.Render(MonsterType.Peasant, output, Point.Zero, sprites);

        // A peasant is a knight, and the plate lookup uses the race of the general statistics.
        Assert.Equal(MonsterStatistics.GetGeneralStats(MonsterType.Peasant).Race, sprites.RequestedRace);
    }

    private static Sprite Filled(int width, int height, byte colour, int offsetX = 0, int offsetY = 0)
    {
        Sprite sprite = new(width, height, offsetX, offsetY);
        sprite.Image.Fill(colour);

        return sprite;
    }

    private sealed class StubSprites(Sprite? plate, Sprite? portrait) : IMonsterFrameSprites
    {
        public MapRace RequestedRace { get; private set; } = MapRace.None;

        public Sprite? GetRaceFrame(MapRace race)
        {
            RequestedRace = race;

            return plate;
        }

        public Sprite? GetMonsterFrame(MonsterType monster) => portrait;
    }
}
