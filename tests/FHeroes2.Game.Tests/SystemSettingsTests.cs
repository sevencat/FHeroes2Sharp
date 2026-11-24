using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Serializes the tests that drive the <see cref="Settings"/> singleton: it is process wide state, and every test in
/// the collection rebuilds it from scratch with <c>Settings.Reset()</c>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SettingsSingletonCollection
{
    /// <summary>Name of the collection.</summary>
    public const string Name = "Settings singleton";
}

/// <summary>
/// Covers <c>system/settings.cpp</c>: the first-run defaults, the clamping of every numeric setter, the two cycles
/// and the question whether the evil interface is on.
/// </summary>
[Collection(SettingsSingletonCollection.Name)]
public sealed class SettingsTests
{
    private static Settings Fresh()
    {
        Settings.Reset();

        return Settings.Get();
    }

    [Fact]
    public void TheFirstRunDefaultsAreTheDesktopOnes()
    {
        Settings settings = Fresh();

        Assert.True(settings.IsFirstGameRun);

        Assert.True(settings.IsShowRadar);
        Assert.True(settings.IsShowIcons);
        Assert.True(settings.IsShowButtons);
        Assert.True(settings.IsShowStatus);
        Assert.True(settings.IsBattleShowGrid);
        Assert.True(settings.IsBattleShowMouseShadow);
        Assert.True(settings.IsBattleShowMoveShadow);
        Assert.True(settings.IsBattleAutoResolve);

        Assert.False(settings.ShowControlPanel);
        Assert.False(settings.IsFullScreen);
        Assert.False(settings.IsBattleAutoSpellcast);
        Assert.False(settings.IsBattleShowDamage);
        Assert.False(settings.IsHideInterface);

        Assert.True(settings.IsEditorAnimationEnabled);
        Assert.False(settings.IsEditorPassabilityEnabled);

        Assert.Equal(Settings.DefaultSpeedDelay, settings.HeroesMoveSpeed);
        Assert.Equal(Settings.DefaultSpeedDelay, settings.AiMoveSpeed);
        Assert.Equal(Settings.DefaultBattleSpeed, settings.BattleSpeed);
        Assert.Equal(6, settings.SoundVolume);
        Assert.Equal(6, settings.MusicVolume);
        Assert.Equal(10, settings.ControllerPointerSpeed);
        Assert.Equal(AdventureMapScrollSpeed.Normal, settings.ScrollSpeed);

        Assert.Equal(InterfaceType.Good, settings.InterfaceType);
        Assert.Equal(MusicSource.External, settings.MusicType);
        Assert.Equal(BattleTurnOrderState.Off, settings.BattleTurnOrder);
        Assert.Equal(ZoomLevel.Level1, settings.ViewWorldZoomLevel);
        Assert.Equal(SaveFileSortingMethod.Filename, settings.SaveFileSortType);
        Assert.Equal(GameDifficulty.Normal, settings.GameDifficulty);
        Assert.Equal(GameTypes.None, settings.GameType);

        Assert.Equal(new ResolutionInfo(640, 480), settings.CurrentResolutionInfo);
        Assert.Equal(new Point(-1, -1), settings.SavedWindowPosition);

        Assert.Equal(string.Empty, settings.GameLanguage);
        Assert.Null(settings.CurrentMapFileInfo);
        Assert.False(settings.IsMapSmoothScrollingEnabled);
        Assert.Empty(settings.Players.All);
    }

    [Fact]
    public void TheVolumesAreClampedToTenSteps()
    {
        Settings settings = Fresh();

        settings.SetSoundVolume(-4);
        Assert.Equal(0, settings.SoundVolume);

        settings.SetSoundVolume(42);
        Assert.Equal(10, settings.SoundVolume);

        settings.SetSoundVolume(7);
        Assert.Equal(7, settings.SoundVolume);

        settings.SetMusicVolume(-1);
        Assert.Equal(0, settings.MusicVolume);

        settings.SetMusicVolume(11);
        Assert.Equal(10, settings.MusicVolume);

        settings.SetMusicVolume(3);
        Assert.Equal(3, settings.MusicVolume);
    }

