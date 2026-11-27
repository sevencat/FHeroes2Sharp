using System.Globalization;

using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The "set the amount" dialog a click on a resource icon asks the host to open, <c>Dialog::SelectCount()</c>
/// (<c>editor_event_details_window.cpp:262-334</c>).
/// </summary>
/// <param name="ResourceType">One of the <see cref="Resource"/> flags.</param>
/// <param name="Value">The amount the event currently gives.</param>
/// <param name="Dialog">The dialog to run; its <see cref="SelectCountDialog.Result"/> is the answer.</param>
public readonly record struct EventDetailsResourceRequest(int ResourceType, int Value, SelectCountDialog Dialog);

/// <summary>
/// The "set the amount" dialog a click on the experience icon asks the host to open
/// (<c>editor_event_details_window.cpp:494-504</c>).
/// </summary>
/// <param name="Value">The experience the event currently gives.</param>
/// <param name="Dialog">The dialog to run; its <see cref="SelectCountDialog.Result"/> is the answer.</param>
public readonly record struct EventDetailsExperienceRequest(int Value, SelectCountDialog Dialog);

/// <summary>
/// The editor's event details window, <c>eventDetailsDialog()</c>
/// (<c>editor_event_details_window.cpp:67-771</c>).
/// </summary>
/// <remarks>
/// <para>
/// 整窗 639×480，左右两列各 306 宽：左列是消息文本（306×207），右列依次是效果（「Effects:」+ 资源面板 306×99）、
/// 神器元素与删除按钮、经验图标与数值、技能元素与删除按钮；左列底部是条件（「Conditions:」+「首次访问后取消」
/// 勾选框 + 人类/电脑颜色框）。进来先把两份颜色集与地图允许的颜色求交收一遍，走时把副本交给调用方。
/// </para>
/// <para>
/// 「点一下换个更大的东西」全交宿主：消息框、神器选择、技能选择、经验数量、资源数量都只发
/// <see cref="MessageRequested"/>、<see cref="ArtifactRequested"/>、<see cref="SecondarySkillRequested"/>、
/// <see cref="ExperienceRequest"/>、<see cref="ResourceCountRequest"/>，宿主弹完再喂
/// <see cref="SetMessage"/>、<see cref="SetArtifact"/>、<see cref="SetSecondarySkill"/>、<see cref="SetExperience"/>、
/// <see cref="SetResourceCount"/>。参考在窗口里直接调 <c>Dialog::inputString</c> / <c>selectArtifact</c> /
/// <c>selectSecondarySkill</c> / <c>SelectCount</c>，其中神器与技能的单选窗端口都还没有。
/// </para>
/// <para>
/// 没有 <c>ImageRestorer</c>：<see cref="Draw"/> 每帧整窗重画，颜色框的方框画在常驻缓冲里、每帧贴回，
/// 勾选标记按状态单独贴。
/// </para>
/// </remarks>
public sealed class EditorEventDetailsWindow
{
    /// <summary>Width of one of the two columns, <c>sectionWidth</c> (<c>:63</c>).</summary>
    public const int SectionWidth = (640 - (3 * ElementOffsetValue)) / 2;

    /// <summary>Gap between the parts of the window, <c>elementOffset</c> (<c>:62</c>).</summary>
    public const int ElementOffset = ElementOffsetValue;

    /// <summary>Width of the window, <c>sectionWidth * 2 + 3 * elementOffset</c> (<c>:76</c>).</summary>
    public const int WindowWidth = (SectionWidth * 2) + (ElementOffset * 3);

    /// <summary>Height of the window, <c>DEFAULT_HEIGHT</c> (<c>:76</c>).</summary>
    public const int WindowHeight = 480;

    /// <summary>Height of the message area, <c>messageArea.height</c> (<c>:65</c>).</summary>
    public const int MessageAreaHeight = 207;

    /// <summary>Height of the reward panel (<c>:116</c>).</summary>
    public const int ResourcePanelHeight = 99;

    /// <summary>Distance between two colour checkboxes, the literal <c>32</c> of <c>:210</c>.</summary>
    public const int CheckboxStepX = 32;

    /// <summary>Room a colour checkbox row takes, the literal <c>35</c> of <c>:217</c>.</summary>
    public const int CheckboxRowHeight = 35;

    /// <summary>Gap between a wrapped label and the row below it (<c>:201</c>).</summary>
    public const int LabelBottomGap = 3;

    /// <summary>Longest message the author may type, <c>Maps::Map_Format::messageCharLimit</c>.</summary>
    public const int MessageCharLimit = 999;

    /// <summary>Smallest amount the resource dialog accepts (<c>:327</c>).</summary>
    public const int ResourceAmountMinimum = -99999;

    /// <summary>Largest amount the resource dialog accepts (<c>:327</c>).</summary>
    public const int ResourceAmountMaximum = 999999;

    /// <summary>Title of the window, <c>MP2::StringObject( OBJ_EVENT ) + ':'</c> (<c>:86</c>).</summary>
    public const string Title = "Event:";

    /// <summary>Label over the message area (<c>:95</c>).</summary>
    public const string MessageLabel = "Message Text:";

    /// <summary>Label over the effects column (<c>:109</c>).</summary>
    public const string EffectsLabel = "Effects:";

    /// <summary>Label over the conditions row (<c>:188</c>).</summary>
    public const string ConditionsLabel = "Conditions:";

    /// <summary>Label of the recurring-event checkbox (<c>:193</c>).</summary>
    public const string RecurringEventLabel = "Cancel event after first visit";

