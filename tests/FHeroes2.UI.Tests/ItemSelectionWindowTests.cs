using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="ItemSelectionWindow"/>, <see cref="SpellSelectionWindow"/> and
/// <see cref="SpellSelectDialog"/> against <c>dialog_selectitems.cpp:379-421</c> and
/// <c>889-1160</c>.
/// </summary>
public sealed class ItemSelectionWindowTests
{
    private const byte Canvas = 3;

    [Fact]
    public void DialogSizeLeavesTheDeductionOnTopOfTheScreenHeight()
    {
        Assert.Equal(new Size(340, 330), SpellSelectDialog.DialogSize());
        Assert.Equal(new Size(340, 450), SpellSelectDialog.DialogSize(new Size(800, 600)));
    }

    [Fact]
    public void BuildSpellListPutsTheRandomEntryFirst()
    {
        List<int> spells = SpellSelectDialog.BuildSpellList(includeRandomSpells: true);

        Assert.Equal((int)SpellType.Random, spells[0]);
        Assert.Equal(Spell.SuitableForSpellBook().Count + 1, spells.Count);
    }

    [Fact]
    public void BuildSpellListLeavesTheRandomEntryOutWhenNotAsked()
    {
        List<int> spells = SpellSelectDialog.BuildSpellList(includeRandomSpells: false);

        Assert.Equal(Spell.SuitableForSpellBook().Select(spell => (int)spell), spells);
    }

    [Fact]
    public void BuildSpellListFollowsTheRequestedLevel()
    {
        List<int> spells = SpellSelectDialog.BuildSpellList(includeRandomSpells: false, spellsLevel: 2);

        Assert.Equal(Spell.SuitableForSpellBook(2).Select(spell => (int)spell), spells);
    }

    [Fact]
    public void BuildSpellListDropsTheExcludedSpells()
    {
        HashSet<int> excluded = new() { (int)SpellType.Fireball, (int)SpellType.LightningBolt };

        List<int> spells = SpellSelectDialog.BuildSpellList(includeRandomSpells: false,
            excludeSpellsList: excluded);

        Assert.DoesNotContain((int)SpellType.Fireball, spells);
        Assert.DoesNotContain((int)SpellType.LightningBolt, spells);
        Assert.Equal(Spell.SuitableForSpellBook().Count - 2, spells.Count);
    }

    [Fact]
    public void TryCreateReturnsNullWhenNothingIsLeft()
    {
        // Level ten does not exist, so the reference's list is empty and its message is shown instead.
        SpellSelectionWindow? window = SpellSelectDialog.TryCreate((int)SpellType.None,
            includeRandomSpells: false, excludeSpellsList: null, spellsLevel: 10, Array.Empty<Sprite>(),
            CreateArtwork(), CreateFont(), CreateFont());

        Assert.Null(window);
    }

    [Fact]
    public void WindowLaysTheListBetweenTheTitleAndTheButtons()
    {
        SpellSelectionWindow window = CreateWindow();

        Assert.Equal(new Rect(150, 75, 340, 330), window.Window.ActiveArea);
        Assert.Equal(new Rect(160, 105, 300, 260), window.ListArea);
        Assert.Equal(new Rect(165, 110, 290, 250), window.ItemsArea);

        // 250 / 55 truncates to four rows, exactly like the reference's integer division.
        Assert.Equal(4, window.VisibleItemCount);
    }

    [Fact]
    public void WindowLaysTheScrollBarIntoTheListArea()
    {
        SpellSelectionWindow window = CreateWindow();

        // activeArea.x + width - 25 + 3, listArea.y + 19, ten pixels wide, minus 38 for the two steps.
        Assert.Equal(new Rect(468, 124, 10, 222), window.ScrollBar.Area);
    }

    [Fact]
    public void WindowPutsTheButtonsIntoTheBottomCorners()
    {
        SpellSelectionWindow window = CreateWindow();
        Rect area = window.Window.ActiveArea;

        Assert.Equal(2, window.Buttons.Count);

        Rect okay = window.Buttons[0].Area;
        Rect cancel = window.Buttons[1].Area;

        // Twenty pixels in from the left and the right, seven up from the bottom.
        Assert.Equal(area.X + 20, okay.X);
        Assert.Equal(area.Y + area.Height - okay.Height - 7, okay.Y);
        Assert.Equal(area.X + area.Width - cancel.Width - 20, cancel.X);
        Assert.Equal(area.Y + area.Height - cancel.Height - 7, cancel.Y);
    }

    [Fact]
    public void WindowStartsOnTheSpellTheCallerAskedFor()
    {
        SpellSelectionWindow window = CreateWindow();

        window.SelectSpell(SpellType.Fireball);

        Assert.Equal(SpellType.Fireball, window.SelectedSpell);
    }

    [Fact]
    public void WindowAnswersOkayOnADoubleClickOfTheSelectedRow()
    {
        SpellSelectionWindow window = CreateWindow();
        window.SelectSpell(SpellType.Fireball);

        // The list starts at the first row, so the fireball sits in the first row; click it again.
        Point point = new(window.ItemsArea.X + 20, window.ItemsArea.Y + (window.ItemsArea.Height / 4 / 2));

        window.ProcessEvents(Click(point));

        Assert.Equal(DialogButton.Ok, window.Result);
        Assert.True(window.IsClosed);
    }

