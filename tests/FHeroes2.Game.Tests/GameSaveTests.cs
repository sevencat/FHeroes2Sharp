using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Campaign;
using FHeroes2.Game.System;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// A save file end to end: the plain-text header (<c>game_io.cpp:64-103</c>, <c>:307-355</c>) and the compressed game
/// (<c>:116-164</c>, <c>:166-305</c>).
/// </summary>
/// <remarks>
/// 前半的用例只看明文段：地图信息、头部、魔数、版本、存档目录——读档界面画列表要的就是这些。后半把整份游戏写进临时
/// 文件再读回来：世界、设置、结局、战役进度与收尾魔数，压缩层夹在中间。这些用例都动 <see cref="Settings.Get"/>、
/// <see cref="World.Current"/> 和 <see cref="GameOverResult.Current"/> 这几个进程级单例，所以整类挂到
/// <see cref="SettingsSingletonCollection"/> 上串行跑。
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class GameSaveTests
{
    private const int MapSize = 36;

    /// <summary>Tile the hero of every round-trip test stands on; grass and far from the edges.</summary>
    private const int HeroTileIndex = 37;

    private const int HeroColumn = HeroTileIndex % MapSize;

    private const int HeroRow = HeroTileIndex / MapSize;

    [Fact]
    public void TheMapInformationSurvivesARoundTrip()
    {
        Mp2FileInfo written = Map();

        StreamBuf stream = new();
        stream.SetBigEndian(true);

        SaveFileInfo.Write(stream, written);
        stream.Seek(0);

        Mp2FileInfo read = SaveFileInfo.Read(stream, SaveFileFormatInfo.Current);

        Assert.False(stream.Fail);
        Assert.Equal(0, stream.Size);
        Assert.Equal("Island.sav", read.FilePath);
        Assert.Equal("Island", read.Name);
        Assert.Equal("Two players, one ship.", read.Description);
        Assert.Equal(72, read.Width);
        Assert.Equal(72, read.Height);
        Assert.Equal(GameDifficulty.Hard, read.Difficulty);
        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, read.KingdomColors);
        Assert.Equal(PlayerColor.Blue, read.HumanPlayers);
        Assert.Equal(PlayerColor.Red, read.ComputerPlayers);
        Assert.Equal(PlayerColor.Green, read.RandomRacePlayers);
        Assert.Equal(MapRace.Knight, read.PlayerRaces[0]);
        Assert.Equal(MapRace.Warlock, read.PlayerRaces[1]);
        Assert.Equal(PlayerColor.Blue, read.PlayerUnions[0]);
        Assert.Equal(PlayerColor.Blue, read.PlayerUnions[1]);
        Assert.Equal(MapVictoryCondition.DefeatEveryone, read.VictoryCondition);
        Assert.True(read.ComputerAlsoWins);
        Assert.False(read.AllowNormalVictory);
        Assert.Equal([3, 7], read.VictoryConditionParameters);
        Assert.Equal(MapLossCondition.LoseTown, read.LossCondition);
        Assert.Equal([5, 0], read.LossConditionParameters);
        Assert.Equal(1_700_000_000u, read.Timestamp);
        Assert.True(read.StartWithHeroInFirstCastle);
        Assert.Equal(MapVersion.PriceOfLoyalty, read.Version);
        Assert.Equal(3u, read.WorldDay);
        Assert.Equal(2u, read.WorldWeek);
        Assert.Equal(4u, read.WorldMonth);
        Assert.Equal(1, read.MainLanguage);
        Assert.Equal("Made with the map editor.", read.CreatorNotes);
    }

    [Fact]
    public void TheFilePathIsWrittenWithoutItsDirectory()
    {
        // maps_fileinfo.cpp:666：只写文件名，路径由读档时所在机器的设置决定。
        Mp2FileInfo written = Map() with { FilePath = Path.Combine("C:", "maps", "Island.sav") };

        StreamBuf stream = new();
        stream.SetBigEndian(true);

        SaveFileInfo.Write(stream, written);
        stream.Seek(0);

        Assert.Equal("Island.sav", SaveFileInfo.Read(stream, SaveFileFormatInfo.Current).FilePath);
    }

    [Fact]
    public void AnOlderFileHasNoLanguageAndNoNotes()
    {
        // 两个尾巴各有版本门：1.1.03 之前没有 mainLanguage，1.1.15 之前没有 creatorNotes（maps_fileinfo.cpp:711-725）。
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        SaveFileInfo.Write(stream, Map());

        // 把两个尾巴剪掉，模拟老版本写出来的块。
        stream.Seek(0);

        Mp2FileInfo full = SaveFileInfo.Read(stream, SaveFileFormatInfo.Current);
        StreamBuf older = new();
        older.SetBigEndian(true);
        SaveFormatSamples.WriteMapInfo(older, full, SaveFileFormat.Release1_0_05);
        older.Seek(0);

        Mp2FileInfo read = SaveFileInfo.Read(older, SaveFileFormat.Release1_0_05);

        Assert.Equal(0, read.MainLanguage);
        Assert.Equal(string.Empty, read.CreatorNotes);
        Assert.Equal(full.Name, read.Name);
        Assert.Equal(full.WorldDay, read.WorldDay);
    }

    [Fact]
    public void TheHeaderCarriesTheRequirementsAndTheGameType()
    {
        GameSave.SaveFileHeader header = GameSave.SaveFileHeader.Create(
            Map() with { Version = MapVersion.PriceOfLoyalty },
            GameTypes.Standard,
            worldDay: 3,
            worldWeek: 2,
            worldMonth: 4);

        StreamBuf stream = new();
        stream.SetBigEndian(true);

        header.Write(stream);
        stream.Seek(0);

        GameSave.SaveFileHeader read = GameSave.SaveFileHeader.Read(stream);

        Assert.Equal(GameSave.SaveFileHeader.RequiresPolResources, read.Requirements);
        Assert.Equal(GameTypes.Standard, read.GameType);
        Assert.Equal(3u, read.Info.WorldDay);
        Assert.True(read.Info.Timestamp > 0);
    }

    [Fact]
    public void ASaveOfTheOriginalCampaignNeedsNoPolResources()
    {
        GameSave.SaveFileHeader header = GameSave.SaveFileHeader.Create(
            Map() with { Version = MapVersion.SuccessionWars },
            GameTypes.Campaign,
            worldDay: 1,
            worldWeek: 1,
            worldMonth: 1);

        Assert.Equal(0, header.Requirements);
    }

    [Fact]
    public void TheSaveDirectorySitsUnderTheUserDataDirectory()
    {
        string? previous = GameDataDirectory.OverrideRoot;
        string root = Path.Combine(Path.GetTempPath(), "fh2-save-dir");

        try
        {
            GameDataDirectory.OverrideRoot = root;

            Assert.Equal(Path.Combine(root, "files", "save"), GameSave.GetSaveDir());
        }
        finally
        {
            GameDataDirectory.OverrideRoot = previous;
        }
    }

    [Fact]
    public void TheInterScenarioSaveIsNamedAfterTheMap()
    {
        // game_io.cpp:419-422：文件名是「地图名 + _Complete + 扩展名」，写进存档目录。
        StartGame(gameType: GameTypes.Campaign);

        string? previous = GameDataDirectory.OverrideRoot;
        string root = Path.Combine(Path.GetTempPath(), $"fh2-complete-{Guid.NewGuid():N}");

        try
        {
            GameDataDirectory.OverrideRoot = root;
            CampaignSaveData.Get().Reset();

            Assert.True(GameSave.SaveCompletedCampaignScenario());

            string[] written = Directory.GetFiles(GameSave.GetSaveDir(), "*_Complete" + GameSave.GetSaveFileExtension());

            Assert.Single(written);
            Assert.Equal("Island_Complete" + GameSave.GetSaveFileExtension(), Path.GetFileName(written[0]));
        }
        finally
        {
            GameDataDirectory.OverrideRoot = previous;

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void TheInterScenarioSaveHoldsTheScenarioThatWasJustFinished()
    {
        // 参考在「记完成、还没挪到下一关」的那一刻写档（game_campaign.cpp:1165-1172），存档里因此
        // CurrentScenarioInfoId == LastCompletedScenarioInfoId；读档靠这个判据认出「这一关打完了」
        // （game_io.cpp:261-265）。顺序写反了这条用例就会红。
        StartGame(gameType: GameTypes.Campaign);

        string? previous = GameDataDirectory.OverrideRoot;
        string root = Path.Combine(Path.GetTempPath(), $"fh2-complete-{Guid.NewGuid():N}");
        CampaignSaveData campaign = CampaignSaveData.Get();

        try
        {
            GameDataDirectory.OverrideRoot = root;
            campaign.Reset();
            campaign.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));

            Assert.True(CampaignProgress.RecordFinishedScenario(World.Current));
            Assert.Equal(campaign.CurrentScenarioInfoId, campaign.GetLastCompletedScenarioInfoId());

            Assert.True(GameSave.SaveCompletedCampaignScenario());

            string path = Assert.Single(Directory.GetFiles(GameSave.GetSaveDir(), "*_Complete" + GameSave.GetSaveFileExtension()));

            // 清空之后读回来，读到的一定是文件里的那份。
            campaign.Reset();
            World.Current = new World();
            Settings.Reset();
            Settings.Get().SetGameType(GameTypes.Campaign);

            Assert.True(GameSave.Load(path));
            Assert.False(campaign.IsStarting);
            Assert.Equal(campaign.CurrentScenarioInfoId, campaign.GetLastCompletedScenarioInfoId());
        }
        finally
        {
            campaign.Reset();
            GameDataDirectory.OverrideRoot = previous;

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void TheUserDataDirectoryEndsWithTheApplicationName()
    {
        string? previous = GameDataDirectory.OverrideRoot;

        try
        {
            GameDataDirectory.OverrideRoot = null;

            Assert.EndsWith("fheroes2", GameDataDirectory.Get("fheroes2"));
        }
        finally
        {
            GameDataDirectory.OverrideRoot = previous;
        }
    }

    [Fact]
    public void TheHeaderOfAFileReadBackNamesTheFileItCameFrom()
    {
        string path = WriteSaveFile(FileBytes(Map()));

        try
        {
            Settings.Get().SetGameType(GameTypes.Standard);

            Assert.True(GameSave.LoadSav2FileInfo(path, out Mp2FileInfo info));
            Assert.Equal(path, info.FilePath);
            Assert.Equal("Island", info.Name);
            Assert.Equal(SaveFileFormatInfo.Current, GameSave.VersionOfCurrentSaveFile);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AFileOfAnotherGameTypeIsRejected()
    {
        string path = WriteSaveFile(FileBytes(Map()));

        try
        {
            Settings.Get().SetGameType(GameTypes.Campaign);

            Assert.False(GameSave.LoadSav2FileInfo(path, out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AFileWithTheWrongMagicIsRejected()
    {
        byte[] bytes = FileBytes(Map());
        bytes[0] = 0x00;

        string path = WriteSaveFile(bytes);

        try
        {
            Settings.Get().SetGameType(GameTypes.Standard);

            Assert.False(GameSave.LoadSav2FileInfo(path, out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AVersionFromTheFutureIsRejected()
    {
        byte[] bytes = FileBytes(Map(), (SaveFileFormat)((ushort)SaveFileFormatInfo.Current + 1));

        string path = WriteSaveFile(bytes);

        try
        {
            Settings.Get().SetGameType(GameTypes.Standard);

            Assert.False(GameSave.LoadSav2FileInfo(path, out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheWholeGameSurvivesASaveAndALoad()
    {
        World world = StartGame();
        world.NewDay();
        world.NewDay();
        Assert.True(world.ClearFog(PlayerColor.Blue) > 0);

        string path = TempSavePath();

        try
        {
            Assert.True(GameSave.Save(path));
            Assert.Equal(path, GameSave.LastSaveName);

            // 读档换掉的是整份世界：先把现场弄得面目全非，读回来还是存档里的那个。
            World.Current = new World();
            World.Current.LoadMap(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));
            Settings.Reset();
            Settings.Get().SetGameType(GameTypes.Standard);
            GameOverResult.Current = new GameOverResult();

            Assert.True(GameSave.Load(path));

            World restored = World.Current;

            Assert.Equal(MapSize, restored.Width);
            Assert.Equal(MapSize, restored.Height);
            Assert.Equal(MapSize * MapSize, restored.Tiles.Count);
            Assert.Equal(world.DayCount, restored.DayCount);
            Assert.Equal(world.WeekCount, restored.WeekCount);
            Assert.Equal(HeroTileIndex, restored.Heroes.GetAtMapIndex(HeroTileIndex)?.MapIndex);

            // 存档先写世界后写设置，世界读的时候还没有地图头；读档把设置里那份交回来（game_io.cpp:275 附近）。
            Assert.Equal("Island", restored.MapInfo?.Name);

            // 设置里那五样：语言、地图、难度、玩法、玩家表。
            Settings loaded = Settings.Get();
            Assert.Equal(GameTypes.Standard, loaded.GameType);
            Assert.Equal(GameDifficulty.Hard, loaded.GameDifficulty);
            Assert.Equal("Island", loaded.CurrentMapFileInfo?.Name);
            Assert.Equal(PlayerColor.Blue | PlayerColor.Red, loaded.Players.GetColors());

            Assert.Equal(PlayerColor.Blue | PlayerColor.Red, GameOverResult.Current.Colors);
            Assert.Equal(GameOver.ConditionNone, GameOverResult.Current.Condition);

            // 迷雾只存颜色，方向掩码是派生数据，参考读档后按玩家重算（game_startgame.cpp:863）。
            restored.Fog!.UpdateDirections(PlayerColor.Blue);
            Assert.False(restored.Fog.IsFogged(HeroColumn, HeroRow, PlayerColor.Blue));
            Assert.True(restored.Fog.IsFogged(HeroColumn, HeroRow, PlayerColor.Red));
            Assert.Equal(world.Fog!.CountFogged(), restored.Fog.CountFogged());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnAutomaticSaveKeepsTheLastSaveName()
    {
        StartGame();
        GameSave.LastSaveName = "previous.sav";

        string path = TempSavePath();

        try
        {
            Assert.True(GameSave.Save(path, autoSave: true));
            Assert.Equal("previous.sav", GameSave.LastSaveName);

            // 快速存档走的是另一边：它会成为「继续游戏」的默认位置。
            Assert.True(GameSave.Save(path));
            Assert.Equal(path, GameSave.LastSaveName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheAutomaticSlotGoesUnderTheSaveDirectory()
    {
        string? previousRoot = GameDataDirectory.OverrideRoot;
        string root = Path.Combine(Path.GetTempPath(), $"fh2-autosave-{Guid.NewGuid():N}");

        try
        {
            GameDataDirectory.OverrideRoot = root;
            StartGame();
            GameSave.LastSaveName = "previous.sav";

            // 干净安装上 files/save 还不存在：参考在启动时建好（game_init.cpp:205-209），端口在写盘前建。
            Assert.True(GameSave.AutoSave());
            Assert.True(File.Exists(Path.Combine(root, "files", "save", GameSave.AutoSaveName + ".sav")));

            // 自动存档不是玩家选的那一档，不能抢走「继续游戏」的位置。
            Assert.Equal("previous.sav", GameSave.LastSaveName);

            // 快速存档是那一档（game_io.cpp:111-114 传的是 false）。
            Assert.True(GameSave.QuickSave());
            Assert.True(File.Exists(Path.Combine(root, "files", "save", GameSave.QuickSaveName + ".sav")));
            Assert.Equal(
                Path.Combine(GameSave.GetSaveDir(), GameSave.QuickSaveName + ".sav"),
                GameSave.LastSaveName);
        }
        finally
        {
            GameDataDirectory.OverrideRoot = previousRoot;

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ASaveWithoutAGameIsRefusedAndWritesNoFile()
    {
        Settings.Reset();
        World.Current = new World();
        GameSave.LastSaveName = "previous.sav";

        string path = TempSavePath();

        try
        {
            Assert.False(GameSave.Save(path));
            Assert.False(File.Exists(path), "A refused save must not leave an empty file behind.");
            Assert.Equal("previous.sav", GameSave.LastSaveName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ATruncatedSaveLeavesTheRunningGameAlone()
    {
        StartGame();

        string path = TempSavePath();

        try
        {
            Assert.True(GameSave.Save(path));

            byte[] bytes = File.ReadAllBytes(path);
            File.WriteAllBytes(path, bytes[..(bytes.Length / 2)]);

            World.Current = new World();
            World.Current.LoadMap(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

            Assert.False(GameSave.Load(path));

            // 压缩块解不开就走不到 World.ReadFrom：正在玩的那局不能被半份存档顶掉。
            Assert.Equal(MapSize, World.Current.Width);
            Assert.Equal(MapSize * MapSize, World.Current.Tiles.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ASaveWithoutTheEndMarkerIsRejected()
    {
        World world = StartGame();

        string marked = WriteSaveFile(SaveBytes(world));
        string unmarked = WriteSaveFile(SaveBytes(world, endMarker: false));

        try
        {
            // 两份只差最后两个字节：有魔数的能读，没魔数的被当成坏档（game_io.cpp:153-157、:267-272）。
            Assert.True(GameSave.Load(marked));
            Assert.False(GameSave.Load(unmarked));
        }
        finally
        {
            File.Delete(marked);
            File.Delete(unmarked);
        }
    }

    [Fact]
    public void AFileThatNeedsTheExpansionIsRejectedWhenItIsNotInstalled()
    {
        StartGame(MapVersion.PriceOfLoyalty);

        string path = TempSavePath();

        try
        {
            Assert.True(GameSave.Save(path));

            Settings.Reset();
            Settings settings = Settings.Get();
            settings.SetGameType(GameTypes.Standard);

            Assert.False(settings.IsPriceOfLoyaltySupported);
            Assert.False(GameSave.Load(path));

            settings.EnablePriceOfLoyaltySupport(true);

            Assert.True(GameSave.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ACampaignSaveCarriesTheProgressBetweenScenarios()
    {
        StartGame(gameType: GameTypes.Campaign);

        ScenarioInfoId scenario = new(CampaignId.Roland, 2);
        CampaignSaveData campaign = CampaignSaveData.Get();

        campaign.Reset();
        campaign.SetDifficulty(CampaignDifficulty.Increased, resetMinDifficulty: true);
        campaign.SetCurrentScenarioInfo(scenario, bonusId: 1);
        campaign.AddCurrentMapToFinished();
        campaign.AddDaysPassed(9);

        string path = TempSavePath();

        try
        {
            Assert.True(GameSave.Save(path));

            // 战役进度是进程级单例：清空之后再读，读到的一定是文件里的那份。
            campaign.Reset();
            World.Current = new World();
            Settings.Reset();
            Settings.Get().SetGameType(GameTypes.Campaign);

            Assert.True(GameSave.Load(path));

            Assert.Equal(scenario, campaign.CurrentScenarioInfoId);
            Assert.Equal(1, campaign.CurrentScenarioBonusId);
            Assert.Equal([scenario], campaign.FinishedMaps);
            Assert.Equal(9u, campaign.DaysPassed);
            Assert.Equal(CampaignDifficulty.Increased, campaign.Difficulty);
            Assert.False(campaign.IsStarting);
        }
        finally
        {
            campaign.Reset();
            File.Delete(path);
        }
    }

    /// <summary>
    /// Starts a game the way the reference does: settings for a map, the player slots of its header, the world, and a
    /// fresh verdict. Everything here is a process-wide singleton, so the caller's test class owns it.
    /// </summary>
    private static World StartGame(MapVersion version = MapVersion.SuccessionWars, GameTypes gameType = GameTypes.Standard)
    {
        Settings.Reset();

        Settings settings = Settings.Get();
        settings.SetGameType(gameType);
        settings.GameDifficulty = GameDifficulty.Hard;

        Mp2Map map = MapWithHero(Map() with { Version = version, Width = MapSize, Height = MapSize });
        settings.SetCurrentMapFileInfo(map.Info);
        settings.Players.Init(map.Info);

        World world = new();
        world.LoadMap(map);
        World.Current = world;

        GameOverResult.Current = new GameOverResult();
        GameOverResult.Current.Reset();

        return world;
    }

    /// <summary>Puts the single hero of the round-trip fixture onto a blank map of <see cref="MapSize"/>.</summary>
    private static Mp2Map MapWithHero(Mp2FileInfo info)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(HeroTileIndex, HeroTile());

        byte[] block = new byte[Mp2HeroInfo.StructureSize];
        block[17] = 1;

        return map with { Info = info, InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    /// <summary>The tile a blue hero stands on, the same encoding <see cref="WorldSaveTests"/> uses.</summary>
    private static Mp2TileInfo HeroTile() => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
        BottomIcnImageIndex = 0,
        Quantity1 = (1 << 3) | 1,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.Hero,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>
    /// Builds a save file the way <see cref="GameSave.Save"/> does, with the two knobs the negative tests need: the
    /// campaign block and the end-of-data marker.
    /// </summary>
    private static byte[] SaveBytes(World world, bool endMarker = true)
    {
        Settings settings = Settings.Get();

        StreamBuf file = new();
        file.SetBigEndian(true);

        file.Write(GameSave.SaveFileMagicNumber);
        file.Write(((ushort)SaveFileFormatInfo.Current).ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        file.Write((ushort)SaveFileFormatInfo.Current);
        GameSave.SaveFileHeader.Create(
            settings.CurrentMapFileInfo!,
            settings.GameType,
            world.Day,
            world.Week,
            world.Month).Write(file);

        StreamBuf data = new();
        data.SetBigEndian(true);

        world.WriteTo(data);
        settings.Write(data);
        GameOverResult.Current.WriteTo(data);

        if (settings.IsCampaignGameType)
        {
            CampaignSaveData.Get().Write(data);
        }

        if (endMarker)
        {
            data.Write(GameSave.SaveFileMagicNumber);
        }

        Assert.True(Compression.ZipStreamBuf(data, file));

        return file.Written.ToArray();
    }

    private static string TempSavePath() =>
        Path.Combine(Path.GetTempPath(), $"fh2-{Guid.NewGuid():N}.sav");

    private static byte[] FileBytes(Mp2FileInfo info, SaveFileFormat? version = null)
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);

        stream.Write(GameSave.SaveFileMagicNumber);
        stream.Write("SV2");
        stream.Write((ushort)(version ?? SaveFileFormatInfo.Current));
        new GameSave.SaveFileHeader((ushort)0, info, GameTypes.Standard).Write(stream);

        return stream.Written.ToArray();
    }

    private static string WriteSaveFile(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"fh2-{Guid.NewGuid():N}.sav");

        File.WriteAllBytes(path, bytes);

        return path;
    }

    private static Mp2FileInfo Map() =>
        new()
        {
            FilePath = "Island.sav",
            Name = "Island",
            Description = "Two players, one ship.",
            Width = 72,
            Height = 72,
            Difficulty = GameDifficulty.Hard,
            KingdomColors = PlayerColor.Blue | PlayerColor.Red,
            HumanPlayers = PlayerColor.Blue,
            ComputerPlayers = PlayerColor.Red,
            RandomRacePlayers = PlayerColor.Green,
            PlayerRaces = [MapRace.Knight, MapRace.Warlock, MapRace.None, MapRace.None, MapRace.None, MapRace.None],
            PlayerUnions = [PlayerColor.Blue, PlayerColor.Blue, PlayerColor.None, PlayerColor.None, PlayerColor.None, PlayerColor.None],
            VictoryCondition = MapVictoryCondition.DefeatEveryone,
            ComputerAlsoWins = true,
            AllowNormalVictory = false,
            VictoryConditionParameters = [3, 7],
            LossCondition = MapLossCondition.LoseTown,
            LossConditionParameters = [5, 0],
            Timestamp = 1_700_000_000,
            StartWithHeroInFirstCastle = true,
            Version = MapVersion.PriceOfLoyalty,
            WorldDay = 3,
            WorldWeek = 2,
            WorldMonth = 4,
            MainLanguage = 1,
            CreatorNotes = "Made with the map editor.",
        };
}