    /// <summary>Body of the recurring-event checkbox hint (<c>:194</c>).</summary>
    public const string RecurringEventMessage =
        "If this checkbox is checked, the event will trigger only once. If not checked, the event will trigger every time one of the specified players crosses the event tile.";

    /// <summary>Label over the human colours (<c>:198</c>).</summary>
    public const string HumanColorsLabel = "Player colors allowed to get event:";

    /// <summary>Label over the computer colours (<c>:221</c>).</summary>
    public const string ComputerColorsLabel = "Computer colors allowed to get the event:";

    /// <summary>Title of the message input dialog (<c>:407</c>).</summary>
    public const string MessageDialogHeader = "Message:";

    /// <summary>Title of the "set the amount" dialog, with the resource name filled in (<c>:317</c>).</summary>
    public const string ResourceCountLabelFormat = "Set {0} Count:";

    /// <summary>Title of the experience dialog (<c>:496</c>).</summary>
    public const string ExperienceDialogLabel = "Set Experience value:";

    /// <summary>Title of the artifact element hint (<c>:288</c>).</summary>
    public const string ArtifactTitle = "Artifact";

    /// <summary>Body of the artifact hint when the event gives none (<c>:288</c>).</summary>
    public const string NoArtifactMessage = "No artifact will be given by the event.";

    /// <summary>Title of the delete-artifact hint (<c>:290</c>).</summary>
    public const string DeleteArtifactTitle = "Delete Artifact";

    /// <summary>Body of the delete-artifact hint (<c>:290</c>).</summary>
    public const string DeleteArtifactMessage = "Delete an artifact from the event.";

    /// <summary>Title of the secondary skill element hint (<c>:302</c>).</summary>
    public const string SecondarySkillTitle = "Secondary Skill";

    /// <summary>Body of the secondary skill hint when the event gives none (<c>:302</c>).</summary>
    public const string NoSecondarySkillMessage = "No Secondary Skill will be given by the event.";

    /// <summary>Title of the delete-skill hint (<c>:304</c>).</summary>
    public const string DeleteSecondarySkillTitle = "Delete Secondary Skill";

    /// <summary>Body of the delete-skill hint (<c>:304</c>).</summary>
    public const string DeleteSecondarySkillMessage = "Delete the Secondary Skill from the event.";

    /// <summary>Title of the experience hint (<c>:310</c>).</summary>
    public const string ExperienceTitle = "Experience";

    /// <summary>Body of the experience hint (<c>:310</c>).</summary>
    public const string ExperienceMessage =
        "Experience allows your heroes to gain levels, increasing their primary and secondary skills.";

    /// <summary>Body of the CANCEL button's message (<c>:472</c>).</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Body of the OKAY button's message (<c>:475</c>).</summary>
    public const string OkayMessage = "Click to save the Event properties.";

    /// <summary>Title of the reward panel's message (<c>:512</c>).</summary>
    public const string ResourcesTitle = "Resources";

    /// <summary>Body of the reward panel's message when it is empty (<c>:512</c>).</summary>
    public const string NoResourcesMessage = "No resources will be part of this event.";

    /// <summary>Body of the reward panel's message (<c>:521</c>).</summary>
    public const string ResourcesMessage = "These resources will be a part of the event.";

    /// <summary>Hint title of the message area (<c>:534</c>).</summary>
    public const string MessageAreaTitle = "Event Message Text";

    /// <summary>Hint body of the message area (<c>:534</c>).</summary>
    public const string MessageAreaMessage = "Click here to change the event message.";

    /// <summary>Body of the range message a right click on an edit box shows.</summary>
    public const string RangeMessageFormat = "The available values range from {0} to {1}.";

    /// <summary>Hint title of a right click on a human colour checkbox (<c>:178</c>).</summary>
    public const string HumanColorTitleFormat = "Allow {0} human player to get event";

    /// <summary>Hint body of a right click on a human colour checkbox (<c>:179</c>).</summary>
    public const string HumanColorMessageFormat =
        "If this checkbox is checked, this event will trigger for the {0} player if they are controlled by a human.";

    /// <summary>Hint title of a right click on a computer colour checkbox (<c>:239</c>).</summary>
    public const string ComputerColorTitleFormat = "Allow {0} computer player to get event";

    /// <summary>Hint body of a right click on a computer colour checkbox (<c>:240</c>).</summary>
    public const string ComputerColorMessageFormat =
        "If this checkbox is checked, this event will trigger for the {0} player if they are controlled by a computer.";

    /// <summary>Title of the CANCEL button's message (<c>:472</c>).</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Title of the OKAY button's message and of the range hint (<c>:475</c>).</summary>
    public const string OkayTitle = "Okay";

    private const int ElementOffsetValue = 9;

    private static readonly int[] ResourceTypes =
    [
        Resource.Wood, Resource.Sulfur, Resource.Crystal, Resource.Mercury, Resource.Ore, Resource.Gems, Resource.Gold
    ];

    private readonly StandardWindow _window;
    private readonly AdventureMapEventMetadata _event;
    private readonly EditorEventDetailsArtwork _artwork;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _bodyFont;
    private readonly IGlyphSource _smallFont;
    private readonly bool _isEvilInterface;

