using System.Diagnostics;

namespace FHeroes2.Game.Editor;

/// <summary>
/// Something that can be undone and redone, <c>fheroes2::Action</c>
/// (<c>src/fheroes2/editor/history_manager.h:42-50</c>).
/// </summary>
public abstract class MapAction
{
    /// <summary>
    /// Captures the state after the change, <c>prepare()</c>
    /// (<c>src/fheroes2/editor/history_manager.h:97-188</c>).
    /// </summary>
    /// <param name="lastObjectUid">The UID counter of the map; the snapshot action keeps it, the others ignore it.</param>
    /// <returns>False when the action cannot be committed, which makes the creator revert it instead.</returns>
    public abstract bool Prepare(uint lastObjectUid);

    /// <summary>Re-applies the change, <c>redo()</c>.</summary>
    /// <returns>False when the change could not be applied.</returns>
    public abstract bool Redo();

    /// <summary>Reverts the change, <c>undo()</c>.</summary>
    /// <returns>False when the change could not be reverted.</returns>
    public abstract bool Undo();
}

/// <summary>
/// Provides the before/after snapshots a whole-map action needs,
/// the <c>saveMap()</c>/<c>loadMap()</c> pair of <c>GenericMapAction</c>
/// (<c>src/fheroes2/editor/history_manager.cpp:51-123</c>).
/// </summary>
/// <remarks>
/// 参考直接对 <c>Maps::Map_Format::MapFormat</c> 做序列化/反序列化；端口把它抽成两个方法，因为地图读写是
/// `Mp2MapWriter` 与 `Mp2Map` 的活，历史管理器不该知道文件格式。
/// </remarks>
public interface IMapSnapshotProvider
{
    /// <summary>Takes a snapshot of the current map state, <c>saveMap()</c>.</summary>
    /// <returns>The snapshot, or <see langword="null"/> when it could not be taken.</returns>
    byte[]? Capture();

    /// <summary>Restores the map from a snapshot and rebuilds the editor state, <c>loadMap()</c> + <c>readMapInEditor()</c>.</summary>
    /// <param name="snapshot">A snapshot previously returned by <see cref="Capture"/>.</param>
    /// <returns>False when the map could not be restored.</returns>
    bool Restore(byte[] snapshot);
}

/// <summary>
/// Undo and redo for the map editor, <c>fheroes2::HistoryManager</c>
/// (<c>src/fheroes2/editor/history_manager.h:97-188</c>).
/// </summary>
/// <remarks>
/// <para>
/// 只存动作不存地图：动作自己保管「改之前」与「改之后」。
/// 整图改动是两份压缩快照，元数据改动是两份字典副本，所以同一份历史里两种粒度混着放也没问题。
/// </para>
/// <para>
/// 上限 999 步——参考的注释说得很实在：没人会连着撤销这么多步，存多了纯费内存。
/// </para>
/// </remarks>
public sealed class HistoryManager
{
    /// <summary>
    /// How many actions are kept, <c>maxActions</c> (<c>history_manager.h:181</c>).
    /// </summary>
    public const int MaxActions = 999;

    /// <summary>The actions in the order they were done, <c>_actions</c>.</summary>
    private readonly LinkedList<MapAction> _actions = new();

    /// <summary>
    /// How many of them are applied, <c>_lastActionId</c>: everything after it can be redone.
    /// </summary>
    private int _lastActionId;

    /// <summary>The listener of the two availability flags, <c>_stateCallback</c>.</summary>
    private Action<bool, bool>? _stateCallback;

    /// <summary>Whether <see cref="Undo"/> would do anything, <c>isUndoAvailable()</c>.</summary>
    public bool IsUndoAvailable => _lastActionId > 0;

    /// <summary>Whether <see cref="Redo"/> would do anything, <c>isRedoAvailable()</c>.</summary>
    public bool IsRedoAvailable => _lastActionId < _actions.Count;

    /// <summary>How many actions are stored.</summary>
    public int Count => _actions.Count;

