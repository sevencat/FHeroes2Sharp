using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The unit information window, <c>Dialog::ArmyInfo()</c> (<c>dialog_armyinfo.cpp:518-712</c>), run by
/// <see cref="GameHost.RunModal"/>.
/// </summary>
/// <remarks>
/// <para>
/// 参考这个窗口自己转循环：每帧画一遍、跑 <c>ProcessEvents</c>、弹它自己要弹的消息框。端口把循环留给帧泵，窗口
/// 只回答「怎么关的、答案是什么」——退出按钮回 <see cref="DialogButton.Cancel"/>，升级回
/// <see cref="DialogButton.Upgrade"/>，解散回 <see cref="DialogButton.Dismiss"/>，都交给调用方
/// <see cref="ArmyBar.ApplyInfoAnswer"/>。
/// </para>
/// <para>
/// 三个消息框（升级、买不起、解散）按参考就地摞在这一层里，见 <c>_inner</c>；右键提示也走消息框
/// （参考的 <c>showStandardTextMessage( ..., 0 )</c> 是无按钮浮窗，端口用 OK 框代替）。
/// </para>
/// <para>
/// <b>没有单位精灵</b>：参考画的是战斗动画的空闲帧（<c>troop.GetMonster()</c> 的 <c>Idle</c>），端口的怪兽动画
/// 还没转，所以 <see cref="ArmyInfoDialog.Draw"/> 拿到的是 <see langword="null"/>，窗口只有文字与属性块。
/// </para>
/// <para>
/// 没有按钮的那种（<c>Dialog::ArmyInfo( troop, 0 )</c>，右键按住时弹的那个）参考在右键松开时关掉，
/// 端口照样用 <see cref="LocalEvent.IsMouseRightButtonPressed"/> 判断。
/// </para>
/// </remarks>
internal sealed class ArmyInfoModal : IModalScreen
{
    /// <summary>The heading of the upgrade question, the <c>_( "Upgrade" )</c> of the reference.</summary>
    private const string UpgradeTitle = "Upgrade";

    private readonly ArmyInfoDialog _dialog;
    private readonly ArmyInfoArtwork _artwork;
    private readonly ArmyInfoLayout _layout;
    private readonly IGlyphSource _normalFont;
    private readonly IGlyphSource _smallFont;
    private readonly IGlyphSource _nameFont;
    private readonly Sprite? _unitSprite;
    private readonly ButtonSprite _exitButton;
    private readonly ButtonSprite? _upgradeButton;
    private readonly ButtonSprite? _dismissButton;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private ArmyInfoAction _question;
    private ButtonHotKey? _hotKey;

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="dialog">The model of the window.</param>
    /// <param name="artwork">The window's sprites and the button frames.</param>
    /// <param name="layout">Where everything goes.</param>
    /// <param name="fonts">The fonts: white for the attribute block, small white for the description.</param>
    /// <param name="host">Where the message boxes are built from.</param>
    /// <param name="unitSprite">The idle frame of the unit, or <see langword="null"/> for a window without one.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ArmyInfoModal(ArmyInfoDialog dialog, ArmyInfoArtwork artwork, ArmyInfoLayout layout, GameFonts fonts,
        EditorSceneHost host, Sprite? unitSprite = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(host);

        _dialog = dialog;
        _artwork = artwork;
        _layout = layout;
        _host = host;
        _normalFont = fonts.White;
        _smallFont = fonts.SmallWhite;
        _nameFont = fonts.Yellow;
        _unitSprite = unitSprite;

        // 顺序与 ArmyInfoDialog.CreateButtons 一致：升级、解散、退出；缺哪个就少一个。
        List<ButtonSprite> buttons = dialog.CreateButtons(layout, artwork);
        int next = 0;

        if (dialog.CanUpgrade && layout.UpgradeButton is not null && artwork.UpgradeButton is not null)
        {
            _upgradeButton = buttons[next++];
        }

        if (dialog.CanDismiss && layout.DismissButton is not null && artwork.DismissButton is not null)
        {
            _dismissButton = buttons[next++];
        }

