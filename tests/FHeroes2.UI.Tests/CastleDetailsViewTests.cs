using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="CastleDetailsView"/>: the checkboxes, the army strip, the nineteen cells and the
/// status message of the editor's castle details window.
/// </summary>
/// <remarks>
/// 素材全部从存档里读，所以用例在没有游戏数据的机器上直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class CastleDetailsViewTests
{
    [Fact]
    public void AllowCastleCheckbox_SitsInTheRightPanelOfATown() =>
        Assert.True(TryWithView(new CastleMetadata(), PlayerColor.Red, view =>
        {
            PixelBuffer output = CreateCanvas();

            view.Paint(output);

            Rect dialog = view.Frame.DialogArea;
            Assert.Equal(dialog.X + CastleDetailsView.CheckboxOffsetX, view.AllowCastleArea.X);
            Assert.Equal(dialog.Y + CastleDetailsView.AllowCastleOffsetY, view.AllowCastleArea.Y);
            Assert.True(view.AllowCastleArea.Width > 0);
        }));

    [Fact]
    public void AllowCastleCheckbox_IsMissingOnceTheFortStands() => Assert.True(TryWithView(FortCastle(), PlayerColor.Red,
        view =>
        {
            PixelBuffer output = CreateCanvas();

            view.Paint(output);

            // 要塞已经建好，就没有「允许建城堡」这一项，军队那一行的标题也换成 Castle Army。
            Assert.False(view.State.IsTown);
            Assert.Equal(Rect.Empty, view.AllowCastleArea);
        }));

    [Fact]
    public void DefaultBuildingsCheckbox_SitsThirtyPixelsBelowTheCastleOne() =>
        Assert.True(TryWithView(new CastleMetadata(), PlayerColor.Red, view =>
        {
            PixelBuffer output = CreateCanvas();

            view.Paint(output);

            Rect dialog = view.Frame.DialogArea;
            Assert.Equal(view.AllowCastleArea.X, view.DefaultBuildingsArea.X);
            Assert.Equal(dialog.Y + CastleDetailsView.DefaultBuildingsOffsetY, view.DefaultBuildingsArea.Y);
            Assert.Equal(CastleDetailsView.AllowCastleOffsetY + 30, CastleDetailsView.DefaultBuildingsOffsetY);
        }));

    [Fact]
    public void DefaultArmyCheckbox_OnlyForANeutralCastle() =>
        Assert.True(TryWithView(new CastleMetadata(), PlayerColor.None, view =>
        {
            PixelBuffer output = CreateCanvas();

            view.Paint(output);

            Rect dialog = view.Frame.DialogArea;
            Assert.True(view.State.IsNeutral);
            Assert.Equal(dialog.Y + CastleDetailsView.ArmyOffsetY, view.DefaultArmyArea.Y);
            Assert.True(view.DefaultArmyArea.Width > 0);
        }));

    [Fact]
    public void DefaultArmyCheckbox_IsMissingForAnOwnedCastle() =>
        Assert.True(TryWithView(new CastleMetadata(), PlayerColor.Blue, view =>
        {
            PixelBuffer output = CreateCanvas();

            view.Paint(output);

            Assert.False(view.State.IsNeutral);
            Assert.Equal(Rect.Empty, view.DefaultArmyArea);
        }));

    [Fact]
    public void Paint_LeavesTheNameBarShowingTheCastleName() =>
        Assert.True(TryWithView(new CastleMetadata { CustomName = "Alderaan" }, PlayerColor.Red, view =>
        {
            PixelBuffer canvas = CreateCanvas();
            Rect nameArea = view.Frame.NameArea;

            view.Paint(canvas);

            // 名字条本身是 CASLBAR 的一段，写上名字后至少有像素被改过。
            bool anyInk = false;

            for (int y = nameArea.Y; y < nameArea.Bottom && !anyInk; ++y)
            {
                for (int x = nameArea.X; x < nameArea.Right && !anyInk; ++x)
                {
                    anyInk = canvas[x, y] != canvas[nameArea.X, nameArea.Y];
                }
            }

            Assert.True(anyInk);
        }));

    [Fact]
    public void PaintStatusMessage_RepaintsAnUnchangedMessageAfterTheBar() =>
        Assert.True(TryWithView(new CastleMetadata(), PlayerColor.Red, view =>
        {
            PixelBuffer output = CreateCanvas();

            // 第一次写：状态栏底刚被 PaintBottomButtons 重画过，必须能画出来。
            Rect first = view.PaintStatusMessage(output, "Banned building");

            // 第二次同样的消息：底又被重画了，所以同样要返回非空。
            Rect second = view.PaintStatusMessage(output, "Banned building");

            Assert.False(first.IsEmpty);
            Assert.False(second.IsEmpty);
            Assert.Equal(first, second);
        }));

    [Fact]
    public void Paint_WritesTheMessageSetBeforehand() =>
        Assert.True(TryWithView(new CastleMetadata(), PlayerColor.Red, view =>
        {
            PixelBuffer blank = CreateCanvas();
            PixelBuffer withMessage = CreateCanvas();

            view.SetMessage(string.Empty);
            view.Paint(blank);

            view.SetMessage("Selected building is already built");
            view.Paint(withMessage);

            Rect roi = view.StatusBarMessageArea;
            bool anyInk = false;

            for (int y = roi.Y; y < roi.Y + 16 && !anyInk; ++y)
            {
                for (int x = roi.X; x < roi.Right && !anyInk; ++x)
                {
                    anyInk = blank[x, y] != withMessage[x, y];
                }
            }

            Assert.True(anyInk);
        }));

    [Fact]
    public void UpdateButtonPressStates_PressesTheButtonUnderTheHeldMouse() =>
        Assert.True(TryWithView(new CastleMetadata(), PlayerColor.Red, view =>
        {
            view.Paint(CreateCanvas());

            CastleDetailsFrame frame = view.Frame;
            ButtonSprite okay = frame.Okay ?? throw new InvalidOperationException("OKAY was not painted.");
            var local = new LocalEvent();

            view.UpdateButtonPressStates(local);
            Assert.False(okay.IsPressed);

            Rect area = okay.Area;
            var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

            local.OnMouseButton(true, MouseButtonType.Left, point);
            view.UpdateButtonPressStates(local);

            // 按住 OKAY 时只有它自己按下；EXIT 那边光标不在，不受影响。
            Assert.True(okay.IsPressed);
            Assert.False(frame.Exit?.IsPressed ?? false);

            local.OnMouseButton(false, MouseButtonType.Left, point);
            view.UpdateButtonPressStates(local);

            Assert.False(okay.IsPressed);
        }));

    [Fact]
    public void UpdateButtonPressStates_KeepsRestrictPressedWhileTheModeIsOn() =>
        Assert.True(TryWithView(new CastleMetadata(), PlayerColor.Red, view =>
        {
            view.Paint(CreateCanvas());

            ButtonSprite restrict = view.Frame.Restrict ?? throw new InvalidOperationException("RESTRICT was not painted.");
            var local = new LocalEvent();

            view.UpdateButtonPressStates(local);
            Assert.False(restrict.IsPressed);

            // 参考让它一直亮着：buildingRestriction || isMouseLeftButtonPressedAndHeldInArea(...)（:876）。
            view.State.BuildingRestriction = true;
            view.UpdateButtonPressStates(local);

            Assert.True(restrict.IsPressed);
        }));

    [Fact]
    public void Paint_DrawsThePressedLookOfAHeldButton() =>
        Assert.True(TryWithView(new CastleMetadata(), PlayerColor.Red, view =>
        {
            PixelBuffer released = CreateCanvas();
            view.Paint(released);

            ButtonSprite okay = view.Frame.Okay ?? throw new InvalidOperationException("OKAY was not painted.");
            Rect area = okay.Area;
            var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

            var local = new LocalEvent();
            local.OnMouseButton(true, MouseButtonType.Left, point);
            view.UpdateButtonPressStates(local);

            PixelBuffer pressed = CreateCanvas();
            view.Paint(pressed);

            // 按下态是另一帧图，还往右下挪了一像素，所以同一块区域里必然有像素不同。
            bool anyDifference = false;

            for (int y = area.Y; y < area.Bottom && !anyDifference; ++y)
            {
                for (int x = area.X; x < area.Right && !anyDifference; ++x)
                {
                    anyDifference = released[x, y] != pressed[x, y];
                }
            }

            Assert.True(anyDifference);
        }));

    /// <summary>Creates a castle whose fort is already built.</summary>
    private static CastleMetadata FortCastle()
    {
        CastleMetadata metadata = new();
        metadata.BuiltBuildings.Add((uint)BuildingType.Castle);

        return metadata;
    }

    /// <summary>Creates the view against the installed game data.</summary>
    /// <param name="metadata">The castle the window shows.</param>
    /// <param name="color">Owner colour, <see cref="PlayerColor.None"/> for a neutral castle.</param>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithView(CastleMetadata metadata, PlayerColor color, Action<CastleDetailsView> body)
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            CastleDetailsState state = new(metadata, MapRace.Knight, color, new ArmyStack());

            // 机器上有数据却读不出素材，那是真问题，不能悄悄跳过。
            CastleDetailsView view = CastleDetailsView.TryCreate(assets, state, new Palette(), isEvilInterface: false)
                ?? throw new InvalidOperationException("The artwork of the castle details window is missing.");

            body(view);
        }
        finally
        {
            GameAssets.Current = null;
        }

        return true;
    }

    private static PixelBuffer CreateCanvas()
    {
        PixelBuffer canvas = new(640, 480);
        canvas.Fill(3);

        return canvas;
    }
}
