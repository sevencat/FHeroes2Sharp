using System.Buffers.Binary;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// The animation data of a creature as stored in its <c>.BIN</c> file, the reference's
/// <c>Bin_Info::MonsterAnimInfo</c> (<c>src/fheroes2/agg/bin_info.h:32</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：声明 <c>bin_info.h:32-96</c>（枚举 <c>ANIM_TYPE</c> 已单独搬到 <see cref="MonsterAnimId"/>），
/// 解析 <c>bin_info.cpp:88-491</c>，校验 <c>bin_info.cpp:493-507</c>，投射物序号 <c>bin_info.cpp:509-523</c>。
/// 一条记录定长 821 字节（<c>CORRECT_FRM_LENGTH</c>，<c>bin_info.cpp:80</c>），字段全按写死的字节偏移读；
/// 长度不对就整体作废，只留默认值。
/// </para>
/// <para>
/// 读完原始字节后参考还要做十余处修补：原版数据里狼的下方攻击帧、矮人的死亡动画、石像鬼/巨人/美杜莎的
/// 移动序列都有明显错误，升级兵种与基础兵种共用同一份 FRM 时速度要按速度差折算。这些修补逐条照搬，
/// 每条都注明参考行号。
/// </para>
/// <para>
/// 差异：参考的 <c>std::vector&lt;std::vector&lt;int&gt;&gt;</c> 在端口是 <c>int[][]</c>，而修补要
/// <c>resize</c>/<c>insert</c>/<c>erase</c>，数组不能原地伸缩，所以每个修补都换一个新数组赋回去。
/// 行数：<see cref="FrameXOffset"/> 只有前七支动画有（<c>MOVE_START</c> 到 <c>MOVE_ONE</c>），
/// <see cref="AnimationFrames"/> 是全部 33 支。
/// </para>
/// </remarks>
public sealed class MonsterAnimInfo
{
    /// <summary>Number of animation tracks a <c>.BIN</c> record holds, the reference's <c>SHOOT3_END + 1</c>.</summary>
    public const int AnimCount = (int)MonsterAnimId.Shoot3End + 1;

    /// <summary>Length of a correct <c>.BIN</c> record, <c>Bin_Info::CORRECT_FRM_LENGTH</c>.</summary>
    /// <remarks>参考：<c>bin_info.cpp:80</c>。</remarks>
    public const int CorrectFrameLength = 821;

    /// <summary>Number of animation columns in the header: <c>MOVE_START</c> through <c>MOVE_ONE</c>.</summary>
    private const int MoveRowCount = (int)MonsterAnimId.MoveOne + 1;

    /// <summary>Frames each header column holds, <c>bin_info.cpp:103</c>.</summary>
    private const int MoveRowFrames = 16;

    /// <summary>Highest frame count the reference accepts, <c>bin_info.cpp:239</c>.</summary>
    private const int MaxAnimationFrames = 16;

    /// <summary>Highest idle animation count the reference accepts, <c>bin_info.cpp:112</c>.</summary>
    private const int MaxIdleAnimationCount = 5;

    /// <summary>Highest projectile count the reference accepts, <c>bin_info.cpp:223</c>.</summary>
    private const int MaxProjectileCount = 12;

    /// <summary>Number of projectile start points, <c>bin_info.cpp:132</c>.</summary>
    private const int ProjectileOffsetCount = 3;

    // 参考：bin_info.cpp:84-86 的三个速度系数，以及战场格子宽度（Battle::Cell::widthPx，battle_cell.h）。
    private const double MoveSpeedUpgrade = 0.12;
    private const double ShootSpeedUpgrade = 0.08;
    private const double RangerShootSpeed = 0.78;
    private const int CellWidthPx = 44;

    /// <summary>Per-frame x offsets, one row per the first seven animations.</summary>
    /// <remarks>参考：<c>frameXOffset</c>，<c>bin_info.cpp:98-108</c>。</remarks>
    public int[][] FrameXOffset { get; private set; }

    /// <summary>Walking speed.</summary>
    public uint MoveSpeed { get; private set; }

    /// <summary>Ranged attack speed.</summary>
    public uint ShootSpeed { get; private set; }

