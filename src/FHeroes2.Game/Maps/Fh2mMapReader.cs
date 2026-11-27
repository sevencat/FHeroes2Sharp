using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Turns an <c>.fh2m</c> file back into the editor's working map, the read half of
/// <see cref="Fh2mMapWriter"/>.
/// </summary>
/// <remarks>
/// <para>
/// 头部与瓦片能完整搬回来，城堡表、可占领物表与信息块还不能——它们在 <c>.fh2m</c> 里是「瓦片上的对象 + 按 UID 的
/// 元数据」，要按对象组反推出 MP2 那三张独立的表，见 <c>a7b</c>。所以这里造出的地图可以编辑、可以再存成 MP2 的地形层，
/// 但还没有城堡与英雄。
/// </para>
/// <para>
/// <b>对象是反着查的</b>：写出时用 <see cref="MapObjectCatalog.TryGetObjectIndex"/> 把「ICN + 帧号」换成
/// 「组 + 下标」，读入时用 <see cref="MapObjectCatalog.GetObjectInfo"/> 取回对象、再拿它的第一个地面部件当瓦片上的
/// 图形。之所以取第一个地面部件，是因为格式里一个对象只记一个地址，而参考画出整组部件的起点就是主部件。
/// </para>
/// </remarks>
public static class Fh2mMapReader
{
    /// <summary>
    /// Builds the editor's map out of an <c>.fh2m</c> header, its payload included.
    /// </summary>
    /// <param name="header">The header that was read from a file.</param>
    /// <returns>The map, with its tiles and header filled in.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">
    /// <see cref="Fh2mBaseMap.Width"/> does not match the number of tiles, or the payload is unreadable.
    /// </exception>
    public static Mp2Map Build(Fh2mBaseMap header)
    {
        ArgumentNullException.ThrowIfNull(header);

        Fh2mPayload payload = Fh2mPayload.Read(header.Payload);
        int width = header.Width;

        if (width <= 0 || payload.Tiles.Count != width * width)
        {
            throw new InvalidDataException(
                $"The .fh2m file claims a {width}x{width} map but holds {payload.Tiles.Count} tiles.");
        }

        // 空白图只用来借一份 FileInfo 的默认值（版本、语言、时间戳等），尺寸与瓦片随即被覆盖。
        Mp2Map template = Mp2Map.CreateBlank(36, MapGrounds.GrassStartImageIndex);

        Mp2TileInfo[] tiles = new Mp2TileInfo[payload.Tiles.Count];
        List<Mp2CastlePosition> castles = [];
        List<Mp2CaptureObject> captures = [];
        List<Mp2InfoBlock> infoBlocks = [];

        for (int index = 0; index < tiles.Length; ++index)
        {
            Fh2mTile source = payload.Tiles[index];
            int x = index % width;
            int y = index / width;

            // 两张附加表与英雄块都从「瓦片上的对象」反推，所以与瓦片转换同一趟走完。
            castles.AddRange(ReadCastles(source, x, y));
            captures.AddRange(ReadCaptureObjects(source, x, y));

            Mp2TileInfo tile = ToTile(source);

            if (ReadHero(source, payload) is { } hero)
            {
                // 瓦片引用信息块靠那个 13 位的词：ObjectId = 下标 + 1，拆回去高 5 位进 Quantity1、低 8 位进 Quantity2。
                uint blockId = (uint)infoBlocks.Count + 1;

                infoBlocks.Add(new Mp2InfoBlock(infoBlocks.Count, hero.ToBlock()));

                tile = tile with { Quantity1 = (byte)(blockId << 3), Quantity2 = (byte)(blockId >> 5) };
            }
            else if (ReadTextBlock(source, payload) is { } text)
            {
                uint blockId = (uint)infoBlocks.Count + 1;

                infoBlocks.Add(new Mp2InfoBlock(infoBlocks.Count, text));

                tile = tile with { Quantity1 = (byte)(blockId << 3), Quantity2 = (byte)(blockId >> 5) };
            }

            tiles[index] = tile;
        }

        return new Mp2Map
        {
            Info = ToInfo(header, template.Info),
            Tiles = tiles,
            Addons = template.Addons,
            Castles = castles,
            CaptureObjects = captures,
            Heroes = [],
            ObeliskCount = 0,
            InfoBlocks = infoBlocks,
            LastObjectUid = payload.Tiles.SelectMany(tile => tile.Objects).DefaultIfEmpty(default)
                .Max(item => item.Id),
        };
    }

