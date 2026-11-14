using FHeroes2.Assets;
using FHeroes2.Assets.Agg;
using FHeroes2.Assets.Cursor;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.AssetProbe;

/// <summary>
/// Command line inspector for the original game archives. It is intentionally dependency free
/// (no SDL, no window) so it can run in CI and diff the parser behaviour.
///
/// <code>
///   FHeroes2.AssetProbe list    [--data &lt;dir&gt;] [--agg &lt;path&gt;] [--limit &lt;n&gt;]
///   FHeroes2.AssetProbe info    [--data &lt;dir&gt;] [--agg &lt;path&gt;]
///   FHeroes2.AssetProbe extract &lt;NAME&gt; &lt;output&gt; [--data &lt;dir&gt;] [--agg &lt;path&gt;]
///   FHeroes2.AssetProbe sprites &lt;NAME.ICN&gt; &lt;output-dir&gt; [--palette &lt;NAME&gt;] [--format png|bmp]
///   FHeroes2.AssetProbe tiles   &lt;NAME.TIL&gt; &lt;output-dir&gt; [--palette &lt;NAME&gt;] [--format png|bmp]
///   FHeroes2.AssetProbe cursor  &lt;NAME.BMP&gt; &lt;output-dir&gt; [--palette &lt;NAME&gt;] [--format png|bmp]
///   FHeroes2.AssetProbe effects &lt;output-dir&gt; [--palette &lt;NAME&gt;]
///   FHeroes2.AssetProbe window  &lt;output-dir&gt; [--palette &lt;NAME&gt;]
///   FHeroes2.AssetProbe maps    [--data &lt;dir&gt;]
///   FHeroes2.AssetProbe map     &lt;file.mp2&gt; [--limit &lt;n&gt;]
///   FHeroes2.AssetProbe compare &lt;left&gt; &lt;right&gt; [--diff &lt;dir&gt;] [--ignore x,y,w,h]...
/// </code>
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        if (args[0] is "--help" or "-h" or "/?")
        {
            PrintUsage();
            return 0;
        }

        try
        {
            ProbeArguments arguments = ProbeArguments.Parse(args);
            return arguments.Command switch
            {
                "list" => List(arguments),
                "info" => Info(arguments),
                "extract" => Extract(arguments),
                "sprites" => ExportSprites(arguments),
                "tiles" => ExportTiles(arguments),
                "cursor" => ExportCursor(arguments),
                "effects" => ExportEffects(arguments),
                "window" => ExportWindow(arguments),
                "maps" => ListMaps(arguments),
                "map" => InspectMap(arguments),
                "compare" => Compare(arguments),
                _ => Unknown(arguments.Command),
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or BinaryFormatException
                or DirectoryNotFoundException or FileNotFoundException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static int List(ProbeArguments arguments)
    {
        using AssetArchive archive = OpenArchive(arguments);
        Console.WriteLine($"# {archive.Path}: {archive.Count} entries");

        IEnumerable<AssetEntry> entries = arguments.Limit is int limit
            ? archive.Entries.Take(limit)
            : archive.Entries;

        foreach (AssetEntry entry in entries)
        {
            Console.WriteLine($"{entry.Name}\t{entry.Offset}\t{entry.Size}");
        }

        return 0;
    }

    private static int Info(ProbeArguments arguments)
    {
        using AssetArchive archive = OpenArchive(arguments);

        ulong totalBytes = 0;
        Dictionary<string, int> byExtension = new(StringComparer.OrdinalIgnoreCase);
        foreach (AssetEntry entry in archive.Entries)
        {
            totalBytes += entry.Size;

            string extension = Path.GetExtension(entry.Name).TrimStart('.');
            byExtension[extension] = byExtension.GetValueOrDefault(extension) + 1;
        }

        Console.WriteLine($"Archive: {archive.Path}");
        Console.WriteLine($"Entries: {archive.Count}");
        Console.WriteLine($"Payload: {totalBytes} bytes");
        Console.WriteLine("Extensions:");
        foreach ((string extension, int count) in byExtension.OrderByDescending(pair => pair.Value))
        {
            Console.WriteLine($"  .{extension,-6} {count}");
        }

        return 0;
    }

    private static int Extract(ProbeArguments arguments)
    {
        if (arguments.Positional.Count != 2)
        {
            throw new ArgumentException("extract expects exactly two arguments: <NAME> <output>.");
        }

        string name = arguments.Positional[0];
        string output = arguments.Positional[1];

        using AssetArchive archive = OpenArchive(arguments);
        byte[]? data = archive.TryRead(name);
        if (data is null)
        {
            Console.Error.WriteLine($"error: '{name}' was not found in '{archive.Path}'.");
            return 3;
        }

        File.WriteAllBytes(output, data);
        Console.WriteLine($"Wrote {data.Length} bytes of '{name}' to '{Path.GetFullPath(output)}'.");
        return 0;
    }

    /// <summary>
    /// Decodes every sprite of an ICN entry, one file per sprite. PNG is the default and keeps
    /// "skip" transform entries transparent, so the images can be compared with the reference
    /// engine by eye; BMP writes the 8 bit indexed raster instead, composed over
    /// <c>--background</c> because an indexed bitmap has no alpha channel.
    /// </summary>
    private static int ExportSprites(ProbeArguments arguments)
    {
        (string name, string outputDirectory) = ParseExportArguments(arguments, "sprites", "<NAME.ICN>");

        using AssetArchive archive = OpenArchive(arguments);
        byte[]? data = archive.TryRead(name);
        if (data is null)
        {
            Console.Error.WriteLine($"error: '{name}' was not found in '{archive.Path}'.");
            return 3;
        }

        IcnSpriteFile icn = IcnSpriteFile.Parse(data);
        Palette palette = LoadPalette(archive, arguments.PaletteName);
        Directory.CreateDirectory(outputDirectory);

        string stem = Path.GetFileNameWithoutExtension(name);
        Console.WriteLine($"# {name}: {icn.Count} sprites, palette '{arguments.PaletteName}'");

        string extension = ExportExtension(arguments);
        for (int index = 0; index < icn.Count; ++index)
        {
            IcnHeader header = icn.Headers[index];
            Sprite sprite = icn.Sprites[index];
            string file = Path.Combine(outputDirectory, $"{stem}_{index:D3}{extension}");

            WriteSprite(file, sprite, palette, arguments);

            Console.WriteLine(string.Join(
                '\t',
                index.ToString(),
                $"{header.Width}x{header.Height}",
                $"{header.OffsetX},{header.OffsetY}",
                header.IsMonochromatic ? "mono" : "color",
                sprite.HasTransformLayer ? "transform" : "single",
                Path.GetFileName(file)));
        }

        Console.WriteLine(
            $"Wrote {icn.Count} {arguments.Format.ToUpperInvariant()} files to '{Path.GetFullPath(outputDirectory)}'.");
        return 0;
    }

    /// <summary>Decodes every tile of a TIL entry and writes it as one image per tile.</summary>
    private static int ExportTiles(ProbeArguments arguments)
    {
        (string name, string outputDirectory) = ParseExportArguments(arguments, "tiles", "<NAME.TIL>");

        using AssetArchive archive = OpenArchive(arguments);
        byte[]? data = archive.TryRead(name);
        if (data is null)
        {
            Console.Error.WriteLine($"error: '{name}' was not found in '{archive.Path}'.");
            return 3;
        }

        TilFile til = TilFile.Parse(data);
        Palette palette = LoadPalette(archive, arguments.PaletteName);
        Directory.CreateDirectory(outputDirectory);

        string stem = Path.GetFileNameWithoutExtension(name);
        Console.WriteLine($"# {name}: {til.Count} tiles of {til.Width}x{til.Height}, palette '{arguments.PaletteName}'");

        string extension = ExportExtension(arguments);
        for (int index = 0; index < til.Count; ++index)
        {
            string file = Path.Combine(outputDirectory, $"{stem}_{index:D3}{extension}");

            WriteImage(file, til.Images[index], palette, arguments);

            Console.WriteLine($"{index}\t{til.Width}x{til.Height}\t{Path.GetFileName(file)}");
        }

        Console.WriteLine(
            $"Wrote {til.Count} {arguments.Format.ToUpperInvariant()} files to '{Path.GetFullPath(outputDirectory)}'.");
        return 0;
    }

    /// <summary>
    /// Decodes a monochrome cursor entry (<c>ADVMBWnn.BMP</c>, <c>CMSEBWnn.BMP</c>,
    /// <c>SPELBWnn.BMP</c>) and writes it as one image. These archive entries are cursor bitmaps in
    /// the engine's own format, not Windows bitmaps.
    /// </summary>
    private static int ExportCursor(ProbeArguments arguments)
    {
        (string name, string outputDirectory) = ParseExportArguments(arguments, "cursor", "<NAME.BMP>");

        using AssetArchive archive = OpenArchive(arguments);
        byte[]? data = archive.TryRead(name);
        if (data is null)
        {
            Console.Error.WriteLine($"error: '{name}' was not found in '{archive.Path}'.");
            return 3;
        }

        Sprite cursor = MonoCursorFile.Parse(data);
        Palette palette = LoadPalette(archive, arguments.PaletteName);
        Directory.CreateDirectory(outputDirectory);

        int total = cursor.Width * cursor.Height;
        int opaque = cursor.Transform is null ? total : cursor.Transform.Count(entry => entry != 1);
        string stem = Path.GetFileNameWithoutExtension(name);
        string file = Path.Combine(outputDirectory, $"{stem}{ExportExtension(arguments)}");

        WriteSprite(file, cursor, palette, arguments);

        Console.WriteLine(
            $"# {name}: {cursor.Width}x{cursor.Height} monochrome cursor, palette '{arguments.PaletteName}'");
        Console.WriteLine($"{stem}\t{opaque}/{total} opaque\t{Path.GetFileName(file)}");
        Console.WriteLine(
            $"Wrote 1 {arguments.Format.ToUpperInvariant()} file to '{Path.GetFullPath(outputDirectory)}'.");
        return 0;
    }

    /// <summary>File extension of the exported images, derived from <c>--format</c>.</summary>
    private static string ExportExtension(ProbeArguments arguments) =>
        arguments.Format == "bmp" ? ".bmp" : ".png";

    /// <summary>
    /// Renders the dialog effects on synthetic input, the C# half of
    /// <c>fh2_reference effects</c>. The files are always 8 bit BMP because that is what the
    /// reference exporter writes; <c>tools/scripts/CompareEffectsWithReference.ps1</c> hashes both
    /// output folders.
    /// </summary>
    private static int ExportEffects(ProbeArguments arguments)
    {
        if (arguments.Positional.Count != 1)
        {
            throw new ArgumentException("'effects' expects exactly one argument: <output-dir>.");
        }

        string outputDirectory = arguments.Positional[0];

        using AssetArchive archive = OpenArchive(arguments);
        Palette palette = LoadPalette(archive, arguments.PaletteName);
        Directory.CreateDirectory(outputDirectory);

        foreach ((string name, PixelBuffer image) in EffectsPreview.Render())
        {
            string file = Path.Combine(outputDirectory, name);
            File.WriteAllBytes(file, BmpWriter.EncodeIndexed(image, palette));
            Console.WriteLine($"{name}\t{image.Width}x{image.Height}");
        }

        Console.WriteLine(
            $"Wrote 8 BMP files to '{Path.GetFullPath(outputDirectory)}' using palette '{arguments.PaletteName}'.");
        return 0;
    }

    /// <summary>
    /// Renders a <see cref="StandardWindow"/> per case with the artwork of the archive.
    /// </summary>
    private static int ExportWindow(ProbeArguments arguments)
    {
        if (arguments.Positional.Count != 1)
        {
            throw new ArgumentException("'window' expects exactly one argument: <output-dir>.");
        }

        string outputDirectory = arguments.Positional[0];

        using AssetArchive archive = OpenArchive(arguments);
        Palette palette = LoadPalette(archive, arguments.PaletteName);

        StandardWindowChrome? chrome = StandardWindowChrome.TryCreate(
            ReadSprites(archive, StandardWindowChrome.HorizontalAssetName),
            ReadSprites(archive, StandardWindowChrome.VerticalAssetName),
            ReadSprites(archive, StandardWindowChrome.BackgroundAssetName));

        if (chrome is null)
        {
            Console.Error.WriteLine("error: the window artwork is missing from the archive.");
            return 3;
        }

        Directory.CreateDirectory(outputDirectory);

        IReadOnlyList<(string Name, PixelBuffer Image)> images = WindowPreview.Render(chrome);

        foreach ((string name, PixelBuffer image) in images)
        {
            string file = Path.Combine(outputDirectory, name);
            File.WriteAllBytes(file, BmpWriter.EncodeIndexed(image, palette));
            Console.WriteLine($"{name}\t{image.Width}x{image.Height}");
        }

        Console.WriteLine(
            $"Wrote {images.Count} BMP files to '{Path.GetFullPath(outputDirectory)}' using palette '{arguments.PaletteName}'.");
        return 0;
    }

    /// <summary>Decodes an ICN entry of the archive, or returns <see langword="null"/> when it is missing.</summary>
    private static IReadOnlyList<Sprite>? ReadSprites(AssetArchive archive, string name)
    {
        byte[]? data = archive.TryRead(name);
        return data is null ? null : IcnSpriteFile.Parse(data).Sprites;
    }

    /// <summary>
    /// Writes an indexed image. PNG keeps the palette colours with full alpha, BMP keeps the
    /// palette itself and the raw image layer.
    /// </summary>
    private static void WriteImage(string path, PixelBuffer image, Palette palette, ProbeArguments arguments)
    {
        File.WriteAllBytes(
            path,
            arguments.Format == "bmp"
                ? BmpWriter.EncodeIndexed(image, palette)
                : PngWriter.EncodeRgba(image, palette));
    }

    /// <summary>
    /// Writes a sprite. PNG exports the transform layer as transparency, while BMP - like
    /// <c>fheroes2::Save()</c> - composes the sprite over <c>--background</c> (palette index 0 by
    /// default), because an indexed bitmap has no alpha channel.
    /// </summary>
    private static void WriteSprite(string path, Sprite sprite, Palette palette, ProbeArguments arguments)
    {
        File.WriteAllBytes(
            path,
            arguments.Format == "bmp"
                ? BmpWriter.EncodeSprite(sprite, palette, arguments.Background ?? 0)
                : PngWriter.EncodeRgba(sprite.Image, palette, sprite.Transform));
    }

    /// <summary>
    /// Compares two PNG files, or two directories of PNG files with matching names, pixel by pixel.
    /// Areas listed with <c>--ignore</c> are skipped, which is how a screenshot with a live FPS
    /// counter can still act as a pixel baseline.
    /// Returns 0 when everything matches and 3 when a size or a pixel differs, so the tool can gate a
    /// script that checks our decoder against the reference engine output.
    /// </summary>
    private static int Compare(ProbeArguments arguments)
    {
        if (arguments.Positional.Count != 2)
        {
            throw new ArgumentException("compare expects two paths: <left> <right> [--diff <dir>].");
        }

        string left = arguments.Positional[0];
        string right = arguments.Positional[1];
        string? diffDirectory = arguments.DiffDirectory;
        IReadOnlyList<Rect> ignored = arguments.IgnoreAreas;

        if (diffDirectory is not null)
        {
            Directory.CreateDirectory(diffDirectory);
        }

        if (Directory.Exists(left) && Directory.Exists(right))
        {
            return CompareDirectories(left, right, diffDirectory, ignored);
        }

        if (!File.Exists(left))
        {
            throw new FileNotFoundException($"'{left}' does not exist.");
        }

        if (!File.Exists(right))
        {
            throw new FileNotFoundException($"'{right}' does not exist.");
        }

        return CompareFiles(left, right, diffDirectory, ignored) ? 0 : 3;
    }

    private static int CompareDirectories(
        string left, string right, string? diffDirectory, IReadOnlyList<Rect> ignored)
    {
        Dictionary<string, string> counterparts = new(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(right, "*.png", SearchOption.TopDirectoryOnly))
        {
            counterparts[Path.GetFileName(file)] = file;
        }

        string[] files = Directory
            .EnumerateFiles(left, "*.png", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            throw new FileNotFoundException($"'{left}' contains no PNG files.");
        }

        int identical = 0;
        int different = 0;

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (!counterparts.Remove(name, out string? counterpart))
            {
                Console.WriteLine($"{name}\tmissing\tnot found in '{right}'");
                ++different;
            }
            else if (CompareFiles(file, counterpart, diffDirectory, ignored))
            {
                ++identical;
            }
            else
            {
                ++different;
            }
        }

        foreach (string name in counterparts.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{name}\tmissing\tnot found in '{left}'");
            ++different;
        }

        Console.WriteLine($"# {identical} identical, {different} different");
        return different == 0 ? 0 : 3;
    }

    private static bool CompareFiles(
        string leftPath, string rightPath, string? diffDirectory, IReadOnlyList<Rect> ignored)
    {
        PngImage left = ReadPng(leftPath);
        PngImage right = ReadPng(rightPath);
        string name = Path.GetFileName(leftPath);

        if (left.Width != right.Width || left.Height != right.Height)
        {
            Console.WriteLine($"{name}\tsize mismatch\t{left.Width}x{left.Height} vs {right.Width}x{right.Height}");
            return false;
        }

        int differing = 0;
        int ignoredPixels = 0;
        int maxDelta = 0;
        int firstX = -1;
        int firstY = -1;
        byte[] diff = new byte[left.Rgba.Length];

        for (int y = 0; y < left.Height; ++y)
        {
            for (int x = 0; x < left.Width; ++x)
            {
                int target = ((y * left.Width) + x) * 4;
                RgbaColor expected = left[x, y];

                if (IsIgnored(ignored, x, y))
                {
                    ++ignoredPixels;

                    // Keep the baseline pixel in the diff image so that the excluded area is obvious.
                    diff[target] = expected.R;
                    diff[target + 1] = expected.G;
                    diff[target + 2] = expected.B;
                    diff[target + 3] = expected.A;
                    continue;
                }

                RgbaColor actual = right[x, y];
                int delta = Math.Max(
                    Math.Max(Math.Abs(expected.R - actual.R), Math.Abs(expected.G - actual.G)),
                    Math.Max(Math.Abs(expected.B - actual.B), Math.Abs(expected.A - actual.A)));

                if (delta == 0)
                {
                    diff[target] = expected.R;
                    diff[target + 1] = expected.G;
                    diff[target + 2] = expected.B;
                    diff[target + 3] = expected.A;
                    continue;
                }

                ++differing;
                maxDelta = Math.Max(maxDelta, delta);

                if (firstX < 0)
                {
                    firstX = x;
                    firstY = y;
                }

                // Mark the mismatch in magenta so it stands out in an image viewer.
                diff[target] = 255;
                diff[target + 2] = 255;
                diff[target + 3] = 255;
            }
        }

        string ignoredNote = ignoredPixels > 0 ? $"\t{ignoredPixels} pixels ignored" : string.Empty;

        if (differing == 0)
        {
            Console.WriteLine($"{name}\tidentical\t{left.Width}x{left.Height}{ignoredNote}");
            return true;
        }

        Console.WriteLine(
            $"{name}\t{differing} of {left.Width * left.Height} pixels differ\tmax channel delta {maxDelta}" +
            $"\tfirst at {firstX},{firstY}{ignoredNote}");

        if (diffDirectory is not null)
        {
            string file = Path.Combine(diffDirectory, $"diff_{name}");
            File.WriteAllBytes(file, PngWriter.Encode(new PngImage(left.Width, left.Height, diff)));
            Console.WriteLine($"{name}\tdiff\t{Path.GetFileName(file)}");
        }

        return false;
    }

    private static bool IsIgnored(IReadOnlyList<Rect> ignored, int x, int y)
    {
        foreach (Rect area in ignored)
        {
            if (area.Contains(new Point(x, y)))
            {
                return true;
            }
        }

        return false;
    }

    private static PngImage ReadPng(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return PngReader.Decode(stream);
    }

    private static (string Name, string OutputDirectory) ParseExportArguments(
        ProbeArguments arguments,
        string command,
        string namePlaceholder)
    {
        if (arguments.Positional.Count != 2)
        {
            throw new ArgumentException($"{command} expects exactly two arguments: {namePlaceholder} <output-dir>.");
        }

        return (arguments.Positional[0], arguments.Positional[1]);
    }

    /// <summary>Loads the palette entry, falling back to the grayscale ramp when it is missing.</summary>
    private static Palette LoadPalette(AssetArchive archive, string paletteName)
    {
        byte[]? raw = archive.TryRead(paletteName);
        if (raw is null)
        {
            Console.Error.WriteLine($"warning: palette '{paletteName}' was not found, falling back to grayscale.");
            return new Palette();
        }

        return Palette.FromGamePalette(raw);
    }

    private static int ListMaps(ProbeArguments arguments)
    {
        string dataDirectory = AssetLocator.Resolve(arguments.DataDirectory)
            ?? throw new DirectoryNotFoundException(
                "No game data directory found. Use --data <dir> or set " +
                $"{AssetLocator.DataDirectoryEnvironmentVariable}.");

        MapScanResult scan = MapCatalog.Scan([dataDirectory]);

        Console.WriteLine($"# {dataDirectory}: {scan.Maps.Count} playable maps, {scan.Rejected.Count} skipped");

        foreach (Mp2FileInfo map in scan.Maps)
        {
            Console.WriteLine(string.Join(
                '\t',
                Path.GetFileName(map.FilePath),
                map.Name,
                $"{map.Width}x{map.Height}",
                map.Difficulty,
                $"players {map.PlayerCount}: human {PlayerColors.Describe(map.HumanPlayers)}" +
                $" / computer {PlayerColors.Describe(map.ComputerPlayers)}",
                $"races {string.Join('/', map.PlayerRaces)}",
                $"win {map.VictoryCondition}",
                $"loss {map.LossCondition}"));
        }

        // The reference implementation rewrites the victory condition of this rare map layout after
        // looking at the hero sprites. The port defers that until the map loader exists; report how
        // many of the maps of this installation are affected.
        int deferred = scan.Maps.Count(map =>
            map.LossCondition == MapLossCondition.LoseHero
            && map.VictoryCondition == MapVictoryCondition.DefeatEveryone
            && map.HumanPlayerCount == 1);
        Console.WriteLine($"# maps with a deferred hero-loss victory rewrite: {deferred}");

        return 0;
    }

    /// <summary>
    /// Reads one map file at the tile level and prints what the loader found: the summary, the most
    /// frequent object types, the castle and capturable object tables, the longest add-on chain and
    /// the sizes of the information blocks. It is the quickest way to tell whether the MP2 structures
    /// are parsed at the right offsets.
    /// </summary>
    private static int InspectMap(ProbeArguments arguments)
    {
        if (arguments.Positional.Count != 1)
        {
            throw new ArgumentException("map expects exactly one argument: <file.mp2>.");
        }

        string file = arguments.Positional[0];
        Mp2Map map = Mp2Map.Read(file);

        Console.WriteLine($"# {map.Info.FilePath}");
        Console.WriteLine(map.GetSummary());

        int limit = arguments.Limit ?? 10;

        Console.WriteLine("Object types:");
        foreach (IGrouping<Mp2ObjectType, Mp2TileInfo> group in map.Tiles
                     .Where(tile => !tile.IsEmpty)
                     .GroupBy(tile => tile.ObjectType)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key.ToString(), StringComparer.Ordinal)
                     .Take(limit))
        {
            Console.WriteLine($"  {group.Key,-32} {group.Count()}");
        }

        Console.WriteLine($"Terrain images ({map.Tiles.Select(tile => tile.TerrainImageIndex).Distinct().Count()} distinct):");
        foreach (IGrouping<ushort, Mp2TileInfo> group in map.Tiles
                     .GroupBy(tile => tile.TerrainImageIndex)
                     .OrderByDescending(group => group.Count())
                     .Take(limit))
        {
            Console.WriteLine($"  image {group.Key,-5} {group.Count()} tiles");
        }

        Console.WriteLine("Terrain shapes:");
        for (int shape = 0; shape < 4; ++shape)
        {
            Console.WriteLine($"  shape {shape} {map.Tiles.Count(tile => tile.TerrainShape == shape)} tiles");
        }

        // One entry per ICN file the renderer has to open for this map, so a missing tile set shows
        // up here before it shows up as an invisible object on screen.
        Console.WriteLine(
            $"Object ICN types ({map.Tiles.Select(tile => tile.GroundIcnType).Concat(map.Tiles.Select(tile => tile.TopIcnType)).Distinct().Count(type => Mp2ObjectIcnTypes.IsDrawnAsObjectPart(type))} drawn):");
        foreach (IGrouping<Mp2ObjectIcnType, Mp2TileInfo> group in map.Tiles
                     .Where(tile => Mp2ObjectIcnTypes.IsDrawnAsObjectPart(tile.GroundIcnType))
                     .GroupBy(tile => tile.GroundIcnType)
                     .OrderByDescending(group => group.Count())
                     .Take(limit))
        {
            Console.WriteLine($"  {group.Key,-32} {group.Count()} tiles");
        }

        // Heroes come out of the tiles themselves (MINIHERO frame), so this list is complete without
        // parsing a single information block.
        Console.WriteLine($"Heroes ({map.Heroes.Count}):");
        foreach (Mp2MapHero hero in map.Heroes)
        {
            Console.WriteLine(
                $"  {hero.X},{hero.Y}  {hero.Color} {hero.Race}  MINIHERO {hero.SpriteIndex}");
        }

        Console.WriteLine($"Castles ({map.Castles.Count}):");
        foreach (Mp2CastlePosition castle in map.Castles)
        {
            Console.WriteLine($"  {castle.X},{castle.Y}  {castle.Race}  {(castle.HasFort ? "castle" : "town")}");
        }

        Console.WriteLine($"Capturable objects ({map.CaptureObjects.Count}):");
        foreach (Mp2CaptureObject captureObject in map.CaptureObjects)
        {
            Console.WriteLine(
                $"  {captureObject.X},{captureObject.Y}  {captureObject.ObjectType}  (raw {captureObject.RawType})");
        }

        int usedAddons = Math.Max(map.Addons.Count - 1, 0);
        int longestChain = map.Tiles.Max(tile => map.EnumerateAddonChain(tile).Count());
        Console.WriteLine($"Add-ons: {usedAddons} used, longest chain {longestChain}");

        Console.WriteLine($"Information blocks ({map.InfoBlocks.Count}):");
        foreach (IGrouping<int, Mp2InfoBlock> group in map.InfoBlocks
                     .GroupBy(block => block.Data.Length)
                     .OrderBy(group => group.Key))
        {
            Console.WriteLine($"  {group.Key} bytes: {group.Count()}");
        }

        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static AssetArchive OpenArchive(ProbeArguments arguments)
    {
        string? path = arguments.ArchivePath;

        if (path is null)
        {
            string? dataDirectory = AssetLocator.Resolve(arguments.DataDirectory)
                ?? throw new DirectoryNotFoundException(
                    "No game data directory found. Use --data <dir>, --agg <path> or set " +
                    $"{AssetLocator.DataDirectoryEnvironmentVariable}.");

            path = AssetLocator.FindArchive(dataDirectory, AssetLocator.BaseArchiveName)
                ?? throw new FileNotFoundException($"'{AssetLocator.BaseArchiveName}' not found in '{dataDirectory}'.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Archive '{path}' does not exist.");
        }

        return AssetArchive.Open(path);
    }

    private static void PrintUsage()
    {
        Console.WriteLine($"{BuildInfo.DisplayName} — asset probe");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  list    [--data <dir>] [--agg <path>] [--limit <n>]    Lists the entries.");
        Console.WriteLine("  info    [--data <dir>] [--agg <path>]                 Shows statistics.");
        Console.WriteLine("  extract <NAME> <output> [--data <dir>] [--agg <path>] Writes one entry to disk.");
        Console.WriteLine("  sprites <NAME.ICN> <dir> [--palette <NAME>]           Exports sprites.");
        Console.WriteLine("  tiles   <NAME.TIL> <dir> [--palette <NAME>]           Exports tiles.");
        Console.WriteLine("  cursor  <NAME.BMP> <dir> [--palette <NAME>]           Exports one monochrome cursor.");
        Console.WriteLine("  effects <dir> [--palette <NAME>]                     Renders the dialog effects preview.");
        Console.WriteLine("  window  <dir> [--palette <NAME>]                     Renders the standard dialog windows.");
        Console.WriteLine("  maps    [--data <dir>]                               Lists the playable map files.");
        Console.WriteLine("  map     <file.mp2> [--limit <n>]                     Dumps the tile level content of one map.");
        Console.WriteLine("  compare <left> <right> [--diff <dir>] [--ignore x,y,w,h]  Diffs PNG files or folders.");
        Console.WriteLine("                      --ignore skips an area (repeatable), for screenshots whose status");
        Console.WriteLine("                      bar shows a live FPS counter.");
        Console.WriteLine();
        Console.WriteLine("The palette defaults to KB.PAL, which is read from the same archive.");
        Console.WriteLine("--format png (default) or bmp selects the export format; PNG keeps transparent pixels,");
        Console.WriteLine("BMP composes sprites over --background <index> because an indexed bitmap has no alpha.");
        Console.WriteLine("compare exits with 0 when everything matches and 3 when a size or a pixel differs.");
        Console.WriteLine();
        Console.WriteLine($"The data directory is resolved from --data, {AssetLocator.DataDirectoryEnvironmentVariable}");
        Console.WriteLine("or a known local install (F:\\game\\fh2 during development).");
    }
}