    private readonly Point _titlePosition;
    private readonly Point _messageLabelPosition;
    private readonly Rect _messageRoi;
    private readonly Point _effectsLabelPosition;
    private readonly Rect _resourceRoi;
    private readonly int _secondColumnOffsetX;
    private readonly Rect[] _individualResourceRoi = new Rect[7];
    private readonly Rect _artifactRoi;
    private readonly Rect _experienceRoi;
    private readonly Rect _experienceValueRoi;
    private readonly Rect _secondarySkillRoi;
    private readonly Rect _recurringEventArea;
    private readonly int _recurringEventMaxWidth;
    private readonly Point _conditionsLabelPosition;
    private readonly Point _humanLabelPosition;
    private readonly int _humanLabelWidth;
    private readonly Point _computerLabelPosition;
    private readonly int _computerLabelWidth;

    private readonly List<Checkbox> _humanCheckboxes = [];
    private readonly List<Checkbox> _computerCheckboxes = [];
    private readonly PixelBuffer _checkboxLayer;
    private readonly Point _checkboxLayerOrigin;
    private readonly MovableSprite _recurringCheckmark = new();

    private ArtifactDialogElement _artifactElement;
    private ExperienceDialogElement _experienceElement;
    private SecondarySkillDialogElement _secondarySkillElement;

    private readonly ButtonSprite _deleteArtifact;
    private readonly ButtonSprite _deleteSecondarySkill;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;

    private EditorEventDetailsWindow(AdventureMapEventMetadata eventMetadata, PlayerColor availableHumanColors,
        PlayerColor availableComputerColors, EditorEventDetailsArtwork artwork, bool isEvilInterface, Size screen,
        IGlyphSource headerFont, IGlyphSource bodyFont, IGlyphSource smallFont)
    {
        _event = eventMetadata.Clone();
        _artwork = artwork;
        _isEvilInterface = isEvilInterface;
        _headerFont = headerFont;
        _bodyFont = bodyFont;
        _smallFont = smallFont;

        // 参考 :70-77：两份颜色集与地图允许的颜色求交，窗里只留还画得出来的颜色。
        _event.HumanPlayerColors &= (byte)availableHumanColors;
        _event.ComputerPlayerColors &= (byte)availableComputerColors;

        _window = StandardWindow.CreateCentered(new Size(WindowWidth, WindowHeight), renderBackground: true, screen);

        Rect dialogRoi = _window.ActiveArea;

        Size messageArea = new(SectionWidth, MessageAreaHeight);

        int offsetY = dialogRoi.Y + ElementOffset;
        int titleWidth = GlyphText.Measure(headerFont, Title);

        _titlePosition = new Point(dialogRoi.X + ((dialogRoi.Width - titleWidth) / 2), offsetY);

        offsetY += headerFont.LineHeight + ElementOffset;

        int startOffsetY = offsetY;

        _messageRoi = new Rect(dialogRoi.X + ElementOffset, offsetY + bodyFont.LineHeight, messageArea.Width,
            messageArea.Height);
        _messageLabelPosition = new Point(
            _messageRoi.X + ((_messageRoi.Width - GlyphText.Measure(bodyFont, MessageLabel)) / 2), offsetY);

        offsetY = startOffsetY;

        _secondColumnOffsetX = dialogRoi.X + SectionWidth + (2 * ElementOffset);

        _effectsLabelPosition = new Point(
            _secondColumnOffsetX + ((SectionWidth - GlyphText.Measure(bodyFont, EffectsLabel)) / 2), offsetY);

        offsetY += bodyFont.LineHeight;

        _resourceRoi = new Rect(_secondColumnOffsetX, offsetY, SectionWidth, ResourcePanelHeight);

        offsetY += ResourcePanelHeight + ElementOffset;

        _artifactElement = BuildArtifactElement();
        _artifactRoi = new Rect(_secondColumnOffsetX, offsetY, _artifactElement.Area.Width,
            _artifactElement.Area.Height);

        int deleteWidth = artwork.DeleteButton.Released.Width;
        int deleteTop = _artifactRoi.Y + _artifactRoi.Height + 5;

        _deleteArtifact = new ButtonSprite(artwork.DeleteButton.Released, artwork.DeleteButton.Pressed);
        _deleteArtifact.SetPosition(_artifactRoi.X + ((_artifactRoi.Width - deleteWidth) / 2), deleteTop);

        _experienceElement = BuildExperienceElement();
        _experienceRoi = new Rect(_secondColumnOffsetX + SectionWidth - _experienceElement.Area.Width,
            offsetY + ((_artifactRoi.Height - _experienceElement.Area.Height) / 2),
            _experienceElement.Area.Width, _experienceElement.Area.Height);
        _experienceValueRoi = new Rect(_experienceRoi.X, deleteTop, _experienceRoi.Width,
            artwork.DeleteButton.Released.Height);

        _secondarySkillElement = BuildSecondarySkillElement();
        _secondarySkillRoi = new Rect(
            (_artifactRoi.X + _artifactRoi.Width + _experienceRoi.X - _secondarySkillElement.Area.Width) / 2,
            offsetY + ((_artifactRoi.Height - _secondarySkillElement.Area.Height) / 2),
            _secondarySkillElement.Area.Width, _secondarySkillElement.Area.Height);

        _deleteSecondarySkill = new ButtonSprite(artwork.DeleteButton.Released, artwork.DeleteButton.Pressed);
        _deleteSecondarySkill.SetPosition(_secondarySkillRoi.X + ((_secondarySkillRoi.Width - deleteWidth) / 2),
            deleteTop);

        offsetY = _messageRoi.Y + _messageRoi.Height + (2 * ElementOffset);

        _conditionsLabelPosition = new Point(
            dialogRoi.X + ((dialogRoi.Width - GlyphText.Measure(bodyFont, ConditionsLabel)) / 2), offsetY);

        offsetY += bodyFont.LineHeight + ElementOffset;

        // 「首次访问后取消」的框与文字占多大，参考是画的时候才知道（drawCheckboxWithText 的返回值），
        // 这里先按同一套度量算出来，点击才有地方可落。
        _recurringEventMaxWidth = dialogRoi.Width - (2 * ElementOffset);
        _recurringEventArea = new Rect(_messageRoi.X + ElementOffset, offsetY,
            RecurringTextOffsetX + MeasureRecurringLabel(_recurringEventMaxWidth), artwork.CheckboxBox.Height);

        _humanLabelPosition = CreateLabel(bodyFont, HumanColorsLabel, _secondColumnOffsetX, ref offsetY,
            out _humanLabelWidth);

        int availablePlayers = PlayerColors.Count(availableHumanColors | availableComputerColors);
        int checkOffsetX = (SectionWidth - (availablePlayers * CheckboxStepX)) / 2;

        _checkboxLayerOrigin = _window.TotalArea.Position;
        _checkboxLayer = new PixelBuffer(_window.TotalArea.Width, _window.TotalArea.Height);

        Point humanCheckboxOrigin = ToLayer(new Point(_secondColumnOffsetX + checkOffsetX, offsetY));

        EditorUiHelper.CreateColorCheckboxes(_humanCheckboxes, availableHumanColors,
            (PlayerColor)_event.HumanPlayerColors, humanCheckboxOrigin.X, humanCheckboxOrigin.Y, _checkboxLayer);

        offsetY += CheckboxRowHeight;

        _computerLabelPosition = CreateLabel(bodyFont, ComputerColorsLabel, _secondColumnOffsetX, ref offsetY,
            out _computerLabelWidth);

        Point computerCheckboxOrigin = ToLayer(new Point(_secondColumnOffsetX + checkOffsetX, offsetY));

        EditorUiHelper.CreateColorCheckboxes(_computerCheckboxes, availableComputerColors,
            (PlayerColor)_event.ComputerPlayerColors, computerCheckboxOrigin.X, computerCheckboxOrigin.Y,
            _checkboxLayer);

        // 七个资源图标落在哪儿由 RenderResources 决定，画布只是为了让它在建窗时把位置算出来（参考也是建窗时算的）。
        PixelBuffer measurement = new(WindowWidth, WindowHeight);
        EditorUiHelper.RenderResources(_event.Resources, _resourceRoi, measurement, _individualResourceRoi);

        Point okayPosition = _window.RenderPosition(new Point(20, 7),
            new Size(artwork.Okay.Released.Width, artwork.Okay.Released.Height), WindowPadding.BottomLeft);
        Point cancelPosition = _window.RenderPosition(new Point(20, 7),
            new Size(artwork.Cancel.Released.Width, artwork.Cancel.Released.Height), WindowPadding.BottomRight);

        _okay = new ButtonSprite(artwork.Okay.Released, artwork.Okay.Pressed);
        _okay.SetPosition(okayPosition.X, okayPosition.Y);

        _cancel = new ButtonSprite(artwork.Cancel.Released, artwork.Cancel.Pressed);
        _cancel.SetPosition(cancelPosition.X, cancelPosition.Y);
    }

