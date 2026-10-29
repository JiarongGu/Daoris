using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 2 (D212; the foundation design §3.3, §13.4, §19.1, §10's row 1f-i): the move record's claim across real processes,
/// one process being one run of <c>store_runs</c>. Two shells on one home deciding a move of one line make one row and one claim, and the
/// second says once that another Daoris runs on its home; an executor paused after its effect's mark commits and before the effect keeps its
/// move against another process's take, is said <c>move.quiet</c> once past ten minutes and never taken over, and once really killed its
/// move is taken up at the next look and held, its effect never made.
/// </summary>
/// <remarks>
/// The other processes are a small console program this class builds once into <c>_fixtures/</c>, referencing the driver library these
/// tests run against and the store's two packages at the desktop's pins, so each child is a process of its own writing the store through
/// the driver's own code. The fast cases are <see cref="MoveTakeUpTests"/> and <see cref="MoveStoreTests"/>.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class MoveClaimProcessTests(MoveClaimProcessTests.Child child) : IClassFixture<MoveClaimProcessTests.Child>, IDisposable
{
    private readonly string _home = Path.Combine(child.Root, "homes", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<Process> _started = [];

    /// <summary>The child program, built once for the class into a folder of its own under <c>_fixtures/</c>.</summary>
    public sealed class Child : IDisposable
    {
        public string Root { get; } = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-claim-process", Guid.NewGuid().ToString("N")[..8]);

        private readonly Lazy<string> _program;

        public Child() => _program = new(() => Build(Root));

        public string Program => _program.Value;

        public void Dispose() => GitSeed.Discard(Root);
    }

    public void Dispose()
    {
        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Gone already.
            }

            process.Dispose();
        }

        SqliteConnection.ClearAllPools();
    }

    private const string Source =
        """
        using Daoris.Driver;

        var (verb, home, record) = (args[0], args[1], args[2]);
        StoreProcess.Kind = StoreProcess.Shell;
        var store = new MoveStore(home, () => StoreSettled.Yes);
        var unit = new MoveTakeUp(home, store, MoveTakeUpScope.Conversations);
        Console.Out.WriteLine($"run {store.Run} {Environment.ProcessId}");
        var decision = new MoveDecision(record, MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true };
        switch (verb)
        {
            case "decide":
                // A come-up, and a second one to hold that the other shell is said once.
                await unit.ComeUpAsync(_ => true, say: line => Console.Out.WriteLine("said " + line));
                await unit.ComeUpAsync(_ => true, say: line => Console.Out.WriteLine("said " + line));
                Console.Out.WriteLine("ready");
                Console.In.ReadLine();
                var decided = store.Decide(decision);
                Console.Out.WriteLine(decided.Claimed ? $"claimed {decided.Move.Id}" : $"standing {decided.Move.Id}");
                await unit.LookAsync(_ => true);
                Console.Out.WriteLine("done");
                Console.In.ReadLine();
                return 0;
            case "mark":
                var claim = store.Decide(decision).Claim!;
                store.Opening(claim, "opens-" + record);
                store.MarkEffect(claim, MoveEffects.Spawn);
                Console.Out.WriteLine($"marked {claim.Move}");
                // Paused after its mark commits and before its effect, until it is told to go on or killed.
                Console.In.ReadLine();
                File.WriteAllText(Path.Combine(home, "effect-made"), "made");
                store.EffectEnded(claim, "made");
                Console.Out.WriteLine("effect");
                return 0;
            default:
                return 2;
        }
        """;

    /// <summary>The child's project: the driver library these tests run against, and the store's two packages at the desktop's pins.</summary>
    private static string Project()
    {
        var pins = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Directory.Packages.props"));
        string Pin(string package) => Regex.Match(pins, $"""<PackageVersion Include="{Regex.Escape(package)}" Version="([^"]+)" />""").Groups[1].Value;
        var driver = typeof(MoveStore).Assembly.Location;
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>move-claim</AssemblyName>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Data.Sqlite" Version="{Pin("Microsoft.Data.Sqlite")}" />
                <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="{Pin("SQLitePCLRaw.bundle_e_sqlite3")}" />
                <Reference Include="Daoris.Desktop.Driver"><HintPath>{driver}</HintPath></Reference>
              </ItemGroup>
            </Project>
            """;
    }

    /// <summary>The child's program, built into <paramref name="root"/>.</summary>
    private static string Build(string root)
    {
        var source = Path.Combine(root, "fixture");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "MoveClaim.csproj"), Project());
        File.WriteAllText(Path.Combine(source, "Program.cs"), Source);
        // Nothing above this folder is the fixture's to read.
        File.WriteAllText(Path.Combine(source, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(source, "Directory.Packages.props"), "<Project />");
        var output = Path.Combine(root, "built");
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var word in new[] { "build", Path.Combine(source, "MoveClaim.csproj"), "-nodeReuse:false", "-v", "q", "-o", output }) info.ArgumentList.Add(word);
        using var build = Process.Start(info)!;
        var said = build.StandardOutput.ReadToEndAsync();
        var error = build.StandardError.ReadToEndAsync();
        Assert.True(build.WaitForExit(Poll.Patience), "the child's build did not end");
        Assert.True(build.ExitCode == 0, said.Result + error.Result);
        var program = Path.Combine(output, OperatingSystem.IsWindows() ? "move-claim.exe" : "move-claim");
        Assert.True(File.Exists(program), $"the child's build made no program: {said.Result}");
        return program;
    }

    private Process Start(string verb, string record)
    {
        var info = new ProcessStartInfo(child.Program)
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var word in new[] { verb, _home, record }) info.ArgumentList.Add(word);
        var process = Process.Start(info)!;
        _started.Add(process);
        return process;
    }

    /// <summary>The child's next line, within the suite's patience.</summary>
    private static async Task<string> Next(Process process)
    {
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(Poll.Patience);
        Assert.True(line is not null, $"the child ended: {await process.StandardError.ReadToEndAsync()}");
        return line!;
    }

    /// <summary>The lines the child says before <paramref name="mark"/>.</summary>
    private static async Task<List<string>> Until(Process process, string mark)
    {
        var lines = new List<string>();
        for (var line = await Next(process); line != mark; line = await Next(process)) lines.Add(line);
        return lines;
    }

    private int LogLines(string @event) =>
        Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl")
            .SelectMany(File.ReadAllLines)
            .Count(line => line.Contains($"\"event\":\"{@event}\"", StringComparison.Ordinal));

    /// <summary>The system's clock, moved on by the case: a phase written by the child at its real moment stands as long as the case says.</summary>
    private sealed class Later : TimeProvider
    {
        public TimeSpan By { get; set; }

        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + By;
    }

    [Fact]
    public async Task Two_shells_on_one_home_deciding_a_move_of_one_line_make_one_row_and_one_claim_and_the_second_is_said_once()
    {
        Directory.CreateDirectory(_home);
        var first = Start("decide", "s1");
        var firstRun = (await Next(first)).Split(' ');
        var firstSaid = await Until(first, "ready");
        var second = Start("decide", "s1");
        var secondRun = (await Next(second)).Split(' ');
        var secondSaid = await Until(second, "ready");

        // Both decide at once.
        await Task.WhenAll(first.StandardInput.WriteLineAsync("go"), second.StandardInput.WriteLineAsync("go"));
        await first.StandardInput.FlushAsync();
        await second.StandardInput.FlushAsync();
        var decisions = new[] { await Next(first), await Next(second) };
        Assert.Empty(await Until(first, "done"));
        Assert.Empty(await Until(second, "done"));

        Assert.Empty(firstSaid);
        var said = Assert.Single(secondSaid);
        Assert.StartsWith("said Another Daoris runs on this home", said);
        Assert.Contains($"process {firstRun[2]}", said);
        Assert.Single(decisions, line => line.StartsWith("claimed ", StringComparison.Ordinal));
        Assert.Single(decisions, line => line.StartsWith("standing ", StringComparison.Ordinal));
        Assert.Single(decisions.Select(line => line.Split(' ')[1]).Distinct());
        var claimer = decisions[0].StartsWith("claimed ", StringComparison.Ordinal) ? firstRun[1] : secondRun[1];

        var store = new MoveStore(_home, () => StoreSettled.Yes, run: "probe");
        var move = Assert.Single(store.Of("s1"));
        Assert.Equal((claimer, 1L), (move.ClaimRun, move.ClaimGeneration));
        Assert.Equal(
            [(firstRun[1], StoreProcess.Shell, int.Parse(firstRun[2])), (secondRun[1], StoreProcess.Shell, int.Parse(secondRun[2]))],
            store.Runs().Select(run => (run.Run, run.Kind, run.Pid)).OrderBy(run => run.Run == secondRun[1]));
        Assert.Equal(1, LogLines("home.another-shell"));

        first.StandardInput.Close();
        second.StandardInput.Close();
        await Task.WhenAll(first.WaitForExitAsync(), second.WaitForExitAsync()).WaitAsync(Poll.Patience);
    }

    [Fact]
    public async Task An_executor_paused_between_its_mark_and_its_effect_keeps_its_move_is_said_quiet_once_and_once_killed_is_taken_up_and_held()
    {
        Directory.CreateDirectory(_home);
        var executor = Start("mark", "s1");
        var run = (await Next(executor)).Split(' ');
        var marked = long.Parse((await Next(executor)).Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
        var notes = new List<(string Session, Noted Note)>();
        var later = new Later();
        // This process, another run on the home, reading liveness by the lease's own test.
        var store = new MoveStore(_home, () => StoreSettled.Yes, run: "taker-" + Guid.NewGuid().ToString("N")[..8], kind: StoreProcess.Shell, clock: later);
        var unit = new MoveTakeUp(_home, store, MoveTakeUpScope.Conversations, note: (session, note) => notes.Add((session, note)), clock: later);

        // Another process tries to take it while the executor lives, paused: refused, and the look takes nothing.
        Assert.Equal(MoveTakeRefusals.Live, store.Take(marked, 1).Refused);
        var live = await unit.LookAsync(_ => true);
        Assert.Empty(live.Taken);
        Assert.Empty(live.Quiet);

        // Past ten minutes it is said once, on its line and in the log with its run's kind and pid, and still not taken.
        later.By = TimeSpan.FromMinutes(11);
        var quiet = await unit.LookAsync(_ => true);
        Assert.Equal([marked], quiet.Quiet.Select(move => move.Id));
        Assert.Empty(quiet.Taken);
        Assert.Equal("s1", Assert.Single(notes).Session);
        Assert.Empty((await unit.LookAsync(_ => true)).Quiet);
        Assert.Single(notes);
        Assert.Equal(1, LogLines("move.quiet"));
        Assert.Contains(
            File.ReadAllLines(Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").Single()),
            line => line.Contains("\"event\":\"move.quiet\"", StringComparison.Ordinal) && line.Contains($"\"pid\":{run[2]}", StringComparison.Ordinal)
                    && line.Contains("\"kind\":\"shell\"", StringComparison.Ordinal) && line.Contains("\"phase\":\"opening\"", StringComparison.Ordinal));
        Assert.Equal((run[1], 1L), (store.Read(marked)!.ClaimRun, store.Read(marked)!.ClaimGeneration));

        // Killed between its mark and its effect: the next look takes it up and holds it, the effect never made.
        executor.Kill(entireProcessTree: true);
        await executor.WaitForExitAsync().WaitAsync(Poll.Patience);
        var after = await unit.LookAsync(_ => true);

        var held = Assert.Single(after.Held);
        Assert.Equal(
            (marked, (MoveRecordPhase?)MoveRecordPhase.Held, (string?)MoveHoldWhy.Unrecorded, store.Run, 2L),
            (held.Id, held.Phase, held.HeldWhy, held.ClaimRun, held.ClaimGeneration));
        Assert.Null(held.Effect!.Outcome);
        Assert.False(File.Exists(Path.Combine(_home, "effect-made")));
        Assert.Empty((await unit.LookAsync(_ => true)).Held);
    }
}
