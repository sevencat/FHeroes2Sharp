using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The buttons of the editor panel out of <c>EDITBTNS.ICN</c>, including the ten frames the reference
/// draws on top of the thirty five of the game (<c>game_assets.cpp</c>, <c>case ICN::EDITBTNS</c>).
/// </summary>
/// <remarks>
/// <para>
/// The original archive only has the nine instrument buttons of the first and third rows, the six
/// system buttons, the four brush sizes and three spares. The second row of instruments (adventure
/// objects, kingdom objects, monsters) and the undo / redo arrows are assembled by <c>fheroes2</c> at
/// run time: the icon is lifted out of its own archive entry, its shadow is dropped, four
/// <see cref="ImageOps.UpdateShadow"/> passes add the lit top edge and the dark bottom edge, and the
/// result is dropped onto the pressed and the released button.
/// </para>
/// <para>
/// The adventure objects button needs <c>X_LOC1.ICN</c>, which only the Price of Loyalty data has.
/// Without it the button keeps the plain stone face, exactly like the reference does when
/// <c>AGG::isPoLResourceFilePresent()</c> is false.
/// </para>
/// </remarks>
public sealed class EditorButtonSet
{
    public const string AssetName = "EDITBTNS.ICN";

    /// <summary>Width of a button in the panel, in pixels.</summary>
    public const int ButtonWidth = 48;

    /// <summary>Height of a button in the panel, in pixels.</summary>
    public const int ButtonHeight = 36;

    /// <summary>Number of frames, the thirty five of the archive plus the ten the reference draws.</summary>
    public const int FrameCount = 45;

    /// <summary>Released terrain instrument button.</summary>
    public const int TerrainFrame = 0;

    /// <summary>Released landscape objects instrument button.</summary>
    public const int LandscapeObjectsFrame = 2;

    /// <summary>Released detail instrument button.</summary>
    public const int DetailFrame = 4;

    /// <summary>Released stream instrument button.</summary>
    public const int StreamFrame = 6;

    /// <summary>Released road instrument button.</summary>
    public const int RoadFrame = 8;

    /// <summary>Released erase instrument button.</summary>
    public const int EraseFrame = 10;

    /// <summary>Released magnify system button.</summary>
    public const int MagnifyFrame = 12;

    /// <summary>Released specifications system button.</summary>
    public const int SpecsFrame = 18;

    /// <summary>Released file system button.</summary>
    public const int FileFrame = 20;

    /// <summary>Released system button.</summary>
    public const int SystemFrame = 22;

    /// <summary>Released frame of the first brush size button; the four sizes follow in pairs.</summary>
    public const int BrushFrame = 24;

    /// <summary>Number of brush size buttons.</summary>
    public const int BrushCount = 4;

    /// <summary>Width of a brush size button, in pixels.</summary>
    public const int BrushWidth = 24;

    /// <summary>Height of a brush size button, in pixels.</summary>
    public const int BrushHeight = 18;

    /// <summary>First of the ten generated frames.</summary>
    public const int FirstGeneratedFrame = 35;

    /// <summary>Released adventure objects instrument button.</summary>
    public const int AdventureObjectsFrame = 35;

    /// <summary>Released kingdom objects instrument button.</summary>
    public const int KingdomObjectsFrame = 37;

    /// <summary>Released monsters instrument button.</summary>
    public const int MonstersFrame = 39;

    /// <summary>Released undo button.</summary>
    public const int UndoFrame = 41;

    /// <summary>Released redo button.</summary>
    public const int RedoFrame = 43;

    /// <summary>Palette index of the stone face the icon is painted on.</summary>
    private const byte FaceColor = 41;

    /// <summary>Palette index of the pressed stone face.</summary>
    private const byte PressedFaceColor = 46;

    private readonly Sprite[] _frames;

    private EditorButtonSet(Sprite[] frames)
    {
        _frames = frames;
    }

    /// <summary>Number of frames, see <see cref="FrameCount"/>.</summary>
    public int Count => _frames.Length;

    /// <summary>Archive entries the buttons asked for but the archives do not hold.</summary>
    public IReadOnlyCollection<string> Missing { get; private set; } = [];

    /// <summary>The frame at <paramref name="index"/>, or <see langword="null"/> when out of range.</summary>
    public Sprite? this[int index] => index >= 0 && index < _frames.Length ? _frames[index] : null;

