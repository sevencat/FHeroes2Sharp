using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

using SDL3;

namespace FHeroes2.App.Modals;

/// <summary>
/// The town screen of a castle as a window of the adventure map, <c>Game::OpenCastleDialog()</c>
/// (<c>castle.cpp</c>): the reference blocks the hero action until the player leaves the town, and this class is
/// that blocking call on top of <see cref="CastleScene"/>.
/// </summary>
/// <remarks>
/// <para>
/// 城堡屏本身是场景（地图点进城走的是同一个 <see cref="CastleScene"/>），但英雄动作是在地图这一帧里跑完的，
/// 没有第二个场景位可以切，所以这里把它当窗口用：<see cref="IModalScreen.IsFinished"/> 跟着
/// <see cref="CastleScene.IsClosed"/> 走，关掉之后英雄动作接着往下跑（参考在
/// <c>OpenCastleDialog()</c> 返回后才把英雄放到城堡格上）。
/// </para>
/// <para>
/// 键盘只有 ESC 与回车这一小撮（<see cref="HotKey"/> 给的本来就只有这两个意思）：ESC 是 EXIT，回车是城里的
/// 默认按钮。其余按键（左右切城堡、城堡内的快捷键）走不到这里，那些要等城堡屏自己收键盘语义的那天。
/// </para>
/// </remarks>
internal sealed class CastleScreenModal : IModalScreen
{
    private readonly CastleScene _scene;

    /// <summary>Wraps an already built town screen.</summary>
    /// <param name="scene">The castle scene to run as a window.</param>
    /// <exception cref="ArgumentNullException">The scene is <see langword="null"/>.</exception>
    public CastleScreenModal(CastleScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        _scene = scene;
    }

    /// <inheritdoc />
    public bool IsFinished => _scene.IsClosed;

    /// <inheritdoc />
    public void SetCursor(Point? position) => _scene.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => _scene.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => _scene.SetRightButton(down);

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey)
    {
        switch (hotKey)
        {
            case ButtonHotKey.Cancel:
                _scene.HandleKey(SDL.Keycode.Escape, false);

                break;

            case ButtonHotKey.Okay:
                _scene.HandleKey(SDL.Keycode.Return, false);

                break;

            default:
                break;
        }
    }

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _scene.DrawFrame(default);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _scene.Update(elapsedSeconds);
    }
}
