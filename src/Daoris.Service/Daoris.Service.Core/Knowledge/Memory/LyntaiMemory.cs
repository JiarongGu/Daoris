using System.Collections.Concurrent;
using System.Globalization;
using Lyntai.Memory;
using Lyntai.Memory.Engines;
using Lyntai.Storage;
using Lyntai.Storage.Sqlite;
using Lyntai.Storage.Sqlite.Migrations;

namespace Daoris.Knowledge;

/// <summary>
/// Each repository's memory as Lyntai's graph engine keeps it (KNOWCENTRAL2d; the memory coordinator design §1, §2): one engine per
/// repository, named by its registry name, over one SQLite file of this machine's beside <c>knowledge.db</c>. Every engine is opened with
/// <c>ReinforceOn = None</c>, so a recall or an expansion touches, links and logs nothing of the owner's.
/// </summary>
public sealed class LyntaiMemory : IMemoryEngines
{
    /// <summary>The file's name beside the index.</summary>
    public const string FileName = "memory.db";

    /// <summary>The one task every repository's entries are kept under: the engine's name is the repository.</summary>
    internal const string TaskKey = "repository";

    private const string Scope = "entries";

    private readonly string _path;
    private readonly Lock _opening = new();
    private readonly ConcurrentDictionary<(string, MemoryReinforcement), LyntaiEngine> _engines = new();
    private readonly TimeProvider _time;
    private SqliteMemoryGraphStore? _store;

    /// <param name="path">The engine's file, created and migrated at the first open.</param>
    /// <param name="time">The engines' clock; the system's unless a test moves one.</param>
    public LyntaiMemory(string path, TimeProvider? time = null)
    {
        _path = path;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The memory beside the index at <paramref name="databasePath"/>; null for an index held in memory, which keeps no file.</summary>
    public static LyntaiMemory? Beside(string databasePath) =>
        string.IsNullOrWhiteSpace(databasePath) || databasePath.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
            ? null
            : new(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, FileName));

    /// <summary>
    /// The store, its file migrated at the first use. A migration that fails, as one racing another process's first open can, is tried again
    /// at the next use rather than kept.
    /// </summary>
    internal IMemoryGraphStore Store
    {
        get
        {
            if (_store is { } opened) return opened;
            lock (_opening)
            {
                if (_store is null)
                {
                    MigrationRunnerService.MigrateUp(_path, StorageFeature.Memory);
                    _store = new SqliteMemoryGraphStore(new SqliteConnectionFactory(_path), clock: _time.GetUtcNow);
                }

                return _store;
            }
        }
    }

    public IMemoryEngine? Open(string repository, MemoryReinforcement reinforceOn) =>
        string.IsNullOrWhiteSpace(repository)
            ? null
            : _engines.GetOrAdd((repository, reinforceOn), key => new LyntaiEngine(
                new GraphMemoryEngine(
                    key.Item1, Store,
                    new GraphMemoryOptions
                    {
                        ReinforceOn = key.Item2 == MemoryReinforcement.None ? MemoryReinforcementActs.None : MemoryReinforcementActs.All,
                        ReceiptRetention = MemoryApplier.ReceiptRetention,
                    },
                    new GraphMemorySeams { Clock = _time.GetUtcNow }),
                Store));

    /// <summary>One repository's engine behind the coordinator's seam.</summary>
    private sealed class LyntaiEngine(GraphMemoryEngine engine, IMemoryGraphStore store) : IMemoryEngine
    {
        private static readonly string[] Relations = ["disputes", "amends"];

        public bool HoldsReceipts => engine.HoldsReceipts;

        public bool HoldsSupersession => engine.HoldsSupersession;

        public async Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, int limit, CancellationToken ct = default)
        {
            var recall = await engine.RecallAsync(new MemoryQuery(TaskKey, Query: query, Limit: limit, Detail: MemoryDetail.Full), ct)
                .ConfigureAwait(false);
            return [.. recall.Items.Select(Entry)];
        }

