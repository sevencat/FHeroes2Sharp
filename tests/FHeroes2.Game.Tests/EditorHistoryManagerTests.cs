using System.Text;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the editor's undo history (<c>src/fheroes2/editor/history_manager.h</c>,
/// <c>src/fheroes2/editor/history_manager.cpp</c>): commit/rollback, the redo branch, the action limit and the
/// metadata actions.
/// </summary>
public sealed class EditorHistoryManagerTests
{
    /// <summary>
    /// A map that is just a string, so a test can read the whole editor state at a glance.
    /// </summary>
    private sealed class FakeMap : IMapSnapshotProvider
    {
        /// <summary>The current content of the map.</summary>
        public string Content { get; set; } = "empty";

        /// <summary>How many times the map was restored.</summary>
        public int RestoreCount { get; private set; }

        /// <inheritdoc/>
        public byte[]? Capture() => Encoding.UTF8.GetBytes(Content);

        /// <inheritdoc/>
        public bool Restore(byte[] snapshot)
        {
            Content = Encoding.UTF8.GetString(snapshot);
            ++RestoreCount;

            return true;
        }
    }

    [Fact]
    public void CommittedChangeCanBeUndoneAndRedone()
    {
        FakeMap map = new();
        HistoryManager manager = new();

        using (ActionCreator creator = new(manager, map, 0))
        {
            map.Content = "tile painted";
            creator.Commit(0);
        }

        Assert.Equal("tile painted", map.Content);
        Assert.True(manager.IsUndoAvailable);
        Assert.False(manager.IsRedoAvailable);
        Assert.Equal(1, manager.Count);

        Assert.True(manager.Undo());
        Assert.Equal("empty", map.Content);
        Assert.False(manager.IsUndoAvailable);
        Assert.True(manager.IsRedoAvailable);

        Assert.True(manager.Redo());
        Assert.Equal("tile painted", map.Content);
        Assert.True(manager.IsUndoAvailable);
        Assert.False(manager.IsRedoAvailable);
    }

    [Fact]
    public void UncommittedChangeIsRolledBack()
    {
        FakeMap map = new();
        HistoryManager manager = new();

        using (ActionCreator creator = new(manager, map, 0))
        {
            map.Content = "half done";
        }

        // 没 commit 就当没发生：对话框被关掉、中途抛异常都不会留下脏地图。
        Assert.Equal("empty", map.Content);
        Assert.False(manager.IsUndoAvailable);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void UndoThenCommitDropsTheRedoBranch()
    {
        FakeMap map = new();
        HistoryManager manager = new();

        using (ActionCreator creator = new(manager, map, 0))
        {
            map.Content = "first";
            creator.Commit(0);
        }

        Assert.True(manager.Undo());
        Assert.True(manager.IsRedoAvailable);

        using (ActionCreator creator = new(manager, map, 0))
        {
            map.Content = "second";
            creator.Commit(0);
        }

        // 新的改动把「重做」那条分支作废了。
        Assert.False(manager.IsRedoAvailable);
        Assert.Equal(1, manager.Count);

        Assert.True(manager.Undo());
        Assert.Equal("empty", map.Content);
    }

    [Fact]
    public void StateCallbackReportsBothFlags()
    {
        FakeMap map = new();
        HistoryManager manager = new();
        List<(bool CanUndo, bool CanRedo)> states = [];

        manager.SetStateCallback((canUndo, canRedo) => states.Add((canUndo, canRedo)));

        using (ActionCreator creator = new(manager, map, 0))
        {
            map.Content = "changed";
            creator.Commit(0);
        }

        manager.Undo();
        manager.Redo();

        Assert.Equal([(true, false), (false, true), (true, false)], states);

        manager.Reset();
        Assert.Equal((false, false), states[^1]);
    }

    [Fact]
    public void OldestActionsAreDroppedAtTheLimit()
    {
        FakeMap map = new();
        HistoryManager manager = new();

        for (int i = 0; i < HistoryManager.MaxActions + 5; ++i)
        {
            string content = "state " + i;

            using ActionCreator creator = new(manager, map, 0);
            map.Content = content;
            creator.Commit(0);
        }

        Assert.Equal(HistoryManager.MaxActions, manager.Count);

        // 最老的那几步已经丢掉了，撤销最多只能退到第 5 步的状态。
        for (int i = 0; i < HistoryManager.MaxActions; ++i)
        {
            Assert.True(manager.Undo());
        }

        Assert.False(manager.Undo());
        Assert.Equal("state 4", map.Content);
    }

    [Fact]
    public void MetadataActionSwapsTheDictionary()
    {
        Dictionary<uint, string> metadata = new()
        {
            [1] = "before",
        };

        MetadataMapAction<string> action = new(metadata);
        metadata[1] = "after";
        metadata[2] = "new";

        Assert.True(action.Prepare());

        Assert.True(action.Undo());
        Assert.Equal("before", metadata[1]);
        Assert.False(metadata.ContainsKey(2));

        Assert.True(action.Redo());
        Assert.Equal("after", metadata[1]);
        Assert.Equal("new", metadata[2]);
    }

    [Fact]
    public void SnapshotActionRestoresTheObjectUidCounter()
    {
        FakeMap map = new();
        HistoryManager manager = new();

        using (ActionCreator creator = new(manager, map, 41))
        {
            map.Content = "with object";
            creator.Commit(42);
        }

        Assert.True(manager.Undo());
        Assert.Equal(1, map.RestoreCount);
        Assert.Equal("empty", map.Content);

        Assert.True(manager.Redo());
        Assert.Equal("with object", map.Content);
    }
}
