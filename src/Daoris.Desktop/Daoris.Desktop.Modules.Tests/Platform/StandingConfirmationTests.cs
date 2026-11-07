namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The stand-ins the roles' judge (<see cref="StandingConfirmation"/>) is tested with: where a terminal's process stands, the
/// configuration walk, and a holder's tab. The judge's own cases are <c>RoleTerminalTests</c>', <c>RoleActTests</c>' and
/// <c>SupervisorScopeTests</c>'.
/// </summary>
internal static class StandingConfirmationTests
{
    /// <summary>A caller's look that answers one place for every client.</summary>
    internal sealed class Placed(StandingPlace place) : IStandingCaller
    {
        public StandingPlace Answer { get; set; } = place;

        public StandingPlace Place(PipeClientProcess? client) => Answer;
    }

    /// <summary>A walk that answers one thing for every act (STEWARDROLE1a-i): null admits; the walk itself is <c>PipeStandingTests</c>' and <c>HolderWalkTests</c>'.</summary>
    internal sealed class Walked(string? why = null) : IHolderWalk
    {
        public string? Why { get; set; } = why;

        public string? Check(PipeClientProcess? client, HolderProcess? harness) => Why;
    }

    /// <summary>A holder's tab as a stand-in opens it: named by the holder, its harness a process nobody started.</summary>
    internal static HolderOpened Opened(HolderOpen open) =>
        new($"t-{open.Holder}", new HolderProcess(4242, DateTime.UnixEpoch, @"C:\home\toolchain\claude-code\2.1.300\bin\claude.exe", @"C:\home\config"), null, null);
}
