using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The step's own options reach its agent: the node's options as the graph writes them (an auto-complete the pull-request step asks for,
/// its merge strategy), told in the session's words and kept beside the attempt as its ask, so the agent passes them to the plugin's
/// tool rather than losing what the plugin's own <c>work/perform</c> would have been given.
/// </summary>
public sealed class StepAgentOptionsTests
{
    private static StepAgentAsk Ask(IReadOnlyDictionary<string, string>? options = null) =>
        new("inv-1", "run:v-1", "pr", 1, "daoris-azure-devops", "pull-request", "Open the pull request.", "engine", null, "feature/x",
            "https://dev.azure.com/acme/Proj/_git/app", "main", "0123456789abcdef0123456789abcdef01234567")
        {
            Options = options ?? new Dictionary<string, string>(StringComparer.Ordinal),
        };

    [Fact]
    public void A_step_with_options_tells_them_to_its_agent_as_the_graph_writes_them()
    {
        var prompt = Ask(new Dictionary<string, string>(StringComparer.Ordinal) { ["autoComplete"] = "true", ["mergeStrategy"] = "\"squash\"" }).Prompt();

        Assert.Contains("- the step's options: {\"autoComplete\":true,\"mergeStrategy\":\"squash\"}", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_step_with_none_says_none_are_given_and_the_ask_keeps_them_across_its_file()
    {
        Assert.Contains("- the step's options: (none given)", Ask().Prompt(), StringComparison.Ordinal);

        var kept = StepAgentAsk.Read(Ask(new Dictionary<string, string>(StringComparer.Ordinal) { ["autoComplete"] = "true" }).Write());

        Assert.Equal("true", kept!.Options["autoComplete"]);
    }

    [Fact]
    public async Task The_ask_composed_for_an_attempt_carries_the_node_s_options()
    {
        var node = new NodeDeclaration("pr", "daoris-azure-devops/pull-request", 1, NodeReach.Performed)
        {
            Goal = AuthoredText.Of("Open the pull request."),
            Options = new Dictionary<string, string>(StringComparer.Ordinal) { ["autoComplete"] = "true" },
        };
        var attempt = new NodeAttempt("pr", 1, "daoris-azure-devops",
            new WorkPerformCall("run:v-1", "daoris-azure-devops/pull-request", 1, "inv-1", "k-1", "0123456789abcdef0123456789abcdef01234567", [], DateTimeOffset.UtcNow.AddMinutes(5)),
            AttemptStates.Recorded);
        var told = new WatchedUnit("engine", "games", "", "feature/x", "0123456789abcdef0123456789abcdef01234567", "main", null, ["pull-request"]);

        var ask = await StepAsks.OfAsync("run:v-1", attempt, node, told, landing: null, CancellationToken.None);

        Assert.Equal("true", ask!.Options["autoComplete"]);
    }
}
