namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The two answers of a treasure chest, <c>Dialog::SelectGoldOrExp()</c> (<c>dialog_chest.cpp:45-132</c>).
/// </summary>
public enum GoldOrExperience
{
    /// <summary>Take the experience, the <c>false</c> the reference returns.</summary>
    Experience = 0,

    /// <summary>Take the gold, the <c>true</c> the reference returns.</summary>
    Gold = 1,
}

/// <summary>
/// "Gold or experience?", <c>Dialog::SelectGoldOrExp()</c> (<c>dialog.h:116</c>, <c>dialog_chest.cpp:45</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference answers with a <c>bool</c> whose <c>true</c> means gold; <see cref="GoldOrExperience"/> says the same
/// thing by name. The chest cannot be dismissed - the player has to choose one of the two - so there is no cancel
/// value in the enum, exactly as there is no third branch in the reference.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet.
/// </para>
/// </remarks>
public sealed class GoldOrExperienceDialog
{
    private GoldOrExperienceDialog(string header, string message, uint gold, uint experience)
    {
        Header = header;
        Message = message;
        Gold = gold;
        Experience = experience;
    }

    /// <summary>
    /// Builds the dialog, <c>Dialog::SelectGoldOrExp()</c> (<c>dialog_chest.cpp:45</c>).
    /// </summary>
    /// <param name="header">Translated title.</param>
    /// <param name="message">Translated message under the title.</param>
    /// <param name="gold">Gold the chest offers, <c>gold</c> of the reference.</param>
    /// <param name="experience">Experience the chest offers, <c>expr</c> of the reference.</param>
    /// <returns>The model.</returns>
    public static GoldOrExperienceDialog Create(string header, string message, uint gold, uint experience) =>
        new(header, message, gold, experience);

    /// <summary>Translated title.</summary>
    public string Header { get; }

    /// <summary>Translated message under the title.</summary>
    public string Message { get; }

    /// <summary>Gold the chest offers.</summary>
    public uint Gold { get; }

    /// <summary>Experience the chest offers.</summary>
    public uint Experience { get; }

    /// <summary>The buttons the dialog has: the two choices, no cancel.</summary>
    public DialogButton Buttons => DialogButton.Yes | DialogButton.No;

    /// <summary>
    /// The result of pressing a button, <c>return result</c> at <c>dialog_chest.cpp:132</c>.
    /// </summary>
    /// <param name="index">Index of the pressed button: 0 gold, 1 experience.</param>
    /// <returns>The chosen reward.</returns>
    public static GoldOrExperience FromButton(int index) =>
        index == 0 ? GoldOrExperience.Gold : GoldOrExperience.Experience;
}
