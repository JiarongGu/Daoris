using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent supervisor's decision on a work's landing or departure is refused <c>own-work</c> where the session its turn ran in is one of the
/// work's makers or a helper they started, read from the holder the decision door keeps and never from its words; a terminal's holder names
/// no session and is the person's channel, and a work whose makers are not read refuses nothing on a guess, since the role's own turn is
/// Daoris's, not a working session. Seen failing first: the admission read the grant and the hold alone.
/// </summary>
public sealed class SupervisorOwnWorkTests
{
    private static SupervisorHolder Agent(string session) =>
        SupervisorHolders.OfAgent(new SupervisorAgentHolder("supervisor", 1, "claude", null, null, null, "read-only", session));

    [Fact]
    public void A_maker_or_a_helper_it_started_is_refused_and_an_independent_session_is_not()
    {
        IReadOnlyCollection<string> makers = ["s-worker", "s-helper"];

        Assert.Equal(SupervisorWhy.OwnWork, SupervisorOwnWork.Refused(Agent("s-worker"), makers));
        Assert.Equal(SupervisorWhy.OwnWork, SupervisorOwnWork.Refused(Agent("S-HELPER"), makers));
        Assert.Null(SupervisorOwnWork.Refused(Agent("s-supervisor"), makers));
    }

    [Fact]
    public void A_terminals_holder_and_makers_not_read_refuse_nothing()
    {
        Assert.Null(SupervisorOwnWork.Refused(new SupervisorHolder(RoleHolders.Terminal, null), ["s-worker"]));
        Assert.Null(SupervisorOwnWork.Refused(Agent("s-worker"), null));
    }

    [Fact]
    public void A_landings_run_and_a_departures_quest_name_the_run_whose_makers_are_read()
    {
        Assert.Equal("q7", SupervisorOwnWork.RunOf(SupervisorSubject.Landing("s-1", "q7", "review", 1)));
        Assert.Null(SupervisorOwnWork.RunOf(SupervisorSubject.Question("s-1", "k1")));
    }
}
