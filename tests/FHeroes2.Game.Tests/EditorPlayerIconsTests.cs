using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The six <c>NGEXTRA</c> frames the editor needs, built at load time
/// (<c>game_assets.cpp:3965-3989</c>) and served through <see cref="Assets"/>.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorPlayerIconsTests
{
    [Fact]
    public void TheTableGainsSixPlayerIcons()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            // 归档里只有 82 帧（0-81），补完是 88。
            Assert.Equal(EditorPlayerIcons.ArchiveFrameCount,
                assets.TryReadIcn(EditorPlayerIcons.FileName)?.Count ?? 0);
            Assert.Equal(EditorPlayerIcons.ArchiveFrameCount + EditorPlayerIcons.FrameCount,
                Assets.GetImageCount(EditorPlayerIcons.FileName));

            for (int i = 0; i < EditorPlayerIcons.FrameCount; ++i)
            {
                Sprite? icon = Assets.GetImage(EditorPlayerIcons.FileName, EditorPlayerIcons.FirstFrame + i);

                Assert.NotNull(icon);
                Assert.Equal(EditorPlayerIcons.IconWidth, icon!.Width);
                Assert.Equal(EditorPlayerIcons.IconHeight, icon.Height);

                // 参考拼图前 _disableTransformLayer()：图标是不透明的单层图，角上的底图不会被抠穿。
                Assert.False(icon.HasTransformLayer);
            }

            Assert.Null(Assets.GetImage(EditorPlayerIcons.FileName,
                EditorPlayerIcons.FirstFrame + EditorPlayerIcons.FrameCount));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void EveryColourGetsItsOwnIcon()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite[] icons = new Sprite[EditorPlayerIcons.FrameCount];

            for (int i = 0; i < icons.Length; ++i)
            {
                icons[i] = Assets.GetImage(EditorPlayerIcons.FileName, EditorPlayerIcons.FirstFrame + i)!;
            }

            // 六种颜色拼出来必须两两不同，否则就是把同一张图发了六遍。
            for (int i = 0; i < icons.Length; ++i)
            {
                for (int j = i + 1; j < icons.Length; ++j)
                {
                    Assert.NotEqual(icons[i].Image.Pixels, icons[j].Image.Pixels);
                }
            }

            // 拼过的图既不是「只给电脑」那一帧，也不是「只有人类」那一帧的照搬。
            for (int i = 0; i < icons.Length; ++i)
            {
                Sprite computer = Assets.GetImage(EditorPlayerIcons.FileName, i + 3)!;
                Sprite human = Assets.GetImage(EditorPlayerIcons.FileName, i + 9)!;

                Assert.NotEqual(computer.Image.Pixels, icons[i].Image.Pixels);
                Assert.NotEqual(human.Image.Pixels, icons[i].Image.Pixels);
            }
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TheArchivedFramesAreLeftAlone()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            IcnSpriteFile archive = assets.TryReadIcn(EditorPlayerIcons.FileName)!;

            // 补帧只往后加，归档原有的 0-81 一帧都不动——除了 NgExtraWideIcons 那两组「原位改图」
            // （27-33、39-44），它们本来就该跟归档不一样，另见 NgExtraWideIconsTests。
            for (int frameId = 0; frameId < EditorPlayerIcons.ArchiveFrameCount; ++frameId)
            {
                if (NgExtraWideIcons.IsPatchedFrame(frameId))
                {
                    continue;
                }

                Sprite? served = Assets.GetImage(EditorPlayerIcons.FileName, frameId);

                Assert.NotNull(served);
                Assert.Equal(archive.Sprites[frameId].Image.Pixels, served!.Image.Pixels);
            }
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void ATableOfAnotherSizeIsNotPatched()
    {
        // 参考那句 if ( images.size() != 82 ) break; —— 资材版本不对就原样不动。
        Assert.Null(EditorPlayerIcons.Build(CreateFrames(EditorPlayerIcons.ArchiveFrameCount - 1), EditorPlayerIcons.FirstFrame));
        Assert.Null(EditorPlayerIcons.Build(CreateFrames(EditorPlayerIcons.ArchiveFrameCount + 1), EditorPlayerIcons.FirstFrame));

        Assert.Null(EditorPlayerIcons.Build(CreateFrames(EditorPlayerIcons.ArchiveFrameCount), EditorPlayerIcons.FirstFrame - 1));
        Assert.Null(EditorPlayerIcons.Build(CreateFrames(EditorPlayerIcons.ArchiveFrameCount),
            EditorPlayerIcons.FirstFrame + EditorPlayerIcons.FrameCount));
    }

    [Fact]
    public void AColourWhoseComputerFrameHasAnotherSizeIsSkipped()
    {
        // 参考在尺寸不对时 continue，那一帧留在 resize 出来的空图上；端口回 null。
        Assert.Null(EditorPlayerIcons.Build(CreateFrames(EditorPlayerIcons.ArchiveFrameCount, width: 10), EditorPlayerIcons.FirstFrame));
        Assert.NotNull(EditorPlayerIcons.Build(CreateFrames(EditorPlayerIcons.ArchiveFrameCount), EditorPlayerIcons.FirstFrame));
    }

    [Fact]
    public void OnlyTheEditorTableGrows()
    {
        Assert.Equal(EditorPlayerIcons.ArchiveFrameCount + EditorPlayerIcons.FrameCount,
            EditorPlayerIcons.GetFrameCount(EditorPlayerIcons.FileName, EditorPlayerIcons.ArchiveFrameCount));

        // 别的表、别的帧数都不动。
        Assert.Equal(12, EditorPlayerIcons.GetFrameCount("MISC12.ICN", 12));
        Assert.Equal(81, EditorPlayerIcons.GetFrameCount(EditorPlayerIcons.FileName, 81));
        Assert.True(EditorPlayerIcons.IsFileName("ngextra.icn"));
        Assert.False(EditorPlayerIcons.IsFileName("MISC12.ICN"));
    }

    private static IReadOnlyList<Sprite> CreateFrames(int count, int width = EditorPlayerIcons.IconWidth)
    {
        List<Sprite> frames = new(count);

        for (int i = 0; i < count; ++i)
        {
            frames.Add(new Sprite(width, EditorPlayerIcons.IconHeight));
        }

        return frames;
    }
}
