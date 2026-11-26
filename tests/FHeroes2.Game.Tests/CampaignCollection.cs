using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The campaign tests share two process-wide singletons: <c>CampaignSaveData.Get()</c> and
/// <c>Settings.Get().Players</c>. They must not run side by side.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CampaignCollection
{
    /// <summary>The collection name to put on the campaign test classes.</summary>
    public const string Name = "Campaign";
}
