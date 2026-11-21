using FHeroes2.Assets.Maps;
using FHeroes2.Game;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

namespace FHeroes2.UI.Dialogs;

/// <summary>The columns the "Best Hero" and "Best Hero Stats" rows print, <c>drawHeroStats()</c>.</summary>
/// <param name="Id">The portrait id, <c>Heroes::GetID()</c>; the small portrait is <c>MINIPORT</c> frame <c>Id - 1</c>.</param>
/// <param name="Attack">Primary attack.</param>
/// <param name="Defense">Primary defense.</param>
/// <param name="Power">Spell power.</param>
/// <param name="Knowledge">Knowledge.</param>
public readonly record struct ThievesGuildHero(int Id, int Attack, int Defense, int Power, int Knowledge);

/// <summary>Everything one column of the thieves' guild report shows.</summary>
/// <param name="Color">The player the column belongs to.</param>
/// <param name="Towns">Towns without a fort.</param>
/// <param name="Castles">Towns with a fort.</param>
/// <param name="Heroes">Heroes in the field.</param>
/// <param name="Gold">Gold in the treasury.</param>
/// <param name="WoodAndOre">Wood plus ore.</param>
/// <param name="RareResources">Gems, crystal, sulfur and mercury summed.</param>
/// <param name="Obelisks">Obelisk tiles visited.</param>
/// <param name="Artifacts">Artifacts owned.</param>
/// <param name="ArmyStrength">Total army strength, truncated like the reference's cast.</param>
/// <param name="Income">Gold per day.</param>
/// <param name="Personality">"Human" or the AI personality, <c>GetPersonalityString()</c>.</param>
/// <param name="BestHero">The hero the ranking compares, or <see langword="null"/> when the kingdom has none.</param>
/// <param name="StrongestMonster">The strongest creature owned, or <see langword="null"/>.</param>
public sealed record ThievesGuildPlayer(
    PlayerColor Color,
    int Towns,
    int Castles,
    int Heroes,
    int Gold,
    int WoodAndOre,
    int RareResources,
    int Obelisks,
    int Artifacts,
    int ArmyStrength,
    int Income,
    string Personality,
    ThievesGuildHero? BestHero,
    MonsterType? StrongestMonster)
{
    /// <summary>The value a row ranks by.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The value of this player for that row.</returns>
    public int Value(ThievesGuildRow row) => row switch
    {
        ThievesGuildRow.Towns => Towns,
        ThievesGuildRow.Castles => Castles,
        ThievesGuildRow.Heroes => Heroes,
        ThievesGuildRow.Gold => Gold,
        ThievesGuildRow.WoodAndOre => WoodAndOre,
        ThievesGuildRow.RareResources => RareResources,
        ThievesGuildRow.Obelisks => Obelisks,
        ThievesGuildRow.Artifacts => Artifacts,
        ThievesGuildRow.ArmyStrength => ArmyStrength,
        ThievesGuildRow.Income => Income,
        _ => 0,
    };
}

/// <summary>
/// The player ranking the thieves' guild and the oracle show, <c>Dialog::ThievesGuild()</c>
/// (<c>dialog_thievesguild.cpp:242-486</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把十个 <c>lambda</c> 直接塞进 <c>getInfo()</c>；端口先把每个玩家的一格数据读成
/// <see cref="ThievesGuildPlayer"/>，窗口只负责把它画出来，排行由 <see cref="ThievesGuildRanking"/> 算。
/// </para>
/// <para>
/// <b>门槛</b>：城里每多一座盗贼公会，就多露两三行（<see cref="ThievesGuildRows.RequiredGuilds"/>）；神谕处传
/// <c>0xFF</c>，于是所有行都显示。整张表和 <c>world</c> 无关之后，单测直接构造几个玩家就能钉住分组与排序。
/// </para>
/// </remarks>
public sealed class ThievesGuildReport
{
    /// <summary>The guild count the oracle passes, <c>0xFF</c>.</summary>
    public const int OracleGuilds = 0xFF;

    private readonly ThievesGuildPlayer[] _players;

    private ThievesGuildReport(ThievesGuildPlayer[] players, int thievesGuilds, bool isOracle)
    {
        _players = players;
        ThievesGuilds = thievesGuilds;
        IsOracle = isOracle;
    }

    /// <summary>The columns, left to right.</summary>
    public IReadOnlyList<ThievesGuildPlayer> Players => _players;

    /// <summary>Guilds the kingdom owns, or <see cref="OracleGuilds"/>.</summary>
    public int ThievesGuilds { get; }

    /// <summary>Whether the window was opened at the oracle instead of a guild.</summary>
    public bool IsOracle { get; }

