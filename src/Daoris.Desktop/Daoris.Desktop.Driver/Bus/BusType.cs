using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// One bus type, declared once in <see cref="BusTypes"/>: its name and version, whether it is a fact, a request or a
/// response, its payload's fields and their shapes, who emits it, who must handle or answer it and by when, how many times the bus tries
/// that one again past the deadline, and what its give-up does.
/// </summary>
/// <param name="Name">Its namespaced name (<c>check.started</c>).</param>
/// <param name="Version">Its version, from 1: a payload whose meaning changes is a new version.</param>
/// <param name="Class">A fact, a request or a response (<see cref="BusClasses"/>).</param>
/// <param name="Emitter">Who appends it: the envelope's emitter.</param>
/// <param name="Obliged">A fact's obliged handler, a request's answerer: who must act on it by its deadline.</param>
/// <param name="Deadline">By when from its append, measured or provisional.</param>
/// <param name="Retries">How many times past the deadline the bus tries the obliged one again before the give-up.</param>
/// <param name="GiveUp">What the give-up does, from the closed list (<see cref="BusGiveUps"/>).</param>
/// <param name="Fields">Its payload's fields, each with its shape: any other field is dropped at emit, and so is a value of another shape.</param>
public sealed record BusType(
    string Name, int Version, string Class, string Emitter, string Obliged, BusDeadline Deadline, int Retries, string GiveUp, IReadOnlyList<BusField> Fields)
{
    /// <summary>Its type and version as an envelope carries it, <c>name/version</c> (<c>check.started/1</c>).</summary>
    public string Kind => string.Create(CultureInfo.InvariantCulture, $"{Name}/{Version}");

    /// <summary>
    /// The payload as this type declares it, in the draft's order: a declared field's value kept where it has its shape; a field not
    /// declared, one named twice and a value of another shape dropped, each named with its reason (<see cref="BusDrops"/>). A dropped value
    /// leaves its field null, unknown; a list keeps the ids that fit. The fact is kept whatever is dropped.
    /// </summary>
    public BusAdmitted Admit(IReadOnlyList<(string Key, object? Value)> payload)
    {
        var kept = new List<(string Key, object? Value)>(payload.Count);
        var dropped = new List<(string Field, string Reason)>();
        foreach (var (key, value) in payload)
        {
            if (Fields.FirstOrDefault(field => field.Name == key) is not { } declared)
            {
                dropped.Add((key, BusDrops.Undeclared));
                continue;
            }

            if (kept.Exists(pair => pair.Key == key))
            {
                dropped.Add((key, BusDrops.Twice));
                continue;
            }

            var (fits, misfits) = BusShapes.Admit(declared.Shape, value);
            for (var n = 0; n < misfits; n++) dropped.Add((key, BusDrops.Shape));
            kept.Add((key, fits));
        }

        return new BusAdmitted(kept, dropped);
    }
}

/// <summary>What a type's payload kept at emit, and each field or value it dropped with its reason.</summary>
public sealed record BusAdmitted(IReadOnlyList<(string Key, object? Value)> Kept, IReadOnlyList<(string Field, string Reason)> Dropped);

/// <summary>One field of a type's payload: its name and its shape (<see cref="BusShapes"/>).</summary>
public sealed record BusField(string Name, string Shape);

/// <summary>
/// A type's deadline: how long from its append its obliged one has, and what sets it. A provisional one names why no measure sets it
/// yet, so the watchdog runs against it in shadow first: a deadline chosen too short would give up live work by itself.
/// </summary>
/// <param name="Within">How long from the append.</param>
/// <param name="Basis">What set it: the measured handling times it reads, or, provisional, why none did and what the value stands on.</param>
/// <param name="Provisional">Whether no measure set it yet.</param>
public sealed record BusDeadline(TimeSpan Within, string Basis, bool Provisional)
{
    /// <summary>A deadline set from the handling times <c>daoris bus stats</c> read, which <paramref name="basis"/> names with their commit.</summary>
    public static BusDeadline Measured(TimeSpan within, string basis) => new(within, basis, Provisional: false);

