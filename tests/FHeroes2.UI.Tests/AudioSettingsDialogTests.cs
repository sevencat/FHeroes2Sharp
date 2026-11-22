using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Audio;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The audio page of the system options (<c>dialog_audio.cpp</c>): layout, the two volumes, the music type and the 3D
/// switch.
/// </summary>
public sealed class AudioSettingsDialogTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void WithoutEveryFrameThereIsNoPage()
    {
        Assert.False(Artwork(frames: 11).IsComplete);
        Assert.Null(AudioSettingsDialog.TryCreate(Screen, Artwork(frames: 11), Font(), Font()));
        Assert.True(Artwork().IsComplete);
    }

    [Fact]
    public void ThePageIsTheSizeOfTheReferenceWindow()
    {
        AudioSettingsDialog dialog = Create();

        Assert.Equal(AudioSettingsDialog.Width, dialog.Window.ActiveArea.Width);
        Assert.Equal(AudioSettingsDialog.Height, dialog.Window.ActiveArea.Height);
    }

    [Fact]
    public void TheFourIconsSitAtTheReferenceOffsets()
    {
        AudioSettingsDialog dialog = Create();
        Rect active = dialog.Window.ActiveArea;
        int left = active.X + AudioSettingsDialog.OptionsOffsetX;
        int top = active.Y + AudioSettingsDialog.OptionsOffsetY;

        Assert.Equal(new Rect(left, top, 65, 65), dialog.MusicVolumeArea);
        Assert.Equal(new Rect(left + 118, top, 65, 65), dialog.SoundVolumeArea);
        Assert.Equal(new Rect(left, top + 110, 65, 65), dialog.MusicTypeArea);
        Assert.Equal(new Rect(left + 118, top + 110, 65, 65), dialog.Audio3DArea);
    }

    [Fact]
    public void WithoutADeviceTheVolumesDoNotMove()
    {
        Settings settings = Settings.Get();
        settings.SetMusicVolume(6);
        AudioSettingsDialog dialog = Create(audio: null);

        Assert.False(Click(dialog, dialog.MusicVolumeArea));
        Assert.Equal(6, settings.MusicVolume);
        Assert.False(dialog.IsDirty);
    }

    [Fact]
    public void ClickingTheMusicVolumeStepsItAndPushesTheDevice()
    {
        Settings settings = Settings.Get();
        settings.SetMusicVolume(6);
        FakeAudioDevice device = new();
        AudioSettingsDialog dialog = Create(device);

        Assert.False(Click(dialog, dialog.MusicVolumeArea));

        Assert.Equal(7, settings.MusicVolume);
        Assert.Equal(70, device.MusicVolume);
        Assert.True(dialog.IsDirty);

        settings.SetMusicVolume(6);
    }

    [Fact]
    public void TheVolumeWrapsAfterTen()
    {
        Settings settings = Settings.Get();
        settings.SetMusicVolume(10);
        FakeAudioDevice device = new();
        AudioSettingsDialog dialog = Create(device);

        Click(dialog, dialog.MusicVolumeArea);

        // 参考的 ( volume + 1 ) % 11：过 10 之后回到 0，也就是「off」。
        Assert.Equal(0, settings.MusicVolume);
        Assert.Equal(0, device.MusicVolume);

        settings.SetMusicVolume(6);
    }

    [Fact]
    public void TheWheelStepsTheSoundVolume()
    {
        Settings settings = Settings.Get();
        settings.SetSoundVolume(6);
        FakeAudioDevice device = new();
        AudioSettingsDialog dialog = Create(device);

        Wheel(dialog, dialog.SoundVolumeArea, up: true);

        Assert.Equal(7, settings.SoundVolume);
        Assert.Equal(70, device.SoundVolume);

        Wheel(dialog, dialog.SoundVolumeArea, up: false);

        Assert.Equal(6, settings.SoundVolume);

        settings.SetSoundVolume(6);
    }

    [Fact]
    public void ClickingTheMusicTypeCyclesIt()
    {
        Settings settings = Settings.Get();
        settings.SetMusicType(MusicSource.External);
        AudioSettingsDialog dialog = Create(new FakeAudioDevice());

        Click(dialog, dialog.MusicTypeArea);

        Assert.Equal(MusicSource.MidiOriginal, settings.MusicType);
        Assert.True(dialog.IsDirty);

        settings.SetMusicType(MusicSource.External);
    }

    [Fact]
    public void ClickingThe3DAudioTogglesItAndRebuildsTheAmbience()
    {
        Settings settings = Settings.Get();
        settings.Set3DAudio(false);
        AudioSettingsDialog dialog = Create(new FakeAudioDevice());
        int rebuilt = 0;

        Assert.False(dialog.ProcessEvents(ClickEvent(dialog.Audio3DArea), () => ++rebuilt));

        Assert.True(settings.Is3DAudioEnabled);
        Assert.Equal(1, rebuilt);
        Assert.True(dialog.IsDirty);

        settings.Set3DAudio(false);
    }

    [Fact]
    public void EnterLeavesThePage()
    {
        AudioSettingsDialog dialog = Create();

        LocalEvent enter = new();
        enter.OnKeyboard(Key.Enter, KeyModifier.None, KeyboardEventState.Down);

        Assert.True(dialog.ProcessEvents(enter));

        LocalEvent escape = new();
        escape.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down);

        Assert.True(dialog.ProcessEvents(escape));
    }

    [Fact]
    public void RightClicksAskForTheExplanations()
    {
        AudioSettingsDialog dialog = Create();

        RightClick(dialog, dialog.MusicVolumeArea);

        DialogPopup music = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("Music", music.Header);
        Assert.NotNull(dialog.TakePopup());

        RightClick(dialog, dialog.Audio3DArea);

        DialogPopup audio3D = dialog.Popup ?? throw new InvalidOperationException("A popup was expected.");

        Assert.Equal("3D Audio", audio3D.Header);
    }

    private static AudioSettingsDialog Create(FakeAudioDevice? device = null) =>
        Create(device is null ? null : new AudioManager(device, new StubAudioSource(), Settings.Get()));

    private static AudioSettingsDialog Create(AudioManager? audio) =>
        AudioSettingsDialog.TryCreate(Screen, Artwork(), Font(), Font(), audio)
            ?? throw new InvalidOperationException("The page should have been created.");

    private static bool Click(AudioSettingsDialog dialog, Rect area) =>
        dialog.ProcessEvents(ClickEvent(area));

    private static LocalEvent ClickEvent(Rect area)
    {
        LocalEvent events = new();
        Point position = new(area.X + 4, area.Y + 4);

        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return events;
    }

    private static void RightClick(AudioSettingsDialog dialog, Rect area)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, new Point(area.X + 4, area.Y + 4));
        dialog.ProcessEvents(events);
    }

    private static bool Wheel(AudioSettingsDialog dialog, Rect area, bool up)
    {
        LocalEvent events = new();

        events.OnMouseMotion(new Point(area.X + 4, area.Y + 4));
        events.OnMouseWheel(new Point(0, up ? 1 : -1));

        return dialog.ProcessEvents(events);
    }

    private static AudioSettingsArtwork Artwork(int frames = 12) => new()
    {
        SpanelFrames = [.. Enumerable.Range(0, frames).Select(_ => new Sprite(65, 65))],
        Buttons = new FakeButtonArtworkSource(),
    };

    private static FakeGlyphSource Font() => new(6, 12);

    private sealed class FakeAudioDevice : IAudioDevice
    {
        public bool IsValid => true;

        public bool IsMusicPlaying => false;

        public int MusicVolume { get; private set; } = 100;

        public int SoundVolume { get; private set; } = 100;

        public int Play(ReadOnlySpan<byte> wave, bool loop, AudioLoopEffectInfo? position = null) => -1;

        public void SetPosition(int channel, short angle, byte distance)
        {
        }

        public void StopSound(int channel)
        {
        }

        public void StopAllSounds()
        {
        }

        public bool IsSoundPlaying(int channel) => false;

        public bool PlayMusicFromDatabase(ulong uid, MusicPlaybackMode mode) => false;

        public void PlayMusic(ulong uid, ReadOnlySpan<byte> midi, MusicPlaybackMode mode)
        {
        }

        public void PlayMusic(ulong uid, string filePath, MusicPlaybackMode mode)
        {
        }

        public void StopMusic()
        {
        }

        public void SetMusicVolume(int percent) => MusicVolume = percent;

        public void SetSoundVolume(int percent) => SoundVolume = percent;
    }

    private sealed class StubAudioSource : IAudioSource
    {
        public bool IsExpansionArchiveAvailable => false;

        public IReadOnlyList<string> MusicDirectories => [];

        public byte[]? TryGetSoundBody(M82Sound sound) => null;

        public byte[]? TryGetMidi(XmiTrack track, bool ignoreExpansion) => null;
    }

    private sealed class FakeButtonArtworkSource : IDialogButtonArtworkSource
    {
        private readonly DialogButtonArtwork _artwork;

        public FakeButtonArtworkSource()
        {
            Sprite sprite = new(new PixelBuffer(60, 25), 0, 0);
            _artwork = new DialogButtonArtwork(sprite, sprite);
        }

        public DialogButtonArtwork Get(DialogButton button) => _artwork;
    }

    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly int _advance;

        public FakeGlyphSource(int advance, int lineHeight)
        {
            _advance = advance;
            LineHeight = lineHeight;
        }

        public int LineHeight { get; }

        public int SpaceWidth => _advance;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => _advance;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = new Sprite(new PixelBuffer(_advance, LineHeight), 0, 0);
            return true;
        }
    }
}
