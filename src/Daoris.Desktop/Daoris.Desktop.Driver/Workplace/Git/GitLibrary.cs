using LibGit2Sharp;

namespace Daoris.Driver;

/// <summary>
/// The git library's one door: a question asked of LibGit2Sharp in the asker's folder, admitted from the repository's own files at
/// each question (<see cref="GitLibraryAdmission"/>), run on the thread pool in a <see cref="Repository"/> opened for that one call and
/// disposed before it answers, so no pack, index or graph the library mapped outlives the call. Null where the question is refused,
/// the library cannot open the folder or a call throws, and the caller asks git.
/// </summary>
/// <remarks>
/// <para><b>Nothing of the binding leaves it.</b> A call answers strings and ids made inside it; no LibGit2Sharp object crosses back.</para>
/// <para><b>Configuration the repository's own.</b> libgit2's global, XDG, system and program-data search paths point at a folder that
/// holds no file, so a call reads the repository's <c>config</c> and its tree's <c>config.worktree</c> alone; what git reads beyond
/// them that a question depends on is the admission's to refuse. Owner validation stays on, so a folder git refuses for its owner the
/// library refuses too.</para>
/// </remarks>
internal static class GitLibrary
{
    /// <summary>The calls that run at once in the process; a fan-out over many repositories waits for one to end.</summary>
    internal const int AtOnce = 8;

    private static readonly Lazy<bool> s_settled = new(Settle, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly SemaphoreSlim s_calls = new(AtOnce, AtOnce);
    private static int s_open;

    /// <summary>The repositories open now: zero between calls, never above <see cref="AtOnce"/>.</summary>
    internal static int Open => Volatile.Read(ref s_open);

    /// <summary>
    /// <paramref name="call"/>'s answer over <paramref name="folder"/>'s repository, opened for it alone; null where refused or failed.
    /// </summary>
    internal static async Task<T?> AskAsync<T>(string folder, GitLibraryQuestion question, Func<GitLibraryCall, T?> call, CancellationToken ct)
        where T : class
    {
        var began = System.Diagnostics.Stopwatch.GetTimestamp();
        await s_calls.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (GitLibraryFaults.RefusalOf(folder) is { } fault) return Fell<T>(folder, fault);
            if (GitLibraryAdmission.RefusalOf(folder, question) is { } refusal) return Fell<T>(folder, refusal);
            if (RefFiles.GitDirOf(folder) is not { } gitDir || RefFiles.CommonDirOf(folder) is not { } commonDir) return Fell<T>(folder, GitLibraryAdmission.NoRepository);
            var (answer, why) = await Task.Run(() => Call(folder, gitDir, commonDir, call), ct).ConfigureAwait(false);
            if (answer is null) return Fell<T>(folder, why ?? GitLibraryJournal.Unanswered);
            GitLibraryJournal.Answered(folder);
            GitLaunch.Answered(question, began);
            return answer;
        }
        finally
        {
            s_calls.Release();
        }
    }

    private static T? Fell<T>(string folder, string reason)
        where T : class
    {
        GitLibraryJournal.Fell(folder, reason);
        return null;
    }

    private static (T? Answer, string? Why) Call<T>(string folder, string gitDir, string commonDir, Func<GitLibraryCall, T?> call)
        where T : class
    {
        if (!s_settled.Value) return (null, GitLibraryJournal.Native);
        Interlocked.Increment(ref s_open);
        var lease = GitLibraryFaults.Enter(folder);
        try
        {
            using var repository = new Repository(folder);
            return (call(new GitLibraryCall(repository, gitDir, commonDir)), null);
        }
        catch (Exception)
        {
            return (null, GitLibraryJournal.Failed);
        }
        finally
        {
            GitLibraryFaults.Leave(lease);
            Interlocked.Decrement(ref s_open);
        }
    }

    private static bool Settle()
    {
        try
        {
            var none = Path.Combine(AppContext.BaseDirectory, "git-library-config-none");
            foreach (var level in new[] { ConfigurationLevel.Global, ConfigurationLevel.Xdg, ConfigurationLevel.System, ConfigurationLevel.ProgramData })
            {
                GlobalSettings.SetConfigSearchPaths(level, none);
            }

            return true;
        }
        catch (Exception error) when (error is LibGit2SharpException or DllNotFoundException or TypeInitializationException)
        {
            // No native library beside the driver: every question is git's.
            return false;
        }
    }
}
