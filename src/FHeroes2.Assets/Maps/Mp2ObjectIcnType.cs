namespace FHeroes2.Assets.Maps;

/// <summary>
/// The ICN tile set an object part belongs to, the port of <c>MP2::ObjectIcnType</c>
/// (<c>mp2.h</c>). The value is the top six bits of the <c>objectName</c> bytes of a tile or an
/// add-on. The order is the one of <c>getIcnIdFromObjectIcnType</c>, so the numeric values are part
/// of the map format and must not be renumbered.
/// </summary>
public enum Mp2ObjectIcnType : byte
{
    /// <summary>Nothing is drawn, <c>OBJ_ICN_TYPE_UNKNOWN</c>.</summary>
    Unknown = 0,

    Unused1 = 1,
    Unused2 = 2,
    Unused3 = 3,
    Unused4 = 4,
    Unused5 = 5,

    /// <summary><c>BOAT32.ICN</c>; boats are stored with this type by mistake of the original editor.</summary>
    Boat32 = 6,

    Unused7 = 7,
    Unused8 = 8,
    Unused9 = 9,

    /// <summary><c>OBJNHAUN.ICN</c>: the flying ghost over a mine.</summary>
    ObjnHaun = 10,

    /// <summary><c>OBJNARTI.ICN</c>: artifacts on the map.</summary>
    ObjnArti = 11,

    /// <summary><c>MON32.ICN</c> static monster; the engine draws <c>MINIMON.ICN</c> instead.</summary>
    Mons32 = 12,

    Unused13 = 13,

    /// <summary><c>FLAG32.ICN</c>: the flags of a castle.</summary>
    Flag32 = 14,

    Unused15 = 15,
    Unused16 = 16,
    Unused17 = 17,
    Unused18 = 18,
    Unused19 = 19,

    /// <summary><c>MINIMON.ICN</c>.</summary>
    MiniMon = 20,

    /// <summary><c>MINIHERO.ICN</c>: the heroes placed by the original editor.</summary>
    MiniHero = 21,

    MtnSnow = 22,
    MtnSwmp = 23,
    MtnLava = 24,
    MtnDsrt = 25,
    MtnDirt = 26,
    MtnMult = 27,
    Unused28 = 28,

    /// <summary><c>EXTRAOVR.ICN</c>: extra overlay of mines.</summary>
    ExtraOvr = 29,

    Road = 30,
    MtnCrck = 31,
    MtnGras = 32,
    TreJngl = 33,
    TreVil = 34,

    /// <summary><c>OBJNTOWN.ICN</c>: towns and castles.</summary>
    ObjnTown = 35,

    /// <summary><c>OBJNTWBA.ICN</c>: the basement of a town.</summary>
    ObjnTwba = 36,

    /// <summary><c>OBJNTWSH.ICN</c>: the shadows of a town.</summary>
    ObjnTwsh = 37,

    /// <summary><c>OBJNTWRD.ICN</c>: a random town.</summary>
    ObjnTwrd = 38,

    /// <summary><c>OBJNXTRA.ICN</c>: elementals guarding an object.</summary>
    ObjnXtra = 39,

    ObjnWat2 = 40,
    ObjnMul2 = 41,
    TreSnow = 42,
    TreFir = 43,
    TreFall = 44,
    Stream = 45,

    /// <summary><c>OBJNRSRC.ICN</c>: resource piles.</summary>
    ObjnRsrc = 46,

    Unused47 = 47,
    ObjnGra2 = 48,
    TreDeci = 49,
    ObjnWatr = 50,
    ObjnGras = 51,
    ObjnSnow = 52,
    ObjnSwmp = 53,
    ObjnLava = 54,
    ObjnDsrt = 55,
    ObjnDirt = 56,
    ObjnCrck = 57,
    ObjnLav3 = 58,

    /// <summary><c>OBJNMULT.ICN</c>: miscellaneous ground objects.</summary>
    ObjnMult = 59,

    ObjnLav2 = 60,

    /// <summary><c>X_LOC1.ICN</c>: a The Price of Loyalty object.</summary>
    XLoc1 = 61,

    /// <summary><c>X_LOC2.ICN</c>: a The Price of Loyalty object.</summary>
    XLoc2 = 62,

    /// <summary><c>X_LOC3.ICN</c>: a The Price of Loyalty object.</summary>
    XLoc3 = 63,
}

/// <summary>Helpers of <see cref="Mp2ObjectIcnType"/>.</summary>
public static class Mp2ObjectIcnTypes
{
    /// <summary>Whether the tile set draws anything at all.</summary>
    public static bool IsKnown(Mp2ObjectIcnType type) => type != Mp2ObjectIcnType.Unknown;

    /// <summary>
    /// Whether the sprite of a ground or top part can be blitted straight onto its tile, the port of
    /// <c>isObjectPartDirectRenderingRestricted</c> of the reference. The four excluded sets either
    /// hold nothing usable (<c>UNKNOWN</c>), are split over several tiles by the engine
    /// (<c>BOAT32</c>, <c>MINIHERO</c>) or are rendered by the monster renderer (<c>MONS32</c>).
    /// </summary>
    public static bool IsDrawnAsObjectPart(Mp2ObjectIcnType type) =>
        type is not (Mp2ObjectIcnType.Unknown
            or Mp2ObjectIcnType.Boat32
            or Mp2ObjectIcnType.MiniHero
            or Mp2ObjectIcnType.Mons32);