    /// <summary>
    /// Builds the hero information block of a tile, the <c>MP2_HEROES</c> branch of the block loop
    /// (<c>world_loadmap.cpp:579-596</c>).
    /// </summary>
    /// <remarks>
    /// 元数据表里有 <see cref="HeroMetadata"/> 的按 id 查；查不到就用一个空块——参考对一个没有块的英雄瓦片也只记一条
    /// 警告然后按默认值建英雄，端口照做，免得地图因为缺块而少一位英雄。
    /// </remarks>
    /// <param name="tile">The tile as the file stored it.</param>
    /// <param name="payload">The payload, for the hero metadata table.</param>
    /// <returns>The block, or <see langword="null"/> when the tile holds no hero.</returns>
    private static Mp2HeroInfo? ReadHero(Fh2mTile tile, Fh2mPayload payload)
    {
        foreach (Fh2mTileObject item in tile.Objects)
        {
            if ((ObjectGroup)item.Group != ObjectGroup.KingdomHeroes || ToPart(item) is not { } part
                || part.IcnType != Mp2ObjectIcnType.MiniHero)
            {
                continue;
            }

            _ = payload.Heroes.TryGetValue(item.Id, out HeroMetadata? metadata);

            return ToHeroInfo(metadata);
        }

        return null;
    }

    /// <summary>
    /// Builds the text information block of a tile — a sign, a bottle, a ground event or a sphinx — the other three
    /// branches of the block loop (<c>world_loadmap.cpp:624-650</c>).
    /// </summary>
    /// <remarks>
    /// 四类块的负载各有自己的布局，读端与写端在 <see cref="Mp2SignInfo"/>、<see cref="Mp2EventInfo"/>、
    /// <see cref="Mp2SphinxInfo"/> 里。这里只负责「这个瓦片是哪种对象」与「元数据表里有没有它」。
    /// </remarks>
    /// <param name="tile">The tile as the file stored it.</param>
    /// <param name="payload">The payload, for the three metadata tables.</param>
    /// <returns>The block payload, or <see langword="null"/> when the tile holds no such object.</returns>
    private static byte[]? ReadTextBlock(Fh2mTile tile, Fh2mPayload payload)
    {
        foreach (Fh2mTileObject item in tile.Objects)
        {
            ObjectGroup group = (ObjectGroup)item.Group;

            if (group >= ObjectGroup.GroupCount || item.Index >= MapObjectCatalog.GetObjectCount(group))
            {
                continue;
            }

            ObjectInfo info = MapObjectCatalog.GetObjectInfo(group, (int)item.Index);

            switch (Mp2ObjectTypes.NonActionType(info.ObjectType))
            {
                case Mp2ObjectType.Sign:
                case Mp2ObjectType.Bottle:
                    return new Mp2SignInfo
                    {
                        Message = payload.Signs.TryGetValue(item.Id, out SignMetadata? sign) ? sign.Message : string.Empty,
                    }.ToBlock();

                case Mp2ObjectType.Event:
                    return ToEventInfo(payload, item.Id).ToBlock();

                case Mp2ObjectType.Sphinx:
                    return ToSphinxInfo(payload, item.Id).ToBlock();

                default:
                    continue;
            }
        }

        return null;
    }

