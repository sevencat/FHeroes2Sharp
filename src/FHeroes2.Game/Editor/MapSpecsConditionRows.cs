using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The text and the icon of a victory or loss condition row, a port of <c>getVictoryConditionText</c>,
/// <c>getVictoryIcnIndex</c>, <c>getLossConditionText</c>, <c>getAllianceOrderText</c>, <c>getLossIcnIndex</c>,
/// <c>redrawVictoryCondition</c> and <c>redrawLossCondition</c>
/// (<c>src/fheroes2/editor/editor_map_specs_window.cpp:499-623</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这几支函数是下拉框与条件面板共用的「一行长什么样」：图标取自 <c>REQUESTS</c> 的 30-35（胜利）与 36-39（失败），
/// 文字与 <c>GameOver::GetString</c> 的那张短表是同一套话术——只有「全灭所有人」这一条例外，参考在这里写
/// <c>"None."</c>，因为它是「没有特殊条件」的默认项，不该显示成「击败所有敌人的英雄并占领所有城镇」。
/// </para>
/// <para>
/// <b>图标是参数，不是全局资源。</b>参考在函数里直接 <c>Assets::getImage( ICN::REQUESTS, ... )</c>；端口把
/// <see cref="Sprite"/> 交给调用方，窗口按 <see cref="GetVictoryIconIndex"/> 取帧后传进来，测试也就能用一张
/// 纯色图验证贴图位置而不必打开归档。
/// </para>
/// <para>
/// <b>行的画法。</b>图标贴到 <c>roi.x + 1, roi.y</c>，文字从 <c>roi.x + 20, roi.y + 2</c> 起画并裁剪到整行里；
/// 选中行用黄色字体、其余用白色，这一点与参考的两支 <c>redraw*Condition</c> 相同。
/// </para>
/// </remarks>
public static class MapSpecsConditionRows
{
    /// <summary>
    /// The text of a victory condition, <c>getVictoryConditionText()</c>
    /// (<c>editor_map_specs_window.cpp:499-521</c>).
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>The row text.</returns>
    public static string GetVictoryText(MapVictoryCondition condition) => condition switch
    {
        MapVictoryCondition.DefeatEveryone => "None.",
        MapVictoryCondition.CaptureTown => GameOver.GetString(GameOver.WinsTown),
        MapVictoryCondition.KillHero => GameOver.GetString(GameOver.WinsHero),
        MapVictoryCondition.ObtainArtifact => GameOver.GetString(GameOver.WinsArtifact),
        MapVictoryCondition.DefeatOtherSide => "One side defeats the other.",
        MapVictoryCondition.CollectGold => "Accumulate gold.",
        _ => "Unknown",
    };

    /// <summary>
    /// The frame of <c>REQUESTS</c> that stands for a victory condition, <c>getVictoryIcnIndex()</c>
    /// (<c>editor_map_specs_window.cpp:523-545</c>).
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>The frame index, <c>0</c> for an unknown condition.</returns>
    public static int GetVictoryIconIndex(MapVictoryCondition condition) => condition switch
    {
        MapVictoryCondition.DefeatEveryone => 30,
        MapVictoryCondition.CaptureTown => 31,
        MapVictoryCondition.KillHero => 32,
        MapVictoryCondition.ObtainArtifact => 33,
        MapVictoryCondition.DefeatOtherSide => 34,
        MapVictoryCondition.CollectGold => 35,
        _ => 0,
    };

    /// <summary>
    /// The text of a loss condition, <c>getLossConditionText()</c>
    /// (<c>editor_map_specs_window.cpp:547-565</c>).
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>The row text.</returns>
    public static string GetLossText(MapLossCondition condition) => condition switch
    {
        MapLossCondition.Everything => "None.",
        MapLossCondition.LoseTown => GameOver.GetString(GameOver.LossTown),
        MapLossCondition.LoseHero => GameOver.GetString(GameOver.LossHero),
        MapLossCondition.OutOfTime => "Run out of time.",
        _ => "Unknown",
    };

    /// <summary>
    /// The frame of <c>REQUESTS</c> that stands for a loss condition, <c>getLossIcnIndex()</c>
    /// (<c>editor_map_specs_window.cpp:589-607</c>).
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>The frame index, <c>0</c> for an unknown condition.</returns>
    public static int GetLossIconIndex(MapLossCondition condition) => condition switch
    {
        MapLossCondition.Everything => 36,
        MapLossCondition.LoseTown => 37,
        MapLossCondition.LoseHero => 38,
        MapLossCondition.OutOfTime => 39,
        _ => 0,
    };

    /// <summary>
    /// The ordinal of an alliance in the victory condition's list, <c>getAllianceOrderText()</c>
    /// (<c>editor_map_specs_window.cpp:568-587</c>).
    /// </summary>
    /// <remarks>
    /// 参考用 <c>"alliance|1st"</c> 这类带上下文的键值做本地化；端口没有这一层，直接给序数词。序号从 0 起，
    /// 最多到 4——引擎只有六个颜色，最多五份同盟。
    /// </remarks>
    /// <param name="index">Zero-based order.</param>
    /// <returns>The ordinal, <c>"Unknown"</c> outside 0-4.</returns>
    public static string GetAllianceOrderText(int index) => index switch
    {
        0 => "1st",
        1 => "2nd",
        2 => "3rd",
        3 => "4th",
        4 => "5th",
        _ => "Unknown",
    };

    /// <summary>
    /// Paints a victory condition row, <c>redrawVictoryCondition()</c>
    /// (<c>editor_map_specs_window.cpp:609-615</c>).
    /// </summary>
    /// <param name="condition">The condition the row stands for.</param>
    /// <param name="roi">The row.</param>
    /// <param name="yellowFont">Whether the row is the selected one, <c>yellowFont</c>.</param>
    /// <param name="icon">Frame <see cref="GetVictoryIconIndex"/> of <c>REQUESTS</c>; <see langword="null"/> draws the text alone.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static void DrawVictory(MapVictoryCondition condition, in Rect roi, bool yellowFont, Sprite? icon, PixelBuffer output) =>
        Draw(GetVictoryText(condition), roi, yellowFont, icon, output);

    /// <summary>
    /// Paints a loss condition row, <c>redrawLossCondition()</c>
    /// (<c>editor_map_specs_window.cpp:617-623</c>).
    /// </summary>
    /// <param name="condition">The condition the row stands for.</param>
    /// <param name="roi">The row.</param>
    /// <param name="yellowFont">Whether the row is the selected one, <c>yellowFont</c>.</param>
    /// <param name="icon">Frame <see cref="GetLossIconIndex"/> of <c>REQUESTS</c>; <see langword="null"/> draws the text alone.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static void DrawLoss(MapLossCondition condition, in Rect roi, bool yellowFont, Sprite? icon, PixelBuffer output) =>
        Draw(GetLossText(condition), roi, yellowFont, icon, output);

    /// <summary>Paints the icon and the text of a row, the body of both <c>redraw*Condition</c> functions.</summary>
    private static void Draw(string text, in Rect roi, bool yellowFont, Sprite? icon, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        // 图标缺失时只画文字：编辑器窗口不该因为美术件不全就画不出来。
        if (icon is not null)
        {
            ImageOps.BlitSprite(output, new Point(roi.X + 1, roi.Y), icon);
        }

        Text label = new(text, yellowFont ? FontType.NormalYellow() : FontType.NormalWhite());
        label.DrawInRoi(roi.X + 20, roi.Y + 2, output, roi);
    }
}
