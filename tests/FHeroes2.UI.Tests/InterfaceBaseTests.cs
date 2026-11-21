using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="BaseInterface"/> and <see cref="GameArea"/>'s bookkeeping, <c>gui/interface_base.h:60-186</c> and
/// <c>gui/interface_gamearea.h:58-142</c>.
/// </summary>
public sealed class InterfaceBaseTests
{
    [Fact]
    public void SetRedraw_AccumulatesTheMask()
    {
        TestInterface @interface = new();

        Assert.False(@interface.NeedRedraw);
        Assert.Equal(Redraw.None, @interface.GetRedrawMask());

        @interface.SetRedraw(Redraw.Radar);
        @interface.SetRedraw(Redraw.Border | Redraw.GameArea);

        Assert.True(@interface.NeedRedraw);
        Assert.Equal(Redraw.Radar | Redraw.Border | Redraw.GameArea, @interface.GetRedrawMask());
    }

    [Fact]
    public void RedrawMask_HasTheReferenceValues()
    {
        Assert.Equal(0x01u, (uint)Redraw.RadarCursor);
        Assert.Equal(0x02u, (uint)Redraw.Radar);
        Assert.Equal(0x04u, (uint)Redraw.Border);
        Assert.Equal(0x08u, (uint)Redraw.GameArea);
        Assert.Equal(0x10u, (uint)Redraw.Status);
        Assert.Equal(0x20u, (uint)Redraw.Heroes);
        Assert.Equal(0x40u, (uint)Redraw.Castles);
        Assert.Equal(0x80u, (uint)Redraw.Buttons);
        Assert.Equal(0x100u, (uint)Redraw.Panel);
        Assert.Equal(0x1FFu, (uint)Redraw.All);
        Assert.Equal(0x200u, (uint)Redraw.Passabilities);

        // REDRAW_ICONS is the pair of the two halves of the icons panel.
        Assert.Equal(Redraw.Heroes | Redraw.Castles, Redraw.Icons);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    [InlineData(319, false)]
    public void IsScrollLeft_UsesTheBorderWidth(int x, bool expected)
    {
        Assert.Equal(expected, BaseInterface.IsScrollLeft(new Point(x, 200)));
    }

    [Theory]
    [InlineData(624, true)]
    [InlineData(625, true)]
    [InlineData(639, true)]
    [InlineData(623, false)]
    public void IsScrollRight_UsesTheBorderWidth(int x, bool expected)
    {
        Assert.Equal(expected, BaseInterface.IsScrollRight(new Point(x, 200), new Size(640, 480)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public void IsScrollTop_UsesTheBorderWidth(int y, bool expected)
    {
        Assert.Equal(expected, BaseInterface.IsScrollTop(new Point(200, y)));
    }

    [Theory]
    [InlineData(464, true)]
    [InlineData(479, true)]
    [InlineData(463, false)]
    public void IsScrollBottom_UsesTheBorderWidth(int y, bool expected)
    {
        Assert.Equal(expected, BaseInterface.IsScrollBottom(new Point(200, y), new Size(640, 480)));
    }

    [Fact]
    public void GameArea_KeepsTheViewportRectangle()
    {
        TestInterface @interface = new();
        GameArea area = @interface.GetGameArea();

        area.SetAreaPosition(16, 16, 608, 448);

        Assert.Equal(new Rect(16, 16, 608, 448), area.Roi);
        Assert.Equal(new Point(304, 224), area.MiddlePoint);

        // SetCenter() clears the scroll request, which is all the viewport can do until G3.
        Assert.False(area.NeedScroll);
    }

    [Fact]
    public void IsEditor_AndDragMovement_AreReported()
    {
        TestInterface game = new();
        TestInterface editor = new(isEditor: true);

        Assert.False(game.IsEditor);
        Assert.True(editor.IsEditor);
        Assert.True(game.UseMouseDragMovement);
        Assert.NotNull(game.GetRadar());
    }

    [Fact]
    public void RenderWithFadeInOrPlanRender_IsNotPorted()
    {
        TestInterface @interface = new();

        Assert.Throws<NotImplementedException>(() => @interface.RenderWithFadeInOrPlanRender(Redraw.All));
    }

    [Fact]
    public void ObjectFadingOut_ReachesZeroInSteps()
    {
        ObjectFadingOutInfo info = new(7, 100, FHeroes2.Assets.Maps.Mp2ObjectType.Trees);
        int steps = 0;

        Assert.Equal((byte)255, info.AlphaValue);
        Assert.False(info.IsAnimationCompleted);

        while (!info.Update())
        {
            ++steps;
        }

        // 255 down to 0 in steps of 20: twelve steps leave 15, the thirteenth clamps to 0.
        Assert.Equal(12, steps);
        Assert.Equal((byte)0, info.AlphaValue);
        Assert.True(info.IsAnimationCompleted);
    }

    [Fact]
    public void ObjectFadingIn_ReachesFullOpacityInSteps()
    {
        ObjectFadingInInfo info = new(7, 100, FHeroes2.Assets.Maps.Mp2ObjectType.Trees);
        int steps = 0;

        Assert.Equal((byte)0, info.AlphaValue);

        while (!info.Update())
        {
            ++steps;
        }

        // 0 up to 255 in steps of 20: twelve steps leave 240, the thirteenth clamps to 255.
        Assert.Equal(12, steps);
        Assert.Equal((byte)255, info.AlphaValue);
        Assert.True(info.IsAnimationCompleted);
    }

    [Fact]
    public void ObjectAnimation_KeepsTheObjectItBelongsTo()
    {
        ObjectFadingInInfo info = new(42, 321, FHeroes2.Assets.Maps.Mp2ObjectType.Trees);

        Assert.Equal(42u, info.Uid);
        Assert.Equal(321, info.TileId);
        Assert.Equal(FHeroes2.Assets.Maps.Mp2ObjectType.Trees, info.Type);
        Assert.Equal(ObjectFadingInInfo.AlphaStep, ObjectFadingOutInfo.AlphaStep);
    }

    private sealed class TestInterface : BaseInterface
    {
        public TestInterface(bool isEditor = false)
            : base(isEditor, new PixelBuffer(640, 480))
        {
        }

        public override void RedrawInterface(Redraw force) => SetRedraw(force);

        public override void MouseCursorAreaClickLeft(int tileIndex)
        {
        }

        public override void MouseCursorAreaPressRight(int tileIndex)
        {
        }

        public override void MouseCursorAreaLongPressLeft(int tileIndex)
        {
        }

        public override void Reset()
        {
        }

        public override void UpdateCursor(int tileIndex)
        {
        }
    }
}
