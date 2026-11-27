namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The eight buttons of the panel, in the order <c>Interface::ButtonsPanel::SetPos</c> lays them out:
/// two rows of four, 36x36 each.
/// </summary>
/// <remarks>
/// 只有数据留在这里：面板本身（布局、绘制、点击）在 <c>FHeroes2.UI.Interface.ButtonsPanel</c>，与参考里
/// <c>gui/interface_buttons.cpp</c> 的位置一致。这个枚举与下面两个类型留在模型层，是因为按钮的语义（哪八个、哪一个换图标、
/// 哪一种状态禁用）属于冒险地图，而不是某一块砖。
/// </remarks>
public enum AdventureButton
{
    /// <summary>Jump to the next hero that can still move.</summary>
    NextHero = 0,

    /// <summary>Walk the path of the current hero, visit the object he stands on or reset his path.</summary>
    HeroMovement = 1,

    /// <summary>Open the castle of the current hero (or the last one).</summary>
    Kingdom = 2,

    /// <summary>Cast an adventure map spell, needs a hero with a spell book.</summary>
    Spell = 3,

    /// <summary>End the turn of the current player.</summary>
    EndTurn = 4,

    /// <summary>Tear down the tents adventure map option / pass to the next player.</summary>
    Adventure = 5,

    /// <summary>Save, load and the other game commands.</summary>
    File = 6,

    /// <summary>Sound, music, options and the main menu.</summary>
    System = 7,
}

/// <summary>
/// The three meanings of the hero movement button, the frames
/// <c>Interface::ButtonsPanel::_setButtonStatus</c> switches between: walk the current path (2,3),
/// visit the object under the hero (16,17) or show that the path is idle (18,19).
/// </summary>
public enum HeroMovementIcon
{
    /// <summary>The hero has a path that can be walked, frames 2 and 3.</summary>
    Walk,

    /// <summary>The hero stands on an object he may visit again, frames 16 and 17.</summary>
    Action,

    /// <summary>No path, or a path that cannot be walked: frames 18 and 19, greyed out.</summary>
    Reset,
}

/// <summary>
/// Which of the buttons are enabled and which glyph the movement button shows. It is the data of
/// <c>Interface::ButtonsPanel::_setButtonStatus</c>, which turns the state of the focused hero and of
/// his kingdom into these five values.
/// </summary>
/// <param name="Movement">What the movement button offers.</param>
/// <param name="IsMovementEnabled">Whether the movement button reacts to a click.</param>
/// <param name="IsSpellEnabled">Whether the spell button reacts to a click.</param>
/// <param name="IsNextHeroEnabled">Whether the next hero button reacts to a click.</param>
/// <param name="HasValidPath">
/// Whether the focus hero has a path at all, <c>GetPath().isValidForMovement()</c>: the reference checks it a second
/// time before it accepts a long press on the movement button, to keep the long press delay from firing for a hero
/// without a path.
/// </param>
/// <remarks>
/// The other five buttons are never disabled by the reference, so they do not need a flag.
/// </remarks>
public readonly record struct AdventureButtonStatus(
    HeroMovementIcon Movement,
    bool IsMovementEnabled,
    bool IsSpellEnabled,
    bool IsNextHeroEnabled,
    bool HasValidPath = false)
{
    /// <summary>
    /// The status without a focused hero: every branch of <c>_setButtonStatus</c> that tests
    /// <c>currentHero</c> falls through, so only the five unconditional buttons stay enabled.
    /// </summary>
    public static AdventureButtonStatus WithoutHero { get; } =
        new(HeroMovementIcon.Reset, IsMovementEnabled: false, IsSpellEnabled: false, IsNextHeroEnabled: false);
}
