namespace Daoris.Service.Tests;

/// <summary>MIXAGENT2a-i: what a session reads of the helper tools says what each does and what it leaves to the person's rule.</summary>
public sealed partial class McpToolDescriptionTests
{
    [Fact]
    public void The_helper_tools_say_the_answer_is_another_agents_claim_and_the_rule_is_the_persons()
    {
        var ask = string.Join(" ", Descriptions().Where(d => d.Tool == "helper_ask").Select(d => d.Text));
        var answer = string.Join(" ", Descriptions().Where(d => d.Tool == "helper_answer").Select(d => d.Text));

        Assert.Contains("another agent's claim", ask);
        Assert.Contains("person's rule", ask);
        Assert.Contains("changes nothing", ask);
        Assert.Contains("claim", answer);
        Assert.Contains("once", answer);
    }
}
