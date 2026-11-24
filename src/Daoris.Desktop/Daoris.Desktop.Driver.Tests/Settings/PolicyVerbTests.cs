using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris settings risk set</c> and <c>apply</c>, the terminal's doors to a change of the risk policy. An agent's set is a proposal pressed to
/// <c>POLICY_PROPOSE</c> (applied only on the owner's own turn naming it, <c>apply</c>); the owner's own terminal sets at once on the revision it read,
/// through <c>POLICY_SET</c> or, with no Daoris running, by the file; each says the route's answer and acts alone for no proposal.
/// </summary>
public sealed class PolicyVerbTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "policy-verb", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();

    public PolicyVerbTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private DriverChoicesWorld World(string? session = null, bool inAgentJob = false) => new()
    {
        Verbs = DriverChoiceVerbs.All,
        Instance = new InstanceWorld { Home = _home, Environment = name => name == DriverChoicesCommand.SessionVariable ? session : null, Out = _out, Error = new StringWriter(), Verb = "daoris" },
        ConfigPath = ConfigPath,
        InAgentJob = () => inAgentJob,
    };

    private ChoicePress Plan(string words, string? session = null, bool inAgentJob = false) =>
        DriverChoicesCommand.Plan(words.Split(' '), DriverConfig.Empty, World(session, inAgentJob));

    private static JsonElement Edit(ChoicePress press) => JsonSerializer.SerializeToElement(press.Payload).GetProperty("edits")[0];

    [Fact]
    public void An_agents_set_is_a_proposal_pressed_to_its_route_carrying_its_own_session()
    {
        var press = Plan("risk set repository/Aurora release.start owner --mode restricted", "s-ask");

        Assert.Equal("POLICY_PROPOSE", press.Route!.Route);
        var edit = Edit(press);
        Assert.Equal(("repository/Aurora", "release.start", "owner", "restricted"),
            (edit.GetProperty("place").GetString(), edit.GetProperty("act").GetString(), edit.GetProperty("class").GetString(), edit.GetProperty("mode").GetString()));
        Assert.Equal("s-ask", JsonSerializer.SerializeToElement(press.Payload).GetProperty("conversation").GetString());
        var explicitly = JsonSerializer.SerializeToElement(Plan("risk set machine line.land owner --conversation s-other", "s-ask").Payload);
        Assert.Equal("s-other", explicitly.GetProperty("conversation").GetString());
    }

    [Fact]
    public void The_owners_own_terminal_sets_through_its_route_at_the_revision_it_read()
    {
        var press = Plan("risk set machine line.land owner");

        Assert.Equal("POLICY_SET", press.Route!.Route);
        var payload = JsonSerializer.SerializeToElement(press.Payload);
        Assert.Equal(RiskPolicy.SeedRevision, payload.GetProperty("revision").GetString());
        Assert.Equal(("machine", "line.land", "owner"), (Edit(press).GetProperty("place").GetString(), Edit(press).GetProperty("act").GetString(), Edit(press).GetProperty("class").GetString()));
        Assert.False(payload.TryGetProperty("conversation", out _));
    }

    [Fact]
    public async Task With_no_daoris_running_the_owners_terminal_writes_the_row_and_journals_the_cli()
    {
        var press = Plan("risk set repository/Aurora spend.money owner");

        await press.Act(CancellationToken.None);

        var row = Assert.Single(RiskPolicy.Of(DriverConfig.Load(ConfigPath)).Rows["repository/Aurora"]);
        Assert.Equal((RiskActs.SpendMoney, RiskClasses.Owner), (row.Kind, row.Class));
        Assert.Equal(PolicyChangeLine.Cli, Assert.Single(PolicyChangeJournal.Read(_home)).Door);
    }

    [Fact]
    public async Task A_process_in_an_agents_job_with_no_session_sets_nothing()
    {
        var press = Plan("risk set machine line.land owner", inAgentJob: true);

        await Assert.ThrowsAsync<ChoiceWords>(() => press.Act(CancellationToken.None));
        Assert.False(File.Exists(ConfigPath));
    }

    [Fact]
    public void A_cast_and_an_access_mode_are_set_by_their_own_words()
    {
        var cast = Edit(Plan("risk set repository/Aurora cast reviewer agent:codex"));
        var back = Edit(Plan("risk set repository/Aurora cast reviewer default"));
        var access = Edit(Plan("risk set machine access permissions=auto routing=intake"));

        Assert.Equal(("cast", "reviewer", "agent:codex"), (cast.GetProperty("subject").GetString(), cast.GetProperty("act").GetString(), cast.GetProperty("by").GetString()));
        Assert.Equal(JsonValueKind.Null, back.GetProperty("by").ValueKind);
        Assert.Equal(("access", "permissions=auto;routing=intake"), (access.GetProperty("subject").GetString(), access.GetProperty("class").GetString()));
        Assert.Throws<ChoiceWords>(() => Plan("risk set machine cast reviewer"));
    }

    [Fact]
    public void Remove_takes_a_row_away_and_a_role_row_names_its_role()
    {
        var removed = Edit(Plan("risk set machine line.land remove"));
        var role = Edit(Plan("risk set machine spend.money role --by supervisor"));

        Assert.Equal(JsonValueKind.Null, removed.GetProperty("class").ValueKind);
        Assert.Equal("supervisor", role.GetProperty("by").GetString());
    }

    [Fact]
    public void Apply_presses_the_id_and_digest_and_says_a_refusal_with_exit_one()
    {
        var press = Plan("risk apply pc-0123456 0123456789ab");
        Assert.Equal("POLICY_APPLY", press.Route!.Route);
        var payload = JsonSerializer.SerializeToElement(press.Payload);
        Assert.Equal(("pc-0123456", "0123456789ab"), (payload.GetProperty("proposal").GetString(), payload.GetProperty("digest").GetString()));

        var answer = JsonDocument.Parse("""{"done":false,"code":"no-owner-turn","message":"no turn of the owner's own names it"}""").RootElement;
        Assert.Equal(1, press.Words(press.Fields(answer)));
        Assert.Contains("no turn of the owner's own names it", _out.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("risk set machine line.land")]
    [InlineData("risk set machine line.land sometimes")]
    [InlineData("risk set machine policy.change none")]
    [InlineData("risk apply pc-0123456")]
    [InlineData("risk propose machine line.land owner")]
    public void Words_that_name_no_edit_or_no_proposal_are_refused_with_the_usage(string words)
    {
        var refused = Assert.Throws<ChoiceWords>(() => Plan(words));
        Assert.Contains("usage:", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_daoris_running_apply_acts_on_nothing_and_writes_nothing()
    {
        var press = Plan("risk apply pc-0123456 0123456789ab");

        await Assert.ThrowsAsync<ChoiceWords>(() => press.Act(CancellationToken.None));
        Assert.False(File.Exists(ConfigPath));
    }

    [Fact]
    public void Reading_prints_the_act_table_the_window_reads_and_why_names_the_rule_a_wait_stood_on()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, """{"risk":{"repository/Aurora":[{"kind":"spend.money","class":"owner"}]}}""");
        var config = DriverConfig.Load(ConfigPath);
        var at = new DateTimeOffset(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
        RiskJournal.Append(_home, new RiskJudged(
            at, RiskJudged.Opened, "k1", "unchecked done", RiskActs.WorkDone, null, "agent", "work", "Aurora", RiskPlace.RestrictedMode, "q1", "holds", "subject",
            RiskVerdict.Pass, RiskClasses.None, null, RiskVerdict.Seed, "seed", "no risk declared: it passes, recorded (the seed)", "Waiting on the person", true));

        var verb = new PlaceRiskVerb { Acts = (choices, home, _, workspace, repository) => RiskActTable.Lines(RiskActTable.Of(choices, home, at, workspace, repository)), Verdicts = RiskWhy.Said };
        var world = World();
        verb.Said(["risk", "work", "--repository", "Aurora"], config, world);
        var said = _out.ToString();
        verb.Said(["risk", "why", "q1", "holds"], config, world);

        Assert.Contains("spend.money", said, StringComparison.Ordinal);
        Assert.Contains("repository/Aurora", said, StringComparison.Ordinal);
        Assert.Contains("policy.change", said, StringComparison.Ordinal);
        var why = _out.ToString()[said.Length..];
        Assert.Contains("rule seed at seed", why, StringComparison.Ordinal);
        Assert.Contains("no risk declared: it passes, recorded (the seed)", why, StringComparison.Ordinal);
    }
}
