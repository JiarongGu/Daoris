using System.Diagnostics;
using System.Text;
using static Daoris.Driver.WorkingTree;

namespace Daoris.Driver;

/// <summary>How Daoris's own git starts and runs: its start, the three runners and the test seams beneath them; <see cref="WorkingTree"/> is the door.</summary>
internal static class GitRuns
{
    /// <summary>
    /// How Daoris's own git starts: the file Tools resolves for git, by its whole path, with the
    /// tools' environment — or the refusal of a git the person chose that cannot run, which never falls back to
    /// <c>PATH</c>. Every git call here and in <see cref="SessionTrees"/> starts through this.
    /// </summary>
    /// <param name="home">The Daoris home whose <c>tools.json</c> says which git; null is a machine with no home, whose
    /// git is the system's, as before.</param>
    internal static (ProcessStartInfo? Info, string? Refusal) GitStart(string root, IReadOnlyList<string> arguments, string? home)
    {
        var read = home is null ? null : Tools.Read(home);
        var git = read is null
            ? new ToolResolution("git", ToolWay.System, null, CommandPresence.Resolve("git", startable: true), false, null)
            : Tools.Resolve(read, home!, "git");
        if (git.Refused) return (null, git.Problem);

        var info = new ProcessStartInfo
        {
            // The system's git PATH does not find keeps the bare name, so the start fails in the system's own words, as before.
            FileName = git.File ?? "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true, // git runs every tick; from a window it must not flash a console (Adapters.Shell)
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // 🔴 Long paths, for every call: a session tree's prefix is longer than its root's, so
        // a file that fits under the root can pass Windows' 260 characters in a tree. Without this git
        // cannot open it, and says so as a change it cannot read: a clean-up keeps a tree for "uncommitted
        // work" that is only committed files, and a removal fails half done.
        // Said on the command line, so nothing in the repository's own configuration changes; Daoris's own
        // need, so it stays here rather than in the git the person's sessions share.
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("core.longpaths=true");
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        if (read is not null) Tools.Hand(info, read, home!);
        else GitWiring.Hand(info); // with no home there is no tools' file, and still nothing may prompt
        // The system's git, where it is Git for Windows' launcher, starts as the git behind it with the
        // launcher's variables, one process rather than two. A managed git is laid out alike (its record names
        // cmd/git.exe) and goes the same way. A git the person named as a file starts as it is.
        if (git.Way is ToolWay.System or ToolWay.Managed) GitForWindows.Direct(info);
        return (info, null);
    }

    /// <summary>
    /// The git Daoris's own git runs, as a held remote names it: its file, and when that file
    /// is last written, so a git the person chooses in Tools, or one updated in place, is another git and a held remote is tried
    /// again. Null where no git resolves.
    /// </summary>
    internal static string? GitInUse(string? home)
    {
        var read = home is null ? null : Tools.Read(home);
        var git = read is null
            ? new ToolResolution("git", ToolWay.System, null, CommandPresence.Resolve("git", startable: true), false, null)
            : Tools.Resolve(read, home!, "git");
        if (git.Refused || git.File is not { } path) return null;
        var file = new FileInfo(path);
        return file.Exists ? $"{file.FullName}|{file.LastWriteTimeUtc.Ticks}" : file.FullName;
    }

    /// <summary>
    /// The test seam: called with the arguments of each git this flow starts, through <see cref="GitAsync(string, IReadOnlyList{string}, CancellationToken)"/>,
    /// <see cref="ReadGitAsync"/> or <see cref="GitBytesAsync"/> (raised by <see cref="GitLaunch"/>, beneath all three),
    /// so a test counts the product's starts. Null in production; an async local, so classes running in parallel never see
    /// each other's starts. A callback must be safe to call from several threads, as a flow's fetches run side by side.
    /// </summary>
    internal static readonly AsyncLocal<Action<IReadOnlyList<string>>?> Started = new();

    /// <summary>
    /// The test seam for git's answer: where it returns a reply for a start's arguments, that reply stands in for
    /// git, so a missing git, a ref git cannot read and a timeout are each one fast case. Null in production, and an async local.
    /// </summary>
    internal static readonly AsyncLocal<Func<IReadOnlyList<string>, (int Code, string Stdout, string Stderr)?>?> Answered = new();

    /// <summary>
    /// The test seam for git's run: where set, it stands in for the runner, handed the arguments, the bound the call
    /// runs under and the caller's token, so a test sees the bound a kind is given and the token that reaches git, and answers a
    /// run that never ended in it. Null in production, and an async local. It stands in for <see cref="ReadGitAsync"/>'s run too, its
    /// output handed to the reader whole, so Review's census holds every start of its graph.
    /// </summary>
    internal static readonly AsyncLocal<Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ChildRun>>?> Runs = new();

    // Internal rather than private: SessionTrees asks git the same way for the same reason —
    // one process-spawning implementation, not two that differ in encoding or error shape.
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, CancellationToken ct) =>
        GitAsync(root, arguments, environment: null, timeout: null, ct);

