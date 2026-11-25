using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.UI.Battle;

/// <summary>
/// One hero or captain standing beside the battlefield, the drawing half of the reference's
/// <c>Battle::OpponentSprite</c> (<c>battle_interface.cpp:987-1140</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的类同时管面积、动画和绘制；端口把动画状态机放在 <see cref="BattleOpponentAnimation"/>（游戏层，
/// 可离线测），这里只剩「算位置」和「贴图」。
/// </para>
/// <para>
/// 守方（<c>_isFlippedHorizontally</c>）整幅图水平翻转，位置从右边量：x = 640 - 29 - (sprite.x + width)，
/// y 用 <see cref="BattleHeroSprites.RightHeroYOffset"/> 而不是左边那条 183。
/// </para>
/// </remarks>
internal sealed class BattleOpponentSprite
{
    private readonly IViewWorldSpriteSource _sprites;
    private readonly IcnType _icn;
    private readonly Dictionary<int, Sprite> _flippedFrames = [];

    /// <summary>Builds the sprite for one side, <c>OpponentSprite::OpponentSprite()</c>.</summary>
    /// <param name="offset">Top left of the interface, <c>_interfacePosition</c>.</param>
    /// <param name="hero">The hero or captain of the side.</param>
    /// <param name="isFlipped">Whether the side stands on the right, as the defending side does.</param>
    /// <param name="sprites">The archive the poses are read from.</param>
    public BattleOpponentSprite(Point offset, IArmyCommander hero, bool isFlipped, IViewWorldSpriteSource sprites)
    {
        Hero = hero;
        IsFlipped = isFlipped;
        Offset = offset;
        _sprites = sprites;
        _icn = BattleHeroSprites.GetIcn(hero.Race, IsCaptain);
        Animation = new BattleOpponentAnimation(hero.Race, IsCaptain);

        // 面积取站立那一帧的尺寸；取不到就留空，之后的绘制也一并跳过。
        if (_sprites.GetSprite(_icn, Animation.Frame) is { } standing)
        {
            Area = BattleHeroSprites.GetPosition(offset, ScreenBuffer.DefaultWidth, standing, isFlipped, IsCaptain);
        }
    }

    /// <summary>The hero or captain being drawn, <c>OpponentSprite::GetHero()</c>.</summary>
    public IArmyCommander Hero { get; }

    /// <summary>Whether the sprite faces left, <c>OpponentSprite::_isFlippedHorizontally</c>.</summary>
    public bool IsFlipped { get; }

    /// <summary>Top left of the interface the sprite is measured from, <c>OpponentSprite::Offset()</c>.</summary>
    public Point Offset { get; }

    /// <summary>Where the sprite stands, <c>OpponentSprite::_area</c>; empty when the archive is missing.</summary>
    public Rect Area { get; }

    /// <summary>The pose being played, <c>OpponentSprite::_currentAnim</c>.</summary>
    public BattleOpponentAnimation Animation { get; }

    /// <summary>Whether the commander is a captain, <c>HeroBase::isCaptain()</c>.</summary>
    public bool IsCaptain => !Hero.IsHeroes;

    /// <summary>Where a spell cast by this hero leaves the hand, <c>OpponentSprite::GetCastPosition()</c>.</summary>
    /// <returns>The point on the battlefield surface.</returns>
    public Point GetCastPosition() =>
        BattleHeroSprites.GetCastPosition(Area, IsFlipped, BattleHeroSprites.GetCastOffset(Hero.Race, IsCaptain));

    /// <summary>Draws the current frame, <c>OpponentSprite::Redraw()</c>.</summary>
    /// <param name="target">The battlefield surface.</param>
    public void Redraw(PixelBuffer target)
    {
        if (Area.IsEmpty || _sprites.GetSprite(_icn, Animation.Frame) is not { } hero)
        {
            return;
        }

        if (!IsFlipped)
        {
            ImageOps.BlitSprite(target, new Point(Area.X, Area.Y), hero);

            return;
        }

        // 翻转必须连 transform 层一起翻：透明像素记在 transform 里，只翻图像会把这些像素画成 0 号色（黑）。
        if (!_flippedFrames.TryGetValue(Animation.Frame, out Sprite? flipped))
        {
            flipped = hero.Flip(horizontal: true, vertical: false);
            _flippedFrames[Animation.Frame] = flipped;
        }

        ImageOps.BlitSprite(target, new Point(Area.X, Area.Y), flipped);
    }
}
