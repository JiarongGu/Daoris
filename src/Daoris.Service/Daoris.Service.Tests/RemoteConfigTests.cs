using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The machine's remotes are a MAP — workspace → { url, key } — in one file under the profile, with
/// the environment overriding (D48 §5). Absence is the default, silently (D21).
/// </summary>
/// <remarks>
/// The driver carries a deliberate twin of this judgement (`RemoteTarget`, which links against no
/// service assembly); this table mirrors that twin's, because the guarantee is security-relevant —
/// never a mix of an env URL with the file's key, which would quietly aim one machine's key at
/// another's host — and a rule enforced in one twin only is a rule the other will contradict.
/// </remarks>
public sealed class RemoteConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "daoris-remotecfg-" + Guid.NewGuid().ToString("N")[..8]);

    private string ConfigPath => Path.Combine(_dir, "remotes.json");

    public RemoteConfigTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static Func<string, string?> Env(string? url = null, string? key = null, string? workspace = null) =>
        name => name == RemoteConfig.UrlVariable ? url
            : name == RemoteConfig.KeyVariable ? key
            : name == RemoteConfig.WorkspaceVariable ? workspace
            : null;

    [Fact]
    public void Absence_is_silent()
    {
        Assert.Empty(RemoteConfig.Load(Env(), ConfigPath));
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

        var remotes = RemoteConfig.Load(Env(), ConfigPath);

        Assert.Equal(2, remotes.Count);
        Assert.Equal("https://aurora.example.com", remotes["aurora"].Url);
        Assert.Equal("dk_aurorakey", remotes["aurora"].Key);
        Assert.Equal("https://tools.example.com", remotes["tools"].Url);
        // A workspace name is a person's name for a circle: two spellings differing only in case are
        // one circle, or the boundary is one nobody can see (Workspaces.Same).
        Assert.True(remotes.ContainsKey("AURORA"));
    }

    /// <summary>
    /// The environment is the answer for the WHOLE MACHINE, not one entry of it: either variable
    /// present means the file is not read at all. A merge would let a developer's real map leak into a
    /// process that thought it had named its only remote — which is exactly what the gate's hermetic
    /// guard relies on not happening.
    /// </summary>
    [Fact]
    public void The_environment_pair_replaces_the_file_whole_and_names_its_workspace()
    {
        File.WriteAllText(ConfigPath, """{ "aurora": { "url": "https://aurora.example.com", "key": "dk_filekey" } }""");

        var overridden = RemoteConfig.Load(Env("https://env.example.com/", "dk_envkey", "tools"), ConfigPath);

        Assert.Equal(["tools"], overridden.Keys);
        Assert.Equal("https://env.example.com", overridden["tools"].Url);
        Assert.Equal("dk_envkey", overridden["tools"].Key);
    }

    /// <summary>An unnamed env pair serves the workspace silence means, everywhere (D48 §2).</summary>
    [Fact]
    public void An_unnamed_environment_pair_serves_the_default_workspace()
    {
        var remotes = RemoteConfig.Load(Env("https://env.example.com", "dk_envkey"), ConfigPath);

        Assert.Equal([Workspaces.Default], remotes.Keys);
    }

    /// <summary>A half-set environment pair never mixes with the file's other half.</summary>
    [Fact]
    public void A_half_declared_remote_is_no_remote()
    {
        File.WriteAllText(ConfigPath, """{ "aurora": { "url": "https://aurora.example.com", "key": "dk_filekey" } }""");
        Assert.Empty(RemoteConfig.Load(Env(url: "https://env.example.com"), ConfigPath));
        Assert.Empty(RemoteConfig.Load(Env(key: "dk_envkey"), ConfigPath));

        Assert.Empty(RemoteConfig.Load(Env(url: "https://env.example.com"), Path.Combine(_dir, "absent.json")));
    }

    /// <summary>One unusable entry is one workspace with no remote — never a machine with none.</summary>
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

        var remotes = RemoteConfig.Load(Env(), ConfigPath);

        Assert.Equal(["aurora"], remotes.Keys);
    }

    [Fact]
    public void A_malformed_file_is_no_remote_rather_than_a_crash()
    {
        File.WriteAllText(ConfigPath, "{ not json");
        Assert.Empty(RemoteConfig.Load(Env(), ConfigPath));
    }

    /// <summary>
    /// The shape that preceded workspaces — one flat `{ url, key }` — is not read as anything. Nothing
    /// is deployed, so the file is rebuilt rather than migrated (the store's own rule), and a surface
    /// that silently re-homed an old remote into `default` would be guessing which circle it served.
    /// </summary>
    [Fact]
    public void The_pre_workspace_flat_shape_names_no_remote()
    {
        File.WriteAllText(ConfigPath, """{ "url": "https://daoris.example.com", "key": "dk_filekey" }""");

        Assert.Empty(RemoteConfig.Load(Env(), ConfigPath));
    }
}