    /// <summary>
    /// Registers the listener called after every change, <c>setStateCallback()</c>
    /// (<c>history_manager.h:100-103</c>).
    /// </summary>
    /// <param name="stateCallback">Receives <c>(isUndoAvailable, isRedoAvailable)</c>.</param>
    public void SetStateCallback(Action<bool, bool>? stateCallback)
    {
        _stateCallback = stateCallback;
    }

    /// <summary>
    /// Forgets everything, <c>reset()</c> (<c>history_manager.h:105-113</c>).
    /// </summary>
    public void Reset()
    {
        _actions.Clear();
        _lastActionId = 0;

        NotifyState();
    }

    /// <summary>
    /// Appends a new action, dropping whatever was undone before it, <c>add()</c>
    /// (<c>history_manager.h:115-133</c>).
    /// </summary>
    /// <param name="action">The action, already prepared.</param>
    public void Add(MapAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // A new action invalidates the redo branch.
        while (_actions.Count > _lastActionId)
        {
            _actions.RemoveLast();
        }

        _actions.AddLast(action);
        ++_lastActionId;

        if (_actions.Count > MaxActions)
        {
            _actions.RemoveFirst();
            --_lastActionId;
        }

        NotifyState();

        Debug.Assert(_actions.Count <= MaxActions);
    }

    /// <summary>Reverts the last applied action, <c>undo()</c> (<c>history_manager.h:145-160</c>).</summary>
    /// <returns>False when there is nothing to undo or the action failed.</returns>
    public bool Undo()
    {
        if (!IsUndoAvailable)
        {
            return false;
        }

        --_lastActionId;
        bool result = NodeAt(_lastActionId).Value.Undo();

        NotifyState();

        return result;
    }

    /// <summary>Re-applies the next undone action, <c>redo()</c> (<c>history_manager.h:162-177</c>).</summary>
    /// <returns>False when there is nothing to redo or the action failed.</returns>
    public bool Redo()
    {
        if (!IsRedoAvailable)
        {
            return false;
        }

        bool result = NodeAt(_lastActionId).Value.Redo();
        ++_lastActionId;

        NotifyState();

        return result;
    }

    /// <summary>The node holding the action at the given index.</summary>
    /// <param name="index">The index inside the stored actions.</param>
    /// <returns>The node.</returns>
    private LinkedListNode<MapAction> NodeAt(int index)
    {
        LinkedListNode<MapAction>? node = _actions.First;

        for (int i = 0; i < index && node is not null; ++i)
        {
            node = node.Next;
        }

        Debug.Assert(node is not null);

        return node!;
    }

    /// <summary>Tells the listener about the current availability.</summary>
    private void NotifyState()
    {
        _stateCallback?.Invoke(IsUndoAvailable, IsRedoAvailable);
    }
}

/// <summary>
/// A whole-map action: keeps a snapshot before and after,
/// <c>GenericMapAction</c> (<c>src/fheroes2/editor/history_manager.cpp:51-123</c>).
/// </summary>
/// <remarks>
/// 也记下「改之前最后一个对象编号」。撤销后如果编号不回退，重做出来的新对象会拿到更靠后的号，
/// 存档里就会出现一整段空洞的编号。
/// </remarks>
internal sealed class SnapshotMapAction : MapAction
{
    /// <summary>The editor's access to the map, <c>_mapFormat</c> plus the two serializers.</summary>
    private readonly IMapSnapshotProvider _provider;

    /// <summary>The map before the change, <c>_beforeMapFormat</c>.</summary>
    private readonly byte[] _before;

    /// <summary>The map after the change, filled in by <see cref="Prepare"/>, <c>_afterMapFormat</c>.</summary>
    private byte[]? _after;

    /// <summary>The last object UID before the change, <c>_latestObjectUIDBefore</c>.</summary>
    private readonly uint _lastObjectUidBefore;

    /// <summary>The last object UID after the change, <c>_latestObjectUIDAfter</c>.</summary>
    private uint _lastObjectUidAfter;

