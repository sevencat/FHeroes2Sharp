using FHeroes2.Assets.Audio;
using FHeroes2.Game.Audio;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the two bookkeeping pieces of <c>audio_manager.cpp</c> that hold no device: the queue of the sound thread
/// and the rules that keep a looping sound on its channel.
/// </summary>
public sealed class AudioLoopEffectsTests
{
    private static readonly AudioLoopEffectInfo Origin = new(0, 0);

    private static SortedDictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>> Request(
        M82Sound sound,
        params AudioLoopEffectInfo[] effects) =>
        new() { [sound] = effects };

    /// <summary>
    /// Builds the bookkeeping with the positioning already known to be on. The first update after the switch changes
    /// every channel, so a test about the reuse rules has to start from the second one.
    /// </summary>
    private static AudioLoopEffects Stable(Func<int, bool> isChannelPlaying)
    {
        AudioLoopEffects effects = new(isChannelPlaying);

        effects.Plan(new SortedDictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>>(), is3DAudioEnabled: true);

        return effects;
    }

    [Fact]
    public void AnEffectThatDidNotMoveKeepsItsChannel()
    {
        HashSet<int> playing = [1];
        AudioLoopEffects effects = Stable(playing.Contains);
        AudioLoopEffectInfo info = new(90, 10);

        effects.Register(M82Sound.Aelmattk, info, 1);

        AudioLoopPlan plan = effects.Plan(Request(M82Sound.Aelmattk, info), is3DAudioEnabled: true);

        Assert.Empty(plan.ChannelsToStop);
        Assert.Empty(plan.ChannelsToMove);
        Assert.Empty(plan.EffectsToStart);
        Assert.Equal(1, effects.GetChannels(M82Sound.Aelmattk).Single().Channel);
    }

    [Fact]
    public void AMovedEffectTakesOverTheClosestChannel()
    {
        HashSet<int> playing = [7];
        AudioLoopEffects effects = Stable(playing.Contains);

        effects.Register(M82Sound.Aelmattk, Origin, 7);

        AudioLoopPlan plan = effects.Plan(Request(M82Sound.Aelmattk, new AudioLoopEffectInfo(10, 0)), true);

        Assert.Empty(plan.ChannelsToStop);
        Assert.Empty(plan.EffectsToStart);

        MovedLoopChannel moved = Assert.Single(plan.ChannelsToMove);
        Assert.Equal(7, moved.Channel);
        Assert.Equal(new AudioLoopEffectInfo(10, 0), moved.Info);
        Assert.Equal(7, effects.GetChannels(M82Sound.Aelmattk).Single().Channel);
    }

    [Fact]
    public void ASoundThatIsNoLongerWantedStops()
    {
        HashSet<int> playing = [1, 2];
        AudioLoopEffects effects = Stable(playing.Contains);

        effects.Register(M82Sound.Aelmattk, Origin, 1);
        effects.Register(M82Sound.Badluck, Origin, 2);

        AudioLoopPlan plan = effects.Plan(Request(M82Sound.Aelmattk, Origin), true);

        Assert.Equal(2, Assert.Single(plan.ChannelsToStop));
        Assert.Empty(plan.EffectsToStart);
        Assert.Equal(1, effects.ChannelCount);
    }

    [Fact]
    public void AChannelThatStoppedBehindTheBackIsNotReused()
    {
        HashSet<int> playing = [];
        AudioLoopEffects effects = Stable(playing.Contains);

        effects.Register(M82Sound.Aelmattk, Origin, 1);

        AudioLoopPlan plan = effects.Plan(Request(M82Sound.Aelmattk, Origin), true);

        // The channel is already silent, so there is nothing to stop and the sound has to be started again.
        Assert.Empty(plan.ChannelsToStop);
        Assert.Empty(plan.ChannelsToMove);
        Assert.Equal(M82Sound.Aelmattk, Assert.Single(plan.EffectsToStart).Sound);
    }

    [Fact]
    public void ASoundThatWasNeverHeardStartsOnANewChannel()
    {
        AudioLoopEffects effects = Stable(_ => true);

        AudioLoopPlan plan = effects.Plan(
            Request(M82Sound.Aelmattk, Origin, new AudioLoopEffectInfo(180, 5)),
            true);

        Assert.Empty(plan.ChannelsToStop);
        Assert.Empty(plan.ChannelsToMove);
        Assert.Equal(2, plan.EffectsToStart.Count);
    }

