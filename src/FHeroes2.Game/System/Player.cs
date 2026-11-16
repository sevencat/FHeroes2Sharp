using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.System;

/// <summary>
/// One of the six slots of a map, <c>class Player</c> (<c>players.h:100-205</c>).
/// </summary>
/// <remarks>
/// <para>
/// A player is a colour, a race, a controller, the colours it is allied with, a name and — when the adventure map
/// is being played — where its camera looks. It is <b>not</b> a kingdom: the kingdom is found through the colour,
/// and a player can exist for a colour whose kingdom has not been built yet.
/// </para>
/// <para>
/// The reference inherits from <c>BitModes</c> and from <c>Control</c>; the port inherits the first and folds the
/// second into a property plus the two extension methods of <see cref="PlayerControlExtensions"/>. The single flag
/// stored in the bit word is <see cref="Players.StInGame"/>, which the serialiser shared with all the other
/// <c>BitModes</c> users — a save file writes the word before the player's own fields.
/// </para>
/// <para>
/// The AI auto-control pair needs a word of explanation, because the two booleans look redundant. Turning the
/// mode <b>on</b> takes effect at once; turning it <b>off</b> only records the intention, because the AI may be
/// in the middle of its turn and has to finish it. <see cref="CommitAIAutoControlMode"/> is what the game calls at
/// the end of that turn, and it is the only place where the actual value follows the planned one.
/// </para>
/// </remarks>
public sealed class Player : BitModes
{
    /// <summary>
    /// How much of a human player's income the handicap takes away, <c>Player::HandicapStatus</c>
    /// (<c>players.h:103-108</c>).
    /// </summary>
    public enum HandicapStatus : byte
    {
        /// <summary>No strings attached.</summary>
        None = 0,

        /// <summary>15% fewer resources per turn.</summary>
        Mild = 1,

        /// <summary>30% fewer resources per turn.</summary>
        Severe = 2,
    }

    /// <summary>
    /// A player of the given colour, with a random personality, <c>Player::Player()</c>
    /// (<c>players.cpp:117-122</c>).
    /// </summary>
    /// <param name="color">The colour; <see cref="PlayerColor.None"/> for a slot not yet assigned.</param>
    public Player(PlayerColor color = PlayerColor.None)
    {
        Personality = AiPersonalityHelper.Random();
        Color = color;
        Friends = color;
    }

    /// <summary>The name the player was given, or an empty string while it is still the default one.</summary>
    public string CustomName { get; private set; } = string.Empty;

    /// <summary>What the adventure map is looking at, <c>GetFocus()</c>.</summary>
    public PlayerFocus Focus { get; } = new PlayerFocus();

    /// <summary>How the computer plays this slot, <c>AI::Personality</c>.</summary>
    public AiPersonality Personality { get; set; }

    /// <summary>The control flags as they were assigned, without the AI auto-control override.</summary>
    public PlayerControl Control { get; set; }

    /// <summary>The race of the player's kingdom, <c>Race::NONE</c> in the reference.</summary>
    public MapRace Race { get; set; } = MapRace.None;

    /// <summary>The colour of the slot, <c>GetColor()</c>.</summary>
    public PlayerColor Color { get; set; }

    /// <summary>The colours this player may not attack, <c>GetFriends()</c>; always contains its own.</summary>
    public PlayerColor Friends { get; set; }

    /// <summary>The handicap of a human player, <c>getHandicapStatus()</c>.</summary>
    public HandicapStatus Handicap { get; private set; } = HandicapStatus.None;

    /// <summary>Whether the computer is playing for this human slot right now, <c>isAIAutoControlMode()</c>.</summary>
    public bool IsAiAutoControlMode { get; private set; }

    /// <summary>Whether the mode will be on after the AI finishes, <c>_isAIAutoControlModePlanned</c>.</summary>
    private bool isAiAutoControlModePlanned;

    /// <summary>
    /// Whether the player takes part in the game, the <c>ST_INGAME</c> bit of the mode word.
    /// </summary>
    public bool IsPlay => HasModes(Players.StInGame);

