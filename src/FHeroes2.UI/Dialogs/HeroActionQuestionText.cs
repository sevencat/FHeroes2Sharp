using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The wording of the yes/no questions of a hero action, the <c>Dialog::ArmyJoinFree</c> … <c>Dialog::YES|NO</c>
/// family of <c>heroes_action.cpp</c>: which title and which sentence go with which
/// <see cref="HeroActionQuestion"/>.
/// </summary>
/// <remarks>
/// <para>
/// 措辞归界面（ADR-122）：游戏层只问「问了什么」，这里负责把那句话按参考原文写出来。参考的每个问句都是
/// 就地拼出来的字符串（<c>_( "A group of %{monster} …" )</c>），端口把它收到一处，宿主拿到的就是
/// 「标题 + 正文」两行。
/// </para>
/// <para>
/// <b>合并的文案</b>：参考里同一类动作在不同对象上有不同的句子（坟场/沉船/弃船三段、
/// 九座怪物居所各一段、三座可打服的居所各一段）。游戏层的问句枚举把同类收成一个值（见
/// <see cref="HeroActionQuestion"/> 的说明），所以这里只能挑一句通吃的：数量占多数的那一种（沉船），或者
/// 把对象名换成 <c>%{monster}</c>。要逐对象还原就得给枚举加对象类型，那是后面的事，不在这里猜。
/// </para>
/// <para>
/// 数字与名字按参考的规矩拼：怪物名走 <see cref="MonsterNames"/> 并转小写（参考的
/// <c>StringReplaceWithLowercase</c>），价钱走 <c>%{gold}</c> 的位置。
/// </para>
/// </remarks>
public static class HeroActionQuestionText
{
    /// <summary>Title of the free-join window, <c>Dialog::ArmyJoinFree</c>'s <c>_( "Followers" )</c>.</summary>
    private const string FollowersTitle = "Followers";

    /// <summary>Title of the pyramid, <c>MP2::StringObject( OBJ_PYRAMID )</c>.</summary>
    private const string PyramidTitle = "Pyramid";

    /// <summary>Title of the abandoned mine, <c>MP2::StringObject( OBJ_ABANDONED_MINE )</c>.</summary>
    private const string AbandonedMineTitle = "Abandoned Mine";

    /// <summary>Title of the daemon cave, <c>MP2::StringObject( OBJ_DAEMON_CAVE )</c>.</summary>
    private const string DaemonCaveTitle = "Daemon Cave";

    /// <summary>Title of the genie's lamp, <c>MP2::StringObject( OBJ_GENIE_LAMP )</c>.</summary>
    private const string GenieLampTitle = "Genie's Lamp";

    /// <summary>Title of the alchemist's tower, <c>MP2::StringObject( OBJ_ALCHEMIST_TOWER )</c>.</summary>
    private const string AlchemistTowerTitle = "Alchemist's Tower";

    /// <summary>Title of the sphinx, <c>MP2::StringObject( OBJ_SPHINX )</c>.</summary>
    public const string SphinxTitle = "Sphinx";

    /// <summary>
    /// The title and the sentence of a question that carries a stack, <c>Dialog::ArmyJoinFree</c>,
    /// <c>Dialog::ArmyJoinWithCost</c> and <c>Dialog::RunAway</c>.
    /// </summary>
    /// <param name="question">Which question.</param>
    /// <param name="troop">The stack the question is about.</param>
    /// <param name="gold">The sum the question quotes, zero when it quotes none.</param>
    /// <returns>Title of the window, empty for none, and the sentence under it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The question carries no stack.</exception>
    /// <summary>
    /// The body of the riddle input window, <c>heroes_action.cpp:3558-3561</c>: the lead-in, the riddle between
    /// quotes and the call to answer.
    /// </summary>
    /// <param name="riddle">The riddle as the map stores it.</param>
    /// <returns>The text under the title of the input window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="riddle"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 参考在谜语后面还挂了一段 <c>"sphinx|'"</c>：<c>|</c> 是 gettext 的分句标记，屏幕上只留单引号，端口直接写单引号。
    /// </remarks>
    public static string SphinxRiddle(string riddle)
    {
        ArgumentNullException.ThrowIfNull(riddle);

        return $"The Sphinx asks you the following riddle:\n\n'{riddle}'\n\nYour answer?";
    }

