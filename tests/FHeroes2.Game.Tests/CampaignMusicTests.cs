using FHeroes2.Assets.Audio;
using FHeroes2.Game.Campaign;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The campaign selection themes, <c>playCampaignMusic()</c> (<c>game_campaign.cpp:872-888</c>).
/// </summary>
public sealed class CampaignMusicTests
{
    [Fact]
    public void RolandPlaysTheRolandTheme()
    {
        Assert.Equal(MusicTrack.RolandCampaignScreen, CampaignMusic.GetSelectionTrack(CampaignId.Roland));
    }

    [Fact]
    public void ArchibaldPlaysTheArchibaldTheme()
    {
        Assert.Equal(MusicTrack.ArchibaldCampaignScreen, CampaignMusic.GetSelectionTrack(CampaignId.Archibald));
    }

    /// <summary>
    /// 参考的 <c>playCampaignMusic()</c> 只有两个分支，价格联盟那几套既不换曲也不报错。
    /// </summary>
    [Theory]
    [InlineData(CampaignId.PriceOfLoyalty)]
    [InlineData(CampaignId.VoyageHome)]
    [InlineData(CampaignId.WizardsIsle)]
    [InlineData(CampaignId.Descendants)]
    public void CampaignsWithoutAThemeKeepTheCurrentMusic(int campaignId)
    {
        Assert.Null(CampaignMusic.GetSelectionTrack(campaignId));
    }
}
