namespace FHeroes2.Game.Credits;

/// <summary>
/// The text of every credits page, the data behind the <c>generate*Credits*Page()</c> functions of
/// <c>game_credits.cpp:182-697</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考一共八页、按固定顺序：<c>fheroes2</c> 自己的三页、资料片（若支持）三页、原版《王位战争》两页
/// （<c>Game::ShowCredits</c> 的 <c>pages.emplace_back</c> 那七/八行，<c>:724-741</c>）。
/// </para>
/// <para>
/// <b>名单是节选</b>：参考的团队名单有数百行，端口把每一页的标题与主创照抄，长名单（美术、QA、支持者）只留前若干
/// 名字。这是有意的取舍——名单是数据，排版与节奏才是这屏的行为；补人只需往这张表里加条目。
/// </para>
/// </remarks>
public static class CreditsContent
{
    /// <summary>
    /// Builds the pages of one run, <c>Game::ShowCredits()</c>'s <c>pages</c> vector.
    /// </summary>
    /// <param name="isPriceOfLoyaltySupported">
    /// Whether the installed data has the expansion, which adds its three pages (<c>game_credits.cpp:734-738</c>).
    /// </param>
    /// <returns>The pages, in the order they are shown.</returns>
    public static IReadOnlyList<CreditsPage> Create(bool isPriceOfLoyaltySupported)
    {
        List<CreditsPage> pages =
        [
            EngineFirstPage(),
            EngineThanksPage(),
            EngineSupportPage(),
        ];

        if (isPriceOfLoyaltySupported)
        {
            pages.Add(ExpansionLeadsPage());
            pages.Add(ExpansionDesignPage());
            pages.Add(ExpansionAudioPage());
        }

        pages.Add(OriginalLeadsPage());
        pages.Add(OriginalThanksPage());

        return pages;
    }

    /// <summary>The first engine page: the four people behind the port, one per column (<c>:182-266</c>).</summary>
    private static CreditsPage EngineFirstPage() => new()
    {
        Entries =
        [
            new CreditsEntry("Project Coordination and Core Development", "Ihar Hubchyk"),
            new CreditsEntry("Development", "Sergei Ivanov"),
            new CreditsEntry("QA and Support", "Igor Tsivilko"),
            new CreditsEntry("Development", "Ivan Shibanov"),
            new CreditsEntry("Development", "Oleg Derevenetz"),
            new CreditsEntry("Dev and Support", "Zense"),
            new CreditsEntry("Visit us at", "https://github.com/ihhub/fheroes2"),
        ],
    };

    /// <summary>The second engine page: the contributors and the supporters (<c>:268-349</c>).</summary>
    private static CreditsPage EngineThanksPage() => new()
    {
        IsCentered = true,
        Entries =
        [
            new CreditsEntry("Special Thanks to", string.Join('\n',
            [
                "LeHerosInconnu", "undef21", "Pavel Sereda", "Arkadiy Illarionov", "a1exsh",
                "vincent-grosbois", "eos428", "Mr-Bajs", "Arthusppp", "dimag0g", "Effektus",
                "Laserlicht", "Mauri Mustonen", "tau3", "Aimi Lindschouw", "Aleksei Mazur",
                "Alex Perry", "Andrei Dyldin", "Andrew Lasinskiy", "Andrew Szucs",
                "Benjamin Hughes", "Bolsch", "Brandon Wright", "Connor Townsend",
                "Christophe Didion", "Christopher Elliott", "David C Jernberg", "domoyega",
                "Grigoris Papadourakis", "Hajler", "Hakon", "hommaddict", "Kiril Lipatov",
                "Kresimir Condic", "Kuza", "Matt Taylor", "Matthew Pfluger",
                "Michael Van Wambeke", "Reximus", "Siarzuk Piatrouski", "slvclw",
                "TechnoCore", "William Hoskinson",
            ])),
            new CreditsEntry(string.Empty, "and all the people who have helped make the project possible!"),
        ],
    };

