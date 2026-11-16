using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Icn;

/// <summary>
/// The lookups behind the <see cref="IcnType"/> ids, the free functions of namespace <c>ICN</c> in
/// <c>src/fheroes2/agg/icn.cpp</c>.
/// </summary>
/// <remarks>
/// 参考：<c>icn.h</c> 的 <c>getIcnFileName</c>/<c>getAnimatedIcnIndex</c>/<c>getHeroPortraitIcnId</c>/
/// <c>getCaptainIcnId</c>/<c>getBuildingIcnId</c>/<c>getCastleIcnId</c>/<c>getFlagIcnId</c>，实现分别在
/// <c>icn.cpp</c> 的 224/229/726/884/906/928/950 行。
/// 英雄头像那支（<c>getHeroPortraitIcnId</c>）依赖 <c>Heroes</c> 的 id，而 id 定义在游戏层的 <c>Hero</c> 里，
/// 所以它在 <c>FHeroes2.Game.IcnIds</c>，不在本类。
/// </remarks>
public static class IcnCatalog
{
    /// <summary>
    /// The archive entry name of an ICN, the reference's <c>ICN::getIcnFileName</c>.
    /// </summary>
    /// <param name="icnId">The ICN.</param>
    /// <returns>
    /// The file name, or <c>CUSTOM</c> for the ids at and above <see cref="IcnType.LastValidFileIcn"/>: those
    /// are images the engine draws at run time, not archive entries.
    /// </returns>
    /// <remarks>参考：<c>icn.cpp:224</c>——越界的 id 也返回 <c>CUSTOM</c>（参考的写法是同一条三元表达式）。</remarks>
    public static string GetFileName(IcnType icnId) =>
        icnId >= IcnType.Unknown && icnId < IcnType.LastValidFileIcn ? IcnFileNames.ById[(int)icnId] : "CUSTOM";

    /// <summary>
    /// The frame of an animated ICN that belongs at <paramref name="currentFrameNumber"/>, the reference's
    /// <c>ICN::getAnimatedIcnIndex</c>.
    /// </summary>
    /// <param name="icnId">The ICN that is being animated.</param>
    /// <param name="startIndex">Frame the animation starts at, which some entries offset from.</param>
    /// <param name="currentFrameNumber">Animation counter, which may run past the end of the cycle.</param>
    /// <param name="quantity">
    /// The reference's third argument: one entry (the magic garden) shows a full cycle only when the object
    /// has something to give, and a single frame otherwise.
    /// </param>
    /// <returns>The frame to draw, or 0 when the ICN has no animation.</returns>
    /// <remarks>
    /// 参考：<c>icn.cpp:229</c>。两百多行 <c>switch</c>，逐族逐地形列出「哪个 ICN、从第几帧起、几帧一循环」；
    /// 端口一一照搬，含 <c>OBJN*</c> 那些按 <paramref name="startIndex"/> 再分小段的条目（瓶子、沉船、海鸥、
    /// 漩涡、磨坊、火山……）。
    /// 差异：参考在三支 <c>X_LOC*</c> 里查 <c>Settings::Get().isPriceOfLoyaltySupported()</c>；这里改成参数，
    /// 因为 <c>Settings</c> 属于游戏层。
    /// </remarks>
    public static uint GetAnimatedIndex(IcnType icnId, uint startIndex, uint currentFrameNumber, bool quantity = false) =>
        GetAnimatedIndex(icnId, startIndex, currentFrameNumber, quantity, false);

