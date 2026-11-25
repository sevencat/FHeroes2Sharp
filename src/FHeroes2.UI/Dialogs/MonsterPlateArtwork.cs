using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The archive-backed sprites of <see cref="MonsterDialogElement"/>, the <c>renderMonsterFrame()</c> call of
/// <c>ui_dialog.cpp:918-928</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的元素自己拿 <c>ICN::STRIP</c> 的第 12 帧当底板、拿 <c>renderMonsterFrame()</c> 画种族背景与住宅立绘；
/// 端口把图交给调用方（<see cref="MonsterDialogElement"/> 不碰档案），这个类就是「从档案里取」的那一份。
/// </para>
/// <para>
/// 种族背景与立绘直接复用 <see cref="ArmyBarArtwork"/>（兵栏的同一套 <c>STRIP</c> / <c>MONH####</c>，帧号相同），
/// 底板是 <c>STRIP</c> 第 12 帧，兵栏不用它，所以单独取。三张图都懒加载并缓存。
/// </para>
/// </remarks>
public sealed class MonsterPlateArtwork
{
    private readonly ArmyBarArtwork _portraits = new();

    private Sprite? _plate;
    private bool _plateLoaded;

    /// <summary>
    /// Builds the element of a creature, <c>MonsterDialogElement( troop )</c> of the reference.
    /// </summary>
    /// <param name="monster">The creature on the plate.</param>
    /// <param name="popupHeader">Header of the popup the clicks ask for; empty for none.</param>
    /// <param name="popupBody">Body of the popup the clicks ask for; empty for none.</param>
    /// <returns>The element, or <see langword="null"/> when <c>STRIP</c> frame 12 is missing.</returns>
    public MonsterDialogElement? TryCreate(MonsterType monster, string popupHeader = "", string popupBody = "")
    {
        if (LoadPlate() is not { } plate)
        {
            return null;
        }

        MapRace race = MonsterStatistics.GetGeneralStats(monster).Race;

        return new MonsterDialogElement(
            plate,
            _portraits.GetRaceFrame(race),
            _portraits.GetMonsterFrame(monster),
            popupHeader,
            popupBody);
    }

    /// <summary>Frame <see cref="MonsterDialogElement.PlateFrame"/> of <c>STRIP</c>, read once.</summary>
    /// <returns>The plate, or <see langword="null"/> when the archive is missing it.</returns>
    private Sprite? LoadPlate()
    {
        if (!_plateLoaded)
        {
            _plateLoaded = true;
            _plate = GameAssets.GetImage(
                IcnCatalog.GetFileName(IcnType.Strip),
                MonsterDialogElement.PlateFrame);
        }

        return _plate;
    }
}