    /// <summary>
    /// Captures the "before" state, the reference constructor (<c>history_manager.cpp:54-61</c>).
    /// </summary>
    /// <param name="provider">The editor's map access.</param>
    /// <param name="lastObjectUid">The UID counter of the map.</param>
    public SnapshotMapAction(IMapSnapshotProvider provider, uint lastObjectUid)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
        _before = provider.Capture() ?? [];
        _lastObjectUidBefore = lastObjectUid;

        Debug.Assert(_before.Length > 0, "The map snapshot before the change could not be taken.");
    }

    /// <summary>The UID counter that has to be restored together with the map.</summary>
    public uint LastObjectUidAfter => _lastObjectUidAfter;

    /// <summary>
    /// Captures the "after" state, <c>prepare()</c> (<c>history_manager.cpp:63-73</c>).
    /// </summary>
    /// <param name="lastObjectUid">The UID counter of the changed map.</param>
    /// <returns>False when the snapshot could not be taken.</returns>
    public override bool Prepare(uint lastObjectUid)
    {
        _after = _provider.Capture();
        _lastObjectUidAfter = lastObjectUid;

        return _after is { Length: > 0 };
    }

    /// <inheritdoc/>
    public override bool Redo()
    {
        return _after is not null && _provider.Restore(_after);
    }

    /// <inheritdoc/>
    public override bool Undo()
    {
        return _provider.Restore(_before);
    }

    /// <summary>The UID counter the undo restores, <c>_latestObjectUIDBefore</c>.</summary>
    public uint LastObjectUidBefore => _lastObjectUidBefore;
}

/// <summary>
/// A metadata-only action: snapshots one dictionary of per-object data,
/// <c>MetadataMapAction&lt;T&gt;</c> (<c>src/fheroes2/editor/history_manager.cpp:125-159</c>).
/// </summary>
/// <typeparam name="T">The per-object metadata.</typeparam>
public sealed class MetadataMapAction<T> : MapAction
{
    /// <summary>The dictionary the action works on, <c>_metadata</c>.</summary>
    private readonly IDictionary<uint, T> _metadata;

    /// <summary>Its content before the change, <c>_beforeMetadata</c>.</summary>
    private readonly Dictionary<uint, T> _before;

    /// <summary>Its content after the change, filled in by <see cref="Prepare"/>, <c>_afterMetadata</c>.</summary>
    private Dictionary<uint, T>? _after;

    /// <summary>
    /// Snapshots the dictionary, the reference constructor (<c>history_manager.cpp:128-134</c>).
    /// </summary>
    /// <param name="metadata">The dictionary to track; the action keeps a reference to it.</param>
    public MetadataMapAction(IDictionary<uint, T> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        _metadata = metadata;
        _before = new Dictionary<uint, T>(metadata);
    }

    /// <summary>
    /// Captures the state after the change, <c>prepare()</c> (<c>history_manager.cpp:136-140</c>).
    /// </summary>
    /// <param name="lastObjectUid">Ignored: a metadata action moves no object.</param>
    /// <returns>Always true: copying a dictionary cannot fail.</returns>
    public override bool Prepare(uint lastObjectUid)
    {
        _after = new Dictionary<uint, T>(_metadata);

        return true;
    }

    /// <inheritdoc/>
    public override bool Redo()
    {
        if (_after is null)
        {
            return false;
        }

        Replace(_after);

        return true;
    }

    /// <inheritdoc/>
    public override bool Undo()
    {
        Replace(_before);

        return true;
    }

    /// <summary>Makes the tracked dictionary hold the given content.</summary>
    /// <param name="source">The content to copy in.</param>
    private void Replace(Dictionary<uint, T> source)
    {
        _metadata.Clear();

        foreach (KeyValuePair<uint, T> entry in source)
        {
            _metadata[entry.Key] = entry.Value;
        }
    }
}

