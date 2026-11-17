namespace FHeroes2.UI.Dialogs;

/// <summary>
/// "Build a new ship?", <c>Dialog::BuyBoat()</c> (<c>dialog.h:133</c>, <c>dialog_buyboat.cpp:38-92</c>).
/// </summary>
/// <remarks>
/// <para>
/// The dialog shows a boat, the words "Build a new ship:" and the resource cost of
/// <c>PaymentConditions::BuyBoat()</c>, then asks with OK and cancel. When the caller passes
/// <c>enable = false</c> the reference presses and disables the OK button - the dialog still opens but cannot buy.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet. The cost is not part of the model either, since
/// the price table (<c>PaymentConditions</c>) has no port yet and the dialog only displays it.
/// </para>
/// </remarks>
public sealed class BuyBoatDialog
{
    private BuyBoatDialog(bool isPurchaseEnabled)
    {
        IsPurchaseEnabled = isPurchaseEnabled;
    }

    /// <summary>
    /// Builds the dialog, <c>Dialog::BuyBoat( bool enable )</c> (<c>dialog_buyboat.cpp:38</c>).
    /// </summary>
    /// <param name="enable">Whether the OK button is usable, <c>enable</c> of the reference.</param>
    /// <returns>The model.</returns>
    public static BuyBoatDialog Create(bool enable) => new(enable);

    /// <summary>Whether the boat can be bought, i.e. the OK button is enabled.</summary>
    public bool IsPurchaseEnabled { get; }

    /// <summary>The buttons the dialog has.</summary>
    public DialogButton Buttons => DialogButton.Ok | DialogButton.Cancel;

    /// <summary>
    /// The result of pressing a button, <c>buttonGroup.processEvents()</c> at <c>dialog_buyboat.cpp:85</c>.
    /// </summary>
    /// <param name="index">Index of the pressed button: 0 OK, 1 cancel.</param>
    /// <returns><see cref="DialogButton.Ok"/> or <see cref="DialogButton.Cancel"/>.</returns>
    public static DialogButton FromButton(int index) =>
        index == 0 ? DialogButton.Ok : DialogButton.Cancel;
}
