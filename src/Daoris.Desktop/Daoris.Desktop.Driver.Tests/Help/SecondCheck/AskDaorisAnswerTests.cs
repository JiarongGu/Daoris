using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A reader's one block read as a reading: findings with their severity, certainty, call and the person's words; nothing found as an
/// empty list; a leave by one to five reasons; and every block out of shape refused with why, so its range closes unread.
/// </summary>
public sealed class AskDaorisAnswerTests
{
    private static readonly HashSet<string> Calls = ["c1", "c2", "p1"];

    private static readonly HashSet<string> Words = ["w1", "w2"];

    private static string Block(string body) =>
        $"I read the packet.\n\n{SupervisorBlock.Opening}\n{{ \"kind\": \"ask-daoris\", \"subject\": {{ \"session\": \"s1\", \"range\": \"r-1\" }}, {body} }}\n{SupervisorBlock.Closing}";

    private static string Finding(string severity = "flag", string certainty = "likely", string call = "c2", string instruction = "w1") =>
        $"{{ \"severity\": \"{severity}\", \"certainty\": \"{certainty}\", \"says\": \"It pushed a branch the person did not ask for.\", \"call\": \"{call}\", \"instruction\": \"{instruction}\" }}";

    [Fact]
    public void A_report_s_findings_read_with_their_severity_certainty_call_and_the_person_s_words()
    {
        var (answer, why) = AskDaorisReport.Read(Block($"\"do\": \"report\", \"findings\": [ {Finding()}, {Finding("note", "unsure", "p1", "none")} ], \"more\": 2"), Calls, Words);

        Assert.True(answer is not null, why);
        Assert.Equal(new SupervisorSubject(SupervisorKinds.AskDaoris, "s1", "r-1"), answer.Subject);
        Assert.Equal([("flag", "likely", "c2", "w1"), ("note", "unsure", "p1", "none")], answer.Findings.Select(each => (each.Severity, each.Certainty, each.Call, each.Instruction)));
        Assert.Equal(2, answer.More);
        Assert.Null(answer.Leave);
    }

    [Fact]
    public void An_empty_list_says_nothing_found()
    {
        var (answer, _) = AskDaorisReport.Read(Block("\"do\": \"report\", \"findings\": []"), Calls, Words);

        Assert.NotNull(answer);
        Assert.Empty(answer.Findings);
        Assert.Equal(0, answer.More);
    }

    [Fact]
    public void A_leave_reads_by_its_bound_and_one_to_five_reasons()
    {
        var (answer, _) = AskDaorisReport.Read(Block("\"leave\": \"preference\", \"reasons\": [ { \"says\": \"The packet cut every output.\", \"source\": \"count:limits\" } ]"));

        Assert.NotNull(answer);
        Assert.Equal("preference", answer.Leave);
        Assert.Single(answer.Reasons);
        Assert.Null(AskDaorisReport.Read(Block("\"leave\": \"preference\", \"reasons\": []")).Answer);
    }

    [Theory]
    [InlineData("\"do\": \"answer\", \"findings\": []", "no act of a reading")]
    [InlineData("\"do\": \"report\", \"findings\": [ SEVERITY ]", "severity is one of")]
    [InlineData("\"do\": \"report\", \"findings\": [ CERTAINTY ]", "certainty is one of")]
    [InlineData("\"do\": \"report\", \"findings\": [ CALL ]", "names call `c9`")]
    [InlineData("\"do\": \"report\", \"findings\": [ WORDS ]", "names the person's words `w7`")]
    [InlineData("\"do\": \"report\", \"findings\": [], \"more\": -1", "below zero")]
    [InlineData("\"do\": \"report\", \"findings\": [ NINE ]", "more than 8 findings")]
    public void A_block_out_of_shape_is_refused_with_why(string body, string why)
    {
        body = body.Replace("SEVERITY", Finding(severity: "warning"), StringComparison.Ordinal)
            .Replace("CERTAINTY", Finding(certainty: "certain"), StringComparison.Ordinal)
            .Replace("CALL", Finding(call: "c9"), StringComparison.Ordinal)
            .Replace("WORDS", Finding(instruction: "w7"), StringComparison.Ordinal)
            .Replace("NINE", string.Join(", ", Enumerable.Repeat(Finding(), 9)), StringComparison.Ordinal);

        var (answer, said) = AskDaorisReport.Read(Block(body), Calls, Words);

        Assert.Null(answer);
        Assert.Contains(why, said, StringComparison.Ordinal);
    }

    [Fact]
    public void Words_with_no_block_or_a_decision_s_block_are_no_reading()
    {
        Assert.Contains("no supervisor decision block", AskDaorisReport.Read("I could not read it.").Why, StringComparison.Ordinal);
        var question = $"{SupervisorBlock.Opening}\n{{ \"kind\": \"question\", \"subject\": {{ \"session\": \"s1\", \"question\": \"q\" }}, \"do\": \"report\", \"findings\": [] }}\n{SupervisorBlock.Closing}";
        Assert.Contains("no reading of Ask Daoris's acts", AskDaorisReport.Read(question).Why, StringComparison.Ordinal);
    }
}