    /// <summary>Turns the event metadata of the file into the ground event block of the MP2 format.</summary>
    /// <param name="payload">The payload, for the table.</param>
    /// <param name="id">Object UID of the event.</param>
    /// <returns>The block.</returns>
    private static Mp2EventInfo ToEventInfo(Fh2mPayload payload, uint id)
    {
        if (!payload.Events.TryGetValue(id, out AdventureMapEventMetadata? metadata))
        {
            return new Mp2EventInfo();
        }

        // 参考的块里资源顺序是木、水银、矿、硫、水晶、宝石、金。
        int[] resources =
        [
            metadata.Resources.Wood,
            metadata.Resources.Mercury,
            metadata.Resources.Ore,
            metadata.Resources.Sulfur,
            metadata.Resources.Crystal,
            metadata.Resources.Gems,
            metadata.Resources.Gold,
        ];

        return new Mp2EventInfo
        {
            Resources = resources,
            ArtifactId = Math.Max(0, metadata.Artifact - 1),
            IsComputerAllowed = metadata.ComputerPlayerColors != 0,
            IsSingleTime = !metadata.IsRecurringEvent,
            Colors = (PlayerColor)metadata.HumanPlayerColors,
            Message = metadata.Message,
        };
    }

    /// <summary>Turns the sphinx metadata of the file into the sphinx block of the MP2 format.</summary>
    /// <param name="payload">The payload, for the table.</param>
    /// <param name="id">Object UID of the sphinx.</param>
    /// <returns>The block.</returns>
    private static Mp2SphinxInfo ToSphinxInfo(Fh2mPayload payload, uint id)
    {
        if (!payload.Sphinxes.TryGetValue(id, out SphinxMetadata? metadata))
        {
            return new Mp2SphinxInfo();
        }

        int[] resources =
        [
            metadata.Resources.Wood,
            metadata.Resources.Mercury,
            metadata.Resources.Ore,
            metadata.Resources.Sulfur,
            metadata.Resources.Crystal,
            metadata.Resources.Gems,
            metadata.Resources.Gold,
        ];

        return new Mp2SphinxInfo
        {
            Resources = resources,
            ArtifactId = Math.Max(0, metadata.Artifact - 1),
            Answers = metadata.Answers,
            Riddle = metadata.Riddle,
        };
    }

    /// <summary>
    /// Turns the hero metadata of the file into the hero block of the MP2 format.
    /// </summary>
    /// <param name="metadata">Its metadata, or <see langword="null"/> when the file had none.</param>
    /// <returns>The block, with everything the two formats share.</returns>
    private static Mp2HeroInfo ToHeroInfo(HeroMetadata? metadata)
    {
        if (metadata is null)
        {
            return new Mp2HeroInfo();
        }

        // 元数据里的怪物/神器/副技编号是从 1 起的引擎值，MP2 的块里是从 0 起的文件值。
        int[] monsters = new int[Mp2HeroInfo.TroopCount];
        int[] counts = new int[Mp2HeroInfo.TroopCount];

        for (int slot = 0; slot < Mp2HeroInfo.TroopCount; ++slot)
        {
            monsters[slot] = Math.Max(0, metadata.ArmyMonsterType[slot] - 1);
            counts[slot] = Math.Max(0, metadata.ArmyMonsterCount[slot]);
        }

        int[] artifacts = new int[Mp2HeroInfo.ArtifactCount];

        for (int slot = 0; slot < Mp2HeroInfo.ArtifactCount; ++slot)
        {
            artifacts[slot] = Math.Max(0, metadata.Artifact[slot] - 1);
        }

        int[] skills = new int[Mp2HeroInfo.SecondarySkillCount];
        int[] levels = new int[Mp2HeroInfo.SecondarySkillCount];

        for (int slot = 0; slot < Mp2HeroInfo.SecondarySkillCount; ++slot)
        {
            skills[slot] = Math.Max(0, metadata.SecondarySkill[slot] - 1);
            levels[slot] = metadata.SecondarySkillLevel[slot];
        }

        return new Mp2HeroInfo
        {
            HasCustomArmy = Array.Exists(monsters, monster => monster > 0),
            MonsterIds = monsters,
            MonsterCounts = counts,
            HasCustomPortrait = metadata.CustomPortrait > 0,
            PortraitId = Math.Max(0, metadata.CustomPortrait - 1),
            ArtifactIds = artifacts,
            Experience = (uint)Math.Max(0, metadata.CustomExperience),
            HasCustomSecondarySkills = Array.Exists(skills, skill => skill > 0),
            SecondarySkillIds = skills,
            SecondarySkillLevels = levels,
            HasCustomName = metadata.CustomName.Length > 0,
            Name = metadata.CustomName,
            IsOnPatrol = metadata.IsOnPatrol,
            PatrolDistanceOrJailRace = metadata.PatrolRadius,
        };
    }


