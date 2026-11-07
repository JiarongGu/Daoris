using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A refused build says its refusal at every look, and the person's next word tries again, since its cause may have moved: a going back
/// while a delivery is unresolved answers *Update now* with the sentence naming the delivery, and once it settles, the next press, from
/// the screen or the terminal's request, installs it. A look with no new word checks nothing again.
/// </summary>
public sealed class UpdateRefusalRetryTests : Bridge, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly string _install;

    private DateTimeOffset _now = Now;

    private int _closed;

    public UpdateRefusalRetryTests()
    {
        _install = Path.Combine(Home, "install");
        StagedInstalls.Prepare(_install);
        StagedInstalls.Stage(_install, "b1");
    }

    private string DataHome => Path.Combine(_install, "data");

    // A running build above the stage's 0.0.1, so installing it is a going back.
    private InstallUpdater Updater() => new(
        _install, DataHome, new MachineLog(DataHome, "desktop", () => Now),
        work: () => new UpdateWork(0, 0),
        relaunch: _ => Task.FromResult(true),
        close: () => _closed++,
        bus: Bus, clock: () => _now, pid: 4242)
        { Running = "0.42.0", ClosingForUpdate = _ => { } };

    [Fact]
    public async Task Update_now_while_a_delivery_is_unresolved_says_why_and_the_next_press_installs_once_it_settles()
    {
        var (store, claim) = Unresolved();
        using var updater = Updater();

        var refused = await updater.SayAsync(UpdateMode.Now, "screen");

        Assert.Equal(UpdateStates.Refused, refused.State);
        Assert.Equal(InstallUpdater.Deliveries, refused.Problem?.Code);
        Assert.Contains("1 delivery is unresolved (`unit-1` by branch at a1b2c3d4)", refused.Problem?.Message);

        store.Settle(claim, DeliverySettlements.Stopped, "stopped by the person");
        Assert.Equal(UpdateStates.Refused, (await updater.LookAsync()).State);
        Assert.Single(Logged("update.refused", "warn"));
        Assert.Equal(0, _closed);

        _now = Now.AddMinutes(1);
        Assert.Equal(UpdateStates.Applying, (await updater.SayAsync(UpdateMode.Now, "screen")).State);
        Assert.Equal(1, _closed);
    }

    [Fact]
    public async Task The_terminal_s_new_word_after_a_refusal_tries_again()
    {
        var (store, claim) = Unresolved();
        using var updater = Updater();
        InstallUpdate.Write(DataHome, new UpdateRequest(UpdateMode.Now, "b1", Now));
        Assert.Equal(UpdateStates.Refused, (await updater.LookAsync()).State);

        store.Settle(claim, DeliverySettlements.Stopped, "stopped by the person");
        InstallUpdate.Write(DataHome, new UpdateRequest(UpdateMode.Now, "b1", Now.AddMinutes(1)));

        Assert.Equal(UpdateStates.Applying, (await updater.LookAsync()).State);
        Assert.Equal(1, _closed);
    }

    // The delivery record's pooled connection holds its file until cleared, which the home's removal would meet.
    public new void Dispose()
    {
        SqliteConnection.ClearAllPools();
        base.Dispose();
    }

    private (DeliveryStore Store, DeliveryClaim Claim) Unresolved()
    {
        var store = new DeliveryStore(DataHome, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);
        var started = store.Start(
            new DeliveryIntent("unit-1", "engine", "a1b2c3d4e5", DeliveryMethods.Branch, "main", "main", "feature/tidy", null, "Tidy", null, "epoch-1"),
            DeliveryAuthorization.Person("accept-1", "r1", "graph-digest", """{"method":"branch"}""", "[]"));
        Assert.True(started.Started, started.Refused ?? started.Held);
        return (store, started.Claim!);
    }

    private List<JsonObject> Logged(string name, string level)
    {
        var folder = DaorisHome.Entry(DataHome, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .Where(line => line["event"]!.GetValue<string>() == name && line["level"]!.GetValue<string>() == level)
            .ToList();
    }
}
