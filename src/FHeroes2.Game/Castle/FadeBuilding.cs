namespace FHeroes2.Game.Castle;

/// <summary>
/// The fade-in of a building that was just bought, <c>CastleDialog::FadeBuilding</c>
/// (<c>castle.h:446-510</c>, <c>castle_building.cpp:479-493</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference asks <c>Game::validateAnimationDelay( CASTLE_ITEM_UPDATE_DELAY )</c> before moving the alpha; the
/// port has no frame clock down here, so <see cref="Update"/> takes the answer as an argument and the town screen
/// feeds it. Everything else — the nine-step ramp, the two starting states and the "done" test — is the reference's.
/// </para>
/// <para>
/// A boat is a special case: it fades in with the shipyard's own alpha but the shipyard stays drawn behind it, which
/// is why <see cref="IsOnlyBoat"/> travels next to the building id instead of being inferred from it.
/// </para>
/// </remarks>
public sealed class FadeBuilding
{
    /// <summary>How far the alpha moves per accepted update, <c>_alpha += 9</c>.</summary>
    private const int AlphaStep = 9;

    /// <summary>The building whose fade is running, <c>_building</c>.</summary>
    public BuildingType Building { get; private set; } = BuildingType.Nothing;

    /// <summary>How opaque the fading building is, <c>_alpha</c>: 0 is invisible, 255 is finished.</summary>
    public byte Alpha { get; private set; } = byte.MaxValue;

    /// <summary>Whether the fade is the shipyard's boat rather than the shipyard itself, <c>_isOnlyBoat</c>.</summary>
    public bool IsOnlyBoat { get; private set; }

    /// <summary>Whether nothing is fading right now, <c>isFadeDone()</c>: the alpha is at its maximum.</summary>
    public bool IsFadeDone => Alpha == byte.MaxValue;

    /// <summary>Whether a fade is under way, the negation of <see cref="IsFadeDone"/>.</summary>
    public bool IsFadeInProgress => Alpha < byte.MaxValue;

    /// <summary>Starts fading a building in from nothing, <c>startFadeBuilding()</c>.</summary>
    /// <param name="building">The building that was just bought.</param>
    public void StartFadeBuilding(BuildingType building)
    {
        Alpha = 0;
        Building = building;
        IsOnlyBoat = false;
    }

    /// <summary>Starts fading the shipyard's boat in, <c>startFadeBoat()</c>.</summary>
    /// <remarks>The building is always the shipyard; only the boat is drawn with the alpha, hence the flag.</remarks>
    public void StartFadeBoat()
    {
        Alpha = 0;
        Building = BuildingType.Shipyard;
        IsOnlyBoat = true;
    }

    /// <summary>Ends the fade at once, <c>stopFade()</c>.</summary>
    public void StopFade()
    {
        Alpha = byte.MaxValue;
        Building = BuildingType.Nothing;
        IsOnlyBoat = false;
    }

    /// <summary>
    /// Moves the alpha one step if a fade is running and the frame is due, <c>updateFadeAlpha()</c>.
    /// </summary>
    /// <param name="delayElapsed">Whether <c>CASTLE_ITEM_UPDATE_DELAY</c> has expired, as the frame clock decides.</param>
    /// <returns><see langword="true"/> when the alpha moved, which is what the caller redraws on.</returns>
    public bool Update(bool delayElapsed)
    {
        if (!delayElapsed || !IsFadeInProgress)
        {
            return false;
        }

        Alpha = Alpha < byte.MaxValue - AlphaStep ? (byte)(Alpha + AlphaStep) : byte.MaxValue;

        return true;
    }
}
