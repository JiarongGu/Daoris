namespace Daoris.Driver;

/// <summary>
/// One transient release: the record's session <i>Try again</i> released, and the
/// end it released, the record's <c>updated</c> as the press read it.
/// </summary>
public sealed record TransientRelease(string Session, DateTimeOffset Ended);
