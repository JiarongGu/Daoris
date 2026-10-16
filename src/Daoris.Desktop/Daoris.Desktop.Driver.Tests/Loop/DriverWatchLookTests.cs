using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The watch's look, timed (FABRIC-EVENT2, the design §A2's correction to row 1): each look writes <c>watch.looked</c> to the
/// machine log with its ms and what woke it, the loop's start, its pace, a session's end or a nudge, and a nudge's topic and
/// subject where it carried one. A plain <see cref="DriverWatch.Nudge()"/> carries none, and means everything, as it always did.
/// </summary>
/// <remarks>
/// A torn <c>driver.json</c> fails each look before it asks the service anything, so no service stands in: the failure is
/// heard, a nudge given from it, and the next look runs at once.
/// </remarks>
public sealed class DriverWatchLookTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-look-" + Guid.NewGuid().ToString("N")[..8]);

    public DriverWatchLookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task Each_look_is_timed_in_the_machine_log_with_what_woke_it()
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, """{ "drivable": [], """);
        using (var log = new MachineLog(_home, "driver"))
        using (var client = new ServiceClient("http://127.0.0.1:9", null))
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            DriverWatch? watch = null;
            var heard = 0;
            watch = new DriverWatch(
                client, config, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), sync: null,
                harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
            {
                Log = log,
            };

            await watch.RunAsync(
                (_, _) => Task.CompletedTask,
                _ =>
                {
                    switch (++heard)
                    {
                        case 1: watch.Nudge(ChangeTopics.Requests, "requests"); break;
                        case 2: watch.Nudge(); break;
                        // FABRIC-EVENT2b: a terminal's words kept by a loop wake it by their topic, the session as the subject.
                        case 3: watch.Said("s1"); break;
                        default: stop.Cancel(); break;
                    }

                    return Task.CompletedTask;
                },
                stop.Token);
        }

        var looks = Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl")
            .SelectMany(File.ReadAllLines)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == "watch.looked")
            .Select(line => line.GetProperty("data"))
            .ToArray();

        Assert.Equal(["start", "nudge", "nudge", "nudge"], looks.Select(data => data.GetProperty("by").GetString()));
        Assert.Equal([null, "requests", null, "requests"], looks.Select(data => data.GetProperty("topic").GetString()));
        Assert.Equal([null, "requests", null, "s1"], looks.Select(data => data.GetProperty("subject").GetString()));
        // FABRIC-EVENT3a: a topic's wake runs the local plan alone; the start and a plain nudge, which means everything, the whole look.
        Assert.Equal(["look", "plan", "look", "plan"], looks.Select(data => data.GetProperty("part").GetString()));
        Assert.All(looks, data =>
        {
            Assert.True(data.GetProperty("failed").GetBoolean());
            Assert.True(data.GetProperty("ms").GetInt64() >= 0);
            Assert.True(data.GetProperty("nudges").GetInt32() >= 0);
        });
    }
}
