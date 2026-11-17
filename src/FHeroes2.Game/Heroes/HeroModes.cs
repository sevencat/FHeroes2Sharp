namespace FHeroes2.Game.Heroes;

/// <summary>
/// The mode bits of a hero, the subset of <c>Heroes::Modes</c> (<c>heroes.h:196</c>) that the port needs —
/// the hiring rules, the recruitment pool and the battle's "already cast this round" flag.
/// </summary>
/// <remarks>
/// <para>
/// The values are the reference's bit positions and must not be renumbered: they are also what a savegame
/// stores. The remaining bits (<c>NOTDEFAULTS</c>, <c>HOURS</c>, <c>PATROL</c>, <c>UNKNOWN</c>,
/// <c>SHIPMASTER</c>, <c>JAIL</c>-adjacent battle modes and the AI helper bits) arrive with the systems
/// that set them; adding a bit ahead of its user would be dead weight.
/// </para>
/// <para>
/// <see cref="Recruit"/> does not mean "hired": it marks a hero that some kingdom already offers in its
/// tavern (<c>Heroes::RECRUIT</c>), which is why <see cref="AllHeroes.GetHeroForHire"/> tries to avoid
/// those heroes. <see cref="Jail"/> is the prison of the campaign maps, <see cref="Custom"/> marks a hero
/// with a custom portrait — the recruitment pool avoids handing out two heroes with the same portrait.
/// </para>
/// </remarks>
[Flags]
public enum HeroModes : uint
{
    /// <summary>No mode set.</summary>
    None = 0x0000_0000,

    /// <summary>
    /// The hero has already cast a battle spell this round, <c>Heroes::SPELLCASTED</c>
    /// (<c>heroes.h:200</c>). <c>Battle::Force::NewTurn()</c> clears it at the start of every round.
    /// </summary>
    Spellcasted = 0x0000_0004,

    /// <summary>
    /// The hero may still move today, <c>Heroes::ENABLEMOVE</c> (<c>heroes.h:195</c>). Set at the start of a
    /// turn and cleared by <see cref="Hero"/> when the last movement point is spent.
    /// </summary>
    MoveEnabled = 0x0000_0008,

    /// <summary>The hero is offered for recruitment in at least one kingdom, <c>Heroes::RECRUIT</c>.</summary>
    Recruit = 0x0000_0040,

    /// <summary>The hero sits in a prison and cannot be hired, <c>Heroes::JAIL</c>.</summary>
    Jail = 0x0000_0080,

    /// <summary>
    /// The hero is spending his action on the tile he stands on, <c>Heroes::ACTION</c>
    /// (<c>heroes.h:203</c>). <c>Move()</c> refuses to start while it is set, which is what keeps the
    /// walking animation and the action animation from running at the same time.
    /// </summary>
    Action = 0x0000_0100,

    /// <summary>The hero has a custom portrait, <c>Heroes::CUSTOM</c>.</summary>
    Custom = 0x0000_1000,

    /// <summary>
    /// The hero is a guard that keeps to the area around his position, <c>Heroes::PATROL</c>
    /// (<c>heroes.h:214</c>).
    /// </summary>
    Patrol = 0x0000_8000,
}