    /// <inheritdoc cref="GetAnimatedIndex(IcnType,uint,uint,bool)"/>
    /// <param name="icnId">The ICN that is being animated.</param>
    /// <param name="startIndex">Frame the animation starts at.</param>
    /// <param name="currentFrameNumber">Animation counter.</param>
    /// <param name="quantity">Whether the animated object has something to give.</param>
    /// <param name="priceOfLoyaltySupported">
    /// What <c>Settings::Get().isPriceOfLoyaltySupported()</c> would answer: the three <c>X_LOC*</c> files are
    /// expansion data, so a Succession Wars installation has no frames to animate.
    /// </param>
    public static uint GetAnimatedIndex(IcnType icnId, uint startIndex, uint currentFrameNumber, bool quantity, bool priceOfLoyaltySupported)
    {
        switch (icnId)
        {
        case IcnType.Twnbboat:
        case IcnType.Twnkboat:
        case IcnType.Twnnboat:
        case IcnType.Twnsboat:
        case IcnType.Twnwboat:
        case IcnType.Twnzboat:
            return 1 + (currentFrameNumber % 9);
        case IcnType.Cmbtcapb:
        case IcnType.Cmbtcapk:
        case IcnType.Cmbtcapn:
        case IcnType.Cmbtcaps:
        case IcnType.Cmbtcapw:
        case IcnType.Cmbtcapz:
            return 1 + (currentFrameNumber % 10);
        case IcnType.Cmbthrob:
            return 1 + (currentFrameNumber % 18);
        case IcnType.Cmbthrok:
        case IcnType.Cmbthron:
            return 1 + (currentFrameNumber % 19);
        case IcnType.Cmbthros:
        case IcnType.Cmbthrow:
            return 1 + (currentFrameNumber % 16);
        case IcnType.Cmbthroz:
            return 1 + (currentFrameNumber % 18);
        case IcnType.Herofl00:
        case IcnType.Herofl01:
        case IcnType.Herofl02:
        case IcnType.Herofl03:
        case IcnType.Herofl04:
        case IcnType.Herofl05:
        case IcnType.Herofl06:
            return currentFrameNumber % 5;
        case IcnType.Twnbdock:
        case IcnType.Twnkdock:
        case IcnType.Twnndock:
        case IcnType.Twnsdock:
        case IcnType.Twnwdock:
        case IcnType.Twnzdock:
        case IcnType.Twnbext0:
        case IcnType.Twnkext0:
        case IcnType.Twnnext0:
        case IcnType.Twnsext0:
        case IcnType.Twnwext0:
        case IcnType.Twnzext0:
        case IcnType.Twnbcapt:
        case IcnType.Twnbdw3:
        case IcnType.Twnbdw4:
        case IcnType.Twnbdw5:
        case IcnType.Twnbext1:
        case IcnType.Twnbmoat:
        case IcnType.Twnbup3:
        case IcnType.Twnbup4:
        case IcnType.Twnkcstl:
        case IcnType.Twnkdw0:
        case IcnType.Twnkltur:
        case IcnType.Twnkrtur:
        case IcnType.Twnkthie:
        case IcnType.Twnktvrn:
        case IcnType.Twnncstl:
        case IcnType.Twnndw2:
        case IcnType.Twnnup2:
        case IcnType.Twnscapt:
        case IcnType.Twnscstl:
        case IcnType.Twnsdw0:
        case IcnType.Twnsdw1:
        case IcnType.Twnsext1:
        case IcnType.Twnsthie:
        case IcnType.Twnstvrn:
        case IcnType.Twnsup1:
        case IcnType.Twnswel2:
        case IcnType.Twnwcapt:
        case IcnType.Twnwcstl:
        case IcnType.Twnwmoat:
        case IcnType.Twnzcstl:
        case IcnType.Twnzdw0:
        case IcnType.Twnzdw2:
        case IcnType.Twnzthie:
        case IcnType.Twnzup2:
            return 1 + (currentFrameNumber % 5);
        case IcnType.Twnbcstl:
        case IcnType.Twnkdw2:
        case IcnType.Twnkup2:
        case IcnType.Twnndw5:
        case IcnType.Twnnwel2:
        case IcnType.Twnwdw0:
        case IcnType.Twnwwel2:
        case IcnType.Twnztvrn:
            return 1 + (currentFrameNumber % 6);
        case IcnType.Twnkdw4:
        case IcnType.Twnkup4:
            return 1 + (currentFrameNumber % 7);
        case IcnType.Tavwin:
            return 2 + (currentFrameNumber % 20);
        case IcnType.Cmbtlos1:
            return 1 + (currentFrameNumber % 30);
        case IcnType.Cmbtlos2:
            return 1 + (currentFrameNumber % 29);
        case IcnType.Cmbtlos3:
            return 1 + (currentFrameNumber % 22);
        case IcnType.Cmbtfle1:
            return 1 + (currentFrameNumber % 43);
        case IcnType.Cmbtfle2:
            return 1 + (currentFrameNumber % 26);
        case IcnType.Cmbtfle3:
            return 1 + (currentFrameNumber % 25);
        case IcnType.Cmbtsurr:
        case IcnType.Wincmbt:
            return 1 + (currentFrameNumber % 20);
        case IcnType.Minimon:
            return startIndex + 1 + (currentFrameNumber % 6);
        case IcnType.Twnnmage:
            return startIndex + 1 + (currentFrameNumber % 5);
        case IcnType.Twnbmage:
            return startIndex == 4 ? startIndex + 1 + (currentFrameNumber % 8) : 0;
        case IcnType.Shnganim:
            return 1 + (currentFrameNumber % 39);
        case IcnType.Btnshngl:
            return startIndex + (currentFrameNumber % 4);
        case IcnType.Objnhaun:
            return currentFrameNumber % 15;
        case IcnType.Objnwatr:
            switch (startIndex)
            {
            // bottle
            case 0x00:
                // 'startIndex' is equal to 0 here so it is not used in the expression.
                return (currentFrameNumber % 11) + 1;
            // shadow
            case 0x0C:
            // chest
            case 0x13:
            // shadow
            case 0x26:
            // flotsam
            case 0x2D:
            // Magellan's Maps building shadow.
            case 0x37:
            // Magellan's Maps building boat.
            case 0x3E:
            // Magellan's Maps building waves.
            case 0x45:
            // seaweed
            case 0x4C:
            case 0x53:
            case 0x5A:
            case 0x61:
            case 0x68:
            // sailor-man
            case 0x6F:
            // shadow
            case 0xBC:
            // buoy
            case 0xC3:
            // broken ship (right)
            case 0xE2:
            case 0xE9:
            case 0xF1:
            case 0xF8:
                return startIndex + (currentFrameNumber % 6) + 1;
            // seagull on stones
            case 0x76:
            case 0x86:
            case 0x96:
            case 0xA6:
                return startIndex + (currentFrameNumber % 15) + 1;
            // whirlpool
            case 0xCA:
            case 0xCE:
            case 0xD2:
            case 0xD6:
            case 0xDA:
            case 0xDE:
                return startIndex + (currentFrameNumber % 3) + 1;
            default:
                return 0;
            }
        case IcnType.Objnwat2:
            switch (startIndex)
            {
            // sail broken ship (left)
            case 0x03:
            case 0x0C:
                return startIndex + (currentFrameNumber % 6) + 1;
            default:
                return 0;
            }
        case IcnType.Objncrck:
            switch (startIndex)
            {
            // pool of oil
            case 0x50:
            case 0x5B:
            case 0x66:
            case 0x71:
            case 0x7C:
            case 0x89:
            case 0x94:
            case 0x9F:
            case 0xAA:
            // smoke from chimney
            case 0xBE:
            // shadow smoke
            case 0xCA:
                return startIndex + (currentFrameNumber % 10) + 1;
            default:
                return 0;
            }
        case IcnType.Objndirt:
            switch (startIndex)
            {
            // mill
            case 0x99:
            case 0x9D:
            case 0xA1:
            case 0xA5:
            case 0xA9:
            case 0xAD:
            case 0xB1:
            case 0xB5:
            case 0xB9:
            case 0xBD:
                return startIndex + (currentFrameNumber % 3) + 1;
            default:
                return 0;
            }
        case IcnType.Objndsrt:
            switch (startIndex)
            {
            // campfire
            case 0x36:
            case 0x3D:
                return startIndex + (currentFrameNumber % 6) + 1;
            default:
                return 0;
            }
        case IcnType.Objngra2:
            switch (startIndex)
            {
            // mill
            case 0x17:
            case 0x1B:
            case 0x1F:
            case 0x23:
            case 0x27:
            case 0x2B:
            case 0x2F:
            case 0x33:
            case 0x37:
            case 0x3B:
                return startIndex + (currentFrameNumber % 3) + 1;
            // smoke from chimney
            case 0x3F:
            case 0x46:
            case 0x4D:
            // Archer House
            case 0x54:
            // smoke from chimney
            case 0x5D:
            case 0x64:
            // shadow smoke
            case 0x6B:
            // Peasant Hut
            case 0x72:
                return startIndex + (currentFrameNumber % 6) + 1;
            default:
                return 0;
            }
        case IcnType.Objnlav2:
            switch (startIndex)
            {
            // middle volcano
            case 0x00:
            // shadow
            case 0x07:
            case 0x0E:
            // lava
            case 0x15:
                return startIndex + (currentFrameNumber % 6) + 1;
            // small volcano
            // shadow
            case 0x21:
            case 0x2C:
            // lava
            case 0x37:
            case 0x43:
                return startIndex + (currentFrameNumber % 10) + 1;
            default:
                return 0;
            }
        case IcnType.Objnlav3:
            // big volcano
            switch (startIndex)
            {
            // smoke
            case 0x00:
            case 0x0F:
            case 0x1E:
            case 0x2D:
            case 0x3C:
            case 0x4B:
            case 0x5A:
            case 0x69:
            case 0x87:
            case 0x96:
            case 0xA5:
            // shadow
            case 0x78:
            case 0xB4:
            case 0xC3:
            case 0xD2:
            case 0xE1:
                return startIndex + (currentFrameNumber % 14) + 1;
            default:
                return 0;
            }
        case IcnType.Objnlava:
            switch (startIndex)
            {
            // shadow of lava
            case 0x4E:
            case 0x58:
            case 0x62:
                return startIndex + (currentFrameNumber % 9) + 1;
            default:
                return 0;
            }
        case IcnType.Objnmul2:
            switch (startIndex)
            {
            // lighthouse
            case 0x3D:
                return startIndex + (currentFrameNumber % 9) + 1;
            // Alchemist Tower
            case 0x1B:
            // watermill
            case 0x53:
            case 0x5A:
            case 0x62:
            case 0x69:
            // fire in Wagon Camp
            case 0x81:
            // smoke smithy (2 chimney)
            case 0xA6:
            // smoke smithy (1 chimney)
            case 0xAD:
            // shadow smoke
            case 0xB4:
                return startIndex + (currentFrameNumber % 6) + 1;
            // magic garden
            case 0xBE:
                return quantity ? startIndex + (currentFrameNumber % 6) + 1 : startIndex + 7;
            default:
                return 0;
            }
        case IcnType.Objnmult:
            switch (startIndex)
            {
            // smoke
            case 0x05:
            // shadow
            case 0x0F:
            case 0x19:
                return startIndex + (currentFrameNumber % 9) + 1;
            // smoke
            case 0x24:
            // shadow
            case 0x2D:
                return startIndex + (currentFrameNumber % 8) + 1;
            // smoke
            case 0x5A:
            // shadow
            case 0x61:
            case 0x68:
            case 0x7C:
            // campfire
            case 0x83:
                return startIndex + (currentFrameNumber % 6) + 1;
            default:
                return 0;
            }
        case IcnType.Objnsnow:
            switch (startIndex)
            {
            // fire camp
            case 0x04:
            // Alchemist Tower
            case 0x97:
            // watermill
            case 0xA2:
            case 0xA9:
            case 0xB1:
            case 0xB8:
                return startIndex + (currentFrameNumber % 6) + 1;
            // mill
            case 0x60:
            case 0x64:
            case 0x68:
            case 0x6C:
            case 0x70:
            case 0x74:
            case 0x78:
            case 0x7C:
            case 0x80:
            case 0x84:
                return startIndex + (currentFrameNumber % 3) + 1;
            default:
                return 0;
            }
        case IcnType.Objnswmp:
            switch (startIndex)
            {
            // shadow
            case 0x00:
            case 0x0E:
            case 0x2B:
            // smoke
            case 0x07:
            case 0x22:
            case 0x33:
            // light in window
            case 0x16:
            case 0x3A:
            case 0x43:
            case 0x4A:
                return startIndex + (currentFrameNumber % 6) + 1;
            default:
                return 0;
            }
        // extra objects for loyalty version
        case IcnType.XLoc1:
            if (priceOfLoyaltySupported)
            {
                switch (startIndex)
                {
                // alchemist tower
                case 0x04:
                case 0x0D:
                case 0x16:
                // arena
                case 0x1F:
                case 0x28:
                case 0x32:
                case 0x3B:
                // earth altar
                case 0x55:
                case 0x5E:
                case 0x67:
                    return startIndex + (currentFrameNumber % 8) + 1;
                default:
                    return 0;
                }
            }

            break;
        // extra objects for loyalty version
        case IcnType.XLoc2:
            if (priceOfLoyaltySupported)
            {
                switch (startIndex)
                {
                // mermaid
                case 0x0A:
                case 0x13:
                case 0x1C:
                case 0x25:
                // sirens
                case 0x2F:
                case 0x38:
                case 0x41:
                case 0x4A:
                case 0x53:
                case 0x5C:
                case 0x66:
                    return startIndex + (currentFrameNumber % 8) + 1;
                default:
                    return 0;
                }
            }

            break;
        // extra objects for loyalty version
        case IcnType.XLoc3:
            if (priceOfLoyaltySupported)
            {
                switch (startIndex)
                {
                // hut magi
                case 0x00:
                case 0x0A:
                case 0x14:
                // eye magi
                case 0x20:
                case 0x29:
                case 0x32:
                    return startIndex + (currentFrameNumber % 8) + 1;
                // barrier
                case 0x3C:
                case 0x42:
                case 0x48:
                case 0x4E:
                case 0x54:
                case 0x5A:
                case 0x60:
                case 0x66:
                    return startIndex + (currentFrameNumber % 4) + 1;
                default:
                    return 0;
                }
            }

            break;
        default:
            break;
        }

        return 0;
    }

