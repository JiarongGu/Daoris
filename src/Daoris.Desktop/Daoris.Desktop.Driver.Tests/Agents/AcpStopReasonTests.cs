using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class AcpStopReasonTests
{
    [Theory]
    [InlineData("""{"stopReason":"end_turn"}""", "end_turn")]
    [InlineData("""{"stopReason":7}""", "unknown")]
    [InlineData("""{"stopReason":null}""", "unknown")]
    [InlineData("""{}""", "unknown")]
    [InlineData("null", "unknown")]
    [InlineData("\"done\"", "unknown")]
    public void A_prompt_answer_s_stop_reason_is_its_text_or_unknown_whatever_the_answer_s_shape(string result, string expected)
    {
        using var document = JsonDocument.Parse(result);

        Assert.Equal(expected, AcpProtocol.StopReason(document.RootElement));
    }

    [Fact]
    public void An_answer_with_no_result_at_all_is_unknown()
    {
        Assert.Equal("unknown", AcpProtocol.StopReason(default));
    }
}
