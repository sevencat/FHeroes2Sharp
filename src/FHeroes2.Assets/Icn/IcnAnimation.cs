using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Icn;

/// <summary>
/// A looping animation over a range of sprites of one ICN file: the part of
/// <c>ICN::getAnimatedIcnIndex()</c> (<c>src/fheroes2/agg/icn.cpp</c>) plus the frame delay of
/// <c>Game::DelayType</c> (<c>src/fheroes2/game/game_delays.cpp</c>) that the port needs.
/// </summary>
/// <remarks>
/// <para>
/// The reference engine does not store frame sequences in the file: a hard coded table maps every
/// animated ICN to a sprite index range, for example <c>SHNGANIM</c> (the lantern of the main menu)
/// is <c>1 + frame % 39</c> while sprite 0 is the unlit lantern drawn once. Porting that whole
/// table would mean translating hundreds of cases that no scene uses yet, so each scene states the
/// range it animates and this class owns the timing.
/// </para>
/// <para>
/// Timing matches <c>fheroes2::TimeDelay</c>: elapsed time accumulates and every whole
/// <see cref="FrameDuration"/> advances the frame by one, so a slow frame does not make the
/// animation lag behind and a long stall skips frames instead of replaying them. The sprites are
/// not copied, so all frames keep their own anchor from the ICN header
/// (<c>fheroes2::Blit( sprite, display, sprite.x(), sprite.y() )</c>).
/// </para>
/// </remarks>
public sealed class IcnAnimation
{
    private readonly IReadOnlyList<Sprite> _sprites;
    private readonly int _firstFrame;
    private readonly int _frameCount;
    private TimeSpan _accumulated;

    /// <summary>
    /// Creates an animation over <paramref name="frameCount"/> consecutive sprites, starting at
    /// <paramref name="firstFrame"/>.
    /// </summary>
    /// <param name="sprites">Sprites of the ICN file, in file order.</param>
    /// <param name="firstFrame">Index of the first animated sprite inside <paramref name="sprites"/>.</param>
    /// <param name="frameCount">Number of sprites the animation cycles through.</param>
    /// <param name="frameDuration">Time one frame stays on screen.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="firstFrame"/> is negative, <paramref name="frameCount"/> is not positive or
    /// <paramref name="frameDuration"/> is zero or negative.
    /// </exception>
    /// <exception cref="ArgumentException">The range does not fit into <paramref name="sprites"/>.</exception>
    public IcnAnimation(
        IReadOnlyList<Sprite> sprites, int firstFrame, int frameCount, TimeSpan frameDuration)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        if (firstFrame < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstFrame), firstFrame, "The first frame index cannot be negative.");
        }

        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameCount), frameCount, "An animation needs at least one frame.");
        }

        if (firstFrame + frameCount > sprites.Count)
        {
            throw new ArgumentException(
                $"Frames {firstFrame}..{firstFrame + frameCount - 1} do not fit into the " +
                $"{sprites.Count} sprites of the ICN file.",
                nameof(frameCount));
        }

        if (frameDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameDuration), frameDuration, "The frame duration must be positive.");
        }

        _sprites = sprites;
        _firstFrame = firstFrame;
        _frameCount = frameCount;
        FrameDuration = frameDuration;
    }

    /// <summary>Time one frame stays on screen.</summary>
    public TimeSpan FrameDuration { get; }

    /// <summary>Number of sprites the animation cycles through.</summary>
    public int FrameCount => _frameCount;

    /// <summary>Zero based position inside the cycle, <c>0..FrameCount - 1</c>.</summary>
    public int FrameNumber { get; private set; }

    /// <summary>Index of <see cref="Current"/> inside the sprite list of the ICN file.</summary>
    public int SpriteIndex => _firstFrame + FrameNumber;

    /// <summary>The sprite to blit for the current frame, at its own ICN anchor.</summary>
    public Sprite Current => _sprites[SpriteIndex];

    /// <summary>
    /// Creates an animation over a parsed ICN file, see
    /// <see cref="IcnAnimation(IReadOnlyList{Sprite}, int, int, TimeSpan)"/>.
    /// </summary>
    public static IcnAnimation FromFile(
        IcnSpriteFile file, int firstFrame, int frameCount, TimeSpan frameDuration)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new IcnAnimation(file.Sprites, firstFrame, frameCount, frameDuration);
    }

    /// <summary>
    /// Adds <paramref name="elapsed"/> to the clock and advances the frame once per whole
    /// <see cref="FrameDuration"/>, wrapping around at the end of the cycle.
    /// </summary>
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return;
        }

        // Work with the remainder so that a huge step cannot overflow the tick counter.
        long ticks = _accumulated.Ticks + elapsed.Ticks;
        long steps = ticks / FrameDuration.Ticks;

        _accumulated = TimeSpan.FromTicks(ticks % FrameDuration.Ticks);

        if (steps > 0)
        {
            FrameNumber = (int)((FrameNumber + steps) % _frameCount);
        }
    }

    /// <summary>Rewinds the animation to its first frame and forgets the accumulated time.</summary>
    public void Reset()
    {
        FrameNumber = 0;
        _accumulated = TimeSpan.Zero;
    }
}
