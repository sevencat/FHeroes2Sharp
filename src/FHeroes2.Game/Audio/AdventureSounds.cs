using FHeroes2.Assets.Audio;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Audio;

/// <summary>
/// The ambient loops of the adventure map, <c>Game::EnvironmentSoundMixer()</c> and
/// <c>Game::UpdateSoundsOnFocusUpdate()</c> (<c>game/game.cpp:155-192</c>, <c>:194-325</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps these next to the rest of the <c>Game::</c> namespace in <c>game.cpp</c>; the port has no
/// <c>Game</c> facade and <see cref="AudioManager"/> already owns the looping-sound planner the mixer feeds, so they
/// land beside it.
/// </para>
/// <para>
/// The reference reads the settings, the world and the mixer through three singletons. The port passes the settings
/// and the audio manager in, because a test needs to build them without a sound card, and takes the world as an
/// argument for the same reason <see cref="TileAmbientSound"/> does.
/// </para>
/// </remarks>
public static class AdventureSounds
{
    /// <summary>
    /// Width of one map tile in map pixels, <c>fheroes2::tileWidthPx</c>. The engine constant has no port home yet —
    /// the copies in the UI (<c>GameBorder</c>) are out of reach of this assembly.
    /// </summary>
    private const int TileWidthPx = 32;

    /// <summary>
    /// Whether the loops follow the focus, <c>Game::UpdateSoundsOnFocusUpdate()</c> and
    /// <c>Game::SetUpdateSoundsOnFocusUpdate()</c> (<c>game/game.cpp:155-163</c>).
    /// </summary>
    /// <remarks>The reference starts with the flag on (<c>game.cpp:64</c>).</remarks>
    public static bool UpdateSoundsOnFocusUpdate { get; set; } = true;

    /// <summary>
    /// Asks for the loops of the tiles around the focus, <c>Game::EnvironmentSoundMixer()</c>
    /// (<c>game/game.cpp:194-325</c>).
    /// </summary>
    /// <param name="world">The world whose tiles are scanned.</param>
    /// <param name="settings">The settings, which say whether sound is positioned and who the current player is.</param>
    /// <param name="audioManager">The manager that owns the looping channels.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The area scanned is a square of <c>±3</c> tiles, grown by one when the hero is between two tiles so that a
    /// moving hero does not cut a loop off abruptly. Tiles are visited by distance from the centre, so the
    /// <c>32 - 2</c> channels go to the closest sources first; two channels are kept for the hero's own movement
    /// sounds.
    /// </para>
    /// <para>
    /// Two sources of the same sound in the same direction are merged into one, keeping the closer distance. The
    /// reference writes the merge as a <c>find_if</c> that updates the match in place.
    /// </para>
    /// </remarks>
    public static void EnvironmentSoundMixer(World world, Settings settings, AudioManager audioManager)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(audioManager);

        int availableChannels = AudioManager.ChannelCount;
        if (availableChannels <= 2)
        {
            // 2 channels are left for hero's movement.
            return;
        }

        availableChannels -= 2;

        MapGeometry geometry = world.Geometry
                               ?? throw new InvalidOperationException("The ambient loops need a map of non-zero size.");

        Point center;
        Point tilePixelOffset = new(0, 0);

        Player? player = settings.Players.Current;
        if (player != null)
        {
            PlayerFocus focus = player.Focus;

            if (focus.GetHeroes() is { } hero)
            {
                center = geometry.GetPoint(hero.MapIndex);
                tilePixelOffset = hero.GetCurrentPixelOffset();
            }
            else if (focus.GetCastle() is { } castle)
            {
                center = geometry.GetPoint(castle.TileIndex);
            }
            else
            {
                center = new Point(world.Width / 2, world.Height / 2);
            }
        }
        else
        {
            center = new Point(world.Width / 2, world.Height / 2);
        }

        SortedDictionary<M82Sound, List<AudioLoopEffectInfo>> soundEffects = [];

        const int maxOffset = 3;

        // Usual area of getting object sounds around a center is 7 x 7 pixel. However, in case of a moving hero we
        // need to expand the area to make sound transition smooth.
        int scanningOffset = maxOffset;
        if (tilePixelOffset != new Point(0, 0))
        {
            ++scanningOffset;
        }

        List<Point> positions = new(2 * 2 * scanningOffset * scanningOffset);

        for (int y = -scanningOffset; y <= scanningOffset; ++y)
        {
            int posY = y + center.Y;

            for (int x = -scanningOffset; x <= scanningOffset; ++x)
            {
                if (geometry.IsValidAbsPoint(x + center.X, posY))
                {
                    positions.Add(new Point(x, y));
                }
            }
        }

        // Sort positions by distance to the center. LINQ's OrderBy is a stable sort, like std::stable_sort.
        List<Point> sortedPositions = [.. positions.OrderBy(pos => (pos.X * pos.X) + (pos.Y * pos.Y))];

        double maxDistance = Math.Sqrt(((maxOffset * maxOffset) + (maxOffset * maxOffset)) * (double)(TileWidthPx * TileWidthPx));

        bool is3DAudioEnabled = settings.Is3DAudioEnabled;

        foreach (Point pos in sortedPositions)
        {
            M82Sound soundType = TileAmbientSound.Get(world.GetTile(pos.X + center.X, pos.Y + center.Y), world);
            if (soundType == M82Sound.Unknown)
            {
                continue;
            }

            Point actualPosition = new((pos.X * TileWidthPx) - tilePixelOffset.X, (pos.Y * TileWidthPx) - tilePixelOffset.Y);

            double dblDistance = Math.Sqrt((actualPosition.X * (double)actualPosition.X) + (actualPosition.Y * (double)actualPosition.Y));
            if (dblDistance >= maxDistance)
            {
                continue;
            }

            // std::lround() rounds halfway cases away from zero, which is not Math.Round()'s default.
            byte distance = (byte)Math.Round(dblDistance * 255 / maxDistance, MidpointRounding.AwayFromZero);

            short angle = 0;

            if (is3DAudioEnabled)
            {
                // This is a schema how the direction of sound looks like:
                // |      0     |
                // | 270     90 |
                // |     180    |
                // so the direction to an object on the top is 0 degrees, on the right side - 90, bottom - 180 and left
                // side - 270 degrees.

                // We need to swap X and Y axes and invert Y axis as on screen Y axis goes from top to bottom.
                angle = (short)(Math.Atan2(actualPosition.X, -actualPosition.Y) * 180 / Math.PI);

                // It is extremely important to normalize the angle.
                if (angle < 0)
                {
                    angle = (short)(360 + angle);
                }
            }

            if (!soundEffects.TryGetValue(soundType, out List<AudioLoopEffectInfo>? effects))
            {
                effects = [];
                soundEffects[soundType] = effects;
            }

            // If there is already a source of the same sound in this direction, then choose the one that is closer.
            bool merged = false;
            for (int i = 0; i < effects.Count; ++i)
            {
                if (effects[i].Angle != angle)
                {
                    continue;
                }

                effects[i] = effects[i] with { Distance = Math.Min(distance, effects[i].Distance) };
                merged = true;

                break;
            }

            if (merged)
            {
                continue;
            }

            // Otherwise, use the current one for now.
            effects.Add(new AudioLoopEffectInfo(angle, distance));

            --availableChannels;
            if (availableChannels == 0)
            {
                break;
            }
        }

        SortedDictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>> request = [];
        foreach (KeyValuePair<M82Sound, List<AudioLoopEffectInfo>> pair in soundEffects)
        {
            request[pair.Key] = pair.Value;
        }

        audioManager.PlayLoopSoundsAsync(request);
    }
}
