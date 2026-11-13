namespace FHeroes2.Core.Imaging;

/// <summary>
/// Cursor themes of the original engine, the equivalent of <c>Cursor::CursorType</c> plus
/// <c>Cursor::MapHeroCursor</c> (<c>src/fheroes2/gui/cursor.h</c>).
/// </summary>
/// <remarks>
/// The values are grouped by the source image set: <c>0x1000</c> adventure map,
/// <c>0x2000</c> combat, <c>0x3000</c> spells, <c>0x4000</c> adventure map with a distance digit.
/// The low byte is the sprite index inside that set (<c>theme &amp; 0xFF</c>) and the high nibble
/// selects the set itself, exactly like the bit tests in <c>Cursor::SetThemes()</c>.
/// </remarks>
public enum CursorTheme : int
{
    None = 0x0000,

    // ADVMCO.ICN
    Pointer = 0x1000,
    Wait = 0x1001,
    Heroes = 0x1002,
    Castle = 0x1003,

    // 0x1004 to 0x101F are not used by the original engine anymore.
    ScrollTop = 0x1020,
    ScrollTopRight = 0x1021,
    ScrollRight = 0x1022,
    ScrollBottomRight = 0x1023,
    ScrollBottom = 0x1024,
    ScrollBottomLeft = 0x1025,
    ScrollLeft = 0x1026,
    ScrollTopLeft = 0x1027,
    PointerVideo = 0x1028,

    // CMSECO.ICN
    CombatNone = 0x2000,
    CombatMove = 0x2001,
    CombatFly = 0x2002,
    CombatArrow = 0x2003,
    CombatHero = 0x2004,
    CombatInfo = 0x2005,
    CombatPointer = 0x2006,
    SwordTopRight = 0x2007,
    SwordRight = 0x2008,
    SwordBottomRight = 0x2009,
    SwordBottomLeft = 0x200A,
    SwordLeft = 0x200B,
    SwordTopLeft = 0x200C,
    SwordTop = 0x200D,
    SwordBottom = 0x200E,
    CombatBrokenArrow = 0x200F,

    // SPELCO.ICN
    SpellNone = CombatNone,
    SpellSlow = 0x3001,
    SpellUnknown = 0x3002,
    SpellCurse = 0x3003,
    SpellLightningBolt = 0x3004,
    SpellChainLightning = 0x3005,
    SpellCure = 0x3006,
    SpellBless = 0x3007,
    SpellFireball = 0x3008,
    SpellFireblast = 0x3009,
    SpellTeleport = 0x300A,
    SpellElementalStorm = 0x300B,
    SpellResurrectTrue = 0x300C,
    SpellResurrect = 0x300D,
    SpellHaste = 0x300E,
    SpellShield = 0x300F,
    SpellArmageddon = 0x3010,
    SpellAntiMagic = 0x3011,
    SpellDispel = 0x3012,
    SpellBerserker = 0x3013,
    SpellParalyze = 0x3014,
    SpellBlind = 0x3015,
    SpellHolyWord = 0x3016,
    SpellHolyShout = 0x3017,
    SpellMeteorShower = 0x3018,
    SpellAnimateDead = 0x3019,
    SpellMirrorImage = 0x301A,
    SpellBloodLust = 0x301B,
    SpellDeathRipple = 0x301C,
    SpellDeathWave = 0x301D,
    SpellSteelSkin = 0x301E,
    SpellStoneSkin = 0x301F,
    SpellDragonSlayer = 0x3020,
    SpellEarthquake = 0x3021,
    SpellDisruptingRay = 0x3022,
    SpellColdRing = 0x3023,
    SpellColdRay = 0x3024,
    SpellHypnotize = 0x3025,
    SpellArrow = 0x3026,