    [Fact]
    public void TheSpeedsAreClampedButOnlyTheComputerMayStandStill()
    {
        Settings settings = Fresh();

        settings.SetAiMoveSpeed(-2);
        Assert.Equal(0, settings.AiMoveSpeed);

        settings.SetAiMoveSpeed(0);
        Assert.Equal(0, settings.AiMoveSpeed);

        settings.SetAiMoveSpeed(99);
        Assert.Equal(10, settings.AiMoveSpeed);

        settings.SetHeroesMoveSpeed(0);
        Assert.Equal(1, settings.HeroesMoveSpeed);

        settings.SetHeroesMoveSpeed(-3);
        Assert.Equal(1, settings.HeroesMoveSpeed);

        settings.SetHeroesMoveSpeed(11);
        Assert.Equal(10, settings.HeroesMoveSpeed);

        settings.SetBattleSpeed(0);
        Assert.Equal(1, settings.BattleSpeed);

        settings.SetBattleSpeed(20);
        Assert.Equal(10, settings.BattleSpeed);

        settings.SetBattleSpeed(8);
        Assert.Equal(8, settings.BattleSpeed);
    }

    [Fact]
    public void TheScrollSpeedIsClampedToTheFiveValuesOfTheEnum()
    {
        Settings settings = Fresh();

        settings.SetScrollSpeed(-1);
        Assert.Equal(AdventureMapScrollSpeed.None, settings.ScrollSpeed);

        settings.SetScrollSpeed(3);
        Assert.Equal(AdventureMapScrollSpeed.Fast, settings.ScrollSpeed);

        settings.SetScrollSpeed(99);
        Assert.Equal(AdventureMapScrollSpeed.VeryFast, settings.ScrollSpeed);

        settings.SetScrollSpeed(0);
        Assert.Equal(AdventureMapScrollSpeed.None, settings.ScrollSpeed);
    }

    [Fact]
    public void SwitchingTheExpansionOffAlsoLeavesTheExpansionMusic()
    {
        Settings settings = Fresh();

        Assert.False(settings.IsPriceOfLoyaltySupported);

        settings.EnablePriceOfLoyaltySupport(true);
        Assert.True(settings.IsPriceOfLoyaltySupported);

        settings.SetMusicType(MusicSource.MidiExpansion);
        settings.EnablePriceOfLoyaltySupport(false);

        Assert.False(settings.IsPriceOfLoyaltySupported);
        Assert.Equal(MusicSource.MidiOriginal, settings.MusicType);
    }

    [Fact]
    public void TheMusicOfAnotherSourceSurvivesTheExpansionBeingSwitchedOff()
    {
        Settings settings = Fresh();

        settings.SetMusicType(MusicSource.External);
        settings.EnablePriceOfLoyaltySupport(true);
        settings.EnablePriceOfLoyaltySupport(false);

        Assert.Equal(MusicSource.External, settings.MusicType);
    }

    [Fact]
    public void EachSwitchSetsAndClearsItsOwnBit()
    {
        Settings settings = Fresh();

        settings.SetFullScreen(true);
        Assert.True(settings.IsFullScreen);

        settings.SetFullScreen(false);
        Assert.False(settings.IsFullScreen);

        settings.SetVSync(true);
        Assert.True(settings.IsVSyncEnabled);

        settings.SetShowRadar(false);
        Assert.False(settings.IsShowRadar);
        Assert.True(settings.IsShowIcons);

        settings.SetBattleShowDamage(true);
        Assert.True(settings.IsBattleShowDamage);
        Assert.True(settings.IsBattleShowGrid);

        settings.SetBattleAutoSpellcast(true);
        Assert.True(settings.IsBattleAutoSpellcast);

        settings.SetEditorPassability(true);
        Assert.True(settings.IsEditorPassabilityEnabled);

        settings.SetEditorAnimation(false);
        Assert.False(settings.IsEditorAnimationEnabled);
    }

