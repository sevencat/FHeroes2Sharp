using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.H2D;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork the editor's spell selection window is assembled from,
/// <c>openEditorSpellSelectionWindow()</c> (<c>editor_spell_selection.cpp:207-276</c>).
/// </summary>
/// <remarks>
/// <para>
/// 窗口是满屏标准窗，底子铺 <c>STONEBAK</c>；每个法术是一张 <c>TOWNWIND</c> 第 0 帧的卷轴（未启用时同一张图过灰度
/// 调色板），图标来自 <c>SPELLS</c>，名字用小白字。
/// </para>
/// <para>
/// 四个按钮在存档里同样没有 .ICN：参考用 <c>makeButtonSprites()</c> 现场合成。OKAY/CANCEL 走
/// <c>BUTTON_SMALL_OKAY_*</c>/<c>BUTTON_SMALL_CANCEL_*</c> 运行期表（<see cref="GameAssets.GetImage"/> 认得）；
/// Toggle All On/Off 是 86x25 的空板子加 h2d 图标；等级 1..5 是 46x25 的空板子加数字。
/// </para>
/// <para>
/// h2d 图标缺席时（没有装 <c>res</c> 那份资源）两个 Toggle 按钮退化成没有图案的空板子，按键的位置和尺寸都不变——
/// 这是端口里唯一一处素材可以缺而窗口仍然成立的地方。
/// </para>
/// </remarks>
public sealed class EditorSpellSelectionArtwork
{
    /// <summary>Size of the Toggle All buttons, <c>spellSelectionButtonSize</c> (<c>:1660</c> of <c>game_assets.cpp</c>).</summary>
    public static readonly Size ToggleButtonSize = new(86, 25);

    /// <summary>Size of the level buttons, <c>spellButton</c> (<c>:1660</c> of <c>game_assets.cpp</c>).</summary>
    public static readonly Size LevelButtonSize = new(46, 25);

    private readonly Dictionary<SpellType, Sprite> _spellIcons = [];

    private EditorSpellSelectionArtwork()
    {
    }

    /// <summary>The <c>STONEBAK</c> texture the active area is filled with.</summary>
    public Sprite StoneBackground { get; private init; } = null!;

    /// <summary>The scroll of an enabled spell, <c>TOWNWIND</c> frame 0.</summary>
    public Sprite Scroll { get; private init; } = null!;

    /// <summary>The same scroll seen through the gray palette, <c>:105-106</c>.</summary>
    public Sprite InactiveScroll { get; private init; } = null!;

    /// <summary>The OKAY button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Okay { get; private init; }

    /// <summary>The CANCEL button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Cancel { get; private init; }

    /// <summary>The Enable All Spells button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) ToggleAllOn { get; private init; }

    /// <summary>The Disable All Spells button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) ToggleAllOff { get; private init; }

    /// <summary>The five level buttons, level 1 first.</summary>
    public IReadOnlyList<(Sprite Released, Sprite Pressed)> LevelButtons { get; private init; } = [];

    /// <summary>
    /// Reads the artwork out of the installed game data.
    /// </summary>
    /// <param name="isEvilInterface">Whether the evil look is used.</param>
    /// <returns>The artwork, or <see langword="null"/> when the game data is missing or incomplete.</returns>
    /// <remarks>
    /// 依赖 <see cref="GameAssets.Current"/> 已指向游戏数据——合成出来的按钮表与归档里的图都从那里取。
    /// </remarks>
    public static EditorSpellSelectionArtwork? TryCreate(bool isEvilInterface)
    {
        Sprite? stone = GameAssets.GetImage(
            GameAssets.GetFileName(isEvilInterface ? IcnType.StonebakEvil : IcnType.Stonebak), 0);

        Sprite? scroll = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Townwind), 0);

        (Sprite Released, Sprite Pressed)? okay = ReadButtons(
            isEvilInterface ? IcnType.ButtonSmallOkayEvil : IcnType.ButtonSmallOkayGood);

        (Sprite Released, Sprite Pressed)? cancel = ReadButtons(
            isEvilInterface ? IcnType.ButtonSmallCancelEvil : IcnType.ButtonSmallCancelGood);

        if (stone is null || scroll is null || okay is null || cancel is null)
        {
            return null;
        }

        Sprite inactiveScroll = new(scroll.Image.Clone());
        ImageOps.ApplyPalette(inactiveScroll.Image, PaletteId.Gray);

        return new EditorSpellSelectionArtwork
        {
            StoneBackground = stone,
            Scroll = scroll,
            InactiveScroll = inactiveScroll,
            Okay = okay.Value,
            Cancel = cancel.Value,
            ToggleAllOn = CreateToggleButton(stone, isEvilInterface, "toggle-all-on-released.image",
                "toggle-all-on-pressed.image"),
            ToggleAllOff = CreateToggleButton(stone, isEvilInterface, "toggle-all-off-released.image",
                "toggle-all-off-pressed.image"),
            LevelButtons = CreateLevelButtons(stone, isEvilInterface)
        };
    }

    /// <summary>
    /// The icon of a spell, <c>Assets::getImage( ICN::SPELLS, spell.IndexSprite() )</c> (<c>:117</c>).
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

        if (icon is not null)
        {
            _spellIcons[spell] = icon;
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

    private static (Sprite Released, Sprite Pressed) CreateLevelButton(Sprite stone, bool isEvilInterface, int level)
    {
        (Sprite released, Sprite pressed) = ButtonFactory.MakeButtonSprites(
            level.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LevelButtonSize,
            isEvilInterface,
            stone);

        return (released, pressed);
    }

    private static IReadOnlyList<(Sprite Released, Sprite Pressed)> CreateLevelButtons(Sprite stone,
        bool isEvilInterface)
    {
        (Sprite Released, Sprite Pressed)[] buttons = new (Sprite, Sprite)[5];

        for (int i = 0; i < buttons.Length; ++i)
        {
            buttons[i] = CreateLevelButton(stone, isEvilInterface, i + 1);
        }

        return buttons;
    }

    private static (Sprite Released, Sprite Pressed) CreateToggleButton(Sprite stone, bool isEvilInterface,
        string releasedIcon, string pressedIcon)
    {
        (Sprite released, Sprite pressed) = ButtonFactory.MakeButtonSprites(string.Empty, ToggleButtonSize,
            isEvilInterface, stone);

        if (H2DResources.TryReadCurrentImage(releasedIcon, out Sprite? icon))
        {
            ImageOps.BlitSprite(
                released.Image,
                new Point((released.Width - icon.Width) / 2, (released.Height - icon.Height) / 2),
                icon);
        }

        if (H2DResources.TryReadCurrentImage(pressedIcon, out Sprite? pressedImage))
        {
            ImageOps.BlitSprite(
                pressed.Image,
                new Point((pressed.Width - pressedImage.Width) / 2, ((pressed.Height - pressedImage.Height) / 2) + 1),
                pressedImage);
        }

        return (released, pressed);
    }
}