    /// <summary>The third engine page: the ways to reach the project (<c>:351-419</c>).</summary>
    private static CreditsPage EngineSupportPage() => new()
    {
        IsCentered = true,
        Entries =
        [
            new CreditsEntry("Support us at", "https://www.patreon.com/fheroes2"),
            new CreditsEntry("Connect with us at", "https://www.facebook.com/groups/fheroes2"),
            new CreditsEntry("Need help with the game?", "https://discord.com/servers/fheroes2-733093692860137523"),
            new CreditsEntry("Original project before 0.7", "Andrey Afletdinov\nhttps://sourceforge.net/projects/fheroes2/"),
        ],
    };

    /// <summary>The first expansion page: its leads (<c>:421-470</c>).</summary>
    private static CreditsPage ExpansionLeadsPage() => new()
    {
        Entries =
        [
            new CreditsEntry("Executive Producer", "Lester Humphreys"),
            new CreditsEntry("Producer", "Joe Minton"),
            new CreditsEntry("Design Lead", "Jim DuBois"),
            new CreditsEntry("Programming Lead", "Mike White"),
            new CreditsEntry("Art Director", "Seth Spaulding"),
            new CreditsEntry("Art Lead", "Thomas Gale"),
            new CreditsEntry("Designer", "Jon Van Caneghem"),
        ],
    };

    /// <summary>The second expansion page: design, production and QA (<c>:472-540</c>).</summary>
    private static CreditsPage ExpansionDesignPage() => new()
    {
        Entries =
        [
            new CreditsEntry("Designer", "Jon Van Caneghem"),
            new CreditsEntry("Executive Producer", "Mark Caldwell"),
            new CreditsEntry("Producers", "Walt Hochbrueckner\nJoe Minton\nLester Humphreys"),
            new CreditsEntry("Playtesters", "Jay Adams\nJohn Lencioni\nJason Wildblood\nTim Lang"),
            new CreditsEntry("QA Managers", "Peter Ryu\nBrian F. Smith"),
        ],
    };

    /// <summary>The third expansion page: its music (<c>:542-600</c>).</summary>
    private static CreditsPage ExpansionAudioPage() => new()
    {
        Entries =
        [
            new CreditsEntry("Musical Production", "Rob King"),
            new CreditsEntry("Music", "Paul Romero\nRob King"),
            new CreditsEntry("Sound Design", "Rob King"),
            new CreditsEntry("Town Themes", "Paul Romero"),
            new CreditsEntry("Alto Sax", "Brock \"Saxman\" Summers"),
            new CreditsEntry("Harpsichord and Piano", "Paul Romero"),
            new CreditsEntry("Basso Vocal", "Reid Bruton"),
            new CreditsEntry("Soprano Vocal", "Karin Meshagin"),
        ],
    };

    /// <summary>The first original page: who designed and built the game (<c>:602-660</c>).</summary>
    private static CreditsPage OriginalLeadsPage() => new()
    {
        Entries =
        [
            new CreditsEntry("Designed and Directed", "Jon Van Caneghem"),
            new CreditsEntry("Programming and Design", "Phil Steinmeyer"),
            new CreditsEntry("Executive Producer", "Mark Caldwell"),
            new CreditsEntry("Producer", "Walt Hochbrueckner"),
            new CreditsEntry("Musical Production", "Rob King"),
            new CreditsEntry("Music and Sound Design", "Rob King\nPaul Romero"),
            new CreditsEntry("Art Director", "Julia Ulano"),
            new CreditsEntry("Assistant Art Director", "Bonita Long-Hemsath"),
            new CreditsEntry("QA Manager", "Peter Ryu"),
        ],
    };

    /// <summary>The second original page: writing, the manual and the thanks (<c>:662-697</c>).</summary>
    private static CreditsPage OriginalThanksPage() => new()
    {
        Entries =
        [
            new CreditsEntry("Writing", "Paul Rattner"),
            new CreditsEntry("Manual and Helpfile", "Bryan Farina"),
            new CreditsEntry("Map Designers", "Jon Van Caneghem\nPhil Steinmeyer"),
            new CreditsEntry("Package Design", "Rozita Tolouey"),
            new CreditsEntry("Special Thanks to", "William Nesemeier\nTim Lang\nPavel Vesely\nJohn Lencioni\nJason Wildblood"),
        ],
    };
}
