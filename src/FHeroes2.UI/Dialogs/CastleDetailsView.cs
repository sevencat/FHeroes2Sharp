using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Gui;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Everything the editor's castle details window paints, in the order it has to be painted,
/// <c>castleDetailsDialog()</c> (<c>editor_castle_details_window.cpp:619-810</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这个类只负责画面，不处理输入：勾选框、军队条、十九个建筑格、状态栏一次画完，事件循环归 <c>a7l.3.2</c>。
/// 画面里没有动画也没有随时间变化的东西，所以「重画」就是老老实实从背景开始整块重画——与参考那套
/// 「先画一次、之后只 render 脏区」的做法不同，参考的脏区在端口里没有对应的显示层。
/// </para>
/// <para>
/// 顺序是参考的：背景 → 名字 → SET SPELLS → 两个勾选框 → RESTRICT → 军队条 → 建筑格 → 底部按钮与状态栏 →
/// 状态栏文字。状态栏文字必须最后：底部那一步会把 <c>CASLBAR</c> 盖在上面，先写就会被抹掉，所以每次
/// <see cref="Paint"/> 都要让 <see cref="StatusBar.Reset"/> 作废缓存。
/// </para>
/// <para>
/// 参考的勾选标记是 <c>fheroes2::MovableSprite</c>，由显示层自动跟着重绘；端口没有显示层，标记要由这里在
/// <see cref="EditorUiHelper.DrawCheckboxWithText"/> 之后自己贴上去，见 <see cref="PaintCheckMark"/>。
/// </para>
/// </remarks>
public sealed class CastleDetailsView
{
    /// <summary>Offset of the two upper checkboxes from the top of the active area, <c>:669</c> and <c>:673</c>.</summary>
    public const int AllowCastleOffsetY = 130;

    /// <summary>Offset of the Default Buildings checkbox, <c>:673</c>.</summary>
    public const int DefaultBuildingsOffsetY = AllowCastleOffsetY + 30;

    /// <summary>Offset of the army strip from the top of the active area, <c>:695</c>.</summary>
    public const int ArmyOffsetY = 311;

    /// <summary>Horizontal offset of a checkbox from the left edge of the right panel, <c>rightPartOffsetX + 10</c>.</summary>
    public const int CheckboxOffsetX = CastleDetailsFrame.RightPanelOffsetX + 10;

    /// <summary>Horizontal offset of the army bar inside the right panel, <c>:709</c>.</summary>
    public const int ArmyBarOffsetX = 33;

    /// <summary>Vertical offset of the army bar inside the right panel, <c>:709</c>.</summary>
    public const int ArmyBarOffsetY = 332;

    /// <summary>Horizontal offset of the captain's frame inside the right panel, <c>:756</c>.</summary>
    public const int CaptainFrameOffsetX = 33;

    /// <summary>Vertical offset of the captain's frame inside the right panel, <c>:756</c>.</summary>
    public const int CaptainFrameOffsetY = 232;

    /// <summary>Caption of the checkbox that lets the town be built, <c>gettext_noop( "Allow Castle build" )</c>.</summary>
    public const string AllowCastleText = "Allow Castle build";

    /// <summary>Caption of the checkbox that shows every building as built, <c>:673</c>.</summary>
    public const string DefaultBuildingsText = "Default Buildings";

    /// <summary>Caption of the checkbox that keeps the default defender army, <c>:696</c>.</summary>
    public const string DefaultArmyText = "Default Army";

    /// <summary>Caption of the army strip of a town, <c>:706</c>.</summary>
    public const string TownArmyText = "Town Army";

    /// <summary>Caption of the army strip of a castle, <c>:708</c>.</summary>
    public const string CastleArmyText = "Castle Army";

    private const int CaptionRightMargin = 10;

    private readonly CastleDetailsFrame _frame;
    private readonly CastleDetailsState _state;
    private readonly CastleBuildingCellArtwork _cellArtwork;
    private readonly Sprite _captainFrame;
    private readonly ArmyBar _armyBar;
    private readonly IArmyBarSprites _armySprites = new ArmyBarArtwork();
    private readonly Palette _gamePalette;
    private readonly bool _isEvilInterface;

    private readonly MovableSprite _allowCastleSign = new();
    private readonly MovableSprite _defaultBuildingsSign = new();
    private readonly MovableSprite _defaultArmySign = new();

    private StatusBar? _statusBar;
    private PixelBuffer? _statusBarOutput;
    private string _message = string.Empty;

