using System.Globalization;

using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The "set the amount" dialog a click on a resource icon asks the host to open, <c>Dialog::SelectCount()</c>
/// (<c>editor_sphinx_window.cpp:301</c>).
/// </summary>
/// <param name="ResourceType">One of the <see cref="Resource"/> flags.</param>
/// <param name="Value">The amount the funds hold right now.</param>
/// <param name="Dialog">The dialog to run; its <see cref="SelectCountDialog.Result"/> is the answer.</param>
public readonly record struct SphinxResourceRequest(int ResourceType, int Value, SelectCountDialog Dialog);

/// <summary>
/// The editor's sphinx window, <c>Editor::openSphinxWindow()</c> (<c>editor_sphinx_window.cpp:151-471</c>).
/// </summary>
/// <remarks>
/// <para>
/// 550×400 的标准窗（250×180 的谜题区、250×155 的答案列表、250×99 的奖励面板）：标题「Sphinx:」居中，谜题区与
/// 答案列表并排，列表右侧是滚动条，列表底下三个 <c>CELLWIN</c> 小按钮（增、改、删），再往下是「Reward:」、神器槽与
/// 删除按钮、资源面板，最后是标准的 OKAY/CANCEL。
/// </para>
/// <para>
/// 参考在窗口里直接调 <c>Dialog::inputString</c> / <c>Dialog::selectArtifact</c> / <c>Dialog::SelectCount</c>，
/// 端口把前两样交回宿主：本类只发 <see cref="RiddleRequested"/>、<see cref="AnswerAddRequested"/>、
/// <see cref="AnswerEditRequested"/>、<see cref="ArtifactRequested"/> 与 <see cref="ResourceCountRequest"/>，
/// 宿主弹完再喂 <see cref="SetRiddle"/>、<see cref="AddAnswer"/>、<see cref="ReplaceAnswer"/>、
/// <see cref="SetArtifact"/>、<see cref="SetResourceCount"/>。
/// </para>
/// <para>
/// 答案有两条规矩：最多 <see cref="LongestAnswer"/> 个字；不许与列表里已有的答案重复（重复时给
/// <see cref="Popup"/> 提示、不写回）。
/// </para>
/// </remarks>
public sealed class EditorSphinxWindow : ListBox<string>
{
    /// <summary>Width of the riddle area, <c>riddleArea.width</c> (<c>:62</c>).</summary>
    public const int RiddleAreaWidth = 250;

    /// <summary>Height of the riddle area, <c>riddleArea.height</c> (<c>:62</c>).</summary>
    public const int RiddleAreaHeight = 180;

    /// <summary>Width of the answer area, <c>answerArea.width</c> (<c>:64</c>).</summary>
    public const int AnswerAreaWidth = 250;

    /// <summary>Height of the answer area, <c>answerArea.height</c> (<c>:64</c>).</summary>
    public const int AnswerAreaHeight = 155;

    /// <summary>Gap around the elements, <c>elementOffset</c> (<c>:66</c>).</summary>
    public const int ElementOffset = 9;

    /// <summary>Room left under the list, <c>listAreaHeightDeduction</c> (<c>:68</c>).</summary>
    public const int ListAreaHeightDeduction = 8;

    /// <summary>Longest answer the author may type, <c>longestAnswer</c> (<c>:70</c>).</summary>
    public const int LongestAnswer = 64;

    /// <summary>Width of the window's active area, <c>riddleArea.width + answerArea.width + 50</c> (<c>:156</c>).</summary>
    public const int ActiveWidth = RiddleAreaWidth + AnswerAreaWidth + 50;

    /// <summary>Height of the window's active area, <c>riddleArea.height + 220</c> (<c>:156</c>).</summary>
    public const int ActiveHeight = RiddleAreaHeight + 220;

    /// <summary>Rows on screen at once, <c>SetAreaMaxItems( 10 )</c> (<c>:204</c>).</summary>
    public const int MaxVisibleItemCount = 10;

    /// <summary>Height of a scroll button, <c>topPartHeight</c> (<c>:197</c>).</summary>
    public const int ScrollButtonHeight = 19;

    /// <summary>Left inset of a row's text, the <c>+ 5</c> of <c>:92</c>.</summary>
    public const int RowTextInsetX = 5;

    /// <summary>Top inset of a row's text, the <c>+ 5</c> of <c>:92</c>.</summary>
    public const int RowTextInsetY = 5;

    /// <summary>Room a row text leaves on both sides, the <c>10</c> of <c>:91</c>.</summary>
    public const int RowTextInsetTotal = 10;

