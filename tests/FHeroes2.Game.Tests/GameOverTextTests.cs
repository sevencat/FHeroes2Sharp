using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The two text producers of <c>game_over.cpp</c>, ported by <see cref="GameOver.GetString"/> and
/// <see cref="GameOver.GetDescription"/>.
/// </summary>
[Collection(SettingsSingletonCollection.Name)]
public sealed class GameOverTextTests
{
    /// <summary>A map information block with the given condition parameters only.</summary>
    /// <param name="victoryParameters">The parameters of the victory condition.</param>
    /// <param name="lossParameters">The parameters of the loss condition.</param>
    /// <returns>The block.</returns>
    private static Mp2FileInfo Info(int[]? victoryParameters = null, int[]? lossParameters = null) => new()
    {
        FilePath = "gameover.mp2",
        Name = "GameOver",
        Description = string.Empty,
        Width = 36,
        Height = 36,
        Difficulty = GameDifficulty.Normal,
        VictoryConditionParameters = victoryParameters ?? [0, 0],
        LossConditionParameters = lossParameters ?? [0, 0],
    };

    [Theory]
    [InlineData(GameOver.WinsAll, "Defeat all enemy heroes and capture all enemy towns and castles.")]
    [InlineData(GameOver.WinsTown, "Capture a specific town.")]
    [InlineData(GameOver.WinsHero, "Defeat a specific hero.")]
    [InlineData(GameOver.WinsArtifact, "Find a specific artifact.")]
    [InlineData(GameOver.WinsSide, "Your side defeats the opposing side.")]
    [InlineData(GameOver.WinsGold, "Accumulate a large amount of gold.")]
    [InlineData(GameOver.LossAll, "Lose all your heroes and towns.")]
    [InlineData(GameOver.LossTown, "Lose a specific town.")]
    [InlineData(GameOver.LossHero, "Lose a specific hero.")]
    [InlineData(GameOver.LossTime, "Run out of time. (Fail to win by a certain point.)")]
    [InlineData(GameOver.ConditionNone, "None")]
    public void GetStringPrintsTheConditionTable(uint condition, string expected) =>
        Assert.Equal(expected, GameOver.GetString(condition));

    [Fact]
    public void DefeatingEveryoneIsThePlainSentence()
    {
        Assert.Equal(
            GameOver.GetString(GameOver.WinsAll),
            GameOver.GetDescription(GameOver.WinsAll, Info()));
    }

    [Fact]
    public void GoldCountsTheStoredThousands()
    {
        // 参数里存的是千枚金币。
        Assert.Equal(
            "Accumulate 10000 gold.",
            GameOver.GetDescription(GameOver.WinsGold, Info(victoryParameters: [10, 0])));
    }

    [Fact]
    public void TheUltimateArtifactHasNoName()
    {
        Assert.Equal(
            "Find the ultimate artifact.",
            GameOver.GetDescription(GameOver.WinsArtifact, Info(victoryParameters: [0])));
    }

    [Fact]
    public void ASpecificArtifactIsNamed()
    {
        Mp2FileInfo info = Info(victoryParameters: [(int)ArtifactType.ThunderMace]);

        Assert.Equal(
            $"Find the '{ArtifactTable.Get(ArtifactType.ThunderMace).Name}' artifact.",
            GameOver.GetDescription(GameOver.WinsArtifact, info));
    }

    [Fact]
    public void NormalVictoryIsAppendedToAnotherVictoryCondition()
    {
        // 攒金胜利同时允许全灭时，参考在句尾补一句，提醒还能打光对手。
        Assert.Equal(
            "Accumulate 10000 gold., or you may win by defeating all enemy heroes and capturing all enemy towns and castles.",
            GameOver.GetDescription(GameOver.WinsGold | GameOver.WinsAll, Info(victoryParameters: [10, 0])));
    }

    [Fact]
    public void TimeLimitIsSplitIntoMonthsWeeksAndDays()
    {
        // 第 29 天是期限：第 28 天结束之前必须赢，也就是第二个月第一周的末尾。
        Assert.Equal(
            "Fail to win by the end of month 2, week 1, day 1.",
            GameOver.GetDescription(GameOver.LossTime, Info(lossParameters: [29, 0])));
    }

    [Fact]
    public void OnlyAnExactDefeatReplacesTheVictorySentence()
    {
        // 参考这里用 `==` 而不是位与：判定结果每次只会带一条输法，所以相等成立；混着别的位就保留胜利那句。
        Assert.Equal(
            GameOver.GetString(GameOver.LossAll),
            GameOver.GetDescription(GameOver.LossAll, Info()));

        Assert.Equal(
            "Accumulate 10000 gold.",
            GameOver.GetDescription(GameOver.WinsGold | GameOver.LossAll, Info(victoryParameters: [10, 0])));
    }

