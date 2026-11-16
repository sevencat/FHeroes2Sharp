using FHeroes2.Assets.Audio;

namespace FHeroes2.Game.Audio;

/// <summary>One playing looping sound, <c>ChannelAudioLoopEffectInfo</c> (<c>audio_manager.cpp:175-190</c>).</summary>
/// <param name="Info">Where the sound is heard from.</param>
/// <param name="Channel">The channel it plays on.</param>
public readonly record struct LoopChannelEffect(AudioLoopEffectInfo Info, int Channel);

/// <summary>A channel that has to be moved, produced by <see cref="AudioLoopEffects.Plan"/>.</summary>
/// <param name="Channel">The channel to move.</param>
/// <param name="Info">Where it should be heard from now.</param>
public readonly record struct MovedLoopChannel(int Channel, AudioLoopEffectInfo Info);

/// <summary>A looping sound that has to be started, produced by <see cref="AudioLoopEffects.Plan"/>.</summary>
/// <param name="Sound">The sound effect.</param>
/// <param name="Info">Where it is heard from.</param>
public readonly record struct PendingLoopEffect(M82Sound Sound, AudioLoopEffectInfo Info);

/// <summary>What <see cref="AudioLoopEffects.Plan"/> wants the device to do.</summary>
/// <remarks>
/// The reference calls the mixer in the middle of the algorithm. The port collects the calls instead, so that the
/// reuse rules can be checked without a sound card; the caller makes the calls and reports the channels of the new
/// sounds back with <see cref="AudioLoopEffects.Register"/>.
/// </remarks>
public sealed class AudioLoopPlan
{
    internal AudioLoopPlan(
        IReadOnlyList<int> channelsToStop,
        IReadOnlyList<MovedLoopChannel> channelsToMove,
        IReadOnlyList<PendingLoopEffect> effectsToStart)
    {
        ChannelsToStop = channelsToStop;
        ChannelsToMove = channelsToMove;
        EffectsToStart = effectsToStart;
    }

    /// <summary>Channels that have to be stopped.</summary>
    public IReadOnlyList<int> ChannelsToStop { get; }

    /// <summary>Channels of reused sounds that have to be moved to their new position.</summary>
    public IReadOnlyList<MovedLoopChannel> ChannelsToMove { get; }

    /// <summary>Sounds that have to be started on a new channel.</summary>
    public IReadOnlyList<PendingLoopEffect> EffectsToStart { get; }
}

/// <summary>
/// The looping sound effects currently heard and the rules for replacing them, <c>playLoopSoundsImpl()</c>
/// (<c>audio_manager.cpp:646-776</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every frame the world says which looping sounds should be heard and from where. Restarting them all every frame
/// would click, so the manager tries to keep the channels it already has: an effect with exactly the same position
/// keeps its channel untouched, one that moved takes over the channel of the sound it is closest to, and only what
/// is left over is started or stopped.
/// </para>
/// <para>
/// The order of the effects decides which channel is paired with which, and it is the order of the reference: the
/// sounds walk up in the order of their types, and within one type the order of the requests.
/// </para>
/// </remarks>
public sealed class AudioLoopEffects
{
    private readonly Func<int, bool> _isChannelPlaying;

    private readonly SortedDictionary<M82Sound, List<LoopChannelEffect>> _current = new();

    private bool _is3DEnabled;

    /// <summary>Builds the bookkeeping.</summary>
    /// <param name="isChannelPlaying">
    /// Whether a channel still plays, <c>Mixer::isPlaying()</c>. A channel that was stopped behind the manager's back
    /// must not be reused.
    /// </param>
    public AudioLoopEffects(Func<int, bool> isChannelPlaying)
    {
        _isChannelPlaying = isChannelPlaying;
    }

    /// <summary>Whether the current sounds carry a position, <c>is3DAudioLoopEffectsEnabled</c>.</summary>
    public bool Is3DEnabled => _is3DEnabled;