    private CastleDetailsView(CastleDetailsFrame frame, CastleDetailsState state,
        CastleBuildingCellArtwork cellArtwork, Sprite captainFrame, Palette gamePalette, bool isEvilInterface)
    {
        _frame = frame;
        _state = state;
        _cellArtwork = cellArtwork;
        _captainFrame = captainFrame;
        _gamePalette = gamePalette;
        _isEvilInterface = isEvilInterface;

        Rect dialog = frame.DialogArea;

        _armyBar = new ArmyBar(state.Army, miniSprites: true, readOnly: false, canChange: true,
            saveLastTroop: false);
        _armyBar.SetTableSize(new Size(3, 2));
        _armyBar.SetItemsCountPerRow(new[] { 2, 3 });
        _armyBar.SetInBetweenItemsOffset(new Size(3, 3));
        _armyBar.SetRenderingOffset(
            new Point(dialog.X + ArmyBarOffsetX, dialog.Y + ArmyBarOffsetY));
    }

    /// <summary>The window frame the controls sit in.</summary>
    public CastleDetailsFrame Frame => _frame;

    /// <summary>The state the window paints.</summary>
    public CastleDetailsState State => _state;

    /// <summary>
    /// The area the panels of the window live in, <c>dialogRoi</c> (<c>editor_castle_details_window.cpp:383</c>):
    /// the mage guild sub-window and its pickers are placed in it.
    /// </summary>
    public Rect DialogArea => _frame.DialogArea;

    /// <summary>
    /// The strip the castle name is clicked in, empty when the castle is not a town (the name of a ruin is
    /// not editable, <c>:648-655</c>).
    /// </summary>
    public Rect NameArea => _frame.NameArea;

    /// <summary>The area of the Allow Castle checkbox, empty when the castle is not a town.</summary>
    public Rect AllowCastleArea { get; private set; } = Rect.Empty;

    /// <summary>The area of the Default Buildings checkbox.</summary>
    public Rect DefaultBuildingsArea { get; private set; } = Rect.Empty;

    /// <summary>The area of the Default Army checkbox, empty when the army is not the default one.</summary>
    public Rect DefaultArmyArea { get; private set; } = Rect.Empty;

    /// <summary>The strip the status message is centred in.</summary>
    public Rect StatusBarMessageArea => _frame.StatusBarMessageArea;

    /// <summary>The area of the six army slots, <c>armyBar.GetArea()</c>.</summary>
    public Rect ArmyBarArea => _armyBar.RenderingArea;

    /// <summary>Area of the OKAY button, <c>buttonOkay.area()</c>; empty when the frame has none.</summary>
    public Rect OkayArea => _frame.Okay?.Area ?? Rect.Empty;

    /// <summary>Area of the EXIT button, <c>buttonExit.area()</c>; empty when the frame has none.</summary>
    public Rect ExitArea => _frame.Exit?.Area ?? Rect.Empty;

    /// <summary>The area of the SET SPELLS button, the one that opens the mage guild sub-window.</summary>
    public Rect SpellsArea => _frame.Spells?.Area ?? Rect.Empty;

    /// <summary>
    /// The army strip itself, <c>armyBar</c> of the reference: the editor's window hands the events of the bar
    /// area to it directly (<c>armyBar.QueueEventProcessing( &amp;message )</c>, <c>:992-1003</c>), because nothing
    /// of the castle window answers the bar's own questions.
    /// </summary>
    public ArmyBar ArmyBar => _armyBar;

