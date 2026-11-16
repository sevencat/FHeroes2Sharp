using FHeroes2.Assets.Audio;

namespace FHeroes2.Game.Audio;

/// <summary>
/// What the sound thread still has to do, <c>AsyncSoundManager</c> (<c>audio_manager.cpp:85-250</c>), without the
/// thread.
/// </summary>
/// <remarks>
/// <para>
/// The reference runs a worker thread that wakes up on every request. The port keeps the bookkeeping and lets the
/// host decide when to drain it: <see cref="AudioManager.Update"/> takes the tasks one by one on the main thread,
/// which is what the reference effectively gets anyway once the worker owns the queue.
/// </para>
/// <para>
/// The three kinds of work are not queued alike, and that is the point of this class. A tune is a state, not a
/// request: pushing a second one replaces the first, because only the last tune asked for should be heard. Sound
/// effects are events: they queue up in order and are played one after another. Looping effects are a state again:
/// the newest snapshot replaces the older one.
/// </para>
/// <para>
/// Removing work also forgets what is running: a task that has already been taken but not finished must not be
/// applied, otherwise a stop order would be undone by the request it was meant to cancel.
/// </para>
/// </remarks>
public sealed class AudioTaskQueue
{
    private readonly Queue<M82Sound> _sounds = new();

    private MusicRequest? _music;

    private LoopSoundRequest? _loop;

    /// <summary>What the sound thread is doing right now, <c>_taskToExecute</c>.</summary>
    public AudioTaskType Running { get; private set; }

    /// <summary>Whether a tune is waiting, <c>isMusicTaskPresent()</c>.</summary>
    public bool HasMusicTask => _music.HasValue;

    /// <summary>Whether looping effects are waiting.</summary>
    public bool HasLoopSoundTask => _loop is not null;

    /// <summary>How many sound effects are waiting.</summary>
    public int PendingSoundCount => _sounds.Count;

    /// <summary>Asks for a tune, replacing the one that was waiting, <c>pushMusic()</c>.</summary>
    /// <param name="request">The tune to start.</param>
    public void PushMusic(MusicRequest request) => _music = request;

    /// <summary>Queues one sound effect, <c>pushSound()</c>.</summary>
    /// <param name="sound">The sound effect to play.</param>
    public void PushSound(M82Sound sound) => _sounds.Enqueue(sound);

    /// <summary>Asks for new looping effects, replacing the ones that were waiting, <c>pushLoopSound()</c>.</summary>
    /// <param name="request">The effects to hear.</param>
    public void PushLoopSound(LoopSoundRequest request) => _loop = request;

    /// <summary>Cancels the waiting tune, <c>removeMusicTask()</c>.</summary>
    public void RemoveMusicTask()
    {
        _music = null;

        if (Running == AudioTaskType.PlayMusic)
        {
            Running = AudioTaskType.None;
        }
    }

    /// <summary>Cancels the waiting sound effects, <c>removeSoundTasks()</c>.</summary>
    public void RemoveSoundTasks()
    {
        _sounds.Clear();

        if (Running == AudioTaskType.PlaySound)
        {
            Running = AudioTaskType.None;
        }
    }

    /// <summary>
    /// Cancels the waiting sound effects and the waiting looping effects, <c>removeAllSoundTasks()</c>.
    /// </summary>
    public void RemoveAllSoundTasks()
    {
        _sounds.Clear();
        _loop = null;

        if (Running is AudioTaskType.PlaySound or AudioTaskType.PlayLoopSound)
        {
            Running = AudioTaskType.None;
        }
    }

    /// <summary>Cancels everything, <c>removeAllTasks()</c>.</summary>
    public void RemoveAllTasks()
    {
        _music = null;
        _sounds.Clear();
        _loop = null;
        Running = AudioTaskType.None;
    }

    /// <summary>
    /// Takes the next task, <c>prepareTask()</c>.
    /// </summary>
    /// <remarks>
    /// A tune comes first, then the sound effects in the order they were queued, then the looping effects. The task is
    /// taken out of the queue right away, which is why <see cref="RemoveSoundTasks"/> and its neighbours also forget
    /// what is running: a cancel that arrives between taking and doing the task must win.
    /// </remarks>
    /// <param name="task">The task to do.</param>
    /// <returns>False when there is nothing to do.</returns>
    public bool TryTakeNext(out AudioTask task)
    {
        if (_music is { } music)
        {
            _music = null;
            Running = AudioTaskType.PlayMusic;
            task = AudioTask.ForMusic(music);
            return true;
        }

        if (_sounds.Count > 0)
        {
            Running = AudioTaskType.PlaySound;
            task = AudioTask.ForSound(_sounds.Dequeue());
            return true;
        }

        if (_loop is { } loop)
        {
            _loop = null;
            Running = AudioTaskType.PlayLoopSound;
            task = AudioTask.ForLoop(loop);
            return true;
        }

        Running = AudioTaskType.None;
        task = default;
        return false;
    }
}
