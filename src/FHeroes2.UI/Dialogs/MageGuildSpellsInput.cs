using FHeroes2.Core.Geometry;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the mage guild spells sub-window asks its host to do when a click needs another window or a message,
/// <c>mageGuildSpellsDialog()</c> (<c>editor_castle_details_window.cpp:407-605</c>).
/// </summary>
/// <remarks>
/// 这里只发请求、不做弹窗：法术选择器与禁用清单都是模态窗，宿主自己开、自己把结果回填
/// （<see cref="MageGuildSpellsInput.ApplySelectedSpell"/>、<see cref="MageGuildSpellsInput.ApplyBannedSpells"/>）。
/// 与 <see cref="CastleDetailsRequest"/>、<see cref="EditorSpellSelectionRequest"/> 同一套路。
/// </remarks>
public enum MageGuildSpellsRequestKind
{
    /// <summary>Nothing to ask for.</summary>
    None,

    /// <summary>Show a standard text message, <c>showStandardTextMessage()</c>.</summary>
    ShowMessage,

    /// <summary>Show the description of a spell, <c>SpellDialogElement::showPopup()</c>.</summary>
    ShowSpell,

    /// <summary>Open the spell picker for one row, <c>Dialog::selectSpell()</c>.</summary>
    SelectSpell,

    /// <summary>Open the banned spells window for one level, <c>Editor::openSpellSelectionWindow()</c>.</summary>
    SelectBannedSpells
}

/// <summary>
/// A request of <see cref="MageGuildSpellsInput"/> to its host.
/// </summary>
public readonly record struct MageGuildSpellsRequest
{
    /// <summary>What the host has to do.</summary>
    public MageGuildSpellsRequestKind Kind { get; private init; }

    /// <summary>Title of the message.</summary>
    public string Title { get; private init; }

    /// <summary>Body of the message.</summary>
    public string Body { get; private init; }

    /// <summary>The spell whose description is wanted.</summary>
    public SpellType Spell { get; private init; }

    /// <summary>Spell level, 1 to 5, of the picker or of the banned spells window.</summary>
    public int SpellLevel { get; private init; }

    /// <summary>The spell the spot holds right now, which the picker marks as current.</summary>
    public SpellType CurrentSpell { get; private init; }

    /// <summary>The spells the picker must not offer.</summary>
    public IReadOnlyList<int> ExcludedSpells { get; private init; }

    /// <summary>The banned spells of the level the sub-window opens at.</summary>
    public IReadOnlyList<int> BannedSpells { get; private init; }

    /// <summary>A request that does nothing.</summary>
    public static MageGuildSpellsRequest None => default;

    /// <summary>
    /// Asks for a standard text message.
    /// </summary>
    /// <param name="title">Title of the message.</param>
    /// <param name="body">Body of the message.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static MageGuildSpellsRequest ShowMessage(string title, string body)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(body);

        return new MageGuildSpellsRequest
        {
            Kind = MageGuildSpellsRequestKind.ShowMessage,
            Title = title,
            Body = body
        };
    }

    /// <summary>
    /// Asks for the description of a spell.
    /// </summary>
    /// <param name="spell">The spell.</param>
    /// <returns>The request.</returns>
    public static MageGuildSpellsRequest ShowSpell(SpellType spell) => new()
    {
        Kind = MageGuildSpellsRequestKind.ShowSpell,
        Spell = spell
    };

    /// <summary>
    /// Asks for the spell picker of one row.
    /// </summary>
    /// <param name="spellLevel">Spell level, 1 to 5.</param>
    /// <param name="currentSpell">The spell the spot holds right now.</param>
    /// <param name="excludedSpells">The spells the picker must not offer.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="excludedSpells"/> is <see langword="null"/>.</exception>
    public static MageGuildSpellsRequest SelectSpell(int spellLevel, SpellType currentSpell,
        IReadOnlyList<int> excludedSpells)
    {
        ArgumentNullException.ThrowIfNull(excludedSpells);

        return new MageGuildSpellsRequest
        {
            Kind = MageGuildSpellsRequestKind.SelectSpell,
            SpellLevel = spellLevel,
            CurrentSpell = currentSpell,
            ExcludedSpells = excludedSpells
        };
    }

    /// <summary>
    /// Asks for the banned spells window of one level.
    /// </summary>
    /// <param name="spellLevel">Spell level, 1 to 5.</param>
    /// <param name="title">Title of the window, the guild's name for that level plus a colon.</param>
    /// <param name="bannedSpells">The banned spells of that level.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static MageGuildSpellsRequest SelectBannedSpells(int spellLevel, string title,
        IReadOnlyList<int> bannedSpells)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(bannedSpells);

        return new MageGuildSpellsRequest
        {
            Kind = MageGuildSpellsRequestKind.SelectBannedSpells,
            SpellLevel = spellLevel,
            Title = title,
            BannedSpells = bannedSpells
        };
    }
}

