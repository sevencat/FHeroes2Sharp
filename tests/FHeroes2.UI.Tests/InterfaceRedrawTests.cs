using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The <c>REDRAW_*</c> masks of <c>src/fheroes2/gui/interface_base.h:21-46</c>.
/// </summary>
public sealed class InterfaceRedrawTests
{
    [Theory]
    [InlineData(InterfaceRedraw.RadarCursor, 0x001u)]
    [InlineData(InterfaceRedraw.Radar, 0x002u)]
    [InlineData(InterfaceRedraw.Border, 0x004u)]
    [InlineData(InterfaceRedraw.GameArea, 0x008u)]
    [InlineData(InterfaceRedraw.Status, 0x010u)]
    [InlineData(InterfaceRedraw.Heroes, 0x020u)]
    [InlineData(InterfaceRedraw.Castles, 0x040u)]
    [InlineData(InterfaceRedraw.Buttons, 0x080u)]
    [InlineData(InterfaceRedraw.Panel, 0x100u)]
    [InlineData(InterfaceRedraw.Passabilities, 0x200u)]
    public void EachFlag_HasTheBitOfTheReference(InterfaceRedraw flag, uint expected)
    {
        Assert.Equal(expected, (uint)flag);
    }

    [Fact]
    public void Icons_IsHeroesAndCastles()
    {
        Assert.Equal(0x060u, (uint)InterfaceRedraw.Icons);
        Assert.Equal(InterfaceRedraw.Heroes | InterfaceRedraw.Castles, InterfaceRedraw.Icons);
    }

    [Fact]
    public void All_HoldsEveryScreenFlagButNotThePassabilityOverlay()
    {
        // The reference keeps REDRAW_PASSABILITIES out of REDRAW_ALL: the overlay is painted by the passability
        // mode, not by a normal redraw.
        Assert.Equal(0x1FFu, (uint)InterfaceRedraw.All);
        Assert.Equal(InterfaceRedraw.None, InterfaceRedraw.All & InterfaceRedraw.Passabilities);
    }
}
