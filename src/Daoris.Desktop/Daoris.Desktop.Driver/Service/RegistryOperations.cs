using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The codes a registry operation refuses in: <c>connect</c>, <c>retire</c>, <c>wire</c> and <c>import</c> on
/// <see cref="ServiceClient"/>. One table, read by the terminal's command; the window has no caller of these today, so none is
/// declared in its catalogue and the shell would say any of them verbatim.
/// </summary>
public static class RegistryCodes
{
    public const string LinkedWorktree = "REGISTRY_LINKED_WORKTREE";
    public const string NotDeclared = "REGISTRY_NOT_DECLARED";
    public const string ManifestUnreadable = "REGISTRY_MANIFEST_UNREADABLE";
    public const string LanesUnreadable = "REGISTRY_LANES_UNREADABLE";
    public const string ServiceRefused = "REGISTRY_REFUSED";
    public const string FolderNotLocal = "REGISTRY_FOLDER_NOT_LOCAL";

    /// <summary>Each check an operation refuses by, and the code it is said in.</summary>
    public static IReadOnlyDictionary<string, string> Table { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [RegistryRefusal.LinkedWorktree] = LinkedWorktree,
        [RegistryRefusal.NotDeclared] = NotDeclared,
        [RegistryRefusal.ManifestUnreadable] = ManifestUnreadable,
        [RegistryRefusal.LanesUnreadable] = LanesUnreadable,
        [RegistryRefusal.ServiceRefused] = ServiceRefused,
        [RegistryRefusal.FolderNotLocal] = FolderNotLocal,
    };

    /// <summary>The code a refusal is said in.</summary>
    public static string Of(RegistryRefusal refused) => Table[refused.Check];
}

/// <summary>
/// A registry operation refusing by a named check, in its sentence and with the exit the CLI's verb gave it (a policy refusal 1, the
/// rest 2). The service's own sentence is carried verbatim. Nothing is changed.
/// </summary>
public sealed class RegistryRefusal(string check, string message, int exit) : DriverException(message)
{
    public const string LinkedWorktree = "linked-worktree";
    public const string NotDeclared = "not-declared";
    public const string ManifestUnreadable = "manifest-unreadable";
    public const string LanesUnreadable = "lanes-unreadable";
    public const string ServiceRefused = "service-refused";
    public const string FolderNotLocal = "folder-not-local";

    public string Check { get; } = check;

    public int Exit { get; } = exit;
}

/// <summary>What <c>connect</c> sends: the repository, the service it goes to, and the registration body in the order the registry reads it.</summary>
public sealed record ConnectPlan(string Repository, string Service, JsonObject Body)
{
    private JsonObject? Domain => Body["domain"] as JsonObject;

    public int Owns => (Domain?["owns"] as JsonArray)?.Count ?? 0;

    public int Accepts => (Domain?["accepts"] as JsonArray)?.Count ?? 0;

    /// <summary>What the repository says it uses, by the rule.</summary>
    public IReadOnlyList<string> Uses => [.. (Domain?["uses"] as JsonArray ?? []).Select(use => (string)use!)];

    /// <summary>The lanes sent: each id, and whether it is the steward's.</summary>
    public IReadOnlyList<(string Id, bool Steward)> Lanes =>
        [.. (Body["lanes"] as JsonArray ?? []).OfType<JsonObject>().Select(lane => ((string)lane["id"]!, lane["steward"] is { } steward && (bool)steward))];
}

/// <summary>A registration taken: what is sent, and the workspace the service says took, which is null for a service too old to say.</summary>
public sealed record ConnectDone(ConnectPlan Plan, string? Workspace);

/// <summary>
/// A repository's first registration from its working tree: its manifest and lanes read, a linked worktree
/// refused, the body composed as the registry follows a line (<see cref="LineRegistration"/>) with the workspace, if one is stated,
/// and the root only for a service on this machine. <c>register</c> only re-follows the rows this machine's host already holds; this
/// reaches any service, a remote one included.
/// </summary>
public static class RegistryConnect
{
    /// <summary>What <c>connect</c> would send from <paramref name="root"/> to <paramref name="service"/>, or the refusal that stops it before anything is.</summary>
    /// <param name="workspace">The wiring statement; null says nothing, which preserves the row's workspace.</param>
    /// <exception cref="RegistryRefusal">A linked worktree, a manifest that does not read or declares nothing, or lanes refused whole.</exception>
    public static ConnectPlan Plan(string root, string? workspace, string service)
    {
        // Before anything else: a registration re-pointed at an ephemeral tree works until the tree is removed.
        if (LinkedWorktree.MainOf(root) is { } main)
        {
            throw new RegistryRefusal(
                RegistryRefusal.LinkedWorktree,
                "this is a linked worktree — a session's scratch checkout, not the repository's home. "
                + "Registering it would point the machine's registry at a tree that is removed when its "
                + $"session's work is merged. Run `daoris connect` from the main tree instead:\n  {main}",
                1);
        }

        var manifest = Path.Combine(root, DeclarationNames.Manifest);
        if (!File.Exists(manifest))
        {
            throw new RegistryRefusal(
                RegistryRefusal.ManifestUnreadable, $"no {DeclarationNames.Manifest} in '{root}' — run 'daoris init' first", 2);
        }

        var name = Path.GetFileName(root.TrimEnd('/', '\\'));
        var lanes = Path.Combine(root, DeclarationNames.Lanes);
        var composed = LineRegistration.Compose(
            name, AtomicFile.ReadText(manifest), File.Exists(lanes) ? AtomicFile.ReadText(lanes) : null, root, IsLocal(service));
        if (composed.ManifestProblem is { } problem) throw new RegistryRefusal(RegistryRefusal.ManifestUnreadable, problem, 2);
        if (!composed.Declares)
        {
            // Registering an empty declaration puts the repository on the map as something that answers nothing.
            throw new RegistryRefusal(
                RegistryRefusal.NotDeclared,
                $"{name} has not said what it is. Fill in 'domain' in daoris.json first:\n"
                + "  summary — one line, for someone who has never opened this repository\n"
                + "  owns    — the areas where a change belongs here rather than anywhere else\n"
                + "  accepts — the kinds of work it is worth asking of you\n"
                + "  That declaration is how siblings know whether a quest is yours.",
                1);
        }

        if (composed.LanesProblems.Count > 0)
        {
            throw new RegistryRefusal(
                RegistryRefusal.LanesUnreadable,
                composed.LanesProblems is [var only] && only.EndsWith("is not valid JSON", StringComparison.Ordinal)
                    ? only
                    : $"{DeclarationNames.Lanes} cannot be read:\n{string.Join('\n', composed.LanesProblems.Select(each => $"  {each}"))}",
                1);
        }

        var body = composed.Body!;
        // Absent, never empty: an absent field preserves the existing row, and "" would re-point it to the default.
        if (!string.IsNullOrEmpty(workspace)) body["workspace"] = workspace;
        return new ConnectPlan(name, service, body);
    }

    /// <summary>Whether a service is on this machine: loopback, in every spelling; an address that does not parse withholds the root.</summary>
    public static bool IsLocal(string service) =>
        Uri.TryCreate(service, UriKind.Absolute, out var uri) && (LoopbackHosts.Is(uri.Host) || uri.Host == "::1");
}