/// <summary>
/// The relay transport's one judgement: what the remote's bytes MEAN. The relay tests inject a fake
/// remote, so this parse — status 0, `error` vs `message`, a non-quest answer — was the only real
/// transport code nothing exercised.
/// </summary>
public sealed class RemoteQuestParseTests
{
    [Fact]
    public void An_error_payload_carries_the_remote_judgement_verbatim()
    {
        var answer = HttpRemoteQuests.Parse(409, """{ "error": "quest `#abc123` is already Taken" }""");

        Assert.Equal(409, answer.Status);
        Assert.Equal("quest `#abc123` is already Taken", answer.Message);
        Assert.Null(answer.Quest);
    }

    [Fact]
    public void A_quest_answer_parses_whole()
    {
        var answer = HttpRemoteQuests.Parse(200, """
            { "message": "Published quest `#abc123` to `engine`.",
              "quest": { "id": "abc123", "from": "game", "to": "engine", "title": "T", "body": "B",
                         "status": "Open", "filed": "2026-09-20T10:00:00+00:00",
                         "updated": "2026-09-20T10:00:00+00:00" } }
            """);

        Assert.Equal("Published quest `#abc123` to `engine`.", answer.Message);
        Assert.Equal("abc123", answer.Quest!.Id);
        Assert.Equal(QuestStatus.Open, answer.Quest.Status);
        Assert.Null(answer.Quest.Note);
    }

    /// <summary>A proxy's HTML error page must become a sentence, not an exception.</summary>
    [Fact]
    public void A_non_quest_answer_is_named_and_truncated_rather_than_thrown()
    {
        var answer = HttpRemoteQuests.Parse(502, "<html>" + new string('x', 300) + "</html>");

        Assert.Equal(502, answer.Status);
        Assert.Contains("not a quest response", answer.Message);
        Assert.Contains("…", answer.Message);
        Assert.True(answer.Message.Length < 300);
    }

    /// <summary>A quest object missing a field the shape requires is a malformed answer, not a crash.</summary>
    [Fact]
    public void A_truncated_quest_object_is_a_malformed_answer()
    {
        var answer = HttpRemoteQuests.Parse(200, """{ "message": "ok", "quest": { "id": "abc123" } }""");

        Assert.Contains("not a quest response", answer.Message);
        Assert.Null(answer.Quest);
    }
}

/// <summary>
/// Which remote a verb reaches (D48 §5): the quest's workspace decides, because one shared deployment
/// serves one circle. A circle with no entry syncs nowhere, silently — absence is the default (D21).
/// </summary>
public sealed class RemoteQuestRouteTests
{
    private static readonly IReadOnlyDictionary<string, RemoteConfig> Two =
        new Dictionary<string, RemoteConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["aurora"] = new("https://aurora.example.com", "dk_a"),
            ["tools"] = new("https://tools.example.com", "dk_t"),
        };

    [Fact]
    public void No_remotes_at_all_is_no_router()
    {
        Assert.Null(RemoteQuestRoutes.From(new Dictionary<string, RemoteConfig>()));
    }

    [Fact]
    public void A_workspace_reaches_its_own_remote_and_no_other()
    {
        var routes = RemoteQuestRoutes.From(Two)!;

        Assert.NotNull(routes.For("aurora"));
        Assert.NotNull(routes.For("AURORA"));
        Assert.NotSame(routes.For("aurora"), routes.For("tools"));
        Assert.Null(routes.For("nobody"));
        // Silence is `default`, which this machine has no entry for — so it reaches nowhere.
        Assert.Null(routes.For(null));
    }

    [Fact]
    public void The_circles_with_a_remote_are_nameable_for_a_refusal_that_has_to_say_which()
    {
        Assert.Equal(["aurora", "tools"], RemoteQuestRoutes.From(Two)!.Workspaces.OrderBy(w => w, StringComparer.Ordinal));
    }
}
