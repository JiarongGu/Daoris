namespace Daoris.Ports.Processes;

/// <summary>
/// Places a start of a class on the machine, which is what lets a start refuse for want of a place: the application asks before it
/// starts and releases the place it got.
/// </summary>
public interface IMachineAdmission
{
    /// <summary>Waits for a place for a start of <paramref name="admissionClass"/> and answers the place to release.</summary>
    ValueTask<IAdmittedPlace> PlaceAsync(string admissionClass, CancellationToken ct);
}

/// <summary>A place the machine lent to one start; disposing it gives the place back.</summary>
public interface IAdmittedPlace : IAsyncDisposable
{
    /// <summary>The class it is lent to.</summary>
    string AdmissionClass { get; }
}

/// <summary>Whether a process the application once started still runs.</summary>
public interface IProcessLiveness
{
    /// <summary>
    /// Whether the process of <paramref name="id"/> created at <paramref name="created"/> still runs. The creation time is
    /// part of the question, since an id is reused once its process ends.
    /// </summary>
    bool Runs(int id, DateTimeOffset created);
}
