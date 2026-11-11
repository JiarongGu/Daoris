namespace Daoris.Driver;

/// <summary>
/// The model a supervisor's turn ran on, as its harness reported it for that session's turns (each answer's model on the native door,
/// each usage update's on the protocol door), never the model asked, which its holder keeps beside it. None reported is unknown.
/// </summary>
internal sealed class SuperviseRan : IDisposable
{
    private readonly ISessionEvents _events;
    private readonly string _session;
    private readonly ActorReports _reports = new();
    private readonly Lock _gate = new();

    public SuperviseRan(ISessionEvents events, string session)
    {
        (_events, _session) = (events, session);
        events.Evented += Heard;
    }

    /// <summary>Each model reported, once and in the order reported; null where the harness reports none.</summary>
    public string? Model
    {
        get
        {
            lock (_gate) return _reports.Snapshot is { Models.Count: > 0 } ran ? string.Join(", ", ran.Models) : null;
        }
    }

    private void Heard(string session, SessionEvent e)
    {
        if (session != _session || e.Kind != SessionEventKind.Turn) return;
        lock (_gate) _reports.Report(e.Models?.Reported ?? []);
    }

    public void Dispose() => _events.Evented -= Heard;
}