    /// <summary>Flying speed.</summary>
    public uint FlightSpeed { get; private set; }

    /// <summary>The pixel the creature looks from, used to aim a projectile.</summary>
    public Point EyePosition { get; private set; }

    /// <summary>How far left the troop counter sits, in pixels.</summary>
    public int TroopCountOffsetLeft { get; private set; }

    /// <summary>How far right the troop counter sits, in pixels.</summary>
    public int TroopCountOffsetRight { get; private set; }

    /// <summary>Where a projectile leaves the creature, one point per shooting angle.</summary>
    public Point[] ProjectileOffset { get; private set; }

    /// <summary>Angles a projectile can leave at, in degrees.</summary>
    public float[] ProjectileAngles { get; private set; }

    /// <summary>Weight of each idle animation when the game picks one.</summary>
    public float[] IdlePriority { get; private set; }

    /// <summary>Delays of the idle animations the game never picks.</summary>
    public uint[] UnusedIdleDelays { get; private set; }

    /// <summary>How many idle animations the creature has.</summary>
    public uint IdleAnimationCount { get; private set; }

    /// <summary>Delay between idle animations, in milliseconds.</summary>
    public uint IdleAnimationDelay { get; private set; }

    /// <summary>Frame numbers per animation, one row per <see cref="MonsterAnimId"/>.</summary>
    public int[][] AnimationFrames { get; private set; }

    /// <summary>
    /// Reads one record and applies the reference's corrections to the original data.
    /// </summary>
    /// <param name="monsterId">The creature the bytes belong to; the corrections are per creature.</param>
    /// <param name="bytes">The contents of the <c>.BIN</c> file; anything but 821 bytes yields an empty record.</param>
    /// <remarks>参考：<c>bin_info.cpp:88</c>。</remarks>
    public MonsterAnimInfo(MonsterType monsterId = MonsterType.Unknown, ReadOnlySpan<byte> bytes = default)
    {
        // 参考：bin_info.h:73 的默认初始化（moveSpeed{ 450 }），长度不对时保留下来的就是这些值。
        MoveSpeed = 450;
        FrameXOffset = [];
        AnimationFrames = [];
        ProjectileOffset = [];
        ProjectileAngles = [];
        IdlePriority = [];
        UnusedIdleDelays = [];

        if (bytes.Length != CorrectFrameLength)
        {
            return;
        }

        // 参考：bin_info.cpp:96，视线落点。
        EyePosition = new Point(ReadInt16(bytes, 1), ReadInt16(bytes, 3));

        // 参考：bin_info.cpp:98-108，七列各 16 帧的 x 偏移。
        FrameXOffset = new int[MoveRowCount][];

        for (int moveId = 0; moveId < MoveRowCount; ++moveId)
        {
            int[] row = new int[MoveRowFrames];
            for (int frame = 0; frame < MoveRowFrames; ++frame)
            {
                row[frame] = (sbyte)bytes[5 + (moveId * MoveRowFrames) + frame];
            }

            FrameXOffset[moveId] = row;
        }

        // 参考：bin_info.cpp:110-124，待机动画的条数、权重与延迟。
        uint idleCount = bytes[117];
        IdleAnimationCount = idleCount > MaxIdleAnimationCount ? MaxIdleAnimationCount : idleCount;

        IdlePriority = new float[IdleAnimationCount];
        for (int i = 0; i < IdlePriority.Length; ++i)
        {
            IdlePriority[i] = ReadSingle(bytes, 118 + (i * 4));
        }

        UnusedIdleDelays = new uint[IdleAnimationCount];
        for (int i = 0; i < UnusedIdleDelays.Length; ++i)
        {
            UnusedIdleDelays[i] = ReadUInt32(bytes, 138 + (i * 4));
        }

        IdleAnimationDelay = ReadUInt32(bytes, 158);

        // 参考：bin_info.cpp:126-129，三支速度。
        MoveSpeed = ReadUInt32(bytes, 162);
        ShootSpeed = ReadUInt32(bytes, 166);
        FlightSpeed = ReadUInt32(bytes, 170);

        // 参考：bin_info.cpp:131-134，三个投射物起点。
        ProjectileOffset = new Point[ProjectileOffsetCount];
        for (int i = 0; i < ProjectileOffsetCount; ++i)
        {
            ProjectileOffset[i] = new Point(ReadInt16(bytes, 174 + (i * 4)), ReadInt16(bytes, 176 + (i * 4)));
        }

        // 参考：bin_info.cpp:136-220，按兵种修正投射物起点。
        FixProjectileOffsets(monsterId);

        int projectileCount = bytes[186];
        if (projectileCount > MaxProjectileCount)
        {
            projectileCount = MaxProjectileCount;
        }

        ProjectileAngles = new float[projectileCount];
        for (int i = 0; i < ProjectileAngles.Length; ++i)
        {
            ProjectileAngles[i] = ReadSingle(bytes, 187 + (i * 4));
        }

        // 参考：bin_info.cpp:231-233，兵数计数器的左右偏移。
        TroopCountOffsetLeft = ReadInt32(bytes, 235);
        TroopCountOffsetRight = ReadInt32(bytes, 239);

        // 参考：bin_info.cpp:235-248，33 支动画各自的帧数与帧号。
        AnimationFrames = new int[AnimCount][];

        for (int index = 0; index < AnimCount; ++index)
        {
            int count = bytes[243 + index];
            if (count > MaxAnimationFrames)
            {
                count = MaxAnimationFrames;
            }

            int[] frames = new int[count];
            for (int frame = 0; frame < count; ++frame)
            {
                frames[frame] = bytes[277 + (index * MaxAnimationFrames) + frame];
            }

            AnimationFrames[index] = frames;
        }

        // 参考：bin_info.cpp:250-266，原版数据的动画帧错误。
        FixAnimationFrames(monsterId);

        // 参考：bin_info.cpp:268-309，升级兵种共用 FRM 时按速度差折算。
        ApplySpeedUpgrade(monsterId);

        // 参考：bin_info.cpp:311-490，移动动画的偏移与帧序修补。
        FixMovementOffsets(monsterId);
    }

