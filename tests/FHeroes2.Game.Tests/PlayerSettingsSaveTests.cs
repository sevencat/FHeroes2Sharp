using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.System;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The plain-text blocks of a save that do not need the world: the player slots (<c>players.cpp:244-281</c>,
/// <c>551-599</c>), the camera (<c>players.cpp:205-242</c>) and the settings (<c>settings.cpp:1280-1310</c>).
/// </summary>
/// <remarks>
/// 这几块都按「写一遍、改一改、读回来」测。位宽是这里最容易错的地方——颜色在新格式是一个字节、老格式是四个字节，
/// 种族在任何格式都是四个字节——所以两条老格式的路径各有一条手写字节的测试，而不是只靠往返自证。
/// </remarks>
public sealed class PlayerSettingsSaveTests
{
    [Fact]
    public void APlayerSurvivesARoundTrip()
    {
        Player written = new(PlayerColor.Red)
        {
            Control = PlayerControl.Ai,
            Race = MapRace.Warlock,
            Friends = PlayerColor.Blue,
            Personality = AiPersonality.Builder,
        };

        written.SetName("Alice");
        written.SetHandicapStatus(Player.HandicapStatus.Severe);
        written.ResetModes(uint.MaxValue);
        written.SetModes(0x0F0F_0F0Fu);

        StreamBuf stream = new();
        stream.SetBigEndian(true);

        written.Write(stream);
        stream.Seek(0);

        Player read = new();

        read.Read(stream, SaveFileFormatInfo.Current);

        Assert.Equal(0x0F0F_0F0Fu, read.Modes);
        Assert.Equal(PlayerControl.Ai, read.Control);
        Assert.Equal(PlayerColor.Red, read.Color);
        Assert.Equal(MapRace.Warlock, read.Race);
        Assert.Equal(PlayerColor.Blue, read.Friends);
        Assert.Equal("Alice", read.CustomName);
        Assert.Equal(AiPersonality.Builder, read.Personality);
        Assert.Equal(Player.HandicapStatus.Severe, read.Handicap);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void APlayerOfAnOldFormatKeepsItsColourAsAnInt32()
    {
        // players.cpp:266-275：1.1.09 之前颜色、种族、盟友都是 int32_t。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(0x0000_0010u);
        stream.Write((int)PlayerControl.Human);
        stream.Write((int)PlayerColor.Green);
        stream.Write((int)MapRace.Sorceress);
        stream.Write((int)PlayerColor.Yellow);
        stream.Write("Bob");
        stream.Write((int)0);
        stream.Write(-1);
        stream.Write((int)AiPersonality.Explorer);
        stream.Write((byte)Player.HandicapStatus.Mild);

        stream.Seek(0);

        Player read = new();

        read.Read(stream, SaveFileFormat.Release1_1_08);

        Assert.Equal(0x10u, read.Modes);
        Assert.Equal(PlayerControl.Human, read.Control);
        Assert.Equal(PlayerColor.Green, read.Color);
        Assert.Equal(MapRace.Sorceress, read.Race);
        Assert.Equal(PlayerColor.Yellow, read.Friends);
        Assert.Equal("Bob", read.CustomName);
        Assert.Equal(AiPersonality.Explorer, read.Personality);
        Assert.Equal(Player.HandicapStatus.Mild, read.Handicap);
        Assert.Equal(FocusKind.None, read.Focus.Kind);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void APLayerOfABuildBefore1009SkipsOneField()
    {
        // players.cpp:258-262：多出来的那个整型字段读完就丢，后面的字段不能因此错位。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(0u);
        stream.Write(0xDEAD_BEEFu);
        stream.Write((int)PlayerControl.Ai);
        stream.Write((int)PlayerColor.Blue);
        stream.Write((int)MapRace.Barbarian);
        stream.Write((int)PlayerColor.Blue);
        stream.Write("Carl");
        stream.Write((int)FocusKind.None);
        stream.Write(-1);
        stream.Write((int)AiPersonality.Warrior);
        stream.Write((byte)Player.HandicapStatus.None);

        stream.Seek(0);

        Player read = new();

        read.Read(stream, SaveFileFormat.Release1_0_07);

        Assert.Equal(PlayerColor.Blue, read.Color);
        Assert.Equal(MapRace.Barbarian, read.Race);
        Assert.Equal("Carl", read.CustomName);
        Assert.Equal(0, stream.Size);
    }

    [Fact]
    public void TheSlotsSurviveARoundTrip()
    {
        try
        {
            TheSlotsSurviveARoundTripBody();
        }
        finally
        {
            ResetGlobalState();
        }
    }

    private static void TheSlotsSurviveARoundTripBody()
    {
        Players written = new();

        written.Init(PlayerColor.Red | PlayerColor.Blue);
        written.SetCurrentColor(PlayerColor.Blue);

        Player? red = null;
        Player? blue = null;

        foreach (Player player in written.All)
        {
            if (player.Color == PlayerColor.Red)
            {
                red = player;
            }
            else
            {
                blue = player;
            }
        }

        Assert.NotNull(red);
        Assert.NotNull(blue);

        red.SetName("Red One");
        blue.SetName("Blue Two");

        StreamBuf stream = new();
        stream.SetBigEndian(true);

        written.Write(stream);
        stream.Seek(0);

        Players read = new();

        read.Read(stream, SaveFileFormatInfo.Current);

        Assert.Equal(PlayerColor.Red | PlayerColor.Blue, read.GetColors());
        Assert.Equal(PlayerColor.Blue, read.CurrentColor);
        Assert.Equal(2, read.All.Count);
        Assert.Equal("Red One", Players.Get(PlayerColor.Red)?.CustomName);
        Assert.Equal("Blue Two", Players.Get(PlayerColor.Blue)?.CustomName);
        Assert.Equal(0, stream.Size);
    }

    /// <summary>
    /// Puts the singleton and the global slots back to the first-run state.
    /// </summary>
    /// <remarks>
    /// 语言与玩法都是全局的，测试改完不还原会让后面的字体、文本测试用上别的语言。
    /// </remarks>
    private static void ResetGlobalState()
    {
        Settings.Reset();
        Settings.Get().Players.Clear();
    }

    [Fact]
    public void TheSettingsSurviveARoundTrip()
    {
        try
        {
            Settings settings = Settings.Get();

            settings.SetGameLanguage("de");
            settings.SetGameDifficulty(GameDifficulty.Impossible);
            settings.SetGameType(GameTypes.HotSeat);
            settings.SetCurrentMapFileInfo(
                new Mp2FileInfo
                {
                    FilePath = "Island.mp2",
                    Name = "Island",
                    Description = "A map.",
                    Width = 72,
                    Height = 72,
                    Difficulty = GameDifficulty.Hard,
                    Version = MapVersion.PriceOfLoyalty,
                });

            settings.Players.Init(PlayerColor.Green);
            Players.Get(PlayerColor.Green)?.SetName("Greta");

            StreamBuf stream = new();
            stream.SetBigEndian(true);

            settings.Write(stream);
            stream.Seek(0);

            settings.SetGameLanguage("fr");
            settings.SetGameDifficulty(GameDifficulty.Easy);
            settings.SetGameType(GameTypes.Standard);
            settings.Players.Init(PlayerColor.Red);

            settings.Read(stream);

            Assert.Equal("de", settings.LoadedFileLanguage);
            Assert.Equal("fr", settings.GameLanguage);
            Assert.Equal(GameDifficulty.Impossible, settings.GameDifficulty);
            Assert.Equal(GameTypes.HotSeat, settings.GameType);
            Assert.NotNull(settings.CurrentMapFileInfo);
            Assert.Equal("Island", settings.CurrentMapFileInfo!.Name);
            Assert.Equal(72, settings.CurrentMapFileInfo.Width);
            Assert.Equal(MapVersion.PriceOfLoyalty, settings.CurrentMapFileInfo.Version);
            Assert.Equal(PlayerColor.Green, settings.Players.GetColors());
            Assert.Equal("Greta", Players.Get(PlayerColor.Green)?.CustomName);
            Assert.Equal(0, stream.Size);
        }
        finally
        {
            ResetGlobalState();
        }
    }

    [Fact]
    public void TheSettingsOfAnOldFormatSkipOneField()
    {
        // settings.cpp:1296-1299：1.1.01 之前那条整型字段不能把后面的玩法与玩家挤错位。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write("en");
        SaveFormatSamples.WriteMapInfo(
            stream,
            new Mp2FileInfo
            {
                FilePath = "Island.mp2",
                Name = "Island",
                Description = string.Empty,
                Width = 72,
                Height = 72,
                Difficulty = GameDifficulty.Normal,
                Version = MapVersion.SuccessionWars,
            },
            SaveFileFormat.PreRelease1_1_00);

        stream.Write((int)GameDifficulty.Normal);
        stream.Write((int)GameTypes.Campaign);
        stream.Write(0x5EED_5EED);
        stream.Write((int)0);
        stream.Write((int)0);

        stream.Seek(0);

        SaveFileFormat previous = GameSave.VersionOfCurrentSaveFile;

        try
        {
            GameSave.VersionOfCurrentSaveFile = SaveFileFormat.PreRelease1_1_00;

            Settings settings = Settings.Get();

            settings.Read(stream);

            Assert.Equal(GameTypes.Campaign, settings.GameType);
            Assert.Equal(GameDifficulty.Normal, settings.GameDifficulty);
            Assert.Equal(PlayerColor.None, settings.Players.GetColors());
            Assert.Equal(0, stream.Size);
        }
        finally
        {
            GameSave.VersionOfCurrentSaveFile = previous;
            ResetGlobalState();
        }
    }
}
