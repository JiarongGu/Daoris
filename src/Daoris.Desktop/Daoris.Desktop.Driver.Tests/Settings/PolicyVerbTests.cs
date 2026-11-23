using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris settings risk propose</c> and <c>apply</c>, the assistant's terminal door to a change of the risk policy: each presses its route with
/// the edit or the proposal's id and digest, says the route's answer, and acts alone for none (a change to the policy is proposed and confirmed with Daoris running).
/// </summary>
public sealed class PolicyVerbTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "policy-verb", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();

    public PolicyVerbTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private DriverChoicesWorld World(string? session = null) => new()
    {
        Verbs = DriverChoiceVerbs.All,
        Instance = new InstanceWorld { Home = _home, Environment = name => name == DriverChoicesCommand.SessionVariable ? session : null, Out = _out, Error = new StringWriter(), Verb = "daoris" },
        ConfigPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig),
    };

    private ChoicePress Plan(string words, string? session = null) => DriverChoicesCommand.Plan(words.Split(' '), DriverConfig.Empty, World(session));

    [Fact]
    public void Propose_presses_one_edit_to_its_route()
    {
        var press = Plan("risk propose repository/Aurora release.start owner --mode restricted");

        Assert.Equal("POLICY_PROPOSE", press.Route!.Route);
        var edit = Assert.Single(JsonSerializer.SerializeToElement(press.Payload).GetProperty("edits").EnumerateArray());
        Assert.Equal(("repository/Aurora", "release.start", "owner", "restricted"),
            (edit.GetProperty("place").GetString(), edit.GetProperty("act").GetString(), edit.GetProperty("class").GetString(), edit.GetProperty("mode").GetString()));
    }

    [Fact]
    public void Propose_carries_the_terminals_own_session_so_a_card_press_names_the_proposing_conversation()
    {
        var named = JsonSerializer.SerializeToElement(Plan("risk propose machine line.land owner", "s-ask").Payload);
        var alone = JsonSerializer.SerializeToElement(Plan("risk propose machine line.land owner").Payload);
        var explicitly = JsonSerializer.SerializeToElement(Plan("risk propose machine line.land owner --conversation s-other", "s-ask").Payload);

        Assert.Equal("s-ask", named.GetProperty("conversation").GetString());
        Assert.False(alone.TryGetProperty("conversation", out _));
        Assert.Equal("s-other", explicitly.GetProperty("conversation").GetString());
    }

    [Fact]
    public void Remove_takes_a_row_away_and_a_role_row_names_its_role()
    {
        var removed = JsonSerializer.SerializeToElement(Plan("risk propose machine line.land remove").Payload).GetProperty("edits")[0];
        var role = JsonSerializer.SerializeToElement(Plan("risk propose machine spend.money role --by supervisor").Payload).GetProperty("edits")[0];

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
    [InlineData("risk propose machine line.land")]
    [InlineData("risk propose machine line.land sometimes")]
    [InlineData("risk propose machine policy.change none")]
    [InlineData("risk apply pc-0123456")]
    public void Words_that_name_no_edit_or_no_proposal_are_refused_with_the_usage(string words)
    {
        var refused = Assert.Throws<ChoiceWords>(() => Plan(words));
        Assert.Contains("usage:", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_daoris_running_neither_acts_and_nothing_is_written()
    {
        var press = Plan("risk apply pc-0123456 0123456789ab");

        await Assert.ThrowsAsync<ChoiceWords>(() => press.Act(CancellationToken.None));
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.DriverConfig)));
    }
}