    /// <summary>Width of the checkbox box plus the gap the label starts after, the <c>23</c> of <c>drawCheckboxWithText</c>.</summary>
    public const int RecurringTextOffsetX = 23;

    /// <summary>
    /// Builds the window; the event is copied and its colours are clamped to what the map allows.
    /// </summary>
    /// <param name="eventMetadata">The event to edit.</param>
    /// <param name="availableHumanColors">Colours the map lets humans play.</param>
    /// <param name="availableComputerColors">Colours the map lets computers play.</param>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="isEvilInterface">Whether the evil look is used; only the OKAY/CANCEL buttons follow it.</param>
    /// <param name="screen">Screen size.</param>
    /// <param name="headerFont">The normal yellow font; only the title is in it.</param>
    /// <param name="bodyFont">The normal white font.</param>
    /// <param name="smallFont">The small white font the experience value and the skill names use.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static EditorEventDetailsWindow Create(AdventureMapEventMetadata eventMetadata,
        PlayerColor availableHumanColors, PlayerColor availableComputerColors, EditorEventDetailsArtwork artwork,
        bool isEvilInterface, Size screen, IGlyphSource headerFont, IGlyphSource bodyFont, IGlyphSource smallFont)
    {
        ArgumentNullException.ThrowIfNull(eventMetadata);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);
        ArgumentNullException.ThrowIfNull(smallFont);

        return new EditorEventDetailsWindow(eventMetadata, availableHumanColors, availableComputerColors, artwork,
            isEvilInterface, screen, headerFont, bodyFont, smallFont);
    }

    /// <summary>The window the parts sit in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The active area.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The event being edited, a copy of the one passed to <see cref="Create"/>.</summary>
    public AdventureMapEventMetadata Event => _event;

    /// <summary>The message area, <c>messageRoi</c> (<c>:98</c>).</summary>
    public Rect MessageArea => _messageRoi;

    /// <summary>The reward panel, <c>resourceRoi</c> (<c>:116</c>).</summary>
    public Rect ResourceArea => _resourceRoi;

    /// <summary>The area of every resource icon, in the order of <see cref="ResourceTypes"/>.</summary>
    public IReadOnlyList<Rect> ResourceIconAreas => _individualResourceRoi;

    /// <summary>The artifact element, <c>artifactRoi</c> (<c>:119</c>).</summary>
    public Rect ArtifactArea => _artifactRoi;

    /// <summary>The delete-artifact button, <c>buttonDeleteArtifact</c> (<c>:130</c>).</summary>
    public Rect DeleteArtifactArea => _deleteArtifact.Area;

    /// <summary>The experience icon, <c>experienceRoi</c> (<c>:133</c>).</summary>
    public Rect ExperienceArea => _experienceRoi;

    /// <summary>The area under the experience icon that shows the amount, <c>experienceValueRoi</c> (<c>:135</c>).</summary>
    public Rect ExperienceValueArea => _experienceValueRoi;

    /// <summary>The secondary skill element, <c>secondarySkillRoi</c> (<c>:155</c>).</summary>
    public Rect SecondarySkillArea => _secondarySkillRoi;

    /// <summary>The delete-skill button, <c>buttonDeleteSecondarySkill</c> (<c>:162</c>).</summary>
    public Rect DeleteSecondarySkillArea => _deleteSecondarySkill.Area;

    /// <summary>The recurring-event checkbox and its label, <c>recurringEventArea</c> (<c>:193</c>).</summary>
    public Rect RecurringEventArea => _recurringEventArea;

    /// <summary>The colour checkboxes of the human players, in colour order.</summary>
    public IReadOnlyList<Checkbox> HumanCheckboxes => _humanCheckboxes;

    /// <summary>The colour checkboxes of the computer players, in colour order.</summary>
    public IReadOnlyList<Checkbox> ComputerCheckboxes => _computerCheckboxes;

    /// <summary>The artifact element, rebuilt whenever the artifact changes.</summary>
    public ArtifactDialogElement ArtifactElement => _artifactElement;

    /// <summary>The experience element.</summary>
    public ExperienceDialogElement ExperienceElement => _experienceElement;

    /// <summary>The secondary skill element, rebuilt whenever the skill changes.</summary>
    public SecondarySkillDialogElement SecondarySkillElement => _secondarySkillElement;

    /// <summary>The OKAY button.</summary>
    public ButtonSprite Okay => _okay;

    /// <summary>The CANCEL button.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>How the author left the window; reset by <see cref="Process"/>.</summary>
    public EditorListOutcome Result { get; private set; }

    /// <summary>A message the host has to show, or <see langword="null"/>.</summary>
    public DialogPopup? Popup { get; private set; }

    /// <summary>Whether the author clicked the message area, so the host opens the input dialog (<c>:407</c>).</summary>
    public bool MessageRequested { get; private set; }

    /// <summary>
    /// Whether the author clicked the artifact, so the host opens its picker; the answer comes back through
    /// <see cref="SetArtifact"/> (<c>:436-443</c>).
    /// </summary>
    public bool ArtifactRequested { get; private set; }

    /// <summary>
    /// Whether the author clicked the secondary skill, so the host opens its picker; the answer comes back through
    /// <see cref="SetSecondarySkill"/> (<c>:445-453</c>).
    /// </summary>
    public bool SecondarySkillRequested { get; private set; }

    /// <summary>The "set the amount" dialog of the experience, or <see langword="null"/>.</summary>
    public EventDetailsExperienceRequest? ExperienceRequest { get; private set; }

    /// <summary>The "set the amount" dialog of a resource, or <see langword="null"/>.</summary>
    public EventDetailsResourceRequest? ResourceCountRequest { get; private set; }

    /// <summary>Replaces the message, <c>eventMetadata.message = std::move( temp )</c> (<c>:412</c>).</summary>
    /// <param name="message">The text the author typed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public void SetMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _event.Message = message.Length > MessageCharLimit ? message[..MessageCharLimit] : message;
    }

    /// <summary>Writes the artifact the author picked (<c>:437-441</c>).</summary>
    /// <param name="artifact">The artifact.</param>
    /// <param name="metadata">Its extra data, the spell of a spell scroll.</param>
    public void SetArtifact(ArtifactType artifact, int metadata = 0)
    {
        _event.Artifact = (int)artifact;
        _event.ArtifactMetadata = ArtifactTypes.IsValid(artifact) ? metadata : 0;
        _artifactElement = BuildArtifactElement();
    }

    /// <summary>Empties the artifact slot, the <c>RemoveArtifact( &amp;eventMetadata )</c> of <c>:454</c>.</summary>
    public void ClearArtifact() => SetArtifact(ArtifactType.Unknown);

    /// <summary>Writes the secondary skill the author picked (<c>:446-450</c>).</summary>
    /// <param name="skill">The skill.</param>
    /// <param name="level">Its level.</param>
    public void SetSecondarySkill(SecondarySkill skill, SkillLevel level)
    {
        _event.SecondarySkill = (byte)skill;
        _event.SecondarySkillLevel = (byte)level;
        _secondarySkillElement = BuildSecondarySkillElement();
    }

    /// <summary>Empties the skill slot, the <c>RemoveSecondarySkill( &amp;eventMetadata )</c> of <c>:465</c>.</summary>
    public void ClearSecondarySkill() => SetSecondarySkill(SecondarySkill.Unknown, SkillLevel.None);

    /// <summary>Writes the experience the author chose (<c>:506</c>).</summary>
    /// <param name="experience">The amount, clamped to <c>0..HeroExperience.MaxValue</c>.</param>
    public void SetExperience(int experience) =>
        _event.Experience = Math.Clamp(experience, 0, (int)HeroExperience.MaxValue);

    /// <summary>Writes the amount of one resource, the <c>*resourcePtr = temp</c> of <c>:328</c>.</summary>
    /// <param name="resourceType">One of the <see cref="Resource"/> flags.</param>
    /// <param name="value">The amount the author chose.</param>
    public void SetResourceCount(int resourceType, int value)
    {
        Funds resources = _event.Resources;
        Funds.GetPtr(ref resources, resourceType) = Math.Clamp(value, ResourceAmountMinimum, ResourceAmountMaximum);
        _event.Resources = resources;
    }

    /// <summary>Flips the recurring-event checkbox (<c>:213-217</c>).</summary>
    public void ToggleRecurringEvent() => _event.IsRecurringEvent = !_event.IsRecurringEvent;

    /// <summary>Draws the whole window, the <c>display.render()</c> of <c>:413</c> and the element draws.</summary>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame, for the pressed look of the buttons.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(events);

        _window.Draw(target);

        GlyphText.DrawLine(_headerFont, target, _titlePosition, Title);

        StandardWindow.ApplyTextBackgroundShading(target, _messageRoi);
        StandardWindow.ApplyTextBackgroundShading(target, _resourceRoi);
        StandardWindow.ApplyTextBackgroundShading(target, _experienceValueRoi);

        GlyphText.DrawLine(_bodyFont, target, _messageLabelPosition, MessageLabel);
        GlyphText.DrawLine(_bodyFont, target, _effectsLabelPosition, EffectsLabel);
        GlyphText.DrawLine(_bodyFont, target, _conditionsLabelPosition, ConditionsLabel);

        DrawMessageText(target);

        EditorUiHelper.RenderResources(_event.Resources, _resourceRoi, target, _individualResourceRoi);

        _artifactElement.Draw(target, _artifactRoi.Position);
        PaintButton(_deleteArtifact, target, events);

        _experienceElement.Draw(target, _experienceRoi.Position);

        string experience = _event.Experience.ToString(CultureInfo.InvariantCulture);
        int experienceWidth = GlyphText.Measure(_smallFont, experience);

        GlyphText.DrawLine(_smallFont, target,
            new Point(_experienceValueRoi.X + ((_experienceValueRoi.Width - experienceWidth) / 2),
                _experienceValueRoi.Y + 5), experience, _experienceValueRoi);

        _secondarySkillElement.Draw(target, _secondarySkillRoi.Position);
        PaintButton(_deleteSecondarySkill, target, events);

        DrawRecurringCheckbox(target);

        DrawLabel(target, _humanLabelPosition, HumanColorsLabel, _humanLabelWidth);
        DrawLabel(target, _computerLabelPosition, ComputerColorsLabel, _computerLabelWidth);

        ImageOps.Blit(target, _checkboxLayerOrigin, _checkboxLayer,
            new Rect(0, 0, _checkboxLayer.Width, _checkboxLayer.Height));

        foreach (Checkbox checkbox in _humanCheckboxes)
        {
            PaintCheckmark(target, checkbox);
        }

        foreach (Checkbox checkbox in _computerCheckboxes)
        {
            PaintCheckmark(target, checkbox);
        }

        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
    }

    /// <summary>
    /// Handles one frame of events, the body of the <c>while ( le.HandleEvents() )</c> loop of <c>:415-550</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>The result of the frame; <see cref="EditorListOutcome.None"/> while the window stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorListOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Result = EditorListOutcome.None;
        Popup = null;
        MessageRequested = false;
        ArtifactRequested = false;
        SecondarySkillRequested = false;
        ExperienceRequest = null;
        ResourceCountRequest = null;

        // 参考 :416-428：两个按钮。技能与神器都为「无」时 OKAY 才可用，这条在 :424 被注释掉了，
        // 端口照样让它一直可用。
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

        // 参考 :431-476：人类的颜色框。
        if (ProcessColorCheckboxes(_humanCheckboxes, isHuman: true, events))
        {
            return Result;
        }

        // 参考 :479-524：电脑的颜色框。
        if (ProcessColorCheckboxes(_computerCheckboxes, isHuman: false, events))
        {
            return Result;
        }

        // 参考 :527-543：资源图标。
        for (int i = 0; i < ResourceTypes.Length; ++i)
        {
            if (!events.MouseClickLeft(_individualResourceRoi[i]))
            {
                continue;
            }

            int resourceType = ResourceTypes[i];

            ResourceCountRequest = new EventDetailsResourceRequest(resourceType,
                _event.Resources.Get(resourceType),
                SelectCountDialog.Create(string.Format(CultureInfo.InvariantCulture, ResourceCountLabelFormat,
                    ResourceText.Of(resourceType)), ResourceAmountMinimum, ResourceAmountMaximum, 1));

            return Result;
        }

        // 参考 :546-552：消息区改文本。
        if (events.MouseClickLeft(_messageRoi))
        {
            MessageRequested = true;

            return Result;
        }

        // 参考 :554-561：「首次访问后取消」。
        if (events.MouseClickLeft(_recurringEventArea))
        {
            ToggleRecurringEvent();

            return Result;
        }

        // 参考 :563-570：神器。
        if (events.MouseClickLeft(_artifactRoi))
        {
            ArtifactRequested = true;

            return Result;
        }

        // 参考 :572-579：删神器。
        if (events.MouseClickLeft(_deleteArtifact.Area))
        {
            ClearArtifact();

            return Result;
        }

        // 参考 :581-589：技能。
        if (events.MouseClickLeft(_secondarySkillRoi))
        {
            SecondarySkillRequested = true;

            return Result;
        }

        // 参考 :591-600：删技能。
        if (events.MouseClickLeft(_deleteSecondarySkill.Area))
        {
            ClearSecondarySkill();

            return Result;
        }

        // 参考 :602-615：经验，点图标与点下面的数字都是改数量。
        Rect experienceArea = new(_experienceRoi.X, _experienceRoi.Y, _experienceRoi.Width,
            _experienceValueRoi.Bottom - _experienceRoi.Y);

        if (events.MouseClickLeft(experienceArea))
        {
            ExperienceRequest = new EventDetailsExperienceRequest(_event.Experience,
                SelectCountDialog.Create(ExperienceDialogLabel, 0, (int)HeroExperience.MaxValue, 1));

            return Result;
        }

        // 参考 :617-745：其余全是右键说明。
        if (events.IsMouseRightButtonPressedInArea(_cancel.Area))
        {
            Popup = new DialogPopup(CancelTitle, CancelMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_okay.Area))
        {
            Popup = new DialogPopup(OkayTitle, OkayMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_artifactRoi))
        {
            Popup = ArtifactTypes.IsValid((ArtifactType)_event.Artifact)
                ? ElementPopup(_artifactElement, events, _artifactRoi)
                : new DialogPopup(ArtifactTitle, NoArtifactMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_deleteArtifact.Area))
        {
            Popup = new DialogPopup(DeleteArtifactTitle, DeleteArtifactMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_secondarySkillRoi))
        {
            Popup = _event.SecondarySkill != 0
                ? ElementPopup(_secondarySkillElement, events, _secondarySkillRoi)
                : new DialogPopup(SecondarySkillTitle, NoSecondarySkillMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_deleteSecondarySkill.Area))
        {
            Popup = new DialogPopup(DeleteSecondarySkillTitle, DeleteSecondarySkillMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(experienceArea))
        {
            Popup = ElementPopup(_experienceElement, events, _experienceRoi);
        }
        else if (events.IsMouseRightButtonPressedInArea(_resourceRoi))
        {
            Popup = CreateResourcesPopup();
        }
        else if (events.IsMouseRightButtonPressedInArea(_recurringEventArea))
        {
            Popup = new DialogPopup(RecurringEventLabel, RecurringEventMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_messageRoi))
        {
            Popup = new DialogPopup(MessageAreaTitle, MessageAreaMessage);
        }

        return Result;
    }

    private ArtifactDialogElement BuildArtifactElement()
    {
        ArtifactType artifact = (ArtifactType)_event.Artifact;
        bool isValid = ArtifactTypes.IsValid(artifact);
        ArtifactData data = ArtifactTable.Get(artifact);

        return new ArtifactDialogElement(_artwork.ArtifactFrame, _artwork.GetArtifactIcon(artifact),
            isValid ? data.Name : string.Empty,
            isValid ? data.GetDescription(_event.ArtifactMetadata) : string.Empty);
    }

    private ExperienceDialogElement BuildExperienceElement() => new(_artwork.ExperienceIcon, string.Empty,
        ExperienceTitle, ExperienceMessage);

    private SecondarySkillDialogElement BuildSecondarySkillElement()
    {
        SecondarySkill skill = (SecondarySkill)_event.SecondarySkill;
        SkillLevel level = (SkillLevel)_event.SecondarySkillLevel;
        bool isValid = skill != SecondarySkill.Unknown;

        return new SecondarySkillDialogElement(_artwork.SecondarySkillFrame,
            _artwork.GetSecondarySkillIcon(skill),
            isValid ? SecondarySkills.GetName(skill) : string.Empty,
            isValid ? SecondarySkills.GetLevelName(level) : string.Empty,
            isValid ? SecondarySkills.GetName(skill) : string.Empty,
            isValid ? SecondarySkills.GetDescription(skill, level) : string.Empty);
    }

    /// <summary>
    /// Measures a two-line label of the right column and moves <paramref name="y"/> past it
    /// (<c>while ( text.rows( textWidth ) &lt; 2 )</c> of <c>:200-205</c>).
    /// </summary>
    /// <remarks>参考靠不断缩窄宽度逼出两行，遇到一个词就永远出不来第二行，所以这里多加 <c>width &gt; 1</c> 的护栏。</remarks>
    private static Point CreateLabel(IGlyphSource font, string text, int columnX, ref int y, out int width)
    {
        int labelWidth = SectionWidth;

        while (labelWidth > 1 && GlyphText.WrapLines(font, text, labelWidth).Count < 2)
        {
            labelWidth = (labelWidth * 2) / 3;
        }

        Point position = new(columnX + ((SectionWidth - labelWidth) / 2), y);

        width = labelWidth;
        y += LabelBottomGap + (GlyphText.WrapLines(font, text, labelWidth).Count * font.LineHeight);

        return position;
    }

    private void DrawLabel(PixelBuffer target, Point position, string text, int width) =>
        GlyphText.DrawMultiline(_bodyFont, target, position, text, width);

    /// <summary>Draws the message inside its area, the <c>text.drawInRoi()</c> of <c>:100-107</c>.</summary>
    private void DrawMessageText(PixelBuffer target)
    {
        if (string.IsNullOrEmpty(_event.Message))
        {
            return;
        }

        Rect roi = new(_messageRoi.X + 5, _messageRoi.Y + 5, _messageRoi.Width - 10, _messageRoi.Height - 10);
        int y = roi.Y;

        foreach (string line in GlyphText.WrapLines(_bodyFont, _event.Message, roi.Width))
        {
            if (y + _bodyFont.LineHeight > roi.Bottom)
            {
                break;
            }

            GlyphText.DrawLine(_bodyFont, target, new Point(roi.X, y), line, roi);
            y += _bodyFont.LineHeight;
        }
    }

    /// <summary>
    /// Paints the recurring-event checkbox, the <c>drawCheckboxWithText()</c> of <c>:193-195</c>.
    /// </summary>
    /// <remarks>
    /// 用窗口自己那份 <c>CELLWIN</c> 素材与字体画，而不是调
    /// <see cref="EditorUiHelper.DrawCheckboxWithText"/>：那个每帧都去归档现取两帧，而且文字用全局字体表，
    /// 与这里量出来的区域对不上。效果一样：框在左、文字右移 23 像素、勾在框上 <c>+2,+2</c>。
    /// </remarks>
    private void DrawRecurringCheckbox(PixelBuffer target)
    {
        ImageOps.BlitSprite(target, _recurringEventArea.Position, _artwork.CheckboxBox);
        ImageOps.AddGradientShadow(_artwork.CheckboxBox, target, _recurringEventArea.Position, new Point(-4, 4));

        Rect labelRoi = new(_recurringEventArea.X + RecurringTextOffsetX, _recurringEventArea.Y + 4,
            Math.Max(0, _recurringEventMaxWidth - RecurringTextOffsetX), _bodyFont.LineHeight);

        GlyphText.DrawLine(_bodyFont, target, labelRoi.Position, RecurringEventLabel, labelRoi);

        // 勾上表示「首次访问后取消」，也就是事件不重复。
        if (!_event.IsRecurringEvent)
        {
            ImageOps.BlitSprite(target, new Point(_recurringEventArea.X + 2, _recurringEventArea.Y + 2),
                _artwork.Checkmark);
        }
    }

    private int MeasureRecurringLabel(int maxWidth) =>
        Math.Min(GlyphText.Measure(_bodyFont, RecurringEventLabel), Math.Max(0, maxWidth - RecurringTextOffsetX));

    private void PaintCheckmark(PixelBuffer target, Checkbox checkbox)
    {
        MovableSprite checkmark = checkbox.Checkmark;

        if (checkmark.IsHidden || checkmark.Sprite is null)
        {
            return;
        }

        ImageOps.BlitSprite(target,
            new Point(_checkboxLayerOrigin.X + checkmark.X, _checkboxLayerOrigin.Y + checkmark.Y), checkmark.Sprite);
    }

    private Point ToLayer(Point position) => new(position.X - _checkboxLayerOrigin.X, position.Y - _checkboxLayerOrigin.Y);

    /// <summary>The hint an element asks for on a right click, the <c>element-&gt;processEvents()</c> of the reference.</summary>
    private static DialogPopup? ElementPopup(DialogElement element, LocalEvent events, Rect area) =>
        element.ProcessEvents(events, area.Position).Popup;

    /// <summary>Handles one group of colour checkboxes, the loops of <c>:431-524</c>.</summary>
    /// <returns><see langword="true"/> when a right click opened a hint.</returns>
    private bool ProcessColorCheckboxes(List<Checkbox> checkboxes, bool isHuman, LocalEvent events)
    {
        for (int i = 0; i < checkboxes.Count; ++i)
        {
            Checkbox checkbox = checkboxes[i];

            if (events.MouseClickLeft(checkbox.Area))
            {
                // 参考 :437-443 与 :485-491：勾上的补进颜色集，取消勾的从颜色集里去掉。
                bool isChecked = checkbox.Toggle();
                byte colors = isHuman ? _event.HumanPlayerColors : _event.ComputerPlayerColors;

                colors = (byte)(isChecked
                    ? (PlayerColor)colors | checkbox.Color
                    : (PlayerColor)colors & ~checkbox.Color);

                if (isHuman)
                {
                    _event.HumanPlayerColors = colors;
                }
                else
                {
                    _event.ComputerPlayerColors = colors;
                }

                break;
            }

            if (events.IsMouseRightButtonPressedInArea(checkbox.Area))
            {
                string color = PlayerColors.GetName(checkbox.Color);
                string title = string.Format(CultureInfo.InvariantCulture,
                    isHuman ? HumanColorTitleFormat : ComputerColorTitleFormat, color);
                string message = string.Format(CultureInfo.InvariantCulture,
                    isHuman ? HumanColorMessageFormat : ComputerColorMessageFormat, color);

                Popup = new DialogPopup(title, message);

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The hint of the reward panel, <c>:512-523</c>.
    /// </summary>
    /// <remarks>
    /// 参考有资源时弹的是画着资源图标的 <c>showResourceMessage()</c>，端口还没有那个窗口，因此把「给什么」
    /// 按文本列在同一个提示里。
    /// </remarks>
    private DialogPopup CreateResourcesPopup()
    {
        if (_event.Resources.GetValidItemsCount() == 0)
        {
            return new DialogPopup(ResourcesTitle, NoResourcesMessage);
        }

        List<string> parts = [];

        foreach (int resourceType in ResourceTypes)
        {
            int amount = _event.Resources.Get(resourceType);

            if (amount != 0)
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}",
                    ResourceText.Of(resourceType), amount));
            }
        }

        return new DialogPopup(ResourcesTitle, ResourcesMessage + "\n" + string.Join(", ", parts));
    }

    private static void PaintButton(ButtonSprite button, PixelBuffer target, LocalEvent events)
    {
        ImageOps.AddGradientShadow(button.Released, target, button.Area.Position, new Point(-5, 5));

        if (button.IsEnabled)
        {
            button.DrawOnState(events.IsMouseLeftButtonPressedAndHeldInArea(button.Area), target);
        }
        else
        {
            button.Draw(target);
        }
    }
}
