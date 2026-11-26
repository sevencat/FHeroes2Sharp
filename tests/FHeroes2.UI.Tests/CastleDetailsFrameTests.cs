using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="CastleDetailsFrame"/>: the full screen window of the editor's castle details
/// dialog, the two-piece background, the name bar and the status bar with OKAY and EXIT.
/// </summary>
/// <remarks>
/// 画面全部由存档里的素材拼出来，所以绘制用例逐像素与源图对拍；机器没有游戏数据时用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class CastleDetailsFrameTests
{
    [Fact]
    public void CreateWindow_CoversTheScreenWithTheStandardBorder()
    {
        StandardWindow window = CastleDetailsFrame.CreateWindow();

        // 640x480 的屏幕减去两侧各 16 像素的边框。
        Assert.Equal(new Rect(16, 16, 608, 448), window.ActiveArea);
        Assert.False(window.HasBackground);
    }

    [Fact]
    public void NameAreaAndStatusBar_FollowTheReferenceOffsets() => Assert.True(TryWithArchives((_, frame) =>
    {
        // 名字条从右侧面板的左沿起，高比 CASLBAR 少 2 像素，并向下让出 1 像素。
        Rect nameArea = frame.NameArea;
        Assert.Equal(frame.DialogArea.X + CastleDetailsFrame.RightPanelOffsetX, nameArea.X);
        Assert.Equal(frame.DialogArea.Y + 1, nameArea.Y);
        Assert.Equal(frame.DialogArea.Width - CastleDetailsFrame.RightPanelOffsetX, nameArea.Width);
        Assert.Equal(frame.StatusBarArea.Height - 2, nameArea.Height);

        // 状态栏贴着活动区的底边，整幅宽度。
        Rect statusBar = frame.StatusBarArea;
        Assert.Equal(frame.DialogArea.X, statusBar.X);
        Assert.Equal(frame.DialogArea.Bottom - statusBar.Height, statusBar.Y);
        Assert.Equal(frame.DialogArea.Width, statusBar.Width);
    }));

    [Fact]
    public void PaintBackdrop_StitchesTheTwoPanelsAtTheReferenceColumn() =>
        Assert.True(TryWithArchives((assets, frame) =>
        {
            PixelBuffer output = CreateCanvas();
            Sprite construction = ReadSprite(assets, IcnType.Caslwind, 0);
            Sprite stone = ReadSprite(assets, IcnType.Stonebak, 0);
            Rect dialog = frame.DialogArea;

            frame.PaintBackdrop(output);

            // 438 列以左是 CASLWIND 的面板，438 列（含）以右是 STONEBAK；抽几行逐像素对拍。
            // 避开三处会被后来的东西盖住的行：名字条那一条（右侧头 19 行）、两条 4 像素分隔线（226 与 306）。
            // 左半是带透明的 Sprite 拷贝，源图透明（变换值 1）的像素不会盖上去，跳过。
            foreach (int y in new[] { 25, 40, 120, 224, 300, 400, 447 })
            {
                foreach (int x in new[] { 0, 1, 100, 437 })
                {
                    if (IsTransparent(construction, x, y + 1))
                    {
                        continue;
                    }

                    Assert.Equal(construction.Image[x, y + 1], output[dialog.X + x, dialog.Y + y + 1]);
                }

                foreach (int x in new[] { 438, 439, 500, 607 })
                {
                    Assert.Equal(stone.Image[x, y + 1], output[dialog.X + x, dialog.Y + y + 1]);
                }
            }
        }));

    [Fact]
    public void PaintBackdrop_DrawsTheTwoSeparatorsFourPixelsTall() =>
        Assert.True(TryWithArchives((assets, frame) =>
        {
            PixelBuffer output = CreateCanvas();
            Sprite construction = ReadSprite(assets, IcnType.Caslwind, 0);
            Rect dialog = frame.DialogArea;

            frame.PaintBackdrop(output);

            foreach (int offsetY in new[]
                     {
                         CastleDetailsFrame.TopSeparatorOffsetY, CastleDetailsFrame.BottomSeparatorOffsetY
                     })
            {
                for (int row = 0; row < CastleDetailsFrame.SeparatorHeight; ++row)
                {
                    int sourceY = CastleDetailsFrame.SeparatorSourceY + row;
                    Assert.Equal(construction.Image[500, sourceY],
                        output[dialog.X + 500, dialog.Y + offsetY + row]);
                }
            }
        }));

    [Fact]
    public void PaintBackdrop_CopiesTheNameBarFromColumnSeventeen() =>
        Assert.True(TryWithArchives((assets, frame) =>
        {
            PixelBuffer output = CreateCanvas();
            Sprite statusBar = ReadSprite(assets, IcnType.Caslbar, 0);
            Rect nameArea = frame.NameArea;

            frame.PaintBackdrop(output);

            foreach (int x in new[] { 0, 1, 84, nameArea.Width - 1 })
            {
                Assert.Equal(statusBar.Image[CastleDetailsFrame.NameBarSourceX + x, 4],
                    output[nameArea.X + x, frame.DialogArea.Y + 4]);
            }
        }));

    [Fact]
    public void PaintBottomButtons_PutsOkayAndExitInTheStatusBar() => Assert.True(TryWithArchives((_, frame) =>
    {
        PixelBuffer output = CreateCanvas();

        frame.PaintBackdrop(output);
        frame.PaintBottomButtons(output);

        Rect statusBar = frame.StatusBarArea;

        Assert.NotNull(frame.Okay);
        Assert.NotNull(frame.Exit);
        Assert.Equal(statusBar.X, frame.Okay.Area.X);
        Assert.Equal(statusBar.Y, frame.Okay.Area.Y);
        Assert.Equal(statusBar.Right, frame.Exit.Area.Right);
        Assert.Equal(statusBar.Y, frame.Exit.Area.Y);

        // 两个复位按钮：名字的贴右上、军队的贴右下，都离右边 56 像素。右下那个离的是活动区的底边，
        // 不是状态栏的上沿——状态栏自己还有高度。
        Assert.NotNull(frame.ResetName);
        Assert.NotNull(frame.ResetArmy);
        Assert.Equal(frame.DialogArea.Right - 56, frame.ResetName.Area.Right);
        Assert.Equal(frame.DialogArea.Y + 24, frame.ResetName.Area.Y);
        Assert.Equal(frame.DialogArea.Right - 56, frame.ResetArmy.Area.Right);
        Assert.Equal(frame.DialogArea.Bottom - 24, frame.ResetArmy.Area.Bottom);
    }));

    [Fact]
    public void PaintSpellsButton_CentersThePlateAboveTheNameArea() => Assert.True(TryWithArchives((_, frame) =>
    {
        PixelBuffer output = CreateCanvas();

        frame.PaintBackdrop(output);
        frame.PaintSpellsButton(output);

        // 文字自适应按钮：板子比标签宽，水平居中后再右移 219，纵向从活动区上沿下移 78。
        Assert.NotNull(frame.Spells);
        Assert.True(frame.Spells.Area.Width > frame.Spells.Area.Height);
        Assert.Equal(frame.DialogArea.X + ((frame.DialogArea.Width - frame.Spells.Area.Width) / 2) + 219,
            frame.Spells.Area.X);
        Assert.Equal(frame.DialogArea.Y + 78, frame.Spells.Area.Y);

        // 再画一次不会换一块板子，也不会挪位置。
        Rect before = frame.Spells.Area;
        frame.PaintSpellsButton(output);
        Assert.Equal(before, frame.Spells.Area);
    }));

    [Fact]
    public void PaintRestrictButton_CentersThePlateVertically() => Assert.True(TryWithArchives((_, frame) =>
    {
        PixelBuffer output = CreateCanvas();

        frame.PaintBackdrop(output);
        frame.PaintRestrictButton(output);

        Assert.NotNull(frame.Restrict);
        Assert.Equal(frame.DialogArea.X + ((frame.DialogArea.Width - frame.Restrict.Area.Width) / 2) + 219,
            frame.Restrict.Area.X);
        Assert.Equal(frame.DialogArea.Y + ((frame.DialogArea.Height - frame.Restrict.Area.Height) / 2) - 32,
            frame.Restrict.Area.Y);
    }));

    /// <summary>Creates the frame against the installed game data.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArchives(Action<AssetBundle, CastleDetailsFrame> body)
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            // 机器上有数据却拼不出窗口，那是真问题：不能让用例静默通过。
            CastleDetailsFrame frame = CastleDetailsFrame.TryCreate(assets, isEvilInterface: false)
                ?? throw new InvalidOperationException("The artwork of the castle details window is missing.");

            body(assets, frame);
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
    }

    /// <summary>Whether the sprite leaves the destination pixel untouched at this position.</summary>
    /// <param name="sprite">The sprite.</param>
    /// <param name="x">Column inside the sprite.</param>
    /// <param name="y">Row inside the sprite.</param>
    private static bool IsTransparent(Sprite sprite, int x, int y) =>
        sprite.Transform is { } transform && transform[(y * sprite.Width) + x] == 1;

    private static Sprite ReadSprite(AssetBundle assets, IcnType type, int index) =>
        assets.TryReadIcn(IcnCatalog.GetFileName(type)) is { } file && file.Count > index
            ? file.Sprites[index]
            : throw new InvalidOperationException($"The archives have no {type} frame {index}.");

    private static PixelBuffer CreateCanvas()
    {
        PixelBuffer canvas = new(640, 480);
        canvas.Fill(3);

        return canvas;
    }
}
