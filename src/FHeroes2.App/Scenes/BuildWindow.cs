using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// What the building dialog asks the scene to do next.
/// </summary>
public enum BuildAction
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>Close the dialog without building.</summary>
    Exit,

    /// <summary>The player pressed OKAY on a building they can pay for.</summary>
    Build,
}

/// <summary>
/// The building confirm dialog of a town, <c>Dialog::BuyBuilding()</c>
/// (<c>src/fheroes2/castle/buildinginfo.cpp:435-537</c>).
/// </summary>
/// <remarks>
/// <para>
/// 和魔法塔、盗贼公会一样，这一层只做「窗口本体」：素材取自 AGG，布局与绘制交给
/// <see cref="BuildDialogView"/>，动作由场景（<see cref="CastleScene"/>）执行——窗口不碰城堡，
/// 也不扣钱，这样场景能决定买完之后要不要重画城镇屏。
/// </para>
/// <para>
/// 右键在两个按钮上会要一段说明（<c>buildinginfo.cpp:553-558</c>）：OKAY 上是「为什么不能建」
/// （<see cref="BuildingInfo.GetConditionDescription"/>），CANCEL 上是「Exit this menu without doing
/// anything.」。窗口只回答「右键落在哪个按钮上」，说明框由场景摆放与关闭。
/// </para>
/// </remarks>
public sealed class BuildWindow
{
    private readonly BuildDialogView _view;

    private Point? _cursor;
    private DialogButton _lastButton = DialogButton.None;
    private bool _closePressed;
    private bool _confirmPressed;

    private BuildWindow(BuildDialogView view) => _view = view;

    /// <summary>Builds the dialog for one building.</summary>
    /// <param name="assets">The archives the sprites come from.</param>
    /// <param name="info">The building, its status and its price.</param>
    /// <param name="fonts">The game fonts: the white normal face and the small face.</param>
    /// <returns>The window, or <see langword="null"/> when an asset is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> or <paramref name="fonts"/> is <see langword="null"/>.</exception>
    public static BuildWindow? TryCreate(AssetBundle? assets, BuildingInfo info, GameFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(fonts);

        if (assets is null)
        {
            GameLog.Warn("The building dialog needs the archives, and they were not loaded.");

            return null;
        }

        if (SystemButtonArtwork.TryCreate(assets) is not { } buttons)
        {
            return null;
        }

        MapRace race = info.Castle.Race;

        if (BuildDialogArtwork.TryCreate(assets, race) is not { } artwork)
        {
            GameLog.Warn($"The artwork of the building dialog of '{info.Castle.Name}' is missing.");

            return null;
        }

        if (BuildDialogView.TryCreate(info, artwork, fonts.Normal, fonts.Small, buttons) is not { } view)
        {
            GameLog.Warn($"The building dialog of '{info.Castle.Name}' does not fit its frame.");

            return null;
        }

        return new BuildWindow(view);
    }

    /// <summary>The building the dialog is about.</summary>
    public BuildingInfo Info => _view.Info;

    /// <summary>Bounding box of the dialog, used to redraw the town screen around it.</summary>
    public Rect FrameArea => _view.FrameArea;

    /// <summary>Whether OKAY is enabled, <c>BuildingStatus::ALLOW_BUILD</c>.</summary>
    public bool OkayEnabled => _view.OkayEnabled;

    /// <summary>Records the pointer over the dialog.</summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor)
    {
        _cursor = cursor;
        _view.SetCursor(cursor);
    }

    /// <summary>Records the left button; the answer is read by <see cref="Update"/>.</summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down) => _lastButton = _view.SetLeftButton(down);

    /// <summary>
    /// Records the right button and answers which button's help it asks for,
    /// <c>le.isMouseRightButtonPressedInArea( button.area() )</c> (<c>buildinginfo.cpp:553-558</c>).
    /// </summary>
    /// <param name="down">Whether the right button is now held.</param>
    /// <returns>The button under the pointer on the press, <see cref="DialogButton.None"/> otherwise.</returns>
    public DialogButton SetRightButton(bool down)
    {
        if (!down || _cursor is not { } cursor)
        {
            return DialogButton.None;
        }

        if (_view.Buttons[0].Area.Contains(cursor))
        {
            return DialogButton.Ok;
        }

        return _view.Buttons[1].Area.Contains(cursor) ? DialogButton.Cancel : DialogButton.None;
    }

    /// <summary>Marks the close hot key, ESCAPE or CANCEL.</summary>
    public void PressClose() => _closePressed = true;

    /// <summary>Marks the confirm hot key, RETURN or OKAY.</summary>
    public void PressConfirm() => _confirmPressed = true;

    /// <summary>Consumes the events of this frame.</summary>
    /// <returns>What the caller should do next; the window does not close itself.</returns>
    public BuildAction Update()
    {
        BuildAction action = BuildAction.None;

        if (_confirmPressed)
        {
            action = _view.OkayEnabled ? BuildAction.Build : BuildAction.None;
        }
        else if (_closePressed)
        {
            action = BuildAction.Exit;
        }
        else
        {
            action = _lastButton switch
            {
                DialogButton.Ok => _view.OkayEnabled ? BuildAction.Build : BuildAction.None,
                DialogButton.Cancel => BuildAction.Exit,
                _ => BuildAction.None,
            };
        }

        _closePressed = false;
        _confirmPressed = false;
        _lastButton = DialogButton.None;

        return action;
    }

    /// <summary>Paints the dialog over the town screen.</summary>
    /// <param name="target">The indexed frame buffer of the scene.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _view.Draw(target);
    }
}
