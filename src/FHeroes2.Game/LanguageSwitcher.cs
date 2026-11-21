using FHeroes2.Game.System;

namespace FHeroes2.Game;

/// <summary>
/// Switches the interface language for the lifetime of the object,
/// <c>fheroes2::LanguageSwitcher</c> (<c>ui_language.cpp:77-86</c>, <c>ui_language.h:30-44</c>).
/// </summary>
/// <remarks>
/// 参考用它把某个名字临时按它自己的语言渲染（例如地图或英雄名），出了作用域自动切回。参考在构造函数里就调
/// <c>setGameLanguage()</c>，析构时恢复；端口把恢复放在 <see cref="Dispose"/>，用 <c>using</c> 表达同一件事。
/// </remarks>
public sealed class LanguageSwitcher : IDisposable
{
    private readonly string _currentLanguage;

    /// <summary>Switches the interface language to <paramref name="language"/>.</summary>
    /// <param name="language">The language to switch to.</param>
    public LanguageSwitcher(SupportedLanguage language)
    {
        _currentLanguage = Settings.Get().GameLanguage;

        Settings.Get().SetGameLanguage(GameLanguage.GetAbbreviation(language));
    }

    /// <summary>Restores the language that was in effect before construction.</summary>
    public void Dispose() => Settings.Get().SetGameLanguage(_currentLanguage);
}
