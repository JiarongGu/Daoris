using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUGHOOK1a (D148 points 3 and 8, the plugin hooks design §2.2 and §4 rule 3): the query point's answer read by shape, and a
/// handshake naming a point this build lacks said once and never asked, rather than refusing the whole plugin. In-memory
/// streams only, so this is the suite's fast half; <see cref="HookTests"/> keeps the wire's real-process cases.
/// </summary>
public sealed class HookStateTests
{
    private const string Merge = "1111111111111111111111111111111111111111";
    private const string Source = "2222222222222222222222222222222222222222";

    /// <summary>A scripted plugin over two in-memory streams: a handler turns each frame the host sends into the frame it gets back.</summary>
    private sealed class FakePlugin(Func<JsonElement, string?> handle)
    {
        private readonly Channel<string> _toHost = Channel.CreateUnbounded<string>();

        public List<string> Sent { get; } = [];

        public TextReader Incoming => new ChannelReader(_toHost);

        public TextWriter Outgoing => new HandlerWriter(line =>
        {
            Sent.Add(line);
            if (handle(JsonDocument.Parse(line).RootElement) is { } reply) _toHost.Writer.TryWrite(reply);
        });

        private sealed class ChannelReader(Channel<string> channel) : TextReader
        {
            public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) =>
                await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false) && channel.Reader.TryRead(out var line) ? line : null;
        }

        private sealed class HandlerWriter(Action<string> onLine) : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override Task WriteLineAsync(string? value)
            {
                if (value is not null) onLine(value);
                return Task.CompletedTask;
            }
        }
    }

    private static string Ok(JsonElement request, string result) =>
        $$"""{"jsonrpc":"2.0","id":{{request.GetProperty("id").GetRawText()}},"result":{{result}}}""";

    private static string? Method(JsonElement frame) => frame.TryGetProperty("method", out var m) ? m.GetString() : null;

    private static async Task<HookPeer> StatePeerAsync(string answer)
    {
        var plugin = new FakePlugin(frame => Method(frame) switch
        {
            "initialize" => Ok(frame, """{"protocolVersion":1,"points":["work/state"]}"""),
            "hook/work/state" => Ok(frame, answer),
            _ => null,
        });
        var peer = new HookPeer(plugin.Incoming, plugin.Outgoing, "acme.asks");
        await peer.InitializeAsync("h", "d", [HookPoints.State], CancellationToken.None);
        return peer;
    }

    // ——— a point this build lacks (design §4 rule 3)

    /// <summary>
    /// A plugin that grew a point keeps the ones this build has: the point it lacks is said once, under the plugin's name, and
    /// never asked. Listening beyond the manifest is still refused, since the manifest is what a person read.
    /// </summary>
    [Fact]
    public async Task A_point_this_build_lacks_is_said_once_and_never_asked_and_one_beyond_the_manifest_is_still_refused()
    {
        var said = new List<string>();
        var grown = new FakePlugin(frame => Ok(frame, """{"protocolVersion":1,"points":["work/land","work/review"]}"""));
        var peer = new HookPeer(grown.Incoming, grown.Outgoing, "acme.grown", said.Add);

        await peer.InitializeAsync("h", "d", ["work/land", "work/review"], CancellationToken.None);

        Assert.Equal([HookPoints.Land], peer.Points);
        var line = Assert.Single(said);
        Assert.Contains("`work/review`", line);
        Assert.Contains("not a point this build has", line);

        var beyond = new FakePlugin(frame => Ok(frame, """{"protocolVersion":1,"points":["session/ended"]}"""));
        var refused = await Assert.ThrowsAsync<DriverException>(() =>
            new HookPeer(beyond.Incoming, beyond.Outgoing, "acme.gate").InitializeAsync("h", "d", ["quest/consider"], CancellationToken.None));
        Assert.Contains("manifest does not declare", refused.Message);
    }

    // ——— the frame and the answer (design §2.2)

    [Fact]
    public void The_frame_names_the_branch_the_line_and_what_the_record_holds()
    {
        var frame = JsonSerializer.SerializeToElement(HookFrames.State(new StateFrame(
            "engine", "aurora", "D:/fam/engine", "feature/q1-fix", "main", "https://example.test/pr/7", Source)));

        Assert.Equal(["repository", "workspace", "root", "branch", "line", "pullRequest", "pushedTip"],
            frame.EnumerateObject().Select(property => property.Name));
        Assert.Equal("feature/q1-fix", frame.GetProperty("branch").GetString());
        Assert.Equal(Source, frame.GetProperty("pushedTip").GetString());
        var none = JsonSerializer.SerializeToElement(HookFrames.State(new StateFrame("engine", "aurora", "r", "b", null, null, null)));
        Assert.Equal(JsonValueKind.Null, none.GetProperty("line").ValueKind);
        Assert.Equal(JsonValueKind.Null, none.GetProperty("pullRequest").ValueKind);
    }

    /// <summary>A completed answer, whole: each field read, the target without <c>refs/heads/</c>, the commits made lower-case.</summary>
    [Fact]
    public async Task A_completed_answer_is_read_whole()
    {
        var peer = await StatePeerAsync($$"""
            {"state":"completed","pullRequest":"https://example.test/org/project/_git/engine/pullrequest/7",
             "mergeCommit":"{{Merge.ToUpperInvariant().Replace('1', 'A')}}","sourceCommit":"{{Source}}","target":"refs/heads/main",
             "how":"squash","at":"2026-10-04T14:02:11Z","message":"completed by squash"}
            """);

        var answer = await peer.StateAsync(new { branch = "feature/q1-fix" }, CancellationToken.None);

        Assert.Equal(PullRequestStates.Completed, answer.State);
        Assert.Equal("https://example.test/org/project/_git/engine/pullrequest/7", answer.PullRequest);
        Assert.Equal(new string('a', 40), answer.MergeCommit);
        Assert.Equal(Source, answer.SourceCommit);
        Assert.Equal("main", answer.Target);
        Assert.Equal(MergeHow.Squash, answer.How);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 14, 2, 11, TimeSpan.Zero), answer.At);
        Assert.Equal("completed by squash", answer.Message);
        Assert.Equal("acme.asks", answer.Plugin);
    }

    /// <summary>
    /// The answer table both checkers hold (the kit's wire test holds the same rows, <see cref="PluginKitTests"/>): what is an
    /// answer and what is not. Anything else is <c>unreadable</c>, never a state.
    /// </summary>
    public static TheoryData<string, bool> Answers => new()
    {
        { """{"state":"open"}""", true },
        { """{"state":"abandoned","pullRequest":null,"at":"2026-10-04T14:02:11Z","message":"abandoned"}""", true },
        { """{"state":"unknown","message":"no pull request from that branch"}""", true },
        { $$"""{"state":"completed","mergeCommit":"{{Merge}}","sourceCommit":"{{Source}}"}""", true },
        { $$"""{"state":"completed","mergeCommit":"{{Merge}}","sourceCommit":"{{Source}}","how":"fast-forward"}""", true },
        { $$"""{"state":"completed","mergeCommit":"{{Merge}}"}""", false },
        { $$"""{"state":"completed","mergeCommit":"1111111","sourceCommit":"{{Source}}"}""", false },
        { """{"state":"merged"}""", false },
        { """{"state":7}""", false },
        { """{"pullRequest":"https://example.test/pr/7"}""", false },
        { """{"state":"open","pullRequest":"pull/7"}""", false },
        { """{"state":"open","target":7}""", false },
        { """{"state":"open","how":7}""", false },
        { """{"state":"open","at":"yesterday"}""", false },
        { """{"state":"open","message":7}""", false },
        { "null", false },
        { "[]", false },
    };

    [Theory]
    [MemberData(nameof(Answers))]
    public async Task An_answer_of_the_wrong_shape_is_unreadable_naming_the_plugin(string answer, bool accepted)
    {
        var peer = await StatePeerAsync(answer);

        if (accepted)
        {
            Assert.Contains((await peer.StateAsync(new { }, CancellationToken.None)).State, PullRequestStates.All);
            return;
        }

        var error = await Assert.ThrowsAsync<DriverException>(() => peer.StateAsync(new { }, CancellationToken.None));
        Assert.Contains("acme.asks", error.Message);
        Assert.Contains("not a pull request's state", error.Message);
        Assert.Equal(PluginEvents.Unreadable, PluginFailures.KindOf(error, "none"));
    }

    /// <summary>A word for how it merged that Daoris has no word for is null; a message is kept to 300 characters.</summary>
    [Fact]
    public async Task An_unknown_way_of_merging_is_null_and_a_long_message_is_cut()
    {
        var peer = await StatePeerAsync($$"""
            {"state":"completed","mergeCommit":"{{Merge}}","sourceCommit":"{{Source}}","how":"fast-forward","message":"{{new string('x', 400)}}"}
            """);

        var answer = await peer.StateAsync(new { }, CancellationToken.None);

        Assert.Null(answer.How);
        Assert.Equal(300, answer.Message!.Length);
    }

    /// <summary>A channel with no answer at this point fails as the plugin's error: nothing is ever read as a state it did not say.</summary>
    [Fact]
    public async Task A_channel_without_the_point_fails_as_an_error_never_as_a_state()
    {
        IHookChannel channel = new Silent();

        var error = await Assert.ThrowsAsync<DriverException>(() => channel.StateAsync(new { }, CancellationToken.None));

        Assert.Equal(PluginEvents.Errored, PluginFailures.KindOf(error, "none"));
    }

    private sealed class Silent : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => Task.FromResult(new PluginLanding("x", false, null, "x"));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
