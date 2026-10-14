using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The machine's wiring, as the page actually reaches it (D48 §5, D50).
/// </summary>
/// <remarks>
/// This module holds the only credential anywhere in the desktop, so the tests that matter most here
/// are the negative ones: a key goes IN and never comes OUT. The page's own suite asserts that against
/// a mocked bridge answering a redacted string — which proves the page renders what it is given, and
/// nothing at all about what this side gives it.
/// </remarks>
public sealed class RemotesModuleTests : Bridge
{
    private const string Key = "dk_abcd1234wxyzsecret";

    private RemotesModule Module() => new(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));

    private static object Wiring(string workspace, string url, string key) =>
        new { workspace, url, key };

    [Fact]
    public async Task A_machine_with_nothing_wired_answers_an_empty_map_rather_than_failing()
    {
        var state = await AnswerAsync(Module(), "STATE");

        Assert.Empty(state.GetProperty("remotes").EnumerateArray());
        Assert.False(state.GetProperty("fromEnvironment").GetBoolean());
        // The path is reported even when nothing is wired: a person looking for the file needs it.
        Assert.Equal(RemotesPath, state.GetProperty("path").GetString());
    }

    [Fact]
    public async Task Wiring_a_workspace_writes_the_file_the_cli_and_the_sync_loop_read()
    {
        await AnswerAsync(Module(), "SET", Wiring("aurora", "https://aurora.example.com", Key));

        // The FILE is the contract, so it is the file that is asserted — not this module's own answer,
        // which would only prove it agrees with itself.
        var written = RemoteTarget.LoadFile(RemotesPath);
        Assert.Equal("https://aurora.example.com", written["aurora"].Url);
        Assert.Equal(Key, written["aurora"].Key);
    }

    /// <summary>
    /// <b>The key goes in and never comes out.</b> A page that could read one back would put it in a
    /// render tree, a devtools panel, and eventually a screenshot.
    /// </summary>
    [Fact]
    public async Task The_key_is_never_answered_back_beyond_its_audit_prefix()
    {
        await AnswerAsync(Module(), "SET", Wiring("aurora", "https://aurora.example.com", Key));

        var state = await AnswerAsync(Module(), "STATE");

        // The whole answer, not just the key field: a key that leaked into some other property would
        // be exactly as exposed and would pass a field-shaped assertion.
        Assert.DoesNotContain(Key, state.GetRawText(), StringComparison.Ordinal);

        var only = Assert.Single(state.GetProperty("remotes").EnumerateArray().ToList());
        Assert.Equal(RemoteTarget.Redact(Key), only.GetProperty("key").GetString());
        // And the redaction is a PREFIX of the real key, not a fixed mask that merely looks safe —
        // it has to be the same handle the deployment's own `keys list` prints, or it audits nothing.
        Assert.StartsWith(RemoteTarget.Redact(Key).TrimEnd('…'), Key, StringComparison.Ordinal);
    }

    /// <summary>
    /// Half a pair is no remote in every loader (D48 §5), so writing one would put an entry in the
    /// file that silently does nothing. Refused at the door instead, with a sentence saying why.
    /// </summary>
    [Theory]
    [InlineData("", Key)]
    [InlineData("https://aurora.example.com", "")]
    public async Task A_half_declared_remote_is_refused_rather_than_written(string url, string key)
    {
        var refusal = await RefusalAsync(Module(), "SET", Wiring("aurora", url, key));

        // The CODE, because that is what the page translates — a refusal identified only by its
        // English sentence is one no other language can render, and one every rewording breaks.
        Assert.Contains(Refusals.RemoteHalfDeclared, refusal);
        Assert.Contains("workspace=aurora", refusal);
        Assert.False(File.Exists(RemotesPath), "a refused wiring still wrote the file");
    }

    [Fact]
    public async Task Unwiring_something_that_was_never_wired_is_an_answer_not_a_failure()
    {
        // The end state is the one that was asked for, so a failure here would make an idempotent
        // surface look broken — the same judgement `daoris remote remove` makes.
        var state = await AnswerAsync(Module(), "REMOVE", new { workspace = "never-wired" });

        Assert.Empty(state.GetProperty("remotes").EnumerateArray());
    }

    [Fact]
    public async Task Unwiring_leaves_every_other_circle_standing()
    {
        var module = Module();
        await AnswerAsync(module, "SET", Wiring("aurora", "https://aurora.example.com", Key));
        await AnswerAsync(module, "SET", Wiring("tools", "https://tools.example.com", "dk_toolskey00000"));

        await AnswerAsync(module, "REMOVE", new { workspace = "aurora" });

        var left = RemoteTarget.LoadFile(RemotesPath);
        Assert.False(left.ContainsKey("aurora"));
        Assert.True(left.ContainsKey("tools"));
    }

    /// <summary>
    /// With the environment pair set, no loader reads the file (D48 §5) — so the surface must say which
    /// source decided. Otherwise it reports its own last edit as though it were the machine's wiring.
    /// </summary>
    [Fact]
    public async Task The_surface_says_when_the_environment_and_not_the_file_is_in_effect()
    {
        Redirect("DAORIS_REMOTE_URL", "https://from-the-environment.example.com");
        Redirect("DAORIS_REMOTE_KEY", "dk_environment00");

        var state = await AnswerAsync(Module(), "STATE");

        Assert.True(state.GetProperty("fromEnvironment").GetBoolean());
        var only = Assert.Single(state.GetProperty("remotes").EnumerateArray().ToList());
        Assert.Equal("https://from-the-environment.example.com", only.GetProperty("url").GetString());
    }

    /// <summary>
    /// An edit must land in the FILE even when the environment currently outranks it — otherwise a
    /// machine with the pair set could never wire a second workspace. The same rule `daoris remote`
    /// follows, and the twin that had no test on this side.
    /// </summary>
    [Fact]
    public async Task An_edit_lands_in_the_file_even_while_the_environment_outranks_it()
    {
        Redirect("DAORIS_REMOTE_URL", "https://from-the-environment.example.com");
        Redirect("DAORIS_REMOTE_KEY", "dk_environment00");

        await AnswerAsync(Module(), "SET", Wiring("aurora", "https://aurora.example.com", Key));

        var written = RemoteTarget.LoadFile(RemotesPath);
        Assert.Equal("https://aurora.example.com", written["aurora"].Url);
    }

    [Fact]
    public async Task An_unknown_request_type_is_refused_rather_than_silently_answered()
    {
        // The framework's own code for "this module has no such route" — a page older or newer than
        // the host is exactly when this fires, and it must not look like the request succeeded.
        Assert.Contains("NO_ROUTE", await RefusalAsync(Module(), "FROBNICATE"));
    }
}
