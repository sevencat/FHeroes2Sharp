using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The three scope guards of <c>src/fheroes2/gui/ui_tool.cpp:349-393</c>:
/// <c>ScreenPaletteRestorer</c>, <c>ApplicationClosureRestorer</c> and <c>GameInterfaceTypeRestorer</c>.
/// </summary>
public sealed class RestorerTests
{
    /// <summary>A swapped palette is put back when the scope ends.</summary>
    [Fact]
    public void ScreenPaletteRestorer_RestoresTheGamePalette()
    {
        Palette gamePalette = new();
        Palette videoPalette = new(new byte[PaletteTable.Size * 3]);
        ScreenBuffer display = new(new Size(4, 4), gamePalette);

        using (ScreenPaletteRestorer restorer = new(display, gamePalette))
        {
            restorer.ChangePalette(videoPalette);

            Assert.Same(videoPalette, display.Palette);
            Assert.True(restorer.PaletteChanged);
        }

        Assert.Same(gamePalette, display.Palette);
    }

    /// <summary>Without a swap the scope ends silently, the reference's <c>_paletteChanged</c> guard.</summary>
    [Fact]
    public void ScreenPaletteRestorer_LeavesAnUntouchedScreenAlone()
    {
        Palette gamePalette = new();
        ScreenBuffer display = new(new Size(4, 4), gamePalette);

        using (ScreenPaletteRestorer restorer = new(display, gamePalette))
        {
            Assert.False(restorer.PaletteChanged);
        }

        Assert.Same(gamePalette, display.Palette);
    }

    /// <summary>A close request is ignored inside the scope and honoured again afterwards.</summary>
    [Fact]
    public void ApplicationClosureRestorer_BlocksTheCloseRequest()
    {
        LocalEvent events = new();
        bool closeRequested = true;
        events.QuitEventHook = () => closeRequested;

        using (new ApplicationClosureRestorer(events, () => closeRequested))
        {
            Assert.False(events.ProcessQuitEvent());
        }

        Assert.True(events.ProcessQuitEvent());
    }

    /// <summary>The interface type is switched for the scope and switched back at its end.</summary>
    [Fact]
    public void GameInterfaceTypeRestorer_SwitchesAndRestoresTheInterface()
    {
        Settings settings = Settings.Get();
        InterfaceType original = settings.InterfaceType;
        InterfaceType other = original == InterfaceType.Good ? InterfaceType.Evil : InterfaceType.Good;

        try
        {
            using (GameInterfaceTypeRestorer restorer = new(settings, other))
            {
                Assert.Equal(original, restorer.OriginalInterfaceType);
                Assert.Equal(other, settings.InterfaceType);
            }

            Assert.Equal(original, settings.InterfaceType);
        }
        finally
        {
            settings.SetInterfaceType(original);
        }
    }

    /// <summary>Asking for the interface type that is already active still records both sides.</summary>
    [Fact]
    public void GameInterfaceTypeRestorer_KeepsTheSameInterfaceWhenNothingChanges()
    {
        Settings settings = Settings.Get();
        InterfaceType original = settings.InterfaceType;

        try
        {
            using GameInterfaceTypeRestorer restorer = new(settings, original);

            Assert.Equal(original, restorer.InterfaceType);
            Assert.Equal(original, restorer.OriginalInterfaceType);
            Assert.Equal(original, settings.InterfaceType);
        }
        finally
        {
            settings.SetInterfaceType(original);
        }
    }
}
