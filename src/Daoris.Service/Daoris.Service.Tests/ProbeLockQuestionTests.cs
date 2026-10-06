using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The question D24's ORIENT1c note measured, asked of a miniature of this repository's records through the
/// deployment a workspace's own server is (one checkout, its <c>docs/</c> a section each, SQLite, words only).
/// The answer is the TOOL6g note under D125 and the FIX-LOG entry it points to; the competition is what beat
/// them there: a design section titled with <i>lock</i> and <i>decided</i>, and a decision about another lock.
/// </summary>
/// <remarks>
/// The texts are the real entries cut short. Forty decisions about nothing in particular stand for the rest of
/// the record, so <i>what</i> and <i>the</i> are as common here as they are there, and <i>decided</i> and
/// <i>lock</i> as rare.
/// </remarks>
public sealed class ProbeLockQuestionTests : IDisposable
{
    private const string Question = "What decided the probe lock";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-probe-lock-question-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_root, "family", "served", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private const string FixLog = """
        # Fix log

        ## Every account signed out about ten seconds after the application started (2026-10-04)

        **Symptom.** On the install every account read signed out about ten seconds after the application started.

        **Root cause.** Nothing kept two status questions off one account: the roster cached each harness's probe in a
        plain dictionary with nothing between a miss and the probe, so the page's roster and a start's walk ran separate
        probes over the same directories. Asked its status with an access token expired, the agent refreshes it; two at
        once spend one single-use refresh token twice, and the loser signs the account out.

        **Fix.** No probe per look. One status question per configuration home at a time (`ProbeLock`): a gate per home
        in the process, and a lock file under `harnesses/.probing/` taken by creating it new, which the CLI takes too
        (`probelock.ts`, a twin). A probe in flight is shared by every caller.

        **Verification.** `ProbeRaceTests`: four of its first five failed before the fix, all pass after.
        """;

    private const string D125 = """
        ## D125 — An account's limit is read from the agent's own words, cools that account until the reset (2026-10-02)

        **Decision (TOOL4, 2026-10-02).** A limit is read from what the agent said when it refused, never guessed from a
        count. The account it names cools until the reset the agent gave, and the rotation passes it meanwhile.

        **Why.** A limit nobody read is a refusal nobody can wait out.

        **Built 2026-10-02 (TOOL4a): the limit table and its reader** (point 1). Each agent's sentences for a limit are a
        table, read by the driver and the CLI alike, and a sentence the table does not hold is a refusal.

        **Built 2026-10-04 (TOOL6g): a signed-out account is said, and asked about once per sign-out** (points 4, 6 and
        7). On the install every account read signed out. Held by `AccountRotationTests`, `ProbeLockTests` and the CLI's
        `probelock.test.ts`. What building it settled:
        - **How a signed-out account is known: the agent's own status command, cached, asked once per sign-out.** The
          roster's probe says it; a start held on such an account believes that word and asks nothing.
        - **One status question per account at a time** (`ProbeLock`, twinned by the CLI's `probelock.ts`), a probe in
          flight shared by its callers; FIX-LOG has the race this keeps out.

        **Built 2026-10-04 (ROSTER1): the roster asks no account at start, and reads one account on a press.** The first
        roster read after a start still probed every named account, one at a time.
        """;

    private const string SyncDesign = """
        # Sync design

        ## 4. The lock, online and offline — decided: claim by push

        The take itself claims by push. A take commits on the machine where it was made; when the quest's workspace has a
        remote, the take is pushed and awaited before the answer, and the lock is held where the quest lives.

        ## 6. When it runs — decided: automatic, and on demand

        Sync runs on a timer and when a person asks for it.
        """;

    private const string D13 = """
        ## D13 — Drift is measured against the lock, not against the current canon (2026-08-04)

        **Decision.** For a file already in the lock, sync compares what is on disk to the hash the lock recorded: what
        was last written, not what the canon says now.
        """;

    private void Record()
    {
        Write("daoris.json", """{ "source": "s", "documents": { "decisions": "docs/decisions", "fixes": "docs/FIX-LOG.md" } }""");
        Write("docs/FIX-LOG.md", FixLog);
        Write("docs/decisions/D125.md", D125);
        Write("docs/decisions/D13.md", D13);
        Write("docs/2026-09-23-sync-design.md", SyncDesign);
        for (var n = 200; n < 240; n++)
        {
            Write($"docs/decisions/D{n}.md",
                $"## D{n} — The record of part {n} (2026-09-01)\n\n**Decision.** What was chosen for part {n} is "
                + $"written here, with the reasons the record gives for it and the options it weighed.\n");
        }
    }

    private async Task<IReadOnlyList<KnowledgeHit>> AskAsync()
    {
        Record();
        var checkout = Path.Combine(_root, "family", "served");
        var options = new ServiceOptions(
            Path.Combine(_root, "family"), Path.Combine(_root, "knowledge.db"), Repository: checkout, Documents: "docs");
        await using var composed = await ServiceFactory.CreateAsync(options);
        return await composed.Service.SearchAsync(new KnowledgeQuery(Question));
    }

    /// <summary>
    /// ORIENT1f's proof: the fix or the decision that names <c>ProbeLock</c> answers first, where the design
    /// section titled with two of the question's words did.
    /// </summary>
    [Fact]
    public async Task The_probe_lock_question_is_answered_first_by_the_fix_or_the_decision_that_names_it()
    {
        var first = (await AskAsync())[0].Entry;

        Assert.True(
            first.RelativePath is "docs/FIX-LOG.md" or "docs/decisions/D125.md",
            $"answered first by {first.RelativePath}#{first.Anchor}");
    }
}
