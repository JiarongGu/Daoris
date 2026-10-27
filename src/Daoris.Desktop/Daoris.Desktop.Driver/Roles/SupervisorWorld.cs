namespace Daoris.Driver;

/// <summary>
/// What <c>sessions supervise</c> touches of the terminal's world (<see cref="SessionsSuperviseCommand"/>): the service, the home, the
/// configuration, the running Daoris and the holder key this verb took at its entry. The sessions verb's own <c>SessionsWorld</c> implements it
/// directly, so the supervisor's verb names none of the sessions verb's types.
/// </summary>
public interface ISupervisorWorld
{
    /// <summary>The service the record, the quest and the ask are read from.</summary>
    ServiceClient Service { get; }

    /// <summary>The home whose question files and request folder the verb reads.</summary>
    string Home { get; }

    /// <summary>The home's choices, whose standing answers the print reads.</summary>
    DriverConfig Config { get; }

    /// <summary>Where the running Daoris is looked for; null looks for none.</summary>
    InstanceWorld? Instance { get; }

    /// <summary>The holder key this verb took from its terminal's environment at its entry, or null where it had none.</summary>
    Func<string?> HolderKey { get; }
}

/// <summary>What <c>sessions supervise</c> reads of the verb's ask: the form it takes, the session it names and its draft.</summary>
public interface ISupervisorAsk
{
    /// <summary><c>supervise-list</c>, <c>supervise-print</c>, <c>supervise-answer</c> or <c>supervise-leave</c>.</summary>
    string Verb { get; }

    /// <summary>The sessions the verb names.</summary>
    IReadOnlyList<string> Ids { get; }

    /// <summary>The answer or the leaving the verb carries.</summary>
    SupervisorDraft? Supervised { get; }
}
