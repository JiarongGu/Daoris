namespace Daoris.Driver;

/// <summary>One account a start may use, as the walk found it (TOOL4f, D125 §3.3).</summary>
/// <param name="Account">The profile's name, or null for the tool's own configuration home.</param>
/// <param name="Readiness">Whether it may run, and if not, why.</param>
/// <param name="Cooling">Its cool-off, when that is why.</param>
/// <param name="Refusal">
/// The sentence a start held on this account alone would be refused with, the one it was refused with before rotation
/// existed: the hold, the refused key's, or the sign-in's.
/// </param>
public sealed record AccountState(string? Account, AccountReadiness Readiness, CoolingEntry? Cooling = null, string? Refusal = null)
{
    public bool IsReady => Readiness == AccountReadiness.Ready;

    /// <summary><see cref="Refusal"/> by code (ROSTERREFUSE1), its English the same to the byte; null where nothing composed it.</summary>
    public Noted? RefusalNote { get; init; }

    /// <summary>
    /// A ready account a start's walk passed because another question held its lock past the walk's patience (LOCKPASSED1,
    /// PARALLEL1c): still <see cref="AccountReadiness.Ready"/>, since it comes free by itself, and said by its own step.
    /// </summary>
    public bool Locked { get; init; }
}
