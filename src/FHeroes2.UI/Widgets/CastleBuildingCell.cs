using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Gui;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// One building slot of the editor's castle details window, <c>BuildingData</c> of the reference
/// (<c>src/fheroes2/editor/editor_castle_details_window.cpp:79-307</c>).
/// </summary>
/// <remarks>
/// <para>
/// 一格装的不是「一栋建筑」而是一条升级链：骑士的枪兵营能从枪兵营升到升级过的枪兵营，这一格就画当前那一级，右下角
/// 用几个小标记说明「练到第几级」「哪一级之后不许再建」。左键每按一次往下走一级，走到底回到「什么都没建」；限制模式
/// 下每按一次降一级，降到 -1 表示这一级及以后都禁止。
/// </para>
/// <para>
/// <b>两个等级都用 -1 表示「没有」</b>，与参考的 <c>int8_t</c> 一致；<see cref="BuiltLevel"/> 为 0 表示第一级已建，
/// 所以「建了没有」的判断是 <c>&gt;= 0</c> 而不是 <c>!= 0</c>——这一条最容易被顺手写成布尔量。
/// </para>
/// <para>
/// <b>升级链走到底就停</b>：参考用 <c>getUpgradeForBuilding()</c> 的约定「没有更高一级就返回自己」来结束循环，端口
/// 的 <see cref="CastleBuildings.GetUpgradeForBuilding"/> 同一条约定，所以循环条件直接比建筑本身。
/// </para>
/// <para>
/// 这一刀只搬等级与限制的语义；把这一格画出来（建筑图、模糊、小标记、被禁的灰叉）与窗口本身在下一刀，因为画要用
/// 窗口的素材表，脱离窗口写一遍再拆一遍不划算。
/// </para>
/// </remarks>
public sealed class CastleBuildingCell
{
    /// <summary>Width of the slot, <c>_area.width</c>.</summary>
    public const int Width = 137;

    /// <summary>Height of the slot, <c>_area.height</c>.</summary>
    public const int Height = 70;

    /// <summary>Width of the left half, the part that can still be built, <c>_buildArea.width</c>.</summary>
    public const int BuildWidth = 69;

    /// <summary>Width of the right half, the part that is banned, <c>_banArea.width</c>.</summary>
    public const int BanWidth = 68;

    private BuildingType _building;
    private readonly MapRace _race;
    private int _builtLevel = -1;
    private int _restrictedLevel = -1;
    private int _variantCount = 1;
    private int _x;
    private int _y;

    /// <summary>
    /// Reads the state of one upgrade chain out of the two building masks of a castle,
    /// <c>BuildingData::BuildingData()</c> (<c>:82-105</c>).
    /// </summary>
    /// <param name="building">The first building of the chain, for instance <c>Dwelling1</c>.</param>
    /// <param name="race">Race of the castle, which decides what upgrades into what.</param>
    /// <param name="builtBuildings">Mask of the buildings the castle owns.</param>
    /// <param name="restrictedBuildings">Mask of the buildings the map forbids.</param>
    public CastleBuildingCell(BuildingType building, MapRace race, BuildingType builtBuildings, BuildingType restrictedBuildings)
    {
        _building = building;
        _race = race;

        if ((builtBuildings & building) != 0)
        {
            _builtLevel = 0;
        }
        else if ((restrictedBuildings & building) != 0)
        {
            _restrictedLevel = 0;
        }

        // 参考把参数本身当成游标往下走：每一轮先看下一级建没建/禁没禁，再把它当成新的当前级。
        BuildingType current = building;
        for (BuildingType upgraded = CastleBuildings.GetUpgradeForBuilding(race, current);
             upgraded != current;
             upgraded = CastleBuildings.GetUpgradeForBuilding(race, upgraded))
        {
            if ((builtBuildings & upgraded) != 0)
            {
                _builtLevel = _variantCount;
            }
            else if ((restrictedBuildings & upgraded) != 0)
            {
                _restrictedLevel = _variantCount;
            }

            ++_variantCount;
            current = upgraded;
        }
    }

    /// <summary>The first building of the chain, <c>_mainBuildingType</c>.</summary>
    public BuildingType Building => _building;

    /// <summary>Race of the castle, <c>_race</c>.</summary>
    public MapRace Race => _race;

    /// <summary>How many levels the chain has, <c>_buildingVariants</c>.</summary>
    public int VariantCount => _variantCount;

    /// <summary>Level that is built, -1 when nothing of the chain is, <c>_builtId</c>.</summary>
    public int BuiltLevel => _builtLevel;

