namespace Daoris.Driver;

/// <summary>D205's tagged request: an explicit tool alias `default` differs from an account default.</summary>
public sealed record ActorValue(string Source, string? Value, bool Carried);

/// <summary>
/// The model and effort a session was asked to run with, each tagged by where it came from (D205 §4.1), as the service keeps it on the
/// record (<c>asked</c>). The shape is the service's, which reads and writes it; what a start or a resume makes of it (the opening's
/// default, the composer's change laid over it, the spawn's command line and the native arguments) is the roles' (<see cref="ActorAskedChoices"/>).
/// </summary>
public sealed record ActorAsked(string Agent, ActorValue Model, ActorValue Effort)
{
    /// <summary>
    /// The model its choice named, which each account's offer reading is judged for (D205 §4.1, the walk's model parameter); null for
    /// account default, the person's own command or environment, which no reading passes.
    /// </summary>
    public string? NamedModel => Model is { Source: "entry" or "override", Value: { } model } ? model : null;
}
