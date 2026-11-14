using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class CheckOutputProcessTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("check-output");

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task The_broker_log_keeps_the_gates_standard_output_and_error_before_its_verdict()
    {
        File.WriteAllText(_fixture.At("says.mjs"), "console.log('gate stdout'); console.error('gate stderr'); process.exit(1);\n");
        var files = new CheckFiles(_fixture.Home);
        files.WriteAsk(new CheckAsk("a1", "s1", null,
            ["node", _fixture.At("says.mjs")], CheckAsk.ByAgent, DateTimeOffset.UtcNow));
        var broker = new CheckBroker(_fixture.Home, _ => _fixture.Tree);

        var state = await broker.RunNextAsync(CancellationToken.None);

        Assert.Equal(CheckStates.Failed, state!.State);
        Assert.Contains("gate stdout", state.Tail);
        Assert.Contains("gate stderr", state.Tail);
        Assert.Contains("gate stdout", files.ReadLog("a1"));
    }
}
