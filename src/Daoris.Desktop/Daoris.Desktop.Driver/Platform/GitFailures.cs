namespace Daoris.Driver;

/// <summary>Whether a remote act reads or writes (the git-auth design §5.1, §6.1).</summary>
public enum GitAct
{
    Read,
    Write,
}

/// <summary>
/// The classes of a remote act's failure GITAUTH1a holds on (the git-auth design §5.1): those today's ambient credentials meet,
/// and GITAUTH1b's <c>own-input</c>. The identity's and the judge's classes (<c>no-identity</c>, <c>denied</c>, <c>ask</c>) join with
/// the rows that make them.
/// </summary>
public enum GitFailureClass
{
    /// <summary>Any other failure: holds nothing, said per occasion as before.</summary>
    Unclassed,

    /// <summary>The network's: a name not resolved, a connection refused or timed out. Holds nothing; the minute bounds the next try.</summary>
    Unreachable,

    /// <summary>The host refused the sign-in, or none answered: holds reads and writes.</summary>
    Refused,

    /// <summary>The host refused a push it would read: holds writes alone.</summary>
    WriteRefused,

    /// <summary>ssh does not know the host's key: holds reads and writes.</summary>
    HostUnknown,

    /// <summary>The host's key is not the one known for it: holds reads and writes.</summary>
    HostChanged,

    /// <summary>
    /// An input the doors cannot outrank (§4.2): a repository's own <c>core.sshCommand</c> where the ssh door is named by
    /// <c>GIT_SSH</c>, which that setting outranks. Never read from words: <see cref="GitOwnInput"/> finds it before an act.
    /// Holds reads and writes.
    /// </summary>
    OwnInput,
}

/// <summary>
/// A remote act's failure classed by the transport's own words (GITAUTH1a, D198 point 7; the git-auth design §5.1). The words
/// are the ones git, its curl and OpenSSH write, measured against real git and OpenSSH on local servers in
/// <c>GitFailuresProcessTests</c> and <c>GitPromptsProcessTests</c>, and recorded in the GITAUTH1a measurements; a host's own
/// sentence before them (<c>remote: …</c>) is never what classes a failure, since git and ssh say theirs after it.
/// </summary>
public static class GitFailures
{
    // Ordered: a changed key's warning ends in the same "verification failed" an unknown key's does.
    private static readonly string[] HostChanged = ["REMOTE HOST IDENTIFICATION HAS CHANGED", "has changed and you have requested strict checking"];

    private static readonly string[] HostUnknown = ["Host key verification failed", "host key is known for", "No matching host key"];

    private static readonly string[] Refused =
    [
        "Permission denied (", // ssh: every method the host offered was refused, `(publickey)`, `(password,publickey)`
        "Authentication failed for", // git over HTTP: a credential answered and the host refused it
        "terminal prompts disabled", // git over HTTP: none answered, and git may ask nobody
        "could not read Username for",
        "could not read Password for",
        "The requested URL returned error: 401",
        "Too many authentication failures",
    ];

    private const string Forbidden = "The requested URL returned error: 403";

    private static readonly string[] Unreachable =
    [
        "Could not resolve hostname", // ssh
        "Could not resolve host", // curl
        "Connection refused",
        "Connection timed out",
        "Operation timed out",
        "Network is unreachable",
        "No route to host",
        "Failed to connect to", // curl
        "Connection reset",
        "Connection closed by remote host",
        "kex_exchange_identification",
    ];

    /// <summary>The class of <paramref name="words"/>, a remote act's stderr, for an act that <paramref name="act"/>.</summary>
    public static GitFailureClass Class(string? words, GitAct act)
    {
        if (string.IsNullOrWhiteSpace(words)) return GitFailureClass.Unclassed;
        if (Has(words, HostChanged)) return GitFailureClass.HostChanged;
        if (Has(words, HostUnknown)) return GitFailureClass.HostUnknown;
        if (words.Contains(Forbidden, StringComparison.Ordinal)) return act == GitAct.Write ? GitFailureClass.WriteRefused : GitFailureClass.Refused;
        if (Has(words, Refused)) return GitFailureClass.Refused;
        if (Has(words, Unreachable)) return GitFailureClass.Unreachable;
        return GitFailureClass.Unclassed;
    }

    /// <summary>Whether a class holds anything (§5.2): the network's and the unclassed hold nothing.</summary>
    public static bool Holds(GitFailureClass failure) => failure is not (GitFailureClass.Unclassed or GitFailureClass.Unreachable);

    /// <summary>Whether a class holds writes alone (§5.1).</summary>
    public static bool WritesAlone(GitFailureClass failure) => failure == GitFailureClass.WriteRefused;

    /// <summary>The class's code, as the hold, the machine log and the page name it (§11).</summary>
    public static string Code(GitFailureClass failure) => failure switch
    {
        GitFailureClass.Refused => "refused",
        GitFailureClass.WriteRefused => "write-refused",
        GitFailureClass.HostUnknown => "host-unknown",
        GitFailureClass.HostChanged => "host-changed",
        GitFailureClass.OwnInput => "own-input",
        GitFailureClass.Unreachable => "unreachable",
        _ => "unclassed",
    };

    /// <summary>The class a code names, or null for one this build does not know.</summary>
    public static GitFailureClass? OfCode(string? code) => code switch
    {
        "refused" => GitFailureClass.Refused,
        "write-refused" => GitFailureClass.WriteRefused,
        "host-unknown" => GitFailureClass.HostUnknown,
        "host-changed" => GitFailureClass.HostChanged,
        "own-input" => GitFailureClass.OwnInput,
        _ => null,
    };

    private static bool Has(string words, string[] any) => any.Any(each => words.Contains(each, StringComparison.OrdinalIgnoreCase));
}
