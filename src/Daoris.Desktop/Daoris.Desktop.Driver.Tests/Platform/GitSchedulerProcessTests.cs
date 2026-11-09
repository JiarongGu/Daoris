using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git scheduler over real git: hundreds of gits held to the bound; a person's read placed while a push running its hook, a fetch
/// waiting on its grant and a background diff hold every other place; two hooks in two partial clones, and four diffs' textconv
/// helpers, asking back in and each lent a place; a <c>worktree remove</c> while an unendable git is queued in its tree, its own git
/// passing the ask it holds; and a write's order kept, a read queued ahead of it reading the state before it and a read its flow asks
/// after it reading what it wrote.
/// </summary>
/// <remarks>
/// Places are the scheduler's count (<see cref="GitScheduler.Running"/>), never the job's processes, which hold hooks, helpers and
/// transports too. What waits outside Daoris is a shell script git itself runs (a hook, the ssh command, a textconv helper), waiting on
/// a file this class writes; a helper that asks back in writes its process id where the instance pipe would read its caller, and is
/// answered through <see cref="GitCallers"/> with its real ancestry, as <c>PipeGitCaller</c> answers it.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitSchedulerProcessTests(ITestOutputHelper output) : TreeSyncFixture
{
    private static readonly TimeSpan Within = Poll.Patience;

    private string Waits => Directory.CreateDirectory(Path.Combine(Scratch, "waits")).FullName;

    private string Release => Path.Combine(Waits, "release");

    private static string Sh(string path) => path.Replace('\\', '/');

    private static Task<T> Person<T>(Func<Task<T>> ask)
    {
        using (GitScheduler.AsPerson()) return ask();
    }

    private static Task<(int Code, string Stdout, string Stderr)> Git(string root, params string[] arguments) =>
        WorkingTree.GitAsync(root, arguments, CancellationToken.None);

    /// <summary>
    /// A script git runs that says it started, then waits for <paramref name="release"/> (<see cref="Release"/> unless named), then ends
    /// with <paramref name="tail"/>; where <paramref name="when"/> is given, it waits only when its first argument is that.
    /// </summary>
    private string Waiter(string name, string tail, string? release = null, string? when = null)
    {
        var script = Path.Combine(Waits, name + ".sh");
        var wait = $"echo started > \"{Sh(Waits)}/started-{name}-$$\"\nwhile [ ! -f \"{Sh(release ?? Release)}\" ]; do sleep 0.1; done\n";
        File.WriteAllText(script, "#!/bin/sh\n" + (when is null ? wait : $"if [ \"$1\" = {when} ]; then\n{wait}fi\n") + tail + "\n");
        return script;
    }

    /// <summary>A script git runs that asks back in, as a hook or a helper asks the window, and waits for its answer before <paramref name="tail"/>.</summary>
    private string Asker(string name, string tail)
    {
        var script = Path.Combine(Waits, name + ".sh");
        var asks = Sh(Waits);
        File.WriteAllText(script,
            "#!/bin/sh\nme=$$\n"
            + $"printf '%s\\n%s\\n' \"$(cat /proc/$me/winpid)\" \"$(pwd -W)\" > \"{asks}/ask-$me.part\" && mv \"{asks}/ask-$me.part\" \"{asks}/ask-$me\"\n"
            + $"while [ ! -f \"{asks}/answer-$me\" ]; do sleep 0.1; done\n{tail}\n");
        return script;
    }

    private int Started(string name) => Directory.GetFiles(Waits, $"started-{name}-*").Length;

    private static async Task HookAsync(string root, string name, string script)
    {
        var hook = Path.Combine(root, ".git", "hooks", name);
        Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        await File.WriteAllTextAsync(hook, (await File.ReadAllTextAsync(script)).Replace("\r\n", "\n"));
    }

    /// <summary>A repository whose <c>slow.txt</c> changed in its last commit and is shown through the <c>slow</c> textconv driver.</summary>
    private async Task<string> DiffedAsync(string name)
    {
        var root = await RepositoryAsync(name);
        await CommitAsync(root, ".gitattributes", "slow.txt diff=slow\n", "attributes");
        await CommitAsync(root, "slow.txt", "one\n", "slow one");
        await CommitAsync(root, "slow.txt", "two\n", "slow two");
        return root;
    }

    private static string[] Diff(string script, string prefix = "a/") =>
        ["-c", $"diff.slow.textconv=sh '{Sh(script)}'", "diff", $"--src-prefix={prefix}", "HEAD~1", "HEAD", "--", "slow.txt"];

    /// <summary>
    /// The instance pipe's stand-in: each ask a script wrote is answered by <paramref name="ask"/> beneath the git its process descends
    /// from, read by its real ancestry, until <paramref name="stop"/>; how many were asked and how many found a placed git above them.
    /// </summary>
    private async Task<(int Asked, int Lent)> AnswerAsync(Func<string, Task<string>> ask, CancellationToken stop)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var answering = new List<Task>();
        var lent = 0;
        while (!stop.IsCancellationRequested)
        {
            foreach (var file in Directory.GetFiles(Waits, "ask-*").Where(file => !file.EndsWith(".part", StringComparison.Ordinal) && seen.Add(file)))
            {
                answering.Add(OneAsync(file));
            }

            try
            {
                await Task.Delay(50, stop);
            }
            catch (OperationCanceledException)
            {
            }
        }

        foreach (var file in Directory.GetFiles(Waits, "ask-*").Where(file => !file.EndsWith(".part", StringComparison.Ordinal) && seen.Add(file)))
        {
            answering.Add(OneAsync(file));
        }

        await Task.WhenAll(answering).WaitAsync(Within);
        return (seen.Count, lent);

        async Task OneAsync(string file)
        {
            var lines = (await File.ReadAllTextAsync(file)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var ancestors = ProcessParents.AncestorsOf(int.Parse(lines[0], System.Globalization.CultureInfo.InvariantCulture));
            if (GitScheduler.Shared.PlaceOf(ancestors) is not null) Interlocked.Increment(ref lent);
            string answer;
            using (GitCallers.Beneath(() => ancestors)) answer = await ask(Path.GetFullPath(lines[1]));
            await File.WriteAllTextAsync(Path.Combine(Waits, "answer-" + Path.GetFileName(file)["ask-".Length..]), answer);
        }
    }

    [Fact]
    public async Task A_fan_out_of_hundreds_of_real_gits_holds_no_more_places_than_the_bound()
    {
        var roots = await Task.WhenAll(Enumerable.Range(0, 6).Select(at => RepositoryAsync($"fan{at}")));
        var heads = await Task.WhenAll(roots.Select(root => RevAsync(root, "HEAD")));
        var most = 0;
        using var sampling = new CancellationTokenSource();
        var sampler = Task.Run(async () =>
        {
            while (!sampling.IsCancellationRequested)
            {
                most = Math.Max(most, GitScheduler.Shared.Running);
                await Task.Yield();
            }
        });
        var begun = GitScheduler.Shared.Started;
        var clock = Stopwatch.StartNew();

        var asks = Enumerable.Range(0, 240).Select(at =>
        {
            Task<(int Code, string Stdout, string Stderr)> Ask() => Git(roots[at % 6], "log", "-1", $"--format=%H {at}");
            return at % 3 == 0 ? Person(Ask) : Ask();
        }).ToList();
        var answers = await Task.WhenAll(asks).WaitAsync(Poll.Patience);
        await sampling.CancelAsync();
        await sampler;

        foreach (var (answer, at) in answers.Select((answer, at) => (answer, at)))
        {
            Assert.True(answer.Code == 0, answer.Stderr);
            Assert.Equal($"{heads[at % 6]} {at}", answer.Stdout.Trim());
        }

        Assert.InRange(most, 1, GitScheduler.Bound);
        Assert.True(GitScheduler.Shared.Started - begun >= 240, "every git passed the scheduler");
        output.WriteLine($"240 gits in {clock.Elapsed.TotalSeconds:0.0} s, at most {most} places held");
    }

    [Fact]
    public async Task A_persons_read_is_placed_while_a_hook_running_push_a_fetch_waiting_on_its_grant_and_a_background_diff_hold_every_other_place()
    {
        var (pusher, _) = await RepositoryWithOriginAsync("pusher");
        var fetcher = await RepositoryAsync("fetcher");
        var differ = await DiffedAsync("differ");
        await HookAsync(pusher, "pre-push", Waiter("hook", "exit 0"));
        var ssh = Waiter("ssh", "exit 1");
        var textconv = Waiter("textconv", "cat \"$1\"");

        var push = Person(() => Git(pusher, "push", "origin", "HEAD:refs/heads/pushed"));
        var fetch = Person(() => Git(fetcher, "-c", "ssh.variant=simple", "-c", $"core.sshCommand=sh '{Sh(ssh)}'", "fetch", "ssh://grant.invalid/held.git"));
        var diff = Git(differ, Diff(textconv));
        await Poll.Until(() => Started("hook") > 0 && Started("ssh") > 0 && Started("textconv") > 0);
        Assert.InRange(GitScheduler.Shared.Running, 3, GitScheduler.Bound);

        long waited = 0;
        var clock = Stopwatch.StartNew();
        (int Code, string Stdout, string Stderr) mine;
        using (GitScheduler.Timing(ticks => waited = ticks)) mine = await Person(() => Git(differ, "rev-parse", "HEAD")).WaitAsync(Within);
        clock.Stop();
        await File.WriteAllTextAsync(Release, "");

        Assert.True(mine.Code == 0, mine.Stderr);
        Assert.True((await push.WaitAsync(Within)).Code == 0);
        Assert.NotEqual(0, (await fetch.WaitAsync(Within)).Code);
        Assert.Contains("+two", (await diff.WaitAsync(Within)).Stdout, StringComparison.Ordinal);
        output.WriteLine($"the person's read waited {Stopwatch.GetElapsedTime(0, waited).TotalMilliseconds:0} ms in its queue, {clock.ElapsedMilliseconds} ms in all");
    }

    [Fact]
    public async Task Two_hook_running_commits_in_two_partial_clones_whose_hooks_each_read_a_filtered_blob_both_end_0()
    {
        var promised = await RepositoryAsync("promised");
        await CommitAsync(promised, "a.txt", "one\n", "a one");
        await CommitAsync(promised, "a.txt", "two\n", "a two");
        await GitAsync(promised, "config", "uploadpack.allowFilter", "true");
        var blob = await RevAsync(promised, "HEAD~1:a.txt");
        var hook = Asker("hook", "exit 0");
        var clones = new[] { "clone-one", "clone-two" }.Select(name => Path.Combine(Scratch, name)).ToArray();
        foreach (var clone in clones)
        {
            await GitAsync(Scratch, "clone", "--quiet", "--filter=blob:none", "--no-local", promised, clone);
            await GitSeed.NoMaintenanceAsync(clone);
            await HookAsync(clone, "pre-commit", hook);
        }

        using var stop = new CancellationTokenSource();
        var answering = AnswerAsync(async root =>
        {
            var read = await GitObjectReads.ReadAsync(root, blob, 64, CancellationToken.None);
            return read is { Answer.Missing: false } got ? Encoding.UTF8.GetString(got.Bytes, got.Answer.Start, got.Answer.Count) : "missing";
        }, stop.Token);
        var commits = clones.Select(clone => Git(clone, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--allow-empty", "-m", "hooked")).ToList();

        var done = await Task.WhenAll(commits).WaitAsync(Within);
        await stop.CancelAsync();
        var (asked, lent) = await answering;

        Assert.All(done, commit => Assert.True(commit.Code == 0, commit.Stderr));
        Assert.Equal(2, asked);
        Assert.Equal(2, lent);
        Assert.All(Directory.GetFiles(Waits, "answer-*"), answer => Assert.Equal("one\n", File.ReadAllText(answer)));
    }

    [Fact]
    public async Task Four_diffs_whose_textconv_helpers_ask_back_in_hold_all_four_places_and_each_ends()
    {
        var roots = await Task.WhenAll(Enumerable.Range(0, 4).Select(at => DiffedAsync($"helped{at}")));
        var helper = Asker("textconv", "cat \"$1\"");
        using var stop = new CancellationTokenSource();
        var answering = AnswerAsync(async root => (await Git(root, "rev-parse", "HEAD")).Stdout, stop.Token);

        var diffs = roots.Take(3).Select(root => Git(root, Diff(helper)))
            .Append(Person(() => Git(roots[3], Diff(helper)))).ToList();
        var done = await Task.WhenAll(diffs).WaitAsync(Within);
        await stop.CancelAsync();
        var (asked, lent) = await answering;

        Assert.All(done, diff => Assert.Contains("+two", diff.Stdout, StringComparison.Ordinal));
        Assert.InRange(asked, 4, 8);
        Assert.Equal(asked, lent);
    }

    [Fact]
    public async Task A_worktree_remove_runs_the_unendable_git_queued_in_its_tree_to_its_end_and_answers_one_asked_after_as_the_trees_absence()
    {
        var root = await DiffedAsync("removed");
        var tree = Path.Combine(Scratch, "removed-tree");
        await GitAsync(root, "worktree", "add", "--quiet", "-b", "tree", tree);
        var textconv = Waiter("textconv", "cat \"$1\"");
        // The repository's two background places, held.
        var held = new[] { "a/", "b/" }.Select(prefix => Git(root, Diff(textconv, prefix))).ToList();
        await Poll.Until(() => Started("textconv") >= 2);

        // A status told nothing of optional locks refreshes the tree's index under its lock: a git no removal may end.
        var queued = Git(tree, "status", "--porcelain");
        var removal = Person(() => Git(root, "worktree", "remove", "--force", tree));
        var after = Git(tree, "rev-parse", "HEAD");
        Assert.False(queued.IsCompleted || removal.IsCompleted || after.IsCompleted);
        await File.WriteAllTextAsync(Release, "");

        var ran = await queued.WaitAsync(Within);
        Assert.True(ran.Code == 0, ran.Stderr);
        Assert.Equal("", ran.Stdout.Trim());
        var removed = await removal.WaitAsync(Within);
        Assert.True(removed.Code == 0, removed.Stderr);
        Assert.False(Directory.Exists(tree));
        Assert.NotEqual(0, (await after.WaitAsync(Within)).Code);
        await Task.WhenAll(held).WaitAsync(Within);
    }

    [Fact]
    public async Task A_write_passes_no_read_queued_ahead_of_it_and_a_read_its_flow_asks_after_it_reads_what_it_wrote()
    {
        var root = await DiffedAsync("ordered");
        var before = await RevAsync(root, "HEAD~1");
        var moved = await RevAsync(root, "HEAD");
        await GitAsync(root, "update-ref", "refs/heads/later", before);
        var textconv = Waiter("textconv", "cat \"$1\"");
        var held = new[] { "a/", "b/" }.Select(prefix => Git(root, Diff(textconv, prefix))).ToList();
        await Poll.Until(() => Started("textconv") >= 2);
        // The write holds its transaction prepared, the ref not yet moved, until told: a read placed before it reads the ref before it.
        var written = Path.Combine(Waits, "written");
        await HookAsync(root, "reference-transaction", Waiter("transaction", "exit 0", written, when: "prepared"));

        var read = Git(root, "rev-parse", "refs/heads/later");
        var write = Task.Run(async () =>
        {
            var wrote = await Git(root, "update-ref", "refs/heads/later", moved);
            return (wrote, after: await Git(root, "rev-parse", "refs/heads/later"));
        });
        await File.WriteAllTextAsync(Release, "");

        Assert.Equal(before, (await read.WaitAsync(Within)).Stdout.Trim());
        await Poll.Until(() => Started("transaction") > 0);
        await File.WriteAllTextAsync(written, "");
        var (wrote, after) = await write.WaitAsync(Within);
        Assert.True(wrote.Code == 0, wrote.Stderr);
        Assert.Equal(moved, after.Stdout.Trim());
        await Task.WhenAll(held).WaitAsync(Within);
    }
}
