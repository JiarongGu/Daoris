using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The offers readings: <c>data/agent-offers.json</c>, per agent, version and account, judged per account for one
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

    /// <summary>
    /// A door's offers part reads the provider's settings file, extended on the native door by the efforts only a session carries, and
    /// where the door states nothing what its open last listed on an account of its own id.
    /// </summary>
    [Fact]
    public void A_doors_offers_part_reads_the_settings_file_or_what_its_open_listed()
    {
        var file = new ClaudeSettingsFile();

        var native = new SettingsOffers(SessionOnlyEfforts: true).Of(_home, "claude-code", file)!;
        var protocol = new SettingsOffers(SessionOnlyEfforts: false).Of(_home, "claude-code-acp", file)!;

        Assert.Equal(file.Models, native.Models);
        Assert.Equal([.. file.Efforts, .. file.SessionOnly], native.Efforts);
        Assert.Equal(file.Efforts, protocol.Efforts);
        Assert.DoesNotContain(file.SessionOnly[0], protocol.Efforts);

        Assert.Null(ListedOffers.Instance.Of(_home, "codex-acp", null));
        Assert.Null(AgentOffers.Offered(_home, "codex-acp", "1.0.0", "acct-1", ["gpt"], ["low"], Now));
        var listed = ListedOffers.Instance.Of(_home, "codex-acp", null)!;
        Assert.Equal(["gpt"], listed.Models);
        Assert.Equal(["low"], listed.Efforts);
        Assert.Null(ListedOffers.Instance.Of(_home, "claude-code-acp", null));
    }

    private string? Judge(string? account, string? model, string? version = "1.0.0", DateTimeOffset? at = null) =>
        AgentOffers.Offers(_home, "claude-code-acp", version, account, at ?? Now).Session.Judge(model);

    /// <summary>
    /// A door's offers are read in two scopes, a session's from the account's own reading at this version and the account defaults'
    /// from what the provider states; one account's refusal passes that account while another of the same door and version is tried, and a
    /// reading of another version offers nothing.
    /// </summary>
    [Fact]
    public void Offers_are_read_per_account_in_two_scopes()
    {
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "1.0.0", "acct-1", ["sonnet"], ["low", "max"], Now));
        Assert.Null(AgentOffers.Offered(_home, "claude-code-acp", "1.0.0", "acct-2", ["sonnet", "opus"], null, Now));
        Assert.Null(AgentOffers.Refused(_home, "claude-code-acp", "1.0.0", "acct-1", "opus", Now));
        var readings = AgentOffers.Read(_home);
        var defaults = new AccountDefaultOffers(["low", "medium", "high", "xhigh"]);

        var first = AgentOffers.Offers(readings, "claude-code-acp", "1.0.0", "acct-1", Now, defaults);
        var second = AgentOffers.Offers(readings, "claude-code-acp", "1.0.0", "acct-2", Now, defaults);

        Assert.Equal(["sonnet"], first.Session.Models);
        Assert.Equal(["low", "max"], first.Session.Efforts);
        Assert.Equal(["opus"], first.Session.Refused);
        Assert.Equal(AgentOffers.NotOffered, first.Session.Judge("opus"));
        Assert.Null(second.Session.Judge("opus"));
        Assert.Equal(["sonnet", "opus"], second.Session.Models);
        // The session scope holds `max`, which the account defaults never do.
        Assert.DoesNotContain("max", first.Defaults.Efforts);
        Assert.Same(SessionOffers.None, AgentOffers.Offers(readings, "claude-code-acp", "2.0.0", "acct-1", Now).Session);
        Assert.Empty(AgentOffers.Offers(readings, "claude-code-acp", "1.0.0", "acct-1", Now).Defaults.Efforts);
        Assert.Empty(AgentOffers.Offers(readings, "claude-code-acp", "1.0.0", "acct-1", Now.AddDays(2)).Session.Refused);
    }

    /// <summary>
    /// Two accounts offering differently are judged apart, each for the model asked, by the account's own refusal; a list that
    /// lacks a model is no pass by itself (Claude's door lists aliases and takes a full id), and account default is never
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
    /// A refusal at an open is kept by the model refused, <c>refused</c> where the list offered it, and passes the account for that
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
    /// A reading of another version, or a refusal a day old, is a hint, and the account is tried; an entitlement that changes with no
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

    /// <summary>A reading from an older version never replaces a newer one; the tool's own home is a key of its own.</summary>
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
