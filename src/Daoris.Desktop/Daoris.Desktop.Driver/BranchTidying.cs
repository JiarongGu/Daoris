using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The look's own tidy of empty session branches (AUTOTIDY1, D88's note): a session branch that holds nothing beyond the line,
/// with nothing in its tree, goes without a press, and the look says so in its report and the machine log. Held by the watch
/// across its looks, since each look's driver is built fresh: what it already said, and when it last looked.
/// </summary>
/// <remarks>
/// <para><b>The press's path, not a second one.</b> Each repository is tidied by <see cref="SessionTrees.TidyEmptyAsync"/>,
/// which runs the clean-up's own press over the few branches the line holds, taking only the empty kind
/// (<see cref="SessionTrees.GoesByItself"/>). Every guard is the press's: judged again right before it goes, deleted only
/// while it is the commit judged, never forced.</para>
///
/// <para><b>Said once.</b> A branch kept because git could not read a guard, or because git refused its removal, is said in
/// the report and written to the log the first look it is met, and not again while it lasts: a branch that stays for weeks is
/// one line. What holds a repository (a start in one of its trees, a service that did not answer) is said the same way.</para>
///
/// <para><b>Once each pace</b>, from the first look: a pass reads every checkout's branches with git, which the loop's own pace
/// would repeat every few seconds for nothing.</para>
/// </remarks>
public sealed class BranchTidying(string home)
{
    /// <summary>How often a pass looks, at most.</summary>
    public static readonly TimeSpan DefaultPace = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();

    // What was said and is still so, by repository, branch and code: said again only after a pass where it was not.
    private HashSet<string> _said = new(StringComparer.OrdinalIgnoreCase);

    private DateTimeOffset? _began;

    /// <summary>How often a pass looks, at most; a test's is zero.</summary>
    public TimeSpan Pace { get; init; } = DefaultPace;

    /// <summary>What time it is; the system's by default.</summary>
    internal Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>Whether a pass is due: at the first look, and then once each <see cref="Pace"/>.</summary>
    public bool Due
    {
        get
        {
            lock (_gate) return _began is not { } began || Clock() - began >= Pace;
        }
    }

    /// <summary>A pass begins now: the next is due a pace from here.</summary>
    public void Began()
    {
        lock (_gate) _began = Clock();
    }

    /// <summary>
    /// One pass over every repository with a checkout here: what it removed and what it says once, as the report's lines, and
    /// the same written to <paramref name="log"/>. Never throws for a service or git that does not answer: it says so, once.
    /// </summary>
    public async Task<IReadOnlyList<string>> LookAsync(ServiceClient service, MachineLog? log, CancellationToken ct)
    {
        Began();
        IReadOnlyList<RepoView> registry;
        try
        {
            registry = await service.RegistryAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException)
        {
            return Said([new TidyPass([], TidyKept.Sessions, error.Message)], log, whole: false);
        }

        var trees = new SessionTrees(home);
        var passes = new List<TidyPass>();
        foreach (var row in registry.Where(row => !string.IsNullOrWhiteSpace(row.Root) && Directory.Exists(row.Root)))
        {
            try
            {
                passes.Add(await trees.TidyEmptyAsync(row.Root!, row.Repository, row.Workspace, InUseAsync, ct).ConfigureAwait(false));
            }
            catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
            {
                passes.Add(new TidyPass([], TidyKept.Unread, error.Message)
                {
                    Repository = row.Repository, Workspace = RemoteTarget.Workspace(row.Workspace),
                });
            }
        }

        return Said(passes, log);

        async Task<IReadOnlySet<string>> InUseAsync(CancellationToken token) =>
            (await service.ActiveSessionsAsync(token).ConfigureAwait(false))
                .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// What a pass says: each removal, and each branch or repository kept for a reason the look meets for the first time, in
    /// the report's words and the log's codes.
    /// </summary>
    /// <param name="whole">Whether the pass looked at every repository, so what it did not meet again is forgotten.</param>
    internal IReadOnlyList<string> Said(IReadOnlyList<TidyPass> passes, MachineLog? log, bool whole = true)
    {
        var lines = new List<string>();
        var met = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            foreach (var pass in passes)
            {
                if (pass.Held is { } held)
                {
                    if (!First(pass.Repository, "", held)) continue;
                    lines.Add($"tidy  {Held(pass)}");
                    log?.Warn("branch.kept", ("repository", Name(pass.Repository)), ("workspace", Name(pass.Workspace)), ("branch", null), ("why", held));
                    continue;
                }

                foreach (var result in pass.Results)
                {
                    var item = result.Item;
                    if (result.Removed)
                    {
                        lines.Add($"tidy  {pass.Repository}: removed `{item.Branch}`" + (item.Tree is null ? "" : " with its tree")
                            + $", which held nothing beyond `{item.Where}`.");
                        log?.Info("branch.tidied",
                            ("repository", pass.Repository), ("workspace", pass.Workspace), ("branch", item.Branch), ("tree", item.Tree is not null));
                        continue;
                    }

                    // Kept for what it holds (dirty, in use, a build's output) is the press's, and says nothing; kept because git
                    // could not read a guard, or refused the press's own removal, is said the first look it is met.
                    var (why, clause) = item.Unread
                        ? (TidyKept.Unread, item.Detail ?? "git could not say")
                        : result.Message == "kept" ? (null, null) : (TidyKept.Refused, result.Message);
                    if (why is null || !First(pass.Repository, item.Branch, why)) continue;
                    lines.Add($"tidy  {pass.Repository}: `{item.Branch}` stays, since {Clause(clause!)}.");
                    log?.Warn("branch.kept", ("repository", pass.Repository), ("workspace", pass.Workspace), ("branch", item.Branch), ("why", why));
                }
            }

            // What this pass did not meet again has cleared: met again later, it is said again.
            if (whole) _said = met;
            else _said.UnionWith(met);
        }

        return lines;

        bool First(string repository, string branch, string why)
        {
            var key = $"{repository}\n{branch}\n{why}";
            met.Add(key);
            return !_said.Contains(key);
        }
    }

    /// <summary>What held a whole repository this look, in a sentence after its name.</summary>
    private static string Held(TidyPass pass) => pass.Held switch
    {
        TidyKept.Starting => $"{pass.Repository}: {TreeLock.Starting(pass.Repository)}.",
        TidyKept.Sessions when pass.Repository.Length == 0 =>
            $"the session branches were not looked at, since the service did not answer for its registry: {Clause(pass.Detail ?? "")}.",
        TidyKept.Sessions => $"{pass.Repository}: its empty session branches stay this look, since the service did not say which "
            + $"sessions run: {Clause(pass.Detail ?? "")}.",
        _ => $"{pass.Repository}: its empty session branches stay this look, since git could not list them: {Clause(pass.Detail ?? "")}.",
    };

    /// <summary>The press's sentence as a clause: its own "and is kept" and its full stop are the line's to say.</summary>
    private static string Clause(string sentence) =>
        sentence.Replace(", and is kept", "", StringComparison.Ordinal).Trim().TrimEnd('.');

    private static string? Name(string text) => text.Length == 0 ? null : text;
}
