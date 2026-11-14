namespace FHeroes2.Assets.Audio;

/// <summary>
/// Names of the sound effects stored inside the base archive. The original game keeps them as
/// headerless PCM bodies under the (always upper case) extension <c>.82M</c>, for example
/// <c>PICKUP01.82M</c>; <c>M82::GetString</c> in <c>agg/m82.cpp</c> is the authoritative list.
///
/// <para>
/// The port does not repeat the 300 entry table of the reference implementation: the caller passes
/// the name it wants (the constant of the sound it is about to play) and this type turns it into the
/// archive entry. <see cref="WaveSound.FromGameSound"/> turns the body into something playable.
/// </para>
/// </summary>
public static class GameSound
{
    /// <summary>Extension of the sound effect entries of the base archive.</summary>
    public const string ArchiveExtension = ".82M";

    /// <summary>
    /// Archive name of a sound effect, e.g. <c>pickup01</c> becomes <c>PICKUP01.82M</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The name is empty or contains characters that cannot appear in an archive name.</exception>
    public static string GetArchiveName(string soundName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(soundName);

        foreach (char character in soundName)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                throw new ArgumentException($"'{soundName}' is not a sound effect name.", nameof(soundName));
            }
        }

        return soundName.ToUpperInvariant() + ArchiveExtension;
    }

    /// <summary>
    /// Reads the PCM body of a sound effect from <paramref name="bundle"/>, or returns
    /// <see langword="null"/> when the archive does not contain it (a stripped demo, for example).
    /// </summary>
    public static byte[]? TryReadBody(AssetBundle bundle, string soundName)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        return bundle.TryRead(GetArchiveName(soundName));
    }
}