    [Fact]
    public void WindowAnswersCancelOnAClickOfTheCancelButton()
    {
        SpellSelectionWindow window = CreateWindow();
        Point point = window.Buttons[1].Area.Center;

        window.ProcessEvents(Click(point));

        Assert.Equal(DialogButton.Cancel, window.Result);
    }

    [Fact]
    public void WindowShowsTheSpellDescriptionOnARightClick()
    {
        SpellSelectionWindow window = CreateWindow();
        Point point = new(window.ItemsArea.X + 60, window.ItemsArea.Y + 10);

        window.ProcessEvents(RightClick(point));

        Spell fireball = new(SpellType.Fireball);

        Assert.NotNull(window.Popup);
        Assert.Equal(fireball.GetName(), window.Popup.Value.Header);
        Assert.Equal(fireball.GetDescription(), window.Popup.Value.Body);
        Assert.Equal(DialogButton.None, window.Result);
    }

    [Fact]
    public void RedrawBackgroundShadesTheListAreaAndPaintsTheGroove()
    {
        SpellSelectionWindow window = CreateWindow();
        PixelBuffer image = CreateBuffer(640, 480, Canvas);

        window.RedrawBackground(new Point(0, 0), image);

        // The shading leaves the very inner pixels alone but darkens the frame around the list.
        Assert.NotEqual(Canvas, image[window.ListArea.X + 1, window.ListArea.Y + window.ListArea.Height / 2]);

        // The groove is sixteen pixels wide and sits twenty five pixels in from the right edge of the
        // active area, on top of the shading.
        Assert.Equal(16, window.GrooveArea.Width);
        Assert.Equal(window.Window.ActiveArea.X + window.Window.ActiveArea.Width - 25, window.GrooveArea.X);
        Assert.Equal(40, image[window.GrooveArea.X + 8, window.GrooveArea.Y + 40]);
    }

    [Fact]
    public void WindowRejectsAnEmptyTitle()
    {
        Assert.Throws<ArgumentException>(() => new TestWindow(string.Empty, CreateArtwork()));
    }

    [Fact]
    public void WindowRejectsMissingArtwork()
    {
        Assert.Throws<ArgumentNullException>(() => new TestWindow("Test", null!));
    }

    private static SpellSelectionWindow CreateWindow()
    {
        return new SpellSelectionWindow(
            new[] { (int)SpellType.Fireball, (int)SpellType.LightningBolt, (int)SpellType.Teleport },
            CreateIcons(), SpellSelectDialog.DialogSize(), CreateArtwork(), CreateFont(), CreateFont());
    }

    private static IReadOnlyList<Sprite> CreateIcons()
    {
        List<Sprite> icons = new();

        for (int i = 0; i < 80; ++i)
        {
            icons.Add(new Sprite(CreateBuffer(20, 16, (byte)(i + 1))));
        }

        return icons;
    }

    private static ItemSelectionWindowArtwork CreateArtwork()
    {
        // The groove comes from (536, 176) of the mask, so those pixels get a value of their own and the
        // test can tell the groove apart from the shading around it.
        PixelBuffer groove = CreateBuffer(560, 320, 9);

        for (int y = 176; y < 306; ++y)
        {
            for (int x = 536; x < 552; ++x)
            {
                groove[x, y] = 40;
            }
        }

        return new ItemSelectionWindowArtwork
        {
            ScrollBarBackground = new Sprite(groove),
            ScrollUpReleased = new Sprite(CreateBuffer(14, 15, 11)),
            ScrollUpPressed = new Sprite(CreateBuffer(14, 15, 12)),
            ScrollDownReleased = new Sprite(CreateBuffer(14, 15, 13)),
            ScrollDownPressed = new Sprite(CreateBuffer(14, 15, 14)),
            ScrollSlider = new Sprite(CreateBuffer(10, 25, 15)),
            Okay = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 16)),
                new Sprite(CreateBuffer(40, 20, 17))),
            Cancel = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 18)),
                new Sprite(CreateBuffer(40, 20, 19))),
        };
    }

    private static PixelBuffer CreateBuffer(int width, int height, int value)
    {
        PixelBuffer buffer = new(width, height);
        buffer.Fill((byte)value);

        return buffer;
    }

    private static IGlyphSource CreateFont() => new FakeGlyphSource();

    private static LocalEvent Click(Point point)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        return events;
    }

    private static LocalEvent RightClick(Point point)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, point);

        return events;
    }

    private sealed class TestWindow : ItemSelectionWindow
    {
        public TestWindow(string title, ItemSelectionWindowArtwork artwork)
            : base(new Size(340, 330), title, string.Empty, artwork, CreateFont(), CreateFont())
        {
        }

        public override void RedrawItem(ItemSelectionEntry item, int x, int y, bool current, PixelBuffer output)
        {
        }
    }

    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph = new(CreateBuffer(6, 12, 1));

        public int LineHeight => 12;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }
}