    [Fact]
    public void TheGameTypeIsASetOfBitsAndCampaignIsOneOfThem()
    {
        Settings settings = Fresh();

        Assert.False(settings.IsGameType(GameTypes.None));
        Assert.False(settings.IsCampaignGameType);

        settings.SetGameType(GameTypes.HotSeat);

        Assert.True(settings.IsGameType(GameTypes.Multi));
        Assert.True(settings.IsGameType(GameTypes.HotSeat | GameTypes.Standard));
        Assert.False(settings.IsGameType(GameTypes.Campaign));
        Assert.False(settings.IsCampaignGameType);

        settings.SetGameType(GameTypes.Standard | GameTypes.Campaign);

        Assert.True(settings.IsCampaignGameType);
        Assert.True(settings.IsGameType(GameTypes.Standard));
        Assert.False(settings.IsGameType(GameTypes.HotSeat));
    }

    [Fact]
    public void TheFirstRunMarkIsClearedOnce()
    {
        Settings settings = Fresh();

        settings.ResetFirstGameRun();

        Assert.False(settings.IsFirstGameRun);
    }

    [Fact]
    public void TheSessionKnobsAreStoredAsGiven()
    {
        Settings settings = Fresh();

        settings.SetGameDifficulty(GameDifficulty.Expert);
        Assert.Equal(GameDifficulty.Expert, settings.GameDifficulty);

        settings.SetGameLanguage("de");
        Assert.Equal("de", settings.GameLanguage);

        settings.SetMapSmoothScrolling(true);
        Assert.True(settings.IsMapSmoothScrollingEnabled);

        settings.SetMusicType(MusicSource.MidiOriginal);
        Assert.Equal(MusicSource.MidiOriginal, settings.MusicType);

        Mp2FileInfo map = new()
        {
            FilePath = "test.mp2",
            Name = "Test",
            Description = string.Empty,
            Width = 72,
            Height = 72,
            Difficulty = GameDifficulty.Normal,
        };

        settings.SetCurrentMapFileInfo(map);
        Assert.Same(map, settings.CurrentMapFileInfo);
    }

    [Fact]
    public void TheInterfaceCycleIsDynamicGoodEvil()
    {
        Settings settings = Fresh();

        Assert.Equal(InterfaceType.Good, settings.InterfaceType);

        settings.SwitchToNextInterfaceType();
        Assert.Equal(InterfaceType.Evil, settings.InterfaceType);

        settings.SwitchToNextInterfaceType();
        Assert.Equal(InterfaceType.Dynamic, settings.InterfaceType);

        settings.SwitchToNextInterfaceType();
        Assert.Equal(InterfaceType.Good, settings.InterfaceType);
    }

    [Fact]
    public void TheTurnOrderBarCycleIsOffTopBottom()
    {
        Settings settings = Fresh();

        Assert.Equal(BattleTurnOrderState.Off, settings.BattleTurnOrder);

        settings.SwitchToNextBattleTurnOrderState();
        Assert.Equal(BattleTurnOrderState.Top, settings.BattleTurnOrder);

        settings.SwitchToNextBattleTurnOrderState();
        Assert.Equal(BattleTurnOrderState.Bottom, settings.BattleTurnOrder);

        settings.SwitchToNextBattleTurnOrderState();
        Assert.Equal(BattleTurnOrderState.Off, settings.BattleTurnOrder);
    }

    [Fact]
    public void TheFixedInterfaceSetsAnswerDirectly()
    {
        Settings settings = Fresh();

        settings.SetInterfaceType(InterfaceType.Good);
        Assert.False(settings.IsEvilInterfaceEnabled);

        settings.SetInterfaceType(InterfaceType.Evil);
        Assert.True(settings.IsEvilInterfaceEnabled);
    }

    [Fact]
    public void TheDynamicInterfaceWithoutAPlayerIsTheGoodOne()
    {
        Settings settings = Fresh();

        settings.SetInterfaceType(InterfaceType.Dynamic);

        Assert.False(settings.IsEvilInterfaceEnabled);
    }

