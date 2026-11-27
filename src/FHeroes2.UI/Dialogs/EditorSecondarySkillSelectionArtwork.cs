using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork the editor's secondary skill selection window is assembled from,
/// <c>openSecondarySkillSelectionWindow()</c> (<c>editor_secondary_skill_selection.cpp:243-265</c>).
/// </summary>
/// <remarks>
/// <para>
/// 窗口是满屏标准窗，底子铺 <c>STONEBAK</c>；每个技能是一张 <c>SECSKILL</c> 第 15 帧的框，里面嵌同一张图的
/// 技能图标（第 1..14 帧），未启用时两张图一起过灰度调色板（参考 <c>:104-105</c>、<c>:120-122</c>）。
/// </para>
/// <para>
/// OKAY/CANCEL 从归档里取（<c>BUTTON_SMALL_OKAY_*</c>/<c>BUTTON_SMALL_CANCEL_*</c>），两个全开/全关是合成的：
/// 参考写的 <c>BUTTON_TOGGLE_ALL_ON_*</c>/<c>BUTTON_TOGGLE_ALL_OFF_*</c>（<c>:260-265</c>）在归档里没有 <c>.ICN</c>，
/// 是 <c>game_assets.cpp</c> 用 86x25 空板子加 h2d 图案拼出来的，端口走同一条路（法术窗那两个也是这么来的）。
/// </para>
/// </remarks>
public sealed class EditorSecondarySkillSelectionArtwork
{
    /// <summary>The frame a cell is drawn in, <c>ICN::SECSKILL</c> frame 15 (<c>:80</c>).</summary>
    public const int FrameIndex = 15;

    private readonly Dictionary<SecondarySkill, Sprite> _icons = [];

    private EditorSecondarySkillSelectionArtwork()
    {
    }

    /// <summary>The <c>STONEBAK</c> texture the active area is filled with.</summary>
    public Sprite StoneBackground { get; private init; } = null!;

    /// <summary>The frame of an enabled cell, <c>SECSKILL</c> frame 15.</summary>
    public Sprite Frame { get; private init; } = null!;

    /// <summary>The same frame seen through the gray palette, <c>:104-105</c>.</summary>
    public Sprite InactiveFrame { get; private init; } = null!;

    /// <summary>The OKAY button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Okay { get; private init; }

    /// <summary>The CANCEL button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Cancel { get; private init; }

    /// <summary>The Enable All Secondary Skills button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) ToggleAllOn { get; private init; }

    /// <summary>The Disable All Secondary Skills button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) ToggleAllOff { get; private init; }

    /// <summary>
    /// Reads the artwork out of the installed game data.
    /// </summary>
    /// <param name="isEvilInterface">Whether the evil look is used.</param>
    /// <returns>The artwork, or <see langword="null"/> when the game data is missing or incomplete.</returns>
    /// <remarks>
    /// 依赖 <see cref="GameAssets.Current"/> 已指向游戏数据。
    /// </remarks>
    public static EditorSecondarySkillSelectionArtwork? TryCreate(bool isEvilInterface)
    {
        Sprite? stone = GameAssets.GetImage(
            GameAssets.GetFileName(isEvilInterface ? IcnType.StonebakEvil : IcnType.Stonebak), 0);

        string skillFile = GameAssets.GetFileName(IcnType.Secskill);

        Sprite? frame = GameAssets.GetImage(skillFile, FrameIndex);

        (Sprite Released, Sprite Pressed)? okay = ReadButtons(
            isEvilInterface ? IcnType.ButtonSmallOkayEvil : IcnType.ButtonSmallOkayGood);

        (Sprite Released, Sprite Pressed)? cancel = ReadButtons(
            isEvilInterface ? IcnType.ButtonSmallCancelEvil : IcnType.ButtonSmallCancelGood);

        if (stone is null || frame is null || okay is null || cancel is null)
        {
            return null;
        }

        Sprite inactiveFrame = new(frame.Image.Clone());
        ImageOps.ApplyPalette(inactiveFrame.Image, PaletteId.Gray);

        return new EditorSecondarySkillSelectionArtwork
        {
            StoneBackground = stone,
            Frame = frame,
            InactiveFrame = inactiveFrame,
            Okay = okay.Value,
            Cancel = cancel.Value,
            // 这两个和法术窗一样是合成的：BUTTON_TOGGLE_ALL_* 在归档里没有 .ICN，图案在 h2d 资源里。
            ToggleAllOn = ButtonFactory.MakeToggleAllButton(stone, isEvilInterface, enableAll: true),
            ToggleAllOff = ButtonFactory.MakeToggleAllButton(stone, isEvilInterface, enableAll: false)
        };
    }

    /// <summary>
    /// The icon of a skill, <c>Assets::getImage( ICN::SECSKILL, skill.GetIndexSprite1() )</c> (<c>:110</c>).
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <returns>The icon, or <see langword="null"/> when the archive lacks that frame.</returns>
    public Sprite? GetSkillIcon(SecondarySkill skill)
    {
        if (_icons.TryGetValue(skill, out Sprite? cached))
        {
            return cached;
        }

        Sprite? icon = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Secskill),
            SecondarySkills.GetIndexSprite1(skill));

        if (icon is not null)
        {
            _icons[skill] = icon;
        }

        return icon;
    }

    private static (Sprite Released, Sprite Pressed)? ReadButtons(IcnType type)
    {
        string fileName = GameAssets.GetFileName(type);

        Sprite? released = GameAssets.GetImage(fileName, 0);
        Sprite? pressed = GameAssets.GetImage(fileName, 1);

        return released is null || pressed is null ? null : (released, pressed);
    }
}
