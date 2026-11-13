using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The template table of <c>fheroes2::getButtonSpecificValues()</c>.
/// </summary>
public sealed class DialogButtonTemplateTests
{
    [Fact]
    public void GoodAndEvil_ShareEveryMetricExceptTheTextPalette()
    {
        ButtonTemplateMetrics good = ButtonTemplateMetrics.Get(DialogButtonTemplate.Good);
        ButtonTemplateMetrics evil = ButtonTemplateMetrics.Get(DialogButtonTemplate.Evil);

        Assert.Equal(PaletteId.WhiteFont, good.TextPalette);
        Assert.Equal(PaletteId.GrayFont, evil.TextPalette);
        Assert.Equal(good with { Template = evil.Template, TextPalette = evil.TextPalette }, evil);
    }

    [Fact]
    public void MinimumButtonSize_AddsTheBackgroundBordersToTheTextArea()
    {
        Assert.Equal(new Size(96, 23), ButtonTemplateMetrics.Get(DialogButtonTemplate.Good).MinimumButtonSize);
        Assert.Equal(new Size(28, 120), ButtonTemplateMetrics.Get(DialogButtonTemplate.VerticalGood).MinimumButtonSize);
        Assert.Equal(new Size(61, 18), ButtonTemplateMetrics.Get(DialogButtonTemplate.GuildWell).MinimumButtonSize);
    }

    [Theory]
    [InlineData(DialogButtonTemplate.Good, true)]
    [InlineData(DialogButtonTemplate.Evil, true)]
    [InlineData(DialogButtonTemplate.Pol, false)]
    [InlineData(DialogButtonTemplate.GuildWell, false)]
    [InlineData(DialogButtonTemplate.MapSelect, false)]
    [InlineData(DialogButtonTemplate.VerticalGood, false)]
    public void NeedsExtraHeightForMultilineText_IsOnlySetForTheMainTemplates(DialogButtonTemplate template, bool expected)
    {
        Assert.Equal(expected, ButtonTemplateMetrics.Get(template).NeedsExtraHeightForMultilineText);
    }

    [Fact]
    public void Get_ThrowsForAnUnknownTemplate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ButtonTemplateMetrics.Get((DialogButtonTemplate)99));
    }
}
