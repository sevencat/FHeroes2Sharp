using FHeroes2.Assets.Audio;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// The theme a campaign selection screen plays, <c>playCampaignMusic()</c> (<c>game_campaign.cpp:872-888</c>).
/// </summary>
/// <remarks>
/// 参考只给罗兰与阿基巴德两套战役写了专属曲目；价格联盟那几套沿用阿基巴德的曲子（它们走的是同一个 EVIL 界面），
/// 所以这里返回 <see langword="null"/> 表示「不换曲」，由调用方保持当前音乐。
/// </remarks>
public static class CampaignMusic
{
    /// <summary>
    /// Picks the theme of a campaign, <c>playCampaignMusic()</c> (<c>game_campaign.cpp:872-888</c>).
    /// </summary>
    /// <param name="campaignId">The campaign, see <see cref="CampaignId"/>.</param>
    /// <returns>The track to play, or <see langword="null"/> when the campaign has no theme of its own.</returns>
    public static MusicTrack? GetSelectionTrack(int campaignId)
    {
        return campaignId switch
        {
            CampaignId.Roland => MusicTrack.RolandCampaignScreen,
            CampaignId.Archibald => MusicTrack.ArchibaldCampaignScreen,
            _ => null,
        };
    }
}