    /// <summary>
    /// Loads the artwork of the dialog.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <param name="state">The castle the window shows.</param>
    /// <param name="gamePalette">The game palette in video values, needed to blur the buildings of a
    /// non default castle.</param>
    /// <param name="isEvilInterface">Whether the gray <c>*_EVIL</c> artwork is used.</param>
    /// <returns>The view, or <see langword="null"/> when an archive or a frame is missing.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static CastleDetailsView? TryCreate(AssetBundle assets, CastleDetailsState state, Palette gamePalette,
        bool isEvilInterface)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (CastleDetailsFrame.TryCreate(assets, isEvilInterface) is not { } frame ||
            CastleBuildingCellArtwork.TryCreate(state.Race) is not { } cellArtwork ||
            TryReadCaptainFrame(assets) is not { } captainFrame)
        {
            return null;
        }

        return new CastleDetailsView(frame, state, cellArtwork, captainFrame, gamePalette, isEvilInterface);
    }

    /// <summary>
    /// Sets the line the status bar shows, <c>statusBar.ShowMessage()</c> of the reference.
    /// </summary>
    /// <param name="message">The message; the empty string clears the bar.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public void SetMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _message = message;
    }

    /// <summary>
    /// Paints the whole dialog, from the background to the status message, in the order of
    /// <c>:619-810</c>.
    /// </summary>
    /// <remarks>
    /// 每次都从 <see cref="CastleDetailsFrame.PaintBackdrop"/> 重画：参考靠显示层只刷脏区，端口没有这一层，
    /// 所以按钮的阴影、勾选标记这些「叠上去」的东西都必须跟着重画。按钮画的是此刻的状态，按压态由
    /// <see cref="UpdateButtonPressStates"/> 事先设好——这里不读事件。
    /// </remarks>
    /// <param name="output">Frame the dialog goes into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void Paint(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _frame.PaintBackdrop(output);
        _frame.PaintName(output, CastleDetailsLabels.GetCastleName(_state.Metadata.CustomName, _state.IsTown));
        _frame.PaintSpellsButton(output);

        PaintAllowCastle(output);
        PaintDefaultBuildings(output);

        _frame.PaintRestrictButton(output);

        PaintArmy(output);
        PaintBuildings(output);

        _frame.PaintBottomButtons(output);
        PaintStatusMessage(output, _message);
    }

    /// <summary>
    /// Sets the pressed look of the six buttons from the frame's input, the six <c>drawOnState()</c> calls of
    /// <c>:845-849</c> and <c>:876</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考把这一步夹在绘制与命中判定之间：每帧先 <c>drawOnState( isMouseLeftButtonPressedAndHeldInArea( area ) )</c>
    /// 再 <c>MouseClickLeft</c>。端口的 <see cref="Paint"/> 只画、
    /// <see cref="CastleDetailsInput.Process"/> 只判，所以按压态单拎出来：宿主每帧先调这里、再
    /// <see cref="Paint"/>，<see cref="ButtonBase.UpdatePressState"/> 本来就只改状态不画。
    /// </para>
    /// <para>
    /// RESTRICT 那颗不一样：限制模式开着的时候参考让它一直保持按下（<c>buildingRestriction || ...</c>，<c>:876</c>），
    /// 所以这里不看鼠标，直接 <see cref="ButtonBase.Press"/>。
    /// </para>
    /// <para>
    /// 六颗按钮都是第一次 <see cref="Paint"/> 时才造出来的，在那之前这里是空转。参考的按钮在循环之前就摆好了，
    /// 不过第一帧之前也没有人在看画面，所以这一帧的差别看不出来。
    /// </para>
    /// </remarks>
    /// <param name="events">Input state of the frame.</param>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public void UpdateButtonPressStates(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _frame.Okay?.UpdatePressState(events);
        _frame.Exit?.UpdatePressState(events);
        _frame.ResetName?.UpdatePressState(events);
        _frame.ResetArmy?.UpdatePressState(events);
        _frame.Spells?.UpdatePressState(events);

        if (_frame.Restrict is not { } restrict)
        {
            return;
        }

        if (_state.BuildingRestriction)
        {
            restrict.Press();

            return;
        }

        restrict.UpdatePressState(events);
    }

    /// <summary>
    /// Actually paints the status line set by <see cref="SetMessage"/>, <c>:807-810</c>.
    /// </summary>
    /// <remarks>
    /// 状态栏绑在第一次用到的那个缓冲上（参考的 <c>Display</c> 也是单例），之后换缓冲不会自动跟着走。
    /// 每次调用都 <see cref="StatusBar.Reset"/>，因为 <see cref="CastleDetailsFrame.PaintBottomButtons"/>
    /// 已经把消息底下的 <c>CASLBAR</c> 重画过一遍了。
    /// </remarks>
    /// <param name="output">Frame the message goes into.</param>
    /// <param name="message">The message; empty means nothing to paint.</param>
    /// <returns>The dirty area, <see cref="Rect.Empty"/> when there was nothing to paint.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Rect PaintStatusMessage(PixelBuffer output, string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(message);

        if (_statusBar is null || !ReferenceEquals(_statusBarOutput, output))
        {
            _statusBar = new StatusBar(output);
            _statusBar.SetRoi(StatusBarMessageArea);
            _statusBarOutput = output;
        }

        _statusBar.Reset();

        return _statusBar.ShowMessage(message);
    }

    /// <summary>
    /// Draws the Allow Castle checkbox, which only a town has, <c>:664-671</c>.
    /// </summary>
    /// <param name="output">Frame the checkbox goes into.</param>
    private void PaintAllowCastle(PixelBuffer output)
    {
        if (!_state.IsTown)
        {
            AllowCastleArea = Rect.Empty;
            _allowCastleSign.Hide();

            return;
        }

        Point position = new(_frame.DialogArea.X + CheckboxOffsetX, _frame.DialogArea.Y + AllowCastleOffsetY);

        AllowCastleArea = EditorUiHelper.DrawCheckboxWithText(_allowCastleSign, AllowCastleText, output,
            position.X, position.Y, _isEvilInterface, CaptionWidth);

        if (_state.AllowCastle)
        {
            _allowCastleSign.Show();
        }
        else
        {
            _allowCastleSign.Hide();
        }

        PaintCheckMark(output, _allowCastleSign);
    }

    /// <summary>
    /// Draws the Default Buildings checkbox, <c>:673-683</c>.
    /// </summary>
    /// <param name="output">Frame the checkbox goes into.</param>
    private void PaintDefaultBuildings(PixelBuffer output)
    {
        Point position = new(_frame.DialogArea.X + CheckboxOffsetX, _frame.DialogArea.Y + DefaultBuildingsOffsetY);

        DefaultBuildingsArea = EditorUiHelper.DrawCheckboxWithText(_defaultBuildingsSign, DefaultBuildingsText,
            output, position.X, position.Y, _isEvilInterface, CaptionWidth);

        // 勾上「默认建筑」时参考把标记藏起来：这一格亮的是「已经全是默认建筑」这件事。
        if (_state.CustomBuildings)
        {
            _defaultBuildingsSign.Hide();
        }
        else
        {
            _defaultBuildingsSign.Show();
        }

        PaintCheckMark(output, _defaultBuildingsSign);
    }

    /// <summary>
    /// Draws the army strip: either the Default Army checkbox of a neutral castle, or the "Town Army" /
    /// "Castle Army" caption, and then the six slots, <c>:694-722</c>.
    /// </summary>
    /// <param name="output">Frame the strip goes into.</param>
    private void PaintArmy(PixelBuffer output)
    {
        Rect dialog = _frame.DialogArea;
        Point position = new(dialog.X + CheckboxOffsetX, dialog.Y + ArmyOffsetY);

        if (_state.IsNeutral)
        {
            DefaultArmyArea = EditorUiHelper.DrawCheckboxWithText(_defaultArmySign, DefaultArmyText, output,
                position.X, position.Y, _isEvilInterface, CaptionWidth);

            if (_state.DefaultArmy)
            {
                _defaultArmySign.Show();
            }
            else
            {
                _defaultArmySign.Hide();
            }

            PaintCheckMark(output, _defaultArmySign);
        }
        else
        {
            DefaultArmyArea = Rect.Empty;
            _defaultArmySign.Hide();

            int rightPartWidth = dialog.Width - CastleDetailsFrame.RightPanelOffsetX;
            Text caption = new(_state.IsTown ? TownArmyText : CastleArmyText, FontType.NormalWhite());

            // 标题按右侧面板居中，而不是按勾选框的左边距对齐。
            caption.DrawInRoi(
                dialog.X + CastleDetailsFrame.RightPanelOffsetX + ((rightPartWidth - caption.Width()) / 2),
                position.Y + 4, output, dialog);
        }

        _armyBar.Redraw(output, _armySprites);
    }

    /// <summary>
    /// Draws the nineteen building slots and the captain's frame, <c>:751-768</c>.
    /// </summary>
    /// <remarks>
    /// 参考每帧都重设格子位置，端口照做：位置由 <see cref="CastleDetailsState.LayoutCells"/> 一次算好，
    /// 但队长框要压在队长格底下，所以边框先画、格子后画。
    /// </remarks>
    /// <param name="output">Frame the slots go into.</param>
    private void PaintBuildings(PixelBuffer output)
    {
        Rect dialog = _frame.DialogArea;

        _state.LayoutCells(dialog.X, dialog.Y);

        ImageOps.BlitSprite(output,
            new Point(dialog.X + CastleDetailsFrame.RightPanelOffsetX + CaptainFrameOffsetX,
                dialog.Y + CaptainFrameOffsetY), _captainFrame);

        // 「不是默认建筑」时参考才给格子加上「自定义」的样子。
        foreach (CastleBuildingCell cell in _state.Cells)
        {
            cell.Draw(output, _cellArtwork, _gamePalette, _state.CustomBuildings);
        }
    }

    /// <summary>
    /// Blits a shown check mark, because the port has no display to draw movable sprites by itself.
    /// </summary>
    /// <param name="output">Frame the mark goes into.</param>
    /// <param name="sign">The mark, hidden or shown.</param>
    private static void PaintCheckMark(PixelBuffer output, MovableSprite sign)
    {
        if (sign.IsHidden || sign.Sprite is not { } sprite)
        {
            return;
        }

        ImageOps.BlitSprite(output, new Point(sign.X, sign.Y), sprite);
    }

    /// <summary>Width a checkbox caption may take inside the right panel.</summary>
    private int CaptionWidth => _frame.DialogArea.Width - CastleDetailsFrame.RightPanelOffsetX - CaptionRightMargin;

    /// <summary>
    /// Reads the empty building frame the captain's slot is cut out of, frame 0 of <c>BLDGXTRA</c>.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <returns>The frame, or <see langword="null"/> when it is missing.</returns>
    private static Sprite? TryReadCaptainFrame(AssetBundle assets) =>
        assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Bldgxtra)) is { } file && file.Count > 0
            ? file.Sprites[0]
            : null;
}
