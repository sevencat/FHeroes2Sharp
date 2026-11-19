using FHeroes2.Game;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The tavern window, <c>Castle::_openTavern()</c> (<c>castle_tavern.cpp:39-54</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two paragraphs in one box: a fixed line about the tip, and the rumor of the week under it. The reference composes
/// them as one <c>MultiFontText</c> with two elements — the lead in the normal white font, the rumor in the font of the
/// language it was written in — and that is why <see cref="Rumor"/> keeps its language tag instead of being a bare
/// string.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): the texts and the single button, no layout. The animated keeper of
/// <c>TAVWIN.ICN</c> (<c>{ 0, 1 }</c> at <c>DelayType::CASTLE_TAVERN_DELAY</c>) belongs to the renderer, and the rumor
/// arrives as an argument, so this window does not wait for <c>World.GetCurrentRumor()</c> to be implemented.
/// </para>
/// </remarks>
public sealed class TavernDialog
{
    /// <summary>
    /// The heading, <c>GetStringBuilding( BUILD_TAVERN )</c> — a tavern in all six towns.
    /// </summary>
    public const string Title = "Tavern";

    /// <summary>
    /// The line above the rumor, <c>_( "A generous tip for the barkeep yields the following rumor:" )</c>.
    /// </summary>
    /// <remarks>The reference appends <c>"\n\n"</c> to it and then adds the rumor as a second text element.</remarks>
    public const string Lead = "A generous tip for the barkeep yields the following rumor:";

    private TavernDialog(LocalizedString rumor) => Rumor = rumor;

    /// <summary>
    /// Builds the window around a rumor, <c>Castle::_openTavern()</c> (<c>castle_tavern.cpp:39</c>).
    /// </summary>
    /// <param name="rumor">
    /// The sentence to tell, normally <c>World.GetCurrentRumor()</c>; empty text is refused because the loader never
    /// stores an empty rumor, so an empty one here is a bug rather than a state.
    /// </param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentException"><paramref name="rumor"/> has no text.</exception>
    public static TavernDialog Create(LocalizedString rumor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rumor.Text);

        return new TavernDialog(rumor);
    }

    /// <summary>
    /// Builds the window around the rumor of the week, <c>Castle::_openTavern()</c> (<c>castle_tavern.cpp:39</c>).
    /// </summary>
    /// <remarks>
    /// What a castle screen will call. The window itself never reads the world — the rumor arrives as an argument, and
    /// this overload only saves the caller from doing <c>world.GetCurrentRumor()</c> by hand.
    /// </remarks>
    /// <param name="world">The world to ask for this week's rumor.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public static TavernDialog Create(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return Create(world.GetCurrentRumor());
    }

    /// <summary>The rumor of the week and the language it is written in.</summary>
    public LocalizedString Rumor { get; }

    /// <summary>
    /// The two paragraphs as one string, with the blank line the reference puts between them.
    /// </summary>
    /// <remarks>
    /// For callers that draw a single blob of text; the window itself draws the two halves in different fonts, so a
    /// renderer should use <see cref="Lead"/> and <see cref="Rumor"/> separately.
    /// </remarks>
    public string Body => Lead + "\n\n" + Rumor.Text;

    /// <summary>
    /// The buttons the dialog has: OK only, <c>Dialog::OK</c> at <c>castle_tavern.cpp:53</c>.
    /// </summary>
    public DialogButton Buttons => DialogButton.Ok;
}