    // COLOR_CURSOR_ADVENTURE_MAP / MONO_CURSOR_ADVENTURE_MAP: the pointer plus a distance digit.
    HeroMove = 0x4000,
    HeroMove2 = 0x4001,
    HeroMove3 = 0x4002,
    HeroMove4 = 0x4003,
    HeroMove5 = 0x4004,
    HeroMove6 = 0x4005,
    HeroMove7 = 0x4006,
    HeroMove8 = 0x4007,
    HeroFight = 0x4008,
    HeroFight2 = 0x4009,
    HeroFight3 = 0x400A,
    HeroFight4 = 0x400B,
    HeroFight5 = 0x400C,
    HeroFight6 = 0x400D,
    HeroFight7 = 0x400E,
    HeroFight8 = 0x400F,
    HeroBoat = 0x4010,
    HeroBoat2 = 0x4011,
    HeroBoat3 = 0x4012,
    HeroBoat4 = 0x4013,
    HeroBoat5 = 0x4014,
    HeroBoat6 = 0x4015,
    HeroBoat7 = 0x4016,
    HeroBoat8 = 0x4017,
    HeroAnchor = 0x4018,
    HeroAnchor2 = 0x4019,
    HeroAnchor3 = 0x401A,
    HeroAnchor4 = 0x401B,
    HeroAnchor5 = 0x401C,
    HeroAnchor6 = 0x401D,
    HeroAnchor7 = 0x401E,
    HeroAnchor8 = 0x401F,
    HeroMeet = 0x4020,
    HeroMeet2 = 0x4021,
    HeroMeet3 = 0x4022,
    HeroMeet4 = 0x4023,
    HeroMeet5 = 0x4024,
    HeroMeet6 = 0x4025,
    HeroMeet7 = 0x4026,
    HeroMeet8 = 0x4027,
    HeroAction = 0x4028,
    HeroAction2 = 0x4029,
    HeroAction3 = 0x402A,
    HeroAction4 = 0x402B,
    HeroAction5 = 0x402C,
    HeroAction6 = 0x402D,
    HeroAction7 = 0x402E,
    HeroAction8 = 0x402F,
    HeroBoatAction = 0x4030,
    HeroBoatAction2 = 0x4031,
    HeroBoatAction3 = 0x4032,
    HeroBoatAction4 = 0x4033,
    HeroBoatAction5 = 0x4034,
    HeroBoatAction6 = 0x4035,
    HeroBoatAction7 = 0x4036,
    HeroBoatAction8 = 0x4037,
}

/// <summary>
/// The image set a cursor theme is drawn from, i.e. the <c>icnID</c> switch in
/// <c>Cursor::SetThemes()</c>. The monochrome variants come from the per sprite
/// <c>ADVMBWnn.BMP</c> / <c>SPELBWnn.BMP</c> / <c>CMSEBWnn.BMP</c> archive entries, the color ones
/// from <c>ADVMCO.ICN</c> / <c>SPELCO.ICN</c> / <c>CMSECO.ICN</c>.
/// </summary>
public enum CursorImageSet
{
    /// <summary>Color adventure map cursors (<c>ADVMCO.ICN</c>).</summary>
    AdventureMap,

    /// <summary>Monochrome adventure map cursors (<c>ADVMBWnn.BMP</c>).</summary>
    AdventureMapMonochrome,

    /// <summary>Color combat cursors (<c>CMSECO.ICN</c>).</summary>
    Combat,

    /// <summary>Monochrome combat cursors (<c>CMSEBWnn.BMP</c>).</summary>
    CombatMonochrome,

    /// <summary>Color spell cursors (<c>SPELCO.ICN</c>).</summary>
    Spell,

    /// <summary>Monochrome spell cursors (<c>SPELBWnn.BMP</c>).</summary>
    SpellMonochrome,

    /// <summary>
    /// Color adventure map cursors with a distance digit. The reference engine composes these at
    /// runtime from <c>ADVMCO.ICN</c> and procedurally drawn digits, so they are not a plain set.
    /// </summary>
    AdventureMapDistanceColor,

    /// <summary>Monochrome adventure map cursors with a distance digit.</summary>
    AdventureMapDistanceMonochrome,
}
