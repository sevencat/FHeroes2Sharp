using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the data of the adventure map buttons, <c>Interface::ButtonsPanel</c>: the order the eight buttons are laid
/// out in and the status <c>_setButtonStatus</c> produces when no hero is focused.
/// </summary>
/// <remarks>
/// 面板本身（布局、绘制、点击）的用例在 <c>FHeroes2.UI.Tests.ButtonsPanelTests</c>，因为那个类在 UI 层；这里只守
/// <see cref="AdventureButton"/> 的编号与 <see cref="AdventureButtonStatus.WithoutHero"/>——前者的数值决定面板里的格子
/// 与帧号，后者是「没有英雄时哪些按钮还能按」的唯一出处。
/// </remarks>
public sealed class AdventureButtonsTests
{
    [Fact]
    public void TheEightButtonsAreNumberedInLayoutOrder()
    {
        // 两行四列，按 SetPos 的顺序编号：编号既算格子（第 index 个）也算帧号（2 * index）。
        Assert.Equal(
            new[]
            {
                AdventureButton.NextHero,
                AdventureButton.HeroMovement,
                AdventureButton.Kingdom,
                AdventureButton.Spell,
                AdventureButton.EndTurn,
                AdventureButton.Adventure,
                AdventureButton.File,
                AdventureButton.System,
            },
            Enum.GetValues<AdventureButton>());
    }

    [Fact]
    public void WithoutAHeroOnlyTheFiveUnconditionalButtonsAreEnabled()
    {
        AdventureButtonStatus status = AdventureButtonStatus.WithoutHero;

        Assert.Equal(HeroMovementIcon.Reset, status.Movement);
        Assert.False(status.IsMovementEnabled);
        Assert.False(status.IsSpellEnabled);
        Assert.False(status.IsNextHeroEnabled);
        Assert.False(status.HasValidPath);
    }
}
