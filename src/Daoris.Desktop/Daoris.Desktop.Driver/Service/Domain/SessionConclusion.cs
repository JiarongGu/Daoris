namespace Daoris.Driver;

/// <param name="State">The terminal state, in the wire spelling the ledger accepts.</param>
/// <param name="Note">What is observed — the sentence the record keeps.</param>
public sealed record SessionConclusion(string State, string Note)
{
    /// <summary>
    /// The note's parts, each line's code and values beside its English; null where its writer composed
    /// none, which the record then carries as one <see cref="NoteBy.Before"/> part when a line is added to it.
    /// </summary>
    public IReadOnlyList<NotePart>? Parts { get; init; }

    /// <summary>A conclusion whose note is composed.</summary>
    public static SessionConclusion Of(string state, Noted noted) => new(state, noted.Note) { Parts = noted.Parts };

    /// <summary>The note as composed: its parts, or its English whole.</summary>
    public Noted AsNoted() => Noted.From(Note, Parts);

    /// <summary>The same state, its note followed by the glue and a line (an account's).</summary>
    public SessionConclusion Then(string glue, Noted line) => Of(State, AsNoted().Then(glue, line));

    /// <summary>
    /// The same state, its note without the agent's words that a line will say as facts: a limit's sentence names
    /// the zone of the machine it is said on, and the note travels. Unchanged where the note holds no such words part.
    /// </summary>
    public SessionConclusion Unsaid(string words)
    {
        var noted = AsNoted();
        var unsaid = noted.Unsaid(words, NoteBy.Agent);
        return ReferenceEquals(unsaid, noted) ? this : Of(State, unsaid);
    }
}
