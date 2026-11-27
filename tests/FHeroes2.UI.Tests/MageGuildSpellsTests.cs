using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using GameAssets = FHeroes2.Game.Assets;
using MapRace = FHeroes2.Assets.Maps.MapRace;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the mage guild spells sub-window of the castle details dialog: where the five rows and the three buttons
/// sit, what a click asks the host for, and what OKAY writes (<c>editor_castle_details_window.cpp:318-606</c>).
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MageGuildSpellsTests
{
    private const byte AttributeColor = 5;

    private const int NormalLineHeight = 14;

    /// <summary>The area of the castle details window the sub-window takes over, <c>dialogRoi</c>.</summary>
    private static readonly Rect DialogArea = new(16, 16, 608, 448);

    [Fact]
    public void RowsSitWhereTheReferencePutsThem() => Assert.True(TryWithWindow(subject =>
    {
        // 一级在最下面（dialogRoi.y + 365），往上每级 90 像素（:359）。
        // 给的偏移是这一排的中心：卷轴以它为准左右居中（SpellsInOneRow::setCoords）。
        for (int levelIndex = 0; levelIndex < subject.View.Rows.Count; ++levelIndex)
        {
            Rect firstScroll = subject.View.Rows[levelIndex].Coords[0];

            Assert.Equal(DialogArea.X + MageGuildSpellsView.RowsOffsetX, firstScroll.X + (firstScroll.Width / 2));
            Assert.Equal(
                DialogArea.Y + MageGuildSpellsView.FirstRowOffsetY - (MageGuildSpellsView.RowStepY * levelIndex),
                firstScroll.Y);
        }
    }));

    [Fact]
    public void OkayAndExitHugTheEndsOfTheStatusBar() => Assert.True(TryWithWindow(subject =>
    {
        Assert.Equal(DialogArea.X, subject.View.OkayArea.X);
        Assert.Equal(DialogArea.Right, subject.View.ExitArea.Right);
        Assert.Equal(DialogArea.Bottom, subject.View.OkayArea.Bottom);
        Assert.Equal(DialogArea.Bottom, subject.View.ExitArea.Bottom);
    }));

    [Fact]
    public void BannedSpellsButtonSitsTenPixelsFromTheTopRightCorner() => Assert.True(TryWithWindow(subject =>
    {
        Assert.Equal(DialogArea.Right - MageGuildSpellsView.BannedSpellsInset, subject.View.BannedSpellsArea.Right);
        Assert.Equal(DialogArea.Y + MageGuildSpellsView.BannedSpellsInset, subject.View.BannedSpellsArea.Y);
        Assert.True(subject.View.StatusBarMessageArea.Width > 0);
    }));

    [Fact]
    public void PointerOverAScrollFindsItsLevel() => Assert.True(TryWithWindow(subject =>
    {
        Assert.Equal(0, subject.View.RowIndexAt(Center(subject.View.Rows[0].Coords[0])));
        Assert.Equal(3, subject.View.RowIndexAt(Center(subject.View.Rows[3].Coords[0])));

        // 两排之间的空当什么都不算。
        Assert.Equal(-1, subject.View.RowIndexAt(new Point(subject.View.Rows[0].Coords[0].X, DialogArea.Y + 1)));
    }));

    [Fact]
    public void ClickOnAScrollAsksTheHostForThePicker() => Assert.True(TryWithWindow(subject =>
    {
        subject.Input.Process(Click(Center(subject.View.Rows[1].Coords[0])));

        // 参考 :583-591：点哪一格就让宿主为哪一格开选择器。占位符永远排掉，所以这一级能选的实际上是空的。
        Assert.Equal(MageGuildSpellsRequestKind.SelectSpell, subject.Input.Request.Kind);
        Assert.Equal(2, subject.Input.Request.SpellLevel);
        Assert.Equal(MageGuildSpellTable.GetPlaceholder(2), subject.Input.Request.CurrentSpell);
        Assert.Equal(new[] { (int)MageGuildSpellTable.GetPlaceholder(2) }, subject.Input.Request.ExcludedSpells);
    }));

    [Fact]
    public void PickerAnswerLandsInTheClickedSlot() => Assert.True(TryWithWindow(subject =>
    {
        subject.Input.Process(Click(Center(subject.View.Rows[0].Coords[0])));
        subject.Input.ApplySelectedSpell(SpellType.Bless);

        Assert.Equal(SpellType.Bless, subject.Table.GetSpell(0, 0));
        Assert.Equal(SpellType.Bless, subject.View.Rows[0].Spells[0]);

        // 取消掉的选择器（SPELL_NONE）不写任何东西。
        subject.Input.ApplySelectedSpell(SpellType.None);

        Assert.Equal(SpellType.Bless, subject.Table.GetSpell(0, 0));
    }));

    [Fact]
    public void CurrentSpellStaysSelectable() => Assert.True(TryWithWindow(subject =>
    {
        subject.Table.SetSpell(0, 0, SpellType.Bless);
        subject.View.Refresh();

        subject.Input.Process(Click(Center(subject.View.Rows[0].Coords[0])));

        // 自己那一格留着可选，否则「点错了想点回来」就回不来（:417-419 的 != currentSpellId）。
        Assert.DoesNotContain((int)SpellType.Bless, subject.Input.Request.ExcludedSpells);
    }));

    [Fact]
    public void OkayWritesTheRowsIntoTheMetadata() => Assert.True(TryWithWindow(subject =>
    {
        subject.Table.SetSpell(0, 0, SpellType.Bless);
        subject.Table.SetSpell(4, 0, SpellType.Armageddon);
        subject.View.Refresh();

        Assert.Equal(MageGuildSpellsOutcome.Accepted, subject.Input.Process(Click(Center(subject.View.OkayArea))));

        Assert.True(subject.Input.HasChanges);

        // 键是「等级下标乘十加槽位」的两位数（:508-512）。
        Assert.Equal((int)SpellType.Bless, subject.Metadata.MustHaveSpells[0]);
        Assert.Equal((int)SpellType.Armageddon, subject.Metadata.MustHaveSpells[40]);
    }));

    [Fact]
    public void ExitLeavesTheMetadataAlone() => Assert.True(TryWithWindow(subject =>
    {
        subject.Table.SetSpell(0, 0, SpellType.Bless);

        Assert.Equal(MageGuildSpellsOutcome.Cancelled, subject.Input.Process(Click(Center(subject.View.ExitArea))));

        Assert.False(subject.Input.HasChanges);
        Assert.Empty(subject.Metadata.MustHaveSpells);
    }));

    [Fact]
    public void DefaultOkayHotKeyAcceptsTheRows() => Assert.True(TryWithWindow(subject =>
    {
        subject.Table.SetSpell(0, 0, SpellType.Bless);
        subject.View.Refresh();

        // 参考 :447-448：回车与 OKAY 是同一条路，模态层直接调这一入口。
        Assert.Equal(MageGuildSpellsOutcome.Accepted, subject.Input.Accept());

        Assert.True(subject.Input.HasChanges);
        Assert.Equal((int)SpellType.Bless, subject.Metadata.MustHaveSpells[0]);
    }));

    [Fact]
    public void DefaultCancelHotKeyDropsTheRows() => Assert.True(TryWithWindow(subject =>
    {
        subject.Table.SetSpell(0, 0, SpellType.Bless);

        Assert.Equal(MageGuildSpellsOutcome.Cancelled, subject.Input.Cancel());

        Assert.False(subject.Input.HasChanges);
        Assert.Empty(subject.Metadata.MustHaveSpells);
    }));

    [Fact]
    public void ClickOnBannedSpellsAsksForTheWindow() => Assert.True(TryWithWindow(subject =>
    {
        subject.Input.Process(Click(Center(subject.View.BannedSpellsArea)));

        Assert.Equal(MageGuildSpellsRequestKind.SelectBannedSpells, subject.Input.Request.Kind);
        Assert.Equal(1, subject.Input.Request.SpellLevel);

        // 标题是该等级法师公会的名字加冒号，清单只给这一级的那几条（:486-517）。
        Assert.EndsWith(":", subject.Input.Request.Title);
        Assert.Empty(subject.Input.Request.BannedSpells);
    }));

    [Fact]
    public void BanningTakesTheSpellOutOfItsRow() => Assert.True(TryWithWindow(subject =>
    {
        subject.Table.SetSpell(0, 0, SpellType.Bless);
        subject.View.Refresh();

        subject.Input.ApplyBannedSpells(1, [(int)SpellType.Bless]);

        Assert.True(subject.Input.HasChanges);
        Assert.Equal(new[] { (int)SpellType.Bless }, subject.Table.BannedSpells);
        Assert.Equal(MageGuildSpellTable.GetPlaceholder(1), subject.Table.GetSpell(0, 0));
    }));

    [Fact]
    public void BanningOneLevelKeepsTheOthers() => Assert.True(TryWithWindow(subject =>
    {
        subject.Input.ApplyBannedSpells(1, [(int)SpellType.Bless]);
        subject.Input.ApplyBannedSpells(5, [(int)SpellType.DragonSlayer]);

        Assert.Equal(new[] { (int)SpellType.Bless, (int)SpellType.DragonSlayer }, subject.Table.BannedSpells);
        Assert.Equal(5, subject.Input.CurrentBannedSpellsLevel);
    }));

    [Fact]
    public void TheBannedWindowReopensAtTheLevelItWasMovedTo() => Assert.True(TryWithWindow(subject =>
    {
        // 参考在换等级时改掉 currentBannedSpellsLevel 再重开（:515-521）；宿主只记等级，下一次点按钮从这里开。
        subject.Input.FocusBannedSpellsLevel(4);
        subject.Input.Process(Click(Center(subject.View.BannedSpellsArea)));

        Assert.Equal(MageGuildSpellsRequestKind.SelectBannedSpells, subject.Input.Request.Kind);
        Assert.Equal(4, subject.Input.Request.SpellLevel);
    }));

    [Fact]
    public void HoveringOkayShowsItsHint() => Assert.True(TryWithWindow(subject =>
    {
        subject.Input.Process(Hover(Center(subject.View.OkayArea)));

        Assert.Equal(MageGuildSpellsInput.OkayMessage, subject.View.Message);

        subject.Input.Process(RightPress(Center(subject.View.OkayArea)));

        Assert.Equal(MageGuildSpellsRequestKind.ShowMessage, subject.Input.Request.Kind);
        Assert.Equal(MageGuildSpellsInput.OkayTitle, subject.Input.Request.Title);
    }));

    [Fact]
    public void PointerAwayFromEverythingRestoresTheDefaultHint() => Assert.True(TryWithWindow(subject =>
    {
        subject.Input.Process(Hover(Center(subject.View.OkayArea)));

        // 卷轴左边那一大片空石头，什么都不压。
        subject.Input.Process(Hover(new Point(DialogArea.X + 40, DialogArea.Y + 200)));

        Assert.Equal(MageGuildSpellsInput.DefaultMessage, subject.View.Message);
    }));

    [Fact]
    public void PaintCoversTheWholeDialogArea() => Assert.True(TryWithWindow(subject =>
    {
        PixelBuffer canvas = CreateCanvas();
        subject.View.Paint(canvas);

        // 石头底铺满整块区域（状态栏那一条也画了），所以四角不可能是空的。
        Assert.NotEqual(0, canvas[DialogArea.X, DialogArea.Y]);
        Assert.NotEqual(0, canvas[DialogArea.Right - 1, DialogArea.Bottom - 1]);
        Assert.Equal(0, canvas[DialogArea.X - 1, DialogArea.Y]);
    }));

    /// <summary>Creates the view against the installed game data.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithWindow(Action<TestSubject> body)
    {
        AssetBundle? previous = GameAssets.Current;
        AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            MageGuildSpellsArtwork artwork = MageGuildSpellsArtwork.TryCreate(assets, MapRace.Knight,
                isEvilInterface: false) ?? throw new InvalidOperationException("The artwork of the sub-window is missing.");

            MageGuildSpellTable table = new(MapRace.Knight);
            CastleMetadata metadata = new();
            MageGuildSpellsView view = new(table, artwork, DialogArea, new FakeGlyphSource(NormalLineHeight));

            body(new TestSubject(table, view, new MageGuildSpellsInput(view, metadata), metadata));
        }
        finally
        {
            // 还原成进来时的那个：同一个集合里的其它用例可能还想接着用。
            GameAssets.Current = previous;
            assets.Dispose();
        }

        return true;
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static LocalEvent Hover(Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);

        return events;
    }

    private static LocalEvent Click(Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return events;
    }

    private static LocalEvent RightPress(Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Right, position);

        return events;
    }

    private static PixelBuffer CreateCanvas()
    {
        PixelBuffer canvas = new(640, 480);
        canvas.Fill(0);

        return canvas;
    }

    /// <summary>What a test body works on.</summary>
    /// <param name="Table">The spells the sub-window shows.</param>
    /// <param name="View">What it paints.</param>
    /// <param name="Input">What drives it.</param>
    /// <param name="Metadata">Where OKAY writes.</param>
    private sealed record TestSubject(MageGuildSpellTable Table, MageGuildSpellsView View, MageGuildSpellsInput Input,
        CastleMetadata Metadata);

    /// <summary>A glyph source with fixed metrics whose glyphs are solid blocks.</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(int lineHeight)
        {
            PixelBuffer image = new(6, lineHeight);
            image.Fill(AttributeColor);
            _glyph = new Sprite(image, 0, 0);
            LineHeight = lineHeight;
        }

        public int LineHeight { get; }

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => character == GlyphText.SpaceCharacter ? SpaceWidth : 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
