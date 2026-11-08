using System.Diagnostics;

namespace GitLibraryBench;

/// <summary>A git start's cost split into process creation and git's own work, one at a time and many at once, idle and beside a CPU load.</summary>
internal static class StartCost
{
    public static async Task RunAsync(GitBinary git, string repository, string scratch, int count)
    {
        Report.Machine();
        string[] ask = ["rev-parse", "HEAD"];
        await PhaseAsync("version, one at a time, no repository", git, scratch, count, parallel: false, load: false, ["--version"], scratch);
        await PhaseAsync("rev-parse HEAD, one at a time, untraced", git, repository, count, parallel: false, load: false, ask, null);
        await PhaseAsync("rev-parse HEAD, one at a time", git, repository, count, parallel: false, load: false, ask, scratch);
        await PhaseAsync($"rev-parse HEAD, {count} at once", git, repository, count, parallel: true, load: false, ask, scratch);
        await PhaseAsync("rev-parse HEAD, one at a time, every core busy", git, repository, count, parallel: false, load: true, ask, scratch);
        await PhaseAsync($"rev-parse HEAD, {count} at once, every core busy", git, repository, count, parallel: true, load: true, ask, scratch);
    }

    private static async Task PhaseAsync(
        string name, GitBinary git, string folder, int count, bool parallel, bool load, string[] ask, string? scratch)
    {
        string? traces = null;
        if (scratch is not null)
        {
            traces = Path.Combine(scratch, "trace", Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(traces);
        }

        using var busy = load ? CpuLoad.Start() : null;
        var wall = Stopwatch.StartNew();
        StartSample[] samples;
        if (parallel)
        {
            samples = await Task.WhenAll(Enumerable.Range(0, count).Select(index =>
                Task.Run(async () => (await git.RunAsync(folder, ask, traces is null ? null : Path.Combine(traces, $"{index}.json"))).Sample)));
        }
        else
        {
            samples = new StartSample[count];
            for (var index = 0; index < count; index++)
            {
                samples[index] = (await git.RunAsync(folder, ask, traces is null ? null : Path.Combine(traces, $"{index}.json"))).Sample;
            }
        }

        wall.Stop();
        Report.Starts(name, wall.Elapsed.TotalMilliseconds, samples);
        if (traces is not null) Directory.Delete(traces, recursive: true);
    }
}

/// <summary>A busy thread on every core, until disposed: the load an agent's build puts beside a start.</summary>
internal sealed class CpuLoad : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread[] _threads;

    private CpuLoad()
    {
        _threads = [.. Enumerable.Range(0, Environment.ProcessorCount).Select(_ => new Thread(Spin) { IsBackground = true, Priority = ThreadPriority.Normal })];
        foreach (var thread in _threads) thread.Start();
    }

    public static CpuLoad Start() => new();

    private void Spin()
    {
        var token = _stop.Token;
        var sum = 0UL;
        while (!token.IsCancellationRequested)
        {
            for (var i = 0; i < 100_000; i++) sum += (ulong)i * 2654435761UL;
        }

        GC.KeepAlive(sum);
    }

    public void Dispose()
    {
        _stop.Cancel();
        foreach (var thread in _threads) thread.Join();
        _stop.Dispose();
    }
}
