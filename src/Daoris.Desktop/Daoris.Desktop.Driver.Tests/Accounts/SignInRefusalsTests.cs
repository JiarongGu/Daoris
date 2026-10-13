using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A start the agent refused for its sign-in, read from the door's failure (ROSTER1b; D125 point 1's rule, which an account's
/// limit is read by too): the reader is pure, so this table is the whole contract for which failures are a refused sign-in.
/// </summary>
/// <remarks>
/// The install's sentence is the one recorded (2026-10-04): after an update every account read <i>never read</i>, and four
/// starts in two minutes on an account nobody had signed in to each failed with it. Anything else stays a failure as today.
/// </remarks>
public sealed class SignInRefusalsTests
{
    private static readonly SignInWords Claude = new ClaudeCodeAdapter().Toolchain!.SignIn!;

    /// <summary>The install's refusal, as the protocol door says it, and the same words with the agent's own reason after them.</summary>
    [Theory]
    [InlineData("the ACP agent refused the call: Authentication required")]
    [InlineData("the ACP agent refused the call: Authentication required: run /login")]
    [InlineData("the ACP agent refused the call: authentication required.")]
    [InlineData("Authentication required")]
    public void The_install_s_refusal_is_a_refused_sign_in(string failure)
    {
        Assert.True(SignInRefusals.Read(Claude, failure));
    }

    /// <summary>
    /// Anything else is a failure as today: another refusal, an account's limit, the words quoted in the middle of a
    /// sentence or running on into another, the native door's 401, which AGT3b reads from the transcript, and no words.
    /// </summary>
    [Theory]
    [InlineData("the ACP agent refused the call: Internal error: Overloaded")]
    [InlineData("the ACP agent refused the call: Internal error: You've hit your individual spend limit · your session limit resets 7am (UTC)")]
    [InlineData("the test said Authentication required when it should not have")]
    [InlineData("the ACP agent refused the call: Authentication required for the workspace")]
    [InlineData("Failed to authenticate. API Error: 401 API key is invalid.")]
    [InlineData("")]
    [InlineData("   ")]
    public void A_failure_that_is_not_a_refused_sign_in_is_none(string failure)
    {
        Assert.False(SignInRefusals.Read(Claude, failure));
    }

    [Fact]
    public void An_agent_with_no_entry_and_a_failure_with_no_words_are_none()
    {
        Assert.False(SignInRefusals.Read(null, "the ACP agent refused the call: Authentication required"));
        Assert.False(SignInRefusals.Read(Claude, null));
    }

    // ——— The table: an entry grows only with a recorded sentence.

    /// <summary>Every recorded sentence is recognised by its own entry.</summary>
    [Fact]
    public void Every_recorded_sentence_is_recognised()
    {
        Assert.NotEmpty(Claude.Recorded);
        foreach (var recorded in Claude.Recorded)
        {
            Assert.True(SignInRefusals.Read(Claude, recorded.Sentence), $"not read: {recorded.Sentence}");
            Assert.False(string.IsNullOrWhiteSpace(recorded.Channel));
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", recorded.Seen);
        }
    }

    /// <summary>Both ways, over every entry a toolchain declares: nothing in an entry stands without a recorded sentence.</summary>
    [Fact]
    public void Every_entry_is_proven_by_its_recorded_sentences_both_ways()
    {
        var declared = Declared().ToList();
        Assert.NotEmpty(declared);
        foreach (var (agent, words) in declared)
        {
            Assert.True(Unproven(words).Count == 0, $"{agent}: {string.Join("; ", Unproven(words))}");
        }
    }

    /// <summary>The both-ways check refuses each kind of unproven entry, so its passing above means something.</summary>
    [Fact]
    public void The_both_ways_check_refuses_an_unproven_entry()
    {
        var marker = Claude with { Markers = [.. Claude.Markers, @"(?:^|: )Not logged in(?=$|[.:])"] };
        var sentence = Claude with
        {
            Recorded = [.. Claude.Recorded, new RecordedSignIn("the ACP agent refused the call: Please sign in", "2026-10-05", "made up")],
        };
        var empty = Claude with { Recorded = [] };

        Assert.Contains(Unproven(marker), p => p.Contains("Not logged in", StringComparison.Ordinal));
        Assert.Contains(Unproven(sentence), p => p.Contains("Please sign in", StringComparison.Ordinal));
        Assert.NotEmpty(Unproven(empty));
    }

    /// <summary>
    /// Only an agent whose refusal of a sign-in was recorded on a door declares an entry: Claude Code, and the stub that
    /// mirrors its words as it mirrors <c>Refused</c> and <c>Limits</c>, so a tick can gate it with no account. A door
    /// onto either reads its owner's (AGT7); dsh, Codex's door and a plugin have none, and read every failure as a failure.
    /// </summary>
    [Fact]
    public void Only_an_agent_seen_refusing_a_sign_in_declares_an_entry()
    {
        Assert.Equal(new[] { "claude-code", "stub" }, Declared().Select(d => d.Agent).Order(StringComparer.Ordinal));
        Assert.Same(Claude, new StubAdapter().Toolchain!.SignIn);
    }

    /// <summary>A door reads its owner's words (AGT7): Claude Code's protocol door, and the protocol stub onto the stub.</summary>
    [Fact]
    public void A_door_reads_its_owner_s_words()
    {
        var roster = new HarnessRoster(AdapterSet.Built(), Path.Combine(Path.GetTempPath(), "daoris-signin-" + Guid.NewGuid().ToString("N")[..8], "harnesses.json"));

        Assert.Same(Claude, roster.SignInOf("claude-code-acp"));
        Assert.Same(Claude, roster.SignInOf("acp-stub"));
        Assert.Same(Claude, roster.SignInOf("claude-code"));
        Assert.Null(roster.SignInOf("codex-acp"));
        Assert.Null(roster.SignInOf("dsh"));
    }

    private static IEnumerable<(string Agent, SignInWords Words)> Declared()
    {
        var adapters = AdapterSet.Built();
        foreach (var name in new[] { "stub", "acp-stub", "claude-code", "claude-code-acp", "dsh", "codex-acp" })
        {
            if (adapters.Resolve(name).Toolchain?.SignIn is { } words) yield return (name, words);
        }
    }

    /// <summary>What in an entry no recorded sentence proves: a marker no sentence matches, a sentence no marker matches, and no sentence at all.</summary>
    private static IReadOnlyList<string> Unproven(SignInWords words)
    {
        var problems = new List<string>();
        if (words.Recorded.Count == 0) problems.Add("no recorded sentence");

        problems.AddRange(words.Markers
            .Where(marker => !words.Recorded.Any(recorded => SignInRefusals.Matches(marker, recorded.Sentence)))
            .Select(marker => $"marker `{marker}` matches no recorded sentence"));
        problems.AddRange(words.Recorded
            .Where(recorded => !words.Markers.Any(marker => SignInRefusals.Matches(marker, recorded.Sentence)))
            .Select(recorded => $"recorded sentence `{recorded.Sentence}` matches no marker"));
        return problems;
    }
}
