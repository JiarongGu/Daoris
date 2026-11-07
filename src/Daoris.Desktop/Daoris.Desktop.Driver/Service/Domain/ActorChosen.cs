namespace Daoris.Driver;

/// <summary>
/// Why a role's policy chose a start's choice, as the service keeps it on the record (<c>chosen</c>): the rule the role ran
/// under, how this choice came about (<see cref="StaffBy"/>), and, for a part that follows another choice, the condition and the choice
/// it left. Machine-local; a record made under no policy has none.
/// </summary>
public sealed record ActorChosen(string Rule, string By, string? Condition = null, string? From = null);
