namespace Daoris.Driver;

/// <summary>
/// What the roster needs of the ledger of role starts the roles keep above it (STAFFPOLICY1c): a refused start's account count given back
/// (<see cref="HarnessRoster.Release"/>) gives back the start's reservation with it.
/// </summary>
public interface IStartLedger
{
    /// <summary>The start on account count <paramref name="number"/> was refused before it opened: whatever it held goes back.</summary>
    void ReleaseBound(long number);
}
