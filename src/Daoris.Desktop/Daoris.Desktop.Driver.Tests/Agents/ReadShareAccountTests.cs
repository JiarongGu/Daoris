using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// READSHARE1: each reader of a record AtomicFile moves into place reads it while the move still holds it (<see cref="MoveHold"/>).
/// </summary>
public sealed class ReadShareAccountTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 23, 9, 12, 30, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-readshare-accounts-" + Guid.NewGuid().ToString("N")[..8]);

    public ReadShareAccountTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A record written as AtomicFile writes it, its folder made first.</summary>
    private static string Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, text);
        return path;
    }

    /// <summary>The hold refuses a plain read, so what follows under it proves the reader shares deletion.</summary>
    private static void Refused(string path) => Assert.Throws<IOException>(() => File.ReadAllText(path));

    /// <summary>
    /// The hold let go a second on, for a reader that writes after it reads: the read meets the hold at once, and the write's rename,
    /// which a hold refuses as it refuses any rename over the file, lands within its retries once the hold is gone.
    /// </summary>
    private static Task ReleasedSoon(IDisposable hold) => Task.Run(async () =>
    {
        await Task.Delay(1000);
        hold.Dispose();
    });

    /// <summary>READSHARE1: <c>AccountFileNodes.Read</c> reads an account file its move into place still holds.</summary>
    [Fact]
    public void An_account_file_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Put(Path.Combine(_home, "names.json"), """{"claude-code":{"work":"Work"}}""");

        using (MoveHold.Take(path))
        {
            Refused(path);
            var (root, unreadable) = AccountFileNodes.Read(path);
            Assert.False(unreadable);
            Assert.Equal("Work", (string?)root["claude-code"]?["work"]);
        }
    }

    /// <summary>READSHARE1: <c>SessionUsage.Load</c> reads the usage file its move into place still holds.</summary>
    [Fact]
    public void Usage_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var store = new SessionUsage(_home);
        store.Record(new UsageEntry("s1a2b3c4", "web-app", "claude-code", "work", 1200, 200000, At));
        var path = DaorisHome.Entry(_home, HomeEntry.Usage);

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.Equal(1200, store.Of("s1a2b3c4")?.Used);
        }
    }

    /// <summary>READSHARE1: <c>AgentSettings.Load</c>, through <c>Read</c>, reads an account's settings its move into place still holds.</summary>
    [Fact]
    public void An_account_s_settings_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(_home, "account", AgentSettings.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AgentSettings.Write(path, model: new AgentSettingEdit("opus"), effort: new AgentSettingEdit("high"));

        using (MoveHold.Take(path))
        {
            Refused(path);
            var read = AgentSettings.Read(path);
            Assert.Equal(("opus", "high", (string?)null), (read.Model, read.Effort, read.Problem));
        }
    }

    /// <summary>READSHARE1: <c>HarnessSettings.Load</c> reads the harness wiring its move into place still holds.</summary>
    [Fact]
    public void The_harness_wiring_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Put(DaorisHome.Entry(_home, HomeEntry.Harnesses), """{"defaults":{"claude-code":"work"}}""");

        using (MoveHold.Take(path))
        {
            Refused(path);
            var settings = HarnessSettings.Load(path);
            Assert.Null(settings.Problem);
            Assert.Equal("work", settings.Defaults["claude-code"]);
        }
    }

    /// <summary>READSHARE1: <c>HarnessKeys.Read</c>, through <c>Of</c>, reads the keys file its move into place still holds.</summary>
    [Fact]
    public void An_account_s_key_is_read_while_its_move_into_place_still_holds_the_keys()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Put(DaorisHome.Entry(_home, HomeEntry.Keys), """{"claude-code":{"work":"sk-test-key"}}""");

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.Equal("sk-test-key", HarnessKeys.Of(_home, "claude-code", "work"));
        }
    }

    /// <summary>READSHARE1: <c>ClaudeTrust.Accepted</c> reads the harness's config its move into place still holds.</summary>
    [Fact]
    public void A_folder_s_trust_is_read_while_the_move_into_place_still_holds_the_config()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Put(Path.Combine(_home, "account", ClaudeTrust.FileName), """{"projects":{"C:/work/web-app":{"hasTrustDialogAccepted":true}}}""");

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.True(ClaudeTrust.Accepted(path, @"C:\work\web-app"));
            Assert.False(ClaudeTrust.Accepted(path, @"C:\work\engine"));
        }
    }

    /// <summary>READSHARE1: <c>ClaudeTrust.Grant</c> reads the harness's config its move into place still holds, and keeps what it held.</summary>
    [Fact]
    public async Task A_grant_reads_the_config_its_move_into_place_still_holds()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Put(
            Path.Combine(_home, "account", ClaudeTrust.FileName),
            """{"numStartups":3,"projects":{"C:/work/web-app":{"hasTrustDialogAccepted":false}}}""" + "\n");

        TrustGrant grant;
        using (var hold = MoveHold.Take(path))
        {
            Refused(path);
            var released = ReleasedSoon(hold);
            grant = ClaudeTrust.Grant(path, @"C:\work\web-app");
            await released;
        }

        Assert.Equal(("C:/work/web-app", true, true), (grant.Key, grant.Changed, grant.Verified));
        Assert.Equal(3, (int?)JsonNode.Parse(File.ReadAllText(path))!["numStartups"]);
    }

    /// <summary>READSHARE1: <c>DshProfile.Write</c> reads a profile Daoris wrote while its move into place still holds it, and rewrites it, never refusing it as not Daoris's.</summary>
    [Fact]
    public async Task A_held_profile_Daoris_wrote_is_rewritten_not_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Path.Combine(_home, "profiles", "work");
        var path = DshProfile.Write(profile);

        string again;
        using (var hold = MoveHold.Take(path))
        {
            Refused(path);
            var released = ReleasedSoon(hold);
            again = DshProfile.Write(profile);
            await released;
        }

        Assert.Equal(path, again);
        Assert.StartsWith(DshProfile.Header, File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>READSHARE1: <c>PermissionRules.Load</c> reads the permissions file its move into place still holds.</summary>
    [Fact]
    public void The_permission_rules_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        PermissionRules.Save(_home, PermissionRules.Add(PermissionFile.Empty, RuleScope.Machine, null, RuleList.Allow, "Bash(npm test:*)"));
        var path = PermissionRules.PathOf(_home);

        using (MoveHold.Take(path))
        {
            Refused(path);
            var file = PermissionRules.Load(_home);
            Assert.Null(file.Problem);
            Assert.Equal(["Bash(npm test:*)"], file.Machine.Allow);
        }
    }

    private string Proposal(string id)
    {
        var node = new JsonObject
        {
            ["id"] = id,
            ["proposed"] = At.ToString("O"),
            ["by"] = new JsonObject { ["session"] = "s1a2b3c4" },
            ["change"] = new JsonObject { ["action"] = "add", ["scope"] = "machine", ["list"] = "deny", ["rule"] = "Bash(rm:*)" },
            ["why"] = "The session's own reason.",
            ["state"] = "proposed",
        };
        return Put(DaorisHome.Entry(_home, HomeEntry.RuleProposals, $"{id}.json"), node.ToJsonString() + "\n");
    }

    /// <summary>READSHARE1: <c>RuleProposals.Parse</c>, through <c>Load</c>, reads a proposal its move into place still holds.</summary>
    [Fact]
    public void A_rule_proposal_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Proposal("p1");

        using (MoveHold.Take(path))
        {
            Refused(path);
            var proposal = Assert.Single(RuleProposals.Load(_home));
            Assert.Equal(("p1", "Bash(rm:*)", RuleList.Deny), (proposal.Id, proposal.Change.Rule, proposal.Change.List));
        }
    }

    /// <summary>READSHARE1: <c>RuleProposals.Mark</c>, through <c>Answer</c>, reads a proposal its move into place still holds and keeps its keys.</summary>
    [Fact]
    public async Task A_rule_proposal_is_answered_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Proposal("p2");

        using (var hold = MoveHold.Take(path))
        {
            Refused(path);
            var released = ReleasedSoon(hold);
            RuleProposals.Answer(_home, "p2", accept: false, "not now", At);
            await released;
        }

        var root = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal(("declined", "The session's own reason."), ((string?)root["state"], (string?)root["why"]));
    }

    /// <summary>READSHARE1: <c>Tools.Load</c>, through <c>Read</c>, reads the tools file its move into place still holds.</summary>
    [Fact]
    public void The_tools_file_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Put(DaorisHome.Entry(_home, HomeEntry.Tools), """{"tools":{"git":{"use":"managed","version":"2.51.0"}}}""");

        using (MoveHold.Take(path))
        {
            Refused(path);
            var read = Tools.Read(_home);
            Assert.Null(read.Problem);
            Assert.Equal((ToolWay.Managed, "2.51.0"), (read.Entries["git"].Way, read.Entries["git"].Version));
        }
    }

    /// <summary>READSHARE1: <c>ToolResources.ReadFile</c> reads a resource list's bytes while its move into place still holds it.</summary>
    [Fact]
    public void A_resource_list_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Put(Path.Combine(_home, "copy.json"), "{\"schema\":1,\"tools\":{}}\n");

        using (MoveHold.Take(path))
        {
            Refused(path);
            var list = ToolResources.ReadFile(path, "https://example.org/r.json");
            Assert.Equal(
                (true, (string?)null, "c5dd286d689d840e89162ec6c963a65cac731a88e08d4b8389c416d88185ac3b"),
                (list.Exists, list.Problem, list.Sha256));
        }
    }

    /// <summary>READSHARE1: <c>PluginFolders.ReadText</c> and <c>TextIfThere</c> read a file their move into place still holds.</summary>
    [Fact]
    public void A_plugin_file_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins);
        var path = Path.Combine(folder, "state.json");
        PluginFolders.WriteText(folder, path, """{"on":["review"]}""");

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.Equal(("""{"on":["review"]}""", (string?)null), PluginFolders.ReadText(path));
            Assert.Equal("""{"on":["review"]}""", PluginFolders.TextIfThere(path));
        }
    }

    /// <summary>READSHARE1: <c>HelpGrants.Approvals</c>, through <c>Withdraw</c>, reads the approvals its move into place still holds.</summary>
    [Fact]
    public async Task The_help_approvals_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Put(
            HelpGrants.PathIn(_home),
            """{"help":[{"plugin":"notes","server":"read","allow":["search"]},{"plugin":"tracker","server":"issues","allow":["list"]}]}""" + "\n");

        using (var hold = MoveHold.Take(path))
        {
            Refused(path);
            var released = ReleasedSoon(hold);
            HelpGrants.Withdraw(_home, "notes", "read");
            await released;
        }

        var help = Assert.IsType<JsonArray>(JsonNode.Parse(File.ReadAllText(path))!["help"]);
        Assert.Equal("tracker", (string?)Assert.Single(help)!["plugin"]);
    }

    /// <summary>READSHARE1: <c>RemoteTarget.Load</c>, through <c>LoadFile</c>, reads the remotes map its move into place still holds.</summary>
    [Fact]
    public void The_remotes_map_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = DaorisHome.Entry(_home, HomeEntry.Remotes);
        RemoteTarget.Save(path, new Dictionary<string, RemoteTarget> { ["web"] = new("https://daoris.example.org", "dk_test") });

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.Equal("https://daoris.example.org", RemoteTarget.LoadFile(path)["web"].Url);
        }
    }

    /// <summary>READSHARE1: <c>RemoteFile.Load</c> reads the remotes map its move into place still holds, as an editor reads it.</summary>
    [Fact]
    public void The_remotes_file_is_read_for_an_edit_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = DaorisHome.Entry(_home, HomeEntry.Remotes);
        RemoteTarget.Save(path, new Dictionary<string, RemoteTarget> { ["web"] = new("https://daoris.example.org", "dk_test") });

        using (MoveHold.Take(path))
        {
            Refused(path);
            var file = RemoteFile.Load(path);
            Assert.True(file.Found);
            Assert.Equal("dk_test", file.Remotes["web"].Key);
        }
    }

    /// <summary>READSHARE1: <c>RegistryFollowing.TextIfThere</c>, through <c>Load</c> and <c>Due</c>, reads the lines moved its move into place still holds.</summary>
    [Fact]
    public void The_lines_moved_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        RegistryFollowing.Moved(_home, "web-app", At);
        var path = DaorisHome.Entry(_home, HomeEntry.LinesMoved);

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.Equal(["web-app"], RegistryFollowing.Due(_home));
        }
    }

    /// <summary>READSHARE1: <c>OpinionGateFiles.ReadDues</c> reads the opinions owed while their move into place still holds them.</summary>
    [Fact]
    public void The_opinions_owed_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = DaorisHome.Entry(_home, HomeEntry.OpinionDues);
        OpinionGateFiles.WriteDues(path, [new OpinionDue("s1a2b3c4", "q1", "web-app", "default", "C:/trees/web-app", "landing", At)]);

        using (MoveHold.Take(path))
        {
            Refused(path);
            var due = Assert.Single(OpinionGateFiles.ReadDues(path));
            Assert.Equal(("s1a2b3c4", "q1", "landing"), (due.Session, due.Quest, due.Occasion));
        }
    }

    /// <summary>READSHARE1: <c>OpinionGateFiles.ReadGates</c> reads a chain's gate record while its move into place still holds it.</summary>
    [Fact]
    public void A_chain_s_gate_record_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = DaorisHome.Entry(_home, HomeEntry.OpinionGates, "c1.json");
        OpinionGateFiles.WriteGates(path, "c1", new Dictionary<string, OpinionGateKept>
        {
            ["web-app"] = new() { Asks = [new OpinionAskKept(At, "landing", "work", "abc1234")] },
        });

        using (MoveHold.Take(path))
        {
            Refused(path);
            var ask = Assert.Single(OpinionGateFiles.ReadGates(path)["web-app"].Asks);
            Assert.Equal(("landing", "abc1234"), (ask.Occasion, ask.Tip));
        }
    }

    /// <summary>READSHARE1: <c>OpinionDeliveryFiles.Read</c> reads an opinion's delivery while its move into place still holds it.</summary>
    [Fact]
    public void An_opinion_s_delivery_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = DaorisHome.Entry(_home, HomeEntry.OpinionPackets, "o1", "delivered.json");
        OpinionDeliveryFiles.Write(path, new OpinionDelivered("accepted", At, null) { Recheck = "r1" });

        using (MoveHold.Take(path))
        {
            Refused(path);
            var delivered = OpinionDeliveryFiles.Read(path);
            Assert.Equal(("accepted", "r1"), (delivered.Person, delivered.Recheck));
        }
    }

    /// <summary>READSHARE1: <c>WorkspaceSetupFile.ReadFile</c>, through <c>Load</c>, reads a workspace's plan while its move into place still holds it.</summary>
    [Fact]
    public void A_workspace_s_setup_plan_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        WorkspaceSetupFile.Save(_home, new WorkspaceSetupPlan("work", ["web-app", "engine"], 2, 1) { Created = At });
        var path = WorkspaceSetupFile.PathOf(_home, "work");

        using (MoveHold.Take(path))
        {
            Refused(path);
            var (plan, problem) = WorkspaceSetupFile.Load(_home, "work");
            Assert.Null(problem);
            Assert.Equal(["web-app", "engine"], plan!.Order);
        }
    }

    /// <summary>READSHARE1: <c>UpdateRequests.Read</c> reads the person's update request while its move into place still holds it.</summary>
    [Fact]
    public void An_update_request_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        InstallUpdate.Write(_home, new UpdateRequest(UpdateMode.NotNow, "b42", At));
        var path = InstallUpdate.PathOf(_home);

        using (MoveHold.Take(path))
        {
            Refused(path);
            var request = UpdateRequests.Read(_home);
            Assert.Equal((UpdateMode.NotNow, "b42"), (request?.Mode, request?.Build));
        }
    }
}