    /// <summary>Whether the record holds a playable animation of the given track.</summary>
    /// <remarks>参考：<c>bin_info.h:89</c> 的 <c>hasAnim</c>。</remarks>
    public bool HasAnim(MonsterAnimId animId = MonsterAnimId.Static)
    {
        int index = (int)animId;
        return index < AnimationFrames.Length && AnimationFrames[index].Length != 0;
    }

    /// <summary>
    /// Whether the record carries the animations every creature needs. A record that fails this is
    /// discarded by the loader, which is how the reference treats a broken <c>.BIN</c>.
    /// </summary>
    /// <remarks>参考：<c>bin_info.cpp:493-507</c>。</remarks>
    public bool IsValid()
    {
        if (AnimationFrames.Length != AnimCount)
        {
            return false;
        }

        // 参考：最小动画集合——移动、站立、死亡、受击与三个方向的攻击。
        ReadOnlySpan<MonsterAnimId> required =
        [
            MonsterAnimId.MoveMain,
            MonsterAnimId.Static,
            MonsterAnimId.Death,
            MonsterAnimId.WinceUp,
            MonsterAnimId.Attack1,
            MonsterAnimId.Attack2,
            MonsterAnimId.Attack3,
        ];

        foreach (MonsterAnimId animId in required)
        {
            if (AnimationFrames[(int)animId].Length == 0)
            {
                return false;
            }
        }

        return IdlePriority.Length == IdleAnimationCount;
    }

    /// <summary>
    /// The index of the projectile start point to use for a shot leaving at <paramref name="angle"/>.
    /// </summary>
    /// <remarks>参考：<c>bin_info.cpp:509-523</c>——取相邻两个角度的中点作为分界。</remarks>
    public int GetProjectileId(double angle)
    {
        if (ProjectileAngles.Length == 0)
        {
            return 0;
        }

        for (int id = 0; id < ProjectileAngles.Length - 1; ++id)
        {
            if (angle >= (ProjectileAngles[id] + ProjectileAngles[id + 1]) / 2.0)
            {
                return id;
            }
        }

        return ProjectileAngles.Length - 1;
    }

