using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>How a delivery lands: the <c>land</c> node's methods, the words the record keeps.</summary>
public static class DeliveryMethods
{
    public const string Merge = "merge";
    public const string Branch = "branch";
    public const string Push = "push";
    public const string PullRequest = "pull-request";
    public const string Integrate = "integrate";

    public static IReadOnlyList<string> All { get; } = [Merge, Branch, Push, PullRequest, Integrate];
}

/// <summary>A delivery's phases, in their order: each effect's entry is written ahead of it and its outcome after.</summary>
public static class DeliveryPhases
{
    /// <summary>The intent prepared for its effects: nothing of the landing written yet.</summary>
    public const string Prepared = "prepared";

    /// <summary>The landing's ref written through the ref transaction.</summary>
    public const string RefWritten = "ref-written";

    /// <summary>The landing record written: it proves the branch's preparation only, since it comes before any plugin.</summary>
    public const string Recorded = "recorded";

    /// <summary>The plugin sent the delivery key and its work.</summary>
    public const string PluginDispatched = "plugin-dispatched";

    /// <summary>The plugin's answer kept.</summary>
    public const string PluginAnswered = "plugin-answered";

    /// <summary>Every phase this build writes, in order: an entry at another comes from a newer build.</summary>
    public static IReadOnlyList<string> All { get; } = [Prepared, RefWritten, Recorded, PluginDispatched, PluginAnswered];

    /// <summary>The phase whose outcome reaches <c>land</c>: the plugin's answer for a push or a pull request, the landing record for the rest.</summary>
    public static string ReachOf(string method) => method is DeliveryMethods.Push or DeliveryMethods.PullRequest ? PluginAnswered : Recorded;
}

/// <summary>What a phase's effect came to, read after it and never assumed.</summary>
public static class DeliveryOutcomes
{
    /// <summary>The effect stands as its entry intended.</summary>
    public const string Done = "done";

    /// <summary>Nothing of the effect stands: refused, or read as never made. The phase may be written ahead again.</summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>Read as neither: held for the person to settle, and no later phase is written ahead.</summary>
    public const string Unknown = "unknown";

    public static IReadOnlyList<string> All { get; } = [Done, Withdrawn, Unknown];
}

/// <summary>How a delivery ended: no new delivery starts for its unit until it has.</summary>
public static class DeliverySettlements
{
    /// <summary>Its method's reaching phase is done.</summary>
    public const string Delivered = "delivered";

    /// <summary>No effect of it stands beyond its preparation.</summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>Stopped before its reach: what it did stays, and no further phase is written.</summary>
    public const string Stopped = "stopped";

    public static IReadOnlyList<string> All { get; } = [Delivered, Withdrawn, Stopped];
}

/// <summary>Who authorized a delivery: the person's Accept, or the standing automatic authority.</summary>
public static class DeliveryAuthorities
{
    public const string Person = "person";
    public const string Automatic = "automatic";
}

/// <summary>
/// What a delivery lands, frozen at its start: the unit, its repository, the bound tip, the method and the target
/// (what names it, with the generation), the line, the expanded branch name with its ticket key, the rendered message, the plugin, and the policy epoch's reference.
/// </summary>
public sealed record DeliveryIntent(
    string Unit, string Repository, string Tip, string Method, string Target, string Line, string? Branch, string? Ticket, string Message, string? Plugin,
    string Epoch)
{
    /// <summary>Whether <paramref name="other"/> lands the same thing: the unit, the tip, the method and the target, whichever generation either is.</summary>
    public bool Names(DeliveryIntent other) =>
        string.Equals(Unit, other.Unit, StringComparison.Ordinal) && string.Equals(Tip, other.Tip, StringComparison.Ordinal)
        && string.Equals(Method, other.Method, StringComparison.Ordinal) && string.Equals(Target, other.Target, StringComparison.Ordinal);
}

/// <summary>
/// What a delivery is authorized by and against: the person's Accept (<see cref="Accept"/>, its reference on the unit's record) or the
/// standing automatic authority (<see cref="Scope"/>), each given against the unit's revision, the graph's digest, the resolved options
/// (JSON) and the prerequisites' receipts (a JSON array).
/// </summary>
public sealed record DeliveryAuthorization(string Authority, string? Accept, string? Scope, string Revision, string Graph, string Options, string Receipts)
{
    public static DeliveryAuthorization Person(string accept, string revision, string graph, string options, string receipts) =>
        new(DeliveryAuthorities.Person, accept, null, revision, graph, options, receipts);

