using FHeroes2.Core.Geometry;
using FHeroes2.UI.Battle;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The layout of the battle settings window, <see cref="BattleSettingsWindow"/> against
/// <c>battle_interface_settings.cpp:38-51</c> (the option rectangles) and <c>:201</c> (the window size).
/// </summary>
public sealed class BattleSettingsWindowTests
{
    [Fact]
    public void TheWindowIsThreeOptionRowsTall()
    {
        Assert.Equal(289, BattleSettingsWindow.Width);
        Assert.Equal((OptionItem.OptionsStepY * 3) + 52, BattleSettingsWindow.Height);
        Assert.Equal(382, BattleSettingsWindow.Height);
    }

    [Fact]
    public void TheFirstRowSitsOnTheTopLeftOfTheActiveArea()
    {
        Rect active = new(10, 20, BattleSettingsWindow.Width, BattleSettingsWindow.Height);

        Rect turnOrder = BattleSettingsWindow.OptionArea(active, OptionItem.ThreeOptionsOffsetX,
            OptionItem.OptionsOffsetY);
        Rect grid = BattleSettingsWindow.OptionArea(active,
            OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX, OptionItem.OptionsOffsetY);
        Rect damageInfo = BattleSettingsWindow.OptionArea(active,
            OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2), OptionItem.OptionsOffsetY);

        Assert.Equal(30, turnOrder.X);
        Assert.Equal(51, turnOrder.Y);
        Assert.Equal(122, grid.X);
        Assert.Equal(51, grid.Y);
        Assert.Equal(214, damageInfo.X);
        Assert.Equal(OptionItem.OptionIconSize, damageInfo.Width);
        Assert.Equal(OptionItem.OptionIconSize, damageInfo.Height);
    }

    [Fact]
    public void TheHitPointsOptionIsCentredUnderTheTwoRows()
    {
        Rect active = new(10, 20, BattleSettingsWindow.Width, BattleSettingsWindow.Height);

        Rect hitPoints = BattleSettingsWindow.OptionArea(active,
            OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX,
            OptionItem.OptionsOffsetY + (OptionItem.OptionsStepY * 2));

        Assert.Equal(122, hitPoints.X);
        Assert.Equal(271, hitPoints.Y);
    }
}