    /// <summary>Height of the resource panel, the <c>99</c> of <c>:246</c>.</summary>
    public const int ResourcePanelHeight = 99;

    /// <summary>Smallest amount a resource may be set to, the <c>0</c> of <c>:301</c>.</summary>
    public const int ResourceAmountMinimum = 0;

    /// <summary>Largest amount a resource may be set to, the <c>1000000</c> of <c>:301</c>.</summary>
    public const int ResourceAmountMaximum = 1_000_000;

    /// <summary>Longest riddle the author may type, <c>Maps::Map_Format::messageCharLimit</c> (<c>:317</c>).</summary>
    public const int MessageCharLimit = 999;

    /// <summary>Label of the riddle, <c>_( "Riddle:" )</c> (<c>:167</c>).</summary>
    public const string RiddleLabel = "Riddle:";

    /// <summary>Label of the answer list, <c>_( "Answers:" )</c> (<c>:183</c>).</summary>
    public const string AnswersLabel = "Answers:";

    /// <summary>Label of the reward, <c>_( "Reward:" )</c> (<c>:227</c>).</summary>
    public const string RewardLabel = "Reward:";

    /// <summary>The name in the window's title, <c>MP2::StringObject( MP2::OBJ_SPHINX )</c> (<c>:162</c>).</summary>
    public const string ObjectName = "Sphinx";

    /// <summary>Label of the "set the amount" dialog, <c>_( "Set %{resource-type} Count:" )</c> (<c>:298</c>).</summary>
    public const string ResourceCountLabelFormat = "Set {0} Count:";

    /// <summary>Title of the duplicate answer message, <c>_( "Answer" )</c> (<c>:332</c>).</summary>
    public const string DuplicateAnswerTitle = "Answer";

    /// <summary>Body of the duplicate answer message, <c>_( "This answer exists in the list." )</c>.</summary>
    public const string DuplicateAnswerMessage = "This answer exists in the list.";

    /// <summary>Title of the CANCEL button's message, <c>:414</c>.</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Body of the CANCEL button's message.</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Title of the OKAY button's message, <c>:417</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Body of the OKAY button's message.</summary>
    public const string OkayMessage = "Click to save the Sphinx properties.";

    /// <summary>Title of the "add" button's message, <c>:420</c>.</summary>
    public const string AddTitle = "Add Answer";

    /// <summary>Body of the "add" button's message.</summary>
    public const string AddMessage = "Add an additional answer for the question.";

    /// <summary>Title of the "edit" button's message, <c>:423</c>.</summary>
    public const string EditTitle = "Edit Answer";

    /// <summary>Body of the "edit" button's message.</summary>
    public const string EditMessage = "Edit an existing answer for the question.";

    /// <summary>Title of the "delete" button's message, <c>:426</c>.</summary>
    public const string DeleteTitle = "Delete Answer";

    /// <summary>Body of the "delete" button's message.</summary>
    public const string DeleteMessage = "Delete an existing answer for the question.";

    /// <summary>Title of the riddle's message, <c>_( "Riddle" )</c> (<c>:429</c>).</summary>
    public const string RiddleTitle = "Riddle";

    /// <summary>Title of the artifact's message, <c>_( "Artifact" )</c> (<c>:445</c>).</summary>
    public const string ArtifactTitle = "Artifact";

    /// <summary>Body of the artifact's message when the slot is empty.</summary>
    public const string NoArtifactMessage = "No artifact will be given as a reward.";

    /// <summary>Title of the artifact delete button's message, <c>:449</c>.</summary>
    public const string DeleteArtifactTitle = "Delete Artifact";

    /// <summary>Body of the artifact delete button's message.</summary>
    public const string DeleteArtifactMessage = "Delete an artifact from the reward.";

    /// <summary>Title of the resource panel's message, <c>_( "Resources" )</c> (<c>:453</c>).</summary>
    public const string ResourcesTitle = "Resources";

    /// <summary>Body of the resource panel's message when it holds nothing, <c>:453</c>.</summary>
    public const string NoResourcesMessage = "No resources will be given as a reward.";

    /// <summary>Body of the resource panel's message when it holds something, <c>:457</c>.</summary>
    public const string ResourcesMessage = "Resources will be given as a reward.";

    private static readonly int[] ResourceTypes =
    [
        Resource.Wood, Resource.Sulfur, Resource.Crystal, Resource.Mercury, Resource.Ore, Resource.Gems, Resource.Gold
    ];

    private readonly List<string> _answers = [];

