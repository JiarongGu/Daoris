namespace Daoris.Driver;

/// <summary>
/// One line of This machine's list of old records (ONEWAY1ad-ii; the oneway design §1.3), which its page and Ask Daoris's room read alike: a
/// conversion part's census (<see cref="Part"/>), or a kept read of the register with its condition's state (<see cref="Read"/>).
/// </summary>
/// <param name="Kind"><see cref="Part"/> or <see cref="Read"/>.</param>
/// <param name="Id">The part's name, or the kept read's id.</param>
/// <param name="Ended">Whether nothing old is left: the part's census has ended, or the read's condition holds.</param>
public sealed record KeptLine(string Kind, string Id, bool Ended)
{
    public const string Part = "part";
    public const string Read = "read";

    /// <summary>A part's old records waiting.</summary>
    public int? Count { get; init; }

    /// <summary>A read's records or builds that still need the old way.</summary>
    public string? Reaches { get; init; }

    /// <summary>A read's condition, in words.</summary>
    public string? Ends { get; init; }

    /// <summary>The row that deletes a read.</summary>
    public string? Row { get; init; }

    /// <summary>The build a read kept for an older build waits out, said; null for one that waits on no build.</summary>
    public string? Waits { get; init; }
}
