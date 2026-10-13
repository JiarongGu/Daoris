using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <c>daoris-driver quest check</c> was asked: the quest, and the commit to read, or null for the done's own.</summary>
public sealed record QuestCheckAsk(string Quest, string? Commit);

/// <summary>What the terminal's check reads through: the service, and the git Daoris runs (a test stands it in).</summary>
public sealed class QuestCheckWorld(ServiceClient service)
{
    public ServiceClient Service => service;

    /// <summary>How git is read: the review's seam (REVIEW3).</summary>
    internal WorkingTree.GitRead Git { get; init; } = WorkingTree.ReadGitAsync;
}

/// <summary>
/// The terminal's door to a done's evidence (EVID1b, D144 §3, §5, D50): <c>daoris-driver quest check &lt;id&gt; [--commit
/// &lt;sha&gt;]</c> reads it again, posts what it read as the terminal's, and prints the verdict. The quest page's
/// <i>Check again</i> is the other door, EVID1c's.
/// </summary>
/// <remarks>
/// <para><b>Which commit.</b> The one named, which must be the done's commit or come after it on the same history, so work that
/// arrived later lifts the hold. Named or not, the done's commit is the one a driven end on this machine read (a session's
/// end or the sweep), from its record's evidence bundle, else from the quest's last verdict where a driven end wrote it. With
/// no driven end on record, the commit must be named: Daoris never guesses it from what stands now (D143 §3).</para>
///
/// <para><b>Where.</b> The driven end's own tree while it is still here and still the top of its own repository, else the
/// repository's registered checkout, which holds the same objects. Its base is the record's, for whether the work changed
/// each path; with no record, that is not read.</para>
///
/// <para><b>Exit codes</b> (D144 §5): 0 when every item was found and the verdict kept, 1 when anything is missing or unread or
/// the check was refused, and 2 when a store did not answer.</para>
/// </remarks>
public static class QuestCheckCommand
{
    public const string Usage =
        "usage: daoris-driver quest check <id> [--commit <sha>]\n"
        + "       read a done's evidence again: at the commit a driven end here read, or the one named, which must come after\n"
        + "       it on the same history. With no driven end on record, name the commit.";

    /// <summary>Whether a <c>quest</c> line asks for the check.</summary>
    public static bool Asks(string[] args) => args is ["check", ..];

    /// <summary>The line read, or null with why not.</summary>
    public static QuestCheckAsk? Read(string[] args, out string? problem)
    {
        problem = null;
        string? quest = null;
        string? commit = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--commit")
            {
                if (i + 1 >= args.Length)
                {
                    problem = "`--commit` takes the commit to read.";
                    return null;
                }

                commit = args[++i];
                if (!WorkingTree.IsCommitId(commit))
                {
                    problem = $"`{commit}` is not a commit id: name one by its hex id, 7 to 64 characters.";
                    return null;
                }

                commit = commit.ToLowerInvariant();
                continue;
            }

            if (args[i].StartsWith('-'))
            {
                problem = $"`{args[i]}` is not a flag `quest check` takes.";
                return null;
            }

            if (quest is not null)
            {
                problem = "`quest check` reads one quest at a time.";
                return null;
            }

            quest = args[i].TrimStart('#');
        }

        if (quest is not { Length: > 0 })
        {
            problem = "`quest check` names the quest whose evidence to read.";
            return null;
        }

