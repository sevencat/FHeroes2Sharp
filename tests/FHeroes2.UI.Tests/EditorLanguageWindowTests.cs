using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the editor's language window (<c>editor_language_window.cpp</c>): the list of the map's languages, the
/// "add" and "delete" buttons under it, the double click that closes the window, and the write back described by
/// <c>map_format_helper.cpp:1904</c>.
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorLanguageWindowTests
{
    private const byte AttributeColor = 7;

    private const int BodyLineHeight = 14;

    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void Create_CopiesTheLanguages() => Assert.True(TryWithArtwork(artwork =>
    {
        MapTranslationState state = CreateState();

        EditorLanguageWindow window = CreateWindow(artwork, state);
        _ = state.Add(SupportedLanguage.Polish);

        Assert.Equal(
            new[] { SupportedLanguage.English, SupportedLanguage.French, SupportedLanguage.German },
            window.Languages);
        Assert.Equal(window.Languages, window.OriginalLanguages);
        Assert.Equal(SupportedLanguage.English, window.MainLanguage);
        Assert.Equal(SupportedLanguage.English, window.SelectedLanguage);
    }));

    [Fact]
    public void Layout_PutsTheListOnTopAndTheButtonsUnderIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());
        Rect active = window.ActiveArea;

        Assert.Equal(EditorLanguageWindow.ActiveWidth, active.Width);
        Assert.Equal(EditorLanguageWindow.ActiveHeight, active.Height);
        Assert.Equal(EditorLanguageWindow.LanguageAreaWidth, window.LanguageArea.Width);
        Assert.Equal(EditorLanguageWindow.LanguageAreaHeight, window.LanguageArea.Height);
        Assert.True(window.LanguageArea.Y > active.Y);
        Assert.True(window.ScrollBarArea.X > window.LanguageArea.Right);
        Assert.True(window.ScrollBarArea.Right < active.Right);

        // 参考 :186-195：两个小按钮贴在列表底下 5 像素处，一左一右。
        Assert.Equal(window.LanguageArea.Bottom + EditorLanguageWindow.ButtonOffsetY, window.AddButton.Area.Y);
        Assert.Equal(window.LanguageArea.Bottom + EditorLanguageWindow.ButtonOffsetY, window.DeleteButton.Area.Y);
        Assert.Equal(window.LanguageArea.X, window.AddButton.Area.X);
        Assert.Equal(window.LanguageArea.Right - window.DeleteButton.Area.Width, window.DeleteButton.Area.X);
        Assert.True(window.Cancel.Area.X > window.Okay.Area.X);
        Assert.True(window.Okay.Area.Y > window.AddButton.Area.Bottom);
    }));

    [Fact]
    public void Rows_AreTenTallAndStartAtTheListTop() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());

        // 行高是把列表高（减去底部留白）分给十个可见行，第一行贴着列表顶。
        Assert.Equal(window.ItemsArea.Y, window.RowArea(0).Y);
        Assert.Equal(window.ItemsArea.Height / EditorLanguageWindow.MaxVisibleItemCount, window.RowArea(0).Height);
        Assert.Equal(window.RowArea(0).Bottom, window.RowArea(1).Y);
    }));

    [Fact]
    public void AddLanguage_AppendsTheRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());

        Assert.True(window.AddLanguage(SupportedLanguage.Polish));

        Assert.Equal(4, window.Languages.Count);
        Assert.Equal(SupportedLanguage.Polish, window.Languages[3]);
    }));

    [Fact]
    public void AddLanguage_RejectsADuplicate() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());

        Assert.False(window.AddLanguage(SupportedLanguage.German));
        Assert.Equal(3, window.Languages.Count);
        Assert.Equal(EditorLanguageWindow.LanguageTitle, window.Popup!.Value.Header);
        Assert.Equal(EditorLanguageWindow.AlreadyExistsMessage, window.Popup.Value.Body);
    }));

    [Fact]
    public void AddLanguage_IgnoresTheMainLanguage() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());

        Assert.False(window.AddLanguage(SupportedLanguage.English));
        Assert.Null(window.Popup);
        Assert.Equal(3, window.Languages.Count);
    }));

    [Fact]
    public void RemoveSelectedLanguage_DropsTheRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());

        window.Process(Click(Center(window.RowArea(1))));
        Assert.True(window.RemoveSelectedLanguage());

        Assert.Equal(
            new[] { SupportedLanguage.English, SupportedLanguage.German },
            window.Languages);
        Assert.Equal(SupportedLanguage.English, window.SelectedLanguage);
    }));

    [Fact]
    public void RemoveSelectedLanguage_KeepsTheLastOne() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState(withTranslations: false));

        Assert.False(window.RemoveSelectedLanguage());
        Assert.Single(window.Languages);
        Assert.Equal(EditorLanguageWindow.LastLanguageMessage, window.Popup!.Value.Body);
    }));

    [Fact]
    public void DoubleClickingARow_AcceptsTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());
        Point row = Center(window.RowArea(1));

        Assert.Equal(EditorListOutcome.None, window.Process(Click(row)));
        Assert.Equal(EditorListOutcome.Accepted, window.Process(Click(row)));
    }));

    [Fact]
    public void ClickingTheAddButton_AsksTheHostForThePicker() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());

        _ = window.Process(Click(Center(window.AddButton.Area)));

        Assert.True(window.LanguageAddRequested);
        Assert.Equal(EditorListOutcome.None, window.Result);
    }));

    [Fact]
    public void ClickingTheDeleteButton_DropsTheSelectedRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());

        window.Process(Click(Center(window.RowArea(2))));
        _ = window.Process(Click(Center(window.DeleteButton.Area)));

        Assert.Equal(
            new[] { SupportedLanguage.English, SupportedLanguage.French },
            window.Languages);
    }));

    [Fact]
    public void OkayAndCancel_CloseTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());

        Assert.Equal(EditorListOutcome.Accepted, window.Process(Click(Center(window.Okay.Area))));
        Assert.Equal(EditorListOutcome.Cancelled, window.Process(Click(Center(window.Cancel.Area))));
    }));

    [Fact]
    public void RightClickOnTheButtons_ExplainsThem() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());

        window.Process(RightPress(Center(window.AddButton.Area)));
        Assert.Equal(EditorLanguageWindow.AddTitle, window.Popup!.Value.Header);

        window.Process(RightPress(Center(window.DeleteButton.Area)));
        Assert.Equal(EditorLanguageWindow.DeleteTitle, window.Popup!.Value.Header);

        window.Process(RightPress(Center(window.Okay.Area)));
        Assert.Equal(EditorLanguageWindow.OkayTitle, window.Popup!.Value.Header);

        window.Process(RightPress(Center(window.Cancel.Area)));
        Assert.Equal(EditorLanguageWindow.CancelTitle, window.Popup!.Value.Header);
    }));

    [Fact]
    public void ApplyTo_WritesTheSelectedLanguageBack() => Assert.True(TryWithArtwork(artwork =>
    {
        MapTranslationState state = CreateState();
        EditorLanguageWindow window = CreateWindow(artwork, state);

        // 选中 German（第三行），确定后它成为主语言，English 原文留在翻译里。
        window.Process(Click(Center(window.RowArea(2))));
        Assert.True(window.ApplyTo(state));

        Assert.Equal(SupportedLanguage.German, state.MainLanguage);
        Assert.Equal("Beltway", state.Find(SupportedLanguage.English)!.Name);
        Assert.Equal(string.Empty, state.Current.Name);
        Assert.True(state.Has(SupportedLanguage.French));
    }));

    [Fact]
    public void ApplyTo_LeavesEverythingAloneWhenNothingChanged() => Assert.True(TryWithArtwork(artwork =>
    {
        MapTranslationState state = CreateState();
        EditorLanguageWindow window = CreateWindow(artwork, state);

        Assert.False(window.ApplyTo(state));
        Assert.Equal(SupportedLanguage.English, state.MainLanguage);
        Assert.Equal("Beltway", state.Current.Name);
    }));

    [Fact]
    public void ApplyTo_DropsTheLanguagesThatLeftTheList() => Assert.True(TryWithArtwork(artwork =>
    {
        MapTranslationState state = CreateState();
        EditorLanguageWindow window = CreateWindow(artwork, state);

        // 列表是 [English, French, German]，删掉 French。
        window.Process(Click(Center(window.RowArea(1))));
        _ = window.Process(Click(Center(window.DeleteButton.Area)));
        Assert.True(window.ApplyTo(state));

        Assert.False(state.Has(SupportedLanguage.French));
        Assert.Equal(SupportedLanguage.English, state.MainLanguage);
        Assert.Equal("Beltway", state.Current.Name);
        Assert.True(state.Has(SupportedLanguage.German));
    }));

    [Fact]
    public void Draw_PaintsTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorLanguageWindow window = CreateWindow(artwork, CreateState());
        PixelBuffer canvas = new(Screen.Width, Screen.Height);
        canvas.Fill(0);

        window.Draw(canvas, new LocalEvent());

        Rect active = window.ActiveArea;
        bool painted = false;

        for (int y = active.Y; y < active.Bottom && !painted; ++y)
        {
            for (int x = active.X; x < active.Right; ++x)
            {
                if (canvas[x, y] != 0)
                {
                    painted = true;
                    break;
                }
            }
        }

        Assert.True(painted);
    }));

    [Fact]
    public void CreateFromSpecs_EditsTheSpecsLanguages() => Assert.True(TryWithArtwork(artwork =>
    {
        MapSpecsState specs = CreateSpecs();

        EditorLanguageWindow window = EditorLanguageWindow.Create(specs, artwork, Screen,
            new FakeGlyphSource(AttributeColor, BodyLineHeight), new FakeGlyphSource(AttributeColor, BodyLineHeight));

        Assert.Equal(
            new[] { SupportedLanguage.English, SupportedLanguage.German },
            window.Languages);
        Assert.Equal("Beltway", specs.Name);
        Assert.Equal("Two roads", specs.Description);
    }));

    [Fact]
    public void ApplyToSpecs_MovesTheNameToTheNewMainLanguage() => Assert.True(TryWithArtwork(artwork =>
    {
        MapSpecsState specs = CreateSpecs();
        EditorLanguageWindow window = EditorLanguageWindow.Create(specs, artwork, Screen,
            new FakeGlyphSource(AttributeColor, BodyLineHeight), new FakeGlyphSource(AttributeColor, BodyLineHeight));

        // 列表是 [English, German]，选中 German 后它成为主语言：名字跟着换，原文进 English 的翻译槽。
        window.Process(Click(Center(window.RowArea(1))));
        Assert.True(window.ApplyTo(specs));

        Assert.Equal(SupportedLanguage.German, specs.Translations.MainLanguage);
        Assert.Equal(string.Empty, specs.Name);
        Assert.Equal("Beltway", specs.Translations.Find(SupportedLanguage.English)!.Name);
        Assert.Equal("Two roads", specs.Translations.Find(SupportedLanguage.English)!.Description);
    }));

    [Fact]
    public void CreateLanguagePicker_OffersEveryLanguageWithTheMainOneSelected() => Assert.True(
        TryWithArtwork(artwork =>
        {
            MapSpecsState specs = CreateSpecs();
            EditorLanguageWindow window = EditorLanguageWindow.Create(specs, artwork, Screen,
                new FakeGlyphSource(AttributeColor, BodyLineHeight), new FakeGlyphSource(AttributeColor, BodyLineHeight));

            LanguageSelectionDialog picker = window.CreateLanguagePicker(Screen);

            SupportedLanguage[] all = Enum.GetValues<SupportedLanguage>();

            Assert.Equal(all.Length, picker.Count);
            Assert.Equal(Array.IndexOf(all, SupportedLanguage.English), picker.SelectedIndex);
            Assert.Equal(EditorLanguageWindow.LanguagePickerTitle, picker.Title);
            Assert.Equal(GameLanguage.GetName(SupportedLanguage.English), picker.SelectedName);
            Assert.Equal(GameLanguage.GetName(SupportedLanguage.German), picker.Names[Array.IndexOf(all, SupportedLanguage.German)]);
        }));

    [Fact]
    public void CreateLanguagePicker_SelectsTheMainLanguageWhereverItIs() => Assert.True(TryWithArtwork(artwork =>
    {
        MapSpecsState specs = CreateSpecs();

        // 主语言换成 German，选择窗的初始选中要跟着走。
        _ = specs.Translations.ChangeLanguage(SupportedLanguage.German);
        EditorLanguageWindow window = EditorLanguageWindow.Create(specs, artwork, Screen,
            new FakeGlyphSource(AttributeColor, BodyLineHeight), new FakeGlyphSource(AttributeColor, BodyLineHeight));

        LanguageSelectionDialog picker = window.CreateLanguagePicker(Screen);

        Assert.Equal(Array.IndexOf(Enum.GetValues<SupportedLanguage>(), SupportedLanguage.German), picker.SelectedIndex);
        Assert.Equal(GameLanguage.GetName(SupportedLanguage.German), picker.SelectedName);
    }));

    private static MapSpecsState CreateSpecs()
    {
        Mp2Map map = Mp2Map.CreateBlank(36, 0, "Beltway");
        map.Info = map.Info with { Description = "Two roads" };

        MapSpecsState specs = new(map);
        _ = specs.Translations.Add(SupportedLanguage.German);

        return specs;
    }

    private static MapTranslationState CreateState(bool withTranslations = true)
    {
        MapTranslationState state = new(
            SupportedLanguage.English,
            new MapTranslationText { Name = "Beltway", Description = "Two roads" });

        if (withTranslations)
        {
            _ = state.Add(SupportedLanguage.German);
            _ = state.Add(SupportedLanguage.French);
        }

        return state;
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static LocalEvent Click(Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return events;
    }

    private static LocalEvent RightPress(Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Right, position);

        return events;
    }

    private static EditorLanguageWindow CreateWindow(EditorListArtwork artwork, MapTranslationState state) =>
        EditorLanguageWindow.Create(state, artwork, Screen, new FakeGlyphSource(AttributeColor, BodyLineHeight),
            new FakeGlyphSource(AttributeColor, BodyLineHeight));

    /// <summary>Opens the game data and hands the artwork to the test body.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArtwork(Action<EditorListArtwork> body)
    {
        AssetBundle? previous = GameAssets.Current;
        AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            EditorListArtwork artwork = EditorListArtwork.TryCreate(isEvilInterface: false) ??
                                        throw new InvalidOperationException("The artwork of the list is missing.");

            body(artwork);
        }
        finally
        {
            GameAssets.Current = previous;
            assets.Dispose();
        }

        return true;
    }

    /// <summary>A glyph source with fixed metrics whose glyphs are solid blocks.</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color, int lineHeight)
        {
            PixelBuffer image = new(6, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
            LineHeight = lineHeight;
        }

        public int LineHeight { get; }

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => character == GlyphText.SpaceCharacter ? SpaceWidth : 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
