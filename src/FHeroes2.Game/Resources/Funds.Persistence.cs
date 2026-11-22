using FHeroes2.Core.Io;

namespace FHeroes2.Game.Resources;

/// <summary>
/// Reading and writing a resource bag, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>Funds</c>
/// (<c>resource.cpp:660-689</c>).
/// </summary>
/// <remarks>
/// <para>
/// Seven <c>int32_t</c> in the order wood, mercury, ore, sulfur, crystal, gems, gold — which is not the order the
/// members are declared in (<c>resource.h:128-134</c> lists ore first) and not the order of the port's constructor
/// either. The file order is what is copied here.
/// </para>
/// <para>
/// The reference keeps the amounts as <c>int32_t</c> even though a negative amount only ever means "takes away" in a
/// daily event; the port's fields are <see cref="int"/> as well, so the cast is a no-op but stays explicit.
/// </para>
/// </remarks>
public partial struct Funds
{
    /// <summary>How many bytes a bag takes in a save file.</summary>
    public const int SerializedSize = 7 * sizeof(int);

    /// <summary>
    /// Writes the bag, <c>operator&lt;&lt;</c> (<c>resource.cpp:660</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(Wood);
        stream.Write(Mercury);
        stream.Write(Ore);
        stream.Write(Sulfur);
        stream.Write(Crystal);
        stream.Write(Gems);
        stream.Write(Gold);
    }

    /// <summary>
    /// Reads a bag, <c>operator&gt;&gt;</c> (<c>resource.cpp:672</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>
    /// The bag; a stream that ran out leaves the fields zero and its <c>Fail</c> flag set, like every other reader.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static Funds ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Funds funds = default;

        funds.Wood = stream.ReadInt32();
        funds.Mercury = stream.ReadInt32();
        funds.Ore = stream.ReadInt32();
        funds.Sulfur = stream.ReadInt32();
        funds.Crystal = stream.ReadInt32();
        funds.Gems = stream.ReadInt32();
        funds.Gold = stream.ReadInt32();

        return funds;
    }
}