    /// <summary>
    /// git with variables of its own and a bound on how long it may take: a fetch reaches the network as
    /// the person, and one that waits on a credential nobody is there to type must end rather than hold a press.
    /// </summary>
    /// <remarks>On the bound, git and whatever it started are stopped, and the answer is a failure that says why.</remarks>
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        CancellationToken ct) =>
        GitAsync(root, arguments, environment, timeout, input: null, ct);

    /// <summary>
    /// git reading <paramref name="input"/> as its standard input: <c>update-ref --stdin</c> takes a
    /// transaction there, so a branch's delete and the refs its proof rested on are checked together under git's own locks.
    /// </summary>
    /// <remarks>Written as BOM-less UTF-8, LF as given: git reads a byte-order mark as part of the first command.</remarks>
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, string input, CancellationToken ct) =>
        GitAsync(root, arguments, environment: null, timeout: null, input, ct);

    /// <summary>
    /// git with variables of its own reading <paramref name="input"/>: the piece keeper's <c>update-index --index-info</c>
    /// fills a temporary index named by <c>GIT_INDEX_FILE</c>, never the repository's own.
    /// </summary>
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string input, CancellationToken ct) =>
        GitAsync(root, arguments, environment, timeout: null, input, ct);

    private static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        string? input, CancellationToken ct)
    {
        // Within a landing, a read of a ref or a repository is asked of git once; within a judgement, a question
        // of commits by their ids is asked of its checkout's git once. A call with a variable of its own or a bound (a fetch), one
        // that writes git's input (`update-ref --stdin`) and a test's stand-in answer are always asked as written.
        Func<CancellationToken, Task<(int Code, string Stdout, string Stderr)>> run = token => RunGitAsync(root, arguments, environment, timeout, input, token);
        return environment is null && timeout is null && input is null && Answered.Value is null
            ? GitFacts.Asked(root, arguments, run, ct) ?? RefSnapshot.Asked(root, arguments, run, ct) ?? GitReadOnce.Asked(root, arguments, run, ct) ?? run(ct)
            : run(ct);
    }

    private static Task<(int Code, string Stdout, string Stderr)> RunGitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        string? input, CancellationToken ct)
    {
        // A git that may move a ref drops what this flow is told, before it starts as well as after it ends.
        GitFacts.Ran(arguments);
        // Counted, classified and timed at the one seam, which raises Started; the arguments go on as they came.
        return GitLaunch.LaunchAsync(
            GitLaunch.KindOf(arguments), arguments, GitLaunch.Budget, GitRunner.Whole,
            () => RunLaunchedAsync(root, arguments, environment, timeout, input, ct));
    }

    private static async Task<(int Code, string Stdout, string Stderr)> RunLaunchedAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        string? input, CancellationToken ct)
    {
        if (Answered.Value?.Invoke(arguments) is { } answered) return answered;
        // No git runs unbounded. The caller's bound where it passed one (a fetch, a replay), else
        // its kind's; past it git's tree is ended and the answer is a failure saying so, which every caller reads as a read
        // that failed: its subject unknown until the next wake asks again.
        var bound = GitBounds.Of(arguments, timeout);
        if (Runs.Value is { } stand)
        {
            try
            {
                return Answer(await stand(arguments, bound, ct).ConfigureAwait(false), bound);
            }
            finally
            {
                RefSnapshot.Ran(arguments);
                GitFacts.Ran(arguments);
            }
        }

        // The program's home, never the variable the process inherited: a test's or a tool's process names none.
        var (info, refusal) = GitStart(root, arguments, ProgramHome.Named);
        // A git the person chose that cannot run is git's answer, said in the resolution's words: never PATH's.
        if (info is null) return (-1, "", refusal!);
        if (environment is not null)
        {
            foreach (var (name, value) in environment) info.Environment[name] = value;
        }

        if (input is not null)
        {
            info.RedirectStandardInput = true;
            info.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        // Placed by the scheduler, whose token ends a lock-free read nobody waits for; the run, the wait and the guarded tree kill
        // are ChildProcess's. The caller's own cancellation leaves the ask and throws.
        try
        {
            await GitLazyFetch.PrepareAsync(info, arguments, ct).ConfigureAwait(false);
            var ask = GitStartAsks.For(info, arguments, GitRunner.Whole, bound, shares: input is null);
            var run = await GitScheduler.Shared.RunAsync(ask, token => input is null
                ? ChildProcess.RunAsync(info, bound, token, closeInput: false)
                : ChildProcess.RunAsync(info, bound, token, input, ProcessStarts.Shared), ct).ConfigureAwait(false);
            return Answer(run, bound);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return (-1, "", error.Message);
        }
        finally
        {
            // A git that may have moved a ref, finished, stopped or failed, drops the refs this flow read at once.
            RefSnapshot.Ran(arguments);
            GitFacts.Ran(arguments);
        }
    }

    /// <summary>A run's end as git's answer: its code and streams, or a failure saying it did not start or did not answer in time.</summary>
    private static (int Code, string Stdout, string Stderr) Answer(ChildRun run, TimeSpan bound)
    {
        if (!run.Started) return (-1, "", run.Error?.Message ?? "git did not start");
        // One that outlived its kill is not said to be stopped.
        if (run.TimedOut && run.Error is ProcessUnendedException unended)
        {
            return (-1, "", $"git did not answer within {bound.TotalSeconds:0} seconds, and did not end when stopped: {unended.Reason}.");
        }

        if (run.TimedOut) return (-1, "", $"git did not answer within {bound.TotalSeconds:0} seconds, and was stopped.");
        return (run.ExitCode!.Value, run.Stdout, run.Stderr);
    }

    /// <summary>
    /// The first <paramref name="limit"/> bytes git writes, as bytes: a blob read for a preview, which may be
    /// binary and may be larger than any preview shows. Null where git failed or would not start.
    /// </summary>
    /// <remarks>
    /// Bytes rather than <see cref="GitAsync(string, IReadOnlyList{string}, CancellationToken)"/>'s text, since a blob
    /// decoded as UTF-8 on its way in is no longer the blob; and bounded, since a file has no upper size. Once the bound
    /// is read, git is stopped rather than drained: the rest is only counted, from what git said of the blob's size.
    /// Bounded in time too, by its kind (<see cref="GitBounds"/>); past it, or on the caller's cancellation, git's
    /// tree is ended, and the answer is null, or the cancellation thrown.
    /// </remarks>
    internal static Task<(byte[] Bytes, int Count)?> GitBytesAsync(
        string root, IReadOnlyList<string> arguments, int limit, CancellationToken ct) =>
        GitBytesAsync(root, arguments, limit, input: null, ct);

    /// <summary>The same, git reading <paramref name="input"/> first: <c>cat-file --batch</c> asked its one question.</summary>
    internal static Task<(byte[] Bytes, int Count)?> GitBytesAsync(
        string root, IReadOnlyList<string> arguments, int limit, string? input, CancellationToken ct) =>
        // Through the one seam, which raises Started for this runner too (once RunGitAsync's alone).
        GitLaunch.LaunchAsync(
            GitLaunch.KindOf(arguments), arguments, GitLaunch.Budget, GitRunner.Bytes, () => BytesLaunchedAsync(root, arguments, limit, input, ct));

    private static async Task<(byte[] Bytes, int Count)?> BytesLaunchedAsync(
        string root, IReadOnlyList<string> arguments, int limit, string? input, CancellationToken ct)
    {
        // The blob is read as bytes off the stream beneath; git's own words on the error stream are UTF-8.
        var (info, _) = GitStart(root, arguments, ProgramHome.Named);
        if (info is null) return null;
        if (input is not null)
        {
            info.RedirectStandardInput = true;
            info.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            GitStartInputs.Hand(info, input);
        }

        try
        {
            await GitLazyFetch.PrepareAsync(info, arguments, ct).ConfigureAwait(false);
            var ask = GitStartAsks.For(info, arguments, GitRunner.Bytes, GitBounds.Of(arguments), shares: false);
            return await GitScheduler.Shared.RunAsync(ask, token => BytesPlacedAsync(info, arguments, limit, input, token), ct).ConfigureAwait(false);
        }
        catch (GitUnplacedException)
        {
            return null;
        }
    }

    private static async Task<(byte[] Bytes, int Count)?> BytesPlacedAsync(
        ProcessStartInfo info, IReadOnlyList<string> arguments, int limit, string? input, CancellationToken ct)
    {
        Process? process = null;
        Task<string>? stderr = null;
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            // Through the process-wide gate, off the caller's thread, in its place's class, which the gate reads over the one named here.
            process = await ProcessStarts.Shared.StartAsync(info, ProcessPurpose.Git, StartClass.Background, ct).ConfigureAwait(false)
                      ?? throw new DriverException("git did not start");
            within.CancelAfter(GitBounds.Of(arguments));
            using var ending = Ending(within.Token, process);
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), within.Token).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            stderr = process.StandardError.ReadToEndAsync(within.Token);
            // Grown as git writes, so a whole file's bound is not its room.
            var bound = Math.Max(0, limit);
            var buffer = new byte[Math.Min(bound, 64 * 1024)];
            var count = 0;
            var stream = process.StandardOutput.BaseStream;
            while (count < bound)
            {
                if (count == buffer.Length) Array.Resize(ref buffer, (int)Math.Min(bound, 2L * buffer.Length));
                var got = await Heard(stream.ReadAsync(buffer.AsMemory(count), within.Token), within.Token).ConfigureAwait(false);
                if (got == 0) break;
                count += got;
            }

            // More than the bound is waiting: git would block on a full pipe, so it is stopped. What it wrote is whole.
            var bounded = count == bound && await Heard(stream.ReadAsync(new byte[1], within.Token), within.Token).ConfigureAwait(false) > 0;
            if (bounded)
            {
                OwnedProcesses.End(process);
            }

            await process.WaitForExitAsync(within.Token).ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            // A read the kill below ended answers as git's end would: past the bound, it is none.
            within.Token.ThrowIfCancellationRequested();
            return bounded || process.ExitCode == 0 ? (buffer, count) : null;
        }
        catch (OperationCanceledException) when (process is not null)
        {
            await EndAsync(process, stderr, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return null;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A read that failed under a running git ends it before the answer, as a cancelled one does.
            if (process is not null) await EndAsync(process, stderr, CancellationToken.None).ConfigureAwait(false);
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// A pipe read that ends when <paramref name="ct"/> is cancelled: a read of git's redirected pipe does not hear its
    /// token, and only git's end closes the pipe under it, so a git that outlived its kill would hold its runner in the read and
    /// never be said. The read left behind writes into a buffer nobody reads again.
    /// </summary>
    private static Task<int> Heard(ValueTask<int> read, CancellationToken ct) => read.IsCompleted ? read.AsTask() : read.AsTask().WaitAsync(ct);

    /// <summary>
    /// A streamed git ended as its bound passes or its caller cancels: a read of a pipe does not always hear its
    /// token, and the kill closes the pipe under it, so the read ends with git.
    /// </summary>
    private static CancellationTokenRegistration Ending(CancellationToken within, Process process) =>
        within.Register(() => OwnedProcesses.End(process));

    /// <summary>
    /// A streamed git past its bound or its caller's cancellation: its tree ended, never left running unowned as
    /// a cancelled read once left it, and its error stream's read let finish unobserved, the answer already "none".
    /// Its exit awaited after the kill (<see cref="ChildProcess.EndAsync"/>), so the runner never returns while
    /// git runs on; one that outlived its kill is thrown as <paramref name="ct"/>'s cancellation where the caller cancelled,
    /// and is otherwise already said by the seam, the answer "none" either way.
    /// </summary>
    private static async Task EndAsync(Process process, Task<string>? stderr, CancellationToken ct)
    {
        ProcessUnendedException? unended = null;
        try
        {
            await ChildProcess.EndAsync(process, ct).ConfigureAwait(false);
        }
        catch (ProcessUnendedException said)
        {
            unended = said;
        }

        // A git that runs on holds its pipe open, so its error stream's read is only observed, never awaited.
        var drained = stderr?.ContinueWith(_ => { }, TaskScheduler.Default);
        if (unended is null && drained is not null) await drained.ConfigureAwait(false);
        if (unended is not null && ct.IsCancellationRequested) throw unended;
    }

    /// <summary>
    /// git, read as it answers: each piece of what it writes is handed to <paramref name="take"/>, decoded as
    /// UTF-8, and once <paramref name="take"/> answers false git is stopped rather than drained, since the rest is not
    /// wanted. 0 where it is stopped so, git's own code otherwise, -1 where git did not start or could not be read.
    /// </summary>
    /// <remarks>
    /// <see cref="GitBytesAsync"/>'s shape for text: a range's patch has no upper size, and a review that has spent its
    /// bound reads no further. git's words on the error stream are drained beside it, so a full pipe never holds git.
    /// Bounded in time by its kind (<see cref="GitBounds"/>); past it git's tree is ended and the answer is -1, a
    /// read that failed, and on the caller's cancellation its tree is ended and the cancellation thrown.
    /// </remarks>
    internal static Task<int> ReadGitAsync(
        string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct) =>
        (Runs.Value is null && Answered.Value is null ? GitReadOnce.Streamed(root, arguments, take, ct) : null)
        ?? ReadGitAsync(root, arguments, environment: null, take, ct);

    /// <summary>git read as it answers, with variables of its own (Review's untracked diff names its private index by
    /// <c>GIT_INDEX_FILE</c>): always asked of git.</summary>
    internal static Task<int> ReadGitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, Func<ReadOnlyMemory<char>, bool> take,
        CancellationToken ct) =>
        // Through the one seam, which raises Started for this runner too (once RunGitAsync's alone).
        GitLaunch.LaunchAsync(
            GitLaunch.KindOf(arguments), arguments, GitLaunch.Budget, GitRunner.Streamed, () => ReadLaunchedAsync(root, arguments, environment, take, ct));

    private static async Task<int> ReadLaunchedAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, Func<ReadOnlyMemory<char>, bool> take,
        CancellationToken ct)
    {
        // The run's test seam stands in for this runner too, so a census holds every answer of Review's graph.
        if (Runs.Value is { } stand)
        {
            var run = await stand(arguments, GitBounds.Of(arguments), ct).ConfigureAwait(false);
            if (!run.Started || run.TimedOut) return -1;
            return take(run.Stdout.AsMemory()) ? run.ExitCode!.Value : 0;
        }

        var (info, _) = GitStart(root, arguments, ProgramHome.Named);
        if (info is null) return -1;
        foreach (var (name, value) in environment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        try
        {
            await GitLazyFetch.PrepareAsync(info, arguments, ct).ConfigureAwait(false);
            var ask = GitStartAsks.For(info, arguments, GitRunner.Streamed, GitBounds.Of(arguments), shares: false);
            return await GitScheduler.Shared.RunAsync(ask, token => ReadPlacedAsync(info, arguments, take, token), ct).ConfigureAwait(false);
        }
        catch (GitUnplacedException)
        {
            return -1;
        }
    }

    private static async Task<int> ReadPlacedAsync(
        ProcessStartInfo info, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        Process? process = null;
        Task<string>? stderr = null;
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            // Through the process-wide gate, off the caller's thread, in its place's class, which the gate reads over the one named here.
            process = await ProcessStarts.Shared.StartAsync(info, ProcessPurpose.Git, StartClass.Background, ct).ConfigureAwait(false)
                      ?? throw new DriverException("git did not start");
            within.CancelAfter(GitBounds.Of(arguments));
            using var ending = Ending(within.Token, process);
            stderr = process.StandardError.ReadToEndAsync(within.Token);
            var buffer = new char[16 * 1024];
            var stopped = false;
            int got;
            while (!stopped && (got = await Heard(process.StandardOutput.ReadAsync(buffer.AsMemory(), within.Token), within.Token).ConfigureAwait(false)) > 0)
            {
                stopped = !take(buffer.AsMemory(0, got));
            }

            if (stopped)
            {
                OwnedProcesses.End(process);
            }

            await process.WaitForExitAsync(within.Token).ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            // A read the kill ended answers as git's end would: past the bound, it is a read that failed.
            within.Token.ThrowIfCancellationRequested();
            return stopped ? 0 : process.ExitCode;
        }
        catch (OperationCanceledException) when (process is not null)
        {
            await EndAsync(process, stderr, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return -1;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A read that failed under a running git ends it before the answer, as a cancelled one does.
            if (process is not null) await EndAsync(process, stderr, CancellationToken.None).ConfigureAwait(false);
            return -1;
        }
        finally
        {
            process?.Dispose();
        }
    }
}
