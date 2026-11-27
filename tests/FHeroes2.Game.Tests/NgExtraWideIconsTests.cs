using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The stray column at the right edge of the two groups of wide <c>NGEXTRA</c> icons, removed the way
/// <c>game_assets.cpp:3948-3959</c> does it: column 58 copied over column 59 for rows 44 through 54.
/// </summary>
/// <remarks>
/// 参考是「原位」改图，之后所有人拿到的都是改过的帧；端口不改共享的归档帧表，改在取帧处，所以这里既验
/// <see cref="NgExtraWideIcons.Patch"/> 本身，也验 <see cref="Assets.GetImage"/> 交出去的那一份。
/// </remarks>
public sealed class NgExtraWideIconsTests
{
    [Fact]
    public void TheStrayColumnIsCopiedOverTheNextOne()
    {
        IReadOnlyList<Sprite> frames = CreateFrames();
        Sprite original = frames[NgExtraWideIcons.FirstComputerFrame];

        Sprite? patched = NgExtraWideIcons.Patch(frames, NgExtraWideIcons.FirstComputerFrame);

        Assert.NotNull(patched);
        Assert.Equal(original.Width, patched!.Width);
        Assert.Equal(original.Height, patched.Height);

        // 抄完之后第 58 列与第 59 列在 44-54 行上一模一样，下面那一行（55）不动。
        for (int y = NgExtraWideIcons.FirstRow; y < NgExtraWideIcons.FirstRow + NgExtraWideIcons.RowCount; ++y)
        {
            Assert.Equal(original.Image[NgExtraWideIcons.SourceColumn, y], patched.Image[NgExtraWideIcons.TargetColumn, y]);
        }

        Assert.NotEqual(original.Image[NgExtraWideIcons.SourceColumn, NgExtraWideIcons.FirstRow + NgExtraWideIcons.RowCount],
            patched.Image[NgExtraWideIcons.TargetColumn, NgExtraWideIcons.FirstRow + NgExtraWideIcons.RowCount]);

        // 除那一列以外每个像素都不许变——改的是「右边多一列」，不是重画。
        for (int y = 0; y < original.Height; ++y)
        {
            for (int x = 0; x < original.Width; ++x)
            {
                bool insideStrip = x == NgExtraWideIcons.TargetColumn
                                   && y >= NgExtraWideIcons.FirstRow
                                   && y < NgExtraWideIcons.FirstRow + NgExtraWideIcons.RowCount;

                if (!insideStrip)
                {
                    Assert.Equal(original.Image[x, y], patched.Image[x, y]);
                }
            }
        }

        // 锚点（ICN 头里的偏移）跟着原帧，参考的 Copy 也不改它。
        Assert.Equal(original.OffsetX, patched.OffsetX);
        Assert.Equal(original.OffsetY, patched.OffsetY);
    }

    [Fact]
    public void TheArchivedFrameIsNotWrittenTo()
    {
        IReadOnlyList<Sprite> frames = CreateFrames();
        byte[] before = (byte[])frames[NgExtraWideIcons.FirstComputerFrame].Image.Pixels.Clone();

        _ = NgExtraWideIcons.Patch(frames, NgExtraWideIcons.FirstComputerFrame);

        // 取帧处回的是新图：归档缓存被写脏的话，别的画法会跟着错。
        Assert.Equal(before, frames[NgExtraWideIcons.FirstComputerFrame].Image.Pixels);
    }

    [Fact]
    public void BothWideGroupsArePatched()
    {
        IReadOnlyList<Sprite> frames = CreateFrames();

        for (int frameId = NgExtraWideIcons.FirstComputerFrame;
             frameId < NgExtraWideIcons.FirstComputerFrame + NgExtraWideIcons.ComputerFrameCount;
             ++frameId)
        {
            Assert.NotNull(NgExtraWideIcons.Patch(frames, frameId));
        }

        for (int frameId = NgExtraWideIcons.FirstClassFrame;
             frameId < NgExtraWideIcons.FirstClassFrame + NgExtraWideIcons.ClassFrameCount;
             ++frameId)
        {
            Assert.NotNull(NgExtraWideIcons.Patch(frames, frameId));
        }

        // 两组两侧的邻帧都不在名单里。
        foreach (int frameId in new[] { 0, 26, 34, 38, 45, 81 })
        {
            Assert.False(NgExtraWideIcons.IsPatchedFrame(frameId));
            Assert.Null(NgExtraWideIcons.Patch(frames, frameId));
        }
    }

    [Fact]
    public void ANarrowFrameIsServedAsItIs()
    {
        // 两组里夹着 62×45 的窄图：参考的 if 只看尺寸，尺寸不对就跳过。
        IReadOnlyList<Sprite> frames = CreateFrames(
            width: NgExtraWideIcons.IconWidth,
            height: NgExtraWideIcons.IconHeight - 13);

        Assert.Null(NgExtraWideIcons.Patch(frames, NgExtraWideIcons.FirstComputerFrame));
    }

