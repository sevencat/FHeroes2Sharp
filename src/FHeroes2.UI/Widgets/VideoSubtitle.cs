using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Draws the text the reference burns over a video, <c>Video::Subtitle</c> (<c>game_video.cpp</c>): the campaign rating
/// that runs over <c>WIN.SMK</c> from the fifth second on (<c>game_campaign.cpp:1267-1269</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把字幕放进一个 「位置 + 最宽 + 起始毫秒」 的结构里，由视频循环在每一帧之后按当前时间决定画不画；端口把
/// 「什么时候画」留给调用方（<see cref="FHeroes2.App.Video.VideoModalScreen"/> 按时间判断），这里只负责排版与落笔。
/// </para>
/// <para>
/// 换行用 <see cref="GlyphText.DrawMultiline"/>：显式 <c>\n</c> 断行，超宽时优先在空格处折行，CJK 逐字折行。
/// 参考的宽度 140 是那块烧黑方框的宽度，所以这里的默认值也用它。
/// </para>
/// </remarks>
public static class VideoSubtitle
{
    /// <summary>The width the reference fits the rating into, the <c>140</c> of <c>game_campaign.cpp:1269</c>.</summary>
    public const int ReferenceWidth = 140;

    /// <summary>Draws a subtitle and answers the area it covered.</summary>
    /// <param name="font">The face to draw with.</param>
    /// <param name="target">The frame the video has just been painted into.</param>
    /// <param name="text">The text; <c>\n</c> starts a new line.</param>
    /// <param name="position">Top left corner, in logical pixels.</param>
    /// <param name="maxWidth">Widest a line may be before it wraps; <c>0</c> disables wrapping.</param>
    /// <param name="center">Whether each line is centred inside <paramref name="maxWidth"/>, the reference's subtitle.</param>
    /// <returns>The area that was drawn, or <see cref="Rect.Empty"/> for an empty text.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Rect Draw(
        IGlyphSource font,
        PixelBuffer target,
        string text,
        in Point position,
        int maxWidth = ReferenceWidth,
        bool center = true)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return Rect.Empty;
        }

        Size size = GlyphText.DrawMultiline(font, target, position, text, maxWidth, center && maxWidth > 0);

        return new Rect(position.X, position.Y, size.Width, size.Height);
    }
}
