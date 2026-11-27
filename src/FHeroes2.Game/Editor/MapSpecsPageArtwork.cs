using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The artwork the map specifications page is assembled from, the <c>Assets::getImage()</c> calls of
/// <c>mapSpecificationsDialog()</c> (<c>editor_map_specs_window.cpp:2055-2225</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在窗口函数里随手取图：<c>STONEBAK</c> 铺底、<c>METALLIC_BORDERED_TEXTBOX</c> 做名称框、
/// <c>NGEXTRA</c> 做玩家图标与难度光标、<c>DIFFICULTY_ICON_*</c> 四个独立归档、<c>DROPLISL</c> 做条件下拉框。
/// 这里把「取哪一帧」集中到一个类里，绘制方只管往矩形里贴。
/// </para>
/// <para>
/// 参考的 <c>Assets::getImage()</c> 对缺失的帧返回空图并 <c>assert</c>，端口返回 <see langword="null"/>：
/// <see cref="TryCreate"/> 只保证页面离不开的几张（石头底、名称框、玩家阴影、难度光标、条件背景与下拉按钮）
/// 在，逐帧的图标由调用方按需取，取不到就不画——编辑器窗口不该因为美术件不全就整个打不开。
/// </para>
/// <para>
/// 每帧都缓存：这些表在窗口的整个生命周期里不变，而事件循环每帧都会重画一次。
/// </para>
/// </remarks>
public sealed class MapSpecsPageArtwork
{
    /// <summary><c>NGEXTRA</c> frame of the shadow under a player icon (<c>:2084</c>).</summary>
    public const int PlayerShadowFrame = 61;

    /// <summary><c>NGEXTRA</c> frame of the difficulty cursor (<c>:2109</c>).</summary>
    public const int DifficultyCursorFrame = 62;

    private readonly Dictionary<int, Sprite?> _playerIcons = [];
    private readonly Dictionary<int, Sprite?> _conditionIcons = [];
    private readonly Dictionary<GameDifficulty, Sprite?> _difficultyIcons = [];

    private MapSpecsPageArtwork()
    {
    }

    /// <summary>The <c>STONEBAK</c> texture the page is filled with.</summary>
    public Sprite Stone { get; private init; } = null!;

    /// <summary>The <c>METALLIC_BORDERED_TEXTBOX</c> frame the map name sits in.</summary>
    public Sprite NameBox { get; private init; } = null!;

    /// <summary>The shadow blitted under every player icon.</summary>
    public Sprite PlayerShadow { get; private init; } = null!;

    /// <summary>The frame the difficulty selector snaps to.</summary>
    public Sprite DifficultyCursor { get; private init; } = null!;

    /// <summary>The <c>DROPLISL</c> 0 frame, the background of the selected condition.</summary>
    public Sprite ConditionBackground { get; private init; } = null!;

    /// <summary>The <c>DROPLISL</c> 1 / 2 frames of the drop button.</summary>
    public (Sprite Released, Sprite Pressed) DropButton { get; private init; }

    /// <summary>Whether the evil variants were loaded.</summary>
    public bool IsEvilInterface { get; private init; }

    /// <summary>
    /// Reads the page artwork out of the installed game data.
    /// </summary>
    /// <param name="isEvilInterface">Whether the evil look is used.</param>
    /// <returns>The artwork, or <see langword="null"/> when the game data is missing or lacks a mandatory frame.</returns>
    /// <remarks>
    /// 依赖 <see cref="Assets.Current"/> 已指向游戏数据。
    /// </remarks>
    public static MapSpecsPageArtwork? TryCreate(bool isEvilInterface)
    {
        Sprite? stone = Load(isEvilInterface ? IcnType.StonebakEvil : IcnType.Stonebak, 0);
        Sprite? nameBox = Load(isEvilInterface ? IcnType.MetallicBorderedTextboxEvil : IcnType.MetallicBorderedTextboxGood, 0);
        Sprite? playerShadow = Load(IcnType.Ngextra, PlayerShadowFrame);
        Sprite? difficultyCursor = Load(IcnType.Ngextra, DifficultyCursorFrame);

        IcnType dropList = isEvilInterface ? IcnType.DroplislEvil : IcnType.Droplisl;
        Sprite? conditionBackground = Load(dropList, 0);
        Sprite? dropButtonReleased = Load(dropList, 1);
        Sprite? dropButtonPressed = Load(dropList, 2);

        if (stone is null || nameBox is null || playerShadow is null || difficultyCursor is null
            || conditionBackground is null || dropButtonReleased is null || dropButtonPressed is null)
        {
            return null;
        }

        return new MapSpecsPageArtwork
        {
            Stone = stone,
            NameBox = nameBox,
            PlayerShadow = playerShadow,
            DifficultyCursor = difficultyCursor,
            ConditionBackground = conditionBackground,
            DropButton = (dropButtonReleased, dropButtonPressed),
            IsEvilInterface = isEvilInterface,
        };
    }

    /// <summary>
    /// The <c>NGEXTRA</c> frame of a player icon, <c>Assets::getImage( ICN::NGEXTRA, icnIndex )</c> (<c>:2093</c>).
    /// </summary>
    /// <param name="frameIndex">Frame index, already including the colour offset (<see cref="MapSpecsPageIcons.GetPlayerIconIndex(MapSpecsState, PlayerColor)"/>).</param>
    /// <returns>The icon, or <see langword="null"/> when the archive lacks that frame.</returns>
    public Sprite? GetPlayerIcon(int frameIndex) => LoadCached(_playerIcons, IcnType.Ngextra, frameIndex);

    /// <summary>
    /// The icon of a difficulty, <c>Assets::getImage( ICN::DIFFICULTY_ICON_*, isEvilInterface ? 1 : 0 )</c> (<c>:2119</c>).
    /// </summary>
    /// <param name="difficulty">The difficulty.</param>
    /// <returns>The icon, or <see langword="null"/> when the archive lacks it.</returns>
    public Sprite? GetDifficultyIcon(GameDifficulty difficulty)
    {
        if (_difficultyIcons.TryGetValue(difficulty, out Sprite? cached))
        {
            return cached;
        }

        Sprite? icon = Load(MapSpecsPageIcons.GetDifficultyIconType(difficulty), IsEvilInterface ? 1 : 0);
        _difficultyIcons[difficulty] = icon;

        return icon;
    }

    /// <summary>
    /// The icon of a condition row, <c>Assets::getImage( ICN::REQUESTS, frameIndex )</c>.
    /// </summary>
    /// <param name="frameIndex">Frame index from <see cref="MapSpecsConditionRows.GetVictoryIconIndex"/> or
    /// <see cref="MapSpecsConditionRows.GetLossIconIndex"/>.</param>
    /// <returns>The icon, or <see langword="null"/> when the archive lacks that frame.</returns>
    public Sprite? GetConditionIcon(int frameIndex) => LoadCached(_conditionIcons, IcnType.Requests, frameIndex);

    private static Sprite? Load(IcnType type, int frameId) => Assets.GetImage(Assets.GetFileName(type), frameId);

    private static Sprite? LoadCached(Dictionary<int, Sprite?> cache, IcnType type, int frameIndex)
    {
        if (cache.TryGetValue(frameIndex, out Sprite? cached))
        {
            return cached;
        }

        Sprite? icon = Load(type, frameIndex);
        cache[frameIndex] = icon;

        return icon;
    }
}