    [Fact]
    public void TheDynamicInterfaceFollowsTheRaceOfThePlayerWhoseTurnItIs()
    {
        Settings settings = Fresh();
        Players players = settings.Players;

        players.Init(PlayerColor.Blue | PlayerColor.Red);
        Players.SetPlayerRace(PlayerColor.Blue, MapRace.Knight);
        Players.SetPlayerRace(PlayerColor.Red, MapRace.Necromancer);
        Players.SetPlayerControl(PlayerColor.Blue, PlayerControl.Human);
        Players.SetPlayerControl(PlayerColor.Red, PlayerControl.Human);
        Players.SetPlayerInGame(PlayerColor.Blue, true);
        Players.SetPlayerInGame(PlayerColor.Red, true);

        settings.SetInterfaceType(InterfaceType.Dynamic);
        players.SetCurrentColor(PlayerColor.Blue);

        Assert.False(settings.IsEvilInterfaceEnabled);

        players.SetCurrentColor(PlayerColor.Red);

        Assert.True(settings.IsEvilInterfaceEnabled);

        players.SetCurrentColor(PlayerColor.None);

        Assert.False(settings.IsEvilInterfaceEnabled);
    }

    [Fact]
    public void APlayerThatLeftTheGameDoesNotChooseTheInterface()
    {
        Settings settings = Fresh();
        Players players = settings.Players;

        players.Init(PlayerColor.Blue);
        Players.SetPlayerRace(PlayerColor.Blue, MapRace.Warlock);
        Players.SetPlayerControl(PlayerColor.Blue, PlayerControl.Human);
        players.SetCurrentColor(PlayerColor.Blue);

        settings.SetInterfaceType(InterfaceType.Dynamic);
        Assert.False(settings.IsEvilInterfaceEnabled);

        Players.SetPlayerInGame(PlayerColor.Blue, true);
        Assert.True(settings.IsEvilInterfaceEnabled);

        Players.SetPlayerInGame(PlayerColor.Blue, false);
        Assert.False(settings.IsEvilInterfaceEnabled);
    }

    [Fact]
    public void DuringAnAiTurnTheLastHumanKeepsTheInterface()
    {
        Settings settings = Fresh();
        Players players = settings.Players;

        // Blue hands its turn to the computer and red is the human the screen should keep following, which is
        // the last human of the slot order — not the current, blue, player.
        players.Init(PlayerColor.Blue | PlayerColor.Red);
        Players.SetPlayerRace(PlayerColor.Blue, MapRace.Necromancer);
        Players.SetPlayerRace(PlayerColor.Red, MapRace.Knight);
        Players.SetPlayerInGame(PlayerColor.Blue, true);
        Players.SetPlayerInGame(PlayerColor.Red, true);
        Players.SetPlayerControl(PlayerColor.Blue, PlayerControl.Human);
        Players.SetPlayerControl(PlayerColor.Red, PlayerControl.Human);

        settings.SetInterfaceType(InterfaceType.Dynamic);
        players.SetCurrentColor(PlayerColor.Blue);

        Assert.True(settings.IsEvilInterfaceEnabled);

        Players.Get(PlayerColor.Blue)!.SetAiAutoControlMode(true);

        Assert.False(settings.IsEvilInterfaceEnabled);
    }

