using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The npm signature's three reads held between openings, over real processes: a folder of programs standing in for node and npm on the
/// path (each counts its runs in a file), so the reads never start the machine's own. A fingerprint that agrees hands the earlier
/// reading; a moved program, a moved configuration file or an age past the lifetime runs the read again. Fixtures under the
/// repository's gitignored <c>_fixtures/</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Collection(Alone.Name)]
public sealed class NpmToolchainMemoTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "npm-memo", Guid.NewGuid().ToString("N")[..8]);
    private string Programs => Path.Combine(_scratch, "programs");
    private string Counts => Path.Combine(Programs, "runs.txt");

    public void Dispose() => GitSeed.Discard(_scratch);

    private IReadOnlyDictionary<string, string> Environment()
    {
        var made = new SortedDictionary<string, string>(InstallEnvironment.Of(new FileRule(FileKind.Dependency, [])).ToDictionary(each => each.Key, each => each.Value), StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = $"{Programs};{System.Environment.GetEnvironmentVariable("SystemRoot")}{Path.DirectorySeparatorChar}System32",
            ["USERPROFILE"] = Path.Combine(_scratch, "user"),
        };
        return made;
    }

    private void Programme()
    {
        Directory.CreateDirectory(Path.Combine(Programs, "node_modules", "npm"));
        Directory.CreateDirectory(Path.Combine(_scratch, "user"));
        File.WriteAllText(Path.Combine(Programs, "node.cmd"), "@echo off\r\necho node>>\"%~dp0runs.txt\"\r\necho v24.0.0\r\n");
        File.WriteAllText(Path.Combine(Programs, "npm.cmd"), "@echo off\r\necho npm %1>>\"%~dp0runs.txt\"\r\nif \"%1\"==\"--version\" (echo 11.0.0) else (echo {\"cache\":\"here\"})\r\n");
        File.WriteAllText(Path.Combine(Programs, "node_modules", "npm", "package.json"), """{ "name": "npm", "version": "11.0.0" }""");
    }

    private string Tree(string name)
    {
        var tree = Path.Combine(_scratch, name);
        Directory.CreateDirectory(tree);
        return tree;
    }

    private int Runs() => File.Exists(Counts) ? File.ReadAllLines(Counts).Length : 0;

    private static async Task<IReadOnlyList<CommandRan>> ReadAllAsync(string tree, IReadOnlyDictionary<string, string> environment)
    {
        var reads = new ToolchainReads(Path.Combine(tree, "..", "home"), tree);
        var ran = new List<CommandRan>();
        foreach (var command in new[] { NpmToolchain.NodeVersion, NpmToolchain.NpmVersion, NpmToolchain.NpmConfig }) ran.Add(await reads.ReadAsync(command, environment, CancellationToken.None));
        return ran;
    }

    [Fact]
    public async Task A_second_reading_of_the_same_programs_and_configuration_starts_none_and_hands_the_same_output_for_another_tree()
    {
        if (!OperatingSystem.IsWindows()) return;
        Programme();
        var environment = Environment();
        var first = await ReadAllAsync(Tree("a"), environment);
        Assert.Equal(3, Runs());

        var second = await ReadAllAsync(Tree("b"), environment);

        Assert.Equal(3, Runs());
        Assert.Equal(first.Select(each => each.Output.Trim()), second.Select(each => each.Output.Trim()));
        Assert.Equal(["v24.0.0", "11.0.0"], second.Take(2).Select(each => each.Output.Trim()));
    }

    [Fact]
    public async Task A_moved_package_a_changed_project_file_or_a_reading_past_its_lifetime_runs_again_only_what_it_decides()
    {
        if (!OperatingSystem.IsWindows()) return;
        Programme();
        var environment = Environment();
        await ReadAllAsync(Tree("a"), environment);
        Assert.Equal(3, Runs());

        await File.WriteAllTextAsync(Path.Combine(Tree("b"), ".npmrc"), "registry=https://example.invalid/\n");
        await ReadAllAsync(Path.Combine(_scratch, "b"), environment);
        Assert.Equal(4, Runs());

        await File.WriteAllTextAsync(Path.Combine(Programs, "node_modules", "npm", "package.json"), """{ "name": "npm", "version": "11.0.1" }""");
        File.SetLastWriteTimeUtc(Path.Combine(Programs, "node_modules", "npm", "package.json"), new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ReadAllAsync(Tree("c"), environment);
        Assert.Equal(6, Runs());

        await ReadAllAsync(Tree("d"), environment);
        Assert.Equal(6, Runs());
        ToolchainReads.Memo.Older.Value = (long)ToolchainReads.Memo.Lifetime.TotalMilliseconds + 1000;
        await ReadAllAsync(Tree("e"), environment);
        Assert.Equal(9, Runs());
    }

    [Fact]
    public async Task A_read_that_fails_and_a_command_the_signature_does_not_name_are_never_held()
    {
        if (!OperatingSystem.IsWindows()) return;
        Programme();
        File.WriteAllText(Path.Combine(Programs, "node.cmd"), "@echo off\r\necho node>>\"%~dp0runs.txt\"\r\nexit /b 3\r\n");
        var environment = Environment();
        var reads = new ToolchainReads(Path.Combine(_scratch, "home"), Tree("a"));
        Assert.Equal(3, (await reads.ReadAsync(NpmToolchain.NodeVersion, environment, CancellationToken.None)).Code);
        await new ToolchainReads(Path.Combine(_scratch, "home"), Tree("b")).ReadAsync(NpmToolchain.NodeVersion, environment, CancellationToken.None);
        Assert.Equal(2, Runs());

        Assert.Null(NpmToolchain.Fingerprint("node read.cjs one", environment, Tree("c")));
        Assert.Null(NpmToolchain.Fingerprint(NpmToolchain.NpmConfig, new Dictionary<string, string> { ["PATH"] = Path.Combine(_scratch, "nothing") }, Tree("c")));
    }
}
