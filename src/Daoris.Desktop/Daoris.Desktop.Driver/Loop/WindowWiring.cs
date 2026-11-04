namespace Daoris.Driver;

/// <summary>What only the window knows (design §6): a terminal's reader is handed none, and says <c>window</c> for each.</summary>
public sealed record WindowWiring
{
    public const string Hosted = "hosted";
    public const string Adopted = "adopted";
    public const string None = "none";

    /// <summary>Whether this window started its service, adopted one already running, or has none: <see cref="Hosted"/>, <see cref="Adopted"/>, <see cref="None"/>.</summary>
    public string Held { get; init; } = None;

    /// <summary>How the home stands to the account's <c>DAORIS_HOME</c> (LEFT2): <c>same</c>, <c>overridden</c>, <c>this-start</c>.</summary>
    public string? HomeAccount { get; init; }

    /// <summary>Whether this window set its own home (D63's establishing): the home variable's source is then Daoris.</summary>
    public bool HomeSetByDaoris { get; init; }

    /// <summary>What establishing the home said, verbatim, before it is redacted.</summary>
    public string? HomeNotice { get; init; }

    /// <summary>What the adopted host's page said, verbatim, before it is redacted.</summary>
    public string? HostNotice { get; init; }

    /// <summary>The address this window uses, as it was handed.</summary>
    public string? ServiceUrl { get; init; }

    /// <summary>The supervisor's last probe of that address; null where it has asked nothing.</summary>
    public ServiceProbe? Status { get; init; }

    /// <summary>Whether the host proved it holds this start's person key (PERSONDOOR1); null where nothing was asked.</summary>
    public bool? Proved { get; init; }

    /// <summary>The UI kit's version.</summary>
    public string? Kit { get; init; }

    /// <summary>This program's informational version, as the release workflow stamped it.</summary>
    public string? Program { get; init; }

    /// <summary>When the loop last looked; null before its first look.</summary>
    public DateTimeOffset? Looked { get; init; }

    /// <summary>Whether the loop's service is up (<c>STATE</c>'s <c>ready</c>).</summary>
    public bool Ready { get; init; }

    /// <summary>Whether an update drains the loop; null where no update is wired.</summary>
    public bool? Draining { get; init; }

    /// <summary>This window's process, which a holder of the home is compared to.</summary>
    public int ProcessId { get; init; }
}
