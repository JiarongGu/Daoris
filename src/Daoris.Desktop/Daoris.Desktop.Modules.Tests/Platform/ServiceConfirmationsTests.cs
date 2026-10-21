using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The presses waiting at the service's door, told to the windows (NOTIFY2j, D196 point 4, the notification design §2.5): the
/// desktop's host holds one wait on the service and tells every window what waits at each change, beside the pipe's, so no window
/// polls. An answer that holds what was told tells nothing; an older service, with no held door, has its list read here.
/// </summary>
public sealed class ServiceConfirmationsTests : Bridge
{
    private const string First = """{"id":"a1b2c3d4e5f60718","state":"waiting","method":"POST","path":"/api/asks","route":"/api/asks","act":"an ask","values":[],"fields":[],"body":null,"asked":"2026-10-22T12:00:00+00:00","expires":"2026-10-22T12:02:00+00:00"}""";

    private const string Second = """{"id":"0f1e2d3c4b5a6978","state":"waiting","method":"DELETE","path":"/api/quests/q1","route":"/api/quests/{id}","act":"a quest's deletion","values":[{"name":"id","type":"string","value":"q1"}],"fields":[],"body":null,"asked":"2026-10-22T12:01:00+00:00","expires":"2026-10-22T12:03:00+00:00"}""";

    private static string Answered(string since, params string[] waiting) => $$"""{"since":"{{since}}","waiting":[{{string.Join(",", waiting)}}]}""";

    [Fact]
    public async Task What_waits_is_told_at_each_change_once_from_one_held_wait()
    {
        var doors = new Doors(
            (200, Answered("e1.1", First)),
            // A restarted service's new cursor with the same list: nothing moved, nothing told.
            (200, Answered("f2.0", First)),
            (200, Answered("f2.1", First, Second)));
        using var service = new ServiceClient(doors);
        var confirmations = new ServiceConfirmations(Bus);
        Assert.Equal(new ServiceConfirmationsNow(0, []), confirmations.Now());

        using var stop = new CancellationTokenSource();
        var run = confirmations.RunAsync(service.Confirmations, stop.Token);
        await doors.Drained.WaitAsync(Poll.Patience);
        await stop.CancelAsync();
        await run.WaitAsync(Poll.Patience);

        Assert.Equal(
            ["/api/confirmations/waiting?wait=20", "/api/confirmations/waiting?wait=20&since=e1.1", "/api/confirmations/waiting?wait=20&since=f2.0",
             "/api/confirmations/waiting?wait=20&since=f2.1"],
            doors.Asked);
        var told = Raised.Where(each => each.Type == ServiceConfirmations.Event).Select(Told).ToList();
        Assert.Equal([(1L, "a1b2c3d4e5f60718"), (2L, "a1b2c3d4e5f60718,0f1e2d3c4b5a6978")], told);
        Assert.Equal(2, confirmations.Now().Seq);
        Assert.Equal("a quest's deletion", confirmations.Now().Waiting[1].GetProperty("act").GetString());
    }

    [Fact]
    public async Task An_empty_first_answer_is_told_so_a_window_knows_none_waits()
    {
        var doors = new Doors((200, Answered("e1.0")));
        using var service = new ServiceClient(doors);
        var confirmations = new ServiceConfirmations(Bus);

        using var stop = new CancellationTokenSource();
        var run = confirmations.RunAsync(service.Confirmations, stop.Token);
        await doors.Drained.WaitAsync(Poll.Patience);
        await stop.CancelAsync();
        await run.WaitAsync(Poll.Patience);

        Assert.Equal([(1L, "")], Raised.Where(each => each.Type == ServiceConfirmations.Event).Select(Told).ToList());
    }

    [Fact]
    public async Task An_older_service_has_its_list_read_here_for_every_window()
    {
        var doors = new Doors((404, """{"error":"not found"}"""), (200, $"[{First}]"));
        using var service = new ServiceClient(doors);
        var confirmations = new ServiceConfirmations(Bus);

        using var stop = new CancellationTokenSource();
        var run = confirmations.RunAsync(service.Confirmations, stop.Token);
        // Told once its list is read; the next read is its pace away, which the test does not wait for.
        await doors.Given.WaitAsync(Poll.Patience);
        var until = DateTime.UtcNow + Poll.Patience;
        while (confirmations.Now().Seq == 0)
        {
            Assert.True(DateTime.UtcNow < until, "the older service's list was never told");
            await Task.Delay(10);
        }
        await stop.CancelAsync();
        await run.WaitAsync(Poll.Patience);

        Assert.Equal(["/api/confirmations/waiting?wait=20", "/api/confirmations"], doors.Asked);
        Assert.Equal([(1L, "a1b2c3d4e5f60718")], Raised.Where(each => each.Type == ServiceConfirmations.Event).Select(Told).ToList());
    }

    private static (long Seq, string Ids) Told(Shenora.Core.Events.EventMessage message)
    {
        var told = JsonSerializer.SerializeToElement(message.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return (told.GetProperty("seq").GetInt64(), string.Join(",", told.GetProperty("waiting").EnumerateArray().Select(each => each.GetProperty("id").GetString())));
    }

    /// <summary>The service's doors, answering each ask in turn, then holding every later one until it is cancelled.</summary>
    private sealed class Doors(params (int Status, string Payload)[] answers) : IServiceDoors
    {
        private readonly Queue<(int Status, string Payload)> _answers = new(answers);
        private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _given = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<string> _asked = [];

        public string BaseUrl => "http://127.0.0.1:1";

        /// <summary>Every answer given, and the next ask held.</summary>
        public Task Drained => _drained.Task;

        /// <summary>The last answer taken.</summary>
        public Task Given => _given.Task;

        public IReadOnlyList<string> Asked
        {
            get
            {
                lock (_asked) return [.. _asked];
            }
        }

        public Task<string> GetAsync(string path, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();

        public async Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Assert.Equal(ServiceVerb.Get, verb);
            (int Status, string Payload) answer;
            lock (_asked)
            {
                _asked.Add(path);
                if (!_answers.TryDequeue(out answer))
                {
                    _drained.TrySetResult();
                    answer = (0, "");
                }
                else if (_answers.Count == 0)
                {
                    _given.TrySetResult();
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
