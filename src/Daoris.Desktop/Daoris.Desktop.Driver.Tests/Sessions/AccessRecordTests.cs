using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTONOMY1a (D184 point 1): a driven start keeps the access setup it started under on its record, as D183 keeps Ask Daoris's mode.
/// The start resolves its repository's setup and hands it to the open, which the service keeps on this machine's record alone; its
/// transcript's start note says it in one line where it is not today's behaviour.
/// </summary>
/// <remarks>
/// The start itself runs a real process, which only the <c>Process</c> half reaches; these hold its two seams: the open's body, read
/// back from the stand-in ledger as the service answers it on loopback, and the note a start keeps.
/// </remarks>
public sealed class AccessRecordTests : IDisposable
{
    private readonly StandInLedger _ledger = new();

    public void Dispose() => _ledger.Dispose();

    [Fact]
    public async Task A_driven_open_carries_the_setup_its_start_resolved_and_one_told_none_carries_none()
    {
        _ledger.Publish("q1", "engine");
        _ledger.Publish("q2", "engine");
        using var service = _ledger.Client();
        var config = DriverConfig.Empty
            .WithAccess(new AccessEdit { Workspace = "shop", Full = true })
            .WithAccess(new AccessEdit { Repository = "engine", Set = [new("permissions", "asking")] });

        var (started, _) = await service.OpenSessionAsync("q1", "stub", access: AccessRules.Resolve(config, "engine", "shop").Values);
        var (plain, _) = await service.OpenSessionAsync("q2", "stub");

        Assert.Equal(
            """{"permissions":"asking","routing":"intake","landing":"rule","goAheads":"never","questions":"claude-code"}""",
            _ledger.Session(started!)["access"]!.ToJsonString());
        Assert.Null(_ledger.Session(plain!)["access"]);
    }

    [Fact]
    public void A_start_under_today_s_behaviour_notes_nothing_and_any_other_names_its_setup_in_one_line()
    {
        Assert.Null(AccessRules.StartNote(AccessRules.ResolveLevels(null, null, null)));
        // Set, but to today's values: still today's behaviour, so the transcript reads as it always did.
        Assert.Null(AccessRules.StartNote(AccessRules.ResolveLevels(
            new AccessSetting { Pauses = new Dictionary<string, string> { [AccessRules.Permissions] = AccessRules.Auto } }, null, null)));

        Assert.Equal(
            "Access setup: full access, set for its repository — permissions unrestricted, routing intake, landing rule, goAheads never, questions claude-code.",
            AccessRules.StartNote(AccessRules.ResolveLevels(AccessSetting.FullAccess, null, null)));
        Assert.Equal(
            "Access setup: permissions auto (the default), routing person (the default), landing rule (this machine), goAheads named (the default), questions claude-code (the default).",
            AccessRules.StartNote(AccessRules.ResolveLevels(
                null, null, new AccessSetting { Pauses = new Dictionary<string, string> { [AccessRules.Landing] = AccessRules.Rule } })));
    }
}
