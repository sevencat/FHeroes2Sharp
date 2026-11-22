using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.System;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the players panel, <c>gui/player_info.cpp</c>.
/// </summary>
/// <remarks>
/// <see cref="Players.Init(PlayerColor)"/> 写的是静态的颜色表，所以这里跟换资源包的测试一起串行；像素级的
/// <see cref="PlayersInfo.RedrawInfo"/> 对拍要真的 <c>NGEXTRA</c> 与字体，留到逐函数核对那一轮。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class PlayersInfoTests
{
    [Fact]
    public void TwoPlayersShareTheRowSymmetrically()
    {
        // 6 个槽位铺满 width*6，2 个人落在 1/4 与 3/4 处。
        Assert.Equal(51, GameScenarioInfo.GetStep4Player(0, 51, 2));
        Assert.Equal(204, GameScenarioInfo.GetStep4Player(1, 51, 2));

        // 满员时首尾贴边、步长正好一格。
        Assert.Equal(0, GameScenarioInfo.GetStep4Player(0, 51, 6));
        Assert.Equal(255, GameScenarioInfo.GetStep4Player(5, 51, 6));
    }

    [Fact]
    public void ZeroPlayersIsRejected()
    {
        Assert.Throws<DivideByZeroException>(() => GameScenarioInfo.GetStep4Player(0, 51, 0));
    }

    [Fact]
    public void UpdateInfoLaysTheRoisOutLikeTheReference()
    {
        Players players = new();
        players.Init(PlayerColor.Red | PlayerColor.Blue);

        PlayersInfo info = new();
        info.UpdateInfo(players, new Point(10, 20), new Point(20, 100), new StubIconArtwork(51, 51, 24, 20));

        Assert.Equal(2, info.Count);

        // 类型图标：102 宽的整排（51*2）在 2 人时也按 6 个槽位算，落点是第二个与第五个槽位的一半处。
        Assert.Equal(10 + 51, info[0].PlayerTypeRoi.X);
        Assert.Equal(20, info[0].PlayerTypeRoi.Y);
        Assert.Equal(10 + 204, info[1].PlayerTypeRoi.X);

        // 名字条紧贴类型图标下沿。
        Assert.Equal(info[0].PlayerTypeRoi.Y + info[0].PlayerTypeRoi.Height, info[0].NameRoi.Y);
        Assert.Equal(info[0].PlayerTypeRoi.X, info[0].NameRoi.X);

        // 手气图标在种族图标下方 69 像素处。
        Assert.Equal(100 + 69, info[0].HandicapRoi.Y);
        Assert.Equal(info[0].ClassRoi.X, info[0].HandicapRoi.X);
    }

    [Fact]
    public void HitTestsFindThePlayerUnderThePoint()
    {
        Players players = new();
        players.Init(PlayerColor.Red | PlayerColor.Blue);

        PlayersInfo info = new();
        info.UpdateInfo(players, new Point(0, 0), new Point(0, 100), new StubIconArtwork(51, 51, 24, 20));

        Point onRedType = new(info[0].PlayerTypeRoi.X + 1, info[0].PlayerTypeRoi.Y + 1);

        Assert.Same(info[0].Player, info.GetFromOpponentClick(onRedType));
        Assert.Null(info.GetFromOpponentNameClick(onRedType));
        Assert.Null(info.GetFromClassClick(onRedType));
        Assert.Null(info.GetPlayerFromHandicapRoi(onRedType));

        Point onRedHandicap = new(info[0].HandicapRoi.X + 1, info[0].HandicapRoi.Y + 1);

        Assert.Same(info[0].Player, info.GetPlayerFromHandicapRoi(onRedHandicap));
        Assert.Null(info.GetFromOpponentClick(onRedHandicap));
    }

    [Fact]
    public void ResetSelectionForgetsTheFirstPick()
    {
        Player player = new(PlayerColor.Red);
        PlayersInfo info = new() { CurrentSelectedPlayer = player };

        info.ResetSelection();

        Assert.Null(info.CurrentSelectedPlayer);
    }

    [Fact]
    public void ApplyNameKeepsTheOldOneWhenNothingWasTyped()
    {
        Player player = new(PlayerColor.Red);
        string original = player.Name;

        PlayersInfo.ApplyName(player, null);
        Assert.Equal(original, player.Name);

        PlayersInfo.ApplyName(player, string.Empty);
        Assert.Equal(original, player.Name);

        PlayersInfo.ApplyName(player, "Catherine");
        Assert.Equal("Catherine", player.Name);
    }

    private sealed class StubIconArtwork(int playerTypeWidth, int classWidth, int handicapWidth, int handicapHeight)
        : IPlayersInfoArtwork
    {
        /// <summary>参考只用 3（类型）、51（种族）、0（手气）三帧的尺寸。</summary>
        public Sprite GetIcon(uint frame) => frame switch
        {
            3 => new Sprite(playerTypeWidth, playerTypeWidth),
            51 => new Sprite(classWidth, classWidth),
            0 => new Sprite(handicapWidth, handicapHeight),
            _ => new Sprite(1, 1),
        };
    }
}