    /// <summary>Corrections to the projectile start points, per creature.</summary>
    /// <remarks>参考：<c>bin_info.cpp:136-220</c>——位置改成射击动画最后一帧的位置。</remarks>
    private void FixProjectileOffsets(MonsterType monsterId)
    {
        switch (monsterId)
        {
        case MonsterType.Archer:
        case MonsterType.Ranger:
            ProjectileOffset[0] = ProjectileOffset[0].Offset(-30, 5);
            ProjectileOffset[1] = ProjectileOffset[1].Offset(-46, -5);
            ProjectileOffset[2] = ProjectileOffset[2].Offset(-30, -40);
            break;

        case MonsterType.Orc:
        case MonsterType.OrcChief:
            for (int i = 0; i < ProjectileOffset.Length; ++i)
            {
                ProjectileOffset[i] = new Point(ProjectileOffset[i].X - 20, -36);
            }

            break;

        case MonsterType.Troll:
        case MonsterType.WarTroll:
            ProjectileOffset[0] = ProjectileOffset[0].Offset(-30, -5);
            ProjectileOffset[1] = ProjectileOffset[1].Offset(-14, -1);
            ProjectileOffset[2] = ProjectileOffset[2].Offset(-15, -19);
            break;

        case MonsterType.Elf:
        case MonsterType.GrandElf:
            // 参考：BIN 里精灵与高级精灵下方射击的起点 y 是错的（-1），所以整值重写。
            ProjectileOffset[0] = ProjectileOffset[0].Offset(-19, 22);
            ProjectileOffset[1] = ProjectileOffset[1].Offset(-40, -1);
            ProjectileOffset[2] = new Point(ProjectileOffset[2].X - 19, -54);
            break;

        case MonsterType.Druid:
        case MonsterType.GreaterDruid:
            ProjectileOffset[0] = ProjectileOffset[0].Offset(-12, 23);
            ProjectileOffset[1] = ProjectileOffset[1].Offset(-20, 0);
            ProjectileOffset[2] = ProjectileOffset[2].Offset(-7, -21);
            break;

        case MonsterType.Centaur:
            ProjectileOffset[0] = ProjectileOffset[0].Offset(-24, 31);
            ProjectileOffset[1] = ProjectileOffset[1].Offset(-32, 2);
            ProjectileOffset[2] = ProjectileOffset[2].Offset(-24, -27);
            break;

        case MonsterType.Halfling:
            ProjectileOffset[0] = ProjectileOffset[0].Offset(-7, 11);
            ProjectileOffset[1] = ProjectileOffset[1].Offset(-21, 10);
            ProjectileOffset[2] = ProjectileOffset[2].Offset(-9, -14);
            break;

        case MonsterType.Mage:
        case MonsterType.Archmage:
            ProjectileOffset[0] = ProjectileOffset[0].Offset(0, -2);
            ProjectileOffset[2] = ProjectileOffset[2].Offset(0, 2);
            break;

        case MonsterType.Titan:
            ProjectileOffset[0] = ProjectileOffset[0].Offset(-10, 22);
            ProjectileOffset[1] = ProjectileOffset[1].Offset(-20, 8);
            ProjectileOffset[2] = ProjectileOffset[2].Offset(-15, -22);
            break;

        case MonsterType.Lich:
        case MonsterType.PowerLich:
            ProjectileOffset[0] = ProjectileOffset[0].Offset(-5, 6);
            ProjectileOffset[1] = ProjectileOffset[1].Offset(-16, -9);
            ProjectileOffset[2] = ProjectileOffset[2].Offset(-9, -7);
            break;

        default:
            break;
        }
    }

