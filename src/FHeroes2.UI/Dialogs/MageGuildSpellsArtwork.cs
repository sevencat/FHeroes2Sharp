using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork of <c>mageGuildSpellsDialog()</c> (<c>editor_castle_details_window.cpp:318-606</c>): the stone
/// plate, the guild's own picture, the two scrolls, the BANNED SPELLS button and the spell icons.
/// </summary>
/// <remarks>
/// <para>
/// 背景、状态栏与 OKAY/EXIT 三个按钮跟城堡详情窗是同一套（<see cref="CastleDetailsArtwork"/>），这里不再拼一遍，
/// 直接把它当成一块拿来用；本类只补这个子窗独有的四样：公会建筑图、两种卷轴（<c>TOWNWIND</c> 的 0/1）、
/// 「BANNED SPELLS」按钮（参考现场用 <c>EMPTY_*_BUTTON</c> 加石头底合成，<c>:374-389</c>），以及法术图标。
/// </para>
/// <para>
/// 图标不预先全读：<c>SPELLS</c> 有一百多帧，编辑器里一次最多用到二十来个法术，按需读再记住就够了，
/// 与 <see cref="EditorSpellSelectionArtwork.GetSpellIcon"/> 同一做法。
/// </para>
/// </remarks>
public sealed class MageGuildSpellsArtwork
{
    /// <summary>Label of the button that opens the banned spells sub-window, <c>gettext_noop( "BANNED SPELLS" )</c>.</summary>
    public const string BannedSpellsText = "BANNED SPELLS";

    /// <summary>
    /// Guild level whose picture is drawn, <c>renderMageGuildBuilding( race, 5, ... )</c> (<c>:336</c>): the editor
    /// always shows the fully built guild.
    /// </summary>
    public const int GuildLevel = 5;

    /// <summary>Frame of the guild's archive to read, the guild level minus one.</summary>
    public const int GuildBuildingFrameIndex = GuildLevel - 1;

    private readonly Dictionary<SpellType, Sprite?> _spellIcons = [];

    private MageGuildSpellsArtwork(CastleDetailsArtwork frame, Sprite openedScroll, Sprite foldedScroll,
        Sprite guildBuilding, Sprite bannedReleased, Sprite bannedPressed)
    {
        Frame = frame;
        OpenedScroll = openedScroll;
        FoldedScroll = foldedScroll;
        GuildBuilding = guildBuilding;
        BannedSpellsReleased = bannedReleased;
        BannedSpellsPressed = bannedPressed;
    }

    /// <summary>The plates and buttons shared with the castle details window.</summary>
    public CastleDetailsArtwork Frame { get; }

    /// <summary>The scroll of a slot that holds a spell, <c>TOWNWIND</c> frame 0.</summary>
    public Sprite OpenedScroll { get; }

    /// <summary>The scroll of an empty slot, <c>TOWNWIND</c> frame 1.</summary>
    public Sprite FoldedScroll { get; }

    /// <summary>The picture of the fully built guild of the castle's race, frame <see cref="GuildBuildingFrameIndex"/>.</summary>
    public Sprite GuildBuilding { get; }

    /// <summary>The released plate of the BANNED SPELLS button.</summary>
    public Sprite BannedSpellsReleased { get; }

    /// <summary>The pressed plate of the BANNED SPELLS button.</summary>
    public Sprite BannedSpellsPressed { get; }

    /// <summary>
    /// Reads the artwork of the sub-window.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <param name="race">Race of the castle, which picks the guild's picture.</param>
    /// <param name="isEvilInterface">Whether the gray <c>*_EVIL</c> variants are used.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static MageGuildSpellsArtwork? TryCreate(AssetBundle assets, MapRace race, bool isEvilInterface)
    {
        ArgumentNullException.ThrowIfNull(assets);

        CastleDetailsArtwork? frame = CastleDetailsArtwork.TryCreate(assets, isEvilInterface);
        Sprite? openedScroll = TryGetSprite(assets, IcnType.Townwind, MageGuildArtwork.OpenedScrollFrame);
        Sprite? foldedScroll = TryGetSprite(assets, IcnType.Townwind, MageGuildArtwork.FoldedScrollFrame);
        Sprite? guildBuilding = TryGetSprite(assets, MageGuildArtwork.GetGuildBuildingIcn(race), GuildBuildingFrameIndex);

        if (frame is null || openedScroll is null || foldedScroll is null || guildBuilding is null)
        {
            return null;
        }

        // 参考把 EMPTY_*_BUTTON 与 STONEBAK 拼成一块能装下任意译文宽度的板子（:378-384）。
        (Sprite bannedReleased, Sprite bannedPressed) = ButtonFactory.GetTextAdaptedSprite(BannedSpellsText,
            isEvilInterface ? DialogButtonTemplate.Evil : DialogButtonTemplate.Good, frame.StoneBackground);

        return new MageGuildSpellsArtwork(frame, openedScroll, foldedScroll, guildBuilding, bannedReleased,
            bannedPressed);
    }

    /// <summary>
    /// The icon of a spell, <c>Assets::getImage( ICN::SPELLS, spell.IndexSprite() )</c>.
    /// </summary>
    /// <param name="spell">The spell.</param>
    /// <returns>The icon, or <see langword="null"/> when the archive lacks that frame.</returns>
    public Sprite? GetSpellIcon(SpellType spell)
    {
        if (_spellIcons.TryGetValue(spell, out Sprite? cached))
        {
            return cached;
        }

        Sprite? icon = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Spells), (int)new Spell(spell).IndexSprite());

        _spellIcons[spell] = icon;

        return icon;
    }

    private static Sprite? TryGetSprite(AssetBundle assets, IcnType type, int index)
    {
        if (assets.TryReadIcn(IcnCatalog.GetFileName(type)) is not { } file || file.Count <= index)
        {
            return null;
        }

        return file.Sprites[index];
    }
}