    public static DeliveryAuthorization Automatic(string scope, string revision, string graph, string options, string receipts) =>
        new(DeliveryAuthorities.Automatic, null, scope, revision, graph, options, receipts);
}

/// <summary>The claim a delivery's phases are written under: its run and the generation the last take raised.</summary>
public sealed record DeliveryClaim(string Key, string Run, long Generation);

/// <summary>A delivery as the store holds it.</summary>
public sealed record DeliveryRecord(
    string Key, int Format, DeliveryIntent Intent, DeliveryAuthorization Authorization, string ClaimRun, long ClaimGeneration, DateTimeOffset At)
{
    public string? Settled { get; init; }

    public DateTimeOffset? SettledAt { get; init; }

    public string? Said { get; init; }

    /// <summary>
    /// Which delivery of its unit, tip, method and target this is: 1 for the first, and the next once the one before settles withdrawn, so
    /// a retry is a delivery of its own and a settled row keeps its meaning.
    /// </summary>
    public long Generation { get; init; } = 1;

    /// <summary>Not settled: its unit takes no other delivery, and no rollback is allowed while it stands.</summary>
    public bool Unresolved => SettledAt is null;

    /// <summary>Written by a newer build: read as it stands, never taken or written by this one.</summary>
    public bool Held => Format > DeliveryStore.Format;
}

/// <summary>One phase entry: written ahead of its effect, its outcome written after.</summary>
public sealed record DeliveryPhase(string Delivery, int Ordinal, string Phase, string? Intended, DateTimeOffset BeganAt)
{
    public DateTimeOffset? EndedAt { get; init; }

    public string? Outcome { get; init; }

    /// <summary>What the outcome read: the ref's tip, the landing record, the plugin's answer.</summary>
    public string? Evidence { get; init; }

    public string? Said { get; init; }

    /// <summary>Written ahead and without its outcome: an effect that runs, or one a crash cut, which a restart settles by phase.</summary>
    public bool Open => EndedAt is null;
}

/// <summary>
/// What a start answered: a delivery started (<see cref="Started"/>, with its claim), the delivery its key already names, refused for a
/// delivery of its unit that is not settled, or held while the start's confirmation is not settled. Nothing is written but a start.
/// </summary>
public sealed record DeliveryStart(DeliveryRecord? Delivery, DeliveryClaim? Claim, bool Started, string? Refused, string? Held = null);

/// <summary>What a take answered: the delivery and the claim taken, or why it is refused, nothing written.</summary>
public sealed record DeliveryTaken(DeliveryRecord? Delivery, DeliveryClaim? Claim, string? Refused);

/// <summary>Whether going back to a build older than the delivery record is allowed: refused while any delivery is unresolved, each named.</summary>
public sealed record DeliveryRollback(bool Allowed, string? Refused, IReadOnlyList<DeliveryRecord> Unresolved);

/// <summary>A delivery's write refused before anything is written.</summary>
public sealed class DeliveryRefusedException(string key, string why) : Exception($"delivery {key}: {why}")
{
    public string Key { get; } = key;

    public string Why { get; } = why;
}

