namespace FHeroes2.Game.System;

/// <summary>
/// The formats a save file can be in, <c>enum SaveFileFormat : uint16_t</c>
/// (<c>save_format_version.h:25-62</c>).
/// </summary>
/// <remarks>
/// <para>
/// The numbers are the reference's and must not change: a save file stores one of them in its header, and two
/// builds only read each other's files when the number is understood. Every release that changes the layout of a
/// saved object adds a value at the top; the two entries at the bottom then say which value is written today and
/// how far back reading still works.
/// </para>
/// <para>
/// The port has no save-file reader yet, so nothing consumes the enum. It is here because the numbers belong to
/// the format rather than to the reader, and because a reader that guessed them later would be a reader that
/// guessed wrong.
/// </para>
/// </remarks>
public enum SaveFileFormat : ushort
{
    /// <summary>1.1.18 release, the oldest format of this list and the current one.</summary>
    Release1_1_18 = 10034,

    /// <summary>1.1.15 release.</summary>
    Release1_1_15 = 10033,

    /// <summary>1.1.11.1 release.</summary>
    Release1_1_11_1 = 10032,

    /// <summary>1.1.09 release.</summary>
    Release1_1_09 = 10031,

    /// <summary>1.1.08 release.</summary>
    Release1_1_08 = 10030,

    /// <summary>1.1.08, before its release.</summary>
    PreRelease1_1_08 = 10029,

    /// <summary>1.1.07 release.</summary>
    Release1_1_07 = 10028,

    /// <summary>1.1.06 release.</summary>
    Release1_1_06 = 10027,

    /// <summary>1.1.06, before its first pre-release.</summary>
    PrePreRelease1_1_06 = 10026,

    /// <summary>1.1.04 release.</summary>
    Release1_1_04 = 10025,

    /// <summary>1.1.03 release.</summary>
    Release1_1_03 = 10024,

    /// <summary>1.1.03, before its second pre-release.</summary>
    PreRelease2_1_1_03 = 10023,

    /// <summary>1.1.03, before its first pre-release.</summary>
    PreRelease1_1_1_03 = 10022,

    /// <summary>1.1.01 release.</summary>
    Release1_1_01 = 10021,

    /// <summary>1.1.01, before its release.</summary>
    PreRelease1_1_01 = 10020,

    /// <summary>1.1.00 release.</summary>
    Release1_1_00 = 10019,

    /// <summary>1.1.00, before its third pre-release.</summary>
    PreRelease3_1_1_00 = 10018,

    /// <summary>1.1.00, before its second pre-release.</summary>
    PreRelease2_1_1_00 = 10017,

    /// <summary>1.1.00, before its first pre-release.</summary>
    PreRelease1_1_00 = 10016,

    /// <summary>1.0.10 release.</summary>
    Release1_0_10 = 10015,

    /// <summary>1.0.09 release.</summary>
    Release1_0_09 = 10014,

    /// <summary>1.0.09, before its second pre-release.</summary>
    PreRelease2_1_0_09 = 10013,

    /// <summary>1.0.09, before its first pre-release.</summary>
    PreRelease1_1_0_09 = 10012,

    /// <summary>1.0.07 release.</summary>
    Release1_0_07 = 10011,

    /// <summary>1.0.05 release, the oldest format still readable.</summary>
    Release1_0_05 = 10010,
}

/// <summary>
/// The two roles <see cref="SaveFileFormat"/> plays, <c>LAST_SUPPORTED_FORMAT_VERSION</c> and
/// <c>CURRENT_FORMAT_VERSION</c> (<c>save_format_version.h:57-59</c>).
/// </summary>
public static class SaveFileFormatInfo
{
    /// <summary>The oldest format that can still be read, <c>LAST_SUPPORTED_FORMAT_VERSION</c>.</summary>
    public const SaveFileFormat LastSupported = SaveFileFormat.Release1_0_05;

    /// <summary>The format written today, <c>CURRENT_FORMAT_VERSION</c>.</summary>
    public const SaveFileFormat Current = SaveFileFormat.Release1_1_18;
}