    /// <summary>Level from which building is banned, -1 when nothing is banned, <c>_restrictedId</c>.</summary>
    public int RestrictedLevel => _restrictedLevel;

    /// <summary>The slot's rectangle, <c>_area</c>.</summary>
    public Rect Area => new(_x, _y, Width, Height);

    /// <summary>The left half of the slot, <c>_buildArea</c>.</summary>
    public Rect BuildArea => new(_x, _y, BuildWidth, Height);

    /// <summary>The right half of the slot, <c>_banArea</c>.</summary>
    public Rect BanArea => new(_x + BuildWidth, _y, BanWidth, Height);

    /// <summary>
    /// The building to draw: the built level when there is one, the first building of the chain otherwise,
    /// <c>_getBuildindTypeForRender()</c> (<c>:276-283</c>).
    /// </summary>
    public BuildingType RenderBuilding => _builtLevel < 1 ? _building : GetLevelBuilding(_builtLevel);

    /// <summary>
    /// The building whose description the right button shows, which is the drawn one,
    /// <c>getSetStatusMessage()</c> (<c>:196-199</c>) calls the same helper.
    /// </summary>
    public string RenderBuildingName => CastleBuildingInfo.GetName(_race, RenderBuilding);

    /// <summary>
    /// Every level that is built, from the first one up to <see cref="BuiltLevel"/>,
    /// <c>getBuildLevel()</c> (<c>:128-141</c>).
    /// </summary>
    public IReadOnlyList<BuildingType> BuiltChain
    {
        get
        {
            if (_builtLevel < 0)
            {
                return [];
            }

            BuildingType[] buildings = new BuildingType[_builtLevel + 1];
            for (int i = 0; i <= _builtLevel; ++i)
            {
                buildings[i] = GetLevelBuilding(i);
            }

            return buildings;
        }
    }

    /// <summary>
    /// The first banned level, <c>getRestrictLevel()</c> (<c>:143-146</c>).
    /// </summary>
    public BuildingType RestrictedBuilding => GetLevelBuilding(_restrictedLevel);

    /// <summary>Puts the slot somewhere on the screen, <c>setPosition()</c> (<c>:107-115</c>).</summary>
    /// <param name="posX">Left border.</param>
    /// <param name="posY">Top border.</param>
    public void SetPosition(int posX, int posY)
    {
        _x = posX;
        _y = posY;
    }

    /// <summary>Clears both levels, <c>resetBuilding()</c> (<c>:117-121</c>).</summary>
    public void Reset()
    {
        _builtLevel = -1;
        _restrictedLevel = -1;
    }

    /// <summary>
    /// One left click on the slot, <c>BuildingData::queueEventProcessing()</c> (<c>:148-176</c>).
    /// </summary>
    /// <remarks>
    /// 建的那一档是「往下走一级，走到底回到没建」；禁的那一档是「往上退一级」——从 -1 退到最末一级，因为
    /// 「什么都没禁」与「从第一级就禁」在画面上是两件事。两个方向都保着「已建不能越过已禁」这条线：往上建时把禁线
    /// 顶上去，往上禁时把已建压下来。
    /// </remarks>
    /// <param name="restrictionMode">Whether the author is editing the bans instead of the buildings.</param>
    public void Cycle(bool restrictionMode)
    {
        if (restrictionMode)
        {
            if (_restrictedLevel <= -1)
            {
                _restrictedLevel = _variantCount;
            }

            --_restrictedLevel;

            if (_builtLevel != -1 && _builtLevel >= _restrictedLevel)
            {
                _builtLevel = _restrictedLevel - 1;
            }

            return;
        }

        ++_builtLevel;

        if (_builtLevel >= _variantCount)
        {
            _builtLevel = -1;
        }
        else if (_restrictedLevel != -1 && _builtLevel >= _restrictedLevel)
        {
            _restrictedLevel = _builtLevel < _variantCount - 1 ? _builtLevel + 1 : -1;
        }
    }

    /// <summary>
    /// Paints the slot, <c>BuildingData::redraw()</c> (<c>:201-273</c>).
    /// </summary>
    /// <remarks>
    /// 画的顺序与参考一致：先把建筑图贴进格子的上半（135×57），再在右下角排一列小标记——每一个链上的等级一个，已建
    /// 的用勾、没建的用点、被禁的用灰叉；然后铺名字底条，被禁的那一半用抖动过渡从「可建」切到「灰」；最后写建筑名。
    /// 默认城堡（地图作者没改过）整格压暗一层、名字也压暗，这是参考用来区分「这是原样」的记号。
    /// </remarks>
    /// <param name="display">Frame buffer to draw into.</param>
    /// <param name="artwork">The frames of the slot.</param>
    /// <param name="gamePalette">Game palette in video values, which the blur of a default building needs.</param>
    /// <param name="isNotDefault">Whether the castle is not a default one, that is, no blur and no darkened header.</param>
    public void Draw(PixelBuffer display, CastleBuildingCellArtwork artwork, Palette gamePalette, bool isNotDefault)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(gamePalette);

