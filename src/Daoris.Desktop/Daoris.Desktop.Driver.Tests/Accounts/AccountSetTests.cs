using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent's accounts as one set (POOL1a, D197; <c>docs/2026-10-21-account-pool-design.md</c> §2, §6): the fold's table of shapes,
/// <see cref="HarnessSettings.Change"/> folding before every edit, the receipts said by id, the come-up a held lock refuses, and an
/// older build's reading of a folded file.
/// </summary>
public sealed class AccountSetTests : IDisposable
{
    private const string Agent = "claude";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-account-set-tests", Guid.NewGuid().ToString("N")[..8]);

    private string File_ => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private HarnessSettings Raw(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, json);
        return HarnessSettings.Load(File_);
    }

    private static Dictionary<string, IReadOnlyList<string>> Accounts(params string[] names) =>
        new(StringComparer.OrdinalIgnoreCase) { [Agent] = [.. names.Order(StringComparer.Ordinal)] };

    private static (HarnessSettings Settings, PoolReceipt? Receipt) Folded(HarnessSettings raw, Dictionary<string, IReadOnlyList<string>> accounts)
    {
        var ids = 0;
        var (folded, _) = AccountSet.Fold(raw, accounts, DateTimeOffset.UnixEpoch, () => $"r{++ids}");
        return (folded, folded.Pooled.GetValueOrDefault(Agent)?.LastOrDefault());
    }

    private void Signed(params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, Agent, name));
    }

    /// <summary>Rule 2: the machine's scope, then each workspace's by name, each as its first then its list, then every other account by id.</summary>
    [Fact]
    public void The_order_is_the_union_of_every_scope_and_every_account_and_the_scopes_go()
    {
        var raw = Raw("""
            { "rotation": { "claude": ["b", "a"] },
              "workspaceRotation": { "work": { "claude": ["c", "a"] } },
              "workspaces": { "personal": { "claude": "d" } },
              "workspaceRotationUse": { "work": { "claude": { "switch": "off" } } } }
            """);

        var (folded, receipt) = Folded(raw, Accounts("a", "b", "c", "d", "e"));

        Assert.Equal(["b", "a", "d", "c", "e"], folded.Rotation[Agent]);
        Assert.Empty(folded.Defaults);
        Assert.Empty(folded.Workspaces);
        Assert.Empty(folded.WorkspaceRotation);
        Assert.Empty(folded.WorkspaceUses);
        Assert.NotNull(receipt);
        Assert.Equal([null, "personal", "work"], receipt!.Scopes.Select(scope => scope.Workspace));
        Assert.False(receipt.Said);
        // Every scope's first that is no longer the order's is said with its place.
        Assert.Equal([new MovedFirst("personal", "d", 3), new MovedFirst("work", "c", 4)], receipt.Moved);
        // The one value a workspace a start read set, with nothing beside it, is the set's.
        Assert.Equal("off", RotationUse.Read(folded.Uses[Agent]).Use.Switch);
        Assert.Empty(receipt.NotKept);
        // The account no stored order held is placed and said.
        Assert.Equal([new PlacedAccount("c", 4), new PlacedAccount("d", 3), new PlacedAccount("e", 5)], receipt.Joined);
    }

    /// <summary>Rule 2 (Fable's F9): a scope's first is its default where its own list holds it; a default outside its list is placed as any other account.</summary>
    [Fact]
    public void A_scope_s_first_is_where_its_starts_began_and_a_default_outside_its_list_is_any_other_account()
    {
        var raw = Raw("""{ "defaults": { "claude": "b" }, "rotation": { "claude": ["a", "b"] }, "workspaces": { "w": { "claude": "x" } }, "workspaceRotation": { "w": { "claude": ["c"] } } }""");

        var (folded, receipt) = Folded(raw, Accounts("a", "b", "c", "x"));

        Assert.Equal(["b", "a", "c", "x"], folded.Rotation[Agent]);
        Assert.Equal("b", receipt!.Scopes[0].Begins);
        Assert.Equal("c", receipt.Scopes[1].Begins);
        Assert.Equal([new MovedFirst("w", "c", 3)], receipt.Moved);
    }

    /// <summary>Rule 3 (Fable's F4): a machine scope of one account and no list takes <c>use: order</c>, its own <c>use</c> said as not kept.</summary>
    [Fact]
    public void A_machine_scope_of_one_account_is_used_one_by_one_in_order_and_says_so()
    {
        var raw = Raw("""{ "defaults": { "claude": "b" }, "rotationUse": { "claude": { "use": "goal", "switch": "off" } } }""");

        var (folded, receipt) = Folded(raw, Accounts("a", "b"));

        Assert.Equal(["b", "a"], folded.Rotation[Agent]);
        var use = RotationUse.Read(folded.Uses[Agent]).Use;
        Assert.Equal("order", use.Use);
        Assert.Equal("off", use.Switch);
        Assert.Equal("b", receipt!.InOrder);
        var said = Assert.Single(receipt.NotKept);
        Assert.Null(said.Workspace);
        Assert.Equal(("use", "\"goal\""), (said.Setting, said.Value.GetRawText()));
    }

    /// <summary>Rule 3 (Fable's F7): the machine's entry takes part dormant or read; its fields are the set's and never said.</summary>
    [Fact]
    public void The_machine_s_dormant_entry_is_the_set_s_and_unsaid()
    {
        var raw = Raw("""{ "rotationUse": { "claude": { "switch": "off", "later": 3 } }, "workspaceRotation": { "w": { "claude": ["a"] } } }""");

        var (folded, receipt) = Folded(raw, Accounts("a", "b"));

        Assert.Equal("off", RotationUse.Read(folded.Uses[Agent]).Use.Switch);
        // A field this build does not know, the machine's, is kept as today.
        Assert.Equal(3, folded.Uses[Agent]["later"]!.Value.GetInt32());
        Assert.Empty(receipt!.NotKept);
    }

    /// <summary>Rule 3: workspace entries compared as normalized; agreeing ones are taken, differing ones go to the default and are said.</summary>
    [Fact]
    public void Workspace_settings_are_compared_normalized_and_taken_only_where_they_agree()
    {
        var agree = Raw("""
            { "workspaceRotation": { "v": { "claude": ["a"] }, "w": { "claude": ["b"] } },
              "workspaceRotationUse": { "v": { "claude": { "early": false } }, "w": { "claude": { "switch": "off", "use": "order" } } } }
            """);

        var (taken, said) = Folded(agree, Accounts("a", "b"));
        Assert.Equal("off", RotationUse.Read(taken.Uses[Agent]).Use.Switch);
        Assert.Equal("order", RotationUse.Read(taken.Uses[Agent]).Use.Use);
        Assert.Empty(said!.NotKept);

        var differ = Raw("""
            { "workspaceRotation": { "v": { "claude": ["a"] }, "w": { "claude": ["b"] } },
              "workspaceRotationUse": { "v": { "claude": { "use": "goal" } }, "w": { "claude": { "use": "order" } } } }
            """);

        var (defaulted, receipt) = Folded(differ, Accounts("a", "b"));
        Assert.False(defaulted.Uses.ContainsKey(Agent));
        Assert.Equal([("v", "use"), ("w", "use")], receipt!.NotKept.Select(each => (each.Workspace, each.Setting)));
    }

    /// <summary>Rule 3: a dormant workspace entry is never taken, and every field it set is said.</summary>
    [Fact]
    public void A_dormant_workspace_entry_is_said_and_not_taken()
    {
        var raw = Raw("""{ "workspaces": { "w": { "claude": "a" } }, "workspaceRotationUse": { "w": { "claude": { "switch": "off" } } } }""");

        var (folded, receipt) = Folded(raw, Accounts("a", "b"));

        Assert.False(folded.Uses.ContainsKey(Agent));
        Assert.Equal(("w", "switch"), (receipt!.NotKept[0].Workspace, receipt.NotKept[0].Setting));
    }

    /// <summary>Rule 3: a <c>keep</c> naming no account of the set, or left alone in a set of one, is dropped and said.</summary>
    [Fact]
    public void A_keep_naming_no_account_or_alone_is_dropped_and_said()
    {
        var gone = Raw("""{ "rotation": { "claude": ["a", "b"] }, "rotationUse": { "claude": { "keep": "ghost" } }, "defaults": { "claude": "a" } }""");
        var (folded, receipt) = Folded(gone, Accounts("a", "b"));
        Assert.Null(RotationUse.Read(folded.Uses.GetValueOrDefault(Agent)).Use.Keep);
        Assert.Contains(receipt!.NotKept, each => each.Workspace is null && each.Setting == "keep");

        var alone = Raw("""{ "defaults": { "claude": "a" }, "rotationUse": { "claude": { "keep": "a" } } }""");
        var (one, said) = Folded(alone, Accounts("a"));
        Assert.Null(RotationUse.Read(one.Uses.GetValueOrDefault(Agent)).Use.Keep);
        Assert.Contains(said!.NotKept, each => each.Setting == "keep");
    }

    /// <summary>Rule 2: a name that is no account here is left out and said.</summary>
    [Fact]
    public void A_name_that_is_no_account_is_left_out_and_said()
    {
        var raw = Raw("""{ "rotation": { "claude": ["a", "ghost"] } }""");

        var (folded, receipt) = Folded(raw, Accounts("a", "b"));

        Assert.Equal(["a", "b"], folded.Rotation[Agent]);
        Assert.Equal(["ghost"], receipt!.Gone);
        Assert.Equal([new PlacedAccount("b", 2)], receipt.Joined);
    }

    /// <summary>Rule 1: a folded file folds to itself, and a first order where the file held none is not itself said.</summary>
    [Fact]
    public void A_folded_file_folds_to_itself_and_a_first_order_is_silent()
    {
        var raw = Raw("""{ "versions": { "claude": "2.0.0" } }""");

        var (once, receipt) = Folded(raw, Accounts("b", "a"));
        Assert.Equal(["a", "b"], once.Rotation[Agent]);
        Assert.Null(receipt);

        var (twice, counts) = AccountSet.Fold(once, Accounts("a", "b"), DateTimeOffset.UnixEpoch, () => "again");
        Assert.Empty(counts);
        Assert.Equal(once.Rotation[Agent], twice.Rotation[Agent]);
        Assert.Equal("2.0.0", twice.Versions[Agent]);
    }

    /// <summary>
    /// A raw edit through <see cref="HarnessSettings.Change"/> on an unfolded file leaves it folded, its receipt reading the scopes as
    /// they were; an older build's write is folded again with a second receipt kept beside the unsaid first.
    /// </summary>
    [Fact]
    public void Every_change_folds_first_and_a_second_fold_keeps_the_unsaid_first()
    {
        Signed("a", "b", "c");
        Raw("""{ "rotation": { "claude": ["a"] }, "workspaceRotation": { "w": { "claude": ["b"] } } }""");

        var changed = HarnessSettings.Change(File_, read => read.WithVersion(Agent, "2.0.0"));

        var written = HarnessSettings.Load(File_);
        Assert.Equal(["a", "b", "c"], written.Rotation[Agent]);
        Assert.Empty(written.WorkspaceRotation);
        Assert.Equal("2.0.0", written.Versions[Agent]);
        var first = Assert.Single(written.Pooled[Agent]);
        Assert.Equal(["a", "b"], first.Scopes.Select(scope => string.Join(",", scope.List)));
        Assert.Equal(changed.Pooled[Agent][0].Id, first.Id);

        // An older build writes a workspace's list again; the next change folds it with a receipt of its own.
        var older = JsonDocument.Parse(File.ReadAllText(File_)).RootElement.GetRawText()
            .Replace("\"rotation\"", "\"workspaceRotation\":{\"w\":{\"claude\":[\"c\"]}},\"rotation\"", StringComparison.Ordinal);
        File.WriteAllText(File_, older);
        HarnessSettings.Change(File_, read => read);

        var again = HarnessSettings.Load(File_);
        Assert.Equal(2, again.Pooled[Agent].Count);
        Assert.Equal(first.Id, again.Pooled[Agent][0].Id);
        Assert.False(again.Pooled[Agent][0].Said);
        Assert.Empty(again.WorkspaceRotation);

        // A change over a folded file appends nothing.
        HarnessSettings.Change(File_, read => read.WithVersion(Agent, null));
        Assert.Equal(2, HarnessSettings.Load(File_).Pooled[Agent].Count);
    }

    /// <summary><i>Got it</i> by id marks only the receipts it names; an id no receipt holds is passed over (Fable's F13).</summary>
    [Fact]
    public void Got_it_marks_only_the_receipts_it_names()
    {
        Signed("a", "b");
        Raw("""{ "rotation": { "claude": ["a"] } }""");
        var id = HarnessSettings.Change(File_, read => read).Pooled[Agent][0].Id;
        File.WriteAllText(File_, File.ReadAllText(File_).Replace("\"rotation\"", "\"workspaces\":{\"w\":{\"claude\":\"b\"}},\"rotation\"", StringComparison.Ordinal));
        HarnessSettings.Change(File_, read => read);

        HarnessSettings.Change(File_, read => read.WithSaid(Agent, [id, "nobody"]));

        var receipts = HarnessSettings.Load(File_).Pooled[Agent];
        Assert.Equal([true, false], receipts.Select(receipt => receipt.Said));
    }

    /// <summary>A new account goes last whatever its id (design §2.2): the sign-in's change places it, and says nothing of it.</summary>
    [Fact]
    public void A_new_account_goes_last_whatever_its_id()
    {
        Signed("m", "z");
        HarnessSettings.Change(File_, read => read);
        Assert.Equal(["m", "z"], HarnessSettings.Load(File_).Rotation[Agent]);

        Signed("a");
        HarnessSettings.Change(File_, read => read, account: new AccountChange(Agent, "a", Added: true));

        var written = HarnessSettings.Load(File_);
        Assert.Equal(["m", "z", "a"], written.Rotation[Agent]);
        Assert.False(written.Pooled.ContainsKey(Agent));
    }

    /// <summary>An order edited names its accounts first and keeps the rest in their order; cleared, it is the ids' order and keeps its settings.</summary>
    [Fact]
    public void An_order_edited_is_written_whole_and_cleared_keeps_its_settings()
    {
        Signed("a", "b", "c");
        Raw("""{ "rotation": { "claude": ["c", "b", "a"] }, "rotationUse": { "claude": { "switch": "off" } } }""");

        HarnessSettings.Change(File_, read => read.WithRotation(Agent, ["a"]));
        Assert.Equal(["a", "c", "b"], HarnessSettings.Load(File_).Rotation[Agent]);

        HarnessSettings.Change(File_, read => read.WithRotation(Agent, null));
        var cleared = HarnessSettings.Load(File_);
        Assert.Equal(["a", "b", "c"], cleared.Rotation[Agent]);
        Assert.Equal("off", RotationUse.Read(cleared.Uses[Agent]).Use.Switch);
    }

    /// <summary>An older build reads a folded file as the machine's list naming every account, for every workspace (design §6.4).</summary>
    [Fact]
    public void An_older_build_reads_a_folded_file_as_every_workspace_on_the_same_accounts()
    {
        var raw = Raw("""{ "rotation": { "claude": ["a"] }, "workspaceRotation": { "w": { "claude": ["b"] } }, "workspaceRotationUse": { "w": { "claude": { "use": "order" } } } }""");
        var (folded, _) = Folded(raw, Accounts("a", "b"));

        foreach (var workspace in new string?[] { null, "w", "other" })
        {
            var scope = OlderScope.Of(folded, Agent, workspace);
            Assert.Equal(ChoiceFrom.Machine, scope.From);
            Assert.Equal(["a", "b"], scope.List);
            Assert.Equal("order", scope.Use.Use);
        }
    }

    /// <summary>
    /// Every reader's projection (design §2.4): a raw file read through <see cref="HarnessSettings.LoadSet"/> answers the set, nothing
    /// written.
    /// </summary>
    [Fact]
    public void The_projection_answers_the_set_and_writes_nothing()
    {
        Signed("a", "b");
        var text = """{ "workspaces": { "w": { "claude": "b" } } }""";
        Raw(text);

        var set = HarnessSettings.LoadSet(File_);

        Assert.Equal(["b", "a"], set.Set(Agent).List);
        Assert.Equal(text, File.ReadAllText(File_));
    }

    /// <summary>A come-up a held lock refuses is logged and tried again at the next look (Fable's F11).</summary>
    [Fact]
    public void A_come_up_a_held_lock_refuses_is_logged_and_folded_at_the_next_look()
    {
        Signed("a", "b");
        Raw("""{ "defaults": { "claude": "b" } }""");
        var log = new MachineLog(_home, "driver-test");

        bool refused;
        using (KeepLock.Take(File_))
        {
            refused = Task.Run(() => HarnessSettings.FoldNow(File_, log, within: TimeSpan.FromMilliseconds(50))).Result;
        }

        Assert.False(refused);
        Assert.True(HarnessSettings.Load(File_).Defaults.ContainsKey(Agent));
        Assert.True(HarnessSettings.FoldNow(File_, log));
        Assert.False(HarnessSettings.Load(File_).Defaults.ContainsKey(Agent));
        Assert.Equal(["b", "a"], HarnessSettings.Load(File_).Rotation[Agent]);

        log.Dispose();
        var lines = Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs)).SelectMany(File.ReadAllLines).ToList();
        Assert.Contains(lines, line => line.Contains("\"accounts.pooled\"") && line.Contains("\"held\":true"));
        Assert.Contains(lines, line => line.Contains("\"accounts.pooled\"") && line.Contains("\"scopes\":1"));
    }
}
