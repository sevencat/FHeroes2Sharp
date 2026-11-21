using FHeroes2.Core.Io;

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

    /// <summary>
    /// Writes the text and its language tag, <c>operator&lt;&lt;( OStreamBase &amp;, const LocalizedString &amp; )</c>
    /// (<c>game_string.cpp:25-28</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <remarks>
    /// 参考把可选的 <c>SupportedLanguage</c> 按 <c>std::optional</c> 的通用序列化写：先一个布尔，有值再写一个底层的
    /// <c>uint8_t</c>。端口照抄这个次序，才能和参考的存档互通。
    /// </remarks>
    public void Write(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(Text);
        stream.Write(Language.HasValue);

        if (Language.HasValue)
        {
            stream.Write((byte)Language.Value);
        }
    }

    /// <summary>
    /// Reads a text and its language tag, <c>operator&gt;&gt;( IStreamBase &amp;, LocalizedString &amp; )</c>
    /// (<c>game_string.cpp:30-33</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The string; an empty text with no language when the stream ran out.</returns>
    /// <remarks>越界的语言码当作「没有语言」，与参考对非法枚举值的处理一致：字段照读，值不可信就不用。</remarks>
    public static LocalizedString Read(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        string text = stream.ReadString();
        SupportedLanguage? language = null;

        if (stream.ReadBool())
        {
            byte value = stream.ReadUInt8();
            language = Enum.IsDefined(typeof(SupportedLanguage), (int)value) ? (SupportedLanguage)value : null;
        }

        return new LocalizedString(text, language);
    }
}
