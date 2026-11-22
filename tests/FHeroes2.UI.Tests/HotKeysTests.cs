using FHeroes2.Game.System;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The hot key table of <c>game_hotkeys.cpp</c> and the key names of <c>KeySymGetName()</c>: pure data, so these run
/// without the game data.
/// </summary>
[Collection(HotKeysCollection.Name)]
public class HotKeysTests
{
    [Fact]
    public void KeyNames_EveryKeyHasANameThatParsesBack()
    {
        for (int value = (int)Key.None; value < (int)Key.LastKey; ++value)
        {
            Key key = (Key)value;
            string name = KeyNames.Get(key);

            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.True(KeyNames.TryParse(name, out Key parsed), $"'{name}' does not parse back.");
            Assert.Equal(key, parsed);
        }
    }

    [Fact]
    public void KeyNames_UnknownName_IsRejected()
    {
        Assert.False(KeyNames.TryParse("no such key", out Key key));
        Assert.Equal(Key.None, key);
        Assert.False(KeyNames.TryParse("", out _));
        Assert.False(KeyNames.TryParse(null, out _));
    }

    [Fact]
    public void KeyNames_ParsingIgnoresCase()
    {
        Assert.True(KeyNames.TryParse("left shift", out Key key));
        Assert.Equal(Key.LeftShift, key);
    }

    [Fact]
    public void GameHotKeys_DefaultsMatchTheReference()
    {
        GameHotKeys.Reset();

        Assert.Equal(Key.Enter, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.DefaultOkay));
        Assert.Equal(Key.Escape, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.DefaultCancel));
        Assert.Equal(Key.Delete, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.DefaultDelete));
        Assert.Equal(Key.F4, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.GlobalToggleFullScreen));
        Assert.Equal(Key.F10, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.GlobalToggleTextSupportMode));
        Assert.Equal(Key.KeyPad4, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldLeft));
        Assert.Equal(Key.V, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldViewWorld));
        Assert.Equal(Key.LeftShift, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.ArmySplitStackByHalf));
        Assert.Equal("V", GameHotKeys.GetHotKeyNameByEventId(HotKeyEvent.WorldViewWorld));
    }

    [Fact]
    public void GameHotKeys_SetHotKey_ChangesOnlyThatEvent()
    {
        GameHotKeys.Reset();
        GameHotKeys.SetHotKeyForEvent(HotKeyEvent.WorldViewWorld, Key.F7);

        Assert.Equal(Key.F7, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldViewWorld));
        Assert.Equal(Key.S, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldSaveGame));

        GameHotKeys.Reset();
        Assert.Equal(Key.V, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldViewWorld));
    }

    [Fact]
    public void GameHotKeys_SetHotKey_RejectsPseudoEvents()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameHotKeys.SetHotKeyForEvent(HotKeyEvent.None, Key.A));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameHotKeys.SetHotKeyForEvent(HotKeyEvent.NoEvent, Key.A));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameHotKeys.GetHotKeyForEvent(HotKeyEvent.NoEvent));
    }

    [Fact]
    public void GameHotKeys_SaveAndLoad_RoundTrips()
    {
        GameHotKeys.Reset();
        GameHotKeys.SetHotKeyForEvent(HotKeyEvent.WorldViewWorld, Key.F7);
        GameHotKeys.SetHotKeyForEvent(HotKeyEvent.DefaultOkay, Key.Space);

        string text = GameHotKeys.HotKeySave();

        Assert.Contains("view world = F7", text, StringComparison.Ordinal);
        Assert.Contains("# Default Actions:", text, StringComparison.Ordinal);

        // 改坏内存里的表再读回来，确认 Load 完全以文件为准。
        GameHotKeys.Reset();
        GameHotKeys.HotKeyLoad(text);

        Assert.Equal(Key.F7, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldViewWorld));
        Assert.Equal(Key.Space, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.DefaultOkay));
    }

    [Fact]
    public void GameHotKeys_LoadIgnoresUnknownEventsAndKeys()
    {
        GameHotKeys.Reset();

        GameHotKeys.HotKeyLoad(
            "# a file with junk\n" +
            "view world = F7\n" +
            "no such event = A\n" +
            "save game = NO SUCH KEY\n" +
            "a line without a separator\n");

        Assert.Equal(Key.F7, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldViewWorld));
        Assert.Equal(Key.S, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldSaveGame));
    }

    [Fact]
    public void GameHotKeys_LoadWithoutContent_KeepsTheDefaults()
    {
        GameHotKeys.Reset();
        GameHotKeys.SetHotKeyForEvent(HotKeyEvent.WorldViewWorld, Key.F7);

        GameHotKeys.HotKeyLoad(null);

        Assert.Equal(Key.V, GameHotKeys.GetHotKeyForEvent(HotKeyEvent.WorldViewWorld));
    }

    [Fact]
    public void GameHotKeys_EventNamesAreUnique()
    {
        HashSet<string> names = new(StringComparer.Ordinal);

        for (int value = 1; value < (int)HotKeyEvent.NoEvent; ++value)
        {
            string name = GameHotKeys.GetHotKeyEventNameByEventId((HotKeyEvent)value);
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.True(names.Add(name), $"'{name}' is used twice.");
        }
    }

    [Fact]
    public void GameHotKeys_EveryCategoryHasAName()
    {
        foreach (HotKeyCategory category in Enum.GetValues<HotKeyCategory>())
        {
            Assert.False(string.IsNullOrWhiteSpace(GameHotKeys.GetHotKeyCategoryName(category)));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => GameHotKeys.GetHotKeyCategoryName((HotKeyCategory)99));
    }

    [Fact]
    public void GameHotKeys_GetAllHotKeyEvents_StartsWithTheDefaults()
    {
        GameHotKeys.Reset();

        IReadOnlyList<(HotKeyEvent Event, HotKeyCategory Category)> events = GameHotKeys.GetAllHotKeyEvents();

        Assert.NotEmpty(events);
        Assert.Equal(HotKeyEvent.DefaultOkay, events[0].Event);
        Assert.Equal(HotKeyCategory.Default, events[0].Category);
        Assert.DoesNotContain(events, entry => entry.Event is HotKeyEvent.None or HotKeyEvent.NoEvent);
    }

    [Fact]
    public void GameHotKeys_GlobalKeyDownEvent_TogglesFullScreenUnlessModified()
    {
        GameHotKeys.Reset();
        Settings settings = Settings.Get();
        bool fullScreen = settings.IsFullScreen;

        try
        {
            Assert.False(GameHotKeys.GlobalKeyDownEvent(Key.F4, KeyModifier.Alt));
            Assert.False(GameHotKeys.GlobalKeyDownEvent(Key.F4, KeyModifier.Ctrl));
            Assert.Equal(fullScreen, settings.IsFullScreen);

            Assert.True(GameHotKeys.GlobalKeyDownEvent(Key.F4, KeyModifier.None));
            Assert.NotEqual(fullScreen, settings.IsFullScreen);
        }
        finally
        {
            settings.SetFullScreen(fullScreen);
        }
    }
}
