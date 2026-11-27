using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Editor;

/// <summary>
/// Paints the map specifications page, the drawing half of <c>mapSpecificationsDialog()</c>
/// (<c>editor_map_specs_window.cpp:2054-2210</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把画和事件揉在同一个 <c>while</c> 里，每帧直接往 <c>display</c> 上贴。端口把画面拆成按区块命名的方法：
/// 事件循环（<c>a7l.3.5.3</c>）该重画哪块就调哪个，全量重画调 <see cref="Draw"/>。区块之间没有依赖，
/// 但都假定底图已经刷过——所以 <see cref="Draw"/> 先画石头底，其余方法单独调时由调用方保证背景正确。
/// </para>
/// <para>
/// 坐标全用 <see cref="MapSpecsPageLayout"/> 的绝对值，因此 <c>output</c> 必须是整屏画布（或至少是
/// 与页面同原点的画布），而不是页面大小的小图。这与参考一致：它往 <c>display</c> 上按屏幕坐标贴。
/// </para>
/// <para>
/// 与参考的一处差异：参考用 <c>ImageRestorer</c> 记住描述框底纹，重画描述时先还原再画字；这里每次重画
/// 都从石头底重来，所以不需要还原——代价是重画描述必须连同底图一起刷（<see cref="Draw"/> 就是这么做）。
/// </para>
/// </remarks>
public sealed class MapSpecsPageRenderer
{
    private readonly MapSpecsPageArtwork _artwork;
    private readonly MapSpecsPageLayout _layout;
    private readonly MapSpecsState _state;
    private readonly PixelBuffer _output;

    /// <summary>
    /// Prepares the painter of one page.
    /// </summary>
    /// <param name="artwork">Sprites the page is assembled from.</param>
    /// <param name="layout">Where everything goes.</param>
    /// <param name="state">The specifications being edited; read on every draw so edits show up.</param>
    /// <param name="output">Frame buffer of the scene, in page coordinates.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public MapSpecsPageRenderer(MapSpecsPageArtwork artwork, MapSpecsPageLayout layout, MapSpecsState state, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(output);

