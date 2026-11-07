using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Host policy (the agent-lyntai design §5.2's and §5.3's round-5 paragraphs): an access failure's evidence is kept apart, the reason a
/// field stated and whether the words read as a sign-in or a refused credential, and each reader reads its own: a refused resume's open by
/// today's precedence (the field first, the words second, a sign-in's words before a refusal's), the conclusion's sign-out by the words or
/// the door's own field, the conversation's by the words alone, the conclusion's refusal by the refusal's words alone. Daoris's own types,
/// never a proof that the layer supplies the evidence.
/// </summary>
public sealed class HostPolicyAccessTests
{
    [Theory]
    [InlineData(AccessReason.SignInAgain, false, false, AccessDisposition.SignedOutByCode)]
    [InlineData(AccessReason.CredentialRefused, false, true, AccessDisposition.SignedOutByCode)]
    [InlineData(AccessReason.SignInAgain, true, false, AccessDisposition.SignedOutByCode)]
    [InlineData(null, true, true, AccessDisposition.SignedOutByWords)]
    [InlineData(null, false, true, AccessDisposition.Refused)]
    [InlineData(null, false, false, null)]
    public void Host_policy_reads_a_refused_open_by_today_s_precedence(AccessReason? field, bool signIn, bool refused, AccessDisposition? expected)
    {
        Assert.Equal(expected, new AccessEvidence(field, signIn, refused).Opening);
    }

    /// <summary>A code with words the sign-in table does not know signs out at the conclusion and writes nothing in a conversation.</summary>
    [Fact]
    public void Host_policy_signs_out_at_the_conclusion_by_words_or_field_and_in_a_conversation_by_words_alone()
    {
        var codeOnly = new AccessEvidence(AccessReason.SignInAgain, WordsSignIn: false, WordsRefused: false);
        Assert.Equal((true, false, false), (codeOnly.SignsOutAtConclusion, codeOnly.SignsOutInConversation, codeOnly.Refuses));

        var words = new AccessEvidence(null, WordsSignIn: true, WordsRefused: false);
        Assert.Equal((true, true, false), (words.SignsOutAtConclusion, words.SignsOutInConversation, words.Refuses));

        var refusal = new AccessEvidence(AccessReason.CredentialRefused, WordsSignIn: false, WordsRefused: true);
        Assert.Equal((true, false, true), (refusal.SignsOutAtConclusion, refusal.SignsOutInConversation, refusal.Refuses));
    }

    /// <summary>Today's reader and the evidence agree: what <see cref="AgentErrors.Read"/> answers is what the evidence's opening reads.</summary>
    [Fact]
    public void Host_policy_s_opening_agrees_with_today_s_reader_on_each_disposition()
    {
        var built = ProviderRegistry.Built();
        var seen = new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);
        AccessDisposition? Today(string door, int? code, string words) =>
            AgentErrors.Read(built, built.Door(door), new DoorFailure("protocol", code, null, null, words, DoorWhere.Open), seen, TimeZoneInfo.Utc).Access;

        Assert.Equal(new AccessEvidence(AccessReason.SignInAgain, false, false).Opening, Today("claude-code-acp", -32000, "Authentication required"));
        Assert.Equal(new AccessEvidence(null, true, false).Opening, Today("acp-stub", -32000, "Internal error: Authentication required"));
        Assert.Equal(new AccessEvidence(null, false, true).Opening, Today("claude-code-acp", -32603, "Internal error: API Error: 401 API key is invalid."));
    }
}
