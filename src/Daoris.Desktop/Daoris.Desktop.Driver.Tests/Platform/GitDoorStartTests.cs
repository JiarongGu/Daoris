using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 z-i's measurement of the git door's start with the desktop store (D212, the design §19.10, §13.6's row z-i, C5-1.10): a
/// door's start on a remote act, with <c>Microsoft.Data.Sqlite</c> opening the store read-only, against today's door, cold and warm, with no
/// store, beside a quiet store, beside a writer holding the write lock, beside a writer committing as fast as it can, and beside a
/// write-ahead log's checkpoints; the limit a tenth at p95. The door takes no package in this row and no family it reads moves, so this
/// is a measurement, never a gate: it asserts that every start answered as the door answers and that the store was read, and writes the
/// numbers for the note of D212; the row that first moves a family the door reads runs it again and holds the limit then.
/// </summary>
/// <remarks>
/// <para><b>What is started.</b> Two programs built here from the door's own sources, in scratch under <c>_fixtures/</c>: <i>today's</i>,
/// the files <c>Daoris.Desktop.GitDoor.csproj</c> compiles and nothing else, and <i>with the store</i>, the same files with
/// <c>DesktopStoreReader</c> and the package beside them, whose module initializer opens the store read-only and reads its version, its
/// revisions and a family's table before <c>Main</c> runs, which is where a door would read its families. Both are built in
/// <c>Release</c>, framework-dependent as the door is, named <c>daoris-git-ssh</c> as the door is, and started as git starts it, with no
/// window, one remote act (a push, which the door refuses with one line and no ssh, having read what it judges by) and the door's own
/// home variable naming a scratch home; this process's <c>DAORIS_HOME</c> is never set and the child's is cleared. No agent, window or
/// console is started.</para>
/// <para><b>Cold</b> is a start of a freshly laid-out copy of the program, five copies each: the files are new to the machine's cache and
/// its scanners, as an install's first start after an update is; the machine's own file cache cannot be dropped from here. <b>Warm</b> is
/// the next starts of one copy, <c>DAORIS_MEASURE_STARTS</c> of them (40 by default, the figures in D212's note are 100).</para>
/// <para><b>Held to where it is asked for.</b> The programs are built with the SDK, which takes minutes, so the cases run only where
/// <c>DAORIS_MEASURE_GITDOOR</c> is set, as <see cref="GitSshHoldsProcessTests"/> runs only where an sshd is named; where it is not set each
/// case holds nothing. In the Process half, alone: it measures a wall clock the load of a neighbour would move.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Collection(Alone.Name)]
public sealed class GitDoorStartTests : IDisposable
{
    private const string Variable = "DAORIS_MEASURE_GITDOOR";

    private static readonly string[] Act = ["git@example.invalid", "git-receive-pack 'org/engine.git'"];

    private readonly ITestOutputHelper _said;

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-door-start", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _stores;

    public GitDoorStartTests(ITestOutputHelper said)
    {
        _said = said;
        _stores = Path.Combine(_root, "stores");
        Directory.CreateDirectory(_stores);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static bool Asked => Environment.GetEnvironmentVariable(Variable) is { Length: > 0 };

    private static int Starts =>
        int.TryParse(Environment.GetEnvironmentVariable("DAORIS_MEASURE_STARTS"), CultureInfo.InvariantCulture, out var asked) && asked > 0 ? asked : 40;

    private const int ColdLayouts = 5;

    // ——— The two programs.

    private static string DoorProject => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.GitDoor");

    private static string DriverFolder => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");

    /// <summary>The door's own compiled files, read from its project: each <c>Include</c> and <c>Link</c>, and its <c>Program.cs</c>.</summary>
    internal static IReadOnlyList<(string File, string Link)> DoorSources()
    {
        var project = File.ReadAllText(Path.Combine(DoorProject, "Daoris.Desktop.GitDoor.csproj"));
        var sources = new List<(string, string)> { (Path.Combine(DoorProject, "Program.cs"), "Program.cs") };
        foreach (Match each in Regex.Matches(project, """<Compile Include="([^"]+)" Link="([^"]+)" />"""))
        {
            sources.Add((Path.GetFullPath(Path.Combine(DoorProject, each.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar))), each.Groups[2].Value));
        }

        return sources;
    }

    private static string Pinned(string package)
    {
        var props = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Directory.Packages.props"));
        return Regex.Match(props, $"""<PackageVersion Include="{Regex.Escape(package)}" Version="([^"]+)" />""").Groups[1].Value;
    }

    private const string Initializer = """
        using System.Runtime.CompilerServices;
        using Daoris.Driver;

        internal static class StoreOpenedAtStart
        {
            [ModuleInitializer]
            internal static void Start()
            {
                if (Environment.GetEnvironmentVariable("DAORIS_PROBE_STORE") is { Length: > 0 } path) Read(path);
            }

            // Its own method, so a program that opens no store never loads the package: the JIT reads this body only when it is called.
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void Read(string path)
            {
                using var store = DesktopStoreReader.Open(path);
                if (store is null) return;
                _ = store.Version;
                _ = store.Revisions();
                _ = store.Query("SELECT COUNT(*) FROM imports", row => row.GetInt64(0));
                Console.Error.WriteLine("store read: version " + store.Version);
            }
        }
        """;

    /// <summary>One program built from the door's sources, with the store's reader and its package where <paramref name="withStore"/>.</summary>
    private async Task<string> BuildAsync(string name, bool withStore)
    {
        var folder = Path.Combine(_root, name);
        Directory.CreateDirectory(folder);
        var compile = new StringBuilder();
        foreach (var (file, link) in DoorSources()) compile.AppendLine($"""    <Compile Include="{file}" Link="{link}" />""");
        var packages = "";
        if (withStore)
        {
            compile.AppendLine($"""    <Compile Include="{Path.Combine(DriverFolder, "Store", "DesktopStoreReader.cs")}" Link="DesktopStoreReader.cs" />""");
            packages = $"""
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Data.Sqlite" Version="{Pinned("Microsoft.Data.Sqlite")}" />
                    <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="{Pinned("SQLitePCLRaw.bundle_e_sqlite3")}" />
                  </ItemGroup>
                """;
            // Found beside the project, as the SDK finds every source of a folder.
            await File.WriteAllTextAsync(Path.Combine(folder, "StoreOpenedAtStart.cs"), Initializer);
        }

        // The door's own properties (Daoris.Desktop.GitDoor.csproj), and the desktop tree's (Directory.Build.props) the door inherits.
        await File.WriteAllTextAsync(Path.Combine(folder, name + ".csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>latest</LangVersion>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <CodePage>65001</CodePage>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
                <RootNamespace>Daoris.Desktop.GitDoor</RootNamespace>
                <AssemblyName>daoris-git-ssh</AssemblyName>
                <InvariantGlobalization>true</InvariantGlobalization>
                <UseSystemResourceKeys>true</UseSystemResourceKeys>
              </PropertyGroup>
              <ItemGroup>
            {compile}  </ItemGroup>
            {packages}</Project>
            """);

        var output = Path.Combine(folder, "out");
        var built = await RunAsync("dotnet", ["build", Path.Combine(folder, name + ".csproj"), "-c", "Release", "-o", output, "--nologo", "-v", "q", "-p:UseAppHost=true"], timeout: TimeSpan.FromMinutes(8));
        Assert.True(built.Exit == 0, $"{name} did not build:\n{built.Out}\n{built.Err}");
        var exe = Path.Combine(output, "daoris-git-ssh.exe");
        Assert.True(File.Exists(exe), exe);
        return output;
    }

    private static async Task<(int Exit, string Out, string Err, TimeSpan Took)> RunAsync(string file, IEnumerable<string> arguments, IDictionary<string, string?>? environment = null, TimeSpan? timeout = null)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        // The person's own home is never this measurement's.
        info.Environment.Remove("DAORIS_HOME");
        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                if (value is null) info.Environment.Remove(name);
                else info.Environment[name] = value;
            }
        }

        var clock = Stopwatch.StartNew();
        using var process = Process.Start(info)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var cancel = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(1));
        try
        {
            await process.WaitForExitAsync(cancel.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        var took = clock.Elapsed;
        return (process.ExitCode, await output, await error, took);
    }

    // ——— The cases.

    /// <summary>What is going on in the store while a case's starts are made.</summary>
    private sealed record Case(string Name, string Store, Func<string, IDisposable> Beside);

    private sealed class Busy : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();

        private readonly Task _work;

        public Busy(Action<CancellationToken> work) => _work = Task.Run(() => work(_stop.Token));

        public void Dispose()
        {
            _stop.Cancel();
            _work.GetAwaiter().GetResult();
            _stop.Dispose();
        }
    }

    private static IDisposable Nothing(string path) => new Busy(_ => { });

    /// <summary>A writer that takes the write lock and holds it, its rows uncommitted, for as long as the case runs.</summary>
    private static IDisposable HoldingTheLock(string path) => new Busy(stop =>
    {
        using var store = DesktopStore.Open(path, new DesktopStoreOptions { Kind = "shell", Settled = () => StoreSettled.Yes });
        using var raw = new SqliteConnection($"Data Source={path};Pooling=False");
        raw.Open();
        Run(raw, "BEGIN IMMEDIATE");
        Run(raw, "INSERT INTO store_revisions (family, revision) VALUES ('held', 1) ON CONFLICT (family) DO UPDATE SET revision = revision + 1");
        stop.WaitHandle.WaitOne();
        Run(raw, "ROLLBACK");
    });

    /// <summary>A writer committing as fast as it can, each commit synchronous FULL.</summary>
    private static IDisposable Committing(string path) => new Busy(stop =>
    {
        using var store = DesktopStore.Open(path, new DesktopStoreOptions { Kind = "host", Settled = () => StoreSettled.Yes });
        while (!stop.IsCancellationRequested) store.Write("busy", write => write.Execute("INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('busy', 'p', 1, 'x', 't', 'b', 0, 'left')"));
    });

    /// <summary>A writer committing and then checkpointing the write-ahead log, restarting it, over and over.</summary>
    private static IDisposable Checkpointing(string path) => new Busy(stop =>
    {
        using var store = DesktopStore.Open(path, new DesktopStoreOptions { Kind = "host", Settled = () => StoreSettled.Yes });
        using var raw = new SqliteConnection($"Data Source={path};Pooling=False");
        raw.Open();
        while (!stop.IsCancellationRequested)
        {
            for (var each = 0; each < 20; each++) store.Write("busy", write => write.Execute("INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('busy', 'p', 1, 'x', 't', 'b', 0, 'left')"));
            Run(raw, "PRAGMA wal_checkpoint(TRUNCATE)");
        }
    });

    private static void Run(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private string MakeStore(string name)
    {
        var path = Path.Combine(_stores, name, "data", "desktop.db");
        using var store = DesktopStore.Open(path, new DesktopStoreOptions { Kind = "shell", Settled = () => StoreSettled.Yes });
        // A store of a size a door's families would make: a few hundred rows over the store's own tables, the log left to the cases.
        for (var each = 0; each < 200; each++)
        {
            store.Write("seed", write => write.Execute("INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('seed', 'p', 1, 'x', 't', 'b', 0, 'left')"));
        }

        return path;
    }

    private static double Percentile(IReadOnlyList<double> sorted, double p) =>
        sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];

    private sealed record Sample(string Case, string Variant, double[] Cold, double[] Warm)
    {
        public double WarmP50 => Percentile([.. Warm.Order()], 0.5);

        public double WarmP95 => Percentile([.. Warm.Order()], 0.95);

        public double ColdMedian => Percentile([.. Cold.Order()], 0.5);

        public double ColdMax => Cold.Max();
    }

    /// <summary>
    /// Both programs' starts in one case, each pair made one after the other with the order alternating, so a load that comes and goes
    /// on this machine falls on both alike: the cold layouts first, then the warm starts.
    /// </summary>
    private async Task<(Sample Today, Sample WithStore)> MeasureAsync(string label, string today, string withStore, string store, string home)
    {
        var read = File.Exists(store);   // a store that is not there is read as none, and the program says nothing of it
        var todayEnvironment = new Dictionary<string, string?> { ["DAORIS_GIT_HOME"] = home, ["DAORIS_PROBE_STORE"] = null };
        var storeEnvironment = new Dictionary<string, string?> { ["DAORIS_GIT_HOME"] = home, ["DAORIS_PROBE_STORE"] = store };
        var coldToday = new List<double>();
        var coldStore = new List<double>();
        for (var layout = 0; layout < ColdLayouts; layout++)
        {
            var one = Path.Combine(_root, "layouts", $"{label}-today-{layout}");
            var two = Path.Combine(_root, "layouts", $"{label}-store-{layout}");
            CopyFolder(today, one);
            CopyFolder(withStore, two);
            await PairAsync(layout, one, two, todayEnvironment, storeEnvironment, read, coldToday, coldStore);
        }

        var warmOne = Path.Combine(_root, "layouts", $"{label}-today-warm");
        var warmTwo = Path.Combine(_root, "layouts", $"{label}-store-warm");
        CopyFolder(today, warmOne);
        CopyFolder(withStore, warmTwo);
        var warmToday = new List<double>();
        var warmStore = new List<double>();
        for (var each = 0; each < Starts; each++) await PairAsync(each, warmOne, warmTwo, todayEnvironment, storeEnvironment, read, warmToday, warmStore);
        return (new Sample(label, "today", [.. coldToday], [.. warmToday]), new Sample(label, "with store", [.. coldStore], [.. warmStore]));
    }

    private static async Task PairAsync(
        int turn, string today, string withStore, IDictionary<string, string?> todayEnvironment, IDictionary<string, string?> storeEnvironment, bool read,
        List<double> todayTimes, List<double> storeTimes)
    {
        var door = Path.Combine(today, "daoris-git-ssh.exe");
        var store = Path.Combine(withStore, "daoris-git-ssh.exe");
        if (turn % 2 == 0)
        {
            todayTimes.Add(await StartAsync(door, todayEnvironment, false));
            storeTimes.Add(await StartAsync(store, storeEnvironment, read));
        }
        else
        {
            storeTimes.Add(await StartAsync(store, storeEnvironment, read));
            todayTimes.Add(await StartAsync(door, todayEnvironment, false));
        }
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>One start of a door on its one act: its wall time in milliseconds, after it has answered as the door answers.</summary>
    private static async Task<double> StartAsync(string exe, IDictionary<string, string?> environment, bool expectStore)
    {
        var started = await RunAsync(exe, Act, environment);

        Assert.Equal(128, started.Exit);   // the push refused at the door, which starts no ssh
        Assert.Contains("refuses a push", started.Err);
        Assert.Equal(expectStore, started.Err.Contains("store read: version 1", StringComparison.Ordinal));
        return started.Took.TotalMilliseconds;
    }

    [Fact]
    public async Task A_door_s_start_with_the_store_opened_read_only_is_measured_against_today_s_cold_and_warm_beside_a_busy_writer_and_a_checkpoint()
    {
        if (!Asked) return;

        var today = await BuildAsync("door-today", withStore: false);
        var withStore = await BuildAsync("door-with-store", withStore: true);
        var home = Path.Combine(_root, "home");
        Directory.CreateDirectory(home);

        Case[] cases =
        [
            new("no store", Path.Combine(_stores, "absent", "data", "desktop.db"), Nothing),
            new("quiet store", MakeStore("quiet"), Nothing),
            new("writer holding the lock", MakeStore("locked"), HoldingTheLock),
            new("writer committing", MakeStore("committing"), Committing),
            new("writer checkpointing", MakeStore("checkpointing"), Checkpointing),
        ];

        var samples = new List<Sample>();
        foreach (var each in cases)
        {
            using var beside = each.Beside(each.Store);
            var (before, after) = await MeasureAsync(each.Name, today, withStore, each.Store, home);
            samples.Add(before);
            samples.Add(after);
        }

        var report = Report(samples);
        _said.WriteLine(report);
        var last = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-door-start-last.txt");
        await File.WriteAllTextAsync(last, report);
    }

    private static string Report(IReadOnlyList<Sample> samples)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"git door start, wall ms, {Starts} warm starts and {ColdLayouts} cold layouts per cell, one push refused at the door");
        text.AppendLine("case | variant | cold median | cold max | warm p50 | warm p95 | p50 vs today | p95 vs today | paired median delta");
        foreach (var group in samples.GroupBy(sample => sample.Case))
        {
            var today = group.First(sample => sample.Variant == "today");
            foreach (var sample in group)
            {
                var ratio = sample.WarmP95 / today.WarmP95;
                var same = sample.Variant == "today";
                var delta = sample.Warm.Zip(today.Warm, (own, base_) => own - base_).Order().ToArray();
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"{sample.Case} | {sample.Variant} | {sample.ColdMedian:F0} | {sample.ColdMax:F0} | {sample.WarmP50:F0} | {sample.WarmP95:F0} | "
                    + $"{(same ? "-" : (sample.WarmP50 / today.WarmP50).ToString("P1", CultureInfo.InvariantCulture))} | {(same ? "-" : ratio.ToString("P1", CultureInfo.InvariantCulture))} | "
                    + $"{(same ? "-" : Percentile(delta, 0.5).ToString("F0", CultureInfo.InvariantCulture) + " ms")}");
            }
        }

        return text.ToString();
    }
}
