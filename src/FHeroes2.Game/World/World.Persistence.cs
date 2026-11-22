using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game;

/// <summary>
/// The whole world as a save file, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>World</c>
/// (<c>world.cpp:1448-1515</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的一行链式写法展开成同样顺序的十几步：宽高、瓦片、英雄表、城堡表、王国表、谣言、定时事件、占领表、
/// 终极宝物、日/周/月、胜负条件里的两个英雄 id、地图数据对象、种子。每一步的字节布局都在它自己的
/// <c>*.Persistence.cs</c> 里，这里只管顺序、版本门和读完之后的那一次 <see cref="PostLoad"/>。
/// </para>
/// <para>
/// <b>版本门三道</b>：1.0.10 之前宽高是 <c>uint16</c> 且胜负条件里的 id 差一（<c>world.cpp:1456-1476</c>）；
/// 1.1.06 之前要按占领表重画矿井与灯塔的旗子（<c>world.cpp:1478-1508</c>）。前两道的顺序不能换：id 的
/// <c>++</c> 在读完月之后、读地图对象之前，和参考一样。
/// </para>
/// <para>
/// <b>迷雾要两头搬</b>：参考把迷雾存在瓦片里（<see cref="Tile.FogColors"/> 与
/// <see cref="Tile.FogDirection"/>），端口把它做成了一张独立的表 <see cref="Fog"/>，游戏里更新的是那张表。
/// 写盘前把表落到瓦片上，读盘后把瓦片搬回表里，否则存下来的永远是「全黑」而读回来的世界也全是黑的。
/// </para>
/// <para>
/// <b>读档会换掉整张地图</b>：瓦片、英雄、城堡、占领表、数据对象都是清空重填，和参考读进已有容器一个意思——
/// 在一个已经载入过地图的世界里读存档，得到的就是存档里的那张图，不会和第二张图混在一起。
/// </para>
/// </remarks>
public sealed partial class World
{
    /// <summary>
    /// Writes the world, <c>world.cpp:1448</c>.
    /// </summary>
    /// <remarks>
    /// 参考写的是它的成员，不管有没有地图；端口的世界没有地图时连几何都造不出来（宽高为 0），
    /// 所以这种情况按调用错误处理，而不是写一份全是 0 的文件。
    /// </remarks>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The world holds no map.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (Width <= 0 || Height <= 0)
        {
            throw new InvalidOperationException("A world without a map has nothing to save.");
        }

        MapGeometry geometry = new(Width, Height);

        // 参考的迷雾就在瓦片里，写之前先把端口的迷雾表搬过去（world.cpp:1450 写的就是瓦片）。
        StoreFogIntoTiles(geometry);