        return new QuestCheckAsk(quest, commit);
    }

    public static async Task<int> RunAsync(QuestCheckAsk ask, QuestCheckWorld world, TextWriter output, CancellationToken ct = default)
    {
        var service = world.Service;
        QuestView? quest;
        IReadOnlyList<RepoView> registry;
        IReadOnlyList<SessionRecord> records;
        try
        {
            quest = await service.FindQuestAsync(ask.Quest, ct).ConfigureAwait(false);
            registry = quest is null ? [] : await service.RegistryAsync(ct).ConfigureAwait(false);
            records = quest is null ? [] : await service.SessionRecordsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            output.WriteLine($"daoris-driver: the service did not answer, so nothing was read: {error.Message}");
            return 2;
        }

        if (quest is null)
        {
            output.WriteLine($"daoris-driver: there is no quest `#{ask.Quest}` here. Ids come from `quest_list`.");
            return 1;
        }

        if (!quest.AwaitsEvidence) return NothingWaits(quest, output);

        if (registry.FirstOrDefault(repo => string.Equals(repo.Repository, quest.To, StringComparison.OrdinalIgnoreCase)) is not { Root: { Length: > 0 } root })
        {
            output.WriteLine($"daoris-driver: there is no checkout of `{quest.To}` registered on this machine, so there is nowhere "
                + $"to read quest `#{quest.Id}`'s commit.");
            return 1;
        }

        // The driven end on record here: the newest of this machine's records on the quest whose bundle names the done's commit.
        var own = records
            .Where(record => !record.Teammate && string.Equals(record.Quest, quest.Id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(record => record.Updated)
            .ToList();
        var ended = own.FirstOrDefault(record => EvidenceCheck.DonesCommit(record.Evidence) is not null);
        var dones = EvidenceCheck.DonesCommit(ended?.Evidence)
            ?? (quest.Evidence is { How: EvidenceCodes.SessionEnd or EvidenceCodes.Sweep } read ? read.Commit : null);
        var record = ended ?? own.FirstOrDefault();

        var commit = ask.Commit ?? dones;
        if (commit is null)
        {
            output.WriteLine($"daoris-driver: no driven end on this machine read quest `#{quest.Id}`'s evidence, so name the commit "
                + $"to read: `daoris-driver quest check {quest.Id} --commit <sha>`. Daoris never guesses it from what stands now.");
            return 1;
        }

        var tree = record?.Tree is { Length: > 0 } held && await WorkingTree.IsTopLevelAsync(held, world.Git, ct).ConfigureAwait(false)
            ? held
            : root;

        if (ask.Commit is { } named && dones is not null)
        {
            switch (await EvidenceReader.PlaceAsync(tree, dones, named, world.Git, ct).ConfigureAwait(false))
            {
                case EvidencePlace.Elsewhere:
                    output.WriteLine($"daoris-driver: `{named}` does not come after the done's commit `{Short(dones)}` on its history: a "
                        + "check reads the done's commit or one after it, so the work it reads is the work that was done.");
                    return 1;
                case EvidencePlace.Unknown:
                    output.WriteLine($"daoris-driver: git here cannot place `{named}` against the done's commit `{Short(dones)}`: both "
                        + $"must be commits this checkout of `{quest.To}` holds. Fetch them, then check again.");
                    return 1;
            }
        }

        var outcome = await EvidenceCheck.RunAsync(
                service, quest, new EvidenceAt(tree, EvidenceCodes.Terminal) { Commit = commit, Base = record?.BaseCommit }, world.Git, ct)
            .ConfigureAwait(false);

        output.WriteLine(outcome.Bundle);
        if (outcome.Verdict is not null) output.WriteLine(outcome.Posted.Message);
        return outcome.Posted.Outcome == EvidencePosted.Unanswered ? 2 : outcome.Released ? 0 : 1;
    }

    /// <summary>A quest that waits on no evidence: found already (0), or why nothing waits (1).</summary>
    private static int NothingWaits(QuestView quest, TextWriter output)
    {
        if (quest.Evidence is { Found: true } found)
        {
            output.WriteLine($"Quest `#{quest.Id}`'s evidence was found at `{Short(found.Commit)}` ({found.How}):");
            foreach (var item in found.Items) output.WriteLine(EvidenceCheck.Line(item));
            return 0;
        }

        output.WriteLine(quest.Status != "Done"
            ? $"daoris-driver: quest `#{quest.Id}` is {quest.Status}: its evidence is read on a done, once the session that closed it ends."
            : $"daoris-driver: quest `#{quest.Id}`'s done waits on no evidence: no requirement it met names any, or the person "
              + "accepted it as it stood.");
        return 1;
    }

    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;
}
