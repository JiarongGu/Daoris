namespace Daoris.Driver;

/// <summary>The session's record as the service opened it, for the stages that hand the session its instruction and hold it to its end.</summary>
/// <param name="SessionId">The record's id.</param>
/// <param name="Keyed">The keyed open's answer, with the words it moved; null where the open was not keyed.</param>
/// <param name="Fenced">Why the carry-on's move record could not be written to fence the open, or null where it was fenced or there is no record.</param>
/// <param name="Continues">The session this start continues, why and who asked, as it is named on the record.</param>
/// <param name="Before">Where the tree stood before anything ran in it, which the review reads back on a later launch.</param>
/// <param name="OpenedAt">When the record opened.</param>
/// <param name="LocksTree">Whether the session's own tree is locked in its name while it holds it.</param>
internal sealed record RecordOpened(
    string SessionId, DestinationOpen? Keyed, string? Fenced, ContinuedFrom? Continues, string? Before, DateTimeOffset OpenedAt, bool LocksTree);
