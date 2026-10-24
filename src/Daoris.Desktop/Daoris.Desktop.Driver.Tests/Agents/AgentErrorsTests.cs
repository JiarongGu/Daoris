using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTERRORS1a (D210, the agent-errors design §3.2, §4.1, §4.4): the one reader of an agent's failure, its order and its tables held
/// both ways, read through the real adapter set, so <c>claude-code-acp</c> reads its owner's provider entries after its own and its own
/// <c>-32000</c> declaration is never another door's. A fixture read from an adapter's source is marked source-derived (§3.3).
/// </summary>
public sealed class AgentErrorsTests
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 24, 0, 30, 0, TimeSpan.Zero);

    private const string Overloaded =
        "Internal error: API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment. If it persists, check https://status.claude.com.";

    private static readonly AdapterSet Built = AdapterSet.Built();

    private static AgentError Read(string adapter, int? code, string? kind, string? words, int? status = null, AdapterSet? set = null)
    {
        var adapters = set ?? Built;
        return AgentErrors.Read(
            adapters, adapters.Resolve(adapter), new DoorFailure("protocol", code, kind, status, words, DoorWhere.Prompt), Seen, TimeZoneInfo.Utc);
    }

    /// <summary>Shape 1's frame, source-derived (the design §0, §4.4): <c>-32603</c> with <c>data.errorKind: "overloaded"</c>.</summary>
    [Fact]
    public void The_install_s_529_with_its_kind_reads_transient_on_claude_code_acp_from_its_owner_s_table()
    {
        var read = Read("claude-code-acp", -32603, "overloaded", Overloaded);

        Assert.Equal((AgentErrorKind.Transient, "owner:kind:overloaded"), (read.Kind, read.By));
    }

    [Fact]
    public void The_kind_alone_reads_transient_and_unknown_alone_a_400_and_no_code_read_fatal()
    {
        Assert.Equal(AgentErrorKind.Transient, Read("claude-code-acp", -32603, "overloaded", "Internal error").Kind);
        Assert.Equal(AgentErrorKind.Transient, Read("claude-code-acp", -32603, "server_error", "Internal error").Kind);
        Assert.Equal((AgentErrorKind.Transient, "owner:status:529"), (Read("claude-code-acp", null, null, "x", status: 529).Kind, Read("claude-code-acp", null, null, "x", status: 529).By));

        Assert.Equal((AgentErrorKind.Fatal, AgentErrors.Unread), (Read("claude-code-acp", -32603, "unknown", "Internal error").Kind, Read("claude-code-acp", -32603, "unknown", "Internal error").By));
        Assert.Equal(AgentErrorKind.Fatal, Read("claude-code-acp", null, null, "Internal error", status: 400).Kind);
        Assert.Equal(AgentErrorKind.Fatal, Read("claude-code-acp", -32603, null, "Internal error").Kind);
        // An absent kind is no reading, never `unknown`; a kind the table names as nothing transient makes none.
        Assert.Equal(AgentErrorKind.Fatal, Read("claude-code-acp", -32603, "invalid_request", "Internal error: API Error: 400 bad request").Kind);
    }

    /// <summary>§4.4's sentences, each read as its kind by its own words with no code, and the near ones that must not read transient.</summary>
    [Fact]
    public void Each_recorded_sentence_reads_as_its_kind_and_each_near_one_does_not_read_transient()
    {
        Assert.Equal((AgentErrorKind.Transient, "owner:words:transient-1"), Kind(Read("claude-code-acp", -32603, null, Overloaded)));
        Assert.Equal(
            (AgentErrorKind.Transient, "owner:words:transient-2"),
            Kind(Read("claude-code-acp", -32603, null, "Internal error: API Error: Connection dropped (ECONNRESET)")));

        Assert.Equal((AgentErrorKind.Access, "owner:words:refused"), Kind(Read("claude-code-acp", -32603, null, "Internal error: API Error: 401 API key is invalid.")));
        Assert.Equal(AgentErrorKind.Limit, Read("claude-code-acp", -32603, null, "Internal error: You've hit your weekly limit · resets Oct 26, 10pm (UTC)").Kind);
        Assert.Equal(AgentErrorKind.Fatal, Read("claude-code-acp", -32603, null, "Internal error: API Error: 400 messages: text content blocks must be non-empty").Kind);
        // A quotation that does not start the words, or follow a door's `: `, is not the error.
        Assert.Equal(AgentErrorKind.Fatal, Read("claude-code-acp", -32603, null, "the log said API Error: 529 Overloaded earlier").Kind);

        static (AgentErrorKind, string) Kind(AgentError read) => (read.Kind, read.By);
    }

    /// <summary>Every transient pattern stands on a recorded sentence, and every recorded sentence of the table is read by one (§3.3).</summary>
    [Fact]
    public void Every_transient_pattern_has_a_recorded_sentence_and_every_recorded_sentence_a_pattern()
    {
        var words = ClaudeErrors.Words;
        var sentences = words.Recorded.Where(recorded => !recorded.SourceDerived).Select(recorded => recorded.Sentence).ToList();
        Assert.All(words.Transient, pattern => Assert.Contains(sentences, sentence => AgentErrors.Matches(pattern, sentence)));
        Assert.All(sentences, sentence => Assert.Contains(words.Transient, pattern => AgentErrors.Matches(pattern, sentence)));
        // A frame read from the adapter's source is marked so, apart from one captured on the door.
        Assert.Contains(words.Recorded, recorded => recorded.SourceDerived && recorded.Sentence.Contains("overloaded", StringComparison.Ordinal));
        Assert.All(ClaudeAcpErrors.Words.Recorded, recorded => Assert.True(recorded.SourceDerived));
    }

    /// <summary>§3.2's step 1: a limit's sentence under <c>rate_limit</c> is limited by its words; <c>rate_limit</c> with none, an hour.</summary>
    [Fact]
    public void A_limit_is_read_first_by_its_sentence_then_by_its_code_with_the_unstated_reset_and_never_transient()
    {
        var weekly = Read("claude-code-acp", -32603, "rate_limit", "Internal error: You've hit your weekly limit · resets Oct 26, 10pm (UTC)");
        Assert.Equal((AgentErrorKind.Limit, "owner:words:limit", true), (weekly.Kind, weekly.By, weekly.Limit!.Stated));

        var bare = Read("claude-code-acp", -32603, "rate_limit", "Internal error");
        Assert.Equal((AgentErrorKind.Limit, "owner:kind:rate_limit"), (bare.Kind, bare.By));
        Assert.Equal((Seen + AccountLimits.DefaultCoolOff, false), (bare.Limit!.Until, bare.Limit.Stated));

        // A 429 is a limit even beside a transient sentence (§3.1): step 1 reads it first.
        var both = Read("claude-code-acp", -32603, null, Overloaded, status: 429);
        Assert.Equal((AgentErrorKind.Limit, "owner:status:429"), (both.Kind, both.By));
    }

    /// <summary>
    /// <c>-32000</c> is access only where the door's own server declares it (§3.1): <c>claude-code-acp</c>'s *Please run /login*, and on
    /// the stub, which answers every failed turn <c>-32000</c>, an unrelated failure with the same code stays fatal.
    /// </summary>
    [Fact]
    public void Minus_32000_is_access_only_where_the_door_declares_it()
    {
        Assert.Equal(
            (AgentErrorKind.Access, "own:code:-32000"),
            (Read("claude-code-acp", -32000, null, "Authentication required").Kind, Read("claude-code-acp", -32000, null, "Authentication required").By));

        Assert.Equal(AgentErrorKind.Fatal, Read("acp-stub", -32000, null, "the stub's turn failed").Kind);
        // The stub's sign-in words still read access, by its words and never its code.
        Assert.Equal(AgentErrorKind.Access, Read("acp-stub", -32000, null, "Internal error: Authentication required").Kind);
        // The stub mirrors the provider's codes alone: a kind reads transient, a sentence with no code keeps today's end.
        Assert.Equal(AgentErrorKind.Transient, Read("acp-stub", -32603, "overloaded", Overloaded).Kind);
        Assert.Equal(AgentErrorKind.Fatal, Read("acp-stub", -32603, null, "Internal error: API Error: Connection dropped (ECONNRESET)").Kind);
    }

    /// <summary>
    /// The tables compose (§4.1, round 3): own first, then the owner, never "own or owner", and <c>AuthRequiredCode</c> never inherited.
    /// A plugin with <c>accountOf: claude-code</c> reads its owner's provider entries, never its <c>-32000</c>; one with no owner reads nothing.
    /// </summary>
    [Fact]
    public void A_plugin_with_an_owner_reads_its_owner_s_entries_but_never_a_declaration_of_another_door()
    {
        var joined = Built.Names.ToDictionary(name => name, Built.Resolve, StringComparer.OrdinalIgnoreCase);
        joined["acp-owned"] = new DeclaredAcpAdapter(new PluginHarness("acp-owned", ["node"], AccountOf: "claude-code"), "owned-plugin");
        joined["acp-alone"] = new DeclaredAcpAdapter(new PluginHarness("acp-alone", ["node"]), "alone-plugin");
        var set = new AdapterSet(joined);

        Assert.Equal((AgentErrorKind.Transient, "owner:kind:overloaded"), Pair(Read("acp-owned", -32603, "overloaded", "Internal error", set: set)));
        Assert.Equal(AgentErrorKind.Transient, Read("acp-owned", -32603, null, Overloaded, set: set).Kind);
        Assert.Equal(AgentErrorKind.Limit, Read("acp-owned", -32603, "rate_limit", "Internal error", set: set).Kind);
        Assert.Equal(AgentErrorKind.Fatal, Read("acp-owned", -32000, null, "the plugin's turn failed", set: set).Kind);

        Assert.Equal(AgentErrorKind.Fatal, Read("acp-alone", -32603, "overloaded", Overloaded, set: set).Kind);
        Assert.Equal(AgentErrorKind.Fatal, Read("acp-alone", -32000, null, "Authentication required", set: set).Kind);

        static (AgentErrorKind, string) Pair(AgentError read) => (read.Kind, read.By);
    }

    /// <summary>The harness's own lines are read for access alone (§3.3): a transient sentence there is no transient.</summary>
    [Fact]
    public void The_harness_s_lines_are_read_for_access_alone()
    {
        var lines = new DoorFailure("protocol", null, null, null, Overloaded, DoorWhere.Harness);
        Assert.Equal(AgentErrorKind.Fatal, AgentErrors.Read(Built, Built.Resolve("claude-code-acp"), lines, Seen, TimeZoneInfo.Utc).Kind);

        var refused = lines with { Words = "Failed to authenticate. API Error: 401 API key is invalid." };
        Assert.Equal(AgentErrorKind.Access, AgentErrors.Read(Built, Built.Resolve("claude-code-acp"), refused, Seen, TimeZoneInfo.Utc).Kind);
    }
}