    /// <summary>
    /// Builds the set for an archive bundle; <see langword="null"/> when there is no data or when the
    /// thirty five frames the panel cannot work without are missing.
    /// </summary>
    /// <param name="assets">The archive to read from, may be <see langword="null"/>.</param>
    /// <param name="isEvilInterface">Whether the evil set is wanted, the reference's <c>ICN::EDITBTNS_EVIL</c>.</param>
    /// <returns>The buttons, or <see langword="null"/> when the archive lacks them.</returns>
    /// <remarks>
    /// <c>EDITBTNS_EVIL</c> 归档里没有条目：参考先把好界面的 45 帧全造完，再逐帧
    /// <c>convertToEvilInterface()</c> 换色（<c>game_assets.cpp:3288-3296</c>）。顺序不能颠倒——后补的十帧
    /// 里那几块石头面色（41 / 46）也是要一起换的，先换原料再画就等于漏掉它们。
    /// </remarks>
    public static EditorButtonSet? TryCreate(AssetBundle? assets, bool isEvilInterface = false)
    {
        if (assets is null)
        {
            return null;
        }

        IcnSpriteCache cache = new(assets);
        Sprite[] frames = new Sprite[FrameCount];

        for (int i = 0; i < FirstGeneratedFrame; ++i)
        {
            if (cache.GetSprite(AssetName, i) is not { } frame)
            {
                return null;
            }

            frames[i] = frame;
        }

        Sprite released = Layered(frames[DetailFrame]);
        Sprite pressed = Layered(frames[DetailFrame + 1]);

        // Clean button sprites: the icon area of the detail button becomes the plain stone face.
        ImageOps.FillSprite(released, new Rect(16, 6, 18, 24), FaceColor);
        ImageOps.FillSprite(pressed, new Rect(16, 7, 17, 23), PressedFaceColor);

        for (int i = 0; i < 8; i += 2)
        {
            frames[FirstGeneratedFrame + i] = released.Clone();
            frames[FirstGeneratedFrame + 1 + i] = pressed.Clone();
        }

        // The undo pair came out of the loop above; the redo pair needs its own clean faces.
        frames[RedoFrame] = released.Clone();
        frames[RedoFrame + 1] = pressed.Clone();

        // Adventure objects: only the Price of Loyalty data has the icon.
        if (cache.GetSprite("X_LOC1.ICN", 0) is { } adventure)
        {
            DrawImageOnButton(adventure, frames[AdventureObjectsFrame], frames[AdventureObjectsFrame + 1]);
        }

        if (cache.GetSprite("OBJNARTI.ICN", 13) is { } kingdom)
        {
            DrawImageOnButton(kingdom, frames[KingdomObjectsFrame], frames[KingdomObjectsFrame + 1]);
        }

        if (cache.GetSprite("MONS32.ICN", 11) is { } monsters)
        {
            DrawImageOnButton(monsters, frames[MonstersFrame], frames[MonstersFrame + 1]);
        }

        Sprite undo = CreateUndoImage();
        DrawImageOnButton(undo, frames[UndoFrame], frames[UndoFrame + 1]);
        DrawImageOnButton(FlipLayered(undo), frames[RedoFrame], frames[RedoFrame + 1]);

        // The reference repairs two shadow pixels of the redo arrow, then adds the aliasing of the
        // undo arrow's tail, which the four shadow passes leave with a hard edge.
        ImageOps.SetPixel(frames[RedoFrame], 27, 11, FaceColor);
        ImageOps.SetPixel(frames[RedoFrame + 1], 26, 12, PressedFaceColor);
        ImageOps.SetPixel(frames[RedoFrame], 16, 17, 47);
        ImageOps.SetPixel(frames[RedoFrame + 1], 15, 18, 52);

        ImageOps.SetPixel(frames[UndoFrame], 32, 19, 52);
        ImageOps.DrawLine(frames[UndoFrame], new Point(33, 20), new Point(33, 21), 52);
        ImageOps.SetPixel(frames[UndoFrame + 1], 31, 20, 53);
        ImageOps.DrawLine(frames[UndoFrame + 1], new Point(32, 21), new Point(32, 22), 53);

        if (isEvilInterface)
        {
            ReadOnlySpan<byte> palette = PaletteTable.Get(PaletteId.GoodToEvilInterface);

            foreach (Sprite frame in frames)
            {
                ImageOps.ApplyPalette(frame.Image, palette);
            }
        }

        return new EditorButtonSet(frames) { Missing = cache.Missing };
    }