/// <summary>
/// The delivery record: a desktop-store family, Workplace's, in <c>deliveries</c> and
/// <c>delivery_phases</c>, machine-local and never synced: a landing's frozen intent, its authorization, and each of its effects written
/// ahead and its outcome after.
/// </summary>
/// <remarks>
/// <para><b>One delivery per key, one unresolved per unit</b>: a start inside the store's write lock answers the delivery its key names, or is
/// refused while another of its unit is not settled, so a new tip or method makes a new delivery only once the old one settles.</para>
/// <para><b>The act journal's claim</b> (<see cref="ActJournal"/>): a delivery is claimed by the run that started it, taken only from a run
/// dead by the lease's test, and every phase is written under the claim's check.</para>
/// <para><b>Older builds</b>: the family's step raised the store's writer stamp, so a build before it writes nothing there, and no delivery starts
/// while the start's confirmation is not settled, so a launcher's rollback never meets one. <see cref="Rollback"/> answers any other going back.</para>
/// </remarks>
public sealed class DeliveryStore(
    string home, Func<StoreSettled>? settled = null, Func<StoreRun, bool>? alive = null, string? run = null, string? kind = null, TimeProvider? clock = null)
{
    /// <summary>The family's name in <c>store_revisions</c>.</summary>
    public const string Family = "deliveries";

    /// <summary>The record's shape this build writes and reads.</summary>
    public const int Format = 1;

    /// <summary>The store's step that made the family, and raised the stamp: a store before it holds no delivery.</summary>
    public const int Since = 15;

    public const int GenerationSince = DesktopStore.GenerationVersion;

    private const int UniqueRefused = 2067;

    private const int PrimaryKeyRefused = 1555;

    private const string DeliveryColumns =
        "key, format, unit, repository, tip, method, target, line, branch, ticket, message, plugin, epoch, authority, accept, scope, revision, graph, "
        + "options, receipts, claim_run, claim_generation, at, settled, settled_at, said";

    private const string WriteColumns = DeliveryColumns + ", generation";

    private static string Columns(int version) => version >= GenerationSince ? DeliveryColumns + ", generation" : DeliveryColumns;

    private static string Newest(int version) => version >= GenerationSince ? "at DESC, generation DESC, key" : "at DESC, key";

    private const string PhaseColumns = "delivery, ordinal, phase, intended, began_at, ended_at, outcome, evidence, said";

    private readonly Func<StoreSettled> _settled = settled ?? StartConfirmation.Read;

    private readonly Func<StoreRun, bool> _alive = alive ?? IntegrationStore.Lives;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The run this store writes and claims under.</summary>
    public string Run { get; } = run ?? StoreProcess.Run;

    /// <summary>
    /// The delivery key: a SHA-256 of the unit, the bound tip, the method and the target, lower-case hex. Each part is length-prefixed, so no
    /// two parts joined read as another pair. A retry after a withdrawal is the next <paramref name="generation"/>, whose key reads it as well.
    /// </summary>
    public static string KeyOf(string unit, string tip, string method, string target, long generation = 1)
    {
        var text = new StringBuilder();
        foreach (var part in new[] { unit, tip, method, target }) text.Append(part.Length).Append(':').Append(part);
        if (generation > 1) text.Append("g:").Append(generation.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    // ——— Reads.

    /// <summary>The newest generation of the delivery <paramref name="intent"/> names (its unit, tip, method and target); null where the store holds none.</summary>
    public DeliveryRecord? Latest(DeliveryIntent intent) =>
        Reading(reader => reader.Query(
            $"SELECT {Columns(reader.Version)} FROM deliveries WHERE unit = $unit AND tip = $tip AND method = $method AND target = $target ORDER BY {Newest(reader.Version)}",
            MapDelivery, ("$unit", intent.Unit), ("$tip", intent.Tip), ("$method", intent.Method), ("$target", intent.Target)).FirstOrDefault(), null);

    /// <summary>A delivery by its key; null where the store holds none.</summary>
    public DeliveryRecord? Delivery(string key) =>
        Reading(reader => reader.Query($"SELECT {Columns(reader.Version)} FROM deliveries WHERE key = $key", MapDelivery, ("$key", key)).FirstOrDefault(), null);

    /// <summary>A delivery's phase entries, in the order they were written ahead.</summary>
    public IReadOnlyList<DeliveryPhase> Phases(string key) =>
        Reading(reader => reader.Query($"SELECT {PhaseColumns} FROM delivery_phases WHERE delivery = $key ORDER BY ordinal", MapPhase, ("$key", key)), []);

    /// <summary>Every delivery not settled, oldest first: what a restart settles by phase.</summary>
    public IReadOnlyList<DeliveryRecord> Unresolved() =>
        Reading(reader => reader.Query($"SELECT {Columns(reader.Version)} FROM deliveries WHERE settled_at IS NULL ORDER BY at, key", MapDelivery), []);

    /// <summary>The delivery of <paramref name="unit"/> not settled, or null.</summary>
    public DeliveryRecord? UnresolvedFor(string unit) =>
        Reading(reader => reader.Query($"SELECT {Columns(reader.Version)} FROM deliveries WHERE unit = $unit AND settled_at IS NULL", MapDelivery, ("$unit", unit))
            .FirstOrDefault(), null);

    /// <summary>Every delivery of <paramref name="unit"/>, newest first.</summary>
    public IReadOnlyList<DeliveryRecord> OfUnit(string unit) =>
        Reading(reader => reader.Query($"SELECT {Columns(reader.Version)} FROM deliveries WHERE unit = $unit ORDER BY {Newest(reader.Version)}", MapDelivery, ("$unit", unit)), []);

    /// <summary>Every delivery of <paramref name="repository"/> bound at <paramref name="tip"/>, newest first: what an Accept carrying several sessions delivered.</summary>
    public IReadOnlyList<DeliveryRecord> At(string repository, string tip) =>
        Reading(reader => reader.Query(
            $"SELECT {Columns(reader.Version)} FROM deliveries WHERE repository = $repository COLLATE NOCASE AND tip = $tip COLLATE NOCASE ORDER BY {Newest(reader.Version)}",
            MapDelivery, ("$repository", repository), ("$tip", tip)), []);

    /// <summary>
    /// Whether this home may go back to a build older than the delivery record: refused while any delivery is
    /// unresolved, so an older door never lands, or calls a plugin without the key, behind a newer delivery.
    /// </summary>
    public DeliveryRollback Rollback()
    {
        var open = Unresolved();
        if (open.Count == 0) return new DeliveryRollback(true, null, open);
        var named = string.Join(", ", open.Select(each => $"`{each.Intent.Unit}` by {each.Intent.Method} at {each.Intent.Tip[..Math.Min(8, each.Intent.Tip.Length)]}"));
        var count = open.Count == 1 ? "1 delivery is" : $"{open.Count} deliveries are";
        return new DeliveryRollback(
            false,
            $"going back to a build older than the delivery record is refused while {count} unresolved ({named}): settle "
            + (open.Count == 1 ? "it" : "them") + " first, so an older build never lands behind a newer delivery.",
            open);
    }

    // ——— Writes, each one transaction.

    /// <summary>
    /// Start the delivery of <paramref name="intent"/> under <paramref name="authorization"/>, claimed by this run at claim generation 1: answered the
    /// newest delivery it names (a withdrawn one is no answer: the start is the next generation of it), or refused while another of its unit is not settled, nothing written; held while the start's
    /// confirmation is not settled.
    /// </summary>
    /// <exception cref="DeliveryRefusedException">The intent or its authorization does not hold.</exception>
    public DeliveryStart Start(DeliveryIntent intent, DeliveryAuthorization authorization)
    {
        if (Problem(intent, authorization) is { } problem) throw new DeliveryRefusedException(KeyOf(intent.Unit, intent.Tip, intent.Method, intent.Target), problem);

        // The store's rollback window: a delivery begun here could be met by the build before this one.
        if (_settled() is { Is: false } waiting) return new DeliveryStart(null, null, false, null, waiting.Said ?? "the start's confirmation is not settled");

        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                var latest = LatestIn(write, intent);
                if (latest is not null && latest.Settled != DeliverySettlements.Withdrawn) throw new Unwritten(Same(latest));
                if (UnresolvedIn(write, intent.Unit) is { } standing) throw new Unwritten(Blocked(standing));
                var generation = (latest?.Generation ?? 0) + 1;
                var key = KeyOf(intent.Unit, intent.Tip, intent.Method, intent.Target, generation);
                try
                {
                    Insert(write, key, generation, intent, authorization);
                }
                catch (SqliteException error) when (error.SqliteExtendedErrorCode is UniqueRefused or PrimaryKeyRefused)
                {
                    throw new Unwritten(LatestIn(write, intent) is { } found && found.Settled != DeliverySettlements.Withdrawn
                        ? Same(found)
                        : Blocked(UnresolvedIn(write, intent.Unit)!));
                }

                return new DeliveryStart(DeliveryIn(write, key), new DeliveryClaim(key, Run, 1), true, null);
            });
        }
        catch (Unwritten unwritten)
        {
            return (DeliveryStart)unwritten.Answer;
        }

        DeliveryStart Same(DeliveryRecord found) =>
            new(found, found.Unresolved && !found.Held && found.ClaimRun == Run ? new DeliveryClaim(found.Key, Run, found.ClaimGeneration) : null, false, null);

        static DeliveryStart Blocked(DeliveryRecord standing) =>
            new(standing, null, false,
                $"delivery {standing.Key[..12]} of `{standing.Intent.Unit}` by {standing.Intent.Method} at {standing.Intent.Tip} is not settled, and a unit "
                + "has one delivery at a time: a new tip or method starts once it settles");
    }

    /// <summary>A phase's entry, written ahead of its effect: refused while an entry stands without its outcome, out of order, or after a done one.</summary>
    /// <exception cref="DeliveryRefusedException">The phase may not be written ahead now; nothing is written.</exception>
    public DeliveryPhase Begin(DeliveryClaim claim, string phase, string? intended)
    {
        if (!DeliveryPhases.All.Contains(phase, StringComparer.Ordinal)) throw new DeliveryRefusedException(claim.Key, $"`{phase}` is no phase this build writes");
        return Fenced(claim, (write, delivery) =>
        {
            var entries = Standing(claim, PhasesIn(write, claim.Key));
            if (entries.FirstOrDefault(each => each.Outcome == DeliveryOutcomes.Unknown) is { } unknown)
            {
                throw new DeliveryRefusedException(claim.Key, $"its `{unknown.Phase}` was read as unknown, which the person settles before any later phase");
            }

            var done = entries.Where(each => each.Outcome == DeliveryOutcomes.Done).Select(each => each.Phase).ToHashSet(StringComparer.Ordinal);
            if (done.Contains(phase)) throw new DeliveryRefusedException(claim.Key, $"its `{phase}` is done, and a done phase is never written ahead again");
            var at = IndexOf(phase);
            if (at > 0 && !done.Contains(DeliveryPhases.All[at - 1]))
            {
                throw new DeliveryRefusedException(claim.Key, $"`{phase}` comes after `{DeliveryPhases.All[at - 1]}`, which is not done");
            }

            if (phase is DeliveryPhases.PluginDispatched or DeliveryPhases.PluginAnswered && delivery.Intent.Plugin is null)
            {
                throw new DeliveryRefusedException(claim.Key, "it names no plugin, so it takes no plugin phase");
            }

            var ordinal = entries.Count == 0 ? 0 : entries.Max(each => each.Ordinal) + 1;
            write.Execute(
                "INSERT INTO delivery_phases (delivery, ordinal, phase, intended, began_at) VALUES ($key, $ordinal, $phase, $intended, $at)",
                ("$key", claim.Key), ("$ordinal", ordinal), ("$phase", phase), ("$intended", intended), ("$at", Now()));
            return PhasesIn(write, claim.Key).Single(each => each.Ordinal == ordinal);
        });
    }

    /// <summary>
    /// The outcome of the entry standing for <paramref name="phase"/>, after its effect, with what it read. An outcome written again is the
    /// first's, and changes nothing.
    /// </summary>
    /// <exception cref="DeliveryRefusedException">No entry of the phase is written ahead, or another phase's stands.</exception>
    public DeliveryPhase End(DeliveryClaim claim, string phase, string outcome, string? evidence, string said)
    {
        if (!DeliveryOutcomes.All.Contains(outcome, StringComparer.Ordinal)) throw new DeliveryRefusedException(claim.Key, $"`{outcome}` is no outcome this build writes");
        return Fenced(claim, (write, _) =>
        {
            var entries = PhasesIn(write, claim.Key);
            if (entries.FirstOrDefault(each => each.Open) is not { } open)
            {
                return entries.LastOrDefault(each => each.Phase == phase)
                    ?? throw new DeliveryRefusedException(claim.Key, $"its `{phase}` was not written ahead");
            }

            if (open.Phase != phase) throw new DeliveryRefusedException(claim.Key, $"the entry standing without its outcome is `{open.Phase}`, not `{phase}`");
            write.Execute(
                "UPDATE delivery_phases SET ended_at = $at, outcome = $outcome, evidence = $evidence, said = $said WHERE delivery = $key AND ordinal = $ordinal AND ended_at IS NULL",
                ("$at", Now()), ("$outcome", outcome), ("$evidence", evidence), ("$said", said), ("$key", claim.Key), ("$ordinal", open.Ordinal));
            return PhasesIn(write, claim.Key).Single(each => each.Ordinal == open.Ordinal);
        });
    }

    /// <summary>
    /// The delivery settled <paramref name="how"/>: refused while an entry stands without its outcome, as delivered before its method's reaching
    /// phase is done, and as withdrawn where an effect past its preparation stands. Settled again the same way, nothing changes.
    /// </summary>
    /// <exception cref="DeliveryRefusedException">It may not settle so; nothing is written.</exception>
    public void Settle(DeliveryClaim claim, string how, string said)
    {
        if (!DeliverySettlements.All.Contains(how, StringComparer.Ordinal)) throw new DeliveryRefusedException(claim.Key, $"`{how}` is no settlement this build writes");
        Fenced(claim, (write, delivery) =>
        {
            if (!delivery.Unresolved)
            {
                return delivery.Settled == how ? true : throw new DeliveryRefusedException(claim.Key, $"it is settled already as {delivery.Settled}");
            }

            var entries = Standing(claim, PhasesIn(write, claim.Key));
            var done = entries.Where(each => each.Outcome == DeliveryOutcomes.Done).Select(each => each.Phase).ToList();
            var reach = DeliveryPhases.ReachOf(delivery.Intent.Method);
            if (how == DeliverySettlements.Delivered && !done.Contains(reach))
            {
                throw new DeliveryRefusedException(claim.Key, $"a {delivery.Intent.Method} is delivered once its `{reach}` is done");
            }

            if (how == DeliverySettlements.Withdrawn && done.FirstOrDefault(each => each != DeliveryPhases.Prepared) is { } stands)
            {
                throw new DeliveryRefusedException(claim.Key, $"its `{stands}` is done and stands, so it is not withdrawn: a stop keeps it");
            }

            Changed(write, claim, "UPDATE deliveries SET settled = $how, settled_at = $at, said = $said WHERE key = $key AND settled_at IS NULL",
                ("$how", how), ("$at", Now()), ("$said", said));
            return true;
        }, settledToo: true);
    }

    /// <summary>
    /// Take up a delivery whose claim's run is dead by the lease's test, in one guarded update that writes this run and raises the generation:
    /// refused, nothing written, where it is gone, held, settled or this run's own, the generation is no longer <paramref name="generation"/>,
    /// or its run lives or cannot be told dead.
    /// </summary>
    public DeliveryTaken Take(string key, long generation)
    {
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                var found = DeliveryIn(write, key) ?? throw Refused(null, "the store holds no such delivery");
                if (found.Held) throw Refused(found, "it was written by a newer Daoris, which this build reads and never writes");
                if (!found.Unresolved) throw Refused(found, "it is settled");
                if (string.Equals(found.ClaimRun, Run, StringComparison.Ordinal)) throw Refused(found, "it is this run's own");
                if (found.ClaimGeneration != generation) throw Refused(found, "another run took it first");
                if (RunIn(write, found.ClaimRun) is not { } holder || _alive(holder))
                {
                    throw Refused(found, found.ClaimGeneration > 1 ? "another run took it first" : "the Daoris process that delivers it still runs");
                }

                var taken = write.Execute(
                    """
                    UPDATE deliveries SET claim_run = $mine, claim_generation = claim_generation + 1
                    WHERE key = $key AND claim_run = $run AND claim_generation = $generation AND settled_at IS NULL
                    """,
                    ("$mine", Run), ("$key", key), ("$run", found.ClaimRun), ("$generation", generation));
                if (taken != 1) throw Refused(found, "another run took it first");
                var now = DeliveryIn(write, key)!;
                return new DeliveryTaken(now, new DeliveryClaim(key, Run, now.ClaimGeneration), null);
            });
        }
        catch (Unwritten unwritten)
        {
            return (DeliveryTaken)unwritten.Answer;
        }

        static Unwritten Refused(DeliveryRecord? found, string why) => new(new DeliveryTaken(found, null, why));
    }

    // ——— The writes' own parts.

    /// <summary>What does not hold of a start, or null.</summary>
    private static string? Problem(DeliveryIntent intent, DeliveryAuthorization authorization)
    {
        foreach (var (name, value) in new[]
        {
            ("unit", intent.Unit), ("repository", intent.Repository), ("tip", intent.Tip), ("method", intent.Method), ("target", intent.Target),
            ("line", intent.Line), ("message", intent.Message), ("policy epoch", intent.Epoch), ("revision", authorization.Revision),
            ("graph digest", authorization.Graph), ("resolved options", authorization.Options), ("receipts", authorization.Receipts),
        })
        {
            if (string.IsNullOrWhiteSpace(value)) return $"a delivery names its {name}, and this one's is empty";
        }

        if (!DeliveryMethods.All.Contains(intent.Method, StringComparer.Ordinal)) return $"`{intent.Method}` is no method a delivery lands by";
        if (intent.Method is DeliveryMethods.Push or DeliveryMethods.PullRequest && string.IsNullOrWhiteSpace(intent.Plugin))
        {
            return "a push or a pull request is a plugin's to make, and this delivery names no plugin";
        }

        return authorization.Authority switch
        {
            DeliveryAuthorities.Person when string.IsNullOrWhiteSpace(authorization.Accept) => "a person's delivery names the person's Accept it was given by",
            DeliveryAuthorities.Automatic when string.IsNullOrWhiteSpace(authorization.Scope) => "an automatic delivery names the standing authority's scope",
            DeliveryAuthorities.Person or DeliveryAuthorities.Automatic => null,
            _ => $"`{authorization.Authority}` is no authority a delivery starts under",
        };
    }

    private void Insert(DesktopWrite write, string key, long generation, DeliveryIntent intent, DeliveryAuthorization authorization) => write.Execute(
        $"""
        INSERT INTO deliveries ({WriteColumns})
        VALUES ($key, $format, $unit, $repository, $tip, $method, $target, $line, $branch, $ticket, $message, $plugin, $epoch, $authority, $accept, $scope,
                $revision, $graph, $options, $receipts, $run, 1, $at, NULL, NULL, NULL, $generation)
        """,
        ("$key", key), ("$format", Format), ("$unit", intent.Unit), ("$repository", intent.Repository), ("$tip", intent.Tip), ("$method", intent.Method),
        ("$target", intent.Target), ("$line", intent.Line), ("$branch", intent.Branch), ("$ticket", intent.Ticket), ("$message", intent.Message),
        ("$plugin", intent.Plugin), ("$epoch", intent.Epoch), ("$authority", authorization.Authority), ("$accept", authorization.Accept),
        ("$scope", authorization.Scope), ("$revision", authorization.Revision), ("$graph", authorization.Graph), ("$options", authorization.Options),
        ("$receipts", authorization.Receipts), ("$run", Run), ("$at", Now()), ("$generation", generation));

    /// <summary>
    /// One write under the claim's check: the delivery is there, this build's shape, its run and generation the claim's, which is this store's
    /// run, and not settled unless <paramref name="settledToo"/>.
    /// </summary>
    private T Fenced<T>(DeliveryClaim claim, Func<DesktopWrite, DeliveryRecord, T> work, bool settledToo = false)
    {
        using var store = Writer();
        return store.Write(Family, write =>
        {
            if (!string.Equals(claim.Run, Run, StringComparison.Ordinal)) throw new DeliveryRefusedException(claim.Key, $"this store writes under run {Run}, not {claim.Run}");
            var delivery = DeliveryIn(write, claim.Key) ?? throw new DeliveryRefusedException(claim.Key, "the store holds no such delivery");
            if (delivery.Held) throw new DeliveryRefusedException(claim.Key, "it was written by a newer Daoris, which this build reads and never writes");
            if (!string.Equals(delivery.ClaimRun, claim.Run, StringComparison.Ordinal) || delivery.ClaimGeneration != claim.Generation)
            {
                throw new DeliveryRefusedException(claim.Key, $"run {delivery.ClaimRun} holds it at generation {delivery.ClaimGeneration}");
            }

            if (!delivery.Unresolved && !settledToo) throw new DeliveryRefusedException(claim.Key, $"it is settled ({delivery.Settled})");
            return work(write, delivery);
        });
    }

    /// <summary>The entries, refused where one stands without its outcome or names a phase a newer build wrote.</summary>
    private static IReadOnlyList<DeliveryPhase> Standing(DeliveryClaim claim, IReadOnlyList<DeliveryPhase> entries)
    {
        if (entries.FirstOrDefault(each => IndexOf(each.Phase) < 0) is { } newer)
        {
            throw new DeliveryRefusedException(claim.Key, $"its entry {newer.Ordinal} is `{newer.Phase}`, a phase a newer Daoris wrote");
        }

        if (entries.FirstOrDefault(each => each.Open) is { } open)
        {
            throw new DeliveryRefusedException(claim.Key,
                $"its `{open.Phase}` entry was written ahead at {Moments.Write(open.BeganAt)} and has no outcome: a restart settles it by phase");
        }

        return entries;
    }

    private static int IndexOf(string phase)
    {
        for (var at = 0; at < DeliveryPhases.All.Count; at++)
        {
            if (string.Equals(DeliveryPhases.All[at], phase, StringComparison.Ordinal)) return at;
        }

        return -1;
    }

    /// <summary>An update of the delivery's row under its claim, named in its own condition, which must change exactly that row.</summary>
    private static void Changed(DesktopWrite write, DeliveryClaim claim, string update, params (string Name, object? Value)[] values)
    {
        var changed = write.Execute(
            update + " AND claim_run = $claimRun AND claim_generation = $claimGeneration",
            [.. values, ("$key", claim.Key), ("$claimRun", claim.Run), ("$claimGeneration", claim.Generation)]);
        if (changed != 1) throw new DeliveryRefusedException(claim.Key, "its row changed under the write");
    }

    private static DeliveryRecord? DeliveryIn(DesktopWrite write, string key) =>
        write.Query($"SELECT {WriteColumns} FROM deliveries WHERE key = $key", MapDelivery, ("$key", key)).FirstOrDefault();

    private static DeliveryRecord? LatestIn(DesktopWrite write, DeliveryIntent intent) =>
        write.Query(
            $"SELECT {WriteColumns} FROM deliveries WHERE unit = $unit AND tip = $tip AND method = $method AND target = $target ORDER BY {Newest(int.MaxValue)}",
            MapDelivery, ("$unit", intent.Unit), ("$tip", intent.Tip), ("$method", intent.Method), ("$target", intent.Target)).FirstOrDefault();

    private static DeliveryRecord? UnresolvedIn(DesktopWrite write, string unit) =>
        write.Query($"SELECT {WriteColumns} FROM deliveries WHERE unit = $unit AND settled_at IS NULL", MapDelivery, ("$unit", unit)).FirstOrDefault();

    private static IReadOnlyList<DeliveryPhase> PhasesIn(DesktopWrite write, string key) =>
        write.Query($"SELECT {PhaseColumns} FROM delivery_phases WHERE delivery = $key ORDER BY ordinal", MapPhase, ("$key", key));

    private static StoreRun? RunIn(DesktopWrite write, string run) =>
        write.Query(
            "SELECT run, kind, pid, started, began FROM store_runs WHERE run = $run",
            row => new StoreRun(row.GetString(0), row.GetString(1), row.GetInt32(2), row.IsDBNull(3) ? null : row.GetInt64(3), row.GetString(4)),
            ("$run", run)).FirstOrDefault();

    // ——— The rows read.

    private static string? Text(DbDataReader row, string column)
    {
        var at = row.GetOrdinal(column);
        return row.IsDBNull(at) ? null : row.GetString(at);
    }

    private static long? Number(DbDataReader row, string column)
    {
        var at = row.GetOrdinal(column);
        return row.IsDBNull(at) ? null : row.GetInt64(at);
    }

    private static DeliveryRecord MapDelivery(DbDataReader row) =>
        new(
            Text(row, "key")!,
            (int)Number(row, "format")!.Value,
            new DeliveryIntent(
                Text(row, "unit")!, Text(row, "repository")!, Text(row, "tip")!, Text(row, "method")!, Text(row, "target")!, Text(row, "line")!,
                Text(row, "branch"), Text(row, "ticket"), Text(row, "message")!, Text(row, "plugin"), Text(row, "epoch")!),
            new DeliveryAuthorization(
                Text(row, "authority")!, Text(row, "accept"), Text(row, "scope"), Text(row, "revision")!, Text(row, "graph")!, Text(row, "options")!,
                Text(row, "receipts")!),
            Text(row, "claim_run")!,
            Number(row, "claim_generation")!.Value,
            Moments.Read(Text(row, "at")) ?? throw new DesktopStoreException("a delivery's `at` does not read as a moment"))
        {
            Settled = Text(row, "settled"),
            SettledAt = Moments.Read(Text(row, "settled_at")),
            Said = Text(row, "said"),
            Generation = Enumerable.Range(0, row.FieldCount).Any(at => row.GetName(at) == "generation") ? Number(row, "generation") ?? 1 : 1,
        };

    private static DeliveryPhase MapPhase(DbDataReader row) =>
        new(
            Text(row, "delivery")!, (int)Number(row, "ordinal")!.Value, Text(row, "phase")!, Text(row, "intended"),
            Moments.Read(Text(row, "began_at")) ?? throw new DesktopStoreException("a delivery phase's `began_at` does not read as a moment"))
        {
            EndedAt = Moments.Read(Text(row, "ended_at")),
            Outcome = Text(row, "outcome"),
            Evidence = Text(row, "evidence"),
            Said = Text(row, "said"),
        };

    /// <summary>A read of a store that holds the family, or <paramref name="none"/> where the home has no store or one from before it.</summary>
    private T Reading<T>(Func<DesktopStoreReader, T> read, T none)
    {
        using var reader = DesktopStoreReader.Open(Location);
        return reader is not null && reader.Version >= Since ? read(reader) : none;
    }

    private string Location => DesktopStore.LocationIn(home);

    /// <summary>The store opened for one write under this store's run; a wait on its write lock is a leaf one (<see cref="LockWaits.Store"/>).</summary>
    private DesktopStore Writer()
    {
        LockWaits.Waits(LockWaits.Store);
        return DesktopStore.Open(Location, StoreProcess.Options(_settled) with { Run = Run, Kind = kind ?? StoreProcess.Kind, Clock = _clock });
    }

    private string Now() => Moments.Write(_clock.GetUtcNow());

    /// <summary>A write's answer that writes nothing: thrown out of the store's transaction, so it is rolled back and no revision moves.</summary>
    private sealed class Unwritten(object answer) : Exception
    {
        public object Answer { get; } = answer;
    }
}
