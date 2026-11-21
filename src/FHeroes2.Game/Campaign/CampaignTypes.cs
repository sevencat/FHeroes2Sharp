using FHeroes2.Core.Io;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// The identifiers of the campaigns of the original game, <c>enum CampaignID</c>
/// (<c>src/fheroes2/campaign/campaign_scenariodata.h:35-43</c>).
/// </summary>
/// <remarks>
/// 数值固定：存档里存的就是这个数，改了会让老存档指到别的战役。
/// </remarks>
public static class CampaignId
{
    /// <summary>Roland's campaign, the first one in the list, <c>ROLAND_CAMPAIGN</c>.</summary>
    public const int Roland = 0;

    /// <summary>Archibald's campaign, <c>ARCHIBALD_CAMPAIGN</c>.</summary>
    public const int Archibald = 1;

    /// <summary>The Price of Loyalty campaign, <c>PRICE_OF_LOYALTY_CAMPAIGN</c>.</summary>
    public const int PriceOfLoyalty = 2;

    /// <summary>The Descendants campaign, <c>DESCENDANTS_CAMPAIGN</c>.</summary>
    public const int Descendants = 3;

    /// <summary>Wizard's Isle, <c>WIZARDS_ISLE_CAMPAIGN</c>.</summary>
    public const int WizardsIsle = 4;

    /// <summary>Voyage Home, <c>VOYAGE_HOME_CAMPAIGN</c>.</summary>
    public const int VoyageHome = 5;
}

/// <summary>
/// The victory condition a campaign scenario adds on top of the map's own,
/// <c>enum class ScenarioVictoryCondition</c> (<c>campaign_scenariodata.h:45-51</c>).
/// </summary>
/// <remarks>
/// 原版战役有几关的条件不在图里：拿王冠、抢黑龙城、夺反魔法球。这些条件由战役数据给出，地图本身给不出来。
/// </remarks>
public enum ScenarioVictoryCondition
{
    /// <summary>Whatever the map file says, <c>STANDARD</c>.</summary>
    Standard = 0,

    /// <summary>Take the dragon city, <c>CAPTURE_DRAGON_CITY</c>.</summary>
    CaptureDragonCity = 1,

    /// <summary>Collect the Ultimate Crown, <c>OBTAIN_ULTIMATE_CROWN</c>.</summary>
    ObtainUltimateCrown = 2,

    /// <summary>Collect the Sphere of Negation, <c>OBTAIN_SPHERE_NEGATION</c>.</summary>
    ObtainSphereNegation = 3,
}

/// <summary>
/// The loss condition a campaign scenario adds on top of the map's own,
/// <c>enum class ScenarioLossCondition</c> (<c>campaign_scenariodata.h:53-57</c>).
/// </summary>
public enum ScenarioLossCondition
{
    /// <summary>Whatever the map file says, <c>STANDARD</c>.</summary>
    Standard = 0,

    /// <summary>Losing every Sorceress village ends the game, <c>LOSE_ALL_SORCERESS_VILLAGES</c>.</summary>
    LoseAllSorceressVillages = 1,
}

/// <summary>
/// The position of a scenario inside a campaign, <c>struct ScenarioInfoId</c>
/// (<c>campaign_scenariodata.h:59-86</c>).
/// </summary>
/// <param name="CampaignId">Which campaign, <c>campaignId</c>.</param>
/// <param name="ScenarioId">Which scenario inside it, <c>scenarioId</c>.</param>
/// <remarks>
/// 参考给了一个全 <c>-1</c> 的默认值表示「未开始」，端口照这个约定：<see cref="NotStarted"/>。
/// </remarks>
public readonly record struct ScenarioInfoId(int CampaignId, int ScenarioId)
{
    /// <summary>The value used before any scenario is chosen, <c>{ -1, -1 }</c>.</summary>
    public static ScenarioInfoId NotStarted => new(-1, -1);

    /// <summary>Writes the pair, <c>operator&lt;&lt;</c> of <c>campaign_scenariodata.cpp</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    public void Write(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(CampaignId);
        stream.Write(ScenarioId);
    }

    /// <summary>Reads the pair, <c>operator&gt;&gt;</c> of <c>campaign_scenariodata.cpp</c>.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The pair.</returns>
    public static ScenarioInfoId Read(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        int campaignId = stream.ReadInt32();
        int scenarioId = stream.ReadInt32();

        return new ScenarioInfoId(campaignId, scenarioId);
    }
}

/// <summary>
/// What to do with a file of a video sequence, <c>enum VideoControl : uint32_t</c>
/// (<c>src/fheroes2/game/game_video_type.h:28-48</c>).
/// </summary>
/// <remarks>
/// 参考用的是位标志：低位说「播完等按键」，第 1 位说「循环」，第 2、3 位说「有音轨、有画面」，最后三个组合值只是
/// 常用搭配的别名。端口照位标志移植，判断时用 <see cref="VideoControl.Cutscene"/> 这类组合值做包含测试。
/// </remarks>
[Flags]
public enum VideoControl : uint
{
    /// <summary>Nothing to play, <c>PLAY_NONE</c>.</summary>
    None = 0,

    /// <summary>Play once and wait for the player, <c>PLAY_WAIT</c>.</summary>
    Wait = 1 << 0,

    /// <summary>Loop forever, <c>PLAY_LOOP</c>.</summary>
    Loop = 1 << 1,

    /// <summary>Play the audio stream, <c>PLAY_AUDIO</c>.</summary>
    Audio = 1 << 2,

    /// <summary>Play the video stream, <c>PLAY_VIDEO</c>.</summary>
    Video = 1 << 3,

    /// <summary>Loop the picture without sound, <c>PLAY_VIDEO_LOOP</c>.</summary>
    VideoLoop = Loop | Video,

    /// <summary>Play a cutscene once, <c>PLAY_CUTSCENE</c>.</summary>
    Cutscene = Audio | Video,

    /// <summary>Play a cutscene once and wait, <c>PLAY_CUTSCENE_WAIT</c>.</summary>
    CutsceneWait = Wait | Audio | Video,

    /// <summary>Loop a cutscene, <c>PLAY_CUTSCENE_LOOP</c>.</summary>
    CutsceneLoop = Loop | Audio | Video,
}

/// <summary>
/// One file of a video sequence, <c>struct Video::VideoInfo</c> (<c>game_video.h:39-44</c>).
/// </summary>
/// <param name="FileName">The file in the data directory, <c>fileName</c>.</param>
/// <param name="Control">What to do with it, <c>control</c>.</param>
/// <param name="Offset">Where the picture is drawn, <c>offset</c>.</param>
/// <remarks>
/// 端口没有视频播放器（S12 记录过），但这段数据是战役定义的一部分：哪一关播哪几个文件、播几次、放在哪个位置，
/// 都在战役数据表里。数据先落地，播放器等有界面层时来接。
/// </remarks>
public readonly record struct CampaignVideoInfo(string FileName, VideoControl Control, int OffsetX = 0, int OffsetY = 0);
