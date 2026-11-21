using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the construction window, <c>Castle::_openConstructionDialog()</c> (<c>castle_town.cpp:171-800</c>):
/// where it puts its nineteen cells, what its mouse asks for and what its status line says.
/// </summary>
/// <remarks>
/// 需要 AGG 的几条在缺数据时直接返回；格子坐标、法师塔槽位与船长旗子偏移这几条不依赖资产，任何环境都会跑。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class ConstructionTests
{
    private const byte GlyphColour = 200;

    [Fact]
    public void TheNineteenCellsAreTheReferenceOnes()
    {
        IReadOnlyList<ConstructionSlot> slots = ConstructionLayout.GetSlots(new Castle(0, MapRace.Knight));

        Assert.Equal(ConstructionLayout.SlotCount, slots.Count);

        // 参考先画六种住所（两行三列），再画法师塔、酒馆、盗贼公会，最后是船长格。
        Assert.Equal(new ConstructionSlot(BuildingType.DwellingMonster1, new Point(5, 2)), slots[0]);
        Assert.Equal(new ConstructionSlot(BuildingType.DwellingMonster3, new Point(293, 2)), slots[2]);
        Assert.Equal(new ConstructionSlot(BuildingType.DwellingMonster4, new Point(5, 77)), slots[3]);
        Assert.Equal(new ConstructionSlot(BuildingType.DwellingMonster6, new Point(293, 77)), slots[5]);
        Assert.Equal(new ConstructionSlot(BuildingType.MageGuild1, new Point(5, 157)), slots[6]);
        Assert.Equal(new ConstructionSlot(BuildingType.Captain, new Point(444, 165)), slots[18]);
    }

    [Theory]
    [InlineData(0, BuildingType.MageGuild1)]
    [InlineData(1, BuildingType.MageGuild2)]
    [InlineData(2, BuildingType.MageGuild3)]
    [InlineData(3, BuildingType.MageGuild4)]
    [InlineData(4, BuildingType.MageGuild5)]
    [InlineData(5, BuildingType.MageGuild5)]
    public void TheMageGuildCellOffersTheNextLevel(int level, BuildingType expected) =>
        Assert.Equal(expected, ConstructionLayout.GetMageGuildSlot(level));

    [Theory]
    [InlineData(MapRace.Knight, 43, 9)]
    [InlineData(MapRace.Barbarian, 42, 8)]
    [InlineData(MapRace.Sorceress, 43, 9)]
    [InlineData(MapRace.Warlock, 41, 9)]
    [InlineData(MapRace.Wizard, 42, 10)]
    [InlineData(MapRace.Necromancer, 42, 9)]
    public void TheFlagSitsWhereEachRacePutsIt(MapRace race, int x, int y) =>
        Assert.Equal(new Point(x, y), ConstructionLayout.GetCaptainFlagOffset(race));

    [Fact]
    public void TryCreateWithoutArchivesReturnsNull()
    {
        GameAssets.Current = null;

        Assert.Null(ConstructionArtwork.TryCreate(MapRace.Knight, PlayerColor.Blue));
    }

    [Fact]
    public void TheCellsSitOnTheirSlots()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());

        Assert.Equal(ConstructionLayout.SlotCount, view.Cells.Count);

        IReadOnlyList<ConstructionSlot> slots = ConstructionLayout.GetSlots(castle);

        for (int i = 0; i < slots.Count; ++i)
        {
            Assert.Equal(slots[i].Building, view.Cells[i].Info.Type);
            Assert.Equal(slots[i].Offset, new Point(view.Cells[i].Area.X, view.Cells[i].Area.Y));
        }
    }

    [Fact]
    public void HitTestFindsTheWindowChrome()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());

        Assert.Equal(
            ConstructionTarget.Exit,
            view.HitTest(Middle(view.Layout.GetExitButtonArea(artwork.ExitButton))).Target);
        Assert.Equal(ConstructionTarget.PreviousTown, view.HitTest(Middle(view.Layout.PreviousCastleButton)).Target);
        Assert.Equal(ConstructionTarget.NextTown, view.HitTest(Middle(view.Layout.NextCastleButton)).Target);
        Assert.Equal(ConstructionTarget.Income, view.HitTest(Middle(view.Layout.ResourceHitArea)).Target);
        Assert.Equal(ConstructionTarget.FirstHero, view.HitTest(Middle(ConstructionLayout.FirstHeroArea)).Target);
        Assert.Equal(ConstructionTarget.SecondHero, view.HitTest(Middle(ConstructionLayout.SecondHeroArea)).Target);
        Assert.True(view.HitTest(cursor: null).IsNone);
    }

    [Fact]
    public void HitTestFindsACellAndItsIndex()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());

        ConstructionHit hit = view.HitTest(Middle(view.Cells[6].Area));

        Assert.Equal(ConstructionTarget.Building, hit.Target);
        Assert.Equal(6, hit.CellIndex);
    }

    [Fact]
    public void TheFormationIconsAreOnlyLiveWithACaptain()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(MapRace.Knight, PlayerColor.Blue)!;

        ConstructionView without = ConstructionView.Create(
            CastleWith(BuildingType.Castle),
            artwork,
            Font(),
            Rich());

        // 没有船长时那一块属于还没建的船长格，点下去开的是船长的价格框。
        ConstructionHit beforeCaptain = without.HitTest(Middle(without.SpreadFormationArea), forClick: true);

        Assert.Equal(ConstructionTarget.Building, beforeCaptain.Target);
        Assert.True(without.Cells[beforeCaptain.CellIndex].IsCaptain);

        ConstructionView with = ConstructionView.Create(
            CastleWith(BuildingType.Castle, BuildingType.Captain),
            artwork,
            Font(),
            Rich());

        // 「散开」图标压在船长格上（船长格 135×70，从 444,165 起），点击要穿过已建好的船长格才落到图标上。
        Assert.Equal(
            ConstructionTarget.SpreadFormation,
            with.HitTest(Middle(with.SpreadFormationArea), forClick: true).Target);
        Assert.Equal(ConstructionTarget.GroupedFormation, with.HitTest(Middle(with.GroupedFormationArea)).Target);
    }

    [Fact]
    public void TheStatusLineFollowsTheReference()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle, BuildingType.Captain);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());

        Assert.Equal(
            ConstructionView.ExitStatusText,
            view.GetStatusMessage(Middle(view.Layout.GetExitButtonArea(artwork.ExitButton)), null, null, false, false, true));

        // 状态栏按参考的顺序先问格子（castle_town.cpp:558）再问阵型图标（:583），而「散开」图标压在船长格上，
        // 所以那一小块说的是船长格自己的状态——参考也有这个角落。
        Assert.Equal(
            view.Cells[^1].Info.GetConditionDescription(),
            view.GetStatusMessage(Middle(view.SpreadFormationArea), null, null, false, false, true));

        Assert.Equal(
            ConstructionView.GroupedFormationStatusText,
            view.GetStatusMessage(Middle(view.GroupedFormationArea), null, null, false, false, true));

        // 只有一座城时两个箭头不说话，`castle_town.cpp:627-629` 的 `CanSwitchCastle`。
        Assert.Equal(
            string.Empty,
            view.GetStatusMessage(Middle(view.Layout.PreviousCastleButton), null, null, false, false, false));
        Assert.Equal(
            "Show previous town",
            view.GetStatusMessage(Middle(view.Layout.PreviousCastleButton), null, null, false, false, true));

        // 状态条本身的空白不说话，交给场景写日期那一行。
        Assert.Equal(
            string.Empty,
            view.GetStatusMessage(Middle(view.Layout.StatusBarArea), null, null, false, false, true));
    }

    [Fact]
    public void ACellSpeaksItsCondition()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());

        ConstructionCell cell = view.Cells.First(entry => entry.Info.Status == BuildingStatus.AllowBuild);

        Assert.Equal(
            cell.Info.GetConditionDescription(),
            view.GetStatusMessage(Middle(cell.Area), null, null, false, false, true));
    }

    [Fact]
    public void OnlyBuildableCellsOpenThePriceBox()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle, BuildingType.DwellingMonster1);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());

        for (int i = 0; i < view.Cells.Count; ++i)
        {
            BuildingStatus status = view.Cells[i].Info.Status;
            bool expected = status is BuildingStatus.AllowBuild or BuildingStatus.LackResources;

            Assert.Equal(expected, view.CanOpenCell(i));
        }

        Assert.False(view.CanOpenCell(-1));
        Assert.False(view.CanOpenCell(view.Cells.Count));
    }

    [Fact]
    public void CellNamesFitIntoOneHundredAndTwentyFivePixels()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        IGlyphSource font = Font();
        ConstructionView view = ConstructionView.Create(castle, artwork, font, Rich());

        foreach (ConstructionCell cell in view.Cells)
        {
            Assert.True(GlyphText.Measure(font, cell.Name) <= ConstructionCell.NameMaxWidth);
        }
    }

    [Fact]
    public void PressingACellAndReleasingOnItAsksForTheBuilding()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());
        ConstructionInput input = new(view);

        ConstructionCell cell = view.Cells.First(entry => entry.Info.Status == BuildingStatus.AllowBuild);
        int index = view.Cells.ToList().IndexOf(cell);
        Point point = Middle(cell.Area);

        input.SetCursor(point);
        input.SetLeftButton(true);
        input.SetLeftButton(false);

        ConstructionHit? action = input.TakeAction();

        Assert.NotNull(action);
        Assert.Equal(ConstructionTarget.Building, action!.Value.Target);
        Assert.Equal(index, action.Value.CellIndex);
        Assert.Null(input.TakeAction());
    }

    [Fact]
    public void PressingOnePlaceAndReleasingOnAnotherDoesNothing()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());
        ConstructionInput input = new(view);

        input.SetCursor(Middle(view.Layout.GetExitButtonArea(artwork.ExitButton)));
        input.SetLeftButton(true);

        input.SetCursor(Middle(ConstructionLayout.FirstHeroArea));
        input.SetLeftButton(false);

        Assert.Null(input.TakeAction());
    }

    [Fact]
    public void ACellThatCannotBeBuiltIsNotArmed()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle, BuildingType.DwellingMonster1);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());
        ConstructionInput input = new(view);

        ConstructionCell built = view.Cells.First(entry => entry.Info.Status == BuildingStatus.AlreadyBuilt);
        Point point = Middle(built.Area);

        input.SetCursor(point);
        input.SetLeftButton(true);
        input.SetLeftButton(false);

        Assert.Null(input.TakeAction());
    }

    [Fact]
    public void TheTownsAreReachableOnlyWithMoreThanOne()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());
        ConstructionInput input = new(view) { CanSwitchCastle = false };

        input.SetCursor(Middle(view.Layout.NextCastleButton));
        input.SetLeftButton(true);
        input.SetLeftButton(false);

        Assert.Null(input.TakeAction());

        input.CanSwitchCastle = true;
        input.SetLeftButton(true);
        input.SetLeftButton(false);

        Assert.Equal(ConstructionTarget.NextTown, input.TakeAction()?.Target);
    }

    [Fact]
    public void DrawStampsTheStonePlateWhileThereIsNoCaptain()
    {
        using Archives? archives = Archives.Open();
        if (archives is null)
        {
            return;
        }

        Castle castle = CastleWith(BuildingType.Castle);
        ConstructionArtwork artwork = ConstructionArtwork.TryCreate(castle.Race, castle.Color)!;
        ConstructionView view = ConstructionView.Create(castle, artwork, Font(), Rich());

        PixelBuffer target = new(ConstructionLayout.Width, ConstructionLayout.Height);

        view.Draw(target, new ConstructionFrame { Funds = Rich() });

        // 参考把整屏的 STONEBAK 按同样的坐标裁一块贴上去（castle_town.cpp:204-208），所以像素应当一致。
        Rect plate = ConstructionLayout.CaptainPlateArea;
        Assert.Equal(artwork.CaptainPlate.Image[plate.X, plate.Y], target[plate.X, plate.Y]);
    }

    private static Point Middle(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static Castle CastleWith(params BuildingType[] buildings)
    {
        Castle castle = new(0, MapRace.Knight);
        Funds treasury = Rich();

        foreach (BuildingType building in buildings)
        {
            castle.ActionNewDay();

            Assert.True(castle.BuyBuilding(building, ref treasury), $"{building} should be buyable");
        }

        // 买过东西的那一天所有格子都是「今天不能建」，窗口要的是第二天的样子。
        castle.ActionNewDay();

        return castle;
    }

    private static Funds Rich() => new()
    {
        Wood = 999_999,
        Mercury = 999_999,
        Ore = 999_999,
        Sulfur = 999_999,
        Crystal = 999_999,
        Gems = 999_999,
        Gold = 999_999,
    };

    private static IGlyphSource Font() => new BlockFont(8, 6, GlyphColour);

    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte colour)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;

            PixelBuffer image = new(advance, lineHeight);
            image.Fill(colour);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight { get; }

        public int SpaceWidth { get; }

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }

    private sealed class Archives : IDisposable
    {
        private readonly AssetBundle _bundle;

        private Archives(AssetBundle bundle) => _bundle = bundle;

        public static Archives? Open()
        {
            if (AssetLocator.Resolve() is not { } directory)
            {
                return null;
            }

            AssetBundle? bundle = AssetBundle.Open(directory);
            if (bundle is null)
            {
                return null;
            }

            GameAssets.Current = bundle;

            return new Archives(bundle);
        }

        public void Dispose() => GameAssets.Current = null;
    }
}