    /// <summary>Corrections to the frame numbers, per creature.</summary>
    /// <remarks>参考：<c>bin_info.cpp:250-266</c>。</remarks>
    private void FixAnimationFrames(MonsterType monsterId)
    {
        if (monsterId == MonsterType.Wolf)
        {
            // 参考：狼的下方攻击用了错误的一帧（16 应为 2）。
            int[] attack3 = AnimationFrames[(int)MonsterAnimId.Attack3];
            if (attack3.Length == 3 && attack3[0] == 16)
            {
                attack3[0] = 2;
            }

            int[] attack3End = AnimationFrames[(int)MonsterAnimId.Attack3End];
            if (attack3End.Length == 3 && attack3End[2] == 16)
            {
                attack3End[2] = 2;
            }
        }

        if (monsterId is MonsterType.Dwarf or MonsterType.BattleDwarf)
        {
            // 参考：矮人的死亡动画在 BIN 里是错的，整支换成 49…55 共七帧。
            int[] death = AnimationFrames[(int)MonsterAnimId.Death];
            if (death.Length == 8)
            {
                int[] corrected = new int[7];
                for (int i = 0; i < corrected.Length; ++i)
                {
                    corrected[i] = 49 + i;
                }

                AnimationFrames[(int)MonsterAnimId.Death] = corrected;
            }
        }
    }

    /// <summary>Folds the speed difference of an upgraded creature into the animation speeds.</summary>
    /// <remarks>参考：<c>bin_info.cpp:268-309</c>。</remarks>
    private void ApplySpeedUpgrade(MonsterType monsterId)
    {
        int speedDiff = SpeedDifference(monsterId);
        if (speedDiff == 0)
        {
            return;
        }

        MoveSpeed = (uint)((1 - (MoveSpeedUpgrade * speedDiff)) * MoveSpeed);

        // 参考：游侠升级后多一次攻击，所以射击速度单独用一个系数。
        ShootSpeed = monsterId == MonsterType.Ranger
            ? (uint)(ShootSpeed * RangerShootSpeed)
            : (uint)((1 - (ShootSpeedUpgrade * speedDiff)) * ShootSpeed);
    }

    /// <summary>
    /// How much faster an upgraded creature is than the creature whose FRM file it shares.
    /// </summary>
    /// <remarks>参考：<c>bin_info.cpp:269-298</c>——升级兵种与「前一个兵种」比，元素与火元素比。</remarks>
    private static int SpeedDifference(MonsterType monsterId)
    {
        switch (monsterId)
        {
        case MonsterType.Ranger:
        case MonsterType.VeteranPikeman:
        case MonsterType.MasterSwordsman:
        case MonsterType.Champion:
        case MonsterType.Crusader:
        case MonsterType.OrcChief:
        case MonsterType.OgreLord:
        case MonsterType.WarTroll:
        case MonsterType.BattleDwarf:
        case MonsterType.GrandElf:
        case MonsterType.GreaterDruid:
        case MonsterType.MinotaurKing:
        case MonsterType.SteelGolem:
        case MonsterType.Archmage:
        case MonsterType.MutantZombie:
        case MonsterType.RoyalMummy:
        case MonsterType.VampireLord:
        case MonsterType.PowerLich:
            return (int)MonsterStatistics.GetBattleStats(monsterId).Speed
                   - (int)MonsterStatistics.GetBattleStats((MonsterType)((int)monsterId - 1)).Speed;

        case MonsterType.EarthElement:
        case MonsterType.AirElement:
        case MonsterType.WaterElement:
            return (int)MonsterStatistics.GetBattleStats(monsterId).Speed
                   - (int)MonsterStatistics.GetBattleStats(MonsterType.FireElement).Speed;

        default:
            return 0;
        }
    }

