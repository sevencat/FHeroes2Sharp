using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;

namespace FHeroes2.Game.Maps;

/// <summary>
/// What a tile of the generator's grid is used for, <c>Maps::Random_Generator::NodeType</c>
/// (<c>map_random_generator_info.h:52-61</c>).
/// </summary>
public enum RandomMapNodeType : byte
{
    /// <summary>Free land no region has taken yet, <c>OPEN</c>.</summary>
    Open,

    /// <summary>The edge of a region, <c>BORDER</c>.</summary>
    Border,

    /// <summary>Space reserved for an object, <c>ACTION</c>.</summary>
    Action,

    /// <summary>Space taken by an obstacle, <c>OBSTACLE</c>.</summary>
    Obstacle,

    /// <summary>A road end that joins two regions, <c>CONNECTOR</c>.</summary>
    Connector,

    /// <summary>A tile of a road, <c>PATH</c>.</summary>
    Path,

    /// <summary>Where land meets water, <c>COAST</c>.</summary>
    Coast,
}

/// <summary>
/// One tile of the generator's grid, <c>Maps::Random_Generator::Node</c> (<c>map_random_generator_info.h:63-78</c>).
/// </summary>
/// <remarks>
/// 参考把这东西放在 <c>std::vector&lt;Node&gt;</c> 里，区域持有的是元素的引用；端口把它做成引用类型，同一个理由——
/// 区域扩张时写的是网格里的那一格，不是副本。
/// </remarks>
public sealed class RandomMapNode
{
    /// <summary>Creates a node of the grid.</summary>
    /// <param name="index">Its absolute index, <c>y * width + x</c>.</param>
    /// <param name="region">The region it belongs to, <c>0</c> while none took it.</param>
    /// <param name="type">What the tile is used for.</param>
    public RandomMapNode(int index, uint region, RandomMapNodeType type)
    {
        Index = index;
        Region = region;
        Type = type;
    }

    /// <summary>The absolute index of the tile, <c>index</c>; <c>-1</c> for the out of bounds node.</summary>
    public int Index { get; }

    /// <summary>The region that took the tile, <c>region</c>; <c>0</c> while none did.</summary>
    public uint Region { get; internal set; }

    /// <summary>What the tile is used for, <c>type</c>.</summary>
    public RandomMapNodeType Type { get; internal set; }
}

/// <summary>
/// The grid the random map generator lays its regions on, <c>Maps::Random_Generator::MapStateManager</c>
/// (<c>map_random_generator_info.h:80-190</c>, <c>map_random_generator_info.cpp:43-89</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考直接从 <c>world</c> 的宽高取坐标换算（<c>Maps::GetPoint</c> 用 <c>world.w()</c>）；端口把它收进这个对象，
/// 因为生成器要能在没有世界的情况下跑单元测试。索引约定不变：<c>index = y * Width + x</c>。
/// </para>
/// <para>
/// <b>事务</b>：参考让写操作把旧状态记进历史，<c>MapStateTransaction</c> 在析构时回滚、<c>commit()</c> 之后才保留。
/// 端口用 <see cref="BeginTransaction"/> 拿一个可 <c>Commit</c> 的句柄，离开 <c>using</c> 时没提交就回滚——同一个语义，
/// 但不需要 C++ 的析构技巧。区域扩张失败时要靠它把格子放回去。
/// </para>
/// <para>
/// <b>越界</b>：参考对越界访问返回一个 <c>_outOfBounds</c> 节点（索引 <c>-1</c>、类型 <c>BORDER</c>）并断言，
/// 端口同样返回那个共享节点。写它等于丢进黑洞——生成器只会读它的 <see cref="RandomMapNode.Index"/>。
/// </para>
/// </remarks>
public sealed class RandomMapState
{
    /// <summary>The eight neighbours of a tile, starting north and going clockwise, <c>directionOffsets</c>.</summary>
    public static readonly Point[] DirectionOffsets =
    [
        new(0, -1),
        new(1, 0),
        new(0, 1),
        new(-1, 0),
        new(-1, -1),
        new(1, -1),
        new(1, 1),
        new(-1, 1),
    ];

    private readonly RandomMapNode[] _data;
    private readonly List<(int Index, uint Region, RandomMapNodeType Type)> _history = [];
    private readonly List<int> _transactionRecords = [];
    private readonly RandomMapNode _outOfBounds = new(-1, 0, RandomMapNodeType.Border);

    /// <summary>Creates a square grid of open nodes, <c>MapStateManager::MapStateManager()</c>.</summary>
    /// <param name="width">Edge length of the map.</param>
    /// <param name="height">Edge length of the map; the reference asserts it equals <paramref name="width"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either size is not positive.</exception>
    public RandomMapState(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        _data = new RandomMapNode[width * height];

        for (int index = 0; index < _data.Length; ++index)
        {
            _data[index] = new RandomMapNode(index, 0, RandomMapNodeType.Open);
        }
    }

    /// <summary>Edge length of the map, the <c>_mapSize</c> of the reference.</summary>
    public int Width { get; }

    /// <summary>Height of the map; the reference keeps only one size because a map is square.</summary>
    public int Height { get; }

    /// <summary>How many nodes the grid holds.</summary>
    public int Count => _data.Length;

    /// <summary>Whether a point is inside the map, <c>Maps::isValidAbsPoint()</c> (<c>maps.cpp:382</c>).</summary>
    /// <param name="point">Tile coordinates.</param>
    /// <returns><see langword="true"/> when both coordinates are inside the map.</returns>
    public bool IsValidPoint(in Point point) =>
        point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;

