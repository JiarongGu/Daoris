using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTLYNTAI1b-i, first (the agent-lyntai design §5.3, round 3 and round 4): an access failure carries how it was read as a typed
/// disposition, set by <see cref="AgentErrors.Read"/> from the entry that matched, so a refused resume's open reads it in place of
/// <see cref="AgentError.By"/>'s suffix. The three values are today's three: a refused credential, a sign-in's words, and a code the door
/// declares. Every other kind carries none.
/// </summary>
public sealed class AccessDispositionTests
{
    private static readonly DateTimeOffset Seen = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private static readonly ProviderRegistry Built = ProviderRegistry.Built();

    private static AgentError Read(string adapter, int? code, string? kind, string? words, string where = DoorWhere.Open) =>
        AgentErrors.Read(Built, Built.Door(adapter), new DoorFailure("protocol", code, kind, null, words, where), Seen, TimeZoneInfo.Utc);

    [Fact]
    public void A_refused_credential_reads_refused_on_the_wire_and_on_the_harness_s_lines()
    {
        Assert.Equal(AccessDisposition.Refused, Read("claude-code-acp", -32603, null, "Internal error: API Error: 401 API key is invalid.").Access);
        Assert.Equal(
            AccessDisposition.Refused,
            Read("claude-code-acp", null, null, "Failed to authenticate. API Error: 401 API key is invalid.", DoorWhere.Lines).Access);
    }

    [Fact]
    public void A_sign_in_s_words_read_signed_out_by_words()
    {
        Assert.Equal(AccessDisposition.SignedOutByWords, Read("acp-stub", -32000, null, "Internal error: Authentication required").Access);
    }

    [Fact]
    public void A_declared_code_or_an_access_category_reads_signed_out_by_code()
    {
        Assert.Equal(AccessDisposition.SignedOutByCode, Read("claude-code-acp", -32000, null, "Authentication required").Access);
        Assert.Equal(AccessDisposition.SignedOutByCode, Read("codex-acp", null, "access", "Selected model is at capacity.").Access);
    }

    [Fact]
    public void Every_other_kind_carries_no_disposition()
    {
        Assert.Null(Read("claude-code-acp", -32603, "rate_limit", "Internal error").Access);
        Assert.Null(Read("claude-code-acp", -32603, "overloaded", "Internal error").Access);
        Assert.Null(Read("claude-code-acp", -32603, null, "Internal error").Access);
    }
}
