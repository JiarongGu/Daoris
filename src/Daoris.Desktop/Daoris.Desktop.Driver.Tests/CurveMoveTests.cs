using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SWITCH1c-ii (D165 point 3; the switch design §1.2, §2 and §4): a conversation over its account's usage curve moves on its own,
/// at its turn's end, to another account of the same agent that has room. What the runner judges: a turn just ended with nothing
/// queued, no hand-over waiting or taken, the scope's switch, the account's reading, the others read where the decision asks, and
/// the thirty minutes' hold from the record's <c>created</c> where it moved by the curve. The real client over an in-process ledger
/// and a stand-in agent whose usage question answers in memory, so nothing is spawned: the fast half. A conversation that ends and
/// another that starts are <c>CurveMoveProcessTests</c>'.
/// </summary>
public sealed class CurveMoveTests : IDisposable
{
    /// <summary>Ten minutes after the ledger's records were created (<c>2026-10-03T08:00:00Z</c>).</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 8, 10, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-curve-move-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public CurveMoveTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 The hold reads when a record was made (SWITCH1c-ii): `created`, as the ledger answers it, on the record a run reads.</summary>
    [Fact]
    public async Task A_record_says_when_it_was_created()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c0nv0001", "working", "talk", _root, profile: "account-1");
        using var client = ledger.Client();

        var record = (await client.RecordAsync("c0nv0001"))!;

        Assert.Equal(new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero), record.Created);
    }
}