/// <summary>
/// Pairs a change with the history manager so that the change is either committed or silently reverted,
/// <c>fheroes2::ActionCreator</c> (<c>src/fheroes2/editor/history_manager.h:53-94</c>).
/// </summary>
/// <remarks>
/// C# 用 <see cref="IDisposable"/> 代替析构函数：<c>using</c> 块结束时若没调用 <see cref="Commit"/>，
/// 就把改动撤回——对话框被关掉、异常中途抛出时都不会留下「改了但没进历史」的地图。
/// </remarks>
public sealed class ActionCreator : IDisposable
{
    /// <summary>
    /// The kinds of change, <c>ActionCreator::ActionType</c> (<c>history_manager.h:56-72</c>).
    /// </summary>
    public static class ActionType
    {
        /// <summary>Any change; the most expensive one, <c>GENERIC</c>.</summary>
        public const int Generic = 0;

        /// <summary>Hero metadata, <c>HERO_METADATA</c>.</summary>
        public const int HeroMetadata = 1;

        /// <summary>Castle metadata, <c>CASTLE_METADATA</c>.</summary>
        public const int CastleMetadata = 2;

        /// <summary>Sphinx metadata, <c>SPHINX_METADATA</c>.</summary>
        public const int SphinxMetadata = 3;

        /// <summary>Sign metadata, <c>SIGN_METADATA</c>.</summary>
        public const int SignMetadata = 4;

        /// <summary>Adventure map event metadata, <c>ADVENTURE_MAP_EVENT_METADATA</c>.</summary>
        public const int AdventureMapEventMetadata = 5;

        /// <summary>Selection object metadata, <c>SELECTION_METADATA</c>.</summary>
        public const int SelectionMetadata = 6;

        /// <summary>Monster metadata, <c>MONSTER_METADATA</c>.</summary>
        public const int MonsterMetadata = 7;

        /// <summary>Artifact metadata, <c>ARTIFACT_METADATA</c>.</summary>
        public const int ArtifactMetadata = 8;

        /// <summary>Resource metadata, <c>RESOURCE_METADATA</c>.</summary>
        public const int ResourceMetadata = 9;
    }

    /// <summary>The manager the change goes into, <c>_manager</c>.</summary>
    private readonly HistoryManager _manager;

    /// <summary>The change being built, <c>_action</c>.</summary>
    private MapAction? _action;

    /// <summary>
    /// Starts tracking a change, the reference constructor (<c>history_manager.cpp:164-203</c>).
    /// </summary>
    /// <param name="manager">The history manager.</param>
    /// <param name="provider">The editor's map access, used by the generic action.</param>
    /// <param name="metadata">The per-object metadata tables of the map, used by every other action type.</param>
    /// <param name="lastObjectUid">The UID counter of the map.</param>
    /// <param name="type">One of <see cref="ActionType"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not one of <see cref="ActionType"/>.</exception>
    public ActionCreator(
        HistoryManager manager, IMapSnapshotProvider provider, MapObjectMetadataStore metadata, uint lastObjectUid, int type = ActionType.Generic)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(metadata);

        _manager = manager;

        // 整图快照与「只动一本元数据表」是同一份历史的两种粒度：前者存两份压缩的地图，后者只存一本表的前后副本。
        _action = type == ActionType.Generic ? new SnapshotMapAction(provider, lastObjectUid) : metadata.CreateAction(type);
    }

    /// <summary>
    /// Accepts the change, <c>commit()</c> (<c>history_manager.cpp:205-217</c>).
    /// </summary>
    /// <param name="lastObjectUidAfter">The UID counter of the changed map.</param>
    public void Commit(uint lastObjectUidAfter)
    {
        if (_action is null || !_action.Prepare(lastObjectUidAfter))
        {
            // 没准备好就留在手上：松开 <see cref="Dispose"/> 会把它撤回去，与参考的析构一样。
            return;
        }

        _manager.Add(_action);
        _action = null;
    }

    /// <summary>
    /// Drops the change, reverting it if it was never committed, the reference destructor
    /// (<c>history_manager.h:76-82</c>).
    /// </summary>
    public void Dispose()
    {
        if (_action is not null)
        {
            _action.Undo();
            _action = null;
        }
    }
}