    /// <summary>
    /// Rebuilds the castle position entry of a tile, when the tile is a town's entrance,
    /// the castle table of <c>World::LoadMapMP2()</c> (<c>world_loadmap.cpp:320-370</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 入口瓦片的 <c>OBJNTOWN</c> 帧号 = <c>13 + (要塞 ? 0 : 16) + 种族 × 32</c>（十六格城镇图里
    /// <c>(0,0)</c> 的序号是 13），所以反过来就是 <c>种族 = 帧号 / 32</c>、
    /// <c>要塞 = 帧号 % 32 &lt; 16</c>。文件里的 <c>RawType</c> 低三位是种族、最高位是要塞。
    /// </para>
    /// <para>
    /// 随机城镇（<c>OBJNTWRD</c>）的帧号里没有种族，只有「第几格 + 要塞位」，种族记为 <c>NONE</c>（6）——
    /// 与参考在这些瓦片上留 <c>Race::NONE</c> 一致。
    /// </para>
    /// </remarks>
    /// <param name="tile">The tile as the file stored it.</param>
    /// <param name="x">Column of the tile.</param>
    /// <param name="y">Row of the tile.</param>
    /// <returns>One entry for a town entrance, none otherwise.</returns>
    private static IEnumerable<Mp2CastlePosition> ReadCastles(Fh2mTile tile, int x, int y)
    {
        foreach (Fh2mTileObject item in tile.Objects)
        {
            if ((ObjectGroup)item.Group != ObjectGroup.KingdomTowns)
            {
                continue;
            }

            if (ToPart(item) is not { } part)
            {
                continue;
            }

            // 十六格城镇图里的第 13 格就是城门；有要塞时在下半组，即第 29 格。
            int local = (int)(part.IcnIndex % 32);

            if (local is not (EntranceTileIndex or (EntranceTileIndex + FortifiedTownTileCount)))
            {
                continue;
            }

            bool isRandomTown = part.IcnType == Mp2ObjectIcnType.ObjnTwrd;
            int raceIndex = isRandomTown ? CastleRaceNoneIndex : (int)(part.IcnIndex / 32);
            raceIndex = Math.Clamp(raceIndex, 0, CastleRaceNoneIndex);

            int rawType = raceIndex | (local >= FortifiedTownTileCount ? FortifiedCastleFlag : 0);

            yield return new Mp2CastlePosition(x, y, rawType, CastleRaceOf(rawType));
        }
    }

    /// <summary>Index of <see cref="MapRace.None"/> in the castle table's race field, <c>6</c>.</summary>
    private const int CastleRaceNoneIndex = 6;

    /// <summary>
    /// The race a castle table entry stands for, the <c>CastleRaces</c> table of the map format.
    /// </summary>
    /// <param name="rawType">The byte of the entry.</param>
    /// <returns>The race, <see cref="MapRace.None"/> for the seventh value.</returns>
    private static MapRace CastleRaceOf(int rawType) => (rawType & 0x7F) switch
    {
        0 => MapRace.Knight,
        1 => MapRace.Barbarian,
        2 => MapRace.Sorceress,
        3 => MapRace.Warlock,
        4 => MapRace.Wizard,
        5 => MapRace.Necromancer,
        _ => MapRace.None,
    };

    /// <summary>Tile number of a town's gate inside the sixteen tile group, <c>( 0, 0 )</c> of the layout.</summary>
    private const int EntranceTileIndex = 13;

    /// <summary>How many tiles one town sprite group holds, the <c>16</c> of the layout.</summary>
    private const int FortifiedTownTileCount = 16;

    /// <summary>High bit of the castle table that marks a built fort.</summary>
    private const int FortifiedCastleFlag = 0x80;

