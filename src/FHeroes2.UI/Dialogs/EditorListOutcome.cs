namespace FHeroes2.UI.Dialogs;

/// <summary>
/// How the author left an editor list window — the <c>bool</c> that <c>openRumorWindow()</c>
/// (<c>editor_rumor_window.cpp:135-305</c>) and <c>openDailyEventsWindow()</c>
/// (<c>editor_daily_events_window.cpp:160-319</c>) return.
/// </summary>
/// <remarks>
/// 两个窗口都是「列表 + 三个小按钮 + OKAY/CANCEL」，参考用 <c>bool</c> 表示「留不留下改过的内容」，
/// 分成三档是为了让宿主能区分「没动」和「取消」。
/// </remarks>
public enum EditorListOutcome
{
    /// <summary>The window stays open.</summary>
    None,

    /// <summary>Accepted; the caller keeps the list the window collected.</summary>
    Accepted,

    /// <summary>Cancelled.</summary>
    Cancelled
}
