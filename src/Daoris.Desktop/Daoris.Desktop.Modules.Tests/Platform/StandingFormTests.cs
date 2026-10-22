using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// TERMTRUST1a part (ii) (the terminal-trust design §2.1 and §8 case 6): the payloads the verbs send through the running Daoris, as the
/// driver's <c>StandingFormTests.Sent</c> holds them byte for byte, are each covered by the judge as its press. A twin: a verb that grows
/// a field fails the driver's table, and a form the judge narrows fails this one.
/// </summary>
public sealed class StandingFormTests : Bridge
{
    /// <summary>The driver's <c>StandingFormTests.Sent</c>, the same table.</summary>
    public static TheoryData<string, string, string> Sent => new()
    {
        { StandingPresses.Stop, "STOP_SESSION", """{"id":"s1a2b3c4","said":true}""" },
        { StandingPresses.Finish, "RESOLVE_SESSION", """{"id":"s1a2b3c4","state":"completed","said":true}""" },
        { StandingPresses.Hold, "SET_HOLD", """{"repository":"engine","held":true}""" },
        { StandingPresses.Resume, "SET_HOLD", """{"repository":"engine","held":false}""" },
    };

    [Theory]
    [MemberData(nameof(Sent))]
    public void Each_payload_a_verb_sends_is_covered_as_its_press(string press, string route, string payload)
    {
        var standing = new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: true);
        standing.Load();
        Assert.True(standing.Set(StandingPresses.Built).Done);
        // A resume is of a hold a covered hold made this run, from not held to held.
        if (press == StandingPresses.Resume)
        {
            Assert.True(standing.Judge(DriverModule.Name, "SET_HOLD", JsonDocument.Parse("""{"repository":"engine","held":true}""").RootElement, null).Covered);
            standing.HeldNow = repository => repository == "engine";
        }

        var judged = standing.Judge(DriverModule.Name, route, JsonDocument.Parse(payload).RootElement, null);

        Assert.Equal((true, press), (judged.Covered, judged.Press));
    }
}
