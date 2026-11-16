namespace FHeroes2.Assets.Audio;

/// <summary>
/// The lookups of the <see cref="XmiTrack"/> ids, the <c>XMI::GetString</c> and <c>XMI::FromMUS</c> of
/// <c>src/fheroes2/agg/xmi.cpp</c>.
/// </summary>
/// <remarks>
/// 参考：实现分别在 <c>xmi.cpp:55</c> 与 <c>xmi.cpp:62</c>。
/// 名字表由 <c>tools/scripts/GenerateAggEnums.ps1</c> 从 <c>xmimap</c> 生成——它不是「成员名加后缀」，六支
/// <c>MidiOriginal*</c> 指向别人家的文件，表里的注释抄自参考，因为那是原版游戏的 bug 而不是规则。
/// </remarks>
public static class XmiCatalog
{
    /// <summary>
    /// The archive entry name of a MIDI track, the reference's <c>XMI::GetString</c>.
    /// </summary>
    /// <param name="track">The track.</param>
    /// <returns>
    /// The file name, or <c>UNKNOWN</c> for <see cref="XmiTrack.Unknown"/> and for any value outside the
    /// enumeration.
    /// </returns>
    /// <remarks>参考：<c>xmi.cpp:55</c>——越界返回 <c>xmimap[UNKNOWN].string</c>，也就是 <c>"UNKNOWN"</c>。</remarks>
    public static string GetFileName(XmiTrack track) =>
        track > XmiTrack.Unknown && track <= XmiTrack.MidiOriginalNecromancer ? XmiFileNames.ById[(int)track] : XmiFileNames.ById[(int)XmiTrack.Unknown];

    /// <summary>
    /// The MIDI track that carries a music track, the reference's <c>XMI::FromMUS( track, expansion )</c>.
    /// </summary>
    /// <param name="music">The music track the game wants to play.</param>
    /// <param name="priceOfLoyaltySupported">
    /// The reference's <c>expansion</c> argument: an expansion installation gets the <c>MIDI*</c> renditions of
    /// the castle themes, a Succession Wars one keeps the <c>MidiOriginal*</c> aliases, because the original
    /// release shipped the castle themes in the wrong files.
    /// </param>
    /// <returns>
    /// The track, or <see cref="XmiTrack.Unknown"/> for the tracks the reference gives no branch (the new week
    /// and month screens, the campaign screens, the puzzle map...).
    /// </returns>
    /// <remarks>参考：<c>xmi.cpp:62</c>，全部十七支 <c>case</c> 照搬。</remarks>
    public static XmiTrack FromMusicTrack(MusicTrack music, bool priceOfLoyaltySupported = true) => music switch
    {
        MusicTrack.Battle1 => XmiTrack.Midi0002,
        MusicTrack.Battle2 => XmiTrack.Midi0003,
        MusicTrack.Battle3 => XmiTrack.Midi0004,
        // Sorceress didn't have own XMI file in original release unfortunately
        MusicTrack.SorceressCastle => XmiTrack.Midi0005,
        MusicTrack.WarlockCastle => priceOfLoyaltySupported ? XmiTrack.Midi0006 : XmiTrack.MidiOriginalWarlock,
        MusicTrack.NecromancerCastle => priceOfLoyaltySupported ? XmiTrack.Midi0007 : XmiTrack.MidiOriginalNecromancer,
        MusicTrack.KnightCastle => priceOfLoyaltySupported ? XmiTrack.Midi0008 : XmiTrack.MidiOriginalKnight,
        MusicTrack.BarbarianCastle => priceOfLoyaltySupported ? XmiTrack.Midi0009 : XmiTrack.MidiOriginalBarbarian,
        MusicTrack.WizardCastle => priceOfLoyaltySupported ? XmiTrack.Midi0010 : XmiTrack.MidiOriginalWizard,
        MusicTrack.Lava => XmiTrack.Midi0011,
        MusicTrack.Desert => XmiTrack.Midi0013,
        MusicTrack.Snow => XmiTrack.Midi0014,
        MusicTrack.Swamp => XmiTrack.Midi0015,
        MusicTrack.Dirt => XmiTrack.Midi0017,
        MusicTrack.Grass => XmiTrack.Midi0018,
        MusicTrack.MainMenu => XmiTrack.Midi0042,
        MusicTrack.Victory => XmiTrack.Midi0043,
        _ => XmiTrack.Unknown,
    };
}