    /// <summary>The channels of one sound that are playing, in the order they were started.</summary>
    /// <param name="sound">The sound effect.</param>
    /// <returns>The channels, empty when the sound is not heard.</returns>
    public IReadOnlyList<LoopChannelEffect> GetChannels(M82Sound sound) =>
        _current.TryGetValue(sound, out List<LoopChannelEffect>? effects) ? effects : [];

    /// <summary>How many channels are held.</summary>
    public int ChannelCount => _current.Sum(pair => pair.Value.Count);

    /// <summary>
    /// Works out what has to change to hear the requested effects, <c>playLoopSoundsImpl()</c>.
    /// </summary>
    /// <param name="requested">The effects to hear, keyed by the sound type.</param>
    /// <param name="is3DAudioEnabled">Whether the effects carry a position.</param>
    /// <returns>The calls the device has to make.</returns>
    public AudioLoopPlan Plan(
        IReadOnlyDictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>> requested,
        bool is3DAudioEnabled)
    {
        List<int> channelsToStop = [];
        List<MovedLoopChannel> channelsToMove = [];
        List<PendingLoopEffect> effectsToStart = [];

        // Turning the positioning on or off changes what the running channels are worth, so they all go.
        if (_is3DEnabled != is3DAudioEnabled)
        {
            _is3DEnabled = is3DAudioEnabled;

            foreach (List<LoopChannelEffect> effects in _current.Values)
            {
                foreach (LoopChannelEffect effect in effects)
                {
                    if (_isChannelPlaying(effect.Channel))
                    {
                        channelsToStop.Add(effect.Channel);
                    }
                }
            }

            _current.Clear();
        }

        SortedDictionary<M82Sound, List<LoopChannelEffect>> replaceable = [];

        foreach (KeyValuePair<M82Sound, List<LoopChannelEffect>> pair in _current)
        {
            replaceable[pair.Key] = pair.Value;
        }

        _current.Clear();

        // A channel that was stopped behind the manager's back cannot be reused.
        foreach (M82Sound sound in replaceable.Keys.ToList())
        {
            replaceable[sound].RemoveAll(effect => !_isChannelPlaying(effect.Channel));

            if (replaceable[sound].Count == 0)
            {
                replaceable.Remove(sound);
            }
        }

        SortedDictionary<M82Sound, List<AudioLoopEffectInfo>> stillToAdd = [];

        foreach (KeyValuePair<M82Sound, IReadOnlyList<AudioLoopEffectInfo>> pair in requested)
        {
            stillToAdd[pair.Key] = [.. pair.Value];
        }

        foreach (M82Sound sound in stillToAdd.Keys.ToList())
        {
            if (!replaceable.TryGetValue(sound, out List<LoopChannelEffect>? effectsToReplace))
            {
                continue;
            }

            List<AudioLoopEffectInfo> effectsToAdd = stillToAdd[sound];

            // An effect that did not move keeps its channel untouched, which is the common case for a sound that
            // is simply still there.
            for (int addIndex = 0; addIndex < effectsToAdd.Count;)
            {
                int replaceIndex = effectsToReplace.FindIndex(effect => effect.Info == effectsToAdd[addIndex]);

                if (replaceIndex < 0)
                {
                    ++addIndex;
                    continue;
                }

                Add(sound, effectsToReplace[replaceIndex]);

                effectsToReplace.RemoveAt(replaceIndex);
                effectsToAdd.RemoveAt(addIndex);
            }

            // What is left moved. The closest channel of the same sound is taken over and turned towards the new
            // position instead of being restarted.
            int pairs = Math.Min(effectsToAdd.Count, effectsToReplace.Count);

            while (pairs-- > 0)
            {
                (int addIndex, int replaceIndex) = FindClosestPair(effectsToAdd, effectsToReplace);

                AudioLoopEffectInfo info = effectsToAdd[addIndex];
                int channel = effectsToReplace[replaceIndex].Channel;

                Add(sound, new LoopChannelEffect(info, channel));

                effectsToReplace.RemoveAt(replaceIndex);
                effectsToAdd.RemoveAt(addIndex);

                channelsToMove.Add(new MovedLoopChannel(channel, info));
            }

            if (effectsToReplace.Count == 0)
            {
                replaceable.Remove(sound);
            }
        }

        // Sounds that are no longer wanted stop.
        foreach (List<LoopChannelEffect> effects in replaceable.Values)
        {
            foreach (LoopChannelEffect effect in effects)
            {
                channelsToStop.Add(effect.Channel);
            }
        }

        // Everything that could not take over a channel is a new sound.
        foreach (KeyValuePair<M82Sound, List<AudioLoopEffectInfo>> pair in stillToAdd)
        {
            foreach (AudioLoopEffectInfo info in pair.Value)
            {
                effectsToStart.Add(new PendingLoopEffect(pair.Key, info));
            }
        }

        return new AudioLoopPlan(channelsToStop, channelsToMove, effectsToStart);
    }

