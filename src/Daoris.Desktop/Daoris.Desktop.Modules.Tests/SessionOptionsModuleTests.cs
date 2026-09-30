using System.Text.Json;
using Daoris.Desktop.Modules;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// **One conversation's model and effort, over the bridge** (AGT6b, D98): what the conversation's agent
/// offered on the protocol door, asked for by the page, and a change to one of them.
/// </summary>
/// <remarks>
/// The conversation itself — its session, its options, the mode refused — is the driver's, and
/// <c>ProtocolChatTests</c> drives it against a stand-in agent. This holds the bridge's half: the shape the
/// page reads, and what a page asking early or of nothing is told.
/// </remarks>
public sealed class SessionOptionsModuleTests : Bridge
{
    private DriverModule Module() => new(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));

    /// <summary>A composer asks of every conversation it shows, so nothing to offer is an empty list, never a refusal.</summary>
    [Fact]
    public async Task A_conversation_nothing_here_holds_is_answered_with_no_options()
    {
        var answer = await AnswerAsync(Module(), "SESSION_OPTIONS", new { id = "nothing-here" });

        Assert.Equal("nothing-here", answer.GetProperty("session").GetString());
        Assert.Empty(answer.GetProperty("options").EnumerateArray());
    }

    /// <summary>A change needs a conversation the driver holds; before the driver is up, the sentence says so.</summary>
    [Fact]
    public async Task A_change_before_the_driver_is_up_says_so()
    {
        var refusal = await RefusalAsync(Module(), "SET_SESSION_OPTION", new { id = "s1", option = "model", value = "sonnet" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>
    /// The answer and the live event are one shape: each option's id, the agent's own name for it, its
    /// category, its value now, and the values it takes with their names and descriptions — carried as the
    /// agent gave them, never translated.
    /// </summary>
    [Fact]
    public void The_options_are_answered_in_the_agents_words_in_the_shape_the_page_reads()
    {
        var answer = JsonSerializer.SerializeToElement(
            DriverModule.OptionsAnswer("s1", [
                new AcpConfigOption("model", "Model", "model", "default",
                    [new AcpConfigChoice("default", "Default (recommended)", "the tool's default"), new AcpConfigChoice("sonnet", "Sonnet")]),
                new AcpConfigOption("effort", "Effort", "thought_level", "high", [new AcpConfigChoice("high", "High")]),
            ]),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal("s1", answer.GetProperty("session").GetString());
        var model = answer.GetProperty("options")[0];
        Assert.Equal("model", model.GetProperty("id").GetString());
        Assert.Equal("Model", model.GetProperty("name").GetString());
        Assert.Equal("model", model.GetProperty("category").GetString());
        Assert.Equal("default", model.GetProperty("current").GetString());
        var first = model.GetProperty("choices")[0];
        Assert.Equal("default", first.GetProperty("value").GetString());
        Assert.Equal("Default (recommended)", first.GetProperty("name").GetString());
        Assert.Equal("the tool's default", first.GetProperty("description").GetString());
        Assert.Equal("thought_level", answer.GetProperty("options")[1].GetProperty("category").GetString());
    }
}
