using Daoris.Assistant.Ports;

namespace Daoris.Assistant.Testing;

/// <summary>The machine's checkouts as a memory tree: each named, placed, and holding the files the test puts in it.</summary>
public sealed class MemoryCheckouts : ICheckouts
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (string Place, Dictionary<string, string> Files)> _held = new(StringComparer.Ordinal);

    /// <summary>Holds a checkout named <paramref name="name"/> at <paramref name="place"/>, empty.</summary>
    public void Add(string name, string place)
    {
        lock (_gate) _held[name] = (place, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <summary>Puts <paramref name="text"/> at <paramref name="path"/> in a checkout held.</summary>
    public void Put(string checkout, string path, string text)
    {
        var inside = InsidePath.Require(path, nameof(path));
        lock (_gate)
        {
            if (!_held.TryGetValue(checkout, out var held)) throw new InvalidOperationException($"no checkout '{checkout}' is held");
            held.Files[inside] = text;
        }
    }

    public Task<IReadOnlyList<CheckoutPlace>> ListAsync(CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<CheckoutPlace>>(
                [.. _held.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new CheckoutPlace(pair.Key, pair.Value.Place))]);
        }
    }

    public Task<CheckoutReading> ReadAsync(string checkout, string path, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        var inside = InsidePath.Require(path, nameof(path));
        lock (_gate)
        {
            if (!_held.TryGetValue(checkout, out var held)) return Task.FromResult(new CheckoutReading(checkout, path, CheckoutReadState.UnknownCheckout));
            return Task.FromResult(held.Files.TryGetValue(inside, out var text)
                ? new CheckoutReading(checkout, path, CheckoutReadState.Answered) { Text = text }
                : new CheckoutReading(checkout, path, CheckoutReadState.Missing));
        }
    }
}
