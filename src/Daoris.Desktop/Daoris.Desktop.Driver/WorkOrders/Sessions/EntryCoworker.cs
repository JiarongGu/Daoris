namespace Daoris.Driver;

/// <summary>A co-worker of an entry: a record serving one of its parts, drawn inside that part's session and never listed as its own.</summary>
/// <param name="Session">The co-worker's record.</param>
/// <param name="Role">One of <see cref="LineRole"/> other than a part.</param>
/// <param name="Serves">The part it serves.</param>
/// <param name="Shown">The word its own row shows (<see cref="SessionGrouping.Shown"/>).</param>
/// <param name="State">The state its own row is in.</param>
public sealed record EntryCoworker(string Session, string Role, string Serves, string Shown, string State);