        _exitButton = buttons[next];
    }

    /// <summary>The model being run, for callers that need the troop it shows.</summary>
    public ArmyInfoDialog Dialog => _dialog;

    /// <summary>The area of the exit button.</summary>
    public Rect ExitArea => _exitButton.Area;

    /// <summary>The area of the upgrade button, or <see langword="null"/> when the window has none.</summary>
    public Rect? UpgradeArea => _upgradeButton?.Area;

    /// <summary>The area of the dismiss button, or <see langword="null"/> when the window has none.</summary>
    public Rect? DismissArea => _dismissButton?.Area;

    /// <summary>
    /// The button that closed the window: <see cref="DialogButton.Upgrade"/>, <see cref="DialogButton.Dismiss"/> or
    /// <see cref="DialogButton.Cancel"/>; <see cref="DialogButton.None"/> while it is still open.
    /// </summary>
    public DialogButton Answer { get; private set; }

    /// <summary>Whether a message box is stacked above the window.</summary>
    internal bool HasInnerWindow => _inner.Count > 0;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>The window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <inheritdoc />
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => Top?.SetRightButton(down);

    /// <inheritdoc />
    /// <remarks>
    /// 消息框还开着时热键归它；没有内层窗时先记下，下一帧和鼠标一起交给 <see cref="ArmyInfoDialog.ProcessEvents"/>——
    /// 参考的关窗就是 <c>Game::HotKeyCloseWindow()</c>，回车在这里不算数。
    /// </remarks>
    public void HotKey(ButtonHotKey hotKey)
    {
        if (Top is { } inner)
        {
            inner.HotKey(hotKey);

            return;
        }

        _hotKey = hotKey;
    }

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier) => Top?.PressKey(key, modifier);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _dialog.Draw(frame.Pixels, _layout, _artwork, _normalFont, _smallFont, _nameFont, _unitSprite);

        _exitButton.Draw(frame.Pixels);
        _upgradeButton?.Draw(frame.Pixels);
        _dismissButton?.Draw(frame.Pixels);

        Top?.Draw(frame);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (IsFinished)
        {
            _hotKey = null;

            return;
        }

        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
                ResolveBox(inner is MessageBoxModal box ? box.Answer : DialogButton.None);
            }

            return;
        }

        // 没有按钮的那种窗口靠右键按住：参考 while ( le.isMouseRightButtonPressed() ) 松开就关。
        if (!_dialog.HasButtons)
        {
            if (!LocalEvent.Current.IsMouseRightButtonPressed)
            {
                Answer = DialogButton.Cancel;
                IsFinished = true;
            }

            _hotKey = null;

            return;
        }

        bool closeRequested = _hotKey == ButtonHotKey.Cancel;
        _hotKey = null;

        var hotKeys = new ArmyInfoHotKeys(
            Upgrade: GameHotKeys.HotKeyPressEvent(HotKeyEvent.ArmyUpgradeTroop),
            Dismiss: GameHotKeys.HotKeyPressEvent(HotKeyEvent.ArmyDismiss),
            CloseWindow: closeRequested);

        ArmyInfoFrame result = _dialog.ProcessEvents(LocalEvent.Current, _exitButton, _upgradeButton,
            _dismissButton, hotKeys);

        switch (result.Action)
        {
            case ArmyInfoAction.Exit:
                Answer = DialogButton.Cancel;
                IsFinished = true;

                return;

            case ArmyInfoAction.Upgrade:
                Ask(ArmyInfoAction.Upgrade, UpgradeTitle, ArmyInfoDialog.UpgradePrompt);

                return;

            case ArmyInfoAction.UpgradeDisabled:
                // 参考这里带资源图标与升级花费；端口还没有资源消息框，只弹文字。
                Push(_host.TryCreateMessageBox(UpgradeTitle, ArmyInfoDialog.UpgradeDisabledPrompt));

                return;

            case ArmyInfoAction.Dismiss:
                Ask(ArmyInfoAction.Dismiss, _dialog.PluralName, ArmyInfoDialog.DismissPrompt);

                return;
        }

        if (result.Tooltip is { } tooltip)
        {
            ArmyInfoTooltip hint = ArmyInfoDialog.GetTooltip(tooltip);

            Push(_host.TryCreateMessageBox(hint.Title, hint.Text));
        }
    }

    /// <summary>Asks a YES/NO question; YES closes the window with the answer it stands for.</summary>
    /// <param name="question">Which question: <see cref="ArmyInfoAction.Upgrade"/> or <see cref="ArmyInfoAction.Dismiss"/>.</param>
    /// <param name="title">Title of the box.</param>
    /// <param name="body">Body of the box.</param>
    private void Ask(ArmyInfoAction question, string title, string body)
    {
        MessageBoxModal? box = _host.TryCreateMessageBox(title, body, DialogButton.Yes | DialogButton.No);

        if (box is null)
        {
            GameLog.Warn($"The unit information window has to ask \"{body}\", and the box is missing.");

            return;
        }

        _question = question;
        _inner.Push(box);
    }

    /// <summary>Reads the answer of the box that just closed.</summary>
    /// <remarks>
    /// 只有 YES 才真的关窗：参考里 NO 只是消息框关掉，窗口继续转；提示框（<see cref="ArmyInfoAction.None"/>）问完
    /// 就继续。
    /// </remarks>
    /// <param name="answer">The button the box was closed with.</param>
    private void ResolveBox(DialogButton answer)
    {
        ArmyInfoAction question = _question;
        _question = ArmyInfoAction.None;

        if (question == ArmyInfoAction.None || answer != DialogButton.Yes)
        {
            return;
        }

        Answer = question == ArmyInfoAction.Upgrade ? DialogButton.Upgrade : DialogButton.Dismiss;
        IsFinished = true;
    }

    /// <summary>Stacks a box above the window; a missing one is logged and the window stays usable.</summary>
    /// <param name="box">The box, or <see langword="null"/> when the fonts are missing.</param>
    private void Push(MessageBoxModal? box)
    {
        if (box is null)
        {
            GameLog.Warn("The unit information window has a message to show, and the box is missing.");

            return;
        }

        _inner.Push(box);
    }
}
