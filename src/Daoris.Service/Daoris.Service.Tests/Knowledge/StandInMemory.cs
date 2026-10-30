using System.Text;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A stand-in for Lyntai's engines, one per repository (KNOWCENTRAL2b), repeating the effects the memory coordinator design §6 C names:
/// a same-content write advances the engine and bumps the entry's revision, a repeated relation adds weight, a confirmation guarded by the
/// revision read bumps the revision and names no confirmation, and an end is one guarded change. A recall opened to reinforce touches, co-activates and logs what
/// it returns, as Lyntai's does by default. <see cref="State"/> reads every effect, so a read that writes anything is seen.
/// </summary>
public sealed class StandInMemory : IMemoryEngines
{
    private readonly Dictionary<string, Store> _engines = new(StringComparer.Ordinal);

    /// <summary>The memory of <paramref name="repository"/>, kept from now on.</summary>
    public Store Engine(string repository)
    {
        if (!_engines.TryGetValue(repository, out var engine)) _engines[repository] = engine = new Store();
        return engine;
    }

    public IMemoryEngine? Open(string repository, MemoryReinforcement reinforceOn) =>
        _engines.TryGetValue(repository, out var engine) ? new Opened(engine, reinforceOn) : null;

    /// <summary>Every engine's every effect, as one text to compare.</summary>
    public string State()
    {
        var text = new StringBuilder();
        foreach (var (repository, engine) in _engines.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            text.Append(repository).Append(" advance ").Append(engine.Advance).Append(" log ").Append(engine.Log).AppendLine();
            foreach (var row in engine.Rows.Values)
            {
                text.Append($"  {row.Id} r{row.Revision} uses {row.Uses} stamp {row.Stamp} ended {row.Ended}").AppendLine();
            }

            foreach (var ((from, to, kind), weight) in engine.Links.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
            {
                text.Append($"  {from} {kind} {to} {weight}").AppendLine();
            }
        }

        return text.ToString();
    }

    /// <summary>One repository's memory: its rows, its links, and the sequence each change advances.</summary>
    public sealed class Store
    {
        internal readonly Dictionary<string, Row> Rows = new(StringComparer.Ordinal);
        internal readonly Dictionary<(string From, string To, string Kind), double> Links = [];

        /// <summary>Moved by every change, a same-content write's too.</summary>
        public long Advance { get; internal set; }

        /// <summary>The recalls a reinforcing read logged.</summary>
        public long Log { get; internal set; }

        /// <summary>An entry written, as the engine's remember writes one.</summary>
        public MemoryEntry Keep(string text)
        {
            Advance++;
            if (Rows.Values.FirstOrDefault(row => row.Ended is null && row.Text == text) is { } same)
            {
                same.Revision++;
                return same.Entry;
            }

            var row = new Row($"m{Rows.Count + 1}", text);
            Rows[row.Id] = row;
            return row.Entry;
        }
    }

    internal sealed class Row(string id, string text)
    {
        public string Id { get; } = id;
        public string Text { get; } = text;
        public long Revision { get; set; } = 1;
        public long Uses { get; set; }
        public long Stamp { get; set; }
        public string? Ended { get; set; }
        public MemoryEntry Entry => new(Id, Text, Revision, Ended);
    }

    /// <summary>An engine as the coordinator opened it, reinforcing its recalls or not.</summary>
    private sealed class Opened(Store engine,MemoryReinforcement reinforceOn) : IMemoryEngine
    {
        public Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, int limit, CancellationToken ct = default)
        {
            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var found = engine.Rows.Values
                .Where(row => row.Ended is null && words.Any(word => row.Text.Contains(word, StringComparison.OrdinalIgnoreCase)))
                .Take(limit)
                .ToList();
            Recalled(found);
            // Lyntai's recall co-activates what it returns together.
            if (reinforceOn != MemoryReinforcement.None)
            {
                foreach (var (a, b) in found.SelectMany((row, i) => found.Skip(i + 1).Select(other => (row.Id, other.Id))))
                {
                    engine.Links[(a, b, "co-activated")] = engine.Links.GetValueOrDefault((a, b, "co-activated")) + 1;
                }
            }

            return Task.FromResult<IReadOnlyList<MemoryEntry>>([.. found.Select(row => row.Entry)]);
        }

        public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
        {
            var row = engine.Rows.GetValueOrDefault(id);
            if (row is not null) Recalled([row]);
            return Task.FromResult(row?.Entry);
        }

        public Task<IReadOnlyList<MemoryRelated>> ExpandAsync(string id, CancellationToken ct = default)
        {
            var related = engine.Links
                .Where(link => link.Key.From == id && engine.Rows.ContainsKey(link.Key.To))
                .Select(link => (Link: new MemoryLink(link.Key.From, link.Key.To, link.Key.Kind, link.Value), Row: engine.Rows[link.Key.To]))
                .ToList();
            Recalled([.. related.Select(each => each.Row)]);
            return Task.FromResult<IReadOnlyList<MemoryRelated>>([.. related.Select(each => new MemoryRelated(each.Link, each.Row.Entry))]);
        }

        public Task<MemoryEntry> RememberAsync(string text, CancellationToken ct = default) => Task.FromResult(engine.Keep(text));

        public Task<MemoryLink> RelateAsync(string from, string to, string kind, CancellationToken ct = default)
        {
            engine.Advance++;
            var weight = engine.Links.GetValueOrDefault((from, to, kind)) + 1;
            engine.Links[(from, to, kind)] = weight;
            return Task.FromResult(new MemoryLink(from, to, kind, weight));
        }

        public Task<MemoryEntry?> ConfirmAsync(string id, long revision, CancellationToken ct = default)
        {
            if (engine.Rows.GetValueOrDefault(id) is not { Ended: null } row || row.Revision != revision) return Task.FromResult<MemoryEntry?>(null);
            engine.Advance++;
            row.Revision++;
            row.Stamp = engine.Advance;
            return Task.FromResult<MemoryEntry?>(row.Entry);
        }

        public Task<MemoryEntry?> EndAsync(string id, long revision, string reason, CancellationToken ct = default)
        {
            if (engine.Rows.GetValueOrDefault(id) is not { Ended: null } row || row.Revision != revision) return Task.FromResult<MemoryEntry?>(null);
            engine.Advance++;
            row.Revision++;
            row.Ended = reason;
            return Task.FromResult<MemoryEntry?>(row.Entry);
        }

        private void Recalled(IReadOnlyList<Row> rows)
        {
            if (reinforceOn == MemoryReinforcement.None || rows.Count == 0) return;
            engine.Log++;
            foreach (var row in rows) row.Uses++;
        }
    }
}