    private readonly StandardWindow _window;
    private readonly EditorSphinxArtwork _artwork;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _bodyFont;
    private readonly Rect _riddleRoi;
    private readonly Rect _answerRoi;
    private readonly Rect _resourceRoi;
    private readonly Rect _artifactRoi;
    private readonly Rect[] _individualResourceRoi = new Rect[7];
    private readonly int _scrollBarX;
    private readonly int _riddleLabelY;
    private readonly int _answerLabelY;
    private readonly int _rewardLabelY;
    private readonly Point _titlePosition;
    private readonly int _titleWidth;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;
    private readonly ButtonSprite _add;
    private readonly ButtonSprite _edit;
    private readonly ButtonSprite _delete;
    private readonly ButtonSprite _deleteArtifact;

    private string _riddle;
    private ArtifactType _artifact;
    private int _artifactMetadata;
    private Funds _resources;

    private EditorSphinxWindow(SphinxMetadata metadata, EditorSphinxArtwork artwork, Size screen,
        IGlyphSource headerFont, IGlyphSource bodyFont)
        : base(new Point(0, 0))
    {
        _artwork = artwork;
        _headerFont = headerFont;
        _bodyFont = bodyFont;
        _riddle = metadata.Riddle;
        _artifact = (ArtifactType)metadata.Artifact;
        _artifactMetadata = metadata.ArtifactMetadata;
        _resources = metadata.Resources;

        foreach (string answer in metadata.Answers)
        {
            _answers.Add(answer);
        }

        _window = StandardWindow.CreateCentered(new Size(ActiveWidth, ActiveHeight), renderBackground: true, screen);

        Rect active = _window.ActiveArea;

        // 参考 :160-185：标题离顶 9 像素居中，两个标签与各自的区域一起从标题下 9 像素处往下排。
        int titleWidth = GlyphText.Measure(headerFont, ObjectName + ":");
        int titleY = active.Y + ElementOffset;
        int labelY = titleY + Math.Max(1, headerFont.LineHeight) + ElementOffset;
        int labelHeight = Math.Max(1, bodyFont.LineHeight);

        _titlePosition = new Point(active.X + ((active.Width - titleWidth) / 2), titleY);
        _titleWidth = titleWidth;
        _riddleLabelY = labelY;
        _answerLabelY = labelY;
        _riddleRoi = new Rect(active.X + ElementOffset, labelY + labelHeight, RiddleAreaWidth, RiddleAreaHeight);
        _answerRoi = new Rect(_riddleRoi.Right + ElementOffset, labelY + labelHeight, AnswerAreaWidth,
            AnswerAreaHeight);

        // 参考 :225-232：跳过谜题区的高度再空 9 像素，「Reward:」下面是神器槽与资源面板。
        _rewardLabelY = labelY + labelHeight + RiddleAreaHeight + ElementOffset;
        _artifactRoi = new Rect(_riddleRoi.X + ((RiddleAreaWidth - artwork.ArtifactFrame.Width) / 2),
            _rewardLabelY + labelHeight, artwork.ArtifactFrame.Width, artwork.ArtifactFrame.Height);
        _resourceRoi = new Rect(_answerRoi.X, _rewardLabelY + labelHeight, AnswerAreaWidth, ResourcePanelHeight);

        // 参考 :89、:197 把滚动条放在窗口右边缘（dialogRoi.width - 15，再右移 1 像素）。
        // 偏差（参考 :206 的槽宽照抄了 answerRoi.width）：照抄会从 536 起铺 250 宽、整条压在窗外，
        // 端口把槽收在窗口内，宽度只留滚动条本身的 14 像素。
        _scrollBarX = active.Right - 14;

        SetTopLeft(_answerRoi.Position);
        SetAreaItems(new Rect(_answerRoi.X, _answerRoi.Y, _answerRoi.Width,
            _answerRoi.Height - ListAreaHeightDeduction));
        SetAreaMaxItems(MaxVisibleItemCount);
        SetScrollButtonUp(new ButtonSprite(artwork.List.ScrollBarFrames[0], artwork.List.ScrollBarFrames[1], null,
            _scrollBarX + 1, _answerRoi.Y + 1));
        SetScrollButtonDown(new ButtonSprite(artwork.List.ScrollBarFrames[2], artwork.List.ScrollBarFrames[3], null,
            _scrollBarX + 1, _answerRoi.Bottom - 15));
        SetScrollBarArea(new Rect(_scrollBarX + 3, _answerRoi.Y + ScrollButtonHeight, 10,
            _answerRoi.Height - 21 - ScrollButtonHeight));
        SetScrollBarImage(artwork.List.ScrollBarFrames[4]);

        // 参考 :210-222：三个小按钮隔一行摆在答案区底下，中间那个落在 ( width - 3 * buttonWidth ) / 2 + buttonWidth。
        int buttonWidth = artwork.List.ButtonWidth;
        int buttonOffset = ((AnswerAreaWidth - (3 * buttonWidth)) / 2) + buttonWidth;
        int buttonY = _answerRoi.Bottom + 5;

        _add = new ButtonSprite(artwork.List.Add.Released, artwork.List.Add.Pressed);
        _add.SetPosition(_answerRoi.X, buttonY);

        _edit = new ButtonSprite(artwork.List.Edit.Released, artwork.List.Edit.Pressed);
        _edit.SetPosition(_answerRoi.X + buttonOffset, buttonY);

        _delete = new ButtonSprite(artwork.List.Delete.Released, artwork.List.Delete.Pressed);
        _delete.SetPosition(_answerRoi.Right - buttonWidth, buttonY);

        // 参考 :243：删除神器的按钮在槽下面 5 像素，水平居中。
        _deleteArtifact = new ButtonSprite(artwork.List.Delete.Released, artwork.List.Delete.Pressed);
        _deleteArtifact.SetPosition(_artifactRoi.X + ((_artifactRoi.Width - buttonWidth) / 2),
            _artifactRoi.Bottom + 5);

        _okay = new ButtonSprite(artwork.List.Okay.Released, artwork.List.Okay.Pressed);
        _cancel = new ButtonSprite(artwork.List.Cancel.Released, artwork.List.Cancel.Pressed);

        Point okayPosition = _window.RenderPosition(new Point(20, 7), new Size(_okay.Area.Width, _okay.Area.Height),
            WindowPadding.BottomLeft);
        Point cancelPosition = _window.RenderPosition(new Point(20, 7),
            new Size(_cancel.Area.Width, _cancel.Area.Height), WindowPadding.BottomRight);

        _okay.SetPosition(okayPosition.X, okayPosition.Y);
        _cancel.SetPosition(cancelPosition.X, cancelPosition.Y);

        // 七个资源图标落在哪儿由 RenderResources 决定，画布只是为了让它在建窗时把位置算出来（参考也是建窗时算的）。
        PixelBuffer measurement = new(ActiveWidth, ActiveHeight);
        EditorUiHelper.RenderResources(_resources, _resourceRoi, measurement, _individualResourceRoi);

        SetListContent(_answers);
        Unselect();
        UpdateScrollBarImage();
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="metadata">The sphinx; its fields are copied.</param>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="screen">Screen size, <c>display.width()</c>/<c>height()</c> (<c>:156</c>).</param>
    /// <param name="headerFont">The normal yellow font, also the font of the selected row.</param>
    /// <param name="bodyFont">The normal white font.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static EditorSphinxWindow Create(SphinxMetadata metadata, EditorSphinxArtwork artwork, Size screen,
        IGlyphSource headerFont, IGlyphSource bodyFont)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        return new EditorSphinxWindow(metadata, artwork, screen, headerFont, bodyFont);
    }

    /// <summary>The window the list sits in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The active area.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The rect of the riddle, <c>riddleRoi</c> (<c>:169</c>).</summary>
    public Rect RiddleArea => _riddleRoi;

    /// <summary>The rect of the answer list, <c>answerRoi</c> (<c>:180</c>).</summary>
    public Rect AnswerArea => _answerRoi;

    /// <summary>The rect of the reward's resource panel, <c>resourceRoi</c> (<c>:246</c>).</summary>
    public Rect ResourceArea => _resourceRoi;

    /// <summary>Where the icon of the rewarded artifact goes, <c>artifactRoi</c> (<c>:231</c>).</summary>
    public Rect ArtifactArea => _artifactRoi;

    /// <summary>The rects of the seven resource icons, <c>individualResourceRoi</c> (<c>:251</c>).</summary>
    public IReadOnlyList<Rect> ResourceIconAreas => _individualResourceRoi;

    /// <summary>The OKAY button.</summary>
    public ButtonSprite Okay => _okay;

    /// <summary>The CANCEL button.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>The "add" button.</summary>
    public ButtonSprite AddButton => _add;

    /// <summary>The "edit" button.</summary>
    public ButtonSprite EditButton => _edit;

    /// <summary>The "delete" button.</summary>
    public ButtonSprite DeleteButton => _delete;

    /// <summary>The button that empties the artifact slot, <c>buttonDeleteArtifact</c> (<c>:243</c>).</summary>
    public ButtonSprite DeleteArtifactButton => _deleteArtifact;

    /// <summary>The riddle as it stands, <c>metadata.riddle</c>.</summary>
    public string Riddle => _riddle;

    /// <summary>The answers as they stand, <c>metadata.answers</c>.</summary>
    public IReadOnlyList<string> Answers => _answers;

    /// <summary>The artifact as it stands, <see cref="ArtifactType.Unknown"/> for none.</summary>
    public ArtifactType Artifact => _artifact;

    /// <summary>Extra data of the artifact, the spell of a spell scroll.</summary>
    public int ArtifactMetadata => _artifactMetadata;

    /// <summary>The resources as they stand, <c>metadata.resources</c>.</summary>
    public Funds Resources => _resources;

    /// <summary>How the author left the window; reset by <see cref="Process"/>.</summary>
    public EditorListOutcome Result { get; private set; }

    /// <summary>A message the host has to show, or <see langword="null"/>.</summary>
    public DialogPopup? Popup { get; private set; }

    /// <summary>Whether the author clicked the riddle, so the host opens the input dialog (<c>:313</c>).</summary>
    public bool RiddleRequested { get; private set; }

    /// <summary>
    /// Whether the author clicked "add", so the host asks for a new answer (<c>:327-341</c>). What it gets typed goes
    /// to <see cref="AddAnswer"/>.
    /// </summary>
    public bool AnswerAddRequested { get; private set; }

    /// <summary>
    /// Whether the author asked to edit the selected answer, by double clicking it or clicking "edit"
    /// (<c>:343-365</c>). What the host gets typed goes to <see cref="ReplaceAnswer"/>.
    /// </summary>
    public bool AnswerEditRequested { get; private set; }

    /// <summary>
    /// Whether the author clicked the artifact, so the host opens its picker (<c>:376-403</c>); the answer comes back
    /// through <see cref="SetArtifact"/>.
    /// </summary>
    public bool ArtifactRequested { get; private set; }

    /// <summary>The "set the amount" dialog of a resource, or <see langword="null"/>.</summary>
    public SphinxResourceRequest? ResourceCountRequest { get; private set; }

    /// <summary>The answer under the selection, <c>answerList.GetCurrent()</c> (<c>:350</c>).</summary>
    public string? SelectedAnswer => IsSelected ? _answers[CurrentId] : null;

    /// <summary>The rect of the row at <paramref name="index"/>, as <see cref="ListBox{T}"/> lays the rows out.</summary>
    /// <param name="index">Zero for the first row.</param>
    /// <returns>The rect of the row.</returns>
    public Rect RowArea(int index) => new(ItemsArea.X,
        ItemsArea.Y + ((index * ItemsArea.Height) / MaxVisibleItemCount), ItemsArea.Width,
        ItemsArea.Height / MaxVisibleItemCount);

    /// <summary>Whether the last frame double clicked a row, <c>isDoubleClicked()</c> (<c>:115-118</c>).</summary>
    private bool IsDoubleClicked { get; set; }

    /// <summary>Replaces the riddle, <c>metadata.riddle = std::move( temp )</c> (<c>:318</c>).</summary>
    /// <param name="riddle">The text the author typed; it is capped to <see cref="MessageCharLimit"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="riddle"/> is <see langword="null"/>.</exception>
    public void SetRiddle(string riddle)
    {
        ArgumentNullException.ThrowIfNull(riddle);

        _riddle = riddle.Length > MessageCharLimit ? riddle[..MessageCharLimit] : riddle;
        NeedRedraw = true;
    }

    /// <summary>Appends an answer the author typed, the <c>answers.emplace_back()</c> of <c>:336</c>.</summary>
    /// <param name="answer">The answer; it is capped to <see cref="LongestAnswer"/>.</param>
    /// <returns><see langword="false"/> when the list already holds it; <see cref="Popup"/> then says so.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    public bool AddAnswer(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        string capped = answer.Length > LongestAnswer ? answer[..LongestAnswer] : answer;

        if (_answers.Contains(capped, StringComparer.Ordinal))
        {
            Popup = new DialogPopup(DuplicateAnswerTitle, DuplicateAnswerMessage);

            return false;
        }

        _answers.Add(capped);
        UpdateScrollBarImage();
        NeedRedraw = true;

        return true;
    }

    /// <summary>
    /// Writes a new text over the selected answer, the <c>answerList.GetCurrent() = std::move( temp )</c> of
    /// <c>:360</c>.
    /// </summary>
    /// <param name="answer">The new text; it is capped to <see cref="LongestAnswer"/>.</param>
    /// <returns>
    /// <see langword="false"/> when nothing is selected or another row already holds the text; <see cref="Popup"/>
    /// then says so.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    public bool ReplaceAnswer(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (!IsSelected)
        {
            return false;
        }

        string capped = answer.Length > LongestAnswer ? answer[..LongestAnswer] : answer;

        // 参考 :355：改的和原来一样不算重复。
        if (capped != _answers[CurrentId] && _answers.Contains(capped, StringComparer.Ordinal))
        {
            Popup = new DialogPopup(DuplicateAnswerTitle, DuplicateAnswerMessage);

            return false;
        }

        _answers[CurrentId] = capped;
        NeedRedraw = true;

        return true;
    }

    /// <summary>Drops the selected answer, <c>answerList.RemoveSelected()</c> (<c>:371</c>).</summary>
    /// <returns>The answer that was removed, or <see langword="null"/> when nothing was selected.</returns>
    public string? RemoveSelectedAnswer()
    {
        if (!IsSelected)
        {
            return null;
        }

        string removed = _answers[CurrentId];

        RemoveSelected();
        UpdateScrollBarImage();
        NeedRedraw = true;

        return removed;
    }

    /// <summary>Writes the artifact the author picked (<c>:393-394</c>).</summary>
    /// <param name="artifact">The artifact; an invalid one empties the slot.</param>
    /// <param name="metadata">Its extra data, the spell of a spell scroll.</param>
    public void SetArtifact(ArtifactType artifact, int metadata = 0)
    {
        bool isValid = ArtifactTypes.IsValid(artifact);

        _artifact = isValid ? artifact : ArtifactType.Unknown;
        _artifactMetadata = isValid ? metadata : 0;
        NeedRedraw = true;
    }

    /// <summary>Empties the artifact slot, the <c>metadata.artifact = 0</c> of <c>:405-406</c>.</summary>
    public void ClearArtifact() => SetArtifact(ArtifactType.Unknown);

    /// <summary>Writes the amount of one resource, the <c>*resourcePtr = temp</c> of <c>:302</c>.</summary>
    /// <param name="resourceType">One of the <see cref="Resource"/> flags.</param>
    /// <param name="value">The amount the author chose; it is capped to the dialog's range.</param>
    public void SetResourceCount(int resourceType, int value)
    {
        Funds resources = _resources;
        Funds.GetPtr(ref resources, resourceType) =
            Math.Clamp(value, ResourceAmountMinimum, ResourceAmountMaximum);
        _resources = resources;
        NeedRedraw = true;
    }

    /// <summary>Draws the whole window, the <c>display.render()</c> calls of <c>:260</c> and <c>:305-411</c>.</summary>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame, for the pressed look of the buttons.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(events);

        _window.Draw(target);

        GlyphText.DrawLine(_headerFont, target, _titlePosition, ObjectName + ":",
            new Rect(_titlePosition.X, _titlePosition.Y, _titleWidth, _headerFont.LineHeight));

        StandardWindow.ApplyTextBackgroundShading(target, _riddleRoi);

        // 参考 :174、:184：两个标签各自在自己区域上方居中。
        int riddleLabelWidth = GlyphText.Measure(_bodyFont, RiddleLabel);
        GlyphText.DrawLine(_bodyFont, target,
            new Point(_riddleRoi.X + ((_riddleRoi.Width - riddleLabelWidth) / 2), _riddleLabelY), RiddleLabel);

        int answerLabelWidth = GlyphText.Measure(_bodyFont, AnswersLabel);
        GlyphText.DrawLine(_bodyFont, target,
            new Point(_answerRoi.X + ((_answerRoi.Width - answerLabelWidth) / 2), _answerLabelY), AnswersLabel);

        DrawRiddleText(target);

        // 列表的重绘里带着自己那块底纹与滚动条槽（RedrawBackground）。
        Redraw(target);

        PaintMiniButton(_add, target, events);
        PaintMiniButton(_edit, target, events);
        PaintMiniButton(_delete, target, events);

        int rewardLabelWidth = GlyphText.Measure(_bodyFont, RewardLabel);
        GlyphText.DrawLine(_bodyFont, target,
            new Point(ActiveArea.X + ((ActiveArea.Width - rewardLabelWidth) / 2), _rewardLabelY), RewardLabel);

        ImageOps.BlitSprite(target, _artifactRoi.Position, _artwork.ArtifactFrame);
        ImageOps.BlitSprite(target, new Point(_artifactRoi.X + 6, _artifactRoi.Y + 6),
            _artwork.GetArtifactIcon(_artifact));

        PaintMiniButton(_deleteArtifact, target, events);

        StandardWindow.ApplyTextBackgroundShading(target, _resourceRoi);
        EditorUiHelper.RenderResources(_resources, _resourceRoi, target, _individualResourceRoi);

        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
    }

    /// <summary>
    /// Handles one frame of events, the body of the <c>while ( le.HandleEvents() )</c> loop of <c>:265-467</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>The result of the frame; <see cref="EditorListOutcome.None"/> while the window stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorListOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Result = EditorListOutcome.None;
        Popup = null;
        RiddleRequested = false;
        AnswerAddRequested = false;
        AnswerEditRequested = false;
        ArtifactRequested = false;
        ResourceCountRequest = null;
        IsDoubleClicked = false;

        // 参考 :273-279：两个按钮。
        if (events.MouseClickLeft(_cancel.Area))
        {
            Result = EditorListOutcome.Cancelled;

            return Result;
        }

        if (events.MouseClickLeft(_okay.Area))
        {
            Result = EditorListOutcome.Accepted;

            return Result;
        }

        // 参考 :281-286：列表自己处理滚轮、拖动与点击。
        QueueEventProcessing(events);

        // 参考 :288-311：资源图标。点中就发数量框，写回由宿主喂 SetResourceCount。
        for (int i = 0; i < ResourceTypes.Length; ++i)
        {
            if (!events.MouseClickLeft(_individualResourceRoi[i]))
            {
                continue;
            }

            int resourceType = ResourceTypes[i];

            ResourceCountRequest = new SphinxResourceRequest(resourceType, _resources.Get(resourceType),
                SelectCountDialog.Create(string.Format(CultureInfo.InvariantCulture, ResourceCountLabelFormat,
                    ResourceText.Of(resourceType)), ResourceAmountMinimum, ResourceAmountMaximum, 1));

            return Result;
        }

        // 参考 :313-326：改谜题。
        if (events.MouseClickLeft(_riddleRoi))
        {
            RiddleRequested = true;

            return Result;
        }

        // 参考 :327-342：新增答案。
        if (events.MouseClickLeft(_add.Area))
        {
            AnswerAddRequested = true;

            return Result;
        }

        // 参考 :343-365：双击或「改」，没有选中行就不发请求。
        if (IsDoubleClicked || events.MouseClickLeft(_edit.Area))
        {
            AnswerEditRequested = IsSelected;

            return Result;
        }

        // 参考 :366-375：删掉选中的答案。
        if (events.MouseClickLeft(_delete.Area))
        {
            RemoveSelectedAnswer();

            return Result;
        }

        // 参考 :376-403：换神器。参考还会为法术卷轴再弹选法术窗，端口一并交宿主。
        if (events.MouseClickLeft(_artifactRoi))
        {
            ArtifactRequested = true;

            return Result;
        }

        // 参考 :404-412：清空神器。
        if (events.MouseClickLeft(_deleteArtifact.Area))
        {
            ClearArtifact();

            return Result;
        }

        // 参考 :413-460：右键说明。
        if (events.IsMouseRightButtonPressedInArea(_cancel.Area))
        {
            Popup = new DialogPopup(CancelTitle, CancelMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_okay.Area))
        {
            Popup = new DialogPopup(OkayTitle, OkayMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_add.Area))
        {
            Popup = new DialogPopup(AddTitle, AddMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_edit.Area))
        {
            Popup = new DialogPopup(EditTitle, EditMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_delete.Area))
        {
            Popup = new DialogPopup(DeleteTitle, DeleteMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_riddleRoi))
        {
            Popup = new DialogPopup(RiddleTitle, _riddle);
        }
        else if (events.IsMouseRightButtonPressedInArea(_artifactRoi))
        {
            Popup = CreateArtifactPopup();
        }
        else if (events.IsMouseRightButtonPressedInArea(_deleteArtifact.Area))
        {
            Popup = new DialogPopup(DeleteArtifactTitle, DeleteArtifactMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_resourceRoi))
        {
            Popup = CreateResourcesPopup();
        }

        return Result;
    }

    /// <summary>Draws one row of the list, <c>AnswerListBox::RedrawItem()</c> (<c>:82-97</c>).</summary>
    /// <param name="item">The answer.</param>
    /// <param name="x">Left of the row.</param>
    /// <param name="y">Top of the row.</param>
    /// <param name="current">Whether the row is selected.</param>
    /// <param name="output">Frame to draw into.</param>
    public override void RedrawItem(string item, int x, int y, bool current, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(output);

        Rect area = new(x + RowTextInsetX, y + RowTextInsetY, AnswerAreaWidth - RowTextInsetTotal,
            Math.Max(1, _bodyFont.LineHeight));
        string text = GlyphText.FitToOneRow(current ? _headerFont : _bodyFont, item, area.Width);

        GlyphText.DrawLine(current ? _headerFont : _bodyFont, output, area.Position, text, ItemsArea);
    }

    /// <summary>Draws the background of one row, <c>AnswerListBox::RedrawBackground()</c> (<c>:101-110</c>).</summary>
    /// <param name="destination">Top left of the list.</param>
    /// <param name="output">Frame to draw into.</param>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _ = destination;

        StandardWindow.ApplyTextBackgroundShading(output, _answerRoi);
        StandardWindow.RenderScrollbarBackground(output, new Rect(_scrollBarX, _answerRoi.Y, 14, _answerRoi.Height),
            _artwork.List.ScrollBarBackground);
    }

    /// <inheritdoc/>
    public override void ActionCurrentUp()
    {
    }

    /// <inheritdoc/>
    public override void ActionCurrentDn()
    {
    }

    /// <inheritdoc/>
    public override void ActionListSingleClick(string item)
    {
        _ = item;
    }

    /// <inheritdoc/>
    public override void ActionListDoubleClick(string item)
    {
        _ = item;

        IsDoubleClicked = true;
    }

    /// <inheritdoc/>
    public override void ActionListPressRight(string item) => _ = item;

    /// <summary>Draws the riddle, capped to the rows the area holds (<c>:176-178</c>).</summary>
    /// <param name="target">Frame to draw into.</param>
    private void DrawRiddleText(PixelBuffer target)
    {
        int lineHeight = Math.Max(1, _bodyFont.LineHeight);
        Rect area = new(_riddleRoi.X + 5, _riddleRoi.Y + 5, _riddleRoi.Width - 10, _riddleRoi.Height - 10);
        int rows = Math.Max(1, area.Height / lineHeight);
        IReadOnlyList<string> lines = GlyphText.WrapLines(_bodyFont, _riddle, area.Width);

        // 参考 fitToArea() 把装不下的尾巴丢掉，这里同样只画前 rows 行。
        for (int i = 0; i < lines.Count && i < rows; ++i)
        {
            GlyphText.DrawLine(_bodyFont, target, new Point(area.X, area.Y + (i * lineHeight)), lines[i], area);
        }
    }

    /// <summary>The artifact's message, its name and description or the "none" one (<c>:438-447</c>).</summary>
    /// <returns>The message.</returns>
    private DialogPopup CreateArtifactPopup()
    {
        if (!ArtifactTypes.IsValid(_artifact))
        {
            return new DialogPopup(ArtifactTitle, NoArtifactMessage);
        }

        ArtifactData data = ArtifactTable.Get(_artifact);

        return new DialogPopup(data.Name, data.GetDescription(_artifactMetadata));
    }

    /// <summary>The resource panel's message, the amounts or the "none" one (<c>:451-458</c>).</summary>
    /// <returns>The message.</returns>
    /// <remarks>参考弹 <c>showResourceMessage()</c> 画图标；端口没有那个窗，改成同一提示里列出金额。</remarks>
    private DialogPopup CreateResourcesPopup()
    {
        if (_resources.GetValidItemsCount() == 0)
        {
            return new DialogPopup(ResourcesTitle, NoResourcesMessage);
        }

        List<string> lines = [ResourcesMessage];

        foreach (int resourceType in ResourceTypes)
        {
            int count = _resources.Get(resourceType);

            if (count > 0)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", ResourceText.Of(resourceType),
                    count));
            }
        }

        return new DialogPopup(ResourcesTitle, string.Join("\n", lines));
    }

    /// <summary>Draws a small button, pressed when the pointer holds the left button over it.</summary>
    /// <param name="button">The button.</param>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame.</param>
    private static void PaintMiniButton(ButtonSprite button, PixelBuffer target, LocalEvent events)
    {
        button.DrawOnState(events.IsMouseLeftButtonPressedAndHeldInArea(button.Area), target);
    }

    /// <summary>Draws a big button, pressed when the pointer holds the left button over it.</summary>
    /// <param name="button">The button.</param>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame.</param>
    private static void PaintButton(ButtonSprite button, PixelBuffer target, LocalEvent events)
    {
        button.DrawOnState(events.IsMouseLeftButtonPressedAndHeldInArea(button.Area), target);
    }
}
