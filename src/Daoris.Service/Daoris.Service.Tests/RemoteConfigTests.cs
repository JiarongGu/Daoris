using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The machine's remote is one file under the profile, environment overriding (D47 §9) — and absence
/// is the default, silently (D21). The driver carries a deliberate twin of this judgement
/// (`RemoteTarget`, which links against no service assembly); this table mirrors that twin's, because
/// the guarantee is security-relevant — never a mix of an env URL with the file's key, which would
/// quietly aim one machine's key at another's host — and until now only the twin was tested.
/// </summary>
public sealed class RemoteConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "daoris-remotecfg-" + Guid.NewGuid().ToString("N")[..8]);

    private string ConfigPath => Path.Combine(_dir, "remote.json");

    public RemoteConfigTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static Func<string, string?> Env(string? url = null, string? key = null) =>
        name => name == RemoteConfig.UrlVariable ? url : name == RemoteConfig.KeyVariable ? key : null;

    [Fact]
    public void Absence_is_silent()
    {
        Assert.Null(RemoteConfig.Load(Env(), ConfigPath));
    }

    [Fact]
    public void The_file_names_the_remote_and_the_environment_overrides_it()
    {
        File.WriteAllText(ConfigPath, """{ "url": "https://daoris.example.com/", "key": "dk_filekey" }""");

        var fromFile = RemoteConfig.Load(Env(), ConfigPath);
        Assert.Equal("https://daoris.example.com", fromFile!.Url);
        Assert.Equal("dk_filekey", fromFile.Key);

        var overridden = RemoteConfig.Load(Env("https://other.example.com", "dk_envkey"), ConfigPath);
        Assert.Equal("https://other.example.com", overridden!.Url);
        Assert.Equal("dk_envkey", overridden.Key);
    }

    /// <summary>A half-set environment pair never mixes with the file's other half.</summary>
    [Fact]
    public void A_half_declared_remote_is_no_remote()
    {
        File.WriteAllText(ConfigPath, """{ "url": "https://daoris.example.com", "key": "dk_filekey" }""");
        Assert.Null(RemoteConfig.Load(Env(url: "https://env.example.com"), ConfigPath));

        Assert.Null(RemoteConfig.Load(Env(url: "https://daoris.example.com"), Path.Combine(_dir, "absent.json")));
    }

    [Fact]
    public void A_malformed_file_is_no_remote_rather_than_a_crash()
    {
        File.WriteAllText(ConfigPath, "{ not json");
        Assert.Null(RemoteConfig.Load(Env(), ConfigPath));
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