    /// <summary>
    /// Rebuilds the capturable object entries of a tile, the loop of <c>World::LoadMapMP2()</c>
    /// (<c>world_loadmap.cpp:382-430</c>).
    /// </summary>
    /// <param name="tile">The tile as the file stored it.</param>
    /// <param name="x">Column of the tile.</param>
    /// <param name="y">Row of the tile.</param>
    /// <returns>One entry per capturable object on the tile.</returns>
    private static IEnumerable<Mp2CaptureObject> ReadCaptureObjects(Fh2mTile tile, int x, int y)
    {
        foreach (Fh2mTileObject item in tile.Objects)
        {
            ObjectGroup group = (ObjectGroup)item.Group;

            if (group >= ObjectGroup.GroupCount || item.Index >= MapObjectCatalog.GetObjectCount(group))
            {
                continue;
            }

            ObjectInfo info = MapObjectCatalog.GetObjectInfo(group, (int)item.Index);

            if (CaptureTypeOf(info, out int rawType))
            {
                yield return new Mp2CaptureObject(x, y, rawType, Mp2ObjectTypes.NonActionType(info.ObjectType));
            }
        }
    }

    /// <summary>
    /// The byte the capture table uses for an object, <c>0x00</c> to <c>0x67</c> of
    /// <c>world_loadmap.cpp:389-425</c>.
    /// </summary>
    /// <param name="info">The object on the tile.</param>
    /// <param name="rawType">The byte, when the object is capturable.</param>
    /// <returns><see langword="true"/> when the object belongs in the table.</returns>
    private static bool CaptureTypeOf(ObjectInfo info, out int rawType)
    {
        rawType = 0;

        switch (Mp2ObjectTypes.NonActionType(info.ObjectType))
        {
            case Mp2ObjectType.Sawmill:
                rawType = 0x00;

                return true;

            case Mp2ObjectType.AlchemistLab:
                rawType = 0x01;

                return true;

            case Mp2ObjectType.Mine:
                // 矿的种类存在对象的 metadata[0] 里：Ore 0x02、Sulfur 0x03、Crystal 0x04、Gems 0x05、Gold 0x06。
                rawType = info.Metadata[0] switch
                {
                    (uint)Mp2ResourceType.Ore => 0x02,
                    (uint)Mp2ResourceType.Sulfur => 0x03,
                    (uint)Mp2ResourceType.Crystal => 0x04,
                    (uint)Mp2ResourceType.Gems => 0x05,
                    (uint)Mp2ResourceType.Gold => 0x06,
                    _ => 0x02,
                };

                return true;

            case Mp2ObjectType.Lighthouse:
                rawType = 0x64;

                return true;

            case Mp2ObjectType.DragonCity:
                rawType = 0x65;

                return true;

            case Mp2ObjectType.AbandonedMine:
                rawType = 0x67;

                return true;

            default:
                return false;
        }
    }

    /// <summary>Reads an <c>.fh2m</c> file into the editor's map.</summary>
    /// <param name="filePath">Path of the file.</param>
    /// <returns>The map.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The file is not a readable <c>.fh2m</c>.</exception>
    public static Mp2Map ReadFile(string filePath) => Build(Fh2mBaseMap.ReadFile(filePath));

