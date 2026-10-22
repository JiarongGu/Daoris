using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's housekeeping, the driver's half (HELPMODE1c2a, D183's note; the Ask Daoris modes design §4.1): each request its
/// connector's tool wrote is judged here at the call, then handed to the cleanup's own judges, never copied. What the loop running
/// the conversation answers its <see cref="SessionRequestWatch.Housekeep"/> with.
/// </summary>
/// <remarks>
/// <para><b>Judged at every call, whatever the agent</b> (the design's traps): the record named is an Ask Daoris conversation's;
/// the operation is chosen in <see cref="ConfiguredAccess"/> now, so a revoke refuses the next call, and was handed at this start
/// (its newest receipt, <see cref="HelpWired"/>), so a grant chosen since takes the next start as its room and allow-list do, which
/// the conversation's next turn now is (ACCESSSWAP1, <see cref="ChatRunner.HelpAccessChosen"/>); the
/// repository is registered in the conversation's workspace, read from its record (<see cref="HelpCircle.OfRecord"/>), and has a
/// checkout here. Targets are a repository and branch names, never a path.</para>
///
/// <para><b>The judges, as the person's doors call them</b>: a look is the cleanup's git half (<see cref="SessionTrees.SweepPartAsync"/>),
/// which asks no plugin, since a tool's call is no person's look (D148 point 2); a clean-up is the cleanup's press with the branches
/// named (<see cref="SessionTrees.CleanAsync"/>), each judged again before it goes; a discard is the branch's own, unforced
/// (<see cref="SessionBranchDiscard.DiscardAsync"/>), which keeps a branch whose tree a running or waiting session holds. Nothing
/// is forced, and no recovery ref is dropped.</para>
/// </remarks>
/// <param name="service">The loop's service once it answers; null before, when every request is refused saying so.</param>
public sealed partial class HelpHousekeeping(string home, Func<ServiceClient?> service)
{
    /// <summary>Judge one request and carry it out where it holds; a refusal is an answer, never thrown.</summary>
    public async Task<HousekeepingAnswer> AnswerAsync(SessionRequest request, CancellationToken ct)
    {
        var operation = request.Operation ?? "";
        if (!ConfiguredAccess.HousekeepingBuilt.Contains(operation))
        {
            return HousekeepingAnswer.Refused(operation, HousekeepingAnswer.Named, $"`{operation}` is no housekeeping operation.");
        }

        if (service() is not { } ledger)
        {
            return HousekeepingAnswer.Refused(operation, HousekeepingAnswer.NoService, "Daoris's service is not answering yet, so the conversation's record could not be read.");
        }

        var record = (await ledger.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, request.Session, StringComparison.Ordinal));
        if (record is not { Repository: HelpRoom.Repository })
        {
            return HousekeepingAnswer.Refused(operation, HousekeepingAnswer.NotHelp,
                $"Housekeeping is Ask Daoris's alone, and session `{request.Session}` is no Ask Daoris conversation of this machine's.");
        }

        if (!ConfiguredAccess.Read(home).Housekeeping.Contains(operation))
        {
            return HousekeepingAnswer.Refused(operation, HousekeepingAnswer.NotGranted,
                $"The person has not granted `{ConfiguredAccess.ToolOf(operation)}` under Ask Daoris's configured access, so it is not yours "
                + "to do: say what you would do, and that they grant it there or do it themselves.");
        }

        if (!(new HelpWired(home).Read(request.Session).FirstOrDefault()?.Housekeeping ?? []).Contains(operation))
        {
            return HousekeepingAnswer.Refused(operation, HousekeepingAnswer.NotHanded,
                $"The person granted `{ConfiguredAccess.ToolOf(operation)}` after this conversation started, and a grant holds from the "
                + "start after it: it is yours once this conversation goes on again.");
        }

        var circle = HelpCircle.OfRecord(home, record);
        var registry = await ledger.RegistryAsync(ct).ConfigureAwait(false);
        if (request.Repository is null && operation == ConfiguredAccess.Inspect)
        {
            var mine = registry.Where(row => !string.IsNullOrWhiteSpace(row.Root) && Directory.Exists(row.Root) && Within(row.Workspace, circle))
                .OrderBy(row => row.Repository, StringComparer.Ordinal).ToList();
            return await InspectAsync(mine, ledger, circle, ct).ConfigureAwait(false);
        }

        if (Place(operation, request.Repository, registry, circle) is { Refusal: { } refused }) return refused;
        var row = Place(operation, request.Repository, registry, circle).Row!;
        if (operation == ConfiguredAccess.Inspect) return await InspectAsync([row], ledger, circle, ct).ConfigureAwait(false);

        var names = request.Branches.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (names.FirstOrDefault(name => !BranchName().IsMatch(name) || name.Contains("..", StringComparison.Ordinal)) is { } odd)
        {
            return HousekeepingAnswer.Refused(operation, HousekeepingAnswer.Named, $"`{odd}` is not a branch's name: a target is named as git names it, never a path.");
        }

        return operation == ConfiguredAccess.CleanUp
            ? await CleanUpAsync(row, names, ledger, ct).ConfigureAwait(false)
            : await DiscardAsync(row, names, ledger, ct).ConfigureAwait(false);
    }

    /// <summary>The registry's row a request names, or the refusal: none named, none registered, another workspace's, no checkout here.</summary>
    private static (RepoView? Row, HousekeepingAnswer? Refusal) Place(string operation, string? repository, IReadOnlyList<RepoView> registry, string? circle)
    {
        if (string.IsNullOrWhiteSpace(repository))
        {
            return (null, HousekeepingAnswer.Refused(operation, HousekeepingAnswer.Named, "It names no repository, and its branches are a repository's."));
        }

        var named = registry.Where(row => string.Equals(row.Repository, repository.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 0)
        {
            return (null, HousekeepingAnswer.Refused(operation, HousekeepingAnswer.Unknown, $"`{repository.Trim()}` is no repository registered here."));
        }

        if (named.FirstOrDefault(row => Within(row.Workspace, circle)) is not { } row)
        {
            return (null, HousekeepingAnswer.Refused(operation, HousekeepingAnswer.Outside,
                $"This conversation is workspace `{circle}`'s, and `{named[0].Repository}` is a repository of another workspace: its housekeeping "
                + "is done from a conversation started from a window on that workspace."));
        }

        return string.IsNullOrWhiteSpace(row.Root) || !Directory.Exists(row.Root)
            ? (null, HousekeepingAnswer.Refused(operation, HousekeepingAnswer.NoCheckout, $"`{row.Repository}` has no checkout on this machine, so it has no branches of Daoris's here."))
            : (row, null);
    }

    /// <summary>Whether a registration's workspace is the conversation's; every one where it has none (<c>default</c>'s room, the whole machine).</summary>
    private static bool Within(string? workspace, string? circle) =>
        circle is not { Length: > 0 }
        || string.Equals(RemoteTarget.Workspace(workspace), RemoteTarget.Workspace(circle), StringComparison.OrdinalIgnoreCase);

    /// <summary>The cleanup's list for each repository, its git half alone: each branch, whether it would go now, and why.</summary>
    private async Task<HousekeepingAnswer> InspectAsync(IReadOnlyList<RepoView> rows, ServiceClient ledger, string? circle, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return new HousekeepingAnswer(ConfiguredAccess.Inspect, true, null,
                circle is null ? "No repository here has a checkout on this machine, so there is nothing of Daoris's to clean up."
                    : $"No repository of workspace `{circle}` has a checkout on this machine, so there is nothing of Daoris's to clean up.");
        }

        var inUse = await InUseAsync(ledger, ct).ConfigureAwait(false);
        var trees = new SessionTrees(home);
        var said = new StringBuilder();
        var cleared = 0;
        foreach (var row in rows)
        {
            var plan = await trees.SweepPartAsync(row.Repository, row.Workspace, row.Root!, inUse, ct).ConfigureAwait(false);
            said.Append($"`{row.Repository}` (workspace `{RemoteTarget.Workspace(row.Workspace)}`):\n");
            if (plan.Sessions.Count == 0 && plan.Landed.Count == 0) said.Append("- no branch of Daoris's\n");
            foreach (var item in plan.Sessions) said.Append($"- {(item.Removable ? "cleared" : "kept")}: {TreesWords.Describe(item)}\n");
            foreach (var item in plan.Landed) said.Append($"- {(item.Removable ? "cleared" : "kept")}: {LandedWords.Describe(item)}\n");
            cleared += plan.Sessions.Count(item => item.Removable) + plan.Landed.Count(item => item.Removable);
        }

        said.Append(cleared == 0
            ? "Nothing is cleared to go now."
            : $"{cleared} cleared to go now: name them to `housekeeping_clean_up` to remove them; each is judged again as it goes.");
        return new HousekeepingAnswer(ConfiguredAccess.Inspect, true, null, said.ToString());
    }

    /// <summary>The cleanup's press over the branches named, each judged again before it goes; a name it does not know is said so.</summary>
    private async Task<HousekeepingAnswer> CleanUpAsync(RepoView row, IReadOnlyList<string> names, ServiceClient ledger, CancellationToken ct)
    {
        if (names.Count == 0)
        {
            return HousekeepingAnswer.Refused(ConfiguredAccess.CleanUp, HousekeepingAnswer.Named, "A clean-up names each branch that goes, as the look listed it.");
        }

        var inUse = await InUseAsync(ledger, ct).ConfigureAwait(false);
        var only = names.Select(name => $"{row.Repository}:{name}").ToHashSet(StringComparer.Ordinal);
        var done = await new SessionTrees(home).CleanAsync([(row.Repository, row.Workspace, row.Root)], inUse, only, ct).ConfigureAwait(false);
        var judged = done.Sessions.Select(result => result.Item.Branch).Concat(done.Landed.Select(result => result.Item.Branch)).ToHashSet(StringComparer.Ordinal);
        var said = new List<string>(TreesWords.Clean(done).Out);
        foreach (var unknown in names.Where(name => !judged.Contains(name)))
        {
            said.Add($"  not one of Daoris's branches in `{row.Repository}`: `{unknown}`, so nothing was done to it");
        }

        return new HousekeepingAnswer(ConfiguredAccess.CleanUp, true, null, string.Join("\n", said));
    }

    /// <summary>The branch's own discard, unforced: one session branch, kept where its judge keeps it.</summary>
    private async Task<HousekeepingAnswer> DiscardAsync(RepoView row, IReadOnlyList<string> names, ServiceClient ledger, CancellationToken ct)
    {
        if (names.Count != 1 || !names[0].StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal))
        {
            return HousekeepingAnswer.Refused(ConfiguredAccess.DiscardBranch, HousekeepingAnswer.Named,
                $"A discard names one session branch, `{TreeFollows.SessionPrefix}…`; any other branch is the person's.");
        }

        var removal = await new SessionBranchDiscard(new SessionTrees(home)).DiscardAsync(ledger, row.Repository, names[0], force: false, ct).ConfigureAwait(false);
        return removal.Removed
            ? new HousekeepingAnswer(ConfiguredAccess.DiscardBranch, true, null, Capital(removal.Message))
            : new HousekeepingAnswer(ConfiguredAccess.DiscardBranch, false, HousekeepingAnswer.Kept, $"Kept: {removal.Message}");
    }

    /// <summary>
    /// The trees sessions still running or waiting name, as the service's ledger says now, a session waiting on its own helper's among them
    /// (HELPERWAIT1): each is kept, whatever it holds.
    /// </summary>
    private async Task<HashSet<string>> InUseAsync(ServiceClient ledger, CancellationToken ct)
    {
        var held = (await ledger.ActiveSessionsAsync(ct).ConfigureAwait(false))
            .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        held.UnionWith(await BranchTidying.WaitingOnHelpersAsync(ledger, ct).ConfigureAwait(false));
        return held;
    }

    private static string Capital(string sentence) => sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];

    // A branch as git names it: no space, no backslash, no colon, never beginning with a dash (an option to git) or a slash.
    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9._/-]*$")]
    private static partial Regex BranchName();
}
