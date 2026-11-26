using FHeroes2.Assets.Maps;
using FHeroes2.Game.Highscores;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// The lines the reference burns over <c>WIN.SMK</c> when a campaign is finished, the subtitle of
/// <c>game_campaign.cpp:1245-1269</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考把三段文字拼成一个 <c>MultiFontText</c>：标题与天数用 <c>normalWhite</c>、难度用 <c>smallWhite</c>、
/// 分数与兵种名用 <c>normalWhite</c>，从第 5 秒起画在片子那块烧黑的方框上（位置 <c>(475,110)</c>，最宽 140 像素）。
/// 端口把三段分开放出来，界面层愿意按字体分别画就分别画，只画一种字体也能拼出同一段话。
/// </para>
/// <para>
/// 兵种名是「按天数反查」的那一只（<see cref="HighScoreDataContainer.GetMonsterByDay"/>），不是玩家打过的怪物：
/// 天数越少画得越强，所以速通会看到黑龙、拖到五千八百天才看到农民。
/// </para>
/// </remarks>
public static class CampaignEndingText
{
    /// <summary>The msgid of a campaign difficulty, <c>getCampaignDifficultyText()</c> (<c>game_campaign.cpp:895-910</c>).</summary>
    /// <param name="difficulty">One of <see cref="CampaignDifficulty"/>.</param>
    /// <returns>The msgid, e.g. <c>campaignDifficulty|Default</c>.</returns>
    public static string DifficultyMessageId(int difficulty) => difficulty switch
    {
        CampaignDifficulty.Decreased => "campaignDifficulty|Decreased",
        CampaignDifficulty.Increased => "campaignDifficulty|Increased",
        _ => "campaignDifficulty|Default",
    };

    /// <summary>The title and the day count, <c>_( "Congratulations!\n\nDays: %{days}\n" )</c>.</summary>
    /// <param name="daysPassed">Days the whole campaign took, <c>getDaysPassed()</c>.</param>
    /// <returns>The paragraph, with the reference's line breaks.</returns>
    public static string DaysLine(uint daysPassed) => $"Congratulations!\n\nDays: {daysPassed}\n";

    /// <summary>The difficulty paragraph, <c>_( "\nDifficulty: %{difficulty}\n\n" )</c>.</summary>
    /// <param name="difficultyName">The already translated name of the difficulty.</param>
    /// <returns>The paragraph.</returns>
    public static string DifficultyLine(string difficultyName) =>
        $"\nDifficulty: {difficultyName}\n\n";

    /// <summary>The score paragraph, <c>_( "Score: %{score}\n\nRating:\n%{rating}" )</c>.</summary>
    /// <param name="score">The campaign score, <c>daysPassed * difficultyPercent / 100</c>.</param>
    /// <returns>The paragraph, the rating being the creature of that score.</returns>
    public static string ScoreLine(uint score) => $"Score: {score}\n\nRating:\n{RatingName(score)}";

    /// <summary>Every paragraph in one string, the <c>ratingText</c> the reference builds.</summary>
    /// <param name="daysPassed">Days the whole campaign took.</param>
    /// <param name="difficultyName">The already translated name of the difficulty.</param>
    /// <param name="score">The campaign score.</param>
    /// <returns>The three paragraphs one after another.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="difficultyName"/> is <see langword="null"/>.</exception>
    public static string Describe(uint daysPassed, string difficultyName, uint score)
    {
        ArgumentNullException.ThrowIfNull(difficultyName);

        return DaysLine(daysPassed) + DifficultyLine(difficultyName) + ScoreLine(score);
    }

    /// <summary>
    /// The creature that illustrates a score, <c>HighScoreDataContainer::getMonsterByDay( score ).GetName()</c>.
    /// </summary>
    /// <param name="score">The campaign score.</param>
    /// <returns>The English name of the creature, e.g. <c>Black Dragon</c> for a fast campaign.</returns>
    public static string RatingName(uint score) =>
        Mp2MonsterName.Of((int)HighScoreDataContainer.GetMonsterByDay(score));
}
