using FHeroes2.App.Audio;
using FHeroes2.Assets.Video;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Video;

/// <summary>
/// Plays a Smacker video over the current scene, the port of <c>Video::ShowVideo()</c>
/// (<c>game_video.cpp:98-378</c>): the frames are centred on a black screen with their own palette, the soundtrack is
/// mixed once at the start, and any key or mouse button stops the playback.
/// </summary>
/// <remarks>
/// <para>
/// 参考在自己的循环里按微秒计时推进帧、并处理事件；端口把它写成一个 <see cref="IModalScreen"/>，
/// 由 <see cref="GameHost.RunModal"/> 驱动：<see cref="Update"/> 累积时间并按帧时长推进，
/// <see cref="Draw"/> 每帧把当前帧居中贴到屏上。
/// </para>
/// <para>
/// 参考的跳过条件是「任意按键或鼠标左右键」；端口在 <see cref="HotKey"/>、<see cref="PressKey"/> 和
/// 鼠标按下时直接收尾。停止后不等待按键松开，与参考的 <c>while (le.HandleEvents())</c> 退出后立即返回一致。
/// </para>
/// </remarks>
internal sealed class VideoModalScreen : IModalScreen
{
    private readonly SmkVideoSequence _sequence;
    private readonly AudioPlayer? _audio;
    private readonly bool _playSound;
    private readonly bool _loop;
    private readonly Palette _palette = new();

    private double _elapsed;
    private bool _audioPlayed;

    public VideoModalScreen(SmkVideoSequence sequence, AudioPlayer? audio, bool playSound, bool loop)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        _sequence = sequence;
        _audio = audio;
        _playSound = playSound && audio is not null && audio.IsAvailable;
        _loop = loop;

        IsFinished = _sequence.FrameCount == 0;
    }

    /// <inheritdoc/>
    public bool IsFinished { get; private set; }

    /// <inheritdoc/>
    public void SetCursor(Point? position)
    {
    }

    /// <inheritdoc/>
    public void SetLeftButton(bool down)
    {
        if (down)
        {
            IsFinished = true;
        }
    }

    /// <inheritdoc/>
    public void SetRightButton(bool down)
    {
        if (down)
        {
            IsFinished = true;
        }
    }

    /// <inheritdoc/>
    public void HotKey(ButtonHotKey hotKey) => IsFinished = true;

    /// <inheritdoc/>
    public void PressKey(Key key, KeyModifier modifier) => IsFinished = true;

    /// <inheritdoc/>
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        if (IsFinished)
        {
            return;
        }

        if (!_audioPlayed)
        {
            StartAudio();
            _audioPlayed = true;
        }

        _elapsed += Math.Max(0, elapsedSeconds);

        double frameSeconds = Math.Max(1, _sequence.MicrosecondsPerFrame) / 1_000_000.0;

        while (_elapsed >= frameSeconds)
        {
            _elapsed -= frameSeconds;

            if (_sequence.GetNextFrame())
            {
                continue;
            }

            if (!_loop)
            {
                IsFinished = true;

                return;
            }

            _sequence.ResetFrame();
        }
    }

    /// <inheritdoc/>
    public void Draw(ScreenBuffer frame)
    {
        frame.Pixels.Clear();

        int x = (frame.Pixels.Width - _sequence.Width) / 2;
        int y = (frame.Pixels.Height - _sequence.Height) / 2;

        _sequence.GetCurrentFrame(
            frame.Pixels.AsSpan(),
            frame.Pixels.Width,
            frame.Pixels.Height,
            x,
            y,
            out _,
            out _);

        ReadOnlySpan<byte> palette = _sequence.Palette;

        if (palette.Length >= 256 * 3)
        {
            _palette.Load(palette);
            frame.ChangePalette(_palette);
        }

        frame.InvalidateAll();
    }

    private void StartAudio()
    {
        if (!_playSound || _audio is null)
        {
            return;
        }

        foreach (byte[] channel in _sequence.AudioChannels)
        {
            _audio.PlayWave(channel);
        }
    }
}