    /// <summary>
    /// Who actually drives this slot, <c>Player::GetControl()</c> (<c>players.cpp:130-140</c>).
    /// </summary>
    /// <remarks>
    /// A human slot handed to the computer answers <see cref="PlayerControl.Ai"/>, not <c>Human | Ai</c>: the game
    /// asks this question to decide who moves next, and an answer with both bits would make it ask a human for a
    /// move it has already given to the AI. The stored flags are untouched, which is what lets the mode be
    /// switched off again.
    /// </remarks>
    public PlayerControl EffectiveControl => IsAiAutoControlMode ? PlayerControl.Ai : Control;

    /// <summary>Whether a person drives the slot right now, <c>isControlHuman()</c>.</summary>
    public bool IsControlHuman => EffectiveControl.IsHuman();

    /// <summary>Whether the computer drives the slot right now, <c>isControlAI()</c>.</summary>
    public bool IsControlAi => EffectiveControl.IsAi();

    /// <summary>The colour as a printable word, <c>GetDefaultName()</c>.</summary>
    public string DefaultName => PlayerColors.Describe(Color);

    /// <summary>Whether the name is still the default one, <c>isDefaultName()</c>.</summary>
    public bool IsDefaultName => CustomName.Length == 0;

    /// <summary>The name to draw, <c>Player::GetName()</c>: the custom one, else the colour.</summary>
    public string Name => CustomName.Length == 0 ? DefaultName : CustomName;

    /// <summary>How the personality is printed, <c>Player::GetPersonalityString()</c>.</summary>
    public string PersonalityString => AiPersonalityHelper.GetString(Personality);

    /// <summary>
    /// Whether the slot has the given colour, <c>isColor()</c>.
    /// </summary>
    /// <param name="color">The colour to compare with.</param>
    /// <returns>True when the colours are equal.</returns>
    public bool IsColor(PlayerColor color) => color == Color;

    /// <summary>
    /// Names the player, <c>Player::SetName()</c> (<c>players.cpp:152-160</c>).
    /// </summary>
    /// <remarks>
    /// Giving back the default name clears the custom one instead of storing it, so that a player renamed "Blue"
    /// and a player never renamed are indistinguishable — which matters when a save file is written and read by a
    /// different translation.
    /// </remarks>
    /// <param name="name">The new name.</param>
    public void SetName(string name) => CustomName = name == DefaultName ? string.Empty : name;

    /// <summary>
    /// Marks the slot as taking part in the game or not, <c>Player::SetPlay()</c>.
    /// </summary>
    /// <param name="play">True to put the player in the game.</param>
    public void SetPlay(bool play)
    {
        if (play)
        {
            SetModes(Players.StInGame);
        }
        else
        {
            ResetModes(Players.StInGame);
        }
    }

    /// <summary>
    /// Sets the handicap, <c>Player::setHandicapStatus()</c> (<c>players.cpp:173-186</c>).
    /// </summary>
    /// <remarks>
    /// The reference asserts that the player is not a computer one; the port cannot assert, so a handicap set on an
    /// AI slot is simply stored. Nothing in the game does that today — the handicap screen only offers human slots.
    /// </remarks>
    /// <param name="status">The handicap.</param>
    public void SetHandicapStatus(HandicapStatus status) => Handicap = status;

    /// <summary>
    /// Switches the AI auto-control mode, <c>Player::setAIAutoControlMode()</c> (<c>players.cpp:188-204</c>).
    /// </summary>
    /// <remarks>
    /// Enabling takes effect immediately; disabling only records the plan, because the AI turn in progress has to
    /// be finished first. See the class remarks.
    /// </remarks>
    /// <param name="enable">True to hand the slot to the computer.</param>
    public void SetAiAutoControlMode(bool enable)
    {
        if (enable)
        {
            IsAiAutoControlMode = true;
        }

        isAiAutoControlModePlanned = enable;
    }

    /// <summary>
    /// Applies the planned mode, <c>Player::commitAIAutoControlMode()</c> (<c>players.cpp:206-215</c>).
    /// </summary>
    /// <remarks>
    /// Called at the end of the AI turn; before that the actual value must stay as it was so that the turn that is
    /// running keeps asking the AI for its moves.
    /// </remarks>
    public void CommitAiAutoControlMode()
    {
        if (!IsAiAutoControlMode)
        {
            return;
        }

        IsAiAutoControlMode = isAiAutoControlModePlanned;
    }
}
