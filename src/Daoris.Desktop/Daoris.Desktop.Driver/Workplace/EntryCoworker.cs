namespace Daoris.Driver;

/// <summary>A co-worker of an entry: a record serving one of its parts, drawn inside that part's session and never listed as its own.</summary>
/// <param name="Session">The co-worker's record.</param>
/// <param name="Role">One of the line's roles other than a part (<c>LineRole</c>, which Work orders reads above this).</param>
/// <param name="Serves">The part it serves.</param>
/// <param name="Shown">The word its own row shows (the sessions' grouping's <c>Shown</c>).</param>
/// <param name="State">The state its own row is in.</param>
public sealed record EntryCoworker(string Session, string Role, string Serves, string Shown, string State);
