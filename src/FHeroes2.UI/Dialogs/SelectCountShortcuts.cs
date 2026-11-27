using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The two shortcut buttons of the count window, <c>MIN</c> and <c>MAX</c>, which the reference generates at
/// startup rather than reading them from an <c>.ICN</c> (<c>game_assets.cpp:1261-1294</c>).
/// </summary>
/// <remarks>
/// <para>
/// 六十乘二十五的两颗：MAX 是 <c>RECRUIT</c> 4/5 帧裁出来的（图上本来就印着 MAX），MIN 是拿 <c>STONEBAK</c> 当底、
/// 用按钮字体现写「MIN」合成的，文字区宽 = MAX 的宽度 − 10。
/// </para>
/// <para>
/// 恶界面那两张（<c>UNIFORM_EVIL_MIN/MAX_BUTTON</c>）在参考里就是对好界面版本做调色板替换
/// （<c>game_assets.cpp:1498-1560</c> 的 <c>BUTTON_SMALL_MIN_EVIL</c> 一条），端口照这个做法换色——与
/// <see cref="AutoPlaytestWindow"/> 读滑条帧时一致。
/// </para>
/// </remarks>
public sealed class SelectCountShortcuts
{
    /// <summary>Width of the MAX button, the <c>60</c> of the <c>RECRUIT</c> crop.</summary>
    public const int MaxButtonWidth = 60;

    /// <summary>Height of both buttons, the <c>25</c> of the <c>RECRUIT</c> crop and the MIN text area.</summary>
    public const int ButtonHeight = 25;

    /// <summary>The MAX button, released and pressed.</summary>
    public required (Sprite Released, Sprite Pressed) Max { get; init; }

    /// <summary>The MIN button, released and pressed.</summary>
    public required (Sprite Released, Sprite Pressed) Min { get; init; }

    /// <summary>
    /// Builds the pair out of the installed game data.
    /// </summary>
    /// <param name="isEvilInterface">Whether the gray recolour is used.</param>
    /// <returns>The pair, or <see langword="null"/> when the archive lacks <c>RECRUIT</c>/<c>STONEBAK</c>.</returns>
    public static SelectCountShortcuts? TryCreate(bool isEvilInterface)
    {
        if (ButtonFactory.MakeSmallMaxButton() is not { } max)
        {
            return null;
        }

        if (GameAssets.GetImage(GameAssets.GetFileName(IcnType.Stonebak), 0) is not { } background)
        {
            return null;
        }

        (Sprite Released, Sprite Pressed) min = ButtonFactory.MakeSmallMinButton(MaxButtonWidth, background);

        if (!isEvilInterface)
        {
            return new SelectCountShortcuts { Max = max, Min = min };
        }

        return new SelectCountShortcuts { Max = Recolour(max), Min = Recolour(min) };
    }

    private static (Sprite Released, Sprite Pressed) Recolour((Sprite Released, Sprite Pressed) pair)
    {
        Sprite released = pair.Released.Clone();
        Sprite pressed = pair.Pressed.Clone();

        ImageOps.ApplyPalette(released.Image, PaletteId.GoodToEvilInterface);
        ImageOps.ApplyPalette(pressed.Image, PaletteId.GoodToEvilInterface);

        return (released, pressed);
    }
}
