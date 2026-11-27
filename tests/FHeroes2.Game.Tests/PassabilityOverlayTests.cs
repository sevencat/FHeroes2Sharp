using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The passability markers of the editor, <c>PassableViewSurface()</c> and the blit of <c>redrawPassable()</c>
/// (<c>maps_tiles_render.cpp:188-277</c>, <c>:916-929</c>).
/// </summary>
/// <remarks>
/// 标记只有两种绿（普通可走 90、动作对象 115）和一种红（走不通 0xBA），一条边框一个像素宽。用例只钉这些常量与
/// 分段的判定，不做整帧像素对比——参考自己也是画边框，像素形状由循环决定。
/// </remarks>
public sealed class PassabilityOverlayTests
{
    private const byte Red = 0xBA;
    private const byte Green = 90;
    private const byte ActionGreen = 115;

    /// <summary>The border is drawn into the image layer, everything inside stays transparent.</summary>
    private const byte Transparent = 1;

    [Fact]
    public void IsMarked_NeedsAnActionObjectOrALostDirection()
    {
        // 空地全通又不是动作对象，参考不给标记——编辑器上一张新图因此是干干净净的。
        Assert.False(PassabilityOverlay.IsMarked(Directions.All, Mp2ObjectType.None));
        Assert.True(PassabilityOverlay.IsMarked(Direction.Unknown, Mp2ObjectType.None));
        Assert.True(PassabilityOverlay.IsMarked(Directions.All, Mp2ObjectType.ActionTreasureChest));
    }

    [Fact]
    public void GetSurface_FreeTile_DrawsAGreenFrame()
    {
        Sprite surface = PassabilityOverlay.GetSurface(Directions.All, isActionObject: false);

        Assert.Equal(PassabilityOverlay.Size, surface.Width);
        Assert.Equal(PassabilityOverlay.Size, surface.Height);

        // 四条边整条都是绿的，角上也是。
        for (int offset = 0; offset < PassabilityOverlay.Size; ++offset)
        {
            Assert.Equal(Green, surface.Image.Row(0)[offset]);
            Assert.Equal(Green, surface.Image.Row(PassabilityOverlay.Size - 1)[offset]);
            Assert.Equal(Green, surface.Image.Row(offset)[0]);
            Assert.Equal(Green, surface.Image.Row(offset)[PassabilityOverlay.Size - 1]);
        }

        // 框里面一个像素都不涂：图里是空的，透明层把地块的像素留住（1 = 保留目标）。
        int middle = PassabilityOverlay.Size / 2;

        Assert.Equal(Transparent, surface.Transform![(middle * PassabilityOverlay.Size) + middle]);
        Assert.Equal(0, surface.Image.Row(middle)[middle]);
    }

    [Fact]
    public void GetSurface_BlockedTile_DrawsARedFrame()
    {
        Sprite surface = PassabilityOverlay.GetSurface(Direction.Unknown, isActionObject: false);

        Assert.Equal(Red, surface.Image.Row(0)[0]);
        Assert.Equal(Red, surface.Image.Row(0)[PassabilityOverlay.Size / 2]);
        Assert.Equal(Red, surface.Image.Row(PassabilityOverlay.Size / 2)[0]);
    }

    [Fact]
    public void GetSurface_ActionObject_ShiftsTheGreen()
    {
        Sprite free = PassabilityOverlay.GetSurface(Directions.All, isActionObject: false);
        Sprite action = PassabilityOverlay.GetSurface(Directions.All, isActionObject: true);

        Assert.Equal(Green, free.Image.Row(0)[0]);
        Assert.Equal(ActionGreen, action.Image.Row(0)[0]);
    }

    [Fact]
    public void GetSurface_PartialMask_ColoursTheEdgesByThirds()
    {
        // 只留左上与右：上边与左边只有靠角的那三分之一是绿的，右边整条都绿。
        Sprite surface = PassabilityOverlay.GetSurface(Direction.TopLeft | Direction.Right, isActionObject: false);
        int last = PassabilityOverlay.Size - 1;

        // 上边：只有左边三分之一跟着左上角变绿，中段与右段都是红的。
        Assert.Equal(Green, surface.Image.Row(0)[0]);
        Assert.Equal(Red, surface.Image.Row(0)[PassabilityOverlay.Size / 2]);
        Assert.Equal(Red, surface.Image.Row(0)[last]);

        // 左边同理，只有上三分之一是绿的。
        Assert.Equal(Red, surface.Image.Row(PassabilityOverlay.Size / 2)[0]);
        Assert.Equal(Red, surface.Image.Row(last)[0]);

        // 右边只有中段那三分之一归直走；上三分之一归右上角、下三分之一归右下角，两个都没开，所以是红的。
        Assert.Equal(Red, surface.Image.Row(0)[last]);
        Assert.Equal(Red, surface.Image.Row(5)[last]);
        Assert.Equal(Green, surface.Image.Row(PassabilityOverlay.Size / 2)[last]);
        Assert.Equal(Green, surface.Image.Row(20)[last]);
        Assert.Equal(Red, surface.Image.Row(last)[last]);
        Assert.Equal(Red, surface.Image.Row(last - 1)[last]);
    }

    [Fact]
    public void Draw_PaintsOnlyTheFrameOfTheTile()
    {
        PixelBuffer target = new(40, 40);
        ImageOps.FillRect(target, new Rect(0, 0, 40, 40), 200);

        PassabilityOverlay.Draw(target, new Point(2, 3), Directions.All, isActionObject: false);

        Assert.Equal(Green, target.Row(3)[2]);

        // 框里面与框外都是原样：标记的透明层把内部的像素留住。
        Assert.Equal(200, target.Row(4)[3]);
        Assert.Equal(200, target.Row(3)[1]);
    }
}
