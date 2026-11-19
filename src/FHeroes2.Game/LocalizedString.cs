namespace FHeroes2.Game;

/// <summary>
/// A piece of text together with the language it is written in, <c>LocalizedString</c>
/// (<c>game_string.h:30-50</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference pairs a <c>std::string</c> with an optional <c>SupportedLanguage</c> so that the renderer can pick the
/// font the text needs: a rumor typed into a Russian map must be drawn with the Cyrillic face even when the interface
/// runs in English. A <see langword="null"/> language means «the text is ours», i.e. it has already been translated by
/// the interface and uses the normal font — which is what every built-in rumor returns.
/// </para>
/// <para>
/// The port keeps the tag but has no font fallback yet (see <see cref="SupportedLanguage"/>); carrying the value now
/// means the tavern needs no change when the font layer learns about it.
/// </para>
/// </remarks>
/// <param name="Text">The text itself, <c>text</c>.</param>
/// <param name="Language">
/// The language the text is written in, <c>language</c>; <see langword="null"/> when it is already translated.
/// </param>
public readonly record struct LocalizedString(string Text, SupportedLanguage? Language = null)
{
    /// <summary>The text, so that the value prints as the sentence it carries.</summary>
    /// <returns><see cref="Text"/>.</returns>
    public override string ToString() => Text;
}
