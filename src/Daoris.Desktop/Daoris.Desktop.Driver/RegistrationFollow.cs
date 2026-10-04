using System.Text.Json;
using System.Text.Json.Nodes;

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
    string Repository, string Outcome, string Said, string? Line = null, string? Commit = null, bool Sent = false);

/// <summary>What one pass of following came to: each repository's, and what the refresh after them said where it failed.</summary>
public sealed record FollowReport(IReadOnlyList<RegistrationFollowed> Followed, string? Refresh);

/// <summary>What following reads and writes (WSSETUP5): the registry door, the line, and where each outcome is kept.</summary>
/// <remarks>An interface so every case is held without a service or git; <see cref="RegistrationWorld"/> is the real one.</remarks>
public interface IRegistrationWorld
{
    /// <summary>Whether the service is on this machine, so the root travels (D46, as <c>connect</c>'s <c>isLocalService</c>).</summary>
    bool LocalService { get; }

    /// <summary>Every row this machine's host holds, with its declaration.</summary>
    Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct);

    /// <summary>The two files on the row's line (D86), as git objects at the line's commit.</summary>
    Task<LineFiles> ReadLineAsync(RegistrationRow row, CancellationToken ct);

    /// <summary>The main tree a root is a linked worktree of, or null for a main tree (<c>connect</c>'s <c>linkedWorktreeMain</c>).</summary>
    string? WorktreeMain(string root);

    /// <summary>The branch a set-up's landing made and that still waits for review, where one is recorded (D102); else null.</summary>
    string? SetupWaiting(string repository);

    /// <summary>Send a registration through the registry door, as <c>connect</c> sends one: taken, or the service's words.</summary>
    Task<(bool Ok, string Message)> RegisterAsync(JsonObject body, CancellationToken ct);

    /// <summary>Ask the service to read the index again, the refresh every door has; null when it did, else why not.</summary>
    Task<string?> RefreshAsync(CancellationToken ct);

    /// <summary>One repository followed: kept for its row, and handed to whoever logs it.</summary>
    void Followed(RegistrationFollowed what);
}

/// <summary>
/// Registration follows the line (WSSETUP5, D124 §3): the driver registers a repository from <c>daoris.json</c> and
/// <c>daoris.lanes.json</c> read on its line as git objects, sending what <c>connect</c> would send
/// (<see cref="LineRegistration"/>), for the checkout's root and never a tree, and only where the row holds something
/// else.
/// </summary>
/// <remarks>
/// <para><b>Three moments, never every tick</b> (§3.1): after Daoris moves a line (a landing under <c>merge</c>, a
/// fast-forward of <i>Bring up to date</i>, both recorded by <see cref="SessionTrees"/> in <see cref="RegistryFollowing"/>),
/// once as a watch starts, and when the person asks (<c>daoris-driver register</c>, the row's <i>Refresh</i>).</para>
///
/// <para><b>Every refusal is said</b> (§3.4): on the row (<see cref="RegistryFollowing"/> keeps each outcome's sentence),
/// in the machine log (<c>registry.followed</c>, a name and a word), and, for what changed or what the person must fix,
/// in the tick's report or the terminal. A standing state (not set up, no line) is kept and logged, and not said at
/// every start.</para>
///
/// <para><b>Registering does not re-index</b> (§3.3): once anything was sent, the service is asked for the refresh every
/// door already has, once per pass.</para>
/// </remarks>
public static class RegistrationFollow
{
    /// <summary>
    /// Follow each repository with a checkout here, or those <paramref name="only"/> names: read its line, compose what
    /// <c>connect</c> would send, and send it where the row holds something else.
    /// </summary>
    /// <param name="only">Repositories by name, compared without case; null for every row with a root here.</param>
    public static async Task<FollowReport> FollowAsync(
        IRegistrationWorld world, IReadOnlyCollection<string>? only, CancellationToken ct = default)
    {
        var rows = await world.RegistrationsAsync(ct).ConfigureAwait(false);
        var followed = new List<RegistrationFollowed>();
        if (only is null)
        {
            foreach (var row in rows.Where(row => !string.IsNullOrWhiteSpace(row.Root)).OrderBy(row => row.Repository, StringComparer.Ordinal))
            {
                followed.Add(await OneAsync(world, row, ct).ConfigureAwait(false));
            }
        }
        else
        {
            foreach (var name in only.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var row = rows.FirstOrDefault(each => string.Equals(each.Repository, name.Trim(), StringComparison.OrdinalIgnoreCase));
                followed.Add(row is null
                    ? new(name.Trim(), RegistryOutcome.NotOnRegistry,
                        $"`{name.Trim()}` is not on this machine's registry, so there is nothing to register. Add it on Repositories, "
                        + "or with `daoris import <folder> --workspace <name>`.")
                    : await OneAsync(world, row, ct).ConfigureAwait(false));
            }
        }

        foreach (var each in followed) world.Followed(each);
        var refresh = followed.Any(each => each.Sent) ? await world.RefreshAsync(ct).ConfigureAwait(false) : null;
        return new(followed, refresh);
    }

