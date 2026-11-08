using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The posture is read from an agent's answer as untrusted: a shape that is not the wire's names no modes and throws nothing.</summary>
public sealed class AcpPostureTests
{
    private static JsonElement Answer(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<List<string>> SetAsync(string posture, string json)
    {
        var asked = new List<string>();
        var posturing = new AcpPosture(
            posture, () => null,
            (method, parameters, _) =>
            {
                asked.Add($"{method} {JsonSerializer.Serialize(parameters)}");
                return Task.FromResult(default(JsonElement));
            },
            () => "s1");
        await posturing.SetAsync(Answer(json), CancellationToken.None);
        return asked;
    }

    [Theory]
    [InlineData("""{"modes":null}""")]
    [InlineData("""{"modes":"auto"}""")]
    [InlineData("""{"modes":{"availableModes":[1,null,"auto",{"id":5},{"id":null}]}}""")]
    public async Task A_modes_answer_of_another_shape_names_no_modes_and_a_lenient_posture_asks_nothing(string json) =>
        Assert.Empty(await SetAsync("acceptEdits", json));

    [Theory]
    [InlineData("""{"modes":null}""")]
    [InlineData("""{"modes":[]}""")]
    [InlineData("""{"modes":{"availableModes":[1,{"id":5}]}}""")]
    public async Task A_required_posture_on_such_an_answer_is_refused_in_words_not_thrown_as_a_shape_error(string json)
    {
        var refused = await Assert.ThrowsAsync<DriverException>(() => SetAsync("!bypassPermissions", json));
        Assert.Contains("offers no modes", refused.Message);
    }

    [Fact]
    public async Task A_current_mode_that_is_no_string_is_not_the_posture_and_the_posture_is_set()
    {
        var asked = await SetAsync("acceptEdits", """{"modes":{"currentModeId":7,"availableModes":[{"id":"acceptEdits"}]}}""");

        Assert.Equal(["session/set_mode {\"sessionId\":\"s1\",\"modeId\":\"acceptEdits\"}"], asked);
    }
}
