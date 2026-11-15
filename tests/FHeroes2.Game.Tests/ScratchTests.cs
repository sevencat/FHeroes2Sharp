using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

public sealed class ScratchTests
{
    [Fact]
    public void AuditCatalogObjectTypes()
    {
        List<string> found = [];

        foreach (ObjectGroup group in Enum.GetValues<ObjectGroup>())
        {
            if (group == ObjectGroup.GroupCount)
            {
                continue;
            }

            IReadOnlyList<ObjectInfo> objects = MapObjectCatalog.GetObjectsByGroup(group);

            for (int i = 0; i < objects.Count; ++i)
            {
                ObjectInfo info = objects[i];

                Check(info.ObjectType, $"{group}/{i} main {info.ObjectType}", found);
            }
        }

        File.WriteAllLines(@"F:\code\FHeroes2Sharp\catalog-audit.txt", found.Distinct().OrderBy(text => text, StringComparer.Ordinal));
        Assert.Empty(found);
    }

    private static void Check(Mp2ObjectType type, string where, List<string> found)
    {
        if (type == Mp2ObjectType.None || Mp2ObjectTypes.IsActionObject(type))
        {
            return;
        }

        if ((int)type < 0 || (int)type > 1024)
        {
            found.Add($"{where}: WRAPPED {(int)type}");

            return;
        }

        if (Mp2ObjectTypes.CanBeActionObject(type))
        {
            found.Add($"{where}: non-action {type}");
        }
    }
}
