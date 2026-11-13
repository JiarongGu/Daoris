namespace Daoris.Ports.Processes;

/// <summary>The classes the machine places a start under.</summary>
public static class AdmissionClasses
{
    /// <summary>A start a person waits on: it always finds a place the background cannot hold.</summary>
    public const string Person = "person";

    /// <summary>The machine's own starts.</summary>
    public const string Background = "background";
}

/// <summary>
/// The application's admission of one start, asked before the machine places it: it throws to refuse the start, or answers the place
/// the start belongs to, or null where the machine places the start alone. The root hands it, so the machine's adapter never names
/// the policy that admits a start, which is the application's (git's scheduler issues git's places).
/// </summary>
/// <param name="program">The program the start names.</param>
/// <param name="purpose">What the start is for.</param>
public delegate StartPlace? StartPolicy(string program, string purpose);

/// <summary>A place the application's admission put a start in.</summary>
/// <param name="AdmissionClass">The class the machine places the start under, in place of the one its caller asked.</param>
/// <param name="Bind">Told the started process's id, so the place is held until that process exits.</param>
public sealed record StartPlace(string AdmissionClass, Action<int> Bind);
