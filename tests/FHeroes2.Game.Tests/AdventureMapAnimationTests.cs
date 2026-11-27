using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the animation frames of <see cref="AdventureMapView"/>: the extra blit
/// <c>renderObjectPart()</c> / <c>renderMainObject()</c> do for a part whose catalogue entry carries an animation
/// (<c>maps_tiles_render.cpp:60-80</c>, <c>:166-171</c>).
/// </summary>
/// <remarks>
/// 两张图集都是从 <c>MapObjectCatalog</c> 里挑的真实条目：雪地水车的 177 号有六帧，魔法花园的 190 号也有六帧。替身贴图
/// 每帧填自己的帧号，于是画完看像素就知道选了哪一帧，不需要游戏数据。
/// </remarks>
public sealed class AdventureMapAnimationTests
{
    private const int TileSize = 2;

    /// <summary>Snow water wheel part with a six frame cycle.</summary>
    private const int WaterWheelFrame = 177;

    /// <summary>Magic garden main part with a six frame cycle.</summary>
    private const int MagicGardenFrame = 190;

    [Fact]
    public void Draw_PaintsTheAnimationFrameOfThePhase()
    {
        AdventureMapView view = CreateView(Mp2ObjectIcnType.ObjnSnow, WaterWheelFrame, quantity2: 1);
        PixelBuffer target = new(TileSize, TileSize);

        view.Draw(target, animationPhase: 0);
        Assert.Equal(WaterWheelFrame + 1, target.Row(0)[0]);

        view.Draw(target, animationPhase: 1);
        Assert.Equal(WaterWheelFrame + 2, target.Row(0)[0]);

        // Six frames in the cycle, so a phase one whole cycle later shows the first frame again.
        view.Draw(target, animationPhase: 6);
        Assert.Equal(WaterWheelFrame + 1, target.Row(0)[0]);
    }

    [Fact]
    public void Draw_LeavesPartsWithoutAnimationAtTheirBaseFrame()
    {
        // 15 号是喷泉主图，名册里没有动画帧：只画一层，相位再大也不动。
        AdventureMapView view = CreateView(Mp2ObjectIcnType.ObjnMul2, 15, quantity2: 1);
        PixelBuffer target = new(TileSize, TileSize);

        view.Draw(target, animationPhase: 3);

        Assert.Equal(15, target.Row(0)[0]);
    }

    [Fact]
    public void Draw_ShowsTheVisitedMagicGardenOneCycleFurther()
    {
        // metadata()[1] == 0 的花园被访问过（maps_tiles_render.cpp:166-171）：不是播动画，而是取整轮之后那一帧。
        AdventureMapView visited = CreateView(Mp2ObjectIcnType.ObjnMul2, MagicGardenFrame, quantity2: 0);
        PixelBuffer target = new(TileSize, TileSize);

        visited.Draw(target, animationPhase: 2);
        Assert.Equal(MagicGardenFrame + 6 + 1, target.Row(0)[0]);

        // 没访问过的照常播动画。
        AdventureMapView untouched = CreateView(Mp2ObjectIcnType.ObjnMul2, MagicGardenFrame, quantity2: 1);
        untouched.Draw(target, animationPhase: 2);
        Assert.Equal(MagicGardenFrame + 3, target.Row(0)[0]);
    }

    [Fact]
    public void Draw_SkipsTheSecondFrameWhenTheSourceLacksIt()
    {
        // 名册说 177 号有六帧，图集里却只备了主图：多出来的那一帧取不到，画面停在主图。
        AdventureMapView view = CreateView(
            Mp2ObjectIcnType.ObjnSnow,
            WaterWheelFrame,
            quantity2: 1,
            lastFrame: WaterWheelFrame);
        PixelBuffer target = new(TileSize, TileSize);

        view.Draw(target, animationPhase: 1);

        Assert.Equal(WaterWheelFrame, target.Row(0)[0]);
    }

    private static AdventureMapView CreateView(
        Mp2ObjectIcnType icnType,
        int icnIndex,
        byte quantity2,
        int lastFrame = byte.MaxValue)
    {
        Mp2Map map = SyntheticMap.Create(
            1,
            1,
            _ => default(Mp2TileInfo) with
            {
                TerrainImageIndex = 0,
                TerrainFlags = 0,
                ObjectName1 = (byte)((int)icnType << 2),
                ObjectName2 = 0,
                BottomIcnImageIndex = (byte)icnIndex,
                Quantity1 = 0,
                Quantity2 = quantity2,
            });

        return new AdventureMapView(
            map,
            TerrainTileSet.Create(CreateTerrainTil()),
            new Size(TileSize, TileSize),
            new TestObjectSprites(lastFrame));
    }

    /// <summary>One flat 2x2 terrain image.</summary>
    private static TilFile CreateTerrainTil()
    {
        List<byte> data = [1, 0, TileSize, 0, TileSize, 0];

        for (int pixel = 0; pixel < TileSize * TileSize; ++pixel)
        {
            data.Add(0);
        }

        return TilFile.Parse([.. data]);
    }

    /// <summary>Every object frame is a flat 2x2 fill of its own frame number.</summary>
    /// <param name="lastFrame">The highest frame the set holds, <see cref="byte.MaxValue"/> for a full one.</param>
    private sealed class TestObjectSprites(int lastFrame) : IObjectSpriteSource
    {
        public Sprite? GetSprite(Mp2ObjectIcnType type, int index)
        {
            if (index < 0 || index > Math.Min(lastFrame, byte.MaxValue))
            {
                return null;
            }

            Sprite sprite = new(TileSize, TileSize);
            sprite.Image.Fill((byte)index);

            return sprite;
        }
    }
}
