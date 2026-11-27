namespace FHeroes2.Game.Credits;

/// <summary>Where the credits animation of one page stands, <c>AnimationState</c> of <c>game_credits.cpp:71-76</c>.</summary>
public enum CreditsAnimationState
{
    /// <summary>The page is getting brighter, <c>FADING_IN</c>.</summary>
    FadingIn,

    /// <summary>The page is fully drawn and the counter runs, <c>NO_ACTION</c>.</summary>
    NoAction,

    /// <summary>The page is getting darker, <c>FADING_OUT</c>.</summary>
    FadingOut,
}

/// <summary>
/// The fade and hold rhythm of the credits, the <c>AnimationSequence</c> of <c>game_credits.cpp:78-150</c>.
/// </summary>
/// <remarks>
/// <para>
/// 每 50 毫秒推进一次（<c>animationDelay</c> 在 <c>Game::ShowCredits</c> 里）：亮度按 5 递增到 255，停 100 次
/// （约 5 秒），再按 5 递减到 0，然后换下一页、循环。参考的计数器比较是「<c>&gt;</c> 100」，所以满亮度会保持
/// 101 次，端口照抄。
/// </para>
/// <para>
/// 参考的第一帧另有一条规则：进入 <see cref="CreditsAnimationState.NoAction"/> 前有一句 <c>fadeInHeader = false</c>，
/// 意思是标题条只淡入一次、之后不再重画。端口把这个判断放在 <see cref="IsFirstFadeIn"/> 上，由绘制方决定。
/// </para>
/// </remarks>
public sealed class CreditsSequence
{
    /// <summary>How much one step changes the alpha, <c>ALPHA_VALUE_STEP</c>.</summary>
    public const int AlphaStep = 5;

    /// <summary>Steps the page stays at full brightness, <c>NO_ACTION_COUNTER_LIMIT</c>.</summary>
    public const int NoActionLimit = 100;

    /// <summary>Fully bright, the alpha the page holds at.</summary>
    public const byte FullAlpha = 255;

    private readonly int _pageCount;
    private int _alpha;
    private int _noActionCounter;

    /// <summary>Starts the sequence on its first page, fully dark, <c>AnimationSequence( imageCount )</c>.</summary>
    /// <param name="pageCount">How many pages the run shows.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageCount"/> is not positive.</exception>
    public CreditsSequence(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);

        _pageCount = pageCount;
    }

    /// <summary>How many pages the run shows.</summary>
    public int PageCount => _pageCount;

    /// <summary>The page on screen, <c>pageId()</c>.</summary>
    public int PageId { get; private set; }

    /// <summary>Brightness of the page, 0 being black, <c>alpha()</c>.</summary>
    public byte Alpha => (byte)_alpha;

    /// <summary>Where the fade stands, <c>state()</c>.</summary>
    public CreditsAnimationState State { get; private set; } = CreditsAnimationState.FadingIn;

    /// <summary>Whether the sequence has not gone through a full page yet, i.e. the header still fades in.</summary>
    public bool IsFirstFadeIn { get; private set; } = true;

    /// <summary>
    /// Advances one step, <c>increment()</c>.
    /// </summary>
    /// <remarks>
    /// 换页发生在淡出归零那一格里：<see cref="PageId"/> 加一，越界回到 0，状态回到淡入。参考同时把
    /// <c>IsFirstFadeIn</c> 在第一次进入 <see cref="CreditsAnimationState.NoAction"/> 时关掉。
    /// </remarks>
    public void Increment()
    {
        switch (State)
        {
            case CreditsAnimationState.FadingIn:
                _alpha += AlphaStep;

                if (_alpha > FullAlpha)
                {
                    _alpha = FullAlpha;
                    State = CreditsAnimationState.NoAction;
                    _noActionCounter = 0;
                    IsFirstFadeIn = false;
                }

                break;

            case CreditsAnimationState.NoAction:
                ++_noActionCounter;

                if (_noActionCounter > NoActionLimit)
                {
                    State = CreditsAnimationState.FadingOut;
                }

                break;

            case CreditsAnimationState.FadingOut:
                _alpha -= AlphaStep;

                if (_alpha < 0)
                {
                    _alpha = 0;
                    State = CreditsAnimationState.FadingIn;

                    PageId = (PageId + 1) % _pageCount;
                }

                break;

            default:
                break;
        }
    }
}
