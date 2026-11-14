namespace FHeroes2.Game;

/// <summary>
/// The languages the reference knows about, <c>enum class SupportedLanguage</c>
/// (<c>src/fheroes2/game/game_language.h:29</c>).
/// </summary>
/// <remarks>
/// 顺序与参考一致（英文在前，其余按字母序），值本身没有含义——参考用它选翻译、代码页和资材语言，
/// 本项目目前只用到「这是哪国语言的资材」这一面（见 <see cref="ResourceLanguage"/>）。
/// 翻译文本（<c>.mo</c>）与 <c>getCodePage</c> 还没搬，等 UI 文本需要多语言时再补。
/// </remarks>
public enum SupportedLanguage
{
    /// <summary>English, the fallback for every unrecognized asset.</summary>
    English,

    /// <summary>French.</summary>
    French,

    /// <summary>Polish.</summary>
    Polish,

    /// <summary>German.</summary>
    German,

    /// <summary>Russian.</summary>
    Russian,

    /// <summary>Italian.</summary>
    Italian,

    /// <summary>Czech.</summary>
    Czech,

    /// <summary>Spanish.</summary>
    Spanish,

    /// <summary>Belarusian.</summary>
    Belarusian,

    /// <summary>Bulgarian.</summary>
    Bulgarian,

    /// <summary>Danish.</summary>
    Danish,

    /// <summary>Dutch.</summary>
    Dutch,

    /// <summary>Esperanto.</summary>
    Esperanto,

    /// <summary>Greek.</summary>
    Greek,

    /// <summary>Hungarian.</summary>
    Hungarian,

    /// <summary>Norwegian.</summary>
    Norwegian,

    /// <summary>Portuguese.</summary>
    Portuguese,

    /// <summary>Romanian.</summary>
    Romanian,

    /// <summary>Slovak.</summary>
    Slovak,

    /// <summary>Swedish.</summary>
    Swedish,

    /// <summary>Turkish.</summary>
    Turkish,

    /// <summary>Ukrainian.</summary>
    Ukrainian,

    /// <summary>Vietnamese.</summary>
    Vietnamese,
}
