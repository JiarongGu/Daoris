using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MODELROLE1c2's readings (D205 §3.3, §9.2): <c>data/agent-offers.json</c>, per agent, version and account, judged per account for one
/// model: a reading passes an account only while its version is the start's and it is under a day old, so two accounts offering
/// differently are judged apart, an entitlement change with no version change is read again within a day, and a stale reading is tried.
/// Files only, in a home under <c>_fixtures/</c>.
/// </summary>
public sealed class AgentOffersTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-offers", Guid.NewGuid().ToString("N")[..8]);

    public AgentOffersTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string? Judge(string? account, string? model, string? version = "1.0.0", DateTimeOffset? at = null) =>
        AgentOffers.Judge(AgentOffers.Read(_home), "claude-code-acp", version, account, model, at ?? Now);

    /// <summary>
    /// §3.3: two accounts offering differently are judged apart, each for the model asked, by the account's own refusal; a list that
    /// lacks a model is no pass by itself (MODELROLE1a: Claude's door lists aliases and takes a full id), and account default is never
    /// judged.
    /// </summary>
    [Fact]
    public void Two_accounts_offering_differently_are_judged_apart()
    {
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "1.0.0", "acct-1", ["sonnet", "haiku"], ["low", "high"], Now));
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "1.0.0", "acct-2", ["opus", "sonnet"], null, Now));
        Assert.Null(Judge("acct-1", "opus"));

        Assert.Null(AgentOffers.Refused(_home, "claude-code-acp", "1.0.0", "acct-1", "opus", Now));
        Assert.Equal(AgentOffers.NotOffered, Judge("acct-1", "opus"));
        Assert.Null(Judge("acct-2", "opus"));
        Assert.Null(Judge("acct-1", "sonnet"));
        Assert.Null(Judge("acct-1", null));
        Assert.Null(Judge("acct-3", "opus"));
    }

    /// <summary>
    /// §3.3: a refusal at an open is kept by the model refused, <c>refused</c> where the list offered it, and passes the account for that
    /// model alone; an open that lists it again since keeps the refusal, since it has not tried it.
    /// </summary>
    [Fact]
    public void A_refusal_is_kept_by_the_model_refused()
    {
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "1.0.0", "acct-1", ["opus", "sonnet"], null, Now));
        Assert.Null(AgentOffers.Refused(_home, "claude-code-acp", "1.0.0", "acct-1", "opus", Now));
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "1.0.0", "acct-1", ["opus", "sonnet"], null, Now.AddHours(1)));

        Assert.Equal(AgentOffers.RefusedCode, Judge("acct-1", "opus", at: Now.AddHours(1)));
        Assert.Null(Judge("acct-1", "sonnet"));
        var reading = Assert.Single(AgentOffers.Read(_home));
        Assert.Equal(["opus", "sonnet"], reading.Models);
        Assert.Equal(Now, reading.RefusedAt);
    }

    /// <summary>
    /// §3.3: a reading of another version, or a refusal a day old, is a hint, and the account is tried; an entitlement that changes with no
    /// new version is read again within the day, however many opens list the model meanwhile.
    /// </summary>
    [Fact]
    public void A_stale_or_other_version_reading_is_tried()
    {
        Assert.Null(AgentOffers.Refused(_home, "claude-code-acp", "1.0.0", "acct-1", "opus", Now));
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "1.0.0", "acct-1", ["sonnet"], null, Now.AddHours(20)));

        Assert.Null(Judge("acct-1", "opus", version: "1.1.0"));
        Assert.Null(Judge("acct-1", "opus", at: Now + AgentOffers.Fresh));
        Assert.Equal(AgentOffers.NotOffered, Judge("acct-1", "opus", at: Now + AgentOffers.Fresh - TimeSpan.FromMinutes(1)));
    }

    /// <summary>§9.2: a reading from an older version never replaces a newer one; the tool's own home is a key of its own.</summary>
    [Fact]
    public void An_older_version_never_replaces_a_newer_reading()
    {
        Assert.Null(AgentOffers.Refused(_home, "claude-code-acp", "1.10.0", "acct-1", "opus", Now));
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "1.9.0", "acct-1", ["opus"], null, Now));
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "1.10.0", null, ["opus"], null, Now));

        Assert.Equal(AgentOffers.NotOffered, Judge("acct-1", "opus", version: "1.10.0"));
        Assert.Null(Judge(null, "opus", version: "1.10.0"));
        Assert.Equal(2, AgentOffers.Read(_home).Count);
    }
}
