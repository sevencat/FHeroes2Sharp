using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App;

/// <summary>
/// The battle screen as a modal window of the host's frame pump: the port's stand-in for the blocking
/// <c>Battle::Interface</c> event loop that <c>Arena::HumanTurn()</c> runs for the reference
/// (<c>battle_arena.cpp:513</c>, <c>battle_interface.cpp:2458-2560</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的人类回合直接转界面的事件循环，玩家的每一手都在这个循环里被消化；端口的主循环是场景状态机，所以这层
/// 循环由 <see cref="GameHost.RunModal"/> 借出来，<see cref="BattleScene.Update"/> 每被调一次就画一帧、看一眼输入。
/// 什么时候算「这一手已经交出去了」由调用方给的谓词决定——三种用途各给一个：
/// </para>
/// <list type="bullet">
/// <item>等玩家出招：交出了命令（或战斗已经结束）就收；</item>
/// <item>播动画：行军、挥击、受击播完就收（<c>BattleScene.BeginActionAnimation</c> 那一对）；</item>
/// <item>看结果窗：结果窗关掉就收（<c>DialogBattleSummary</c>）。</item>
/// </list>
/// <para>
/// 输入不必经 <see cref="SetCursor"/> 这一套：战场自己读 <see cref="LocalEvent"/>（宿主的泵每帧都喂过），
/// 与它在 <c>--battle</c> 冒烟里跑时一模一样。
/// </para>
/// </remarks>
internal sealed class BattleScreenModal : IModalScreen
{
    private readonly BattleScene _scene;
    private readonly Func<BattleScene, bool> _isFinished;

    /// <summary>Wraps a battlefield with the predicate that says when the host may stop pumping it.</summary>
    /// <param name="scene">The battlefield to draw and feed.</param>
    /// <param name="isFinished">Whether the window has reached what the caller is waiting for.</param>
    /// <exception cref="ArgumentNullException">The scene or the predicate is <see langword="null"/>.</exception>
    public BattleScreenModal(BattleScene scene, Func<BattleScene, bool> isFinished)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(isFinished);

        _scene = scene;
        _isFinished = isFinished;
    }

    /// <inheritdoc />
    public bool IsFinished => _isFinished(_scene);

    /// <summary>Ignored: the battlefield reads the cursor from <see cref="LocalEvent"/>.</summary>
    /// <param name="position">Cursor position in logical pixels.</param>
    public void SetCursor(Point? position)
    {
    }

    /// <summary>Ignored: the battlefield reads the left button from <see cref="LocalEvent"/>.</summary>
    /// <param name="down">Whether the button is now held.</param>
    public void SetLeftButton(bool down)
    {
    }

    /// <summary>Ignored: the battlefield reads the right button from <see cref="LocalEvent"/>.</summary>
    /// <param name="down">Whether the button is now held.</param>
    public void SetRightButton(bool down)
    {
    }

    /// <summary>Ignored: the battlefield reads the hotkeys from <see cref="LocalEvent"/>.</summary>
    /// <param name="hotKey">Key that was pressed.</param>
    public void HotKey(ButtonHotKey hotKey)
    {
    }

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame) => _scene.DrawFrame(default);

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds) => _scene.Update(elapsedSeconds);
}