    [Fact]
    public void AMissingCastleFallsBackToTheGenericSentence()
    {
        // 没有世界（或图里那座城已经不存在）时参考只 assert 后留空，端口退回通用句式。
        Assert.Equal(
            GameOver.GetString(GameOver.WinsTown),
            GameOver.GetDescription(GameOver.WinsTown, Info(victoryParameters: [12, 34]), world: null));
    }

    [Fact]
    public void AlliesAndEnemiesAreListedByName()
    {
        Settings.Reset();
        Settings settings = Settings.Get();
        settings.Players.Init(PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red);
        settings.Players.SetCurrentColor(PlayerColor.Blue);
        Players.Get(PlayerColor.Blue)!.Friends = PlayerColor.Blue | PlayerColor.Green;

        Assert.Equal(
            "The alliance consisting of Green and you must defeat the enemy Red.",
            GameOver.GetDescription(GameOver.WinsSide, Info()));
    }

    [Fact]
    public void WithoutAlliesTheEnemiesAreAnAllianceWhenThereAreSeveral()
    {
        Settings.Reset();
        Settings settings = Settings.Get();
        settings.Players.Init(PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red);
        settings.Players.SetCurrentColor(PlayerColor.Blue);

        // 名字按地图格式的颜色顺序列出：绿在前，红在后。
        Assert.Equal(
            "You must defeat the enemy alliance of Green, Red.",
            GameOver.GetDescription(GameOver.WinsSide, Info()));
    }

    [Fact]
    public void ASideWithoutEnemiesFallsBackToTheGenericSentence()
    {
        Settings.Reset();
        Settings settings = Settings.Get();
        settings.Players.Init(PlayerColor.Blue);
        settings.Players.SetCurrentColor(PlayerColor.Blue);
        Players.Get(PlayerColor.Blue)!.Friends = PlayerColor.Blue;

        Assert.Equal(
            GameOver.GetString(GameOver.WinsSide),
            GameOver.GetDescription(GameOver.WinsSide, Info()));
    }

    [Fact]
    public void DefeatingEveryoneNeedsNoVictoryDialog()
    {
        // 被全灭的对手只剩这一种可能，参考不为它弹窗。
        Assert.Empty(GameOver.GetVictoryMessage(GameOver.WinsAll, Info()));
    }

    [Fact]
    public void SideVictoryAndGoldVictoryHaveTheirOwnWords()
    {
        Assert.Equal(
            "The enemy is beaten.\nYour side has triumphed!",
            GameOver.GetVictoryMessage(GameOver.WinsSide, Info()));

        Assert.Equal(
            "You have built up over 10000 gold in your treasury.\nAll enemies bow before your wealth and power.",
            GameOver.GetVictoryMessage(GameOver.WinsGold, Info(victoryParameters: [10, 0])));
    }

    [Fact]
    public void TheUltimateArtifactIsSpelledOutInTheVictoryDialog()
    {
        Assert.Equal(
            "You have found the Ultimate Artifact.\nYour quest is complete.",
            GameOver.GetVictoryMessage(GameOver.WinsArtifact, Info(victoryParameters: [0])));
    }

    [Fact]
    public void TheDefeatDialogsCoverEliminationAndTime()
    {
        Assert.Equal(
            "You have been eliminated from the game!!!",
            GameOver.GetLossMessage(GameOver.LossAll, Info()));

        Assert.Equal(
            "You have failed to complete your quest in time.\nAll is lost.",
            GameOver.GetLossMessage(GameOver.LossTime, Info(lossParameters: [29, 0])));
    }

    [Fact]
    public void TheEnemyWinningByGoldIsAnnouncedWithTheTreasury()
    {
        Assert.Equal(
            "The enemy has built up over 10000 gold in his treasury.\nYou must bow done in defeat before his wealth and power.",
            GameOver.GetLossMessage(GameOver.LossEnemyWinsGold, Info(victoryParameters: [10, 0])));
    }

    [Fact]
    public void AMissingObjectGivesNoDialogAtAll()
    {
        Assert.Empty(GameOver.GetVictoryMessage(GameOver.WinsTown, Info(victoryParameters: [12, 34])));
        Assert.Empty(GameOver.GetLossMessage(GameOver.LossHero, Info(lossParameters: [12, 34])));
    }

    [Fact]
    public void ACombinedMaskHasNoDialog()
    {
        // 两个文案表都是 switch 精确匹配，混着别的位就没有对应句子。
        Assert.Empty(GameOver.GetVictoryMessage(GameOver.WinsGold | GameOver.WinsAll, Info(victoryParameters: [10, 0])));
        Assert.Empty(GameOver.GetLossMessage(GameOver.LossAll | GameOver.LossTime, Info()));
    }
}