    /// <summary>
    /// What a tick's report or a terminal says of one outcome: what was sent, and what the person must fix. Null for a
    /// standing state, which the row and the log keep: unchanged, not set up, no line, no checkout here.
    /// </summary>
    public static string? EventLine(RegistrationFollowed followed) =>
        followed.Outcome switch
        {
            RegistryOutcome.Unchanged or RegistryOutcome.NotSetUp or RegistryOutcome.NoLine or RegistryOutcome.NoCheckout => null,
            RegistryOutcome.Registered or RegistryOutcome.DeclaresNothing when !followed.Sent => null,
            _ => $"registry  {followed.Repository}: {followed.Said}",
        };

    private static async Task<RegistrationFollowed> OneAsync(IRegistrationWorld world, RegistrationRow row, CancellationToken ct)
    {
        var name = row.Repository;
        if (string.IsNullOrWhiteSpace(row.Root))
        {
            return new(name, RegistryOutcome.NoCheckout,
                $"`{name}` has no checkout here: its row is a teammate's registration, and its own machine's driver registers it.");
        }

        // `connect` refuses a linked worktree; a row's root never is one (an import never makes one), and is held anyway.
        if (world.WorktreeMain(row.Root) is not null)
        {
            return new(name, RegistryOutcome.Worktree,
                $"`{name}`'s root here is a linked worktree, a session's scratch tree that goes once its work lands, and is "
                + "never registered. Register the repository from its main tree.");
        }

        var files = await world.ReadLineAsync(row, ct).ConfigureAwait(false);
        if (files.Line is null || files.Commit is null)
        {
            return new(name, RegistryOutcome.NoLine,
                // HELPSETUP1: a repository's line is set on its Setup since UX6f (D150 §4.2).
                $"`{name}` has no line here to register from: {files.Problem ?? "git names none"}. Set one with "
                + $"`daoris driver line {name} <branch>`, or on Repositories → the repository's page → Setup → Line and landing.");
        }

        var at = $"`{files.Line}` at `{Short(files.Commit)}`";
        if (files.FileProblem is { } odd)
        {
            return new(name, RegistryOutcome.Unreadable, $"{odd} on its line {at}. Nothing was registered; its row keeps what it held.",
                files.Line, files.Commit);
        }

        if (files.Manifest is null)
        {
            var waiting = world.SetupWaiting(name);
            return new(name, RegistryOutcome.NotSetUp,
                $"`{name}` is not set up on its line `{files.Line}`: no {LineRegistration.ManifestFile} there"
                + (waiting is not null
                    ? $". Its set-up waits for your review on `{waiting}`; once it is merged, *Bring up to date* registers it."
                    : $". `daoris-driver setup {name}` asks its own session to set it up."),
                files.Line, files.Commit);
        }

        var composed = LineRegistration.Compose(name, files.Manifest, files.Lanes, row.Root, world.LocalService);
        if (composed.ManifestProblem is { } problem)
        {
            return new(name, RegistryOutcome.Unreadable,
                $"its {LineRegistration.ManifestFile} on {at} does not read: {problem}. Nothing was registered; its row keeps what it held.",
                files.Line, files.Commit);
        }

        if (composed.LanesProblems.Count > 0)
        {
            return new(name, RegistryOutcome.LanesUnreadable,
                $"its {LineRegistration.LanesFile} on {at} cannot be read: {string.Join("; ", composed.LanesProblems)}. "
                + "Nothing was registered; its row keeps what it held.",
                files.Line, files.Commit);
        }

        // An empty declaration is worse than none (connect's rule): adoption is recorded, the declaration left as held.
        var body = composed.Declares ? composed.Body! : Held(composed.Body!, row);
        var outcome = composed.Declares ? RegistryOutcome.Registered : RegistryOutcome.DeclaresNothing;
        if (Same(body, row))
        {
            return composed.Declares
                ? new(name, RegistryOutcome.Unchanged, $"its row already holds what its line {at} declares.", files.Line, files.Commit)
                : new(name, outcome, DeclaresNothingSaid(name, at), files.Line, files.Commit);
        }

        var (ok, message) = await world.RegisterAsync(body, ct).ConfigureAwait(false);
        if (!ok)
        {
            return new(name, RegistryOutcome.Refused,
                $"the service refused its registration from {at}: {message} Its row keeps what it held.", files.Line, files.Commit);
        }

        return new(name, outcome,
            composed.Declares
                ? $"registered from its line {at}: adopted, and declaring what it owns, with no `connect` run."
                : DeclaresNothingSaid(name, at),
            files.Line, files.Commit, Sent: true);
    }