    /// <summary>Tile coordinates of an index, <c>Maps::GetPoint()</c> (<c>maps.cpp:372</c>).</summary>
    /// <param name="index">Absolute index.</param>
    /// <returns><c>x = index % width</c>, <c>y = index / width</c>.</returns>
    public Point GetPoint(int index) => new(index % Width, index / Width);

    /// <summary>Index of a tile, <c>Maps::GetIndexFromAbsPoint()</c> (<c>maps.cpp:396</c>).</summary>
    /// <param name="point">Tile coordinates.</param>
    /// <returns>The index, or <c>-1</c> for a negative coordinate.</returns>
    public int IndexAt(in Point point) =>
        point.X < 0 || point.Y < 0 ? -1 : (point.Y * Width) + point.X;

    /// <summary>
    /// The octile distance between two tiles, <c>Maps::GetApproximateDistance()</c> (<c>maps.cpp:691-702</c>):
    /// <c>max + min / 2</c>.
    /// </summary>
    /// <param name="first">Index of the first tile.</param>
    /// <param name="second">Index of the second tile.</param>
    /// <returns>The distance in tiles.</returns>
    public uint ApproximateDistance(int first, int second)
    {
        Point a = GetPoint(first);
        Point b = GetPoint(second);

        uint diffX = (uint)Math.Abs(a.X - b.X);
        uint diffY = (uint)Math.Abs(a.Y - b.Y);

        return Math.Max(diffX, diffY) + (Math.Min(diffX, diffY) / 2);
    }

    /// <summary>Reads a node, <c>getNode( position )</c>.</summary>
    /// <param name="point">Tile coordinates.</param>
    /// <returns>The node, or the shared out of bounds node.</returns>
    public RandomMapNode GetNode(in Point point) => GetNode(IndexAt(point));

    /// <summary>Reads a node, <c>getNode( index )</c>.</summary>
    /// <param name="index">Absolute index.</param>
    /// <returns>The node, or the shared out of bounds node.</returns>
    public RandomMapNode GetNode(int index) =>
        index < 0 || index >= _data.Length ? _outOfBounds : _data[index];

    /// <summary>Reads a node to change it, <c>getNodeToUpdate()</c>; the old state is recorded for a rollback.</summary>
    /// <param name="point">Tile coordinates.</param>
    /// <returns>The node, or the shared out of bounds node.</returns>
    public RandomMapNode GetNodeToUpdate(in Point point) => GetNodeToUpdate(IndexAt(point));

    /// <summary>Reads a node to change it, <c>getNodeToUpdate( index )</c>; the old state is recorded for a rollback.</summary>
    /// <param name="index">Absolute index.</param>
    /// <returns>The node, or the shared out of bounds node.</returns>
    public RandomMapNode GetNodeToUpdate(int index)
    {
        if (index < 0 || index >= _data.Length)
        {
            return _outOfBounds;
        }

        if (_transactionRecords.Count > 0)
        {
            RandomMapNode node = _data[index];
            _history.Add((index, node.Region, node.Type));
        }

        return _data[index];
    }

    /// <summary>
    /// Opens a transaction, <c>MapStateTransaction</c>: every change made through a <c>GetNodeToUpdate</c> until the
    /// handle leaves its <c>using</c> block is undone unless <see cref="RandomMapStateTransaction.Commit"/> was called.
    /// </summary>
    /// <returns>The handle that ends the transaction.</returns>
    public RandomMapStateTransaction BeginTransaction()
    {
        int record = _history.Count;
        _transactionRecords.Add(record);

        return new RandomMapStateTransaction(this, record);
    }

    /// <summary>Drops the history of a committed transaction, <c>commitTransaction()</c>.</summary>
    /// <param name="record">Where the transaction started.</param>
    internal void CommitTransaction(int record)
    {
        _transactionRecords.Remove(record);

        if (_transactionRecords.Count == 0)
        {
            _history.Clear();
        }
    }

    /// <summary>Puts every node back the way it was, <c>rollbackTransaction()</c>.</summary>
    /// <param name="record">Where the transaction started.</param>
    internal void RollbackTransaction(int record)
    {
        _transactionRecords.Remove(record);

        if (_history.Count == 0)
        {
            return;
        }

        for (int index = _history.Count; index > record; --index)
        {
            (int nodeIndex, uint region, RandomMapNodeType type) = _history[index - 1];

            RandomMapNode node = _data[nodeIndex];
            node.Region = region;
            node.Type = type;
            _history.RemoveAt(_history.Count - 1);
        }

        if (_transactionRecords.Count == 0)
        {
            _history.Clear();
        }
    }
}

/// <summary>
/// Ends a <see cref="RandomMapState.BeginTransaction"/>: <see cref="Commit"/> keeps the changes, leaving without
/// committing undoes them, <c>Maps::Random_Generator::MapStateTransaction</c>
/// (<c>map_random_generator_info.h:192-224</c>).
/// </summary>
public sealed class RandomMapStateTransaction : IDisposable
{
    private readonly RandomMapState _state;
    private readonly int _record;
    private bool _finished;

    /// <summary>Starts the transaction; call <see cref="RandomMapState.BeginTransaction"/> instead.</summary>
    /// <param name="state">The grid the transaction belongs to.</param>
    /// <param name="record">Where the transaction started in the history.</param>
    internal RandomMapStateTransaction(RandomMapState state, int record)
    {
        _state = state;
        _record = record;
    }

    /// <summary>Keeps every change made so far, <c>commit()</c> of the reference.</summary>
    public void Commit()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _state.CommitTransaction(_record);
    }

    /// <summary>Rolls the changes back when the transaction was not committed.</summary>
    public void Dispose()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _state.RollbackTransaction(_record);
    }
}
