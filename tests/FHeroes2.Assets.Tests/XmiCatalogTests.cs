using FHeroes2.Assets.Audio;
using Xunit;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Pins <c>xmimap</c> (<c>src/fheroes2/agg/xmi.cpp:29</c>), <c>XMI::GetString</c> (<c>xmi.cpp:61</c>)
/// and <c>XMI::FromMUS</c> (<c>xmi.cpp:67</c>), including the six aliases the original release needs.
/// </summary>
public class XmiCatalogTests
{
    [Theory]
    [InlineData(XmiTrack.Midi0002, "MIDI0002.XMI")]
    [InlineData(XmiTrack.Midi0043, "MIDI0043.XMI")]
    [InlineData(XmiTrack.MidiOriginalKnight, "MIDI0009.XMI")]
    [InlineData(XmiTrack.MidiOriginalBarbarian, "MIDI0007.XMI")]
    [InlineData(XmiTrack.MidiOriginalSorceress, "MIDI0005.XMI")]
    [InlineData(XmiTrack.MidiOriginalWarlock, "MIDI0005.XMI")]
    [InlineData(XmiTrack.MidiOriginalWizard, "MIDI0008.XMI")]
    [InlineData(XmiTrack.MidiOriginalNecromancer, "MIDI0006.XMI")]
    public void ATrackMapsToItsArchiveEntry(XmiTrack track, string expected)
    {
        Assert.Equal(expected, XmiCatalog.GetFileName(track));
    }

    [Fact]
    public void AnUnknownTrackMapsToTheUnknownEntry()
    {
        Assert.Equal("UNKNOWN", XmiCatalog.GetFileName(XmiTrack.Unknown));
        Assert.Equal("UNKNOWN", XmiCatalog.GetFileName((XmiTrack)(-1)));
        Assert.Equal("UNKNOWN", XmiCatalog.GetFileName((XmiTrack)100000));
    }

    [Theory]
    [InlineData(MusicTrack.Battle1, XmiTrack.Midi0002)]
    [InlineData(MusicTrack.Battle3, XmiTrack.Midi0004)]
    [InlineData(MusicTrack.SorceressCastle, XmiTrack.Midi0005)]
    [InlineData(MusicTrack.Lava, XmiTrack.Midi0011)]
    [InlineData(MusicTrack.Desert, XmiTrack.Midi0013)]
    [InlineData(MusicTrack.Grass, XmiTrack.Midi0018)]
    [InlineData(MusicTrack.MainMenu, XmiTrack.Midi0042)]
    [InlineData(MusicTrack.Victory, XmiTrack.Midi0043)]
    [InlineData(MusicTrack.Unknown, XmiTrack.Unknown)]
    public void AMusicTrackMapsToOneMidiFile(MusicTrack music, XmiTrack expected)
    {
        Assert.Equal(expected, XmiCatalog.FromMusicTrack(music));
    }

    [Theory]
    [InlineData(MusicTrack.WarlockCastle, XmiTrack.Midi0006, XmiTrack.MidiOriginalWarlock)]
    [InlineData(MusicTrack.NecromancerCastle, XmiTrack.Midi0007, XmiTrack.MidiOriginalNecromancer)]
    [InlineData(MusicTrack.KnightCastle, XmiTrack.Midi0008, XmiTrack.MidiOriginalKnight)]
    [InlineData(MusicTrack.BarbarianCastle, XmiTrack.Midi0009, XmiTrack.MidiOriginalBarbarian)]
    [InlineData(MusicTrack.WizardCastle, XmiTrack.Midi0010, XmiTrack.MidiOriginalWizard)]
    public void TheCastleThemesDependOnTheExpansion(MusicTrack music, XmiTrack withExpansion, XmiTrack withoutExpansion)
    {
        // 参考：xmi.cpp:79-88，Succession Wars 的 HEROES2.AGG 把城堡主题放错了文件。
        Assert.Equal(withExpansion, XmiCatalog.FromMusicTrack(music, priceOfLoyaltySupported: true));
        Assert.Equal(withoutExpansion, XmiCatalog.FromMusicTrack(music, priceOfLoyaltySupported: false));
    }

    [Fact]
    public void TheSorceressThemeIsSharedInBothReleases()
    {
        // 参考：xmi.cpp:76-78，原版根本没有巫师城的 XMI。
        Assert.Equal(XmiTrack.Midi0005, XmiCatalog.FromMusicTrack(MusicTrack.SorceressCastle, priceOfLoyaltySupported: true));
        Assert.Equal(XmiTrack.Midi0005, XmiCatalog.FromMusicTrack(MusicTrack.SorceressCastle, priceOfLoyaltySupported: false));
    }
}
