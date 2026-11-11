namespace Daoris.Driver;

/// <summary>
/// An Accept's kind of work (<see cref="ReviewKinds"/>), as its attempt's review record holds it, which the supervisor's card is worded by;
/// null for any other act, or where no record is kept.
/// </summary>
public static class LandingWork
{
    public static string? Of(string home, SupervisorLandingView view) =>
        view.Do == SupervisorLandingActs.Accept ? ReviewRecords.Read(home, view.Run, view.Node, view.Attempt)?.Kind : null;
}