    /// <summary>
    /// The archive entry that holds the sprites of <paramref name="type"/>, i.e. the name behind
    /// <c>ICN::getIcnIdFromObjectIcnType</c> plus <c>ICN::GetString</c> of the reference. Every
    /// entry is the enum name in upper case with an <c>.ICN</c> suffix.
    /// </summary>
    /// <param name="type">Tile set of an object part.</param>
    /// <param name="priceOfLoyaltySupported">
    /// Whether <c>heroes2x.agg</c> is installed. The three <c>X_LOC*</c> sets only exist there, so
    /// without it they cannot be drawn (the reference returns <c>ICN::UNKNOWN</c>).
    /// </param>
    /// <returns><see langword="null"/> when the part draws nothing or its entry is missing.</returns>
    public static string? GetArchiveName(Mp2ObjectIcnType type, bool priceOfLoyaltySupported = false) =>
        type switch
        {
            Mp2ObjectIcnType.Unknown => null,
            Mp2ObjectIcnType.Boat32 => "BOAT32.ICN",
            Mp2ObjectIcnType.ObjnHaun => "OBJNHAUN.ICN",
            Mp2ObjectIcnType.ObjnArti => "OBJNARTI.ICN",
            Mp2ObjectIcnType.Mons32 => "MONS32.ICN",
            Mp2ObjectIcnType.Flag32 => "FLAG32.ICN",
            Mp2ObjectIcnType.MiniMon => "MINIMON.ICN",
            Mp2ObjectIcnType.MiniHero => "MINIHERO.ICN",
            Mp2ObjectIcnType.MtnSnow => "MTNSNOW.ICN",
            Mp2ObjectIcnType.MtnSwmp => "MTNSWMP.ICN",
            Mp2ObjectIcnType.MtnLava => "MTNLAVA.ICN",
            Mp2ObjectIcnType.MtnDsrt => "MTNDSRT.ICN",
            Mp2ObjectIcnType.MtnDirt => "MTNDIRT.ICN",
            Mp2ObjectIcnType.MtnMult => "MTNMULT.ICN",
            Mp2ObjectIcnType.ExtraOvr => "EXTRAOVR.ICN",
            Mp2ObjectIcnType.Road => "ROAD.ICN",
            Mp2ObjectIcnType.MtnCrck => "MTNCRCK.ICN",
            Mp2ObjectIcnType.MtnGras => "MTNGRAS.ICN",
            Mp2ObjectIcnType.TreJngl => "TREJNGL.ICN",
            Mp2ObjectIcnType.TreVil => "TREEVIL.ICN",
            Mp2ObjectIcnType.ObjnTown => "OBJNTOWN.ICN",
            Mp2ObjectIcnType.ObjnTwba => "OBJNTWBA.ICN",
            Mp2ObjectIcnType.ObjnTwsh => "OBJNTWSH.ICN",
            Mp2ObjectIcnType.ObjnTwrd => "OBJNTWRD.ICN",
            Mp2ObjectIcnType.ObjnXtra => "OBJNXTRA.ICN",
            Mp2ObjectIcnType.ObjnWat2 => "OBJNWAT2.ICN",
            Mp2ObjectIcnType.ObjnMul2 => "OBJNMUL2.ICN",
            Mp2ObjectIcnType.TreSnow => "TRESNOW.ICN",
            Mp2ObjectIcnType.TreFir => "TREFIR.ICN",
            Mp2ObjectIcnType.TreFall => "TREFALL.ICN",
            Mp2ObjectIcnType.Stream => "STREAM.ICN",
            Mp2ObjectIcnType.ObjnRsrc => "OBJNRSRC.ICN",
            Mp2ObjectIcnType.ObjnGra2 => "OBJNGRA2.ICN",
            Mp2ObjectIcnType.TreDeci => "TREDECI.ICN",
            Mp2ObjectIcnType.ObjnWatr => "OBJNWATR.ICN",
            Mp2ObjectIcnType.ObjnGras => "OBJNGRAS.ICN",
            Mp2ObjectIcnType.ObjnSnow => "OBJNSNOW.ICN",
            Mp2ObjectIcnType.ObjnSwmp => "OBJNSWMP.ICN",
            Mp2ObjectIcnType.ObjnLava => "OBJNLAVA.ICN",
            Mp2ObjectIcnType.ObjnDsrt => "OBJNDSRT.ICN",
            Mp2ObjectIcnType.ObjnDirt => "OBJNDIRT.ICN",
            Mp2ObjectIcnType.ObjnCrck => "OBJNCRCK.ICN",
            Mp2ObjectIcnType.ObjnLav3 => "OBJNLAV3.ICN",
            Mp2ObjectIcnType.ObjnMult => "OBJNMULT.ICN",
            Mp2ObjectIcnType.ObjnLav2 => "OBJNLAV2.ICN",
            Mp2ObjectIcnType.XLoc1 => priceOfLoyaltySupported ? "X_LOC1.ICN" : null,
            Mp2ObjectIcnType.XLoc2 => priceOfLoyaltySupported ? "X_LOC2.ICN" : null,
            Mp2ObjectIcnType.XLoc3 => priceOfLoyaltySupported ? "X_LOC3.ICN" : null,
            _ => null,
        };
}