    /// <summary>Remembers a sound that was started, <c>currentAudioLoopEffects[soundType].emplace_back()</c>.</summary>
    /// <param name="sound">The sound effect.</param>
    /// <param name="info">Where it is heard from.</param>
    /// <param name="channel">The channel the device started it on.</param>
    public void Register(M82Sound sound, AudioLoopEffectInfo info, int channel) =>
        Add(sound, new LoopChannelEffect(info, channel));

    /// <summary>Forgets every channel, <c>clearAllAudioLoopEffects()</c>.</summary>
    /// <returns>The channels that were held and have to be stopped.</returns>
    public IReadOnlyList<int> Clear()
    {
        List<int> channels = [];

        foreach (List<LoopChannelEffect> effects in _current.Values)
        {
            foreach (LoopChannelEffect effect in effects)
            {
                if (_isChannelPlaying(effect.Channel))
                {
                    channels.Add(effect.Channel);
                }
            }
        }

        _current.Clear();

        return channels;
    }

    /// <summary>
    /// Finds the closest pair of a sound to add and a channel to reuse,
    /// <c>findPairOfClosestSoundEffects()</c> (<c>audio_manager.cpp:594-629</c>).
    /// </summary>
    /// <remarks>
    /// The angle decides, the distance only breaks a tie. The reference only skips a candidate that is strictly worse,
    /// so of two equally good pairs the one found last wins.
    /// </remarks>
    private static (int AddIndex, int ReplaceIndex) FindClosestPair(
        List<AudioLoopEffectInfo> effectsToAdd,
        List<LoopChannelEffect> effectsToReplace)
    {
        (int AddIndex, int ReplaceIndex) result = (0, 0);

        int bestAngleDiff = int.MaxValue;
        int bestDistanceDiff = int.MaxValue;

        for (int addIndex = 0; addIndex < effectsToAdd.Count; ++addIndex)
        {
            AudioLoopEffectInfo effectToAdd = effectsToAdd[addIndex];

            for (int replaceIndex = 0; replaceIndex < effectsToReplace.Count; ++replaceIndex)
            {
                LoopChannelEffect effectToReplace = effectsToReplace[replaceIndex];

                int angleDiff = Math.Abs(effectToAdd.Angle - effectToReplace.Info.Angle);
                int distanceDiff = Math.Abs(effectToAdd.Distance - effectToReplace.Info.Distance);

                if (bestAngleDiff < angleDiff)
                {
                    continue;
                }

                if (bestAngleDiff == angleDiff && bestDistanceDiff < distanceDiff)
                {
                    continue;
                }

                bestAngleDiff = angleDiff;
                bestDistanceDiff = distanceDiff;

                result = (addIndex, replaceIndex);
            }
        }

        return result;
    }

    private void Add(M82Sound sound, LoopChannelEffect effect)
    {
        if (!_current.TryGetValue(sound, out List<LoopChannelEffect>? effects))
        {
            effects = [];
            _current[sound] = effects;
        }

        effects.Add(effect);
    }
}