/// <summary>
/// What the mage guild spells sub-window did with the event it was given.
/// </summary>
public enum MageGuildSpellsOutcome
{
    /// <summary>The window keeps running.</summary>
    None,

    /// <summary>OKAY was pressed, or OK; the edits are written.</summary>
    Accepted,

    /// <summary>EXIT was pressed, or Escape; the edits are dropped.</summary>
    Cancelled
}

/// <summary>
/// The event handling of <c>mageGuildSpellsDialog()</c> (<c>editor_castle_details_window.cpp:437-600</c>):
/// clicks on OKAY, EXIT, BANNED SPELLS and the five rows, plus the status messages that follow the pointer.
/// </summary>
/// <remarks>
/// <para>
/// 输入层只改 <see cref="MageGuildSpellsView.Table"/> 与状态栏文字，不碰显示缓冲、不开窗：点格子时发一条
/// <see cref="MageGuildSpellsRequestKind.SelectSpell"/> 出去，宿主开完选择器再调
/// <see cref="ApplySelectedSpell"/> 把结果塞回来——「模态」这件事只留在宿主一处。
/// </para>
/// <para>
/// 与参考的一处差别：禁用清单在参考里是「确认就用、Escape 才退出」的循环（<c>:515-555</c>），端口把循环交给宿主，
/// <see cref="ApplyBannedSpells"/> 每被调一次就等于确认一次。
/// </para>
/// </remarks>
public sealed class MageGuildSpellsInput
{
    /// <summary>Status message while the pointer is over OKAY, <c>:558</c>.</summary>
    public const string OkayMessage = "Click to accept the changes made.";

    /// <summary>Status message while the pointer is over EXIT, <c>:565</c>.</summary>
    public const string ExitMessage = "Exit this dialog, discarding the changes made.";

    /// <summary>Status message while the pointer is over BANNED SPELLS, <c>:572</c>.</summary>
    public const string BannedSpellsMessage = "Click to ban spells from appearing in the Mage Guild.";

    /// <summary>Status message the window opens with, <c>:594</c>.</summary>
    public const string DefaultMessage = "Select spells that will appear in the Mage Guild.";

    /// <summary>Title of the OKAY popup, <c>:561</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Title of the EXIT popup, <c>:568</c>.</summary>
    public const string ExitTitle = "Exit";

    /// <summary>Title of the BANNED SPELLS popup, <c>:575</c>.</summary>
    public const string BannedSpellsTitle = "Banned Spells";

    private readonly MageGuildSpellsView _view;
    private readonly CastleMetadata _metadata;

    private int _pickedLevelIndex = -1;
    private int _pickedSlot = -1;
    private bool _hasChanges;

    /// <summary>
    /// Wires the sub-window up.
    /// </summary>
    /// <param name="view">What the input drives.</param>
    /// <param name="metadata">The castle the edits go into when OKAY is pressed.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MageGuildSpellsInput(MageGuildSpellsView view, CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(metadata);

        _view = view;
        _metadata = metadata;

