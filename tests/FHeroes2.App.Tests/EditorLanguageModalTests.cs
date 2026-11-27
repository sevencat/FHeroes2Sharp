using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The language window on the host's frame pump: ADD stacks the picker, the picked language joins the list, and only
/// OKAY writes the list back, <c>openLanguageWindow()</c> of the reference.
/// </summary>
/// <remarks>
/// 用例自己当帧泵，事件塞进 <see cref="LocalEvent.Current"/>，同 <c>EditorDailyEventsModalTests</c>。语言账本直接用
/// <see cref="MapTranslationState"/>，它是参考的 <c>mapFormat</c> 在端口里的替身。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorLanguageModalTests : IDisposable
{
    /// <summary>Size of the editor map the end-to-end test builds.</summary>
    private const int MapSize = 36;

    /// <summary>Entry of the specifications button in the system table, three columns per row.</summary>
    private const int SpecsButtonIndex = 3;

    /// <summary>Width of one button of the bay, the 48x36 frames of <c>EDITBTNS.ICN</c>.</summary>
    private const int ButtonWidth = 48;

    /// <summary>Height of one button of the bay.</summary>
    private const int ButtonHeight = 36;

    private readonly AssetBundle? _previousAssets;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts once for the whole test.</summary>
    public EditorLanguageModalTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }

        _fonts = GameFonts.TryLoad(_assets);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;

        _assets?.Dispose();
    }

    [Fact]
    public void AddingALanguageKeepsItInTheList() =>
        Assert.True(TryOpen(out EditorLanguageModal modal, out _) &&
            AddLanguage(modal, OtherLanguageIndex) &&
            modal.Window.Languages.Count == 2 && modal.Window.Languages[1] == OtherLanguage);

    [Fact]
    public void CancellingThePickerAddsNothing() =>
        Assert.True(TryOpen(out EditorLanguageModal modal, out _) &&
            ClickAdd(modal) &&
            ClickPickerButton(modal, DialogButton.Cancel) &&
            !modal.HasInnerWindow && modal.Window.Languages.Count == 1 && !modal.IsFinished);

    [Fact]
    public void PickingTheMainLanguageIsSilentlyIgnored() =>
        Assert.True(TryOpen(out EditorLanguageModal modal, out _) &&
            ClickAdd(modal) &&
            ClickPickerRow(modal, MainLanguageIndex) &&
            ClickPickerButton(modal, DialogButton.Ok) &&
            !modal.HasInnerWindow && modal.Window.Languages.Count == 1);

    [Fact]
    public void AddingTheSameLanguageTwiceShowsAMessage() =>
        Assert.True(TryOpen(out EditorLanguageModal modal, out _) &&
            AddLanguage(modal, OtherLanguageIndex) &&
            ClickAdd(modal) &&
            ClickPickerRow(modal, OtherLanguageIndex) &&
            ClickPickerButton(modal, DialogButton.Ok) &&
            modal.TopWindow is MessageBoxModal && modal.Window.Languages.Count == 2);

    [Fact]
    public void AcceptingWritesTheListBack()
    {
        if (!TryOpen(out EditorLanguageModal modal, out MapTranslationState translations))
        {
            return;
        }

        Assert.True(AddLanguage(modal, OtherLanguageIndex));

        Click(modal, modal.Window.Okay.Area);

        Assert.True(modal.IsFinished);
        Assert.Equal(EditorListOutcome.Accepted, modal.Outcome);

        // 参考 :227-241 的写回：非当前项进翻译表，当前项变主语言。
        Assert.True(modal.Window.ApplyTo(translations));
        Assert.Equal(2, translations.Count);
        Assert.Equal(OtherLanguage, translations.Languages[1]);
    }

    [Fact]
    public void CancellingWritesNothing()
    {
        if (!TryOpen(out EditorLanguageModal modal, out MapTranslationState translations))
        {
            return;
        }

        Assert.True(AddLanguage(modal, OtherLanguageIndex));

        Click(modal, modal.Window.Cancel.Area);

        Assert.True(modal.IsFinished);
        Assert.Equal(EditorListOutcome.Cancelled, modal.Outcome);
        Assert.Equal(1, translations.Count);
    }

    [Fact]
    public void TheSpecificationsPageOpensTheLanguageWindow()
    {
        if (!TryOpenEditor(out EditorScene scene, out _))
        {
            return;
        }

        bool opened = false;

        scene.RunModal = modal =>
        {
            var page = (EditorMapSpecsModal)modal;

            Click(page, page.Window.Language.Area);

            var language = page.TopWindow as EditorLanguageModal;

            if (language is not null)
            {
                opened = true;

                // 加一门语言再点头：规格页的 ResolveLanguage 才真的被走到（:2298-2311）。
                _ = AddLanguage(language, OtherLanguageIndex);

                Click(language, language.Window.Okay.Area);
                Pump(page);
            }

            Click(page, page.Window.Okay.Area);
        };

        ClickSpecsButton(scene);

        Assert.True(opened);

        // 规格页接受写回，于是地图被标脏：语言那一路没有把答案掉在地上。
        Assert.True(scene.Edits.IsDirty);
    }

    /// <summary>Index of the language every test map starts with, <see cref="MapTranslationState.DefaultLanguage"/>.</summary>
    private static int MainLanguageIndex =>
        Array.IndexOf(Enum.GetValues<SupportedLanguage>(), MapTranslationState.DefaultLanguage);

    /// <summary>Index of a language the default map does not have yet.</summary>
    private static int OtherLanguageIndex =>
        (MainLanguageIndex + 1) % Enum.GetValues<SupportedLanguage>().Length;

    /// <summary>The language <see cref="OtherLanguageIndex"/> points at.</summary>
    private static SupportedLanguage OtherLanguage => Enum.GetValues<SupportedLanguage>()[OtherLanguageIndex];

    /// <summary>Runs <paramref name="body"/> against a window over a one language map, skipping without game data.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Whatever <paramref name="body"/> returned, <see langword="true"/> when the data is missing.</returns>
    private bool Try(Func<EditorLanguageWindow, MapTranslationState, bool> body)
    {
        if (Host() is not { } host || host.TryCreateListArtwork() is not { } artwork ||
            !host.TryGetFonts(out GameFonts? fonts))
        {
            return true;
        }

        var translations = new MapTranslationState();
        EditorLanguageWindow window = EditorLanguageWindow.Create(translations, artwork, ScreenBuffer.DefaultSize,
            fonts.Yellow, fonts.White);

        return body(window, translations);
    }

    /// <summary>Same as <see cref="Try"/>, but hands the modal over instead of the bare window.</summary>
    /// <param name="modal">The window on the host's frame pump.</param>
    /// <param name="translations">The ledger behind it.</param>
    /// <returns><see langword="true"/> when the test can run, <see langword="false"/> without game data.</returns>
    private bool TryOpen(out EditorLanguageModal modal, out MapTranslationState translations)
    {
        EditorLanguageModal? opened = null;
        MapTranslationState? ledger = null;

        bool ran = Try((window, state) =>
        {
            opened = new EditorLanguageModal(window, Host());
            ledger = state;

            return true;
        });

        modal = opened!;
        translations = ledger!;

        return ran && opened is not null;
    }

    /// <summary>Builds the host over the installed data.</summary>
    /// <returns>The host; its members report the missing data themselves.</returns>
    private EditorSceneHost Host() => new(_assets, _fonts);

    /// <summary>Clicks ADD and waits for the picker to be stacked.</summary>
    /// <param name="modal">The language window.</param>
    /// <returns>Whether the picker opened.</returns>
    private bool ClickAdd(EditorLanguageModal modal)
    {
        Click(modal, modal.Window.AddButton.Area);

        return modal.TopWindow is LanguagePickerModal;
    }

    /// <summary>Picks one row of the picker and confirms, then lets the list take the answer.</summary>
    /// <param name="modal">The language window.</param>
    /// <param name="index">Index of the language in <see cref="Enum.GetValues{TEnum}()"/>.</param>
    /// <returns>Whether the picker and the answer were both handled.</returns>
    private bool AddLanguage(EditorLanguageModal modal, int index) =>
        ClickAdd(modal) && ClickPickerRow(modal, index) && ClickPickerButton(modal, DialogButton.Ok);

    /// <summary>Clicks one row of the picker through the cursor path.</summary>
    /// <param name="modal">The language window.</param>
    /// <param name="index">Index of the language.</param>
    /// <returns>Whether the picker was open.</returns>
    private bool ClickPickerRow(EditorLanguageModal modal, int index)
    {
        if (modal.TopWindow is not LanguagePickerModal picker)
        {
            return false;
        }

        PressClick(modal, Centre(picker.Dialog.RowArea(index)));

        return true;
    }

    /// <summary>Clicks a button of the picker through the cursor path and lets the list take the answer.</summary>
    /// <param name="modal">The language window.</param>
    /// <param name="button">The button to click.</param>
    /// <returns>Whether the picker was open.</returns>
    private bool ClickPickerButton(EditorLanguageModal modal, DialogButton button)
    {
        if (modal.TopWindow is not LanguagePickerModal picker)
        {
            return false;
        }

        PressClick(modal, Centre(ButtonArea(picker.Dialog.Buttons, button)));
        Pump(modal);

        return true;
    }

    /// <summary>Area of one button of a standard button row.</summary>
    /// <param name="group">The button row.</param>
    /// <param name="value">The button to look for.</param>
    /// <returns>The area of the button.</returns>
    private static Rect ButtonArea(ButtonGroup group, DialogButton value)
    {
        for (int i = 0; i < group.Count; ++i)
        {
            if (group.ValueOf(i) == value)
            {
                return group[i].Area;
            }
        }

        return Rect.Empty;
    }

    /// <summary>Centre of a rect.</summary>
    /// <param name="area">The rect.</param>
    /// <returns>Its centre, in logical pixels.</returns>
    private static Point Centre(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    /// <summary>
    /// Clicks through the cursor and the button, the path the host drives an inner window with; the innermost window
    /// reads <see cref="LocalEvent"/> only when it is the outermost one.
    /// </summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="point">The point to click.</param>
    private static void PressClick(IModalScreen modal, Point point)
    {
        modal.SetCursor(point);
        modal.SetLeftButton(true);
        modal.SetLeftButton(false);
    }

    /// <summary>Runs one modal frame without input.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one left click on an area of the window, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void Click(IModalScreen modal, Rect area) => Click(modal, Centre(area));

    /// <summary>Feeds one left click on a point, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="point">The point to click.</param>
    private void Click(IModalScreen modal, Point point)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>
    /// Opens the editor with the fonts the pages need; <see langword="false"/> when the machine has no game data.
    /// </summary>
    /// <param name="scene">The scene, when it could be made.</param>
    /// <param name="frame">The frame the scene draws into.</param>
    /// <returns>Whether the scene could be made.</returns>
    private bool TryOpenEditor(out EditorScene scene, out ScreenBuffer frame)
    {
        scene = null!;
        frame = new ScreenBuffer(new Size(640, 480), new Palette());

        if (_assets is null || GameFonts.TryLoad(_assets) is not { } fonts)
        {
            return false;
        }

        GameAssets.Current = _assets;

        EditorScene? created = EditorScene.CreateNew(frame, _assets, fonts, MapSize, out _);

        if (created is null)
        {
            return false;
        }

        scene = created;

        return true;
    }

    /// <summary>Clicks the specifications cell of the panel, the left button path of the scene.</summary>
    /// <param name="scene">The editor scene.</param>
    private static void ClickSpecsButton(EditorScene scene)
    {
        scene.SetCursor(SpecsButtonCentre(scene));
        scene.SetLeftButton(true);
        scene.SetLeftButton(false);
    }

    /// <summary>Centre of the specifications button: the two rows sit at the bottom of the panel bay.</summary>
    /// <param name="scene">The editor scene.</param>
    /// <returns>The point inside the button.</returns>
    private static Point SpecsButtonCentre(EditorScene scene)
    {
        Rect panel = scene.PanelArea;

        return new Point(
            panel.X + ((SpecsButtonIndex % 3) * ButtonWidth) + (ButtonWidth / 2),
            panel.Y + panel.Height - (2 * ButtonHeight) + ((SpecsButtonIndex / 3) * ButtonHeight) +
            (ButtonHeight / 2));
    }
}