    [Fact]
    public void ATableOfAnotherSizeIsNotPatched()
    {
        // 参考那句 if ( images.size() != 82 ) break; —— 资材版本不对就一个字都不改。
        Assert.Null(NgExtraWideIcons.Patch(CreateFrames(count: NgExtraWideIcons.ArchiveFrameCount - 1),
            NgExtraWideIcons.FirstComputerFrame));
        Assert.Null(NgExtraWideIcons.Patch(CreateFrames(count: NgExtraWideIcons.ArchiveFrameCount + 1),
            NgExtraWideIcons.FirstComputerFrame));
        Assert.Null(NgExtraWideIcons.Patch(CreateFrames(), -1));
        Assert.Throws<ArgumentNullException>(() => NgExtraWideIcons.Patch(null!, NgExtraWideIcons.FirstComputerFrame));
    }

    [Fact]
    public void OnlyTheGameTableIsPatched()
    {
        Assert.True(NgExtraWideIcons.IsFileName("NGEXTRA.ICN"));
        Assert.True(NgExtraWideIcons.IsFileName("ngextra.icn"));
        Assert.False(NgExtraWideIcons.IsFileName("MISC12.ICN"));

        // 与补帧的那半个 case 认的是同一张表、同一个帧数。
        Assert.Equal(EditorPlayerIcons.FileName, NgExtraWideIcons.FileName);
        Assert.Equal(EditorPlayerIcons.ArchiveFrameCount, NgExtraWideIcons.ArchiveFrameCount);
    }

    [Fact]
    public void TheServedFramesCarryTheFixUp()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            IcnSpriteFile archive = assets.TryReadIcn(NgExtraWideIcons.FileName)!;

            // 归档必须在取帧之前留一份：取帧若把缓存写脏，这一份就能看出来。
            byte[][] archived = new byte[archive.Count][];
            for (int frameId = 0; frameId < archive.Count; ++frameId)
            {
                archived[frameId] = (byte[])archive.Sprites[frameId].Image.Pixels.Clone();
            }

            // 这套资材确实带着那条杂边，否则下面的比对就是空转。
            int changedFrames = 0;

            for (int frameId = 0; frameId < NgExtraWideIcons.ArchiveFrameCount; ++frameId)
            {
                Sprite served = Assets.GetImage(NgExtraWideIcons.FileName, frameId)!;
                Sprite original = archive.Sprites[frameId];

                Assert.Equal(original.Width, served.Width);
                Assert.Equal(original.Height, served.Height);
                Assert.Equal(original.OffsetX, served.OffsetX);
                Assert.Equal(original.OffsetY, served.OffsetY);

                bool wide = NgExtraWideIcons.IsPatchedFrame(frameId)
                            && original.Width == NgExtraWideIcons.IconWidth
                            && original.Height == NgExtraWideIcons.IconHeight;

                int changedPixels = 0;

                for (int y = 0; y < original.Height; ++y)
                {
                    for (int x = 0; x < original.Width; ++x)
                    {
                        bool patchedPixel = wide
                                            && x == NgExtraWideIcons.TargetColumn
                                            && y >= NgExtraWideIcons.FirstRow
                                            && y < NgExtraWideIcons.FirstRow + NgExtraWideIcons.RowCount;

                        if (patchedPixel)
                        {
                            // 交付的那一帧：44-54 行的第 59 列已经等于第 58 列。
                            Assert.Equal(original.Image[NgExtraWideIcons.SourceColumn, y], served.Image[x, y]);

                            if (original.Image[x, y] != served.Image[x, y])
                            {
                                ++changedPixels;
                            }
                        }
                        else
                        {
                            Assert.Equal(original.Image[x, y], served.Image[x, y]);
                        }
                    }
                }

                if (changedPixels > 0)
                {
                    ++changedFrames;
                }
            }

            Assert.True(changedFrames > 0,
                "None of the wide NGEXTRA frames changed: this game data has no stray column to remove.");

            // 丢出那一份新图之后，归档的 82 帧一个字都没变。
            for (int frameId = 0; frameId < archive.Count; ++frameId)
            {
                Assert.Equal(archived[frameId], archive.Sprites[frameId].Image.Pixels);
            }
        }
        finally
        {
            Assets.Current = null;
        }
    }

    private static IReadOnlyList<Sprite> CreateFrames(
        int count = NgExtraWideIcons.ArchiveFrameCount,
        int width = NgExtraWideIcons.IconWidth,
        int height = NgExtraWideIcons.IconHeight)
    {
        List<Sprite> frames = new(count);

        for (int i = 0; i < count; ++i)
        {
            PixelBuffer pixels = new(width, height);

            // 每个像素带自己的号，抄错列或抄错行都看得出来。
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    pixels[x, y] = (byte)((x * 3) + y + i);
                }
            }

            frames.Add(new Sprite(pixels, 5, 7));
        }

        return frames;
    }
}
