namespace Daoris.Driver;

/// <summary>How a limit's go-on onto another account of the same agent takes its conversation: carried whole, carried and compacted, or briefed.</summary>
internal abstract record CarryPlan
{
    private CarryPlan() { }

    /// <summary>The conversation goes on on the new account: its id, its context and, where it is compacted first, the variables that ask it.</summary>
    internal abstract record Carried(string Conversation, long? Tokens) : CarryPlan;

    /// <summary>Carried as it is.</summary>
    internal sealed record Whole(string Conversation, long? Tokens) : Carried(Conversation, Tokens);

    /// <summary>Carried, its harness compacting it before it goes on, since its context is past <paramref name="Bound"/>.</summary>
    internal sealed record Compacted(string Conversation, long? Tokens, long Bound, IReadOnlyDictionary<string, string> Variables)
        : Carried(Conversation, Tokens);

    /// <summary>Not carried: the new part is handed Daoris's brief, <paramref name="Why"/> by a <see cref="LimitCarry"/> code.</summary>
    internal sealed record Briefed(string Why) : CarryPlan;
}
