using FHeroes2.App.Fonts;
using FHeroes2.App.Video;
using FHeroes2.Assets;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Credits;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// Covers <see cref="CreditsModalScreen"/>: the 50 millisecond rhythm and the ways out of it
/// (<c>game_credits.cpp:762-806</c>).
/// </summary>
/// <remarks>
/// 页面要真的 <c>CBKGLAVA</c>，所以用例需要装好的游戏数据；没有数据时直接返回。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class CreditsModalScreenTests : IDisposable
{
    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly ScreenBuffer _frame = new(new(640, 480), new Palette());

    /// <summary>Publishes the archives the pages are painted from.</summary>
    public CreditsModalScreenTests()
    {
        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();
    }

    [Fact]
    public void OneStepTakesFiftyMilliseconds()
    {
        CreditsModalScreen? screen = Create();

        if (screen is null)
        {
            return;
        }

        Assert.Equal(0, screen.Sequence.Alpha);

        // 25 毫秒还不到一格。
        screen.Update(_frame, 0.025);
        Assert.Equal(0, screen.Sequence.Alpha);

        // 再 25 毫秒正好凑满一格。
        screen.Update(_frame, 0.025);
        Assert.Equal(CreditsSequence.AlphaStep, screen.Sequence.Alpha);
    }

    [Fact]
    public void ALongFrameRunsSeveralSteps()
    {
        CreditsModalScreen? screen = Create();

        if (screen == null)
        {
            return;
        }

        // 一帧 200 毫秒等于四格。
        screen.Update(_frame, 0.2);

        Assert.Equal(CreditsSequence.AlphaStep * 4, screen.Sequence.Alpha);
    }

    [Fact]
    public void AnyKeyEndsTheCredits()
    {
        CreditsModalScreen? screen = Create();

        if (screen is null)
        {
            return;
        }

        screen.PressKey(Key.Escape, KeyModifier.None);

        Assert.True(screen.IsFinished);
    }

    [Fact]
    public void AStandardButtonKeyEndsTheCreditsAsWell()
    {
        CreditsModalScreen? screen = Create();

        if (screen is null)
        {
            return;
        }

        // 参考的跳过条件是「任意键或任意鼠标键」，不区分它是不是标准按钮键。
        screen.HotKey(ButtonHotKey.Okay);

        Assert.True(screen.IsFinished);
    }

    [Fact]
    public void AMouseButtonEndsTheCredits()
    {
        CreditsModalScreen? screen = Create();

        if (screen is null)
        {
            return;
        }

        screen.SetLeftButton(true);

        Assert.True(screen.IsFinished);
    }

    [Fact]
    public void DrawingMarksTheWholeScreen()
    {
        CreditsModalScreen? screen = Create();

        if (screen is null)
        {
            return;
        }

        _frame.TakeDirtyRegion();

        screen.Draw(_frame);

        Assert.True(_frame.HasDirtyRegion);
    }

    /// <summary>Builds the screen, or reports the missing data by returning <see langword="null"/>.</summary>
    /// <returns>The screen, or <see langword="null"/> without the game data.</returns>
    private CreditsModalScreen? Create()
    {
        if (_assets is null)
        {
            return null;
        }

        GameFonts? fonts = GameFonts.TryLoad(_assets);

        if (fonts is null)
        {
            return null;
        }

        CreditsView? view = CreditsView.TryCreate(false, "fheroes2 engine", fonts.Yellow, fonts.White, _frame.Size);

        return view is null ? null : new CreditsModalScreen(view);
    }
}
