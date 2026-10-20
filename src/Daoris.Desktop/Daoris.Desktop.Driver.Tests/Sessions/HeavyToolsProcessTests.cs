using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYRUN1a3's proof (D180's HEAVYRUN1a3 note): a gate run through <c>yarn test</c> or <c>pnpm test</c> hands the tool the bound in
/// the form the version its folder records takes. Each tool is a stub first on the PATH the gates inherit, recording the words it was
/// handed in the folder it ran in; how each version hands them on to the script was read from its source.
/// </summary>
/// <remarks>
/// Real processes: the Process half's. The shell is not asked to look in a gate's own folder (a machine may set
/// <c>NoDefaultCurrentDirectoryInExePath</c>), so the stubs go on this process's PATH, which keeps the class in
/// <see cref="ProcessPath"/>'s collection.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Collection(ProcessPath.Name)]
public sealed class HeavyToolsProcessTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("heavy-tools");

    public void Dispose() => _fixture.Dispose();

    /// <summary>yarn 1 is handed the flag after the script's name, pnpm 9 after its <c>--</c>, pnpm 10 after the name.</summary>
    [Fact]
    public async Task A_gate_s_runner_is_handed_two_workers_through_yarn_and_pnpm()
    {
        if (!OperatingSystem.IsWindows()) return;

        File.WriteAllText(_fixture.At("argv.mjs"), "import { writeFileSync } from 'node:fs';\nwriteFileSync('handed.json', JSON.stringify(process.argv.slice(2)));\n");
        var bin = _fixture.At("bin");
        Directory.CreateDirectory(bin);
        foreach (var tool in new[] { "yarn", "pnpm" }) File.WriteAllText(Path.Combine(bin, $"{tool}.cmd"), $"@node \"{_fixture.At("argv.mjs")}\" %*\r\n");
        _fixture.Installed(_fixture.Checkout, "jest", "29.7.0");
        foreach (var (folder, manager) in new[] { ("classic", "yarn@1.22.22"), ("pnpm9", "pnpm@9.15.9"), ("pnpm10", "pnpm@10.34.7") })
        {
            _fixture.Managed(Path.Combine(_fixture.Checkout, folder), manager, ("test", "jest --ci"));
        }

        _fixture.GatesIn(_fixture.Checkout, ("classic", "yarn test", "classic"), ("pnpm9", "pnpm test", "pnpm9"), ("pnpm10", "pnpm test", "pnpm10"));
        var said = new SaidWriter();
        var world = new HeavyRunWorld(_fixture.Home, _fixture.Checkout, _ => null)
        {
            Waiting = new HeavyWaiting(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30), () => DateTimeOffset.UtcNow),
        };

        var path = Environment.GetEnvironmentVariable("PATH");
        int exit;
        try
        {
            // Prepended, so every other tool stays where it was.
            Environment.SetEnvironmentVariable("PATH", bin + Path.PathSeparator + path);
            exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), world, said, CancellationToken.None).WaitAsync(Poll.Patience);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", path);
        }

        Assert.True(exit == 0, said.Text);
        Assert.Equal(["test", "--maxWorkers=2"], Handed("classic"));
        Assert.Equal(["test", "--", "--maxWorkers=2"], Handed("pnpm9"));
        Assert.Equal(["test", "--maxWorkers=2"], Handed("pnpm10"));
        Assert.Contains("run-heavy: `classic`: yarn test --maxWorkers=2", said.Lines);
        Assert.Contains("run-heavy: `classic` runs Jest 29.7.0 through yarn 1.22.22's `test` script, so its workers are bounded to two (`--maxWorkers=2`).", said.Lines);
        Assert.Contains("run-heavy: `pnpm9`: pnpm test -- --maxWorkers=2", said.Lines);
        Assert.Contains("run-heavy: `pnpm10`: pnpm test --maxWorkers=2", said.Lines);
    }

    private string[] Handed(string folder) =>
        JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(_fixture.Checkout, folder, "handed.json"))) ?? [];
}