    [Fact]
    public void TurningThePositioningOffDropsEveryChannel()
    {
        HashSet<int> playing = [1, 2];
        AudioLoopEffects effects = Stable(playing.Contains);

        effects.Register(M82Sound.Aelmattk, Origin, 1);
        effects.Register(M82Sound.Badluck, Origin, 2);

        AudioLoopPlan plan = effects.Plan(
            new SortedDictionary<M82Sound, IReadOnlyList<AudioLoopEffectInfo>>(),
            is3DAudioEnabled: false);

        Assert.Equal(2, plan.ChannelsToStop.Count);
        Assert.Empty(plan.EffectsToStart);
        Assert.Equal(0, effects.ChannelCount);
        Assert.False(effects.Is3DEnabled);
    }

    [Fact]
    public void ATieGoesToThePairThatWasFoundLast()
    {
        HashSet<int> playing = [1, 2];
        AudioLoopEffects effects = Stable(playing.Contains);

        effects.Register(M82Sound.Aelmattk, new AudioLoopEffectInfo(0, 0), 1);
        effects.Register(M82Sound.Aelmattk, new AudioLoopEffectInfo(90, 0), 2);

        AudioLoopPlan plan = effects.Plan(Request(M82Sound.Aelmattk, new AudioLoopEffectInfo(45, 0)), true);

        // The reference only skips a candidate that is strictly worse, so the last of two equal ones wins.
        Assert.Equal(2, Assert.Single(plan.ChannelsToMove).Channel);
    }

    [Fact]
    public void TheDistanceOnlyBreaksATieOfTheAngle()
    {
        HashSet<int> playing = [1, 2];
        AudioLoopEffects effects = Stable(playing.Contains);

        effects.Register(M82Sound.Aelmattk, new AudioLoopEffectInfo(0, 100), 1);
        effects.Register(M82Sound.Aelmattk, new AudioLoopEffectInfo(0, 10), 2);

        AudioLoopPlan plan = effects.Plan(Request(M82Sound.Aelmattk, new AudioLoopEffectInfo(0, 12)), true);

        Assert.Equal(2, Assert.Single(plan.ChannelsToMove).Channel);
    }

    [Fact]
    public void MoreRequestsThanChannelsMovesWhatItCanAndStartsTheRest()
    {
        HashSet<int> playing = [1];
        AudioLoopEffects effects = Stable(playing.Contains);

        effects.Register(M82Sound.Aelmattk, new AudioLoopEffectInfo(5, 0), 1);

        AudioLoopPlan plan = effects.Plan(
            Request(M82Sound.Aelmattk, new AudioLoopEffectInfo(0, 0), new AudioLoopEffectInfo(90, 0)),
            true);

        Assert.Single(plan.ChannelsToMove);
        Assert.Single(plan.EffectsToStart);
    }

    [Fact]
    public void ClearingReportsTheChannelsToStop()
    {
        HashSet<int> playing = [1, 3];
        AudioLoopEffects effects = Stable(playing.Contains);

        effects.Register(M82Sound.Aelmattk, Origin, 1);
        effects.Register(M82Sound.Aelmattk, Origin, 2);
        effects.Register(M82Sound.Badluck, Origin, 3);

        IReadOnlyList<int> channels = effects.Clear();

        // The silent channel is not reported, the device would have nothing to stop.
        Assert.Equal(new[] { 1, 3 }, channels);
        Assert.Equal(0, effects.ChannelCount);
    }
}

/// <summary>Covers <c>AsyncSoundManager</c> without its thread.</summary>
public sealed class AudioTaskQueueTests
{
    private static MusicRequest Music(MusicTrack track) =>
        new(track, MusicSource.External, MusicPlaybackMode.PlayOnce);

    [Fact]
    public void ATuneReplacesTheTuneThatWasWaiting()
    {
        AudioTaskQueue queue = new();

        queue.PushMusic(Music(MusicTrack.KnightCastle));
        queue.PushMusic(Music(MusicTrack.BarbarianCastle));

        Assert.True(queue.HasMusicTask);
        Assert.True(queue.TryTakeNext(out AudioTask task));
        Assert.Equal(MusicTrack.BarbarianCastle, task.Music!.Value.Track);
        Assert.False(queue.HasMusicTask);
    }

    [Fact]
    public void SoundEffectsKeepTheirOrder()
    {
        AudioTaskQueue queue = new();

        queue.PushSound(M82Sound.Aelmattk);
        queue.PushSound(M82Sound.Badluck);
        queue.PushSound(M82Sound.Bless);

        Assert.Equal(3, queue.PendingSoundCount);

        List<M82Sound> played = [];

        while (queue.TryTakeNext(out AudioTask task))
        {
            Assert.Equal(AudioTaskType.PlaySound, task.Type);
            played.Add(task.Sound);
        }

        Assert.Equal(new[] { M82Sound.Aelmattk, M82Sound.Badluck, M82Sound.Bless }, played);
    }

