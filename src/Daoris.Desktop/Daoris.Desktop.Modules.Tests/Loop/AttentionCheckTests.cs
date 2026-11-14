using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

public sealed class AttentionCheckTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-14T04:39:00Z");
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "attention-check-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void A_failed_check_of_a_done_quest_names_its_check_and_commit_in_the_persons_workspace()
    {
        var facts = Facts("Done");

        var row = AttentionStateSlice.Picked(Assert.Single(NeedsAPerson.Rows(facts)));

        Assert.Equal("check-failed", row["kind"]!.GetValue<string>());
        Assert.Equal("a1", row["id"]!.GetValue<string>());
        Assert.Equal("s1", row["session"]!.GetValue<string>());
        Assert.Equal("studio", row["workspace"]!.GetValue<string>());
        Assert.Equal("engine", row["where"]!.GetValue<string>());
        Assert.Equal("tests", row["check"]!["name"]!.GetValue<string>());
        Assert.Equal("abc123", row["check"]!["tip"]!.GetValue<string>());
        Assert.Equal("boom", row["check"]!["tail"]!.GetValue<string>());
        Assert.Equal(Moments.Write(At), row["since"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("Taken", CheckStates.Failed, null)]
    [InlineData("Done", CheckStates.Reached, null)]
    [InlineData("Done", CheckStates.Running, null)]
    [InlineData("Done", CheckStates.Errored, null)]
    [InlineData("Done", CheckStates.Failed, "handed")]
    public void A_check_the_person_is_not_owed_is_not_an_attention_row(string status, string state, string? handed)
    {
        var facts = Facts(status, state, handed);

        Assert.Empty(NeedsAPerson.Rows(facts).Where(row => row["kind"]!.GetValue<string>() == "check-failed"));
    }

    private AttentionFacts Facts(string status, string state = CheckStates.Failed, string? handed = null)
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(new CheckAsk("a1", "s1", "tests", null, CheckAsk.ByDaoris, At));
        files.WriteState(new CheckState("a1", state, At) { Tip = "abc123", Tail = "boom", Handed = handed });
        return new AttentionFacts
        {
            Sessions = [JsonNode.Parse("""{"id":"s1","quest":"q1","repository":"engine","workspace":"studio","state":"completed"}""")!.AsObject()],
            Quests = [JsonNode.Parse($$"""{"id":"q1","status":"{{status}}","workspace":"studio"}""")!.AsObject()],
            Checks = CheckVerdict.Pending(_home),
        };
    }
}
