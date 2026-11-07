using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service's doors with each request counted by its path, the query left out, and the milliseconds it waited: what a read of
/// the benchmark asked of the service, said beside what it cost.
/// </summary>
internal sealed class BenchDoors(IServiceDoors inner) : IServiceDoors
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (int Count, long Ms)> _asked = new(StringComparer.Ordinal);

    /// <summary>The whole session list read with its closed records, counted apart from the open ones.</summary>
    public const string WholeList = "/api/sessions?includeClosed=true";

    /// <summary>The widest list read as its change since a mark, the kept list's one request.</summary>
    public const string Changes = "/api/sessions/changes";

    /// <summary>The open records alone.</summary>
    public const string Open = "/api/sessions";

    public string BaseUrl => inner.BaseUrl;

    /// <summary>How many requests each path had at the last <see cref="Take"/>.</summary>
    public IReadOnlyDictionary<string, int> LastTaken { get; private set; } = new Dictionary<string, int>();

    /// <summary>What is asked since the last take, the most costly first, and let go.</summary>
    public string Take()
    {
        lock (_gate)
        {
            LastTaken = _asked.ToDictionary(each => each.Key, each => each.Value.Count, StringComparer.Ordinal);
            var said = string.Join(", ", _asked.OrderByDescending(each => each.Value.Ms).Select(each => $"{each.Key} x{each.Value.Count} {each.Value.Ms} ms"));
            _asked.Clear();
            return said.Length == 0 ? "nothing asked" : said;
        }
    }

    /// <summary>How often a read asked for every record, closed ones included: the whole list, or its change since a mark.</summary>
    public static int ListReads(IReadOnlyDictionary<string, int> asked) => asked.GetValueOrDefault(WholeList) + asked.GetValueOrDefault(Changes);

    public Task<string> GetAsync(string path, CancellationToken ct) => CountedAsync(path, () => inner.GetAsync(path, ct));

    public Task<string> PostAsync(string path, string json, CancellationToken ct) => CountedAsync(path, () => inner.PostAsync(path, json, ct));

    public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct) =>
        CountedAsync(path, () => inner.SendAsync(verb, path, json, ct));

    private async Task<T> CountedAsync<T>(string path, Func<Task<T>> send)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            return await send();
        }
        finally
        {
            var key = path.Split('?')[0];
            // The whole list with its closed records is a read of its own, apart from the open ones the same path answers.
            if (key == "/api/sessions" && path.Contains("includeClosed=true", StringComparison.OrdinalIgnoreCase)) key = WholeList;
            lock (_gate)
            {
                var (count, ms) = _asked.GetValueOrDefault(key);
                _asked[key] = (count + 1, ms + watch.ElapsedMilliseconds);
            }
        }
    }

    public void Dispose() => inner.Dispose();
}