    /// <summary>
    /// The title and the sentence of a question that carries a stack, <c>Dialog::ArmyJoinFree</c>,
    /// <c>Dialog::ArmyJoinWithCost</c> and <c>Dialog::RunAway</c>.
    /// </summary>
    /// <param name="question">Which question.</param>
    /// <param name="troop">The stack the question is about.</param>
    /// <param name="gold">The sum the question quotes, zero when it quotes none.</param>
    /// <returns>Title of the window, empty for none, and the sentence under it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The question carries no stack.</exception>
    public static (string Header, string Body) Describe(HeroActionQuestion question, Troop troop, uint gold = 0)
    {
        string monster = MonsterNames.GetMulti(troop.Monster).ToLowerInvariant();

        switch (question)
        {
            case HeroActionQuestion.JoinForFree:
                return (FollowersTitle,
                    $"A group of {monster} with a desire for greater glory wish to join you.\nDo you accept?");

            case HeroActionQuestion.JoinForMoney:
                // 参考按人数分三种写法：一个人用单数，全员加入与部分加入各一句。端口传进来的是「愿意加入的
                // 那一支」而不是「全部守军」，所以走「全员加入」那一句。
                if (troop.Count == 1)
                {
                    string single = MonsterNames.GetPluralName(troop.Monster, 1).ToLowerInvariant();

                    return (string.Empty,
                        $"The {single} is swayed by your diplomatic tongue, and offers to join your army for the sum of {gold} gold.\nDo you accept?");
                }

                return (string.Empty,
                    $"The creatures are swayed by your diplomatic\ntongue, and make you an offer:\n\n" +
                    $"All {troop.Count} of the {monster} will join your army for the sum of {gold} gold.\nDo you accept?");

            case HeroActionQuestion.PursueFleeingMonsters:
                return (string.Empty,
                    $"The {monster}, awed by the power of your forces, begin to scatter.\nDo you wish to pursue and engage them?");

            case HeroActionQuestion.JoinDwellingMonsters:
                return (string.Empty,
                    $"A group of {monster} with a desire for greater glory wish to join you. Do you accept?");

            case HeroActionQuestion.RecruitDwellingMonsters:
                return (string.Empty,
                    $"Some of the {monster} living here are willing to join your army for a price. Do you want to recruit {monster}?");

            case HeroActionQuestion.FightDwellingMonsters:
                return (string.Empty, $"The {monster} living here do not want to join you. Do you want to fight them?");

            case HeroActionQuestion.RecruitDefeatedDwellingMonsters:
                return (string.Empty,
                    $"Some of the surviving {monster} agree to join your army for a price. Do you want to recruit them?");

            default:
                throw new ArgumentOutOfRangeException(nameof(question), question,
                    "This question carries no stack; the other overload describes it.");
        }
    }

    /// <summary>
    /// The title and the sentence of a question that carries no stack, the plain <c>Dialog::YES|NO</c> of
    /// <c>heroes_action.cpp</c>.
    /// </summary>
    /// <param name="question">Which question.</param>
    /// <param name="gold">The sum the question quotes, zero when it quotes none.</param>
    /// <returns>Title of the window, empty for none, and the sentence under it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The question carries a stack.</exception>
    public static (string Header, string Body) Describe(HeroActionQuestion question, uint gold = 0)
    {
        switch (question)
        {
            case HeroActionQuestion.SearchPoorMoraleObject:
                // 参考在坟场/沉船/弃船之间换句子；沉船与弃船占两种，取沉船那句。
                return (string.Empty,
                    "The rotting hulk of a great pirate ship creaks eerily as it is pushed against the rocks. Do you wish to search the shipwreck?");

            case HeroActionQuestion.EnterAbandonedMine:
                return (AbandonedMineTitle,
                    "You come upon an abandoned gold mine. The mine appears to be haunted. Do you wish to enter?");

            case HeroActionQuestion.SearchPyramid:
                return (PyramidTitle,
                    "You come upon the pyramid of a great and ancient king.\nYou are tempted to search it for treasure, but all the old stories warn of fearful curses and undead guardians.\nWill you search?");

            case HeroActionQuestion.EnterDaemonCave:
                return (DaemonCaveTitle,
                    "The entrance to the cave is dark, and a foul, sulfurous smell issues from the cave mouth. Will you enter?");

            case HeroActionQuestion.FightDaemonServants:
                return (DaemonCaveTitle,
                    "You find a powerful and grotesque Demon in the cave. \"Today,\" it rasps, \"you will fight and surely die. But I will give you a choice of deaths. You may fight me, or you may fight my servants. Do you prefer to fight my servants?\"");

            case HeroActionQuestion.PayDaemonRansom:
                return (DaemonCaveTitle,
                    "The Demon leaps upon you and has its claws at your throat before you can even draw your sword. \"Your life is mine,\" it says. \"I will sell it back to you for " +
                    gold + " gold.\"");

            case HeroActionQuestion.RubGenieLamp:
                return (GenieLampTitle,
                    "You stumble upon a dented and tarnished lamp lodged deep in the earth. Do you wish to rub the lamp?");

            case HeroActionQuestion.PayAlchemist:
                // 参考按诅咒物品的件数在单复数之间换句子；问句枚举不带件数，用单数那一句。
                return (AlchemistTowerTitle,
                    "As you enter the Alchemist's Tower, a hobbled, graying man in a brown cloak makes his way towards you.\n" +
                    "He checks your pack, and sees that you have 1 cursed item.\n" +
                    $"For {gold} gold, the alchemist will remove it for you. Do you pay?");

            case HeroActionQuestion.AcceptSphinxChallenge:
                return (SphinxTitle,
                    "\"I have a riddle for you,\" the Sphinx says. \"Answer correctly, and you shall be rewarded. Answer incorrectly, and you shall be eaten. Do you accept the challenge?\"");

            default:
                throw new ArgumentOutOfRangeException(nameof(question), question,
                    "This question carries a stack; the other overload describes it.");
        }
    }

    /// <summary>
    /// The sentence the reference shows after the player refused, <c>heroes_action.cpp:399</c>; the host shows
    /// it itself because the fight starts right after the answer.
    /// </summary>
    /// <param name="question">Which question was refused.</param>
    /// <returns>The sentence, or <see langword="null"/> when the reference says nothing more.</returns>
    public static string? Refusal(HeroActionQuestion question) => question switch
    {
        HeroActionQuestion.JoinForFree or HeroActionQuestion.JoinForMoney =>
            "Insulted by your refusal of their offer, the monsters attack!",
        _ => null,
    };
}
