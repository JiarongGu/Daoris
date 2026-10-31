using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// KNOWCENTRAL2e: the memory changes that wait on the person are read from the service on the driver's poll and held in memory; a change
/// in what waits is told once, an unchanged or failed read tells nothing, and the last answer stands through a failure.
/// </summary>
public sealed class MemoryWaitsTests : Bridge
{
    private const string One = """{"session":"s1","ends":1,"replacements":0,"staged":0,"since":"2026-10-31T09:00:00+00:00"}""";

    private const string Two = """{"session":"s2","ends":0,"replacements":0,"staged":2,"since":"2026-10-31T09:05:00+00:00"}""";

    [Fact]
    public async Task What_waits_is_told_when_it_changes_and_held_through_a_failed_read()
    {
        var doors = new Doors((200, $$"""{"waits":[{{One}}]}"""), (200, $$"""{"waits":[{{One}}]}"""), (500, "down"), (200, $$"""{"waits":[{{One}},{{Two}}]}"""));
        using var service = new ServiceClient(doors);
        var waits = new MemoryWaits(Bus) { Pace = TimeSpan.FromMilliseconds(5) };

        using var stop = new CancellationTokenSource();
        var run = waits.RunAsync(service, stop.Token);
        await doors.Drained.WaitAsync(Poll.Patience);
        await stop.CancelAsync();
        await run.WaitAsync(Poll.Patience);

        Assert.Equal(2, Raised.Count(each => each.Type == MemoryWaits.Event));
        Assert.Equal(["s1", "s2"], waits.Now().Select(each => each.GetProperty("session").GetString()));
        Assert.Equal(2, waits.Now()[1].GetProperty("staged").GetInt32());
    }

    [Fact]
    public async Task A_service_with_no_door_holds_nothing()
    {
        var doors = new Doors((404, """{"error":"not found"}"""));
        using var service = new ServiceClient(doors);

        Assert.Empty(await service.MemoryWaitsAsync());
    }

    /// <summary>The service's doors, answering each ask in turn, then holding every later one until it is cancelled.</summary>
    private sealed class Doors(params (int Status, string Payload)[] answers) : IServiceDoors
    {
        private readonly Queue<(int Status, string Payload)> _answers = new(answers);
        private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string BaseUrl => "http://127.0.0.1:1";

        /// <summary>Every answer given, and the next ask held.</summary>
        public Task Drained => _drained.Task;

        public Task<string> GetAsync(string path, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();

        public async Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Assert.Equal((ServiceVerb.Get, "/api/memory/waits"), (verb, path));
            (int Status, string Payload) answer;
            lock (_answers)
            {
                if (!_answers.TryDequeue(out answer))
                {
                    _drained.TrySetResult();
                    answer = (0, "");
                }
            }

            if (answer.Status == 0) await Task.Delay(Timeout.Infinite, ct);
            return new ServiceReply(answer.Status, answer.Payload);
        }

        public void Dispose()
        {
        }
    }
}