    /// <summary>Turns one tile of the file into the tile structure of an MP2 map.</summary>
    /// <param name="tile">The tile as the file stored it.</param>
    /// <returns>The tile, with at most one ground and one top object.</returns>
    private static Mp2TileInfo ToTile(Fh2mTile tile)
    {
        Mp2TileInfo result = new()
        {
            TerrainImageIndex = tile.TerrainIndex,
            ObjectName1 = 0,
            BottomIcnImageIndex = 0,
            Quantity1 = 0,
            Quantity2 = 0,
            ObjectName2 = 0,
            TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            TerrainFlags = tile.TerrainFlags,
            ObjectType = Mp2ObjectType.None,
            NextAddonIndex = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };

        bool hasGround = false;
        bool hasTop = false;

        foreach (Fh2mTileObject item in tile.Objects)
        {
            if (hasGround && hasTop)
            {
                // MP2 的一格只有地面与顶层两层，第三个对象没有地方放。
                GameLog.Warn($"The .fh2m tile holds more than two objects; {item.Group}/{item.Index} is dropped.");

                break;
            }

            if (ToPart(item) is not { } part)
            {
                GameLog.Warn($"The .fh2m tile holds an object at {item.Group}/{item.Index}, which does not exist.");

                continue;
            }

            if (!hasGround)
            {
                hasGround = true;

                result = result with
                {
                    ObjectName1 = (byte)((int)part.IcnType << 2),
                    BottomIcnImageIndex = (byte)part.IcnIndex,
                    ObjectType = part.ObjectType,
                    Level1ObjectUid = item.Id,
                };
            }
            else
            {
                hasTop = true;

                result = result with
                {
                    ObjectName2 = (byte)((int)part.IcnType << 2),
                    TopIcnImageIndex = (byte)part.IcnIndex,
                    ObjectType = result.ObjectType == Mp2ObjectType.None ? part.ObjectType : result.ObjectType,
                    Level2ObjectUid = item.Id,
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Looks the object up in the catalogue and gives back its main part, the one the tile of an MP2 map stores.
    /// </summary>
    /// <param name="item">The address the file stored.</param>
    /// <returns>The main part, or <see langword="null"/> when the address does not describe an object.</returns>
    private static ObjectPartInfo? ToPart(Fh2mTileObject item)
    {
        ObjectGroup group = (ObjectGroup)item.Group;

        if (group >= ObjectGroup.GroupCount || item.Index >= MapObjectCatalog.GetObjectCount(group))
        {
            return null;
        }

        ObjectInfo info = MapObjectCatalog.GetObjectInfo(group, (int)item.Index);

        return info.GroundLevelParts.Count > 0 ? info.GroundLevelParts[0] : null;
    }

    /// <summary>Turns the header of the file into the header of the editor's map.</summary>
    /// <param name="header">The file header.</param>
    /// <param name="template">A header to take the fields the format does not carry from.</param>
    /// <returns>The map header.</returns>
    private static Mp2FileInfo ToInfo(Fh2mBaseMap header, Mp2FileInfo template) => template with
    {
        FilePath = string.Empty,
        Name = header.Name,
        Description = header.Description,
        CreatorNotes = header.CreatorNotes,
        Width = header.Width,
        Height = header.Width,
        Difficulty = header.Difficulty,
        KingdomColors = header.AvailablePlayerColors,
        HumanPlayers = header.HumanPlayerColors & header.AvailablePlayerColors,
        ComputerPlayers = header.ComputerPlayerColors & header.AvailablePlayerColors,
        PlayerRaces = header.PlayerRaces.Count > 0 ? [.. header.PlayerRaces] : template.PlayerRaces,
        VictoryCondition = Enum.IsDefined((MapVictoryCondition)header.VictoryConditionType)
            ? (MapVictoryCondition)header.VictoryConditionType
            : MapVictoryCondition.DefeatEveryone,
        VictoryConditionParameters = ToParameters(header.VictoryConditionMetadata),
        ComputerAlsoWins = header.IsVictoryConditionApplicableForAi,
        AllowNormalVictory = header.AllowNormalVictory,
        LossCondition = Enum.IsDefined((MapLossCondition)header.LossConditionType)
            ? (MapLossCondition)header.LossConditionType
            : MapLossCondition.Everything,
        LossConditionParameters = ToParameters(header.LossConditionMetadata),
        PlayerUnions = header.Alliances.Count > 0 ? [.. header.Alliances] : template.PlayerUnions,
        MainLanguage = (byte)header.MainLanguage,
    };

    /// <summary>Turns the unsigned numbers of the file into the signed parameters of an MP2 header.</summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The parameters, at least two of them as the header expects.</returns>
    private static int[] ToParameters(IReadOnlyList<uint> values)
    {
        if (values.Count == 0)
        {
            return [0, 0];
        }

        int[] parameters = new int[Math.Max(2, values.Count)];

        for (int index = 0; index < values.Count; ++index)
        {
            parameters[index] = (int)values[index];
        }

        return parameters;
    }
}
