namespace Daoris.Driver;

/// <summary>
/// What following a repository's line came to (WSSETUP5, D124 §3.4): a word from a fixed list, which the machine log's
/// <c>registry.followed</c> carries and the row is keyed on. Never a path or a summary.
/// </summary>
public static class RegistryOutcome
{
    /// <summary>The line's declaration was sent, and taken.</summary>
    public const string Registered = "registered";

    /// <summary>The row already holds what the line declares: nothing was sent.</summary>
    public const string Unchanged = "unchanged";

    /// <summary>Adopted on its line, declaring nothing: adoption recorded, the declaration left as the row held it.</summary>
    public const string DeclaresNothing = "declares-nothing";

    /// <summary>No <c>daoris.json</c> on the line.</summary>
    public const string NotSetUp = "not-set-up";

    /// <summary>No line known here, or none git can read.</summary>
    public const string NoLine = "no-line";

    /// <summary>The manifest on the line does not read, or holds a field that is refused.</summary>
    public const string Unreadable = "unreadable";

    /// <summary>The lanes file on the line is refused whole.</summary>
    public const string LanesUnreadable = "lanes-unreadable";

    /// <summary>The row's root is a linked worktree, which is never registered.</summary>
    public const string Worktree = "worktree";

    /// <summary>A row with no root here: its own machine's driver registers it.</summary>
    public const string NoCheckout = "no-checkout";

    /// <summary>A repository asked for by name that this machine's registry does not hold.</summary>
    public const string NotOnRegistry = "not-on-registry";

    /// <summary>The service refused the registration.</summary>
    public const string Refused = "refused";

    public static IReadOnlyList<string> All { get; } =
        [Registered, Unchanged, DeclaresNothing, NotSetUp, NoLine, Unreadable, LanesUnreadable, Worktree, NoCheckout, NotOnRegistry, Refused];

    /// <summary>Whether an outcome registered nothing because something is in the way.</summary>
    public static bool IsRefusal(string outcome) => outcome is not (Registered or Unchanged or DeclaresNothing);
}

/// <summary>A registry row as the service answers it, with the declaration it holds (the registry door, D48 §7).</summary>
/// <param name="Joined">What the service stored: a join is only an adopted repository's.</param>
/// <param name="SharesKnowledge">What the service stored: knowledge only from a joined repository.</param>
/// <param name="Uses">What it says it uses, as the service read it by the rule.</param>
/// <param name="Lanes">Its lanes' words; none when unstated.</param>
public sealed record RegistrationRow(
    string Repository, string Workspace, string? Root, bool Adopted, string? Summary, IReadOnlyList<string> Owns,
    IReadOnlyList<string> Accepts, IReadOnlyList<string> Uses, IReadOnlyList<string> Packs, bool Joined, bool SharesKnowledge,
    IReadOnlyList<LaneView> Lanes)
{
    /// <summary>
    /// The service's own word that the row is registered: adopted, and declaring something (its <c>Registration.Registered</c>).
    /// Read from the door, never recomputed here, so a workspace plan's <i>set up</i> is the service's (WSSETUP6). Absent is
    /// false: a host that does not say it is not taken to have said yes.
    /// </summary>
    public bool Registered { get; init; }
}

/// <summary>What a repository's line holds of the two files a registration reads (D124 §3.2), read as git objects.</summary>
/// <param name="Line">The line, where one is known.</param>
/// <param name="Commit">The commit the line names, which both files were read at.</param>
/// <param name="Problem">Why the line could not be read; null when it was.</param>
public sealed record LineFiles(string? Line, string? Commit, string? Problem)
{
    /// <summary><c>daoris.json</c>'s text at the commit; null where the line holds none.</summary>
    public string? Manifest { get; init; }

    /// <summary><c>daoris.lanes.json</c>'s text at the commit; null where the line holds none.</summary>
    public string? Lanes { get; init; }

    /// <summary>A file the line holds as something other than a file (a link, a folder); null when each is a file or absent.</summary>
    public string? FileProblem { get; init; }
}

/// <summary>What following one repository's line came to.</summary>
/// <param name="Outcome">One of <see cref="RegistryOutcome"/>'s words.</param>
/// <param name="Said">The sentence the row says, and a terminal: names, a line and a short commit, never a path.</param>
/// <param name="Sent">Whether a registration was sent and taken.</param>
public sealed record RegistrationFollowed(
    string Repository, string Outcome, string Said, string? Line = null, string? Commit = null, bool Sent = false)
{
    /// <summary>
    /// The answer <c>REGISTRY_REFRESH</c> gives for this repository (RPCOPEN1), which <c>daoris-driver register</c> reads back into the
    /// record it prints from (<see cref="Read"/>): beside the page's fields, <paramref name="refresh"/> where the index was not read again.
    /// </summary>
    public object ToWire(string? refresh) => new
    {
        Repository, Outcome, Said, Line, Commit, Registered = Sent, Refused = RegistryOutcome.IsRefusal(Outcome), Refresh = refresh,
    };

    /// <summary>The record an answer carries, with the refresh it names; null where it is not one this terminal reads.</summary>
    public static (RegistrationFollowed Followed, string? Refresh)? Read(System.Text.Json.JsonElement answer)
    {
        string? Text(string name)
        {
            if (answer.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            foreach (var each in answer.EnumerateObject())
            {
                if (string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase) && each.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return each.Value.GetString();
                }
            }

            return null;
        }

        bool Flag(string name) =>
            answer.ValueKind == System.Text.Json.JsonValueKind.Object
            && answer.EnumerateObject().Any(each => string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase)
                                                    && each.Value.ValueKind == System.Text.Json.JsonValueKind.True);

        return Text("repository") is { } repository && Text("outcome") is { } outcome && Text("said") is { } said
            ? (new RegistrationFollowed(repository, outcome, said, Text("line"), Text("commit"), Flag("registered")), Text("refresh"))
            : null;
    }
}

/// <summary>What one pass of following came to: each repository's, and what the refresh after them said where it failed.</summary>
public sealed record FollowReport(IReadOnlyList<RegistrationFollowed> Followed, string? Refresh);

/// <summary>What following last came to for one repository, as its row says it (WSSETUP5, D124 §3.4).</summary>
public sealed record FollowedEntry(string Repository, DateTimeOffset At, string Outcome, string Said, string? Line, string? Commit);
