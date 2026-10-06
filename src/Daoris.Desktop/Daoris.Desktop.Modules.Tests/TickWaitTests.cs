using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A wait as the loop's tick hands it to the page (<see cref="DriverLoop.TickWait"/>, UX6d, D150 §6.2): starts held because
/// every account they may use is cooling, one per account, so *What needs you* lists a start waiting for accounts, an ask's
/// intake among them, which the considerations never carried (they name quests alone).
/// </summary>
/// <remarks>
/// 🔴 <b>No row starts a process to find out</b> (D150 §6.3): what the tick hands is the look's own wait, read from the
/// cool-offs and the refusals the start met (D125 §4), so building it asks no account and starts nothing. These are facts,
/// never the driver's English: the page says them in the reader's language.
/// </remarks>
public sealed class TickWaitTests
{
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly DateTimeOffset Until = new(2026, 10, 6, 4, 42, 0, TimeSpan.Zero);

    private static AccountWait Wait() =>
        new("claude-code-acp", "claude-code", "account-2", "work", Until, true,
            "every `claude-code` account `work` may use is cooling; the first ready, `account-2`, at Oct 6, 04:42 (UTC).")
        {
            Quests = ["q1"],
            Asks = ["a1", "a2"],
            Repositories = ["engine", "ask #a1", "ask #a2"],
            SignedOut = ["account-1", "account-3"],
            Name = "home",
        };

    /// <summary>
    /// The wait's facts: whose account, which and its name, the workspace, until when and whether the agent said so, the
    /// quests and the asks whose intakes it holds, and the accounts it passed signed out.
    /// </summary>
    [Fact]
    public void A_wait_hands_the_page_whose_account_until_when_and_what_it_holds()
    {
        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickWait(Wait()), Wire);

        Assert.Equal("claude-code", shape.GetProperty("agent").GetString());
        Assert.Equal("account-2", shape.GetProperty("account").GetString());
        Assert.Equal("home", shape.GetProperty("name").GetString());
        Assert.Equal("work", shape.GetProperty("workspace").GetString());
        Assert.Equal(Until, shape.GetProperty("until").GetDateTimeOffset());
        Assert.True(shape.GetProperty("stated").GetBoolean());
        Assert.Equal(["q1"], shape.GetProperty("quests").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(["a1", "a2"], shape.GetProperty("asks").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(["account-1", "account-3"], shape.GetProperty("signedOut").EnumerateArray().Select(each => each.GetString()));
    }

    /// <summary>
    /// Facts only: the driver's sentence and the adapter the starts ride stay here, since the page words the wait in the
    /// reader's language and names the agent, never a door.
    /// </summary>
    [Fact]
    public void A_wait_carries_no_sentence_and_no_door()
    {
        var raw = JsonSerializer.SerializeToElement(DriverLoop.TickWait(Wait()), Wire).GetRawText();

        Assert.DoesNotContain("may use is cooling", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("claude-code-acp", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("ask #", raw, StringComparison.Ordinal);
    }

    /// <summary>The tool's own sign-in cooling is a null account, with no name; a wait across workspaces names none.</summary>
    [Fact]
    public void The_own_sign_in_s_wait_is_no_account_and_a_wait_across_workspaces_names_none()
    {
        var own = Wait() with { Account = null, Name = null, Workspace = null, SignedOut = [] };

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickWait(own), Wire);

        Assert.Equal(JsonValueKind.Null, shape.GetProperty("account").ValueKind);
        Assert.Equal(JsonValueKind.Null, shape.GetProperty("name").ValueKind);
        Assert.Equal(JsonValueKind.Null, shape.GetProperty("workspace").ValueKind);
        Assert.Empty(shape.GetProperty("signedOut").EnumerateArray());
    }

    /// <summary>
    /// The tick is forwarded when what the waits say changes (as the considerations' signature does), so a wait that begins
    /// or ends for an intake alone, which moves no consideration, still reaches the page; their order is no change.
    /// </summary>
    [Fact]
    public void The_waits_signature_moves_with_what_they_hold_and_not_with_their_order()
    {
        var first = Wait();
        var second = Wait() with { Account = "account-4", Quests = [], Asks = ["a3"] };

        Assert.Equal(DriverLoop.WaitsSignature([first, second]), DriverLoop.WaitsSignature([second, first]));
        Assert.NotEqual(DriverLoop.WaitsSignature([first]), DriverLoop.WaitsSignature([first, second]));
        Assert.NotEqual(DriverLoop.WaitsSignature([first]), DriverLoop.WaitsSignature([first with { Asks = ["a1"] }]));
        Assert.NotEqual(DriverLoop.WaitsSignature([first]), DriverLoop.WaitsSignature([first with { SignedOut = ["account-1"] }]));
        Assert.NotEqual(DriverLoop.WaitsSignature([first]), DriverLoop.WaitsSignature([first with { Until = Until.AddHours(1) }]));
        Assert.Equal("", DriverLoop.WaitsSignature([]));
    }
}