        public async Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default)
        {
            // An expansion of no hops is the entry alone, ended or not.
            var read = await engine.ExpandAsync(Ref(id), 0, null, MemoryDetail.Full, MemoryValidityReading.History, ct).ConfigureAwait(false);
            return read.Items.Count == 0 ? null : Entry(read.Items[0]);
        }

        public async Task<IReadOnlyList<MemoryRelated>> ExpandAsync(string id, CancellationToken ct = default)
        {
            var read = await engine.ExpandAsync(Ref(id), 1, null, MemoryDetail.Full, MemoryValidityReading.Current, ct).ConfigureAwait(false);
            var neighbours = read.Items.Skip(1).ToList();
            if (neighbours.Count == 0 || !long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var self)) return [];

            var typed = store is ITypedLinkMemoryGraphStore links
                ? await links.LinksAmongAsync(engine.Name, TaskKey, [self, .. neighbours.Select(item => Number(item.Reference.Id))], Relations, ct)
                    .ConfigureAwait(false)
                : [];
            return [.. neighbours.Select(item =>
            {
                var other = Number(item.Reference.Id);
                var kind = typed.FirstOrDefault(link => link.From == self && link.To == other).Kind
                    ?? (typed.FirstOrDefault(link => link.From == other && link.To == self).Kind is { } back ? $"{back} by" : "related");
                return new MemoryRelated(new MemoryLink(id, item.Reference.Id, kind, 1), Entry(item));
            })];
        }

        public async Task<MemoryEntry> RememberAsync(string text, CancellationToken ct = default, string? key = null, string? recheck = null)
        {
            var written = await engine.RememberAsync(new MemoryWrite(TaskKey, Scope, text) { OperationKey = key, Recheck = recheck }, ct)
                .ConfigureAwait(false);
            return await GetAsync(written.Reference.Id, ct).ConfigureAwait(false)
                ?? new MemoryEntry(written.Reference.Id, text, Revision(written.Receipt?.Revision));
        }

        public async Task<MemoryLink> RelateAsync(string from, string to, string kind, CancellationToken ct = default, string? key = null)
        {
            var relation = string.Equals(kind, "disputes", StringComparison.OrdinalIgnoreCase) ? MemoryRelation.Disputes : MemoryRelation.Amends;
            if (key is null) await engine.RelateAsync(TaskKey, Ref(from), Ref(to), relation, ct).ConfigureAwait(false);
            else await engine.RelateKeyedAsync(TaskKey, Ref(from), Ref(to), relation, key, ct).ConfigureAwait(false);
            return new MemoryLink(from, to, kind, 1);
        }

        public async Task<MemoryEntry?> ConfirmAsync(string id, long revision, CancellationToken ct = default, string? key = null)
        {
            MemoryConfirmResult confirmed;
            try
            {
                var expected = revision.ToString(CultureInfo.InvariantCulture);
                confirmed = key is null
                    ? await engine.ConfirmAsync(TaskKey, Ref(id), MemoryCheckOutcome.Held, expectedRevision: expected, ct: ct).ConfigureAwait(false)
                    : await engine.ConfirmKeyedAsync(TaskKey, Ref(id), MemoryCheckOutcome.Held, key, expectedRevision: expected, ct: ct)
                        .ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
                // No such entry: the guard did not hold.
                return null;
            }

            return confirmed.Outcome == MemoryConfirmOutcome.Stamped || confirmed is { Outcome: MemoryConfirmOutcome.Unchanged, Receipt.Replayed: true }
                ? await GetAsync(id, ct).ConfigureAwait(false)
                : null;
        }

        public async Task<MemoryEntry?> EndAsync(string id, long revision, string reason, CancellationToken ct = default, string? key = null)
        {
            MemoryEndResult ended;
            try
            {
                ended = await engine.EndAsync(
                    TaskKey, Ref(id),
                    new MemoryEnding(reason) { ExpectedRevision = revision.ToString(CultureInfo.InvariantCulture), OperationKey = key }, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception refused) when (refused is ArgumentException or InvalidOperationException)
            {
                return null;
            }

            return ended.Outcome == MemoryEndOutcome.Ended || ended is { Outcome: MemoryEndOutcome.AlreadyEnded, Receipt.Replayed: true }
                ? await GetAsync(id, ct).ConfigureAwait(false)
                : null;
        }

        public async Task<MemorySuperseded> SupersedeAsync(
            string text, IReadOnlyList<MemoryPredecessor> predecessors, string reason, CancellationToken ct = default, string? key = null,
            string? recheck = null)
        {
            MemorySupersessionResult replaced;
            try
            {
                replaced = await engine.SupersedeAsync(
                    new MemorySupersession(
                        new MemoryWrite(TaskKey, Scope, text) { Recheck = recheck },
                        [.. predecessors.Select(each => new MemorySupersededEntry(Ref(each.Entry), each.Revision.ToString(CultureInfo.InvariantCulture)))],
                        reason) { OperationKey = key },
                    ct).ConfigureAwait(false);
            }
            catch (Exception refused) when (refused is ArgumentException or InvalidOperationException and not MemoryKeyConflictException)
            {
                // A predecessor gone, the successor itself, or one another successor already ended: nothing was written.
                return new(false, null);
            }

            return replaced is { Applied: true, Successor: { } successor }
                ? new(true, await GetAsync(successor.Id, ct).ConfigureAwait(false) ?? new MemoryEntry(successor.Id, text, Revision(replaced.Revision)))
                : new(false, null);
        }

        public async Task<MemoryKeyReceipt?> ReceiptAsync(string key, CancellationToken ct = default) =>
            await engine.ReceiptAsync(TaskKey, key, ct).ConfigureAwait(false) is { } receipt
                ? new MemoryKeyReceipt(key, receipt.Reference?.Id, receipt.Revision is null ? null : Revision(receipt.Revision))
                : null;

        public async Task<IReadOnlyList<MemoryDue>> DueAsync(DateTimeOffset since, int limit, CancellationToken ct = default) =>
            [.. (await engine.DueAsync(TaskKey, null, since, limit, ct).ConfigureAwait(false))
                .Where(item => !string.IsNullOrWhiteSpace(item.Recheck))
                .Select(item => new MemoryDue(item.Reference.Id, Revision(item.Revision), item.Recheck!))];

        private MemoryRef Ref(string id) => new(engine.Name, id);

        // A replaced entry names its successor before the reason it ended with, whose token stays last.
        private static MemoryEntry Entry(MemoryItem item) => new(
            item.Reference.Id, item.Content ?? item.Headline, Revision(item.Revision),
            item.Validity?.SupersededBy is { } by
                ? $"superseded by {by.Id}" + (item.Validity.EndedReason is { } why ? $": {why}" : "")
                : item.Validity?.EndedReason);

        private static long Revision(string? revision) =>
            long.TryParse(revision, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

        private static long Number(string id) => long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
    }
}
