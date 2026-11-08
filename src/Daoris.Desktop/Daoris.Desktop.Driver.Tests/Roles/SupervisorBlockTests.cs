using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>An agent supervisor's answer is the one fenced block of its last message, in the decision door's form less its generation and proof.</summary>
public sealed class SupervisorBlockTests
{
    private static string Block(string body) => $"I read the record.\n\n{SupervisorBlock.Opening}\n{body}\n{SupervisorBlock.Closing}\n";

    [Fact]
    public void A_decide_and_a_leave_read_and_their_subject_is_the_kind_s_typed_reference()
    {
        var (answer, _) = SupervisorBlock.Read(Block("""
            { "kind": "question", "subject": { "session": "s-1a2b3c4d", "question": "q-7" }, "do": "answer", "choice": "Keep the old name",
              "reasons": [ { "says": "The rename is the owner's to choose.", "source": "docs/decisions/D201.md point 3" } ] }
            """));
        var (leave, _) = SupervisorBlock.Read(Block("""
            { "kind": "course", "subject": { "session": "s-1a2b3c4d", "point": "p-2" }, "leave": "preference", "reasons": [ { "says": "Taste.", "source": "count:answers-alone" } ] }
            """));

        Assert.Equal((SupervisorSubject.Question("s-1a2b3c4d", "q-7"), "answer", "Keep the old name"), (answer!.Subject, answer.Do, answer.Choice));
        Assert.Equal((SupervisorSubject.Point("s-1a2b3c4d", "p-2"), "preference", (string?)null), (leave!.Subject, leave.Leave, leave.Do));
    }

    [Theory]
    [InlineData("no block here")]
    [InlineData("<<< supervisor decision\n{ \"kind\": \"question\" }\n>>>\n<<< supervisor decision\n{ }\n>>>")]
    [InlineData("<<< supervisor decision\n{ \"kind\": \"question\", \"subject\": { \"session\": \"s1\", \"question\": \"q1\" }, \"do\": \"answer\", \"leave\": \"preference\", \"reasons\": [ { \"says\": \"x\", \"source\": \"y\" } ] }\n>>>")]
    [InlineData("<<< supervisor decision\n{ \"kind\": \"question\", \"subject\": { \"session\": \"s1\", \"question\": \"q1\" }, \"leave\": \"whenever\", \"reasons\": [ { \"says\": \"x\", \"source\": \"y\" } ] }\n>>>")]
    [InlineData("<<< supervisor decision\n{ \"kind\": \"question\", \"subject\": { \"session\": \"s1\", \"question\": \"q1\" }, \"do\": \"answer\", \"reasons\": [] }\n>>>")]
    [InlineData("<<< supervisor decision\n{ \"kind\": \"weather\", \"subject\": { \"session\": \"s1\" }, \"do\": \"answer\", \"reasons\": [ { \"says\": \"x\", \"source\": \"y\" } ] }\n>>>")]
    [InlineData("<<< supervisor decision\n{ \"kind\": \"question\", \"subject\": { \"session\": \"s1\", \"question\": \"q1\" }, \"do\": \"answer\", \"reasons\": [ { \"says\": \"x\", \"source\": \"y\" } ] }\n>>>")]
    [InlineData("<<< supervisor decision\n{ \"kind\": \"course\", \"subject\": { \"session\": \"s1\", \"point\": \"p1\" }, \"do\": \"go-on\", \"choice\": \"Keep\", \"reasons\": [ { \"says\": \"x\", \"source\": \"y\" } ] }\n>>>")]
    [InlineData("<<< supervisor decision\n{ not a form\n>>>")]
    [InlineData("<<< supervisor decision\n{ \"kind\": \"question\" }")]
    public void A_block_that_does_not_read_is_none_and_says_why(string message)
    {
        var (block, why) = SupervisorBlock.Read(message);

        Assert.Null(block);
        Assert.False(string.IsNullOrWhiteSpace(why));
    }
}