    /// <summary>
    /// The ICN that holds the captain portrait of a race, the reference's <c>ICN::getCaptainIcnId</c>.
    /// </summary>
    /// <param name="race">The race of the castle.</param>
    /// <returns>The ICN, or <see cref="IcnType.Unknown"/> when the race has no captain.</returns>
    /// <remarks>参考：<c>icn.cpp:884</c>。</remarks>
    public static IcnType GetCaptainIcnId(MapRace race) => race switch
    {
        MapRace.Barbarian => IcnType.Cstlcapb,
        MapRace.Knight => IcnType.Cstlcapk,
        MapRace.Necromancer => IcnType.Cstlcapn,
        MapRace.Sorceress => IcnType.Cstlcaps,
        MapRace.Warlock => IcnType.Cstlcapw,
        MapRace.Wizard => IcnType.Cstlcapz,
        _ => IcnType.Unknown,
    };

    /// <summary>
    /// The ICN that holds the standard castle building of a race, the reference's <c>ICN::getBuildingIcnId</c>.
    /// </summary>
    /// <param name="race">The race of the castle.</param>
    /// <returns>The ICN, or <see cref="IcnType.Unknown"/> when the race has none.</returns>
    /// <remarks>参考：<c>icn.cpp:906</c>。名字容易误会：它取的是塔（<c>CSTL*</c>）而不是「建筑」。</remarks>
    public static IcnType GetBuildingIcnId(MapRace race) => race switch
    {
        MapRace.Barbarian => IcnType.Cstlbarb,
        MapRace.Knight => IcnType.Cstlkngt,
        MapRace.Necromancer => IcnType.Cstlnecr,
        MapRace.Sorceress => IcnType.Cstlsorc,
        MapRace.Warlock => IcnType.Cstlwrlk,
        MapRace.Wizard => IcnType.Cstlwzrd,
        _ => IcnType.Unknown,
    };