        _artwork = artwork;
        _layout = layout;
        _state = state;
        _output = output;
    }

    /// <summary>The layout the painter reads.</summary>
    public MapSpecsPageLayout Layout => _layout;

    /// <summary>Paints the whole page.</summary>
    /// <param name="victoryDropButtonPressed">Whether the victory drop button is held down (<c>:2257-2259</c>).</param>
    /// <param name="lossDropButtonPressed">Whether the loss drop button is held down (<c>:2259-2261</c>).</param>
    public void Draw(bool victoryDropButtonPressed = false, bool lossDropButtonPressed = false)
    {
        DrawBackground();
        DrawName();
        DrawPlayers();
        DrawDifficulties();
        DrawDescription();
        DrawConditions(victoryDropButtonPressed, lossDropButtonPressed);
    }

    /// <summary>Fills the active area with <c>STONEBAK</c> (<c>:2054-2057</c>).</summary>
    public void DrawBackground()
    {
        Rect activeArea = _layout.ActiveArea;

        // 参考把石头图从 (0,0) 起按活动区大小整块拷过去，超出图面的部分自然被裁掉。
        Rect source = new(0, 0, Math.Min(activeArea.Width, _artwork.Stone.Width), Math.Min(activeArea.Height, _artwork.Stone.Height));

        ImageOps.Blit(_output, activeArea.Position, _artwork.Stone.Image, source);
    }

    /// <summary>Paints the name frame, its gradient shadow and the map name (<c>:2064-2074</c>).</summary>
    public void DrawName()
    {
        Rect frame = _layout.ScenarioBoxRoi;

        ImageOps.BlitSprite(_output, frame.Position, _artwork.NameBox);
        ImageOps.AddGradientShadow(_artwork.NameBox, _output, frame.Position, new Point(-5, 5));

        Rect textRoi = _layout.MapNameRoi;

        Text text = new(_state.Name, FontType.NormalWhite());
        text.FitToOneRow(textRoi.Width);
        text.DrawInRoi(textRoi.X, textRoi.Y + 3, textRoi.Width, _output, textRoi);
    }

    /// <summary>Paints the player icons with their shadows (<c>:2076-2097</c>).</summary>
    public void DrawPlayers()
    {
        IReadOnlyList<PlayerColor> colors = PlayerColors.ToList(_state.AvailablePlayers);

        for (int index = 0; index < colors.Count && index < _layout.PlayerRois.Count; ++index)
        {
            ImageOps.BlitSprite(_output, _layout.PlayerShadowRois[index].Position, _artwork.PlayerShadow);

            // 参考：Color::GetIndex( color ) + getPlayerIcnIndex( mapFormat, color )。
            Sprite? icon = _artwork.GetPlayerIcon(PlayerColors.GetIndex(colors[index]) + MapSpecsPageIcons.GetPlayerIconIndex(_state, colors[index]));

            if (icon is not null)
            {
                ImageOps.BlitSprite(_output, _layout.PlayerRois[index].Position, icon);
            }
        }
    }

    /// <summary>Paints the difficulty row and the cursor (<c>:2099-2134</c>).</summary>
    public void DrawDifficulties()
    {
        DrawCenteredTitle("Map Difficulty:", _layout.DifficultyTitleCenterX, _layout.DifficultyTitleY);

        for (int index = 0; index < MapSpecsPageLayout.DifficultyCount; ++index)
        {
            GameDifficulty difficulty = MapSpecsPageIcons.GetDifficultyFromIndex(index);
            Sprite? icon = _artwork.GetDifficultyIcon(difficulty);

            if (icon is not null)
            {
                ImageOps.BlitSprite(_output, _layout.DifficultyIconRois[index].Position, icon);
                ImageOps.AddGradientShadow(icon, _output, _layout.DifficultyIconRois[index].Position, new Point(-5, 5));
            }

            // 名字写在挪过 3 像素的格子里，居中于图标下方 7 像素。
            Rect cell = _layout.DifficultyRois[index];
            Text label = new(Difficulty.GetName(difficulty), FontType.SmallWhite());
            label.Draw(cell.X + ((cell.Width - label.Width()) / 2), cell.Y + cell.Height + 7, _output);
        }

        Rect cursor = _layout.DifficultyRois[MapSpecsPageIcons.GetDifficultyIndex(_state.Difficulty)];

        ImageOps.BlitSprite(_output, cursor.Position, _artwork.DifficultyCursor);
    }

    /// <summary>Paints the description title and the shaded text box (<c>:2136-2150</c>).</summary>
    public void DrawDescription()
    {
        DrawCenteredTitle("Map Description:", _layout.DescriptionTitleCenterX, _layout.DescriptionTitleY);

        ImageOps.ApplyTextBackgroundShading(_output, _layout.DescriptionShadingRoi);

        Rect textRoi = _layout.DescriptionTextRoi;

        Text text = new(_state.Description, FontType.NormalWhite());
        text.FitToArea(textRoi.Width, textRoi.Height);
        text.DrawInRoi(textRoi.X, textRoi.Y, textRoi.Width, _output, textRoi);
    }

    /// <summary>Paints both condition columns: titles, selected rows and drop buttons (<c>:2152-2208</c>).</summary>
    /// <param name="victoryDropButtonPressed">Whether the victory drop button is held down (<c>:2222-2223</c>).</param>
    /// <param name="lossDropButtonPressed">Whether the loss drop button is held down (<c>:2224-2225</c>).</param>
    public void DrawConditions(bool victoryDropButtonPressed = false, bool lossDropButtonPressed = false)
    {
        DrawCenteredTitle("Special Victory Condition:", _layout.VictoryTitleCenterX, _layout.VictoryTitleY);
        DrawCenteredTitle("Special Loss Condition:", _layout.LossTitleCenterX, _layout.LossTitleY);

        ImageOps.BlitSprite(_output, _layout.VictoryBackgroundRoi.Position, _artwork.ConditionBackground);
        MapSpecsConditionRows.DrawVictory(
            _state.VictoryCondition,
            _layout.VictoryTextRoi,
            yellowFont: false,
            _artwork.GetConditionIcon(MapSpecsConditionRows.GetVictoryIconIndex(_state.VictoryCondition)),
            _output);
        ImageOps.BlitSprite(_output, _layout.VictoryButtonRoi.Position,
            victoryDropButtonPressed ? _artwork.DropButton.Pressed : _artwork.DropButton.Released);

        ImageOps.BlitSprite(_output, _layout.LossBackgroundRoi.Position, _artwork.ConditionBackground);
        MapSpecsConditionRows.DrawLoss(
            _state.LossCondition,
            _layout.LossTextRoi,
            yellowFont: false,
            _artwork.GetConditionIcon(MapSpecsConditionRows.GetLossIconIndex(_state.LossCondition)),
            _output);
        ImageOps.BlitSprite(_output, _layout.LossButtonRoi.Position,
            lossDropButtonPressed ? _artwork.DropButton.Pressed : _artwork.DropButton.Released);
    }

    private void DrawCenteredTitle(string label, int centerX, int y)
    {
        Text text = new(label, FontType.NormalWhite());
        text.Draw(centerX - (text.Width() / 2), y, _output);
    }
}
