namespace Daoris.Driver;

/// <summary>
/// One reviewer, helper or partner entry a walk tried and passed, as the one-turn re-ask reads it (<see cref="OneTurnReask{TChoice, TPassed}"/>):
/// the actor's entry that was tried, the code that held it and the account walk's own sentence. Review's <c>ReviewerTried</c> implements it.
/// </summary>
public interface IActorTried
{
    /// <summary>The actor's entry for the agent that was tried, with its model; null where a caller named none.</summary>
    ActorEffective? Entry { get; }

    /// <summary>What held it (a code of the owner's walk).</summary>
    string Held { get; }

    /// <summary>The account walk's own sentence, which names the fix. Machine-local: it may name an account.</summary>
    string Refusal { get; }
}

/// <summary>
/// An owner's walk answer, as the one-turn re-ask reads and re-selects it (<see cref="OneTurnReask{TChoice, TPassed}"/>): whether an entry was chosen,
/// the actor's entry that runs, the account walk's selection it runs on and what the walk passed. The owner's own choice implements it directly
/// (Review's <c>ReviewerChoice</c>), so the re-ask names none of the owner's types and hands its own back.
/// </summary>
/// <typeparam name="TChoice">The implementing choice, which the re-ask returns.</typeparam>
public interface IActorChoice<TChoice> where TChoice : IActorChoice<TChoice>
{
    /// <summary>Whether an entry was chosen.</summary>
    bool Chosen { get; }

    /// <summary>The actor's entry that runs; null where none does.</summary>
    ActorEffective? Effective { get; }

    /// <summary>The account walk's answer for the entry, which its start runs on; null where none can.</summary>
    HarnessSelection? Selection { get; }

    /// <summary>Each entry tried and passed, in the order tried.</summary>
    IReadOnlyList<IActorTried> Tried { get; }

    /// <summary>The same choice on <paramref name="selection"/>: the session that goes on with a later entry keeps the account it opened on.</summary>
    TChoice WithSelection(HarnessSelection? selection);
}

/// <summary>
/// One (entry, account) a one-turn owner already passed in its own call, as the re-ask keeps and hands it back to the owner's walk
/// (<see cref="OneTurnReask{TChoice, TPassed}"/>). The owner's record of it (Review's <c>ReviewerPassed</c>) implements it.
/// </summary>
/// <typeparam name="TPassed">The implementing record, which the re-ask makes and hands back.</typeparam>
public interface IActorPassed<TPassed> where TPassed : IActorPassed<TPassed>
{
    /// <summary>The actor's entry that was passed.</summary>
    ActorEffective Entry { get; }

    /// <summary>The account it was passed on; null for an agent running under its own sign-in.</summary>
    string? Account { get; }

    /// <summary>The record of passing <paramref name="entry"/> on <paramref name="account"/>.</summary>
    static abstract TPassed Of(ActorEffective entry, string? account);
}
