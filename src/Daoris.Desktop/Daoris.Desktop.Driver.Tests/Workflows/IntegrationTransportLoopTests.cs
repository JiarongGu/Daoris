using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The transport's loop outlives a failure it did not foresee: one line's run that throws leaves the next line's run to go on.</summary>
public sealed class IntegrationTransportLoopTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "integration-loop", Guid.NewGuid().ToString("N")[..8]);

    public IntegrationTransportLoopTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public async Task A_line_whose_root_read_fails_with_a_transport_error_does_not_end_the_loop()
    {
        var said = new List<string>();
        var asked = 0;
        await using var transport = new IntegrationTransport(_home, new IntegrationTransportWorld
        {
            Settled = () => StoreSettled.Yes,
            Root = (_, _, _) => Interlocked.Increment(ref asked) == 1 ? throw new HttpRequestException("the service is down") : Task.FromResult<string?>(null),
            Said = line =>
            {
                lock (said) said.Add(line);
            },
        });

        transport.Send(new IntegrationLineSend("engine", "aurora", "main"));
        await WaitAsync(() => Volatile.Read(ref asked) >= 1);
        transport.Send(new IntegrationLineSend("engine", "aurora", "dev"));

        Assert.True(await WaitAsync(() => Volatile.Read(ref asked) >= 2), "the loop ended with the first line's failure, so the second line never ran");
        Assert.Contains(said.ToArray(), line => line.Contains("service is down", StringComparison.Ordinal));
    }

    private static async Task<bool> WaitAsync(Func<bool> done)
    {
        for (var tries = 0; tries < 100 && !done(); tries++) await Task.Delay(50);
        return done();
    }
}