    private static string DeclaresNothingSaid(string name, string at) =>
        $"adopted, declares nothing: its line {at} holds a {LineRegistration.ManifestFile} whose `domain` says nothing, so its "
        + $"adoption is recorded and the declaration is as the row held it. `daoris-driver setup {name}` asks its own session to "
        + "declare what it owns.";

    /// <summary>The body with the declaration the row holds in place of the line's empty one; none where it held none.</summary>
    private static JsonObject Held(JsonObject body, RegistrationRow row)
    {
        var held = (JsonObject)body.DeepClone();
        var declares = !string.IsNullOrWhiteSpace(row.Summary) || row.Owns.Count > 0 || row.Accepts.Count > 0 || row.Uses.Count > 0;
        JsonObject? domain = null;
        if (declares)
        {
            domain = new JsonObject
            {
                ["summary"] = row.Summary ?? "",
                ["owns"] = Strings(row.Owns),
                ["accepts"] = Strings(row.Accepts),
            };
            if (row.Uses.Count > 0) domain["uses"] = Strings(row.Uses);
        }

        held["domain"] = domain;
        return held;
    }

    /// <summary>
    /// Whether the row already holds what the body would store, read as the service stores it: adopted, the declaration's
    /// words (a blank summary is none), what it uses by the rule, the packs, the join and the knowledge as the service
    /// narrows them, and the lanes' words.
    /// </summary>
    private static bool Same(JsonObject body, RegistrationRow row)
    {
        var domain = body["domain"] as JsonObject;
        var join = body["join"]?.GetValue<bool>() == true;
        var knowledge = join && body["shareKnowledge"]?.GetValue<bool>() == true;
        var lanes = (body["lanes"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(lane => new LaneView(Text(lane["id"]) ?? "", Text(lane["title"]) ?? "", Text(lane["summary"]) ?? "", lane["steward"]?.GetValue<bool>() == true))
            .ToList();

        return row.Adopted
            && string.Equals(Blank(Text(domain?["summary"])), Blank(row.Summary), StringComparison.Ordinal)
            && List(domain?["owns"]).SequenceEqual(row.Owns, StringComparer.Ordinal)
            && List(domain?["accepts"]).SequenceEqual(row.Accepts, StringComparer.Ordinal)
            && LineRegistration.UsesOf(domain?["uses"], row.Repository).SequenceEqual(row.Uses, StringComparer.Ordinal)
            && List(body["packs"]).SequenceEqual(row.Packs, StringComparer.Ordinal)
            && join == row.Joined
            && knowledge == row.SharesKnowledge
            && lanes.SequenceEqual(row.Lanes);
    }

    private static string? Text(JsonNode? node) => node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static IReadOnlyList<string> List(JsonNode? node) =>
        node is JsonArray list ? [.. list.Select(Text).OfType<string>()] : [];

    private static JsonArray Strings(IEnumerable<string> values) => [.. values.Select(value => (JsonNode)JsonValue.Create(value))];

    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;
}