    /// <summary>The deadline the longest handling time measured sets: twice it, up to the next five minutes, five at least.</summary>
    public static TimeSpan Reading(long mostMs)
    {
        var fives = Math.Max(1, (long)Math.Ceiling(Math.Max(0, mostMs) * 2.0 / (5 * 60 * 1000)));
        return TimeSpan.FromMinutes(fives * 5);
    }

    /// <summary>A deadline no measure set yet: <paramref name="why"/> says what it stands on until one does.</summary>
    public static BusDeadline Stated(TimeSpan within, string why) => new(within, why, Provisional: true);
}

/// <summary>
/// One thing that ends an obliged entry's timer (<see cref="BusTypes.Ends"/>): a later fact of <see cref="Type"/> on the same subject or the
/// same effect key, or, with no type, the obliged handler's own acknowledgement, which its subscription gives.
/// </summary>
public sealed record BusEnd(BusType? Type, string By)
{
    /// <summary>The handler's acknowledgement: until its subscription comes, nothing on the log clears it, and the comparer reads the look.</summary>
    public static readonly BusEnd Acknowledged = new(null, BusEnds.Acknowledgement);

    /// <summary>A later fact of <paramref name="type"/> on the same subject.</summary>
    public static BusEnd OnSubject(BusType type) => new(type, BusEnds.Subject);

    /// <summary>A later fact of <paramref name="type"/> with the same effect key: the same attempt.</summary>
    public static BusEnd OnEffect(BusType type) => new(type, BusEnds.Effect);
}

/// <summary>How an end matches the timer it clears.</summary>
public static class BusEnds
{
    /// <summary>The same subject.</summary>
    public const string Subject = "subject";

    /// <summary>The same effect key.</summary>
    public const string Effect = "effect";

    /// <summary>The obliged handler's acknowledgement, given by its subscription.</summary>
    public const string Acknowledgement = "acknowledgement";

    public static readonly IReadOnlyList<string> All = [Subject, Effect, Acknowledgement];
}

/// <summary>Whether a type is a fact, a request or a response.</summary>
public static class BusClasses
{
    /// <summary>Something that happened: its obliged one is a handler.</summary>
    public const string Fact = "fact";

    /// <summary>Something asked: its obliged one is its answerer, and it always ends in a response, the bus's own if need be.</summary>
    public const string Request = "request";

    /// <summary>A request's answer.</summary>
    public const string Response = "response";

    public static readonly IReadOnlyList<string> All = [Fact, Request, Response];
}

/// <summary>
/// The closed list of what a give-up does: the bus gives nothing up silently, and the last step of every chain is the owner's final exit.
/// </summary>
public static class BusGiveUps
{
    /// <summary>The bus answers the request itself with a typed refusal naming the miss, and the requester goes on.</summary>
    public const string Refused = "refused";

    /// <summary>The gate the entry would open stays shut, its miss said on its run and on Stalls, never passed for want of an answer.</summary>
    public const string Held = "held";

    /// <summary>An observation no one must act on is dropped and counted.</summary>
    public const string Dropped = "dropped";

    /// <summary>
    /// The subject the entry obliged (a step, a wait) reaches the owner's final exit: one notice with everything tried, then the person's
    /// row until the owner looks; never given up in its place.
    /// </summary>
    public const string FinalExit = "final-exit";

    public static readonly IReadOnlyList<string> All = [Refused, Held, Dropped, FinalExit];
}

/// <summary>Why a payload's field or value is dropped at emit, each counted by the day and said once.</summary>
public static class BusDrops
{
    /// <summary>The type declares no such field.</summary>
    public const string Undeclared = "undeclared";

    /// <summary>The value is not of its field's shape: text that is no id or code, a number where none is declared, an id a list cannot carry.</summary>
    public const string Shape = "shape";

    /// <summary>The field is named twice; the first is kept.</summary>
    public const string Twice = "twice";
}
