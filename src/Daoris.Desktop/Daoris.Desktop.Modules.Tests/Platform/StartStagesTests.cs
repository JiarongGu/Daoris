using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// STARTFAST1: the desktop's start writes one <c>start.stages</c> line, each stage in ms from the process's start, every stage
/// once and in the line's order whatever order they came in, so a slow start says where its seconds went.
/// </summary>
public sealed class StartStagesTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-startfast1-" + Guid.NewGuid().ToString("N")[..8]);

    public StartStagesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private JsonElement[] Lines()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
    }

    /// <summary>
    /// Every stage marked, the pipe after the page as a loop waiting on the home's lease serves it, and some twice: one line,
    /// every stage once in the line's order, each at its first mark, the splash's lifter named and nothing missing.
    /// </summary>
    [Fact]
    public async Task A_start_writes_every_stage_once_and_in_order()
    {
        using var log = new MachineLog(_home, "desktop");
        var stages = new StartStages(before: 120) { Log = log };
        var lift = new SplashLift(stages);

        stages.Mark(StartStages.Home);
        stages.Mark(StartStages.Driver);
        stages.Mark(StartStages.Host);
        stages.Mark(StartStages.Shown);
        stages.Mark(StartStages.Browser);
        var first = stages.Marks[StartStages.Browser];
        await Task.Delay(20);
        stages.Mark(StartStages.Browser);
        lift.PageSaid();
        lift.PageSaid();
        Assert.True(await lift.WaitAsync(log, TimeSpan.FromMinutes(5)).WaitAsync(Poll.Patience));
        Assert.DoesNotContain(Lines(), line => line.GetProperty("event").GetString() == "start.stages");
        stages.Mark(StartStages.Pipe);
        stages.Mark(StartStages.Pipe);
        stages.Write();

        var line = Assert.Single(Lines(), each => each.GetProperty("event").GetString() == "start.stages");
        Assert.Equal("info", line.GetProperty("level").GetString());
        var data = line.GetProperty("data").EnumerateObject().ToArray();
        Assert.Equal([.. StartStages.Order, "by", "missing"], data.Select(each => each.Name));
        Assert.Equal(120, line.GetProperty("data").GetProperty(StartStages.Main).GetInt64());
        Assert.Equal(first, line.GetProperty("data").GetProperty(StartStages.Browser).GetInt64());
        Assert.All(StartStages.Order, stage => Assert.True(line.GetProperty("data").GetProperty(stage).GetInt64() >= 120));
        Assert.Equal("page", line.GetProperty("data").GetProperty("by").GetString());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("data").GetProperty("missing").ValueKind);
    }

    /// <summary>The last stage marked writes the line by itself, without waiting for the stop.</summary>
    [Fact]
    public async Task The_last_stage_writes_the_line()
    {
        using var log = new MachineLog(_home, "desktop");
        var stages = new StartStages(before: 0) { Log = log };
        foreach (var stage in StartStages.Order.Where(each => each is not StartStages.Splash and not StartStages.Main)) stages.Mark(stage);
        Assert.Empty(Lines());

        Assert.True(await new SplashLift(stages).WaitAsync(log, TimeSpan.FromMilliseconds(10)).WaitAsync(Poll.Patience) is false);

        var line = Assert.Single(Lines(), each => each.GetProperty("event").GetString() == "start.stages");
        Assert.Equal("bound", line.GetProperty("data").GetProperty("by").GetString());
    }

    /// <summary>A stage that never comes (a pipe another loop's lease keeps) is named missing once the bound after the splash passes.</summary>
    [Fact]
    public async Task A_stage_that_never_comes_is_named_missing_after_the_bound()
    {
        using var log = new MachineLog(_home, "desktop");
        var stages = new StartStages(before: 0, bound: TimeSpan.FromMilliseconds(50)) { Log = log };
        foreach (var stage in StartStages.Order.Where(each => each is not StartStages.Pipe and not StartStages.Splash)) stages.Mark(stage);

        stages.Lifted("page");
        var deadline = DateTime.UtcNow + Poll.Patience;
        while (!Lines().Any(each => each.GetProperty("event").GetString() == "start.stages") && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        var line = Assert.Single(Lines(), each => each.GetProperty("event").GetString() == "start.stages");
        Assert.Equal(JsonValueKind.Null, line.GetProperty("data").GetProperty(StartStages.Pipe).ValueKind);
        Assert.Equal("pipe", line.GetProperty("data").GetProperty("missing").GetString());
        stages.Write();
        Assert.Single(Lines(), each => each.GetProperty("event").GetString() == "start.stages");
    }

    /// <summary>
    /// Held on the application's own source, as the splash's route is, since the window has no test project: each stage is
    /// marked exactly once across the desktop's sources, the stop writes what came, and the timer begins at the top of Main.
    /// </summary>
    [Fact]
    public void The_start_marks_every_stage_once()
    {
        var sources = new[] { "Daoris.Desktop.App", SourceFiles.ModulesProject }
            .SelectMany(project => SourceFiles.All(project, "*.cs"))
            .Select(File.ReadAllText)
            .ToArray();

        foreach (var stage in StartStages.Order.Where(each => each is not StartStages.Main and not StartStages.Splash))
        {
            var name = char.ToUpperInvariant(stage[0]) + stage[1..];
            var marks = sources.Sum(text => Regex.Matches(text, $@"\.Mark\(StartStages\.{name}\)").Count);
            Assert.True(marks == 1, $"{name} is marked {marks} times");
        }

        var program = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.App", "Program.cs"));
        var main = program[program.IndexOf("private static void Main(", StringComparison.Ordinal)..];
        Assert.True(main.IndexOf("StartStages.Begin()", StringComparison.Ordinal)
            < main.IndexOf("InstallHome.Establish(", StringComparison.Ordinal), "the timer begins after the home");
        Assert.Contains("stages.Write();", main);
        Assert.Contains("_stages?.Lifted(", File.ReadAllText(SourceFiles.Module("SplashLift.cs")));
    }
}
