namespace Daoris.Driver;

/// <summary>
/// What an executor holds of a live run (the design §2.5): open or resume, apply a refused choice's later one, take words, steer,
/// stop a turn or a task, and close. Each operation is admitted by the run's own evidence (§2.4) and one it lacks is answered
/// <see cref="Unsupported"/> naming the ability, before anything is written. Row b declares it; row m makes the executors hold it, and
/// then adds the parameters it names there (<c>DoorInput</c>, <c>IChoiceReask</c>, <c>IResumeState</c>) that no type holds yet.
/// </summary>
public abstract class AgentRun
{
    /// <summary>The answer of an operation the run's evidence does not admit, naming the ability.</summary>
    public static DriverException Unsupported(string ability) =>
        new($"this door does not support {ability} — nothing was written.");

    public abstract Task OpenAsync(CancellationToken ct);

    public abstract Task ResumeAsync(string conversation, CancellationToken ct);

    /// <summary>A refused model's later choice, carried on the same session (O4-F5).</summary>
    public abstract Task ApplyChoiceAsync(ActorAsked choice, CancellationToken ct);

    public abstract Task SendAsync(string text, CancellationToken ct);

    public abstract Task SteerAsync(string text, CancellationToken ct);

    public abstract Task StopTurnAsync(CancellationToken ct);

    public abstract Task StopTaskAsync(string task, CancellationToken ct);

    public abstract Task CloseAsync(CancellationToken ct);
}

/// <summary>
/// The name a protocol door's resume carries (the design §2.6, X): the transport's default recorded as eligibility to negotiate, which
/// planning reads and the open settles by the agent's own answer, never a resume known.
/// </summary>
public static class Eligibility
{
    public const string Applies = "eligibility, negotiated at the open";
}
