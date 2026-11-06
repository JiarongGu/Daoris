namespace Daoris.Driver;

/// <summary>
/// <c>events relay</c> (EVENTHOOK1c; the event hook design §6.1): <c>--where</c> lays this build's copy out in the home and prints its
/// command; <c>--once</c>, run from that copy, serves one pipe for the <c>event-relay</c> plugin and prints one batch. The host routes it at
/// its entry, after the role terminal's gate and before the home's adoption, the generic lease and the machine log, so every line either
/// form writes, on either stream, is one of <see cref="RelaySaid"/>: one catch here says any failure by its closed code. It holds no key
/// and opens no door of Daoris's; only <c>--where</c> holds the home's lease, for its short run, by the generic path's own call.
/// </summary>
[CommandGroup("events", "relay")]
public static class RelayCommand
{
    /// <summary>The variables the relay removes unread: no Daoris key travels with it.</summary>
    public static readonly IReadOnlyList<string> Keys = ["DAORIS_SERVICE_KEY", "DAORIS_REMOTE_KEY"];

    /// <summary>The words after <c>events</c>, run to their exit code.</summary>
    /// <param name="baseDirectory">The folder this program runs from: the install's <c>app/daoris-driver/</c> for <c>--where</c>, a copy for the relay.</param>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, TextWriter output, TextWriter error, string baseDirectory, RelayWorld? world = null, CancellationToken ct = default)
    {
        try
        {
            if (args.Count == 0 || args[0] != "relay")
            {
                error.WriteLine(RelaySaid.Usage);
                return 2;
            }

            if (RelayAsk.Read([.. args.Skip(1)], out var refused) is not { } ask)
            {
                error.WriteLine(refused);
                return 2;
            }

            return ask.Where ? Where(output, error, baseDirectory) : await OnceAsync(ask, output, error, baseDirectory, world ?? new RelayWorld(), ct).ConfigureAwait(false);
        }
        catch (RelayStop stop)
        {
            error.WriteLine(RelaySaid.Stopped(stop.Code));
            return 2;
        }
        catch (Exception failure) when (failure is HomeLeaseException or HomeEntryTwiceException)
        {
            error.WriteLine(RelaySaid.Stopped(failure is HomeLeaseException ? "lease" : "home"));
            return 2;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            error.WriteLine(RelaySaid.Stopped("copy"));
            return 2;
        }
        catch (Exception)
        {
            error.WriteLine(RelaySaid.Stopped("failed"));
            return 2;
        }
    }

    // The install's home and the generic lease, said only by fixed sentences; the copy laid out and its command printed.
    private static int Where(TextWriter output, TextWriter error, string baseDirectory)
    {
        DaorisHome.Adopt(baseDirectory);
        if (DaorisHome.Resolve(Environment.GetEnvironmentVariable, baseDirectory) is not { } home) throw new RelayStop("home");
        using var lease = HomeLease.ForVerb(home, "events relay", out _);
        if (lease is null)
        {
            error.WriteLine(RelaySaid.HeldByAnother);
            return 1;
        }

        var copy = RelayCopy.LayOut(home, baseDirectory, error.WriteLine);
        output.WriteLine(RelaySaid.Start(Path.Combine(copy, OperatingSystem.IsWindows() ? "daoris-driver.exe" : "daoris-driver"), Path.GetFullPath(home)));
        return 0;
    }

    private static async Task<int> OnceAsync(RelayAsk ask, TextWriter output, TextWriter error, string baseDirectory, RelayWorld world, CancellationToken ct)
    {
        // What the host took at entry is let go, and no Daoris key or messaging variable stays in this process.
        HolderKey.Drop();
        HolderKey.Strip();
        foreach (var key in Keys) Environment.SetEnvironmentVariable(key, null);
        if ((ask.Home ?? Environment.GetEnvironmentVariable(DaorisHome.Variable)) is not { Length: > 0 } home)
        {
            error.WriteLine(RelaySaid.NoHome);
            return 2;
        }

        using var held = RelayCopy.Hold(baseDirectory, home, out var build);
        if (held is null)
        {
            error.WriteLine(RelaySaid.NotFromCopy);
            return 1;
        }

        return await RelayRun.RunAsync(home, ask, build, output, error, world, ct).ConfigureAwait(false);
    }
}
