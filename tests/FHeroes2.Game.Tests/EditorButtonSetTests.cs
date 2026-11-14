using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the button set of the editor panel: the thirty five frames of <c>EDITBTNS.ICN</c> plus the
/// ten the reference assembles at run time (<c>game_assets.cpp</c>, <c>case ICN::EDITBTNS</c>).
/// </summary>
public sealed class EditorButtonSetTests
{
    [Fact]
    public void WithoutArchives_ThereAreNoButtons()
    {
        Assert.Null(EditorButtonSet.TryCreate(null));
    }

    [Fact]
    public void FrameTable_IsTheOneOfThePanel()
    {
        // Nine instruments, six system buttons and four brush sizes, plus the ten generated frames.
        Assert.Equal(45, EditorButtonSet.FrameCount);
        Assert.Equal(35, EditorButtonSet.FirstGeneratedFrame);

        // The three object groups and the two arrows sit after the archive frames, a released and a
        // pressed frame each.
        Assert.Equal(EditorButtonSet.FirstGeneratedFrame, EditorButtonSet.AdventureObjectsFrame);
        Assert.Equal(EditorButtonSet.AdventureObjectsFrame + 2, EditorButtonSet.KingdomObjectsFrame);
        Assert.Equal(EditorButtonSet.KingdomObjectsFrame + 2, EditorButtonSet.MonstersFrame);
        Assert.Equal(EditorButtonSet.MonstersFrame + 2, EditorButtonSet.UndoFrame);
        Assert.Equal(EditorButtonSet.UndoFrame + 2, EditorButtonSet.RedoFrame);
        Assert.Equal(EditorButtonSet.FrameCount, EditorButtonSet.RedoFrame + 2);

        // The panel is three columns of fourty eight by thirty six buttons.
        Assert.Equal(48, EditorButtonSet.ButtonWidth);
        Assert.Equal(36, EditorButtonSet.ButtonHeight);
    }

    [Fact]
    public void RealData_HasAButtonOfThePanelSizeForEveryFrame()
    {
        AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        using (assets)
        {
            EditorButtonSet buttons = EditorButtonSet.TryCreate(assets)!;

            Assert.Equal(EditorButtonSet.FrameCount, buttons.Count);
            Assert.Null(buttons[-1]);
            Assert.Null(buttons[EditorButtonSet.FrameCount]);

            for (int i = 0; i < EditorButtonSet.FrameCount; ++i)
            {
                Sprite frame = buttons[i]!;

                // The four brush size pairs are the small ones, everything else fills a panel cell,
                // except the three spares the archive ends with.
                bool isBrush = i >= EditorButtonSet.BrushFrame
                    && i < EditorButtonSet.BrushFrame + (EditorButtonSet.BrushCount * 2);
                bool isSpare = i >= EditorButtonSet.BrushFrame + (EditorButtonSet.BrushCount * 2)
                    && i < EditorButtonSet.FirstGeneratedFrame;

                if (isBrush)
                {
                    Assert.Equal(EditorButtonSet.BrushWidth, frame.Width);
                    Assert.Equal(EditorButtonSet.BrushHeight, frame.Height);
                }
                else if (!isSpare)
                {
                    Assert.Equal(EditorButtonSet.ButtonWidth, frame.Width);
                    Assert.Equal(EditorButtonSet.ButtonHeight, frame.Height);
                }

                // The archive buttons are opaque rectangles; the assembled ones are drawn on a clean
                // face, so they carry the transform layer of the icon and its shadow.
                if (i >= EditorButtonSet.FirstGeneratedFrame)
                {
                    Assert.True(frame.HasTransformLayer, $"frame {i} has no transform layer");
                }
            }
        }
    }

    [Fact]
    public void RealData_HandsTheArchiveFramesThroughUntouched()
    {
        AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        using (assets)
        {
            IcnSpriteFile archive = assets.TryReadIcn(EditorButtonSet.AssetName)!;
            EditorButtonSet buttons = EditorButtonSet.TryCreate(assets)!;

            for (int i = 0; i < EditorButtonSet.FirstGeneratedFrame; ++i)
            {
                Assert.True(
                    buttons[i]!.Image.AsSpan().SequenceEqual(archive.Sprites[i]!.Image.AsSpan()),
                    $"frame {i} of the archive was changed");
            }
        }
    }

    [Fact]
    public void RealData_DrawsADifferentIconOnEveryGeneratedButton()
    {
        AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        using (assets)
        {
            EditorButtonSet buttons = EditorButtonSet.TryCreate(assets)!;
            int[] released =
            [
                EditorButtonSet.AdventureObjectsFrame,
                EditorButtonSet.KingdomObjectsFrame,
                EditorButtonSet.MonstersFrame,
                EditorButtonSet.UndoFrame,
                EditorButtonSet.RedoFrame,
            ];

            // Every generated button carries its own icon, so no two of them are the same.
            for (int i = 0; i < released.Length; ++i)
            {
                for (int j = i + 1; j < released.Length; ++j)
                {
                    Assert.False(
                        buttons[released[i]]!.Image.AsSpan().SequenceEqual(buttons[released[j]]!.Image.AsSpan()),
                        $"frames {released[i]} and {released[j]} are the same button");
                }

                // The pressed frame paints the face with another stone colour.
                Assert.False(
                    buttons[released[i]]!.Image.AsSpan().SequenceEqual(buttons[released[i] + 1]!.Image.AsSpan()),
                    $"frames {released[i]} and {released[i] + 1} are the same button");
            }
        }
    }

    [Fact]
    public void RealData_PutsTheUndoArrowOnItsButton()
    {
        AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        using (assets)
        {
            EditorButtonSet buttons = EditorButtonSet.TryCreate(assets)!;

            // The arrow is nineteen by thirteen, the image eight plus four on each side, so the frame
            // of the icon is twenty three by seventeen and the icon starts at (16, 16) of the button
            // (12 + 2 + 2 horizontally, 9 + 2 + 5 vertically for the tail row).
            const byte arrowColor = 56;
            Assert.Equal(arrowColor, buttons[EditorButtonSet.UndoFrame]!.Image[23, 16]);

            // The redo arrow is the same image mirrored inside its frame, so the tail sits twenty seven
            // pixels in, and the two buttons are mirror images around x = 25.
            Assert.Equal(arrowColor, buttons[EditorButtonSet.RedoFrame]!.Image[27, 16]);
        }
    }
}
