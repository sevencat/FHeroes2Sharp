using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Serialises the tests that flip <see cref="Diagnostics.TextSupport"/>, which is a process-wide switch.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TextSupportCollection
{
    /// <summary>The name of the collection.</summary>
    public const string Name = "TextSupport";
}