    [Fact]
    public void ATuneComesBeforeTheSoundEffects()
    {
        AudioTaskQueue queue = new();

        queue.PushSound(M82Sound.Aelmattk);
        queue.PushMusic(Music(MusicTrack.KnightCastle));
        queue.PushLoopSound(new LoopSoundRequest([], is3DAudioEnabled: false));

        Assert.True(queue.TryTakeNext(out AudioTask first));
        Assert.Equal(AudioTaskType.PlayMusic, first.Type);

        Assert.True(queue.TryTakeNext(out AudioTask second));
        Assert.Equal(AudioTaskType.PlaySound, second.Type);
        Assert.Equal(M82Sound.Aelmattk, second.Sound);

        Assert.True(queue.TryTakeNext(out AudioTask third));
        Assert.Equal(AudioTaskType.PlayLoopSound, third.Type);

        Assert.False(queue.TryTakeNext(out _));
        Assert.Equal(AudioTaskType.None, queue.Running);
    }

    [Fact]
    public void TheNewestLoopingEffectsReplaceTheOlderSnapshot()
    {
        AudioTaskQueue queue = new();
        LoopSoundRequest first = new(
            [new KeyValuePair<M82Sound, IReadOnlyList<AudioLoopEffectInfo>>(M82Sound.Aelmattk, [new AudioLoopEffectInfo(1, 1)])],
            true);
        LoopSoundRequest second = new([], is3DAudioEnabled: false);

        queue.PushLoopSound(first);
        queue.PushLoopSound(second);

        Assert.True(queue.TryTakeNext(out AudioTask task));
        Assert.Same(second, task.Loop);
        Assert.False(queue.HasLoopSoundTask);
    }

    [Fact]
    public void CancellingTheTuneAlsoForgetsTheRunningTask()
    {
        AudioTaskQueue queue = new();

        queue.PushMusic(Music(MusicTrack.KnightCastle));
        Assert.True(queue.TryTakeNext(out _));
        Assert.Equal(AudioTaskType.PlayMusic, queue.Running);

        queue.RemoveMusicTask();

        Assert.Equal(AudioTaskType.None, queue.Running);
        Assert.False(queue.HasMusicTask);
    }

    [Fact]
    public void CancellingTheSoundEffectsAlsoForgetsTheRunningTask()
    {
        AudioTaskQueue queue = new();

        queue.PushSound(M82Sound.Aelmattk);
        Assert.True(queue.TryTakeNext(out _));
        Assert.Equal(AudioTaskType.PlaySound, queue.Running);

        queue.RemoveSoundTasks();

        Assert.Equal(AudioTaskType.None, queue.Running);
        Assert.Equal(0, queue.PendingSoundCount);
    }

    [Fact]
    public void CancellingTheSoundTakesTheLoopingEffectsToo()
    {
        AudioTaskQueue queue = new();

        queue.PushSound(M82Sound.Aelmattk);
        queue.PushLoopSound(new LoopSoundRequest([], is3DAudioEnabled: true));
        Assert.True(queue.TryTakeNext(out _));

        queue.RemoveAllSoundTasks();

        Assert.Equal(0, queue.PendingSoundCount);
        Assert.False(queue.HasLoopSoundTask);
        Assert.Equal(AudioTaskType.None, queue.Running);
    }

    [Fact]
    public void CancellingEverythingEmptiesTheQueue()
    {
        AudioTaskQueue queue = new();

        queue.PushMusic(Music(MusicTrack.KnightCastle));
        queue.PushSound(M82Sound.Aelmattk);
        queue.PushLoopSound(new LoopSoundRequest([], is3DAudioEnabled: false));

        queue.RemoveAllTasks();

        Assert.False(queue.HasMusicTask);
        Assert.Equal(0, queue.PendingSoundCount);
        Assert.False(queue.HasLoopSoundTask);
        Assert.False(queue.TryTakeNext(out _));
    }
}

/// <summary>Covers <c>getMusicUID()</c>.</summary>
public sealed class MusicIdentityTests
{
    [Fact]
    public void TheHighHalfIsTheSourceAndTheLowHalfIsTheTrack()
    {
        ulong identity = MusicIdentity.Get(MusicTrack.KnightCastle, MusicSource.External);

        Assert.Equal((ulong)MusicSource.External << 32, identity & 0xFFFF_FFFF_0000_0000);
        Assert.Equal((uint)MusicTrack.KnightCastle, (uint)identity);
    }

    [Fact]
    public void TheSameTuneFromTwoSourcesIsTwoIdentities()
    {
        Assert.NotEqual(
            MusicIdentity.Get(MusicTrack.KnightCastle, MusicSource.MidiOriginal),
            MusicIdentity.Get(MusicTrack.KnightCastle, MusicSource.MidiExpansion));
    }

    [Fact]
    public void ASentinelHasNoIdentity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MusicIdentity.Get(MusicTrack.Unknown, MusicSource.External));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MusicIdentity.Get(MusicTrack.Unused, MusicSource.External));
    }
}
