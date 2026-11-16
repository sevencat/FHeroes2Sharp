using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using FHeroes2.Assets.H2D;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.H2D;

/// <summary>
/// The images fheroes2 adds on top of the original game data, the namespace <c>h2d</c> of
/// <c>src/fheroes2/h2d/h2d.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The archive is loaded once per run and its content is checked against the list of files the code expects, so an
/// outdated <c>resurrection.h2d</c> fails loudly at start up instead of producing a missing sprite somewhere in the
/// middle of a battle. The reference resolves the file through <c>Settings::findFile</c>; the port has no file search,
/// so the composition root hands over the path of <c>files/data/resurrection.h2d</c>.
/// </para>
/// <para>
/// Like every other gateway of this layer the instance is published through <see cref="Current"/>, which only the
/// composition root assigns. Callers that have no reason to care about the archive use
/// <see cref="TryReadImage(string, out Sprite?)"/>; it answers false when the archive is not loaded at all, which is how
/// the reference's global reader behaves before its initialiser runs.
/// </para>
/// </remarks>
public sealed class H2DResources : IDisposable
{
    /// <summary>The only archive name the game reads.</summary>
    public const string FileName = "resurrection.h2d";

    /// <summary>Where the archive lives, relative to the game data directory.</summary>
    public const string RelativePath = "files/data/resurrection.h2d";

    /// <summary>The reference's <c>resurrectionH2DFileListSample</c>, in the order the source lists it.</summary>
    private static readonly string[] Sample =
    [
        "abandoned_mine_crack.image",
        "abandoned_mine_desert.image",
        "abandoned_mine_lava.image",
        "abandoned_mine_rock.image",
        "abandoned_mine_snow.image",
        "abandoned_mine_swamp.image",
        "adventure-map-grass-cave-diff-01.image",
        "adventure-map-grass-cave-diff-02.image",
        "barbarian_castle_captain_quarter_left_side.image",
        "barrel_animation_0.image",
        "barrel_animation_1.image",
        "barrel_animation_2.image",
        "barrel_animation_3.image",
        "barrel_animation_4.image",
        "barrel_animation_5.image",
        "black_cat.image",
        "black_cat_animation_0.image",
        "black_cat_animation_1.image",
        "black_cat_animation_2.image",
        "black_cat_animation_3.image",
        "black_cat_animation_4.image",
        "black_cat_animation_5.image",
        "book_corner.image",
        "circular_stone_liths_center.image",
        "circular_stone_liths_left.image",
        "circular_stone_liths_top.image",
        "graphics_option_icon.image",
        "hotkeys_option_icon.image",
        "market_icon_evil.image",
        "market_icon_good.image",
        "keyboard_button_pressed_evil.image",
        "keyboard_button_pressed_good.image",
        "keyboard_button_released_evil.image",
        "keyboard_button_released_good.image",
        "knight_castle_left_farm.image",
        "lean-to-diff-part1.image",
        "lean-to-diff-part2.image",
        "main_menu_editor_highlighted_button.image",
        "main_menu_editor_icon.image",
        "main_menu_editor_pressed_button.image",
        "main_menu_editor_released_button.image",
        "mass-dispel-contour.image",
        "missing_sphinx_part.image",
        "monocle.image",
        "observation_tower_desert_bottom_part.image",
        "observation_tower_desert_right_part.image",
        "observation_tower_generic_bottom_part.image",
        "observation_tower_snow_bottom_part.image",
        "observation_tower_snow_right_part.image",
        "observation_tower_snow_top_part.image",
        "petrification_spell_icon.image",
        "petrification_spell_icon_mini.image",
        "resolution_option_icon.image",
        "sorceress_castle_captain_quarter_left_side.image",
        "swordsman_walking_frame_extra_part.image",
        "swordsman_walking_frame_extra_part_mask.image",
        "toggle-all-off-pressed.image",
        "toggle-all-off-released.image",
        "toggle-all-on-pressed.image",
        "toggle-all-on-released.image",
        "townbkg2_fix.image",
        "twnsspec_fix.image",
        "wizard_bay_diff_to_twnzdock.image",
        "wizard_bay_diff_to_twnzdock_animation_0.image",
        "wizard_bay_diff_to_twnzdock_animation_1.image",
        "wizard_bay_diff_to_twnzdock_animation_2.image",
        "wizard_bay_diff_to_twnzdock_animation_3.image",
        "wizard_bay_diff_to_twnzdock_animation_4.image",
    ];

