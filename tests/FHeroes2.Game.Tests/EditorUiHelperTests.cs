using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the first function level 1:1 port (ADR-057): <c>editor_ui_helper.cpp</c>'s
/// <c>Checkbox</c> and <c>createColorCheckboxes</c>.
/// </summary>
/// <remarks>
/// The archive facade is process wide, so these tests own it while they run: the collection keeps
/// them from running next to anything else that would reassign <see cref="Assets.Current"/>.
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorUiHelperTests
{
    [Fact]
    public void EachColourIsPaintedFromItsOwnCellwinFrame()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        IcnSpriteFile? cellwin = assets.TryReadIcn("CELLWIN.ICN");
        if (cellwin is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            // 参考 editor_ui_helper.cpp:40：NONE 用帧 1，其余用 Color::GetIndex( color ) + 43。
            AssertFramePainted(cellwin, 1, PlayerColor.None);

            for (int index = 0; index < PlayerColors.Ordered.Count; ++index)
            {
                AssertFramePainted(cellwin, 43 + index, PlayerColors.Ordered[index]);
            }
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void CheckboxPaintsTheColourBoxAndPlacesTheCheckmarkTwoPixelsIn()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        IcnSpriteFile? cellwin = assets.TryReadIcn("CELLWIN.ICN");
        if (cellwin is null)
        {
            return;
        }

        int frame = PlayerColors.IndexOf(PlayerColor.Blue) + 43;
        if (cellwin.Count <= frame)
        {
            return;
        }

        Sprite icon = cellwin.Sprites[frame];
        PixelBuffer target = new(64, 48);
        target.Fill(0);

        Assets.Current = assets;
        try
        {
            Checkbox box = new(5, 7, PlayerColor.Blue, true, target);

            Assert.Equal(new Rect(5, 7, icon.Width, icon.Height), box.Area);
            Assert.Equal(PlayerColor.Blue, box.Color);
            Assert.False(box.Checkmark.IsHidden, "a checked box must start checked");
            Assert.Equal(7, box.Checkmark.X);
            Assert.Equal(9, box.Checkmark.Y);

            // The box is opaque art: the whole rectangle of the frame must land at the box position.
            for (int row = 0; row < icon.Height; ++row)
            {
                for (int column = 0; column < icon.Width; ++column)
                {
                    Assert.Equal(icon.Image[column, row], target[5 + column, 7 + row]);
                }
            }

            // Nothing is painted outside the box.
            Assert.Equal(0, target[4, 7]);
            Assert.Equal(0, target[5 + icon.Width, 7]);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void ToggleFlipsTheCheckmarkAndReportsTheNewState()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            PixelBuffer target = new(64, 48);
            Checkbox box = new(0, 0, PlayerColor.None, false, target);

            Assert.True(box.Checkmark.IsHidden, "an unchecked box must start unchecked");

            Assert.True(box.Toggle(), "toggle of an unchecked box checks it");
            Assert.False(box.Checkmark.IsHidden);

            Assert.False(box.Toggle(), "toggle of a checked box unchecks it");
            Assert.True(box.Checkmark.IsHidden);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void CreateColorCheckboxesLaysThemOutThirtyTwoPixelsApart()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            PlayerColor available = PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red;
            PixelBuffer target = new(160, 48);

            List<Checkbox> boxes = [];
            EditorUiHelper.CreateColorCheckboxes(boxes, available, PlayerColor.Green, 3, 9, target);

            Assert.Equal(PlayerColors.ToList(available).Count, boxes.Count);
            Assert.Equal(3, boxes[0].Area.X);
            Assert.Equal(9, boxes[0].Area.Y);

            for (int index = 1; index < boxes.Count; ++index)
            {
                Assert.Equal(boxes[index - 1].Area.X + 32, boxes[index].Area.X);
            }

            foreach (Checkbox box in boxes)
            {
                Assert.Equal(box.Color == PlayerColor.Green, !box.Checkmark.IsHidden);
            }
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void MissingArchivesGiveNoImage()
    {
        Assets.Current = null;

        Assert.Null(Assets.GetImage("CELLWIN.ICN", 2));

        // A negative frame is rejected before the archive is consulted.
        Assert.Null(Assets.GetImage("CELLWIN.ICN", -1));
    }

    [Fact]
    public void ReplacingTheBundleDropsTheCache()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite? first = Assets.GetImage("CELLWIN.ICN", 2);
            Assert.NotNull(first);
            Assert.Same(first, Assets.GetImage("CELLWIN.ICN", 2));

            Assets.Current = null;
            Assert.Null(Assets.GetImage("CELLWIN.ICN", 2));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    private static void AssertFramePainted(IcnSpriteFile cellwin, int frameId, PlayerColor color)
    {
        if (frameId >= cellwin.Count)
        {
            return;
        }

        Sprite icon = cellwin.Sprites[frameId];
        PixelBuffer target = new(80, 48);
        target.Fill(0);

        Checkbox box = new(3, 5, color, false, target);

        Assert.Equal(new Rect(3, 5, icon.Width, icon.Height), box.Area);

        for (int row = 0; row < icon.Height; ++row)
        {
            for (int column = 0; column < icon.Width; ++column)
            {
                Assert.Equal(icon.Image[column, row], target[3 + column, 5 + row]);
            }
        }
    }

    [Fact]
    public void RenderResourcesLaysOutSevenIconsInTwoRows()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        IcnSpriteFile? resources = assets.TryReadIcn("RESOURCE.ICN");
        if (resources is null || resources.Count < 7)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Funds funds = new(10, 20, 30, 40, 50, 60, 70);
            Rect roi = new(10, 20, 200, 120);
            PixelBuffer target = new(240, 180);
            target.Fill(0);
            Rect[] areas = new Rect[7];

            EditorUiHelper.RenderResources(funds, roi, target, areas);

            // 参考 editor_ui_helper.cpp:101–118：边缘 7、格 39×32、列间距 ( 200 - 14 - 156 ) / 3 = 10。
            const int MaxWidth = 39;
            const int MaxHeight = 32;
            const int FontHeight = 13;
            const int FirstColumn = 17;
            const int ColumnStep = 49;
            int offsetY0 = 20 + 7 + MaxHeight;
            int offsetY1 = 20 + 7 + (2 * MaxHeight) + FontHeight + 2;

            Assert.Equal(FirstColumn + ((MaxWidth - resources.Sprites[0].Width) / 2), areas[0].X);
            Assert.Equal(offsetY0 - resources.Sprites[0].Height, areas[0].Y);
            Assert.Equal(FirstColumn + ColumnStep + ((MaxWidth - resources.Sprites[3].Width) / 2), areas[1].X);
            Assert.Equal(FirstColumn + (3 * ColumnStep) + ((MaxWidth - resources.Sprites[1].Width) / 2), areas[3].X);
            Assert.Equal(FirstColumn + ((MaxWidth - resources.Sprites[2].Width) / 2), areas[4].X);
            Assert.Equal(offsetY1 - resources.Sprites[2].Height, areas[4].Y);

            // Gold spans two cells on the second row.
            Assert.Equal(FirstColumn + (2 * ColumnStep) + (((MaxWidth * 2) + 10 - resources.Sprites[6].Width) / 2), areas[6].X);
            Assert.Equal(offsetY1 - resources.Sprites[6].Height, areas[6].Y);

            // The icons are blitted pixel for pixel.
            for (int row = 0; row < resources.Sprites[0].Height; ++row)
            {
                for (int column = 0; column < resources.Sprites[0].Width; ++column)
                {
                    Assert.Equal(resources.Sprites[0].Image[column, row], target[areas[0].X + column, areas[0].Y + row]);
                }
            }

            // And the amounts are painted below them.
            bool painted = false;
            for (int y = offsetY0; y < offsetY1 && !painted; ++y)
            {
                for (int x = FirstColumn; x < FirstColumn + MaxWidth; ++x)
                {
                    if (target[x, y] != 0)
                    {
                        painted = true;
                        break;
                    }
                }
            }

            Assert.True(painted, "the amounts must be painted under the icons");
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void RenderResourcesNeedsSevenAreas()
    {
        PixelBuffer target = new(40, 40);

        Assert.Throws<ArgumentException>(() => EditorUiHelper.RenderResources(default, new Rect(0, 0, 100, 100), target, new Rect[6]));
    }
}

/// <summary>
/// Serializes the tests that swap <see cref="Assets.Current"/>; static facades are process wide
/// state, see ADR-057.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalArchiveCollection
{
    /// <summary>Name of the collection.</summary>
    public const string Name = "Global archive facade";
}