        _view.ShowMessage(DefaultMessage);
    }

    /// <summary>What the host has to do for the last processed event.</summary>
    public MageGuildSpellsRequest Request { get; private set; } = MageGuildSpellsRequest.None;

    /// <summary>What the last processed event did to the window.</summary>
    public MageGuildSpellsOutcome Outcome { get; private set; }

    /// <summary>Whether the metadata really changed, either when OKAY was pressed or by a banning.</summary>
    public bool HasChanges => _hasChanges;

    /// <summary>
    /// The level the banned spells window opens at, <c>currentBannedSpellsLevel</c> (<c>:440</c>): it starts at one
    /// and follows whatever level the author last looked at.
    /// </summary>
    public int CurrentBannedSpellsLevel { get; private set; } = 1;

    /// <summary>
    /// Handles one event, <c>:444-600</c>.
    /// </summary>
    /// <remarks>
    /// 检查顺序照抄参考：EXIT、OKAY、BANNED SPELLS、三个按钮的悬停提示、五排卷轴，最后是状态栏默认文字。
    /// 一轮只处理一件事，处理完就返回。
    /// </remarks>
    /// <param name="events">The event of the frame.</param>
    /// <returns>What the window did.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public MageGuildSpellsOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Request = MageGuildSpellsRequest.None;
        Outcome = MageGuildSpellsOutcome.None;

        if (events.MouseClickLeft(_view.ExitArea))
        {
            return Cancel();
        }

        if (events.MouseClickLeft(_view.OkayArea))
        {
            return Accept();
        }

        if (events.MouseClickLeft(_view.BannedSpellsArea))
        {
            RequestBannedSpellsWindow();

            return Outcome;
        }

        if (ProcessHover(events))
        {
            return Outcome;
        }

        if (!ProcessRows(events))
        {
            _view.ShowMessage(DefaultMessage);
        }

        return Outcome;
    }

    /// <summary>
    /// Accepts the spells as they are now, the OKAY button of the reference and its default okay hot key
    /// (<c>:444-448</c>).
    /// </summary>
    /// <returns>The outcome, <see cref="MageGuildSpellsOutcome.Accepted"/>.</returns>
    public MageGuildSpellsOutcome Accept()
    {
        _hasChanges |= _view.Table.Save(_metadata);

        Outcome = MageGuildSpellsOutcome.Accepted;

        return Outcome;
    }

    /// <summary>
    /// Drops the edits and leaves, the EXIT button of the reference and its default cancel hot key
    /// (<c>:443-446</c>).
    /// </summary>
    /// <returns>The outcome, <see cref="MageGuildSpellsOutcome.Cancelled"/>.</returns>
    public MageGuildSpellsOutcome Cancel()
    {
        Outcome = MageGuildSpellsOutcome.Cancelled;

        return Outcome;
    }

    /// <summary>
    /// Pins the spell the picker returned into the spot that asked for it, <c>:429-432</c>.
    /// </summary>
    /// <param name="spell">The spell, or <see cref="SpellType.None"/> when the picker was cancelled.</param>
    public void ApplySelectedSpell(SpellType spell)
    {
        if (spell == SpellType.None || _pickedLevelIndex < 0 || _pickedSlot < 0)
        {
            return;
        }

        _view.Table.SetSpell(_pickedLevelIndex, _pickedSlot, spell);
        _view.Refresh();

        _pickedLevelIndex = -1;
        _pickedSlot = -1;
    }

    /// <summary>
    /// Replaces the banned spells of one level with what the banned spells window returned, <c>:481-549</c>: the
    /// flattened list goes back into the table, which also blanks any row spell that just got banned.
    /// </summary>
    /// <param name="spellLevel">Level the sub-window was showing, 1 to 5.</param>
    /// <param name="bannedSpells">The banned spells of that level; empty clears them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bannedSpells"/> is <see langword="null"/>.</exception>
    public void ApplyBannedSpells(int spellLevel, IReadOnlyList<int> bannedSpells)
    {
        ArgumentNullException.ThrowIfNull(bannedSpells);
        ArgumentOutOfRangeException.ThrowIfLessThan(spellLevel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(spellLevel, MageGuildSpellTable.LevelCount);

        CurrentBannedSpellsLevel = spellLevel;

        List<int>[] grouped = MageGuildSpellTable.GroupByLevel(_view.Table.BannedSpells);
        grouped[spellLevel - 1] = [.. bannedSpells];

        if (_view.Table.SetBannedSpells(MageGuildSpellTable.FlattenGrouped(grouped)))
        {
            _hasChanges = true;
            _view.Refresh();
        }
    }

    /// <summary>
    /// Moves the banned spells window to another level without touching the list, the
    /// <c>currentBannedSpellsLevel = ...</c> of <c>:515-521</c>: the reopened window starts from that level next time.
    /// </summary>
    /// <remarks>
    /// 参考的循环在换等级后把 <c>currentBannedSpellsLevel</c> 改掉再重开；端口在这里只记下等级，重开由宿主负责。
    /// </remarks>
    /// <param name="spellLevel">Spell level, 1 to 5.</param>
    public void FocusBannedSpellsLevel(int spellLevel)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(spellLevel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(spellLevel, MageGuildSpellTable.LevelCount);

        CurrentBannedSpellsLevel = spellLevel;
    }

    /// <summary>
    /// Sends the request that opens the banned spells window, <c>:486-517</c>.
    /// </summary>
    /// <remarks>
    /// 标题取该等级的法师公会名（<c>getBuildingName( 0, BUILD_MAGEGUILD1..5 )</c>）再加一个冒号；清单只给这一级的
    /// 那几条——参考就是把摊开的五个容器里现用的那个递进去。
    /// </remarks>
    private void RequestBannedSpellsWindow()
    {
        int spellLevel = CurrentBannedSpellsLevel;

        Request = MageGuildSpellsRequest.SelectBannedSpells(spellLevel,
            CastleDetailsLabels.GetMageGuildLevelTitle(spellLevel) + ":", GetBannedSpellsOf(spellLevel));
    }

    /// <summary>
    /// The banned spells of one level, in the order the table keeps them.
    /// </summary>
    /// <param name="spellLevel">Spell level, 1 to 5.</param>
    /// <returns>The spell ids.</returns>
    private List<int> GetBannedSpellsOf(int spellLevel) =>
        MageGuildSpellTable.GroupByLevel(_view.Table.BannedSpells)[spellLevel - 1];

    /// <summary>
    /// Updates the status message for whatever button the pointer is over and sends the popup a right click asks
    /// for, <c>:557-577</c>.
    /// </summary>
    /// <param name="events">The event of the frame.</param>
    /// <returns>Whether the pointer is over one of the three buttons.</returns>
    private bool ProcessHover(LocalEvent events)
    {
        Point cursor = events.CursorPosition;

        if (_view.OkayArea.Contains(cursor))
        {
            _view.ShowMessage(OkayMessage);

            if (events.IsMouseRightButtonPressedInArea(_view.OkayArea))
            {
                Request = MageGuildSpellsRequest.ShowMessage(OkayTitle, OkayMessage);
            }

            return true;
        }

        if (_view.ExitArea.Contains(cursor))
        {
            _view.ShowMessage(ExitMessage);

            if (events.IsMouseRightButtonPressedInArea(_view.ExitArea))
            {
                Request = MageGuildSpellsRequest.ShowMessage(ExitTitle, ExitMessage);
            }

            return true;
        }

        if (_view.BannedSpellsArea.Contains(cursor))
        {
            _view.ShowMessage(BannedSpellsMessage);

            if (events.IsMouseRightButtonPressedInArea(_view.BannedSpellsArea))
            {
                Request = MageGuildSpellsRequest.ShowMessage(BannedSpellsTitle, BannedSpellsMessage);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles the row under the pointer, <c>:583-591</c>: a left click asks for the spell picker, a right click
    /// asks for the spell's description.
    /// </summary>
    /// <param name="events">The event of the frame.</param>
    /// <returns>Whether the pointer is over a scroll of a row.</returns>
    private bool ProcessRows(LocalEvent events)
    {
        for (int levelIndex = 0; levelIndex < _view.Rows.Count; ++levelIndex)
        {
            SpellScrollRow row = _view.Rows[levelIndex];
            Point cursor = events.CursorPosition;
            int slot = row.GetIndexAt(cursor);

            if (slot < 0)
            {
                continue;
            }

            SpellType spell = _view.Table.GetSpell(levelIndex, slot);

            if (events.IsMouseRightButtonPressedInArea(row.Coords[slot]))
            {
                // 占位符不是真法术，没有说明可弹（SpellsInOneRow 对 Spell::NONE 也是什么都不做）。
                if (!MageGuildSpellTable.IsPlaceholder(spell))
                {
                    Request = MageGuildSpellsRequest.ShowSpell(spell);
                }

                return true;
            }

            if (events.MouseClickLeft(row.Coords[slot]))
            {
                _pickedLevelIndex = levelIndex;
                _pickedSlot = slot;

                Request = MageGuildSpellsRequest.SelectSpell(levelIndex + 1, spell,
                    _view.Table.GetExcludedSpells(levelIndex, slot));

                return true;
            }

            return true;
        }

        return false;
    }
}