    /// <summary>
    /// The undo arrow of the reference: a nineteen by thirteen image of a left arrow with a tail,
    /// drawn with palette index 56.
    /// </summary>
    /// <returns>A transparent sprite with the arrow on it.</returns>
    private static Sprite CreateUndoImage()
    {
        Sprite image = new(19, 13);
        image.Reset();

        const byte color = 56;

        ImageOps.SetPixel(image, 0, 6, color);

        for (int x = 1; x < 7; ++x)
        {
            ImageOps.DrawLine(image, new Point(x, 6 - x), new Point(x, 6 + x), color);
        }

        ImageOps.FillSprite(image, new Rect(7, 4, 9, 5), color);

        ImageOps.SetPixel(image, 16, 5, color);
        ImageOps.FillSprite(image, new Rect(16, 6, 2, 4), color);
        ImageOps.FillSprite(image, new Rect(17, 8, 2, 4), color);
        ImageOps.SetPixel(image, 18, 12, color);

        return image;
    }

    /// <summary>
    /// Places <paramref name="icon"/> onto the pressed and the released button, with the shading the
    /// reference gives it (<c>drawImageOnButton</c>).
    /// </summary>
    /// <param name="icon">Icon to place, cut to 35x25 at the top left corner.</param>
    /// <param name="released">Button in its raised state.</param>
    /// <param name="pressed">Button in its pressed state.</param>
    private static void DrawImageOnButton(Sprite icon, Sprite released, Sprite pressed)
    {
        const int maxWidth = 39;
        const int maxHeight = 29;

        int width = Math.Min(maxWidth, icon.Width + 4);
        int height = Math.Min(maxHeight, icon.Height + 4);

        Sprite image = new(width, height);
        image.Reset();

        ImageOps.Copy(icon, new Rect(0, 0, 35, 25), image, new Point(2, 2));
        ImageOps.SetTransformLayerTransparent(image);

        ImageOps.UpdateShadow(image, new Point(1, -1), 2, false);
        ImageOps.UpdateShadow(image, new Point(2, -2), 5, false);
        ImageOps.UpdateShadow(image, new Point(-1, 1), 6, false);
        ImageOps.UpdateShadow(image, new Point(-2, 2), 9, false);

        int offsetX = (pressed.Width - width) / 2;
        int offsetY = (pressed.Height - height) / 2;

        ImageOps.Copy(image, image.Image.Bounds, pressed, new Point(offsetX + 1, offsetY));
        ImageOps.ReplaceTransformIdByColorId(image, 6, 10);
        ImageOps.Copy(image, image.Image.Bounds, released, new Point(offsetX + 2, offsetY - 1));
    }

    /// <summary>Same sprite, but with the transform layer the assembly needs.</summary>
    /// <param name="sprite">Sprite to give a transform layer.</param>
    /// <returns>A sprite that owns a transform layer.</returns>
    private static Sprite Layered(Sprite sprite) =>
        sprite.HasTransformLayer
            ? sprite.Clone()
            : new Sprite(sprite.Image.Clone(), sprite.OffsetX, sprite.OffsetY, new byte[sprite.Image.Stride * sprite.Height]);

    /// <summary>Mirrors a sprite horizontally, both layers.</summary>
    /// <param name="source">Sprite to mirror.</param>
    /// <returns>A new sprite with the columns in reverse order.</returns>
    private static Sprite FlipLayered(Sprite source)
    {
        Sprite flipped = new(source.Width, source.Height);

        for (int y = 0; y < source.Height; ++y)
        {
            Span<byte> sourceRow = source.Image.Row(y);
            Span<byte> flippedRow = flipped.Image.Row(y);
            int sourceStride = source.Image.Stride;

            for (int x = 0; x < source.Width; ++x)
            {
                int mirrored = source.Width - 1 - x;

                flippedRow[x] = sourceRow[mirrored];

                if (source.Transform is { } sourceTransform && flipped.Transform is { } flippedTransform)
                {
                    flippedTransform[(y * flipped.Image.Stride) + x] = sourceTransform[(y * sourceStride) + mirrored];
                }
            }
        }

        return flipped;
    }
}
