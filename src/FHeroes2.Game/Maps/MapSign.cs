using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Maps;

/// <summary>
/// A sign or a bottle with a message, the reference's <c>MapSign</c> (<c>maps_objects.h:145</c>), read
/// from its information block by <c>MapSign::LoadFromMP2()</c> (<c>maps_objects.cpp:286</c>).
/// </summary>
/// <remarks>
/// <para>
/// Both objects share one structure: byte 0 is <c>0x01</c>, bytes 1…8 are unused, and the message is a
/// NUL terminated string that runs from byte 9 to the end of the block — the block itself is the length
/// of the string, so the buffer never runs out.
/// </para>
/// <para>
/// <b>What is missing</b>: the reference fills an empty message with a random line (<c>setDefaultMessage()</c>,
/// <c>maps_objects.cpp:315</c>). The port has no random source in the map loader, so an empty message stays
/// empty. The message itself is a <see cref="LocalizedString"/> like the reference's, because the language tag
/// travels with the sign through a save file.
/// </para>
/// </remarks>
public sealed partial class MapSign : MapObject
{
    /// <summary>Smallest block that can be a sign, <c>MP2::MP2_SIGN_STRUCTURE_MIN_SIZE</c> (<c>mp2.h:41</c>).</summary>
    public const int MinimumStructureSize = 10;

    /// <summary>The first byte of a sign block, <c>data[0] == 0x01</c> (<c>maps_objects.cpp:289</c>).</summary>
    private const byte SignMarker = 0x01;

    /// <summary>Offset of the message, after the marker and the eight unused bytes.</summary>
    private const int MessageOffset = 9;

    private MapSign()
    {
    }

    /// <summary>The message of the sign, empty when the block carries none.</summary>
    public LocalizedString Message { get; private set; }

    /// <summary>
    /// Restores the stolen French characters of the message, <c>World::fixFrenchCharactersInStrings()</c>
    /// (<c>world.cpp:1296-1302</c>).
    /// </summary>
    internal void FixFrenchCharacters() => Message = new LocalizedString(MapTextCodec.FixFrenchCharacters(Message.Text), Message.Language);

    /// <summary>
    /// Reads the block of a sign or a bottle, <c>MapSign::LoadFromMP2()</c> (<c>maps_objects.cpp:286</c>).
    /// </summary>
    /// <remarks>
    /// The reference asserts the size and the marker and logs a warning before the caller skips the
    /// block (<c>world_loadmap.cpp:626</c>); the port reports the same decision through the return value.
    /// </remarks>
    /// <param name="data">Payload of the information block, without its size prefix.</param>
    /// <param name="tileIndex">Index of the tile the block belongs to.</param>
    /// <param name="sign">The sign when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="data"/> is a sign block.</returns>
    public static bool TryRead(ReadOnlySpan<byte> data, int tileIndex, out MapSign? sign)
    {
        sign = null;

        if (data.Length < MinimumStructureSize || data[0] != SignMarker)
        {
            return false;
        }

        sign = new MapSign
        {
            // An MP2 block carries no language tag: the text is whatever the map author typed.
            Message = new LocalizedString(MapTextCodec.Decode(data[MessageOffset..])),
        };

        sign.PlaceOn(tileIndex);

        return true;
    }
}
