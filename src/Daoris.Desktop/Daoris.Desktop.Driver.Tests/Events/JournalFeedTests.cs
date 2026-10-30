using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 l-i (the foundation design §4.1, §4.5): a process reads the service's journal from its come-up, each batch told in order after
/// the cursor, a cursor kept advancing after the teller took it, and a cursor the journal no longer covers said as missed.
/// </summary>
public sealed class JournalFeedTests
{
    // No pause between asks: the stand-in answers after a moment of its own, so the feed is never a hot loop here.
    private static readonly TimeSpan Hurry = TimeSpan.Zero;

    /// <summary>The service's two doors, as a scripted journal: rows by position and where it stands.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _rows = [];

        public string Store { get; set; } = "store-a";

        public long Floor { get; set; } = 1;

        public bool Journal { get; set; } = true;

        public void Add(long position, string record = "s1", string? stateOld = "Working", string stateNew = "Completed", string? codes = "ended.done", string marks = "driven")
        {
            lock (_gate)
            {
                _rows.Add(new JsonObject
                {
                    ["position"] = position, ["at"] = 1_761_800_000_000, ["record"] = record, ["family"] = "sessions", ["change"] = "state",
                    ["stateOld"] = stateOld, ["stateNew"] = stateNew, ["limitedOld"] = false, ["limitedNew"] = false, ["noteCodes"] = codes, ["marks"] = marks,
                });
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(5, ct);
            var path = request.RequestUri!.AbsolutePath;
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            lock (_gate)
            {
                var newest = _rows.Count == 0 ? 0 : _rows.Max(row => row["position"]!.GetValue<long>());
                if (path == "/api/changes")
                {
                    var body = new JsonObject { ["since"] = "e.1", ["moved"] = new JsonArray() };
                    if (Journal) body["journal"] = new JsonObject { ["store"] = Store, ["floor"] = Floor, ["newest"] = newest };
                    return Answer(body);
                }

                var since = long.Parse(query["since"]!);
                var rows = new JsonArray([.. _rows.Where(row => row["position"]!.GetValue<long>() > since).Take(int.Parse(query["limit"]!)).Select(row => (JsonNode?)row.DeepClone())]);
                return Answer(new JsonObject { ["store"] = Store, ["floor"] = Floor, ["newest"] = newest, ["rows"] = rows });
            }
        }

        private static HttpResponseMessage Answer(JsonObject body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    }

    private sealed class Teller
    {
        public List<JournalBatch> Batches { get; } = [];

        public IEnumerable<long> Positions => Batches.SelectMany(batch => batch.Rows).Select(row => row.Position);

        public Task Told(JournalBatch batch)
        {
            lock (Batches) Batches.Add(batch);
            return Task.CompletedTask;
        }
    }

    private static async Task<T> Until<T>(Func<T?> read) where T : struct
    {
        for (var n = 0; n < 400; n++)
        {
            if (read() is { } found) return found;
            await Task.Delay(10);
        }

        throw new TimeoutException("the feed did not tell it");
    }

    private static Task Run(StandIn journal, Teller teller, CancellationTokenSource stop, Func<JournalPosition?>? load = null, Action<JournalPosition>? keep = null)
    {
        var client = new ServiceClient("http://ledger.test", null, new HttpClient(journal));
        return Task.Run(() => JournalFeed.RunAsync(client, teller.Told, load, keep, null, stop.Token, Hurry));
    }

    /// <summary>With no cursor a teller begins at the newest position: what the journal held at the come-up is not replayed, what follows is told once, in order.</summary>
    [Fact]
    public async Task A_teller_with_no_cursor_begins_at_the_newest_and_hears_what_follows_in_order()
    {
        var journal = new StandIn();
        journal.Add(1);
        journal.Add(2);
        var teller = new Teller();
        using var stop = new CancellationTokenSource();
        var run = Run(journal, teller, stop);

        await Task.Delay(100);
        Assert.Empty(teller.Batches);
        journal.Add(3, "s2");
        journal.Add(4, "s3");
        await Until<bool>(() => teller.Positions.Count() >= 2 ? true : null);
        await Task.Delay(60);
        await stop.CancelAsync();
        await run;

        Assert.Equal([3L, 4L], teller.Positions);
        Assert.Equal(["s2", "s3"], teller.Batches.SelectMany(batch => batch.Rows).Select(row => row.Record));
        Assert.All(teller.Batches, batch => Assert.False(batch.Missed));
    }

    /// <summary>A kept cursor is where the teller reads on from, and advances after the teller took each batch.</summary>
    [Fact]
    public async Task A_kept_cursor_is_read_on_from_and_advances_after_each_batch()
    {
        var journal = new StandIn();
        for (var n = 1; n <= 4; n++) journal.Add(n);
        var teller = new Teller();
        var kept = new List<JournalPosition>();
        using var stop = new CancellationTokenSource();
        var run = Run(journal, teller, stop, () => new JournalPosition("store-a", 2), position => { lock (kept) kept.Add(position); });

        await Until<bool>(() => teller.Positions.Count() >= 2 ? true : null);
        await stop.CancelAsync();
        await run;

        Assert.Equal([3L, 4L], teller.Positions);
        Assert.Equal(new JournalPosition("store-a", 4), kept[^1]);
    }

    /// <summary>A cursor below the floor, or of another store, is told as missed, with the rows the journal still holds.</summary>
    [Theory]
    [InlineData("store-a", 1L, 6L)]
    [InlineData("store-b", 5L, 1L)]
    public async Task A_cursor_the_journal_no_longer_covers_is_told_as_missed(string cursorStore, long cursorAt, long floor)
    {
        var journal = new StandIn { Floor = floor };
        journal.Add(6);
        journal.Add(7);
        var teller = new Teller();
        using var stop = new CancellationTokenSource();
        var run = Run(journal, teller, stop, () => new JournalPosition(cursorStore, cursorAt));

        await Until<bool>(() => teller.Batches.Count > 0 ? true : null);
        await stop.CancelAsync();
        await run;

        Assert.True(teller.Batches[0].Missed);
        Assert.Equal([6L, 7L], teller.Batches[0].Rows.Select(row => row.Position));
    }

    /// <summary>
    /// Tellers over one wait each read from their own cursor: one that kept its place hears what it had not read, though the process came up
    /// after those rows, and one with none begins at the newest.
    /// </summary>
    [Fact]
    public async Task Tellers_over_one_wait_each_read_from_their_own_cursor()
    {
        var journal = new StandIn();
        for (var n = 1; n <= 4; n++) journal.Add(n);
        var behind = new Teller();
        var fresh = new Teller();
        var kept = new List<JournalPosition>();
        using var stop = new CancellationTokenSource();
        var client = new ServiceClient("http://ledger.test", null, new HttpClient(journal));
        var run = Task.Run(() => JournalFeed.RunAsync(
            client,
            [new JournalTeller(behind.Told, () => new JournalPosition("store-a", 2), position => { lock (kept) kept.Add(position); }), new JournalTeller(fresh.Told)],
            null, stop.Token, Hurry));

        await Until<bool>(() => behind.Positions.Count() >= 2 ? true : null);
        journal.Add(5);
        await Until<bool>(() => fresh.Positions.Any() ? true : null);
        await stop.CancelAsync();
        await run;

        Assert.Equal([3L, 4L, 5L], behind.Positions);
        Assert.Equal([5L], fresh.Positions);
        Assert.Equal(new JournalPosition("store-a", 5), kept[^1]);
    }

    /// <summary>A host with no journal tells nothing and is not a failure.</summary>
    [Fact]
    public async Task A_host_with_no_journal_tells_nothing()
    {
        var journal = new StandIn { Journal = false };
        journal.Add(1);
        var teller = new Teller();
        using var stop = new CancellationTokenSource();
        var run = Run(journal, teller, stop);

        await Task.Delay(150);
        await stop.CancelAsync();
        await run;

        Assert.Empty(teller.Batches);
    }
}
