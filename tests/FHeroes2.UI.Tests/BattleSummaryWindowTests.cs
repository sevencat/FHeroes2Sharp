using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Battle;
using FHeroes2.UI.Input;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The battle result window, <c>Battle::Arena::DialogBattleSummary()</c> (<c>battle_dialogs.cpp:484-783</c>).
/// </summary>
/// <remarks>
/// 几何与输入不依赖档案；底图与动画帧来自 <see cref="DialogSprites"/>，缺图时窗口仍应建得起来。
/// 场景里没有英雄的守方，所以神器交接段只测「空表立刻结束」这一条。
/// </remarks>
public sealed class BattleSummaryWindowTests
{
    private static readonly Size Screen = new(640, 480);

    /// <summary>The animation window and the summary block sit where the reference computes them.</summary>
    [Fact]
    public void TheAnimationAndSummarySitOnTheReferenceOffsets()
    {
        BattleSummaryWindow window = Create();

        Assert.Equal(BattleSummaryWindow.Width, window.Area.Width);
        Assert.Equal(BattleSummaryWindow.Height, window.Area.Height);

        Assert.Equal(window.Area.X + 56, window.AnimationArea.X);
        Assert.Equal(window.Area.Y + 21, window.AnimationArea.Y);
        Assert.Equal(231, window.AnimationArea.Width);
        Assert.Equal(133, window.AnimationArea.Height);

        Assert.Equal(window.Area.X + 11, window.SummaryArea.X);
        Assert.Equal(window.Area.Y + 160, window.SummaryArea.Y);
        Assert.Equal(313, window.SummaryArea.Width);
    }

    /// <summary>Without a restart the OK button is alone and centred at the bottom.</summary>
    [Fact]
    public void ASingleOkayIsCentredAtTheBottom()
    {
        BattleSummaryWindow window = Create();

        Assert.Null(window.RestartButton);

        Rect okay = window.OkayButton.Area;
        int windowCentre = window.Area.X + (window.Area.Width / 2);

        Assert.InRange(Math.Abs((okay.X + (okay.Width / 2)) - windowCentre), 0, 2);
        Assert.True(okay.Y > window.Area.Y + (window.Area.Height / 2));
    }

    /// <summary>A restart without artwork would paint half a button, so there is no window at all.</summary>
    [Fact]
    public void ARestartNeedsItsOwnArtwork() =>
        Assert.Null(BattleSummaryWindow.TryCreate(new DialogSprites(), new DialogMiniMonsters(), Text(), [], [],
            highlightTitle: false, allowToRestart: true, new Sprite(46, 50), new Sprite(46, 50),
            restartReleased: null, restartPressed: null, Screen));

    /// <summary>With both buttons the reference puts OK left and restart right, the <c>{ 23, 5 }</c> margin.</summary>
    [Fact]
    public void ARestartSplitsTheBottomRow()
    {
        BattleSummaryWindow window = Create(allowToRestart: true);

        Assert.True(window.RestartButton is not null);

        Rect okay = window.OkayButton.Area;
        Rect restart = window.RestartButton!.Area;

        Assert.True(okay.X < restart.X);
        Assert.Equal(okay.Y, restart.Y);
        Assert.Equal(window.Area.X + 23, okay.X);
    }

    /// <summary>Without input nothing happens; the modal loop keeps spinning.</summary>
    [Fact]
    public void WithoutInputTheWindowStaysOpen() =>
        Assert.Equal(BattleSummaryWindowAction.None, Create().ProcessEvents(new LocalEvent()));

    /// <summary>Clicking OK reports the close, the reference's <c>btn = 0</c>.</summary>
    [Fact]
    public void TheOkayButtonClosesTheWindow()
    {
        BattleSummaryWindow window = Create();
        LocalEvent events = new();
        Point at = window.OkayButton.Area.Position + new Point(5, 5);

        events.OnMouseButton(true, MouseButtonType.Left, at);
        events.OnMouseButton(false, MouseButtonType.Left, at);

        Assert.Equal(BattleSummaryWindowAction.Close, window.ProcessEvents(events));
    }

    /// <summary>An empty artifact list ends the hand-over at once, which is the case for a wandering monster.</summary>
    [Fact]
    public void AnEmptyHandOverEndsAtOnce()
    {
        BattleSummaryWindow window = Create();

        Assert.False(window.BeginArtifacts([], winnerIsHuman: true));
        Assert.False(window.IsShowingArtifacts);
        Assert.Null(window.CurrentArtifact);
    }

    /// <summary>A missing backdrop only drops the drawing, the window itself is still there.</summary>
    [Fact]
    public void AWindowWithoutBackdropStillBuilds() =>
        Assert.NotNull(BattleSummaryWindow.TryCreate(new DialogSprites(withBackground: false),
            new DialogMiniMonsters(), Text(), [], [], highlightTitle: false, allowToRestart: false,
            new Sprite(46, 50), new Sprite(46, 50), restartReleased: null, restartPressed: null, Screen));

    private static BattleSummaryText Text() =>
        BattleSummaryText.Create(BattleOutcome.Wins, BattleOutcome.Loss, "Ariel", 1000, 2500);

    private static BattleSummaryWindow Create(bool allowToRestart = false) =>
        Assert.IsType<BattleSummaryWindow>(BattleSummaryWindow.TryCreate(
            new DialogSprites(), new DialogMiniMonsters(), Text(), [], [], highlightTitle: true, allowToRestart,
            new Sprite(46, 50), new Sprite(46, 50),
            allowToRestart ? new Sprite(46, 50) : null,
            allowToRestart ? new Sprite(46, 50) : null,
            Screen));

    /// <summary>Stand-in artwork for the two backdrops the window reads.</summary>
    private sealed class DialogSprites : IViewWorldSpriteSource
    {
        private readonly Dictionary<(IcnType Icn, int Index), Sprite> _frames = new();

        public DialogSprites(bool withBackground = true)
        {
            if (withBackground)
            {
                _frames[(IcnType.Winlose, 0)] = new Sprite(335, 424);
                _frames[(IcnType.Winloseb, 0)] = new Sprite(335, 424);
            }
        }

        public Sprite? GetSprite(IcnType icn, int index) => _frames.GetValueOrDefault((icn, index));
    }

    /// <summary>No small creature portraits: the casualties are simply not drawn.</summary>
    private sealed class DialogMiniMonsters : IMiniMonsterSprites
    {
        public Sprite? GetMiniMonster(MonsterType monster) => null;
    }
}
