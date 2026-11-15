namespace Daoris.Knowledge;

/// <summary>
/// How a turn of a record that went on ended, kept on that turn as the go-on writes it: a go-on forgives the record's own flags, so its
/// earlier turns' ends are read here and the record's state says only its last turn's.
/// </summary>
/// <param name="State">The state its record ended in, as the doors spell it.</param>
/// <param name="Limit">An account's limit made its failure.</param>
/// <param name="Interrupted">A stop that was not the person's.</param>
/// <param name="Codes">Its end's note codes, in order, by which a reader judges the end and never by its English.</param>
public sealed record SessionTurnEnd(string State, bool Limit = false, bool Interrupted = false, IReadOnlyList<string>? Codes = null)
{
    /// <summary>The end <paramref name="record"/> stands at now, which is its last turn's.</summary>
    public static SessionTurnEnd Of(Session record) =>
        new(record.StateName, record.Limit, record.Interrupted, NoteParts.Codes(record.NoteParts));

    public bool Equals(SessionTurnEnd? other) =>
        other is not null && State == other.State && Limit == other.Limit && Interrupted == other.Interrupted
        && (Codes ?? []).SequenceEqual(other.Codes ?? []);

    public override int GetHashCode() => HashCode.Combine(State, Limit, Interrupted, (Codes ?? []).Count);
}