        Rect buildingArea = new(_x + 1, _y + 1, 135, 57);

        Sprite icon = artwork.GetBuildingIcon(RenderBuilding) ?? artwork.FallbackIcon;
        Rect iconArea = new(0, 0, Math.Min(buildingArea.Width, icon.Width), Math.Min(buildingArea.Height, icon.Height));

        if (isNotDefault)
        {
            ImageOps.BlitSprite(display, new Point(buildingArea.X, buildingArea.Y), icon, iconArea);
        }
        else
        {
            // 参考用圣言术那层的模糊做「这不是作者改过的城堡」的记号（:217-220）。
            PixelBuffer blurred = SpellEffects.CreateHolyShoutEffect(icon.Image, 1, 0, gamePalette);
            ImageOps.Blit(display, new Point(buildingArea.X, buildingArea.Y), blurred, iconArea);
            ImageOps.ApplyPalette(display, buildingArea, PaletteTable.Get(PaletteId.Darkening));
        }

        // 链上每一级一个标记，从右往左排，每个 20 像素。
        int maxUpgrades = _variantCount;
        for (int i = 0; i < maxUpgrades; ++i)
        {
            int centerX = _x + Width - (maxUpgrades - i) * 20;

            if (_restrictedLevel != -1 && i >= _restrictedLevel)
            {
                Sprite denySign = artwork.DeniedMark;
                ImageOps.BlitSprite(display, new Point(centerX - denySign.Width / 2, _y + 48 - denySign.Height / 2), denySign);
                continue;
            }

            Sprite mark = i > _builtLevel ? artwork.UnbuiltMark : artwork.BuiltMark;
            ImageOps.BlitSprite(display, new Point(centerX - mark.Width / 2, _y + 48 - mark.Height / 2), mark);
        }

        if (_builtLevel > -1)
        {
            ImageOps.BlitSprite(display, new Point(_x, _y + 58), artwork.CellFrame,
                new Rect(0, 58, Width, Math.Min(artwork.CellFrame.Height - 58, Height - 58)));
        }
        else if (_restrictedLevel != 0)
        {
            ImageOps.BlitSprite(display, new Point(_x, _y + 58), artwork.BuildableStrip,
                new Rect(0, 0, Width, Math.Min(artwork.BuildableStrip.Height, Height - 58)));
        }

        if (_restrictedLevel > -1)
        {
            Sprite strip = artwork.DeniedStrip;
            int stripHeight = Math.Min(strip.Height, Height - 58);

            if (_restrictedLevel == 0)
            {
                ImageOps.BlitSprite(display, new Point(_x, _y + 58), strip, new Rect(0, 0, Width - 5, stripHeight));
            }
            else
            {
                ImageOps.CreateDitheringTransition(strip, new Point(BuildWidth - 5, 0), display,
                    new Point(BanArea.X - 5, _y + 58), new Size(10, stripHeight), vertical: true, reverse: false);

                ImageOps.BlitSprite(display, new Point(BanArea.X + 5, _y + 58), strip,
                    new Rect(BuildWidth + 5, 0, BanWidth - 5, stripHeight));
            }
        }

        if (!isNotDefault)
        {
            ImageOps.ApplyPalette(display, new Rect(_x + 6, _y + 59, 125, 12), PaletteTable.Get(PaletteId.Darkening));
        }

        Text name = new(CastleBuildingInfo.GetName(_race, RenderBuilding), FontType.SmallWhite());
        name.FitToOneRow(125);
        name.Draw(_x + 68 - name.Width() / 2, _y + 61, display);
    }

    /// <summary>
    /// The building of a level of the chain, <c>_getBuildingType()</c> (<c>:285-297</c>).
    /// </summary>
    /// <param name="level">Level index, the first building being 0.</param>
    /// <returns>The building, or <see cref="BuildingType.Nothing"/> for a level below zero.</returns>
    public BuildingType GetLevelBuilding(int level)
    {
        if (level < 0)
        {
            return BuildingType.Nothing;
        }

        BuildingType building = _building;
        for (int i = 0; i < level; ++i)
        {
            building = CastleBuildings.GetUpgradeForBuilding(_race, building);
        }

        return building;
    }
}