        stream.Write(Width);
        stream.Write(Height);
        stream.WriteList(_tiles, static (target, tile) => tile.WriteTo(target));
        _heroes.WriteTo(stream, geometry);
        _castles.WriteTo(stream, geometry);
        _kingdoms.WriteTo(stream);
        stream.WriteList(_rumors, static (target, rumor) => target.Write(rumor));
        stream.WriteList(_dailyEvents, static (target, eventDate) => eventDate.WriteTo(target));
        _capturedObjects.WriteTo(stream);
        _ultimateArtifact.WriteTo(stream);
        stream.Write(_day);
        stream.Write(_week);
        stream.Write(_month);
        stream.Write(_heroIdAsWinCondition);
        stream.Write(_heroIdAsLossCondition);
        _mapObjects.WriteTo(stream, geometry);
        stream.Write(_seed);
    }

    /// <summary>
    /// Reads the world, <c>world.cpp:1454</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 读完之后按参考调 <c>PostLoad( false, true )</c>：可通行方向已经在瓦片里，不再重算；UID 计数器抬到地图上的
    /// 最大 UID（<c>world.cpp:1512</c>）。
    /// </para>
    /// <para>
    /// 参考在 1.0.10 之前把胜负条件里的英雄 id <c>++</c>，对「没有这个英雄」（旧编号里是 72）不做特判；端口的
    /// <see cref="Hero"/> 自己读 id 时是特判了的（见 <c>Hero.Persistence.cs</c>），这里保持参考的写法，因为存档里
    /// 的这个字段就是照它写的。
    /// </para>
    /// <para>
    /// 失败时世界被恢复到「没有地图」：尺寸清零、瓦片清空、迷雾丢掉。参考不管这些（它的失败标志由上层负责），
    /// 端口自己收拾一下，免得一个半读的世界被当成可用的地图。
    /// </para>
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream, SaveFileFormat version)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (version < SaveFileFormat.Release1_0_10)
        {
            Width = stream.ReadUInt16();
            Height = stream.ReadUInt16();
        }
        else
        {
            Width = stream.ReadInt32();
            Height = stream.ReadInt32();
        }

        if (Width <= 0 || Height <= 0)
        {
            ForgetMap(stream);
            return;
        }

        MapGeometry geometry = new(Width, Height);

        uint tileCount = stream.ReadUInt32();

        if (tileCount != (uint)geometry.TileCount)
        {
            // 一张图的瓦片数恒为 width * height（world.cpp:1469 直接读进 vector），对不上就是文件坏了。
            ForgetMap(stream);
            return;
        }

        _tiles.Clear();
        _fog = null;

        for (uint i = 0; i < tileCount && !stream.Fail; ++i)
        {
            _tiles.Add(Tile.ReadFrom(stream, version));
        }

        _heroes.ReadFrom(stream, version, geometry);

        // 城堡表的占地表要知道宽度，所以先换一张按当前宽度建的（castle.cpp:2481 清空的也是同一张）。
        _castles = new AllCastles(Width);
        _castles.ReadFrom(stream, version, geometry);

        _kingdoms.ReadFrom(stream, version, this);

        _rumors.Clear();
        _rumors.AddRange(stream.ReadList(static source => source.ReadString()));

        _dailyEvents.Clear();
        _dailyEvents.AddRange(stream.ReadList(source => EventDate.ReadFrom(source, version)));

        _capturedObjects.LoadFrom(stream);

        _ultimateArtifact = UltimateArtifact.ReadFrom(stream);

        _day = stream.ReadUInt32();
        _week = stream.ReadUInt32();
        _month = stream.ReadUInt32();
        _heroIdAsWinCondition = stream.ReadInt32();
        _heroIdAsLossCondition = stream.ReadInt32();

        if (version < SaveFileFormat.Release1_0_10)
        {
            ++_heroIdAsWinCondition;
            ++_heroIdAsLossCondition;
        }

        if (version < SaveFileFormat.PrePreRelease1_1_06)
        {
            RepairCapturedObjectFlags();
        }

        _mapObjects.LoadFrom(stream, version, geometry);

        _seed = stream.ReadUInt32();

        if (stream.Fail)
        {
            ForgetMap(stream);
            return;
        }

        // 迷雾在瓦片里读回来，这里搬回端口那张表；PostLoad 不碰迷雾，先后无所谓。
        RestoreFogFromTiles(geometry);

        PostLoad(false, true);
    }

    /// <summary>
    /// Puts the fog colours of <see cref="MapFog"/> onto the tiles, the direction the save writer needs.
    /// </summary>
    /// <remarks>
    /// 参考没有这一步：它的迷雾本来就是瓦片的一部分。端口在写盘前把表搬回瓦片，让文件里的迷雾与游戏里看见的一致。
    /// 只搬颜色：方向掩码是派生数据，参考也不存（<see cref="Tile.Persistence"/> 的说明）。
    /// </remarks>
    /// <param name="geometry">The map the tiles belong to.</param>
    private void StoreFogIntoTiles(MapGeometry geometry)
    {
        if (_fog is not { } fog)
        {
            // 没有表就是没有地图（Fog 在有尺寸时按需创建），瓦片本身已经是全黑，写出去也对。
            return;
        }

        for (int index = 0; index < _tiles.Count; ++index)
        {
            _tiles[index].SetFogColors(fog.GetFogColors(index % geometry.Width, index / geometry.Width));
        }
    }

    /// <summary>
    /// Moves the fog colours of every tile into <see cref="Fog"/>, the direction the save reader needs.
    /// </summary>
    /// <remarks>
    /// 读回来的瓦片带着自己的迷雾颜色（<see cref="Tile.FogColors"/>），而端口在游戏里改的是 <see cref="MapFog"/>。
    /// 搬过去之后 <see cref="MapFog.IsFogged"/> 与 <see cref="ClearFog(PlayerColor)"/> 都对得上。
    /// <b>方向掩码留在原地不动</b>：文件里没有它们，参考是读完之后由上层按玩家重算的
    /// （<c>game_startgame.cpp:863</c>、<c>interface_gamearea.cpp:869</c>），端口把这一步留给存档层的调用者。
    /// </remarks>
    /// <param name="geometry">The map the tiles belong to.</param>
    private void RestoreFogFromTiles(MapGeometry geometry)
    {
        if (Fog is not { } fog)
        {
            return;
        }

        for (int index = 0; index < _tiles.Count; ++index)
        {
            fog.SetFog(index % geometry.Width, index / geometry.Width, _tiles[index].FogColors);
        }
    }

    /// <summary>
    /// Repaints the flags of the mines and lighthouses a save older than 1.1.06 describes, <c>world.cpp:1478-1508</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 老的存档不带旗子的精灵，只带颜色：矿井的颜色画在它上面那格与右上那格（<c>FLAG32</c> 分两片），灯塔的颜色
    /// 画在自己那格。所以先把旧的两片去掉再重画，否则一次存档会叠出两套旗子。
    /// </para>
    /// <para>
    /// 参考对「上面那格」的越界检查只有 <c>topIndex &gt;= 0</c>，右上那格的判断 <c>topIndex % width &lt; width - 1</c>
    /// 在 C++ 里对负数仍然成立，会读到 <c>vec_tiles[-1]</c>；第一行的矿井正好落进这个洞。端口两个坐标都夹在
    /// 表内，效果是不画越界的那一片，而不是崩掉。
    /// </para>
    /// </remarks>
    private void RepairCapturedObjectFlags()
    {
        foreach (KeyValuePair<int, CapturedObject> entry in _capturedObjects.Entries)
        {
            int tileIndex = entry.Key;
            CapturedObject capturedObject = entry.Value;

            if (capturedObject.Color == PlayerColor.None)
            {
                continue;
            }

            if (capturedObject.ObjectType == Mp2ObjectType.Mine)
            {
                int topIndex = tileIndex - Width;

                if (topIndex >= 0 && topIndex < _tiles.Count)
                {
                    _tiles[topIndex].RemoveObjects(Mp2ObjectIcnType.Flag32);

                    if ((topIndex % Width) < (Width - 1) && (topIndex + 1) < _tiles.Count)
                    {
                        _tiles[topIndex + 1].RemoveObjects(Mp2ObjectIcnType.Flag32);
                    }
                }

                _tiles[tileIndex].SetOwnershipFlag(this, Mp2ObjectType.Mine, capturedObject.Color);
            }
            else if (capturedObject.ObjectType == Mp2ObjectType.Lighthouse)
            {
                _tiles[tileIndex].SetOwnershipFlag(this, Mp2ObjectType.Lighthouse, capturedObject.Color);
            }
        }
    }

    /// <summary>
    /// Drops everything a failed read may have left behind and marks the stream as failed.
    /// </summary>
    /// <param name="stream">The stream to fail.</param>
    private void ForgetMap(StreamBase stream)
    {
        Width = 0;
        Height = 0;
        _tiles.Clear();
        _fog = null;

        stream.SetFail();
    }
}
