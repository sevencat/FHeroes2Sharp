using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests for <see cref="IcnAnimation"/>: the frame range of the ICN table and the
/// <c>fheroes2::TimeDelay</c> timing. The sprites are synthetic so that the expected frame can be
/// asserted by identity; the last test checks the real lantern table.
/// </summary>
public sealed class IcnAnimationTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(250);

    [Fact]
    public void StartsOnTheFirstFrameOfTheRange()
    {
        Sprite[] sprites = CreateSprites(4);

        IcnAnimation animation = new(sprites, 1, 2, Frame);

        Assert.Equal(0, animation.FrameNumber);
        Assert.Equal(1, animation.SpriteIndex);
        Assert.Same(sprites[1], animation.Current);
        Assert.Equal(2, animation.FrameCount);
    }

    [Fact]
    public void Advance_BelowOneFrame_KeepsTheCurrentFrame()
    {
        IcnAnimation animation = new(CreateSprites(3), 0, 3, Frame);

        animation.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal(0, animation.FrameNumber);
    }

    [Fact]
    public void Advance_AccumulatesPartialSteps()
    {
        IcnAnimation animation = new(CreateSprites(3), 0, 3, Frame);

        // 200 ms is not enough, 200 + 60 crosses the 250 ms boundary exactly once.
        animation.Advance(TimeSpan.FromMilliseconds(200));
        animation.Advance(TimeSpan.FromMilliseconds(60));
        Assert.Equal(1, animation.FrameNumber);

        // The 10 ms that are left over count towards the next frame, so 230 ms are now enough.
        animation.Advance(TimeSpan.FromMilliseconds(230));
        Assert.Equal(1, animation.FrameNumber);

        animation.Advance(TimeSpan.FromMilliseconds(10));
        Assert.Equal(2, animation.FrameNumber);
    }

    [Fact]
    public void Advance_SkipsFramesWhenTheStepIsLong()
    {
        IcnAnimation animation = new(CreateSprites(10), 0, 10, Frame);

        animation.Advance(TimeSpan.FromMilliseconds(600));
        Assert.Equal(2, animation.FrameNumber);

        // A stall must not queue the skipped frames: 3 s are exactly 12 frames, so the cycle
        // continues at 4 instead of replaying 2 to 9 afterwards.
        animation.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(4, animation.FrameNumber);
    }

    [Fact]
    public void Advance_WrapsAroundAtTheEndOfTheCycle()
    {
        Sprite[] sprites = CreateSprites(3);
        IcnAnimation animation = new(sprites, 0, 3, Frame);

        animation.Advance(Frame);
        Assert.Equal(1, animation.FrameNumber);
        animation.Advance(Frame);
        Assert.Equal(2, animation.FrameNumber);
        animation.Advance(Frame);
        Assert.Equal(0, animation.FrameNumber);
        Assert.Same(sprites[0], animation.Current);

        // One full cycle lands on the first frame again.
        animation.Advance(Frame * 3);
        Assert.Equal(0, animation.FrameNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-250)]
    public void Advance_IgnoresNonPositiveTime(int milliseconds)
    {
        IcnAnimation animation = new(CreateSprites(3), 0, 3, Frame);
        animation.Advance(Frame);

        animation.Advance(TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(1, animation.FrameNumber);
    }

    [Fact]
    public void Reset_RewindsTheAnimation()
    {
        IcnAnimation animation = new(CreateSprites(3), 0, 3, Frame);
        animation.Advance(TimeSpan.FromMilliseconds(400));
        Assert.Equal(1, animation.FrameNumber);

        animation.Reset();

        Assert.Equal(0, animation.FrameNumber);

        // The accumulated 150 ms are gone as well, so a full frame is needed again.
        animation.Advance(TimeSpan.FromMilliseconds(150));
        Assert.Equal(0, animation.FrameNumber);
        animation.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, animation.FrameNumber);
    }

    [Fact]
    public void Constructor_RejectsARangeOutsideTheSpriteList()
    {
        Sprite[] sprites = CreateSprites(3);

        Assert.Throws<ArgumentException>(() => new IcnAnimation(sprites, 1, 3, Frame));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IcnAnimation(sprites, -1, 1, Frame));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IcnAnimation(sprites, 0, 0, Frame));
    }

    [Fact]
    public void Constructor_RejectsANonPositiveFrameDuration()
    {
        Sprite[] sprites = CreateSprites(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => new IcnAnimation(sprites, 0, 1, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IcnAnimation(sprites, 0, 1, TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Advance_StaysInsideTheConfiguredRange()
    {
        IcnAnimation animation = new(CreateSprites(5), 2, 2, Frame);

        // Only sprites 2 and 3 belong to the animation, sprite 4 must never be drawn.
        animation.Advance(Frame);
        Assert.Equal(3, animation.SpriteIndex);

        animation.Advance(Frame);
        Assert.Equal(2, animation.SpriteIndex);
    }

    [Fact]
    public void Shnganim_HasOneStaticFrameAnd39GlowFrames()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        using AssetBundle bundle = AssetBundle.Open(dataDirectory);
        IcnSpriteFile? file = bundle.TryReadIcn("SHNGANIM.ICN");

        Assert.NotNull(file);

        // The lantern of the main menu: sprite 0 is the unlit lamp drawn once, 1..39 are the glow.
        // ICN::getAnimatedIcnIndex() hard codes exactly this range as "1 + frame % 39".
        Assert.Equal(40, file.Count);
        Assert.Equal(new Core.Geometry.Rect(46, 176, 139, 131), AreaOf(file, 0));

        for (int index = 1; index < file.Count; ++index)
        {
            Assert.Equal(new Core.Geometry.Rect(48, 183, 136, 180), AreaOf(file, index));
            Assert.True(file.Sprites[index].HasTransformLayer);
        }

        IcnAnimation glow = IcnAnimation.FromFile(file, 1, 39, Frame);

        Assert.Same(file.Sprites[1], glow.Current);

        // 39 frames of 250 ms make a 9.75 s cycle, after which the first frame is shown again.
        glow.Advance(TimeSpan.FromMilliseconds(39 * 250));
        Assert.Equal(0, glow.FrameNumber);
    }

    private static Sprite[] CreateSprites(int count)
    {
        Sprite[] sprites = new Sprite[count];

        for (int index = 0; index < count; ++index)
        {
            PixelBuffer image = new(1, 1);
            image.Fill((byte)index);
            sprites[index] = new Sprite(image);
        }

        return sprites;
    }

    private static Core.Geometry.Rect AreaOf(IcnSpriteFile file, int index)
    {
        Sprite sprite = file.Sprites[index];
        return new Core.Geometry.Rect(sprite.OffsetX, sprite.OffsetY, sprite.Width, sprite.Height);
    }
}
