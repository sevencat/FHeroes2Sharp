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

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="CastleDetailsInput"/>: which control the cursor is over, what the status bar says and
/// what a click changes.
/// </summary>
/// <remarks>
/// 素材全部从存档里读，所以用例在没有游戏数据的机器上直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class CastleDetailsInputTests
{
    [Fact]
    public void Idle_ShowsTheCastleSettingsLine() => Assert.True(TryWithInput(TownMetadata(), PlayerColor.Red,
        (input, view) =>
        {
            // 活动区左上角既不在名字条、也不在十九格里，参考的判断链走到底也什么都不会说。
            input.Process(Hover(new Point(view.Frame.DialogArea.X + 2, view.Frame.DialogArea.Y + 2)));

            Assert.Equal(CastleDetailsInput.IdleMessage, input.Message);
            Assert.Equal(CastleDetailsControl.None, input.Cursor);
        }));

    [Fact]
    public void HoverOverTheName_ShowsTheNameHint() => Assert.True(TryWithInput(TownMetadata(), PlayerColor.Red,
        (input, view) =>
        {
            input.Process(Hover(Center(view.NameArea)));

            Assert.Equal(CastleDetailsControl.Name, input.Cursor);
            Assert.Equal(CastleDetailsInput.NameHint, input.Message);
        }));

    [Fact]
    public void ClickOnTheName_AsksTheHostForANewName() => Assert.True(TryWithInput(
        TownMetadata("Alderaan"), PlayerColor.Red, (input, view) =>
        {
            input.Process(Click(Center(view.NameArea)));

            Assert.Equal(CastleDetailsRequestKind.EditName, input.Request.Kind);
            Assert.Equal("Alderaan", input.Request.Title);
        }));

    [Fact]
    public void ClickOnTheAllowCastleSign_TogglesIt() => Assert.True(TryWithInput(TownMetadata(),
        PlayerColor.Red, (input, view) =>
        {
            bool before = input.State.AllowCastle;

            input.Process(Click(Center(view.AllowCastleArea)));

            Assert.NotEqual(before, input.State.AllowCastle);
            // 切换不是弹窗，宿主这一帧不需要做别的事。
            Assert.True(input.Request.IsNone);
        }));

    [Fact]
    public void RightClickOnTheAllowCastleSign_AsksForItsMessage() => Assert.True(TryWithInput(
        TownMetadata(), PlayerColor.Red, (input, view) =>
        {
            input.Process(RightPress(Center(view.AllowCastleArea)));

            Assert.Equal(CastleDetailsRequestKind.ShowMessage, input.Request.Kind);
            Assert.Equal(CastleDetailsInput.AllowCastleTitle, input.Request.Title);
            Assert.Equal(CastleDetailsInput.AllowCastleHint, input.Request.Body);
        }));

    [Fact]
    public void ClickOnTheDefaultBuildingsSign_FlipsTheChoice() => Assert.True(TryWithInput(TownMetadata(),
        PlayerColor.Red, (input, view) =>
        {
            bool before = input.State.CustomBuildings;

            input.Process(Click(Center(view.DefaultBuildingsArea)));

            Assert.NotEqual(before, input.State.CustomBuildings);
        }));

    [Fact]
    public void ClickOnTheRestrictButton_TogglesTheRestrictionMode() => Assert.True(TryWithInput(TownMetadata(),
        PlayerColor.Red, (input, view) =>
        {
            Assert.NotNull(view.Frame.Restrict);
            Assert.False(input.State.BuildingRestriction);

            input.Process(Click(Center(view.Frame.Restrict!.Area)));

            Assert.True(input.State.BuildingRestriction);
        }));

    [Fact]
    public void ClickOnTheDefaultArmySign_OnlyWorksForANeutralCastle() => Assert.True(TryWithInput(
        NeutralMetadata(), PlayerColor.None, (input, view) =>
        {
            Assert.True(input.State.IsNeutral);

            bool before = input.State.DefaultArmy;

            input.Process(Click(Center(view.DefaultArmyArea)));

            Assert.NotEqual(before, input.State.DefaultArmy);
        }));

    [Fact]
    public void ClickOnTheResetNameButton_ClearsTheName() => Assert.True(TryWithInput(
        TownMetadata("Alderaan"), PlayerColor.Red, (input, view) =>
        {
            Assert.NotNull(view.Frame.ResetName);

            input.Process(Click(Center(view.Frame.ResetName!.Area)));

            Assert.Equal(string.Empty, input.State.Metadata.CustomName);
        }));

    [Fact]
    public void ClickOnTheOkayButton_Accepts() => Assert.True(TryWithInput(TownMetadata(), PlayerColor.Red,
        (input, view) =>
        {
            Assert.NotNull(view.Frame.Okay);

            CastleDetailsOutcome outcome = input.Process(Click(Center(view.Frame.Okay!.Area)));

            Assert.Equal(CastleDetailsOutcome.Accepted, outcome);
            Assert.Equal(CastleDetailsOutcome.Accepted, input.Outcome);
        }));

    [Fact]
    public void ClickOnExitWithoutChanges_ClosesRightAway() => Assert.True(TryWithInput(TownMetadata(),
        PlayerColor.Red, (input, view) =>
        {
            Assert.NotNull(view.Frame.Exit);

            CastleDetailsOutcome outcome = input.Process(Click(Center(view.Frame.Exit!.Area)));

            Assert.Equal(CastleDetailsOutcome.Discarded, outcome);
            Assert.True(input.Request.IsNone);
        }));

    [Fact]
    public void ClickOnExitAfterAToggle_AsksTheHostBeforeDiscarding() => Assert.True(TryWithInput(
        TownMetadata(), PlayerColor.Red, (input, view) =>
        {
            input.Process(Click(Center(view.AllowCastleArea)));

            CastleDetailsOutcome outcome = input.Process(Click(Center(view.Frame.Exit!.Area)));

            Assert.Equal(CastleDetailsOutcome.None, outcome);
            Assert.Equal(CastleDetailsRequestKind.ShowMessage, input.Request.Kind);
            Assert.Equal(CastleDetailsInput.UnsavedChangesMessage, input.Request.Body);

            // 宿主弹完框，作者选了「留着」。
            Assert.Equal(CastleDetailsOutcome.None, input.ResolveUnsavedChanges(false));
            // 再来一次，这回选「丢掉」。
            Assert.Equal(CastleDetailsOutcome.Discarded, input.ResolveUnsavedChanges(true));
        }));

    [Fact]
    public void ClickOnASlot_BuildsItAndUnchecksTheDefaultBuildings() => Assert.True(TryWithInput(
        TownMetadata(), PlayerColor.Red, (input, view) =>
        {
            CastleBuildingCell cell = input.State.Cells[0];
            int before = cell.BuiltLevel;

            input.Process(Hover(Center(cell.Area)));
            Assert.Equal(CastleDetailsControl.Building, input.Cursor);
            Assert.Equal(0, input.CursorSlot);
            Assert.Equal(cell.RenderBuildingName, input.Message);

            input.Process(Click(Center(cell.Area)));

            Assert.NotEqual(before, cell.BuiltLevel);

            // 动过某一格就不再是「默认建筑」：勾选标记被摘掉，写回后元数据里也是自定义。
            Assert.True(input.State.CustomBuildings);

            input.State.WriteMetadata();
            Assert.True(input.State.Metadata.CustomBuildings);
        }));

    [Fact]
    public void RightClickOnASlot_AsksForTheDescription() => Assert.True(TryWithInput(TownMetadata(),
        PlayerColor.Red, (input, _) =>
        {
            CastleBuildingCell cell = input.State.Cells[0];

            input.Process(RightPress(Center(cell.Area)));

            Assert.Equal(CastleDetailsRequestKind.ShowMessage, input.Request.Kind);
            Assert.Equal(CastleBuildingInfo.GetName(input.State.Race, cell.RenderBuilding),
                input.Request.Title);
            Assert.Equal(BuildingInfo.GetBuildingDescription(input.State.Race, cell.RenderBuilding),
                input.Request.Body);
        }));

    [Fact]
    public void HoverOverTheArmy_AsksTheHostToFeedTheBar() => Assert.True(TryWithInput(TownMetadata(),
        PlayerColor.Red, (input, view) =>
        {
            input.Process(Hover(Center(view.ArmyBarArea)));

            Assert.Equal(CastleDetailsControl.Army, input.Cursor);
            Assert.Equal(CastleDetailsRequestKind.Army, input.Request.Kind);
        }));

    [Fact]
    public void AWindowThatWasNotTouched_HasNoUnsavedChanges() => Assert.True(TryWithInput(TownMetadata(),
        PlayerColor.Red, (input, _) =>
        {
            Assert.False(input.State.HasUnsavedChanges());
        }));

    [Fact]
    public void AfterAClick_TheWindowHasUnsavedChanges() => Assert.True(TryWithInput(TownMetadata(),
        PlayerColor.Red, (input, view) =>
        {
            input.Process(Click(Center(view.AllowCastleArea)));

            Assert.True(input.State.HasUnsavedChanges());
        }));

    /// <summary>
    /// A town as the editor hands it over: the tent is always built, so the metadata the window writes back is
    /// the same one it read.
    /// </summary>
    /// <param name="name">The custom name, empty for the default one.</param>
    /// <returns>The metadata.</returns>
    private static CastleMetadata TownMetadata(string name = "")
    {
        CastleMetadata metadata = new() { CustomName = name };
        metadata.BuiltBuildings.Add((uint)BuildingType.Tent);

        return metadata;
    }

    /// <summary>A neutral town, which keeps the default guard of its race.</summary>
    /// <returns>The metadata.</returns>
    private static CastleMetadata NeutralMetadata()
    {
        CastleMetadata metadata = TownMetadata();
        MapFormatArmy.SetDefaultCastleDefenderArmy(metadata);

        return metadata;
    }

    /// <summary>Creates the window and the handler against the installed game data.</summary>
    /// <param name="metadata">The castle the window shows.</param>
    /// <param name="color">Owner colour, <see cref="PlayerColor.None"/> for a neutral castle.</param>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithInput(CastleMetadata metadata, PlayerColor color,
        Action<CastleDetailsInput, CastleDetailsView> body)
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            // 军队由调用方从元数据里读进条里（参考也是这样），否则「打开窗口没动过」会被误判成有改动。
            Army army = new();
            MapFormatArmy.LoadCastleArmy(army, metadata);

            CastleDetailsState state = new(metadata, MapRace.Knight, color, army);

            // 命中判定读的是绘制时算出来的区域，所以必须先画一帧。
            CastleDetailsView view = CastleDetailsView.TryCreate(assets, state, new Palette(), isEvilInterface: false)
                ?? throw new InvalidOperationException("The artwork of the castle details window is missing.");

            view.Paint(CreateCanvas());

            body(new CastleDetailsInput(view), view);
        }
        finally
        {
            GameAssets.Current = null;
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
        canvas.Fill(3);

        return canvas;
    }
}