    /// <summary>The reference's fixes to the x offsets and lengths of the moving animations.</summary>
    /// <remarks>参考：<c>bin_info.cpp:311-490</c>。</remarks>
    private void FixMovementOffsets(MonsterType monsterId)
    {
        int moveStart = (int)MonsterAnimId.MoveStart;
        int moveTileStart = (int)MonsterAnimId.MoveTileStart;
        int moveMain = (int)MonsterAnimId.MoveMain;
        int moveTileEnd = (int)MonsterAnimId.MoveTileEnd;
        int moveStop = (int)MonsterAnimId.MoveStop;
        int moveOne = (int)MonsterAnimId.MoveOne;

        // 参考：bin_info.cpp:311-313，落地动画的第一帧偏移沿用「一格的结尾」。
        if (FrameXOffset[moveStop][0] == 0 && FrameXOffset[moveTileEnd][0] != 0)
        {
            FrameXOffset[moveStop][0] = FrameXOffset[moveTileEnd][0];
        }

        // 参考：bin_info.cpp:315-317，把前七支动画的偏移行对齐到各自的帧数。
        for (int index = moveStart; index <= moveOne; ++index)
        {
            FrameXOffset[index] = WithLength(FrameXOffset[index], AnimationFrames[index].Length);
        }

        // 参考：bin_info.cpp:319-329，落地只有一帧且偏移为 0 时按相邻动画推算。
        if (FrameXOffset[moveStop].Length == 1 && FrameXOffset[moveStop][0] == 0)
        {
            if (FrameXOffset[moveTileEnd].Length == 1 && FrameXOffset[moveTileEnd][0] != 0)
            {
                FrameXOffset[moveStop][0] = FrameXOffset[moveTileEnd][0];
            }
            else if (FrameXOffset[moveTileStart].Length == 1 && FrameXOffset[moveTileStart][0] != 0)
            {
                FrameXOffset[moveStop][0] = CellWidthPx + FrameXOffset[moveTileStart][0];
            }
            else if (FrameXOffset[moveMain].Length != 0)
            {
                FrameXOffset[moveStop][0] = FrameXOffset[moveMain][^1];
            }
        }

        FixGolemMovement(monsterId, moveStart, moveTileStart, moveMain, moveOne);

        // 参考：bin_info.cpp:362-366，食人魔的移动序列开头有一段与站立重复的帧。
        if (monsterId is MonsterType.Ogre or MonsterType.OgreLord
            && FrameXOffset[moveMain].Length == 9 && AnimationFrames[moveMain].Length == 9)
        {
            AnimationFrames[moveMain] = AnimationFrames[moveMain][1..];
            FrameXOffset[moveMain] = FrameXOffset[moveMain][1..];
        }

        // 参考：bin_info.cpp:368-377，这些兵种移动时整体右移 3 像素，否则在井里与斜向移动时会抖。
        if (monsterId is MonsterType.Orc or MonsterType.OrcChief or MonsterType.Ogre or MonsterType.OgreLord
            or MonsterType.Dwarf or MonsterType.BattleDwarf or MonsterType.Unicorn or MonsterType.Centaur
            or MonsterType.Boar or MonsterType.Lich or MonsterType.PowerLich or MonsterType.Rogue)
        {
            for (int index = moveStart; index <= moveOne; ++index)
            {
                AddAll(FrameXOffset[index], 3);
            }
        }

        // 参考：bin_info.cpp:379-386，弓箭手与游侠的移动整体右移 1 像素。
        if (monsterId is MonsterType.Archer or MonsterType.Ranger)
        {
            AddAll(FrameXOffset[moveMain], 1);
            AddAll(FrameXOffset[moveOne], 1);
        }

        // 参考：bin_info.cpp:388-399，圣骑士与十字军整体左移 1 像素，第 5、8 帧再各多移一点。
        // 差异：参考在这条分支里直接写 frameXOffset[MOVE_ONE][7]，数据比 8 帧短时会越界，这里先挡掉。
        if (monsterId is MonsterType.Paladin or MonsterType.Crusader
            && FrameXOffset[moveMain].Length == 8 && AnimationFrames[moveMain].Length == 8
            && FrameXOffset[moveOne].Length == 8)
        {
            foreach (int index in (ReadOnlySpan<int>)[moveMain, moveOne])
            {
                AddAll(FrameXOffset[index], -1);
                AddAt(FrameXOffset[index], 4, -2);
                AddAt(FrameXOffset[index], 7, -1);
            }
        }

        // 参考：bin_info.cpp:401-408，哥布林整体右移 6 像素。
        if (monsterId == MonsterType.Goblin)
        {
            foreach (int index in (ReadOnlySpan<int>)[moveTileStart, moveMain, moveStop, moveOne])
            {
                AddAll(FrameXOffset[index], 6);
            }
        }

        // 参考：bin_info.cpp:410-421，巨魔整体右移 2 像素，第 7、14 帧再多 1 像素。
        if (monsterId is MonsterType.Troll or MonsterType.WarTroll
            && FrameXOffset[moveMain].Length == 14 && FrameXOffset[moveOne].Length == 14)
        {
            foreach (int index in (ReadOnlySpan<int>)[moveMain, moveOne])
            {
                AddAll(FrameXOffset[index], 2);
                AddAt(FrameXOffset[index], 6, 1);
                AddAt(FrameXOffset[index], 13, 1);
            }
        }

        // 参考：bin_info.cpp:423-432，巨人/泰坦的逐帧修正。
        if (monsterId is MonsterType.Giant or MonsterType.Titan
            && FrameXOffset[moveMain].Length == 7 && FrameXOffset[moveOne].Length == 7)
        {
            foreach (int index in (ReadOnlySpan<int>)[moveMain, moveOne])
            {
                AddAt(FrameXOffset[index], 0, 3);
                AddAt(FrameXOffset[index], 1, 2);
                AddAt(FrameXOffset[index], 2, 2);
                AddAt(FrameXOffset[index], 5, 1);
                AddAt(FrameXOffset[index], 6, 1);
            }
        }

        // 参考：bin_info.cpp:434-439，游牧民的第三帧左移 2 像素。
        if (monsterId == MonsterType.Nomad
            && FrameXOffset[moveMain].Length == 8 && FrameXOffset[moveOne].Length == 8)
        {
            AddAt(FrameXOffset[moveMain], 2, -2);
            AddAt(FrameXOffset[moveOne], 2, -2);
        }

        // 参考：bin_info.cpp:441-452，美杜莎的逐帧修正。
        if (monsterId == MonsterType.Medusa
            && FrameXOffset[moveMain].Length == 15 && FrameXOffset[moveOne].Length == 15)
        {
            foreach (int index in (ReadOnlySpan<int>)[moveMain, moveOne])
            {
                AddAt(FrameXOffset[index], 3, 2);
                AddAt(FrameXOffset[index], 4, 2);
                AddAt(FrameXOffset[index], 5, 1);
                AddAt(FrameXOffset[index], 9, 1);
                AddAt(FrameXOffset[index], 10, 2);
                AddAt(FrameXOffset[index], 11, 1);
                AddAt(FrameXOffset[index], 14, 1);
            }
        }

        // 参考：bin_info.cpp:454-466，四系元素的逐帧修正。
        if (monsterId is MonsterType.EarthElement or MonsterType.AirElement or MonsterType.FireElement or MonsterType.WaterElement
            && FrameXOffset[moveMain].Length == 8 && FrameXOffset[moveOne].Length == 8)
        {
            foreach (int index in (ReadOnlySpan<int>)[moveMain, moveOne])
            {
                AddAt(FrameXOffset[index], 0, 3);
                AddAt(FrameXOffset[index], 1, 3);
                AddAt(FrameXOffset[index], 2, 5);
                AddAt(FrameXOffset[index], 3, 3);
                AddAt(FrameXOffset[index], 5, -1);
                AddAt(FrameXOffset[index], 6, 1);
                AddAt(FrameXOffset[index], 7, -1);
            }
        }

        // 参考：bin_info.cpp:468-490，剑士与剑术大师的偏移与补帧。
        if (monsterId is MonsterType.Swordsman or MonsterType.MasterSwordsman)
        {
            if (FrameXOffset[moveStart].Length == 2 && FrameXOffset[moveStop].Length == 1)
            {
                FrameXOffset[moveStart][0] = 0;
                FrameXOffset[moveStart][1] = CellWidthPx / 8;
                AddAll(FrameXOffset[moveMain], (CellWidthPx / 4) + 3);
                FrameXOffset[moveStop][0] = CellWidthPx;
            }

            if (AnimationFrames[moveMain].Length == 7 && FrameXOffset[moveMain].Length == 7)
            {
                AnimationFrames[moveMain] = InsertedAt(AnimationFrames[moveMain], 0, 45);
                FrameXOffset[moveMain] = InsertedAt(FrameXOffset[moveMain], 0, 8);
            }

            if (AnimationFrames[moveOne].Length == 10 && FrameXOffset[moveOne].Length == 10)
            {
                AnimationFrames[moveOne] = InsertedAt(AnimationFrames[moveOne], 2, 45);
                FrameXOffset[moveOne] = InsertedAt(FrameXOffset[moveOne], 2, 8);
            }
        }
    }

