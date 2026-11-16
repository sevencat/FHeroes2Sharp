using FHeroes2.Assets.Maps;
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
}
