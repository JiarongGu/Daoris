using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1c (D181 point 3, D125's PARALLEL1c note): choosing an account no longer waits on another account's probe. A walk
/// asks its questions outside its gate, each under the account's own lock, and holds the gate only to read again what the
/// questions left and to count the account it chose; a start whose account stays locked past its patience counts nothing and
/// runs elsewhere, since a question in flight on it may be spending its single-use refresh token.
/// </summary>
/// <remarks>
/// Nothing here starts a process: the agent is present by a file look on a command this test writes, and its status and usage
/// questions are in-process stand-ins (<see cref="HarnessRoster.Asking"/>, <see cref="HarnessRoster.AskingUsage"/>). A question
/// that never answers is held on a task this test lets go as it ends.
/// </remarks>
public sealed class AccountWalkGateTests : IDisposable
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>How long a start may take beside a question that never answers: one held behind it never ends at all.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    /// <summary>A start's patience for its account's lock here, where the roster's own is thirty seconds.</summary>
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(300);

    private static readonly UsageQuestion Question = new("fake", ["fake-agent", "app-server"], CodexUsage.Question.Recorded);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-walk-gate-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly DateTimeOffset _now = Seen;

    // What a question that never answers waits on, let go as the test ends so nothing is left waiting.
    private readonly TaskCompletionSource _never = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Set once the question that never answers has begun, so the second start is made while it is in flight.
    private readonly TaskCompletionSource _begun = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AccountWalkGateTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        _never.TrySetResult();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    /// <summary>A file that is there, so the agent is present by a look and nothing runs.</summary>
    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake" };

    /// <summary>
    /// The fake agent, its status question answered by <paramref name="status"/> and, where its server is asked its windows
    /// (<paramref name="usage"/>), those by <paramref name="windows"/>; a walk's patience for an account's lock is
    /// <paramref name="patience"/>. With no <paramref name="status"/>, the agent's own question is asked, under the account's
    /// lock, where it declares one (<paramref name="asks"/>): a lock not had asks nothing, so nothing runs.
    /// </summary>
    private HarnessRoster Roster(
        Func<string, Task<LoginState>>? status = null, bool usage = false,
        Func<string, Task<IReadOnlyList<WindowReading>?>>? windows = null, TimeSpan? patience = null, bool asks = false) =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
                LoginCheck: asks ? new LoginQuestion(["auth", "status"], @"""loggedIn""\s*:\s*true", @"""loggedIn""\s*:\s*false") : null,
                Usage: usage ? Question : null)),
        }), Settings)
        {
            Clock = () => _now,
            Asking = status is null ? null : (_, account, _) => status(account),
            AskingUsage = (_, account, _) => windows is null ? Task.FromResult<IReadOnlyList<WindowReading>?>(null) : windows(account),
            LockPatience = patience ?? Bound,
        };

    private void Accounts(params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
    }

    private void Wire(Func<HarnessSettings, HarnessSettings> edit) => edit(new HarnessSettings()).Save(Settings);

    /// <summary>The question that never answers: it says it began, then waits for the test's end.</summary>
    private async Task Never()
    {
        _begun.TrySetResult();
        await _never.Task;
    }

    private IReadOnlyList<WindowReading> Readings(double used) =>
        [new WindowReading("session", used, _now.AddHours(3)), new WindowReading("weekly", used, _now.AddDays(4))];

    [Fact]
    public async Task A_status_question_that_never_answers_on_one_account_holds_no_start_on_another()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Use: "order")));
        // Read signed out over an hour ago, so the next start that walks to it asks it again (TOOL6g's backstop).
        AccountReads.Keep(_home, "fake", "account-1", LoginState.Out, Seen - HarnessRoster.SignedOutAskedAgain - TimeSpan.FromMinutes(1));
        var roster = Roster(status: async account =>
        {
            if (account == "account-1") await Never();
            return LoginState.In;
        });

        var first = roster.SelectAsync("fake", Config, null, chosen: "account-1");
        await _begun.Task.WaitAsync(Bound);

        // A pick of the other account, and the list's walk, which passes the account being asked as its last reading says.
        var picked = await roster.SelectAsync("fake", Config, null, chosen: "account-2").WaitAsync(Bound);
        var walked = await roster.SelectAsync("fake", Config, null, null).WaitAsync(Bound);

        Assert.Equal(("account-2", true), (picked.Profile, picked.Allowed));
        Assert.Equal(("account-2", true), (walked.Profile, walked.Allowed));
        Assert.False(first.IsCompleted);

        // Its answer comes: the first start runs on the account it asked about, and is counted once.
        _never.SetResult();
        var asked = await first.WaitAsync(Bound);
        Assert.Equal(("account-1", true), (asked.Profile, asked.Allowed));
        Assert.True(roster.Busy("fake", "account-1"));
    }

    [Fact]
    public async Task A_usage_read_that_never_answers_on_one_account_holds_no_start_on_another()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        // Patience enough for the second account's read to let its lock go, short of the bound.
        var roster = Roster(usage: true, patience: TimeSpan.FromSeconds(2), windows: async account =>
        {
            if (account == "account-1") await Never();
            return Readings(0.10);
        });

        // The list's walk reads each account's windows first (CODEXUSE1), and the first account's read never answers.
        var first = roster.SelectAsync("fake", Config, null, null);
        await _begun.Task.WaitAsync(Bound);

        var picked = await roster.SelectAsync("fake", Config, null, chosen: "account-2").WaitAsync(Bound);
        // Another walk over the list asks neither again; the account whose read holds its lock is passed for this walk.
        var walked = await roster.SelectAsync("fake", Config, null, null).WaitAsync(Bound);

        Assert.Equal(("account-2", true), (picked.Profile, picked.Allowed));
        Assert.Equal(("account-2", true), (walked.Profile, walked.Allowed));
        Assert.False(first.IsCompleted);

        _never.SetResult();
        var read = await first.WaitAsync(Bound);
        Assert.True(read.Allowed, read.Refusal);
    }

    /// <summary>
    /// 🔴 The count's lock (TOOL6g): a start waits out a question in flight on the account it chose, and where that lock stays
    /// held past its patience, the start is not counted there and runs on the next account, never beside the question.
    /// </summary>
    [Fact]
    public async Task A_start_whose_account_stays_locked_past_its_patience_counts_nothing_there_and_runs_on_the_next()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Use: "order")));
        var roster = Roster(patience: Short);

        await using (await ProbeLock.TakeAsync(ProbeLock.PathOf(_home, "fake", "account-1")))
        {
            var selection = await roster.SelectAsync("fake", Config, null, null).WaitAsync(Bound);

            Assert.Equal(("account-2", true), (selection.Profile, selection.Allowed));
            // LOCKPASSED1: the start's line says the account was passed for its lock, never that the goal ranked another ahead of it.
            Assert.Equal(WalkStep.Locked, selection.Rotated!.Step);
            Assert.Equal(OpeningWhy.Locked, Assert.Single(selection.Steps!).Why);
            Assert.Contains("being asked about by another question", selection.Rotated.Why);
        }

        Assert.False(roster.Busy("fake", "account-1"));
        Assert.True(roster.Busy("fake", "account-2"));
    }

    [Fact]
    public async Task A_pick_whose_account_stays_locked_past_its_patience_is_held_and_counts_nothing()
    {
        Accounts("account-1", "account-2");
        var roster = Roster(patience: Short);

        HarnessSelection selection;
        await using (await ProbeLock.TakeAsync(ProbeLock.PathOf(_home, "fake", "account-1")))
        {
            selection = await roster.SelectAsync("fake", Config, null, chosen: "account-1").WaitAsync(Bound);
        }

        Assert.False(selection.Allowed);
        Assert.Contains("`account-1`", selection.Refusal);
        Assert.Null(selection.Cooling);
        // LOCKPASSED1: the hold is said by code, the accounts by id, so the page words it.
        var part = Assert.Single(selection.RefusalNote!.Parts);
        Assert.Equal(NoteCodes.HeldLocked.Code, part.Code);
        Assert.Equal(selection.Refusal, selection.RefusalNote.Note);
        var accounts = Assert.IsAssignableFrom<IReadOnlyList<string>>(Assert.Single(part.Values!, value => value.Key == "accounts").Value);
        Assert.Equal(["account-1"], accounts);
        Assert.False(roster.Busy("fake", "account-1"));

        // Let go, the next start runs on it.
        var next = await roster.SelectAsync("fake", Config, null, chosen: "account-1").WaitAsync(Bound);
        Assert.Equal(("account-1", true), (next.Profile, next.Allowed));
    }

    /// <summary>
    /// The walk's own status question on an account read signed out (TOOL6g) could not have its lock: the account is unknown for
    /// this walk, passed and never counted, and its lock is not waited for again to count it.
    /// </summary>
    [Fact]
    public async Task A_status_question_that_cannot_have_its_lock_leaves_the_account_unknown_for_the_walk()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Use: "order")));
        AccountReads.Keep(_home, "fake", "account-1", LoginState.Out, Seen - HarnessRoster.SignedOutAskedAgain - TimeSpan.FromMinutes(1));
        var patience = TimeSpan.FromSeconds(2);
        var roster = Roster(patience: patience, asks: true);

        HarnessSelection selection;
        var watch = Stopwatch.StartNew();
        await using (await ProbeLock.TakeAsync(ProbeLock.PathOf(_home, "fake", "account-1")))
        {
            selection = await roster.SelectAsync("fake", Config, null, null).WaitAsync(Bound);
        }

        Assert.Equal(("account-2", true), (selection.Profile, selection.Allowed));
        // One wait for the lock, the question's: the account is not then chosen and waited for again to be counted.
        Assert.True(watch.Elapsed < patience * 1.75, $"the walk waited {watch.Elapsed} for a lock it had already not had");
        Assert.False(roster.Busy("fake", "account-1"));
        // Nothing was asked, so nothing was kept: its last reading stands.
        Assert.Equal(LoginState.Out, AccountReads.Of(_home, "fake").Accounts["account-1"].Login);
    }

    [Fact]
    public async Task A_question_in_flight_on_the_chosen_account_is_waited_out_within_its_patience_and_the_start_counts()
    {
        Accounts("account-1");
        var roster = Roster(patience: Bound);
        var held = await ProbeLock.TakeAsync(ProbeLock.PathOf(_home, "fake", "account-1"));
        var watch = Stopwatch.StartNew();
        var letGo = Task.Run(async () =>
        {
            await Task.Delay(Short);
            await held!.DisposeAsync();
        });

        var selection = await roster.SelectAsync("fake", Config, null, chosen: "account-1").WaitAsync(Bound);
        await letGo;

        Assert.Equal(("account-1", true), (selection.Profile, selection.Allowed));
        Assert.True(watch.Elapsed >= Short - TimeSpan.FromMilliseconds(50), "the start did not wait out the question in flight");
        Assert.True(roster.Busy("fake", "account-1"));
    }

    /// <summary>
    /// Starts made together each see the others' counts: the goal spreads them over the list, none counted twice and none
    /// missed (D130 §7: none runs more than ⌈K ÷ N⌉ of K while every account is ready).
    /// </summary>
    [Fact]
    public async Task Starts_made_at_once_spread_over_the_list_each_counted_once()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        var roster = Roster();
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var made = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await go.Task;
            return await roster.SelectAsync("fake", Config, null, null);
        })).ToList();
        go.SetResult();
        var starts = await Task.WhenAll(made).WaitAsync(Bound);

        Assert.All(starts, start => Assert.True(start.Allowed, start.Refusal));
        Assert.Equal(
            [("account-1", 4), ("account-2", 4)],
            starts.GroupBy(start => start.Profile!).Select(group => (group.Key, group.Count())).OrderBy(each => each.Key, StringComparer.Ordinal));

        // The ninth sees all eight: still an even spread, wherever it runs.
        var ninth = await roster.SelectAsync("fake", Config, null, null);
        Assert.True(ninth.Allowed, ninth.Refusal);
    }
}