    /// <summary>
    /// The Iron and Steel Golem movement fix: the original data splits the sequence wrongly and misses the
    /// frames the reference adds to the ICN at load time.
    /// </summary>
    /// <remarks>参考：<c>bin_info.cpp:331-360</c>。</remarks>
    private void FixGolemMovement(MonsterType monsterId, int moveStart, int moveTileStart, int moveMain, int moveOne)
    {
        if (monsterId is not (MonsterType.IronGolem or MonsterType.SteelGolem)
            || FrameXOffset[moveStart].Length != 4 || FrameXOffset[moveMain].Length != 7
            || AnimationFrames[moveMain].Length != 7)
        {
            return;
        }

        // 参考：补上 'MOVE_START' 前四帧的 x 偏移。
        FrameXOffset[moveStart][0] = 0;
        FrameXOffset[moveStart][1] = CellWidthPx * 1 / 8;
        FrameXOffset[moveStart][2] = (CellWidthPx * 2 / 8) + 3;
        FrameXOffset[moveStart][3] = CellWidthPx * 3 / 8;

        // 参考：'MOVE_MAIN' 少了动画开头四分之一。'MOVE_START' 有这段，而 'MOVE_TILE_START' 是空的，
        // 于是把 'MOVE_MAIN' 除最后一帧以外的帧整体搬到 'MOVE_START' 末尾，'MOVE_TILE_START' 换上新帧号。
        AnimationFrames[moveTileStart] =
            WithLength(AnimationFrames[moveTileStart], AnimationFrames[moveTileStart].Length + AnimationFrames[moveMain].Length - 1);
        AnimationFrames[moveStart] = Appended(AnimationFrames[moveStart], AnimationFrames[moveMain][..^1]);
        AnimationFrames[moveMain] = AnimationFrames[moveMain][^1..];

        FrameXOffset[moveStart] = Appended(FrameXOffset[moveStart], FrameXOffset[moveMain][..^1]);
        FrameXOffset[moveTileStart] = FrameXOffset[moveMain][..^1];
        FrameXOffset[moveMain] = FrameXOffset[moveMain][^1..];

        // 参考：坐标原点差半个格子。
        FrameXOffset[moveMain][0] += CellWidthPx / 2;

        for (int id = 0; id < FrameXOffset[moveTileStart].Length; ++id)
        {
            FrameXOffset[moveStart][id + 4] += CellWidthPx / 2;

            // 'MOVE_TILE_START' 还要叠加 "agg_image.cpp" 里加的修正。
            FrameXOffset[moveTileStart][id] += (CellWidthPx / 2) - (((6 - id) * CellWidthPx) / 28);

            // 'MOVE_TILE_START' 用 "agg_image.cpp" 新造的帧，帧号从 40 起。
            AnimationFrames[moveTileStart][id] = 40 + id;
        }
    }

    /// <summary>A copy of <paramref name="values"/> with the given length, padded with zeros or truncated.</summary>
    private static int[] WithLength(int[] values, int length)
    {
        if (values.Length == length)
        {
            return values;
        }

        int[] result = new int[length];
        Array.Copy(values, result, Math.Min(values.Length, length));
        return result;
    }

    /// <summary><paramref name="values"/> followed by <paramref name="tail"/>.</summary>
    private static int[] Appended(int[] values, int[] tail) => [.. values, .. tail];

    /// <summary>A new array holding <paramref name="value"/> at <paramref name="index"/>.</summary>
    private static int[] InsertedAt(int[] values, int index, int value) =>
        [.. values[..index], value, .. values[index..]];

    /// <summary>Adds <paramref name="delta"/> to every element of a row.</summary>
    private static void AddAll(int[] row, int delta)
    {
        for (int i = 0; i < row.Length; ++i)
        {
            row[i] += delta;
        }
    }

    /// <summary>Adds <paramref name="delta"/> to one element of a row.</summary>
    private static void AddAt(int[] row, int index, int delta) => row[index] += delta;

    private static short ReadInt16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);

    private static float ReadSingle(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
}
