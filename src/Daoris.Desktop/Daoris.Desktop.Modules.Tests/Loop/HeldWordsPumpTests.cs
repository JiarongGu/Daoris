using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The pump of the words held for a record to end goes on after a failure its reads do not expect: the words stay held, and the record's next
/// move, or the next word held, tries them again, rather than the pump standing ended with its flag set until the shell restarts.
/// </summary>
public sealed class HeldWordsPumpTests : DriverModuleBridge
{
    private const string Key = "press/9b2e";

    private const string Words = "Port 9090, not 8080.";

    private static string Records(string state) =>
        $$"""[{"id":"c1","repository":"engine","state":"{{state}}","kind":"chat","created":"2026-10-03T08:00:00Z"}]""";

    [Fact]
    public async Task A_pump_ended_by_an_unexpected_failure_tries_the_held_words_again_at_the_records_next_move()
    {
        var winding = true;
        var service = new KeyedSayService { Records = Records("working"), Winding = _ => winding };
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(service)));
        var held = await loop.Words.SayAsync("c1", Words, [], preface: null, "screen", CancellationToken.None, key: Key);
        Assert.True(held!.Sent);

        winding = false;
        service.Records = Records("completed");
        service.SayFault = new InvalidOperationException("a failure no read expects");
        await loop.Service!.AdvanceAsync("c1", "completed", note: "the conversation ended.");
        await UntilAsync(() => service.Faults > 0);
        Assert.True(File.Exists(SessionWords.HeldPath(Home)));

        service.SayFault = null;
        await loop.Service!.AdvanceAsync("c1", "completed", note: "the conversation ended again.");

        await UntilAsync(() => !File.Exists(SessionWords.HeldPath(Home)));
        Assert.Equal(1, service.Kept("c1"));
    }
}
