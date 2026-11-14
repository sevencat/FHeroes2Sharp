namespace FHeroes2.Assets.Maps;

/// <summary>
/// A color of a kingdom on the map, used as a bit set the same way <c>PlayerColor</c> /
/// <c>PlayerColorsSet</c> are in <c>color.h</c>. The numeric values are part of the MP2 format and
/// must not be renumbered.
/// </summary>
[Flags]
public enum PlayerColor : byte
{
    /// <summary>No color at all.</summary>
    None = 0x00,

    /// <summary>Blue player (index 0).</summary>
    Blue = 0x01,

    /// <summary>Green player (index 1).</summary>
    Green = 0x02,

    /// <summary>Red player (index 2).</summary>
    Red = 0x04,

    /// <summary>Yellow player (index 3).</summary>
    Yellow = 0x08,

    /// <summary>Orange player (index 4).</summary>
    Orange = 0x10,

    /// <summary>Purple player (index 5).</summary>
    Purple = 0x20,
}

/// <summary>
/// Order and helpers of the playable colors, mirroring <c>Color::allPlayerColors</c>,
/// <c>Color::Count</c>, <c>Color::GetIndex</c> and <c>Color::IndexToColor</c>.
/// </summary>
public static class PlayerColors
{
    /// <summary>All six playable colors, in the order the MP2 format stores them.</summary>
    public const PlayerColor All =
        PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red | PlayerColor.Yellow | PlayerColor.Orange | PlayerColor.Purple;

    /// <summary>Colors in the order dictated by the map format (blue, green, red, yellow, orange, purple).</summary>
    public static IReadOnlyList<PlayerColor> Ordered { get; } =
    [
        PlayerColor.Blue,
        PlayerColor.Green,
        PlayerColor.Red,
        PlayerColor.Yellow,
        PlayerColor.Orange,
        PlayerColor.Purple,
    ];

    /// <summary>Number of playable colors in <paramref name="colors"/>; unknown bits are ignored.</summary>
    public static int Count(PlayerColor colors)
    {
        int count = 0;
        foreach (PlayerColor color in Ordered)
        {
            if ((colors & color) != 0)
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>Colors of <paramref name="colors"/> as a list, in map order.</summary>
    public static IReadOnlyList<PlayerColor> ToList(PlayerColor colors)
    {
        List<PlayerColor> result = [];
        foreach (PlayerColor color in Ordered)
        {
            if ((colors & color) != 0)
            {
                result.Add(color);
            }
        }

        return result;
    }

    /// <summary>Index of a single <paramref name="color"/> (0 for blue … 5 for purple).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not exactly one playable color.</exception>
    public static int IndexOf(PlayerColor color)
    {
        for (int index = 0; index < Ordered.Count; ++index)
        {
            if (Ordered[index] == color)
            {
                return index;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(color), color, "The value is not a single playable color.");
    }

    /// <summary>Single color of <paramref name="index"/>; the inverse of <see cref="IndexOf"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside 0…5.</exception>
    public static PlayerColor FromIndex(int index)
    {
        if (index < 0 || index >= Ordered.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "A player index must be within 0…5.");
        }

        return Ordered[index];
    }

    /// <summary>Comma separated English names, e.g. <c>Blue, Red</c>; empty for <see cref="PlayerColor.None"/>.</summary>
    public static string Describe(PlayerColor colors)
    {
        List<string> names = [];
        foreach (PlayerColor color in ToList(colors))
        {
            names.Add(color.ToString());
        }

        return string.Join(", ", names);
    }
}