    [Fact]
    public void AConfigurationFileRoundTripsThroughSaveAndRead()
    {
        Settings settings = Fresh();

        settings.SetGameLanguage("ru");
        settings.SetMusicType(MusicSource.MidiOriginal);
        settings.SetSoundVolume(3);
        settings.SetMusicVolume(9);
        settings.SetHeroesMoveSpeed(4);
        settings.SetAiMoveSpeed(1);
        settings.SetBattleSpeed(7);
        settings.SetScrollSpeed(3);
        settings.SetMapSmoothScrolling(false);
        settings.SetBattleGrid(false);
        settings.SetBattleMovementShaded(false);
        settings.SetBattleMouseShaded(false);
        settings.SetBattleMovementAreaHighlight(false);
        settings.SetBattleShowDamage(true);
        settings.SetBattleShowHitPointsBar(false);
        settings.SetBattleAutoResolve(false);
        settings.SetBattleAutoSpellcast(true);
        settings.SetInterfaceType(InterfaceType.Evil);
        settings.SetHideInterface(true);
        settings.SetFullScreen(true);
        settings.SetVSync(false);
        settings.SetTextSupportMode(true);
        settings.SetMonochromeCursor(true);
        settings.Set3DAudio(true);
        settings.SetSystemInfo(true);
        settings.SetAutoSaveAtBeginningOfTurn(true);
        settings.SetCursorSoftwareEmulation(true);
        settings.SetScreenScalingTypeNearest(false);
        settings.SetNumericArmyEstimationView(true);
        settings.SetEditorAnimation(false);
        settings.SetEditorPassability(true);
        settings.SetPosRadar(new Point(5, 6));
        settings.SetPosButtons(new Point(7, 8));
        settings.SetPosIcons(new Point(9, 10));
        settings.SetPosStatus(new Point(11, 12));

        settings.SwitchToNextBattleTurnOrderState();
        settings.SwitchToNextBattleTurnOrderState();

        string path = Path.Combine(Path.GetTempPath(), $"fh2-cfg-{Guid.NewGuid():N}.cfg");

        try
        {
            Assert.True(settings.Save(path));

            Settings restored = Fresh();

            Assert.True(restored.Read(path));

            Assert.Equal("ru", restored.GameLanguage);
            Assert.Equal(MusicSource.MidiOriginal, restored.MusicType);
            Assert.Equal(3, restored.SoundVolume);
            Assert.Equal(9, restored.MusicVolume);
            Assert.Equal(4, restored.HeroesMoveSpeed);
            Assert.Equal(1, restored.AiMoveSpeed);
            Assert.Equal(7, restored.BattleSpeed);
            Assert.Equal(AdventureMapScrollSpeed.Fast, restored.ScrollSpeed);
            Assert.False(restored.IsMapSmoothScrollingEnabled);
            Assert.False(restored.IsBattleShowGrid);
            Assert.False(restored.IsBattleShowMoveShadow);
            Assert.False(restored.IsBattleShowMouseShadow);
            Assert.False(restored.IsBattleMovementAreaHighlight);
            Assert.True(restored.IsBattleShowDamage);
            Assert.False(restored.IsBattleShowHitPointsBar);
            Assert.False(restored.IsBattleAutoResolve);
            Assert.True(restored.IsBattleAutoSpellcast);
            Assert.Equal(BattleTurnOrderState.Bottom, restored.BattleTurnOrder);
            Assert.Equal(InterfaceType.Evil, restored.InterfaceType);
            Assert.True(restored.IsHideInterface);
            Assert.True(restored.IsFullScreen);
            Assert.False(restored.IsVSyncEnabled);
            Assert.True(restored.IsTextSupportMode);
            Assert.True(restored.IsMonochromeCursor);
            Assert.True(restored.Is3DAudioEnabled);
            Assert.True(restored.IsSystemInfo);
            Assert.True(restored.IsAutoSaveAtBeginningOfTurnEnabled);
            Assert.True(restored.IsCursorSoftwareEmulation);
            Assert.False(restored.IsScreenScalingTypeNearest);
            Assert.True(restored.IsNumericArmyEstimationView);
            Assert.False(restored.IsEditorAnimationEnabled);
            Assert.True(restored.IsEditorPassabilityEnabled);

            // 参考的 _controllerPointerSpeed 只有 getter，读配置文件是它唯一的写入点（settings.h:383-385），
            // 所以往返之后应回到文件里的值，这里是默认的 10。
            Assert.Equal(10, restored.ControllerPointerSpeed);
            Assert.Equal(new Point(5, 6), restored.PosRadar);
            Assert.Equal(new Point(7, 8), restored.PosButtons);
            Assert.Equal(new Point(9, 10), restored.PosIcons);
            Assert.Equal(new Point(11, 12), restored.PosStatus);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OnlyAnExplicitOffClearsTheFirstRunFlag()
    {
        Settings settings = Fresh();

        Assert.True(settings.IsFirstGameRun);

        string path = Path.Combine(Path.GetTempPath(), $"fh2-cfg-{Guid.NewGuid():N}.cfg");

        try
        {
            // 缺键时标志不动：参考只在读到 off 时才清（settings.cpp:307-310）。
            File.WriteAllText(path, "music = external\n");
            Assert.True(settings.Read(path));
            Assert.True(settings.IsFirstGameRun);

            File.WriteAllText(path, "first time game run = on\n");
            Assert.True(settings.Read(path));
            Assert.True(settings.IsFirstGameRun);

            File.WriteAllText(path, "first time game run = off\n");
            Assert.True(settings.Read(path));
            Assert.False(settings.IsFirstGameRun);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ASavedResetFirstGameRunStaysClearedInTheFile()
    {
        Settings settings = Fresh();
        string path = Path.Combine(Path.GetTempPath(), $"fh2-cfg-{Guid.NewGuid():N}.cfg");

        try
        {
            // 首运的收尾是 conf.resetFirstGameRun() 加 conf.Save()（game_mainmenu.cpp:300），
            // 这条路要能真正把旗标从文件里抹掉，否则每次启动都会再走一遍语言选择。
            Assert.True(settings.Save(path));
            Assert.Contains("first time game run = on", File.ReadAllText(path), StringComparison.Ordinal);

            settings.ResetFirstGameRun();

            Assert.True(settings.Save(path));
            Assert.Contains("first time game run = off", File.ReadAllText(path), StringComparison.Ordinal);

            Settings reloaded = Fresh();

            Assert.True(reloaded.Read(path));
            Assert.False(reloaded.IsFirstGameRun);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnUnreadableConfigurationFileLeavesTheSettingsAlone()
    {
        Settings settings = Fresh();

        settings.SetSoundVolume(3);

        Assert.False(settings.Read(Path.Combine(Path.GetTempPath(), $"fh2-cfg-{Guid.NewGuid():N}.cfg")));
        Assert.Equal(3, settings.SoundVolume);
    }

    [Fact]
    public void AVideoModeAndAWindowPositionAreReadFromTheFile()
    {
        Settings settings = Fresh();
        string path = Path.Combine(Path.GetTempPath(), $"fh2-cfg-{Guid.NewGuid():N}.cfg");

        try
        {
            // 参考把这两键读进 _resolutionInfo 与 _windowPos（settings.cpp:303-309），启动时由显示层应用；
            // 端口在 Read 里同样存下来，只是应用方是 GameHost.CreatePresenter()。
            File.WriteAllText(path, "videomode = 640x480:1920x1440\ngame window position = [ 128, 96 ]\n");

            Assert.True(settings.Read(path));

            Assert.Equal(new Size(640, 480), settings.CurrentResolutionInfo.GameSize);
            Assert.Equal(new Size(1920, 1440), settings.CurrentResolutionInfo.ScreenSize);
            Assert.Equal(new Point(128, 96), settings.SavedWindowPosition);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AVideoModeAndAWindowPositionAreWrittenWhenTheDisplayGivesThem()
    {
        Settings settings = Fresh();

        string text = settings.GetOptionsString(new ResolutionInfo(640, 480, 1280, 960), new Point(64, 32));

        Assert.Contains("videomode = 640x480:1280x960", text, StringComparison.Ordinal);
        Assert.Contains("game window position = [ 64, 32 ]", text, StringComparison.Ordinal);

        // 写出去的是窗口的现状（settings.cpp:427-433 问 Display::instance()），不改这里读进来的值。
        Assert.Equal(new Point(-1, -1), settings.SavedWindowPosition);
    }

    [Fact]
    public void TheOptionsStringQuotesTheEngineVersionAndEveryKey()
    {
        Settings settings = Fresh();

        string text = settings.GetOptionsString();

        Assert.Contains(EngineVersion.Version, text, StringComparison.Ordinal);
        Assert.Contains("music = external", text, StringComparison.Ordinal);
        Assert.Contains("sound volume = 6", text, StringComparison.Ordinal);
        Assert.Contains("first time game run = on", text, StringComparison.Ordinal);
        Assert.Contains("radar window position = [ -1, -1 ]", text, StringComparison.Ordinal);

        // 分辨率和窗口位置只有显示层给出时才写（读进来的值不是写出去的值），不给参数就整行不出现。
        Assert.DoesNotContain("videomode", text, StringComparison.Ordinal);
        Assert.DoesNotContain("game window position", text, StringComparison.Ordinal);
    }
}
