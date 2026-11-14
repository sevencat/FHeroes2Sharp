using FHeroes2.Assets;
using FHeroes2.Core.Io;

namespace FHeroes2.Game;

/// <summary>
/// Which language version the archives on disk are, <c>fheroes2::getResourceLanguage</c>
/// (<c>src/fheroes2/gui/ui_language.cpp:88</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考没有「资材语言」这个标记文件，而是拿 <c>FONT.ICN</c> 的 CRC32 当指纹：各国译版都改过这张字体表，
/// 每个版本的 CRC 都不一样，比对一张已知表就能认出来，认不出就是英文。因为全靠内容判断，一套资材里混进
/// 别的版本的字体会认成英文——参考也这样。
/// </para>
/// <para>
/// 认出来之后有两类用途：字体修补走哪条分支（法语资材要重画 <c>$</c>/<c>&amp;</c>/<c>*</c>，
/// 见 <see cref="Gui.FontFrenchRepairs"/>），以及部分资材要重排帧（德语/意大利语 CP1252、俄语 CP1251，
/// 见 <see cref="Gui.FontAssetFixes"/>）。
/// </para>
/// <para>
/// 缓存跟着 <see cref="Assets.Current"/> 走：换包要重新算，参考是进程级静态量，因为它一辈子只开一次归档。
/// </para>
/// </remarks>
public static class ResourceLanguage
{
    /// <summary>The entry whose content identifies the version, <c>ICN::FONT</c>.</summary>
    private const string ProbeFileName = "FONT.ICN";

    /// <summary>Known fingerprints of translated <c>FONT.ICN</c> files, from <c>ui_language.cpp:40</c>.</summary>
    private static readonly Dictionary<uint, SupportedLanguage> Fingerprints = new()
    {
        [0x406967B9] = SupportedLanguage.French, // GoG version
        [0x04745D1D] = SupportedLanguage.German, // GoG version
        [0x88774771] = SupportedLanguage.Polish, // GoG version
        [0xDB10FFD8] = SupportedLanguage.Russian, // XXI Vek version
        [0xD5CF8AF3] = SupportedLanguage.Russian, // Buka version
        [0x219B3124] = SupportedLanguage.Italian,
        [0x1CEBD099] = SupportedLanguage.Czech, // CD Projekt
    };

    private static readonly object SyncRoot = new();

    private static AssetBundle? _bundle;
    private static SupportedLanguage? _language;

    /// <summary>
    /// The language version of the current archives, or <see cref="SupportedLanguage.English"/> when the
    /// fingerprint is unknown.
    /// </summary>
    /// <returns>The detected language; cached until <see cref="Assets.Current"/> changes.</returns>
    /// <remarks>参考：<c>ui_language.cpp:88</c>，没有归档或没有字体时也返回英文（参考那里是 <c>assert( 0 )</c>）。</remarks>
    public static SupportedLanguage Get()
    {
        AssetBundle? bundle = Assets.Current;

        lock (SyncRoot)
        {
            if (_language is { } cached && ReferenceEquals(_bundle, bundle))
            {
                return cached;
            }

            SupportedLanguage detected = Detect();

            _bundle = bundle;
            _language = detected;

            return detected;
        }
    }

    private static SupportedLanguage Detect()
    {
        if (Assets.TryReadRaw(ProbeFileName) is not { Length: > 0 } data)
        {
            return SupportedLanguage.English;
        }

        return Fingerprints.TryGetValue(Crc32.Compute(data), out SupportedLanguage language)
            ? language
            : SupportedLanguage.English;
    }
}
