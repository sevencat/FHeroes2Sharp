using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The scenario selection dialog: the panel layout, the size filter, the selection and the buttons.
/// </summary>
public sealed class ScenarioSelectionDialogTests
{
    private static readonly Point PanelOrigin = new(
        (ScreenBuffer.DefaultSize.Width - PanelWidth) / 2,
        (ScreenBuffer.DefaultSize.Height - PanelHeight) / 2);

    private const int PanelWidth = 377;
    private const int PanelHeight = 458;

    [Fact]
    public void ThePanel_IsCentredOnTheScreen()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        Assert.Equal(PanelOrigin, dialog.Area.Position);
        Assert.Equal(PanelWidth, dialog.Area.Width);
        Assert.Equal(PanelHeight, dialog.Area.Height);
    }

    [Fact]
    public void TheList_HangsOffThePanelOrigin()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        Assert.Equal(PanelOrigin.X + ScenarioSelectionDialog.ListOffsetX, dialog.RowArea(0).X);
        Assert.Equal(PanelOrigin.Y + ScenarioSelectionDialog.ListOffsetY, dialog.RowArea(0).Y);
        Assert.Equal(ScenarioSelectionDialog.ListHeight / ScenarioSelectionDialog.VisibleRowCount,
            ScenarioSelectionDialog.RowHeight);
    }

    [Fact]
    public void TheRows_UseTheTruncatedReferencePitch()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);

        // The reference lays row i out at i * 175 / 9, so the pitch alternates between 19 and 20
        // pixels instead of being a fixed 19. A fixed pitch drifts three pixels by the last row.
        int originY = PanelOrigin.Y + ScenarioSelectionDialog.ListOffsetY;

        Assert.Equal(0, dialog.RowArea(0).Y - originY);
        Assert.Equal(19, dialog.RowArea(1).Y - dialog.RowArea(0).Y);
        Assert.Equal(20, dialog.RowArea(3).Y - dialog.RowArea(2).Y);
        Assert.Equal(58, dialog.RowArea(3).Y - originY);
        Assert.Equal(155, dialog.RowArea(8).Y - originY);
        Assert.Equal(ScenarioSelectionDialog.ListHeight,
            dialog.RowArea(ScenarioSelectionDialog.VisibleRowCount - 1).Y
            + dialog.RowArea(ScenarioSelectionDialog.VisibleRowCount - 1).Height - originY);
    }

    [Fact]
    public void ClickingTheMiddleOfEveryRow_SelectsThatRow()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);

        for (int row = 0; row < ScenarioSelectionDialog.VisibleRowCount; ++row)
        {
            Rect area = dialog.RowArea(row);

            dialog.SetCursor(new Point(area.X + 40, area.Y + (area.Height / 2)));
            dialog.SetLeftButton(true);
            dialog.SetLeftButton(false);

            Assert.Equal(row, dialog.SelectedIndex);
        }
    }

    [Fact]
    public void ScrollingByTheWheel_RollsTheRowsWithoutMovingTheSelection()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);
        Rect list = dialog.ListArea;

        dialog.SetCursor(new Point(list.X + 40, list.Y + 40));
        dialog.ScrollBy(4);

        Assert.Equal(4, dialog.Scroll);
        Assert.Equal(0, dialog.SelectedIndex);

        dialog.ScrollBy(-2);
        Assert.Equal(2, dialog.Scroll);
    }

    [Fact]
    public void ScrollingByTheWheel_ClampsToTheList()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);
        Rect list = dialog.ListArea;

        dialog.SetCursor(new Point(list.X + 40, list.Y + 40));

        dialog.ScrollBy(-5);
        Assert.Equal(0, dialog.Scroll);

        dialog.ScrollBy(100);
        Assert.Equal(30 - ScenarioSelectionDialog.VisibleRowCount, dialog.Scroll);
    }

    [Fact]
    public void ScrollingByTheWheel_IgnoresAPointerOutsideTheList()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);

        // The reference only answers a wheel inside the list or the scroll bar.
        dialog.SetCursor(new Point(dialog.Area.X + 5, dialog.Area.Y + 5));
        dialog.ScrollBy(3);

        Assert.Equal(0, dialog.Scroll);
    }

    [Fact]
    public void ClickingTheGroove_JumpsToThatPartOfTheList()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);
        Rect groove = dialog.ScrollBarArea;

        dialog.SetCursor(new Point(groove.X + 4, groove.Y + groove.Height - 1));
        dialog.SetLeftButton(true);
        dialog.SetLeftButton(false);

        Assert.Equal(30 - ScenarioSelectionDialog.VisibleRowCount, dialog.Scroll);
        Assert.Equal(0, dialog.SelectedIndex);
    }

    [Fact]
    public void DraggingTheThumb_ScrollsWithThePointer()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);
        Rect groove = dialog.ScrollBarArea;

        dialog.SetCursor(new Point(groove.X + 4, groove.Y + 2));
        dialog.SetLeftButton(true);
        dialog.SetCursor(new Point(groove.X + 4, groove.Y + groove.Height - 1));
        dialog.SetLeftButton(false);

        Assert.Equal(30 - ScenarioSelectionDialog.VisibleRowCount, dialog.Scroll);

        // Releasing a drag must not pick the row that is under the pointer.
        Assert.Equal(0, dialog.SelectedIndex);
    }

    [Fact]
    public void ClickingTheScrollArrows_RollsTheViewWithoutMovingTheSelection()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);
        Point up = new(
            PanelOrigin.X + ScenarioSelectionDialog.ScrollUpOffsetX + 7,
            PanelOrigin.Y + ScenarioSelectionDialog.ScrollUpOffsetY + 7);
        Point down = new(
            PanelOrigin.X + ScenarioSelectionDialog.ScrollUpOffsetX + 7,
            PanelOrigin.Y + ScenarioSelectionDialog.ScrollDownOffsetY + 7);

        dialog.SetCursor(down);
        dialog.SetLeftButton(true);
        dialog.SetLeftButton(false);

        Assert.Equal(1, dialog.Scroll);
        Assert.Equal(0, dialog.SelectedIndex);

        dialog.SetCursor(up);
        dialog.SetLeftButton(true);
        dialog.SetLeftButton(false);

        Assert.Equal(0, dialog.Scroll);
        Assert.Equal(0, dialog.SelectedIndex);
    }

    [Fact]
    public void WithoutFilter_EveryMapIsListed()
    {
        Mp2FileInfo[] maps = CreateMaps();
        ScenarioSelectionDialog dialog = CreateDialog(maps, out _);

        Assert.Equal(MapSize.None, dialog.Filter);
        Assert.Equal(maps.Length, dialog.Maps.Count);
        Assert.Same(maps[0], dialog.Selected);
    }

    [Fact]
    public void SetFilter_KeepsTheSelectedMapWhenItSurvives()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        // The third map is the second 72x72 one.
        dialog.Select(2);
        Assert.True(dialog.SetFilter(MapSize.Medium));

        Assert.Equal(MapSize.Medium, dialog.Filter);
        Assert.All(dialog.Maps, map => Assert.Equal(72, map.Width));
        Assert.Equal(1, dialog.SelectedIndex);
        Assert.Equal("Medium B", dialog.Selected!.Name);
    }

    [Fact]
    public void SetFilter_FallsBackToTheFirstRow()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        dialog.Select(0);
        Assert.True(dialog.SetFilter(MapSize.Small));

        Assert.Equal(0, dialog.SelectedIndex);
        Assert.Equal("Small A", dialog.Selected!.Name);
    }

    [Fact]
    public void SetFilter_RefusesASizeWithoutMaps()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        Assert.False(dialog.SetFilter(MapSize.ExtraLarge));
        Assert.Equal(MapSize.None, dialog.Filter);
        Assert.Equal(4, dialog.Maps.Count);
    }

    [Fact]
    public void SetFilter_IgnoresTheFilterItAlreadyUses()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        Assert.False(dialog.SetFilter(MapSize.None));
    }

    [Fact]
    public void ClickingASizeButton_AppliesTheFilter()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);
        Rect medium = dialog.FilterArea(1);

        dialog.SetCursor(new Point(medium.X + 3, medium.Y + 3));
        dialog.SetLeftButton(true);
        dialog.SetLeftButton(false);

        Assert.Equal(MapSize.Medium, dialog.Filter);
    }

    [Fact]
    public void Selection_IsClampedToTheList()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        dialog.MoveSelection(-5);
        Assert.Equal(0, dialog.SelectedIndex);

        dialog.MovePage(2);
        Assert.Equal(3, dialog.SelectedIndex);
    }

    [Fact]
    public void TheList_ScrollsToKeepTheSelectionVisible()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);

        Assert.Equal(0, dialog.Scroll);

        dialog.Select(ScenarioSelectionDialog.VisibleRowCount);
        Assert.Equal(1, dialog.Scroll);

        dialog.Select(29);
        Assert.Equal(30 - ScenarioSelectionDialog.VisibleRowCount, dialog.Scroll);

        dialog.Select(0);
        Assert.Equal(0, dialog.Scroll);
    }

    [Fact]
    public void ClickingARow_SelectsIt()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);
        Rect row = dialog.RowArea(2);

        dialog.SetCursor(new Point(row.X + 40, row.Y + 4));
        dialog.SetLeftButton(true);
        dialog.SetLeftButton(false);

        Assert.Equal(2, dialog.SelectedIndex);
    }

    [Fact]
    public void DraggingToAnotherRow_DoesNotSelect()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);
        Rect row = dialog.RowArea(2);

        dialog.SetCursor(new Point(row.X + 40, row.Y + 4));
        dialog.SetLeftButton(true);
        dialog.SetCursor(new Point(row.X + 40, row.Y + ScenarioSelectionDialog.RowHeight + 4));
        dialog.SetLeftButton(false);

        Assert.Equal(0, dialog.SelectedIndex);
    }

    [Fact]
    public void ClickingOkay_ReturnsOk()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);
        Rect okay = dialog.OkayArea;

        Assert.False(okay.IsEmpty);

        dialog.SetCursor(new Point(okay.X + 10, okay.Y + 10));
        dialog.SetLeftButton(true);

        Assert.Equal(DialogButton.Ok, dialog.SetLeftButton(false));
    }

    [Fact]
    public void TheHotKeys_AnswerOkayAndCancel()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        Assert.Equal(DialogButton.Ok, dialog.ProcessHotKey(ButtonHotKey.Okay));
        Assert.Equal(DialogButton.Cancel, dialog.ProcessHotKey(ButtonHotKey.Cancel));
    }

    [Fact]
    public void Drawing_PaintsTheShadowThenThePanel()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);
        dialog.Draw(target);

        Assert.Equal(PanelColor, target[dialog.Area.X + 2, dialog.Area.Y + 2]);
        Assert.Equal(ShadowColor,
            target[dialog.Area.X - ScenarioSelectionDialog.ShadowOffset + 2,
                dialog.Area.Y + ScenarioSelectionDialog.ShadowOffset + 2]);
    }

    [Fact]
    public void Drawing_PaintsTheIconsOfEveryRow()
    {
        Mp2FileInfo[] maps = CreateMaps();
        ScenarioSelectionDialog dialog = CreateDialog(maps, out _);

        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);
        dialog.Draw(target);

        Rect row = dialog.RowArea(0);

        // The row columns are drawn from the panel edge, only the row's y comes from the list area
        // (ScenarioListBox::_offsetX is the dialog position in the reference).
        // The fake icon table fills sprite n with colour IconColorBase + n, so the player count icon
        // of a two kingdom map is the one at index 21.
        Assert.Equal(IconColorBase + 21,
            target[dialog.Area.X + ScenarioSelectionDialog.RowPlayerIconOffsetX,
                row.Y + ScenarioSelectionDialog.RowIconOffsetY]);
        Assert.Equal(IconColorBase + ScenarioDialogArtwork.MapSizeIconIndex,
            target[dialog.Area.X + ScenarioSelectionDialog.RowSizeIconOffsetX,
                row.Y + ScenarioSelectionDialog.RowIconOffsetY]);
    }

    [Fact]
    public void Drawing_UsesTheSelectedFontOnlyForTheCurrentRow()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);
        dialog.Draw(target);

        // The fake fonts are solid blocks: white for the list, yellow for the current row.
        Assert.Contains(SelectedFontColor, RowPixels(target, dialog, 0));
        Assert.DoesNotContain(SelectedFontColor, RowPixels(target, dialog, 1));
        Assert.Contains(FontColor, RowPixels(target, dialog, 1));
    }

    [Fact]
    public void Drawing_PaintsTheSelectedScenarioAndItsDescription()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);

        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);
        dialog.Draw(target);

        int y = dialog.Area.Y + ScenarioSelectionDialog.SelectedOffsetY;

        Assert.Equal(IconColorBase + 21,
            target[dialog.Area.X + ScenarioSelectionDialog.SelectedPlayerIconOffsetX, y]);
        Assert.Contains(FontColor, SelectedNamePixels(dialog, target));
        Assert.Contains(FontColor, DifficultyPixels(dialog, target));
        Assert.Contains(FontColor, DescriptionPixels(dialog, target));
    }

    [Fact]
    public void Drawing_SkipsTheScrollBarForAShortList()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);
        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);

        dialog.Draw(target);

        Rect groove = dialog.ScrollBarArea;
        byte[] pixels = Pixels(target, groove.X, groove.Y, groove.Width, groove.Height);

        Assert.DoesNotContain(SliderColor, pixels);
    }

    [Fact]
    public void Drawing_PaintsTheScrollBarThumbForALongList()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(30), out _);
        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);

        dialog.Draw(target);

        Rect groove = dialog.ScrollBarArea;
        byte[] pixels = Pixels(target, groove.X, groove.Y, groove.Width, groove.Height);

        Assert.Contains(SliderColor, pixels);
    }

    [Fact]
    public void Drawing_PaintsThePressedMapSizeButtonOfTheActiveFilter()
    {
        ScenarioSelectionDialog dialog = CreateDialog(CreateMaps(), out _);
        PixelBuffer target = new(ScreenBuffer.DefaultSize.Width, ScreenBuffer.DefaultSize.Height);

        dialog.Draw(target);

        Rect all = dialog.FilterArea(4);
        Rect small = dialog.FilterArea(0);

        // The released frames are filled with FilterColorBase + 2 * index, the pressed ones with + 1.
        Assert.Equal(FilterColorBase + 9, target[all.X + 1, all.Y + 1]);
        Assert.Equal(FilterColorBase + 0, target[small.X + 1, small.Y + 1]);

        Assert.True(dialog.SetFilter(MapSize.Small));
        dialog.Draw(target);

        Assert.Equal(FilterColorBase + 1, target[small.X + 1, small.Y + 1]);
        Assert.Equal(FilterColorBase + 8, target[all.X + 1, all.Y + 1]);
    }

    [Fact]
    public void Artwork_ReportsMissingIcons()
    {
        ScenarioDialogArtwork artwork = CreateArtwork(iconCount: 5, withSlider: false, withMapTypes: false);

        Assert.Null(artwork.PlayerCountIcon(1));
        Assert.Null(artwork.MapSizeIcon(MapSize.Small));
        Assert.Null(artwork.VictoryIcon(MapVictoryCondition.DefeatEveryone));
        Assert.Null(artwork.LossIcon(MapLossCondition.Everything));
        Assert.Null(artwork.FilterButton(0));
        Assert.Null(artwork.ScrollUpButton);
        Assert.Null(artwork.ScrollDownButton);
        Assert.Null(artwork.MapTypeIcon(MapVersion.SuccessionWars));
    }

    [Fact]
    public void Artwork_MapsTheIconsAndButtonsLikeTheReference()
    {
        ScenarioDialogArtwork artwork = CreateArtwork();

        Assert.Equal(IconColorBase + ScenarioDialogArtwork.PlayerCountIconIndex + 6,
            artwork.PlayerCountIcon(6)!.Image[0, 0]);
        Assert.Null(artwork.PlayerCountIcon(0));
        Assert.Null(artwork.PlayerCountIcon(7));

        Assert.Equal(IconColorBase + ScenarioDialogArtwork.MapSizeIconIndex + 3,
            artwork.MapSizeIcon(MapSize.ExtraLarge)!.Image[0, 0]);
        Assert.Null(artwork.MapSizeIcon(MapSize.None));

        Assert.Equal(IconColorBase + ScenarioDialogArtwork.VictoryIconIndex + (int)MapVictoryCondition.CollectGold,
            artwork.VictoryIcon(MapVictoryCondition.CollectGold)!.Image[0, 0]);
        Assert.Equal(IconColorBase + ScenarioDialogArtwork.LossIconIndex + (int)MapLossCondition.OutOfTime,
            artwork.LossIcon(MapLossCondition.OutOfTime)!.Image[0, 0]);

        // The five filter buttons are the frame pairs 9/10, 11/12, … 17/18 of REQUESTS.ICN.
        Assert.Equal(IconColorBase + ScenarioDialogArtwork.FilterButtonFirstIndex + 8,
            artwork.FilterButton(4)!.Value.Released.Image[0, 0]);
        Assert.Equal(IconColorBase + ScenarioDialogArtwork.FilterButtonFirstIndex + 9,
            artwork.FilterButton(4)!.Value.Pressed.Image[0, 0]);
        Assert.Null(artwork.FilterButton(5));

        Assert.Equal(IconColorBase + ScenarioDialogArtwork.ScrollUpButtonIndex + 1,
            artwork.ScrollUpButton!.Value.Pressed.Image[0, 0]);
        Assert.Equal(IconColorBase + ScenarioDialogArtwork.ScrollDownButtonIndex,
            artwork.ScrollDownButton!.Value.Released.Image[0, 0]);
    }

    [Fact]
    public void Artwork_RejectsMissingTables()
    {
        Sprite panel = Filled(PanelWidth, PanelHeight, PanelColor);

        Assert.Throws<ArgumentNullException>(() => new ScenarioDialogArtwork(null!, null, []));
        Assert.Throws<ArgumentNullException>(() => new ScenarioDialogArtwork(panel, null, null!));
    }

    /// <summary>Colour the fake panel is filled with; a value no icon or text uses.</summary>
    private const byte PanelColor = 9;

    /// <summary>Colour of the fake shadow.</summary>
    private const byte ShadowColor = 5;

    /// <summary>Colour of the fake scroll bar slider.</summary>
    private const byte SliderColor = 60;

    /// <summary>Colour of the first sprite of the fake <c>REQUESTS.ICN</c> table.</summary>
    private const byte IconColorBase = 100;

    /// <summary>Colour of the first released map size filter frame, which is sprite 9 of the table.</summary>
    private const byte FilterColorBase = IconColorBase + ScenarioDialogArtwork.FilterButtonFirstIndex;

    /// <summary>Colour of the fake white font.</summary>
    private const byte FontColor = 2;

    /// <summary>Colour of the fake yellow font used for the current row.</summary>
    private const byte SelectedFontColor = 3;

    /// <summary>Line height of the fake fonts.</summary>
    private const int FontHeight = 15;

    private static ScenarioSelectionDialog CreateDialog(Mp2FileInfo[] maps, out ScenarioDialogArtwork artwork)
    {
        artwork = CreateArtwork();

        return new ScenarioSelectionDialog(
            artwork,
            new FakeGlyphSource(FontColor),
            new FakeGlyphSource(SelectedFontColor),
            ScenarioDialogText.Default,
            maps);
    }

    /// <summary>
    /// Builds the artwork of a real dialog out of flat sprites: a 377x458 panel, a 377x455 shadow,
    /// the 40 frames of <c>REQUESTS.ICN</c> (56x25 button frames, 14x14 arrows, 17x17 icons), an
    /// 8x17 slider and the two OKAY frames. Sprite <c>n</c> of the icon table is filled with
    /// <c>IconColorBase + n</c>, so a painted pixel says which sprite was used.
    /// </summary>
    private static ScenarioDialogArtwork CreateArtwork(int iconCount = 40, bool withSlider = true, bool withMapTypes = true)
    {
        List<Sprite> icons = [];

        for (int index = 0; index < iconCount; ++index)
        {
            byte color = (byte)(IconColorBase + index);
            Sprite sprite = index is >= ScenarioDialogArtwork.FilterButtonFirstIndex and <= 18
                ? Filled(56, 25, color)
                : index is >= ScenarioDialogArtwork.ScrollUpButtonIndex and <= 8
                    ? Filled(14, 14, color)
                    : Filled(ScenarioDialogArtwork.IconSize, ScenarioDialogArtwork.IconSize, color);

            icons.Add(sprite);
        }

        List<Sprite> mapTypes = [Filled(17, 17, 150), Filled(17, 17, 151), Filled(17, 17, 152)];

        return new ScenarioDialogArtwork(
            Filled(PanelWidth, PanelHeight, PanelColor),
            Filled(PanelWidth, PanelHeight - 3, ShadowColor),
            icons,
            withSlider ? Filled(8, 17, SliderColor) : null,
            new DialogButtonArtwork(Filled(95, 25, 70), Filled(95, 25, 71)),
            withMapTypes ? mapTypes : null);
    }

    private static Sprite Filled(int width, int height, byte color)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);

        return new Sprite(image);
    }

    /// <summary>Four maps: one 36x36, two 72x72 and one 108x108, each with two kingdoms.</summary>
    private static Mp2FileInfo[] CreateMaps() =>
    [
        CreateMap("Small A", 36),
        CreateMap("Medium A", 72),
        CreateMap("Medium B", 72),
        CreateMap("Large A", 108),
    ];

    /// <summary><paramref name="count"/> maps of 72x72, enough to fill more than one page.</summary>
    private static Mp2FileInfo[] CreateMaps(int count) =>
        [.. Enumerable.Range(0, count).Select(index => CreateMap($"Map {index}", 72))];

    private static Mp2FileInfo CreateMap(string name, int width) => new()
    {
        FilePath = $@"C:\maps\{name}.mp2",
        Name = name,
        Description = "described",
        Width = width,
        Height = width,
        Difficulty = GameDifficulty.Normal,
        KingdomColors = PlayerColor.Blue | PlayerColor.Red,
        HumanPlayers = PlayerColor.Blue,
        ComputerPlayers = PlayerColor.Red,
    };

    /// <summary>Colours of the name column of <paramref name="row"/>.</summary>
    private static byte[] RowPixels(PixelBuffer target, ScenarioSelectionDialog dialog, int row)
    {
        Rect area = dialog.RowArea(row);

        return Pixels(
            target,
            area.X + ScenarioSelectionDialog.RowNameOffsetX,
            area.Y + ScenarioSelectionDialog.RowTextOffsetY,
            ScenarioSelectionDialog.RowNameWidth,
            FontHeight);
    }

    /// <summary>Colours of the name of the selected map in the details.</summary>
    private static byte[] SelectedNamePixels(ScenarioSelectionDialog dialog, PixelBuffer target) => Pixels(
        target,
        dialog.Area.X + ScenarioSelectionDialog.SelectedNameOffsetX,
        dialog.Area.Y + ScenarioSelectionDialog.SelectedOffsetY + ScenarioSelectionDialog.SelectedNameOffsetY,
        ScenarioSelectionDialog.SelectedNameWidth,
        FontHeight);

    /// <summary>Colours of the difficulty box.</summary>
    private static byte[] DifficultyPixels(ScenarioSelectionDialog dialog, PixelBuffer target) => Pixels(
        target,
        dialog.Area.X + ScenarioSelectionDialog.DifficultyOffsetX,
        dialog.Area.Y + ScenarioSelectionDialog.DifficultyOffsetY,
        ScenarioSelectionDialog.DifficultyWidth,
        FontHeight);

    /// <summary>Colours of the description box.</summary>
    private static byte[] DescriptionPixels(ScenarioSelectionDialog dialog, PixelBuffer target) => Pixels(
        target,
        dialog.Area.X + ScenarioSelectionDialog.DescriptionOffsetX,
        dialog.Area.Y + ScenarioSelectionDialog.DescriptionOffsetY,
        ScenarioSelectionDialog.DescriptionWidth,
        ScenarioSelectionDialog.DescriptionHeight);

    private static byte[] Pixels(PixelBuffer target, int x, int y, int width, int height)
    {
        List<byte> pixels = [];

        for (int row = y; row < y + height; ++row)
        {
            for (int column = x; column < x + width; ++column)
            {
                pixels.Add(target[column, row]);
            }
        }

        return [.. pixels];
    }

    /// <summary>A glyph source whose glyphs are solid blocks; the colour tells fonts apart.</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color)
        {
            PixelBuffer image = new(6, FontHeight);
            image.Fill(color);
            _glyph = new Sprite(image);
        }

        public int LineHeight => FontHeight;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