    /// <summary>
    /// The ICN that holds the castle view of a race, the reference's <c>ICN::getCastleIcnId</c>.
    /// </summary>
    /// <param name="race">The race of the castle.</param>
    /// <returns>The ICN, or <see cref="IcnType.Unknown"/> when the race has none.</returns>
    /// <remarks>参考：<c>icn.cpp:928</c>。</remarks>
    public static IcnType GetCastleIcnId(MapRace race) => race switch
    {
        MapRace.Barbarian => IcnType.Castleb,
        MapRace.Knight => IcnType.Castlek,
        MapRace.Necromancer => IcnType.Castlen,
        MapRace.Sorceress => IcnType.Castles,
        MapRace.Warlock => IcnType.Castlew,
        MapRace.Wizard => IcnType.Castlez,
        _ => IcnType.Unknown,
    };

    /// <summary>
    /// The ICN that holds the waving hero flag of a color, the reference's <c>ICN::getFlagIcnId</c>.
    /// </summary>
    /// <param name="color">The color of the player.</param>
    /// <returns>The ICN; the reference maps everything it does not know (including none) to the neutral flag.</returns>
    /// <remarks>参考：<c>icn.cpp:950</c>——<c>default</c> 落到 <c>HEROFL06</c>（中立/灰），不是 <c>UNKNOWN</c>。</remarks>
    public static IcnType GetFlagIcnId(PlayerColor color) => color switch
    {
        PlayerColor.Blue => IcnType.Herofl00,
        PlayerColor.Green => IcnType.Herofl01,
        PlayerColor.Red => IcnType.Herofl02,
        PlayerColor.Yellow => IcnType.Herofl03,
        PlayerColor.Orange => IcnType.Herofl04,
        PlayerColor.Purple => IcnType.Herofl05,
        _ => IcnType.Herofl06,
    };
}
