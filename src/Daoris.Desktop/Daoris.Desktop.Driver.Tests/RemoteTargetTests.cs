using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The machine's remotes are a MAP — workspace → { url, key } — in one file under the profile, with
/// the environment overriding (D48 §5); absence is the default, silently (D21), so a machine with no
/// remote never has to opt out of one.
/// </summary>
/// <remarks>
/// This table is the twin of the service's `RemoteConfigTests`. The two loaders are deliberate
/// duplicates — this assembly links against no service code — so the tables move together, and a rule
/// that appears in one only is a rule the other will contradict.
/// </remarks>
public sealed class RemoteTargetTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "daoris-remote-" + Guid.NewGuid().ToString("N")[..8]);

    private string ConfigPath => Path.Combine(_dir, "remotes.json");

    public RemoteTargetTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static Func<string, string?> Env(string? url = null, string? key = null, string? workspace = null) =>
        name => name == RemoteTarget.UrlVariable ? url
            : name == RemoteTarget.KeyVariable ? key
            : name == RemoteTarget.WorkspaceVariable ? workspace
            : null;

    [Fact]
    public void Absence_is_silent()
    {
        Assert.Empty(RemoteTarget.Load(Env(), ConfigPath));
    }

    [Fact]
    public void The_file_names_a_remote_per_workspace()
    {
        File.WriteAllText(ConfigPath, """
            {
              "aurora": { "url": "https://aurora.example.com/", "key": "dk_aurorakey" },
              "tools":  { "url": "https://tools.example.com",   "key": "dk_toolskey" }
            }
            """);

        var remotes = RemoteTarget.Load(Env(), ConfigPath);

        Assert.Equal(2, remotes.Count);
        Assert.Equal("https://aurora.example.com", remotes["aurora"].Url);
        Assert.Equal("dk_aurorakey", remotes["aurora"].Key);
        Assert.Equal("https://tools.example.com", remotes["tools"].Url);
        Assert.True(remotes.ContainsKey("AURORA"));
    }

    /// <summary>
    /// The environment is the answer for the WHOLE MACHINE, not one entry of it. A merge would let a
    /// developer's real map leak into a process that thought it had named its only remote — which is
    /// exactly what the family rehearsal's hermetic guard relies on not happening.
    /// </summary>
    [Fact]
    public void The_environment_pair_replaces_the_file_whole_and_names_its_workspace()
    {
        File.WriteAllText(ConfigPath, """{ "aurora": { "url": "https://aurora.example.com", "key": "dk_filekey" } }""");

        var overridden = RemoteTarget.Load(Env("https://env.example.com/", "dk_envkey", "tools"), ConfigPath);

        Assert.Equal(["tools"], overridden.Keys);
        Assert.Equal("https://env.example.com", overridden["tools"].Url);
        Assert.Equal("dk_envkey", overridden["tools"].Key);
    }

    [Fact]
    public void An_unnamed_environment_pair_serves_the_default_workspace()
    {
        var remotes = RemoteTarget.Load(Env("https://env.example.com", "dk_envkey"), ConfigPath);

        Assert.Equal([RemoteTarget.DefaultWorkspace], remotes.Keys);
    }

    [Fact]
    public void A_half_declared_remote_is_no_remote()
    {
        File.WriteAllText(ConfigPath, """{ "aurora": { "url": "https://aurora.example.com", "key": "dk_filekey" } }""");
        Assert.Empty(RemoteTarget.Load(Env(url: "https://env.example.com"), ConfigPath));
        Assert.Empty(RemoteTarget.Load(Env(key: "dk_envkey"), ConfigPath));

        Assert.Empty(RemoteTarget.Load(Env(url: "https://env.example.com"), Path.Combine(_dir, "absent.json")));
    }

    [Fact]
    public void An_entry_missing_half_its_pair_is_skipped_and_the_rest_still_load()
    {
        File.WriteAllText(ConfigPath, """
            {
              "aurora": { "url": "https://aurora.example.com", "key": "dk_aurorakey" },
              "tools":  { "url": "https://tools.example.com" },
              "empty":  {}
            }
            """);

        Assert.Equal(["aurora"], RemoteTarget.Load(Env(), ConfigPath).Keys);
    }

    [Fact]
    public void A_malformed_file_is_no_remote_rather_than_a_crash()
    {
        File.WriteAllText(ConfigPath, "{ not json");
        Assert.Empty(RemoteTarget.Load(Env(), ConfigPath));
    }

    /// <summary>The pre-workspace flat shape names no remote — rebuilt, never migrated (D48 §5).</summary>
    [Fact]
    public void The_pre_workspace_flat_shape_names_no_remote()
    {
        File.WriteAllText(ConfigPath, """{ "url": "https://daoris.example.com", "key": "dk_filekey" }""");

        Assert.Empty(RemoteTarget.Load(Env(), ConfigPath));
    }

    /// <summary>
    /// What the shell's settings surface writes, read back by the loaders — the file IS the contract
    /// between them (D50), so a write the readers cannot read is the one failure that would look fine
    /// from both sides of the screen.
    /// </summary>
    [Fact]
    public void What_the_surface_writes_is_what_every_loader_reads()
    {
        RemoteTarget.Save(ConfigPath, new Dictionary<string, RemoteTarget>
        {
            ["tools"] = new("https://tools.example.com", "dk_toolskey0000"),
            ["aurora"] = new("https://aurora.example.com", "dk_aurorakey000"),
        });

        var reread = RemoteTarget.Load(Env(), ConfigPath);

        Assert.Equal(2, reread.Count);
        Assert.Equal("https://aurora.example.com", reread["aurora"].Url);
        Assert.Equal("dk_toolskey0000", reread["tools"].Key);
        // Sorted and newline-terminated: this file is edited by hand as often as by a surface, and a
        // rewrite that reshuffled it would make every edit look like a bigger change than it was.
        var text = File.ReadAllText(ConfigPath);
        Assert.True(text.IndexOf("aurora", StringComparison.Ordinal) < text.IndexOf("tools", StringComparison.Ordinal));
        Assert.EndsWith("\n", text);
    }

    /// <summary>
    /// CASEFOLD1d: a workspace is one in another case only as <c>OrdinalIgnoreCase</c> finds it, each letter to its one
    /// capital, as the CLI's <c>remotemap.ts</c> finds it through <c>casefold.ts</c> (<c>remotes.test.ts</c> names this
    /// behaviour): a name full case mapping would widen or lower to the same letters is another workspace with its own row,
    /// and the map is written back sorted ordinally, as the CLI writes it.
    /// </summary>
    [Fact]
    public void A_workspace_is_one_only_as_OrdinalIgnoreCase_finds_it()
    {
        var dotted = $"i{(char)0x0307}zmir";
        RemoteTarget.Save(ConfigPath, new Dictionary<string, RemoteTarget>
        {
            ["İzmir"] = new("https://izmir.example.com", "dk_izmirkey0000"),
            ["straße"] = new("https://strasse.example.com", "dk_strassekey00"),
            [dotted] = new("https://other.example.com", "dk_otherkey0000"),
            ["STRASSE"] = new("https://other.example.com", "dk_otherkey0000"),
        });

        var remotes = RemoteTarget.LoadFile(ConfigPath);

        Assert.Equal(4, remotes.Count);
        Assert.Equal("https://izmir.example.com", remotes["İzmir"].Url);
        Assert.Equal("https://other.example.com", remotes[dotted].Url);
        Assert.Equal("https://strasse.example.com", remotes["straße"].Url);
        using var written = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ConfigPath));
        Assert.Equal(["STRASSE", dotted, "straße", "İzmir"], written.RootElement.EnumerateObject().Select(entry => entry.Name));
    }

    /// <summary>
    /// An EDITOR reads the file even when the environment outranks it — otherwise a machine with the
    /// env pair set could never wire a second workspace, and the surface would silently write over
    /// whatever it had just failed to see.
    /// </summary>
    [Fact]
    public void The_editor_s_read_ignores_the_environment()
    {
        File.WriteAllText(ConfigPath, """{ "aurora": { "url": "https://aurora.example.com", "key": "dk_filekey00000" } }""");
        Environment.SetEnvironmentVariable(RemoteTarget.UrlVariable, "https://env.example.com");
        try
        {
            Assert.Equal(["aurora"], RemoteTarget.LoadFile(ConfigPath).Keys);
        }
        finally
        {
            Environment.SetEnvironmentVariable(RemoteTarget.UrlVariable, null);
        }
    }

    /// <summary>
    /// A redaction that leaks a short key is worse than printing it, because it reads as safe. The
    /// prefix shown is the deployment's own audit handle — non-secret by design (D47 §7).
    /// </summary>
    [Fact]
    public void A_redacted_key_shows_the_audit_prefix_and_nothing_else()
    {
        Assert.Equal("dk_abcd1234…", RemoteTarget.Redact("dk_abcd1234wxyzsecret"));
        Assert.Equal("…", RemoteTarget.Redact("dk_short"));
        Assert.Equal("…", RemoteTarget.Redact(""));
    }
}