    /// <summary>The title printed on the status bar.</summary>
    public string Title => IsOracle ? "Oracle: Player Rankings" : "Thieves' Guild: Player Rankings";

    /// <summary>Whether a row paints flags at all.</summary>
    /// <param name="row">The row.</param>
    /// <returns><see langword="true"/> when the kingdom has enough guilds.</returns>
    public bool IsRevealed(ThievesGuildRow row) => ThievesGuildRows.IsRevealed(row, ThievesGuilds);

    /// <summary>Whether the "Best Hero" row prints a portrait.</summary>
    public bool ShowsBestHero => ThievesGuilds > 0;

    /// <summary>Whether the "Best Hero Stats" row prints the four primary skills.</summary>
    public bool ShowsHeroStats => ThievesGuilds > 1;

    /// <summary>Whether the "Personality" row prints anything.</summary>
    public bool ShowsPersonality => ThievesGuilds > 2;

    /// <summary>Whether the "Best Monster" row prints a creature.</summary>
    public bool ShowsBestMonster => ThievesGuilds > 3;

    /// <summary>Builds a report from data that was already gathered.</summary>
    /// <param name="players">The columns, in draw order.</param>
    /// <param name="thievesGuilds">Guilds the kingdom owns, or <see cref="OracleGuilds"/>.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="players"/> is <see langword="null"/>.</exception>
    public static ThievesGuildReport Create(IReadOnlyList<ThievesGuildPlayer> players, int thievesGuilds)
    {
        ArgumentNullException.ThrowIfNull(players);

        return new ThievesGuildReport([.. players], thievesGuilds, thievesGuilds >= OracleGuilds);
    }

    /// <summary>Reads every kingdom in play, the reference's ten lambdas in one pass.</summary>
    /// <param name="world">The world the kingdoms live in.</param>
    /// <param name="viewer">The colour whose guild count decides the thresholds.</param>
    /// <param name="oracle">
    /// Whether the report is opened at the oracle: it then reveals every row and ignores the guild count.
    /// </param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public static ThievesGuildReport FromWorld(World world, PlayerColor viewer, bool oracle = false)
    {
        ArgumentNullException.ThrowIfNull(world);

        // 参考问的是 Settings 里在局的玩家槽；端口问王国，颜色还在但已经出局的王国不算。
        IReadOnlyList<PlayerColor> colors =
        [
            .. PlayerColors.ToList(world.Kingdoms.GetColors()).Where(color => world.GetKingdom(color).IsPlay),
        ];

        ThievesGuildPlayer[] players = new ThievesGuildPlayer[colors.Count];

        for (int i = 0; i < colors.Count; i++)
        {
            PlayerColor color = colors[i];
            Kingdom kingdom = world.GetKingdom(color);
            Hero? hero = kingdom.GetBestHero();
            Player? player = FHeroes2.Game.System.Players.Get(color);

            Funds funds = kingdom.Treasury;
            MonsterType strongest = kingdom.GetStrongestMonster();

            players[i] = new ThievesGuildPlayer(
                color,
                kingdom.CountTowns(),
                kingdom.CountCastles(),
                kingdom.Heroes.Count,
                funds.Gold,
                funds.Wood + funds.Ore,
                funds.Gems + funds.Crystal + funds.Sulfur + funds.Mercury,
                kingdom.CountVisitedObjects(Mp2ObjectType.Obelisk),
                kingdom.GetCountArtifacts(),
                (int)kingdom.GetArmiesStrength(),
                kingdom.GetIncome(resource => world.CountCapturedMines(resource, color)).Gold,
                player is null || player.IsControlHuman ? "Human" : player.PersonalityString,
                hero is null
                    ? null
                    : new ThievesGuildHero(
                        hero.PortraitId,
                        hero.GetAttack(),
                        hero.GetDefense(),
                        hero.GetPower(),
                        hero.GetKnowledge()),
                strongest == MonsterType.Unknown ? null : strongest);
        }

        return new ThievesGuildReport(
            players,
            oracle ? OracleGuilds : world.GetKingdom(viewer).CountThievesGuilds(),
            oracle);
    }

    /// <summary>The ranking of one row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>One group per distinct value, best first.</returns>
    public IReadOnlyList<ThievesGuildGroup> Ranking(ThievesGuildRow row) =>
        ThievesGuildRanking.Build([.. _players.Select(player => player.Color)], color => Find(color)!.Value(row));

    /// <summary>Finds the column of a colour.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The column, or <see langword="null"/> when the colour is not in play.</returns>
    public ThievesGuildPlayer? Find(PlayerColor color)
    {
        foreach (ThievesGuildPlayer player in _players)
        {
            if (player.Color == color)
            {
                return player;
            }
        }

        return null;
    }
}
