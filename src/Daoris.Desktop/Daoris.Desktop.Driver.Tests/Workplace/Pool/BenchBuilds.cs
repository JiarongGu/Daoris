using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What one build of a line wrote, how long each of its parts took, which failed, and the bases the door left standing.</summary>
internal sealed record BenchBuilt(int Files, long Bytes, int Deleted, double Seconds, string Parts, IReadOnlyList<string> Failed, int Unreleased)
{
    internal BenchStep Step => new(Files, Bytes, Deleted, Seconds);
}

/// <summary>
/// A line's build in a tree, per repository shape. This repository's is the door first, <c>daoris tree build</c> at the tree's top,
/// which builds each kept base's producer and releases it, then the two desktop test projects, the contracts generation and the web's
/// build; the web shape's is its one <c>npm run build</c>. What it wrote is counted from the file system, as <see cref="TreeWrites"/> counts.
/// </summary>
internal sealed class BenchBuilds(string home, bool web, bool on, BenchRuns run)
{
    internal async Task<BenchBuilt> BuildAsync(string folder)
    {
        if (!on) return new BenchBuilt(0, 0, 0, 0, "builds off", [], 0);
        var before = TreeWrites.Of(folder);
        var failed = new List<string>();
        var parts = new List<string>();
        var total = 0.0;
        async Task Part(string name, Func<Task<(double Seconds, int Code)>> step)
        {
            var (seconds, code) = await step();
            total += seconds;
            parts.Add($"{name} {seconds:F0} s");
            if (code != 0) failed.Add($"{name} exited {code}");
        }

        if (web)
        {
            await Part("build", () => run.NpmAsync(folder, "run", "build"));
        }
        else
        {
            await Part("door", () => DoorAsync(folder));
            var desktop = Path.Combine(folder, "src", "Daoris.Desktop");
            foreach (var project in new[] { "Daoris.Desktop.Driver.Tests", "Daoris.Desktop.Modules.Tests" })
            {
                await Part(project[(project.IndexOf('.', 7) + 1)..], () => run.TimedAsync(folder, "dotnet", "build", Path.Combine(desktop, project), "-nodeReuse:false", "--nologo", "-v", "q"));
            }

            await Part("contracts", () => run.TimedAsync(folder, "node", "tools/contracts.mjs"));
            await Part("web", () => run.NpmAsync(folder, "--prefix", "src/Daoris.Web", "run", "build"));
        }

        var wrote = before.Until(TreeWrites.Of(folder));
        var unreleased = TreeFilesRecord.Read(home, folder)?.Bases.Count ?? 0;
        return new BenchBuilt(wrote.Written, wrote.Bytes, wrote.Deleted, total, string.Join(", ", parts), failed, unreleased);
    }

    private async Task<(double, int)> DoorAsync(string folder)
    {
        var said = new StringWriter();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var code = await TreeBaseBuilds.BuildAsync(home, folder, said, said);
        var lines = said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var refused = lines.Where(line => line.Contains("nothing is released", StringComparison.Ordinal)).ToList();
        run.Say($"      the door, exit {code}: {lines.Count(line => line.Contains(" released, built by ", StringComparison.Ordinal))} released, "
            + $"{lines.Count(line => line.Contains("is no base", StringComparison.Ordinal))} set aside whole, {refused.Count} not released"
            + $"{(refused.Count > 0 ? ": " + refused[0][..Math.Min(600, refused[0].Length)] : "")}; {lines.FirstOrDefault(line => line.Contains("nothing to release", StringComparison.Ordinal)) ?? ""}");
        return (watch.Elapsed.TotalSeconds, code);
    }
}