    /// <summary>
    /// The names the current version of the archive must hold, <c>resurrectionH2DFileListSample</c>.
    /// </summary>
    /// <remarks>
    /// The reference compares a <c>std::set</c> with the archive's names, and <c>std::string::operator&lt;</c> compares
    /// bytes as unsigned chars; an ordinal comparison of the same ASCII names gives the same order and the same answer.
    /// </remarks>
    public static FrozenSet<string> SampleFileNames { get; } = Sample.ToFrozenSet(StringComparer.Ordinal);

    private H2DResources(H2DArchive archive) => Archive = archive;

    /// <summary>
    /// The archive loaded by the composition root, or <see langword="null"/> when the game runs without one.
    /// </summary>
    public static H2DResources? Current { get; set; }

    /// <summary>The loaded archive.</summary>
    public H2DArchive Archive { get; }

    /// <summary>The entries of the archive, in ordinal order.</summary>
    public IReadOnlyCollection<string> FileNames => Archive.FileNames;

    /// <summary>Builds the path of the archive inside a game data directory, <c>files/data/resurrection.h2d</c>.</summary>
    /// <param name="dataDirectory">The directory that holds <c>files</c>.</param>
    /// <returns>The full path.</returns>
    public static string GetDefaultPath(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        return Path.Combine(dataDirectory, "files", "data", FileName);
    }

    /// <summary>
    /// Opens the archive and checks its content, <c>H2DInitializer::H2DInitializer()</c>.
    /// </summary>
    /// <param name="path">The archive to read.</param>
    /// <returns>The resources; the caller owns them.</returns>
    /// <exception cref="FileNotFoundException">The archive is not there.</exception>
    /// <exception cref="BinaryFormatException">The archive cannot be opened.</exception>
    /// <exception cref="InvalidOperationException">Its content does not match <see cref="SampleFileNames"/>.</exception>
    public static H2DResources Open(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The '{FileName}' file was not found.", path);
        }

        H2DArchive archive = H2DArchive.Open(path);

        if (archive.Count != SampleFileNames.Count || archive.FileNames.Any(name => !SampleFileNames.Contains(name)))
        {
            archive.Dispose();

            throw new InvalidOperationException(
                $"The list of files contained in '{path}' does not match the sample. Make sure that you are using the " +
                $"latest version of the '{FileName}' file.");
        }

        return new H2DResources(archive);
    }

    /// <summary>Whether the loaded archive has an entry with this name.</summary>
    /// <param name="name">The entry name, case sensitive.</param>
    /// <returns>True when the entry is there.</returns>
    public bool Contains(string name) => Archive.Contains(name);

    /// <summary>Reads one sprite from this archive, <c>h2d::readImage</c>.</summary>
    /// <param name="name">The entry name.</param>
    /// <param name="image">The sprite that was built.</param>
    /// <returns>False when the entry is missing or does not hold a readable sprite.</returns>
    public bool TryReadImage(string name, [NotNullWhen(true)] out Sprite? image) =>
        H2DImageCodec.TryRead(Archive, name, out image);

    /// <summary>
    /// Reads one sprite from <see cref="Current"/>, the free function <c>h2d::readImage</c> of the reference.
    /// </summary>
    /// <param name="name">The entry name.</param>
    /// <param name="image">The sprite that was built.</param>
    /// <returns>False when no archive is loaded or the entry cannot be decoded.</returns>
    public static bool TryReadCurrentImage(string name, [NotNullWhen(true)] out Sprite? image)
    {
        H2DResources? resources = Current;

        if (resources is null)
        {
            image = null;
            return false;
        }

        return resources.TryReadImage(name, out image);
    }

    /// <summary>Reads an arbitrary entry of this archive, for the data files that are not sprites.</summary>
    /// <param name="name">The entry name.</param>
    /// <returns>The decompressed bytes, or <see langword="null"/> when the entry is missing.</returns>
    public byte[]? TryReadFile(string name) => Archive.TryRead(name);

    public void Dispose() => Archive.Dispose();
}
