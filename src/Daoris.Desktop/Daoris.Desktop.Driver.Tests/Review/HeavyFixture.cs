using System.Text.Json;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository the heavy run's tests run in (HEAVYRUN1a): a checkout and a linked tree of it, laid out as git lays them out, with no
/// git run, so the slot reads them from git's own files as it reads a real one; a home of its own; and the gates each declares.
/// Scratch in the repository's gitignored <c>_fixtures/</c>, never OS temp.
/// </summary>
internal sealed class HeavyFixture : IDisposable
{
    public HeavyFixture(string name)
    {
        Folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", name, Guid.NewGuid().ToString("N")[..8]);
        Home = Path.Combine(Folder, "home");
        Checkout = Path.Combine(Folder, "engine");
        Tree = Path.Combine(Folder, "engine-tree");
        Other = Path.Combine(Folder, "game");

        Directory.CreateDirectory(Home);

        // The checkout's own git folder, and a linked tree's: its `.git` a file naming its folder under the checkout's, whose
        // `commondir` names the checkout's, as `git worktree add` writes them.
        Directory.CreateDirectory(Path.Combine(Checkout, ".git"));
        var linked = Path.Combine(Checkout, ".git", "worktrees", "engine-tree");
        Directory.CreateDirectory(linked);
        File.WriteAllText(Path.Combine(linked, "commondir"), "../..\n");
        Directory.CreateDirectory(Tree);
        File.WriteAllText(Path.Combine(Tree, ".git"), $"gitdir: {linked}\n");

        Directory.CreateDirectory(Path.Combine(Other, ".git"));
    }

    /// <summary>The scratch folder everything is in.</summary>
    public string Folder { get; }

    /// <summary>The home whose <c>locks/</c> holds the slot.</summary>
    public string Home { get; }

    /// <summary>The repository's root checkout.</summary>
    public string Checkout { get; }

    /// <summary>A linked tree of the same repository.</summary>
    public string Tree { get; }

    /// <summary>Another repository.</summary>
    public string Other { get; }

    /// <summary>Write a checkout's <c>daoris.gates.json</c>: each gate's name, command, and whether it is heavy (null: no key).</summary>
    public void Gates(string checkout, params (string Name, string Run, bool? Heavy)[] gates) =>
        File.WriteAllText(
            Path.Combine(checkout, "daoris.gates.json"),
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["devkit"] = "0.0.1",
                ["gates"] = gates.Select(gate =>
                {
                    var entry = new Dictionary<string, object> { ["name"] = gate.Name, ["run"] = gate.Run };
                    if (gate.Heavy is { } heavy) entry["heavy"] = heavy;
                    return entry;
                }).ToList(),
            }));

    /// <summary>
    /// A test runner installed under <paramref name="folder"/> as npm lays it out (HEAVYRUN1a2): its
    /// <c>node_modules/&lt;package&gt;/package.json</c>, naming <paramref name="version"/> where one is given.
    /// </summary>
    public void Installed(string folder, string package, string? version)
    {
        var at = Path.Combine(folder, "node_modules", package);
        Directory.CreateDirectory(at);
        var manifest = new Dictionary<string, object> { ["name"] = package };
        if (version is not null) manifest["version"] = version;
        File.WriteAllText(Path.Combine(at, "package.json"), JsonSerializer.Serialize(manifest));
    }

    /// <summary>Write <paramref name="folder"/>'s <c>package.json</c>, declaring each script by name (HEAVYRUN1a2).</summary>
    public void Scripts(string folder, params (string Name, string Run)[] scripts) => Managed(folder, null, scripts);

    /// <summary>
    /// Write <paramref name="folder"/>'s <c>package.json</c>, declaring each script by name and, where one is given, the
    /// <c>packageManager</c> corepack reads (HEAVYRUN1a3).
    /// </summary>
    public void Managed(string folder, string? packageManager, params (string Name, string Run)[] scripts)
    {
        Directory.CreateDirectory(folder);
        var manifest = new Dictionary<string, object>
        {
            ["name"] = "engine",
            ["version"] = "0.0.0",
            ["private"] = true,
            ["scripts"] = scripts.ToDictionary(script => script.Name, script => script.Run),
        };
        if (packageManager is not null) manifest["packageManager"] = packageManager;
        File.WriteAllText(Path.Combine(folder, "package.json"), JsonSerializer.Serialize(manifest));
    }

    /// <summary>Write a checkout's <c>daoris.gates.json</c>, each heavy gate run in its own <c>cwd</c> (HEAVYRUN1a3).</summary>
    public void GatesIn(string checkout, params (string Name, string Run, string Cwd)[] gates) =>
        File.WriteAllText(
            Path.Combine(checkout, "daoris.gates.json"),
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["devkit"] = "0.0.1",
                ["gates"] = gates.Select(gate => new Dictionary<string, object> { ["name"] = gate.Name, ["run"] = gate.Run, ["cwd"] = gate.Cwd, ["heavy"] = true }).ToList(),
            }));

    /// <summary>A file in the scratch folder, by name.</summary>
    public string At(string name) => Path.Combine(Folder, name);

    public void Dispose()
    {
        GitSeed.Discard(Folder);
    }
}

/// <summary>What a run said, written from its own task while a test reads it: one lock around both.</summary>
internal sealed class SaidWriter : TextWriter
{
    private readonly System.Text.StringBuilder _said = new();

    public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

    public string Text
    {
        get { lock (_said) return _said.ToString(); }
    }

    /// <summary>Each line said, trimmed, the empty ones left out.</summary>
    public IReadOnlyList<string> Lines => Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public override void Write(char value)
    {
        lock (_said) _said.Append(value);
    }

    public override void Write(string? value)
    {
        lock (_said) _said.Append(value);
    }

    public override void WriteLine(string? value)
    {
        lock (_said) _said.Append(value).Append('\n');
    }

    public override string ToString() => Text;
}
