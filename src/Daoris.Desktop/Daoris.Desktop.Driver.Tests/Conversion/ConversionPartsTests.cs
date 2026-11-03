using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1ae (the oneway design §1.1): the one conversion door in parts. A planted part beside the workflow part, in each outcome; a part that does
/// not answer at Apply written <c>failed</c> and retried in its receipt, then once moved; <i>Leave it</i>; a receipt written before parts read as
/// one workflow part with no build; <c>Stands</c> from a part that is not the workflows'; Undo of a two-part receipt and its refusals; a restart
/// mid-apply; and the terminal's <c>convert</c>.
/// </summary>
public sealed class ConversionPartsTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "parts-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    /// <summary>The registry the workflow part reads: none until <see cref="SeedWorkflows"/>, so a home without one holds nothing old for it.</summary>
    private RepoView[] Registry { get; set; } = [];

    /// <summary>One old workflow shape: a place on Current with its own merge rule, so the workflow part saves a default and removes the rule.</summary>
    private void SeedWorkflows()
    {
        Directory.CreateDirectory(_home);
        Registry = [new("plain", true, "/f/plain", "default")];
        (DriverConfig.Empty with { Landings = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase) { ["plain"] = new(LandingForm.Merge) } }).Save(ConfigPath);
    }

    private ConvertDoors Doors(params IConversionPart[] others) => new(
        _home, _ => Task.FromResult(new ConvertWorld(DriverConfig.Load(ConfigPath), _home, Registry, WorkflowPorts.Store(_home), [])),
        edit => edit(DriverConfig.Load(ConfigPath)).Save(ConfigPath), () => DriverConfig.Load(ConfigPath), () => new HashSet<string>())
    {
        Others = others, Build = "0.42.0", Answered = Answered,
    };

    private AnsweredPlans Answered { get; } = new();

    /// <summary>
    /// A part of an area this test plants: it converts its file in the home from <c>old</c> to <c>new</c>, said once, with a clear of finished
    /// history where asked. Each behaviour a part can meet is a switch.
    /// </summary>
    private sealed class Planted(string name, string home, bool clears = false) : IConversionPart
    {
        public string Name => name;
        public bool Unread { get; set; }
        public bool FailEffect { get; set; }
        public bool LoseAnswer { get; set; }
        public bool Crash { get; set; }
        public string? Gone { get; set; }
        public int Clears { get; private set; }

        public string File => Path.Combine(home, $"planted-{name}.txt");
        public string Value
        {
            get => System.IO.File.Exists(File) ? System.IO.File.ReadAllText(File) : "";
            set
            {
                Directory.CreateDirectory(home);
                System.IO.File.WriteAllText(File, value);
            }
        }

        public string? ReadsGone => Gone;

        public Task<PartCensus> CensusAsync(CancellationToken ct) =>
            Unread ? throw new ConversionUnread("the service did not answer") : Task.FromResult(new PartCensus(name, Old ? 1 : 0, !Old));

        private bool Old => Value.StartsWith("old", StringComparison.Ordinal);

        public async Task<PartPlan> PlanAsync(ConversionChoices choices, CancellationToken ct)
        {
            var census = await CensusAsync(ct);
            var digest = System.Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{name}\n{Value}")));
            return new(name, digest, census.Ended, census, census.Ended ? [] : [$"  write `new` into {name}"],
                clears && !census.Ended ? [new PartClear($"{name}'s finished history", 3, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-02-01T00:00:00Z"), 2048)] : []);
        }

        public PartReceipt Begin(PartPlan plan) => new(name, plan.Digest, ConversionOutcomes.Pending,
            [new($"{name}:write", "write", $"`{name}` written new", ConversionOutcomes.Pending), .. clears ? new[] { new ConversionOp($"{name}:clear", "clear", $"`{name}`'s history cleared", ConversionOutcomes.Pending) { Clear = true } } : []]);

        public PartReceipt Settle(PartReceipt entry) =>
            entry.With($"{name}:write", op => op.Outcome == ConversionOutcomes.Uncertain ? op with { Outcome = Value == "new" ? ConversionOutcomes.Done : ConversionOutcomes.Pending } : op);

        public (PartReceipt Entry, string? Refused) Convert(PartReceipt entry, Action<PartReceipt> keep)
        {
            if (Crash) throw new IOException("the process stopped");
            if (FailEffect) return (entry.With($"{name}:write", op => op with { Outcome = ConversionOutcomes.Failed, Note = "the file is held" }), null);
            Value = "new";
            return (entry.With($"{name}:write", op => op with { Outcome = LoseAnswer ? ConversionOutcomes.Uncertain : ConversionOutcomes.Done }), null);
        }

        public PartReceipt Clear(PartReceipt entry, Action<PartReceipt> keep)
        {
            Clears++;
            return entry.With($"{name}:clear", op => op with { Outcome = ConversionOutcomes.Done });
        }

        public PartUndo UndoPlan(PartReceipt entry) =>
            Value == "new" ? new(null, [$"`{name}` goes back to old"]) { Said = $"`{name}` is old again" } : new($"moved: `{name}` reads `{Value}` now, not `new`.", []);

        public void Undo(PartReceipt entry)
        {
            if (Value != "new") throw new DriverException($"moved: `{name}` moved");
            Value = "old";
        }
    }

    private Planted Plant(string name = "settings", bool clears = false)
    {
        var part = new Planted(name, _home, clears) { Value = "old" };
        return part;
    }

    [Fact]
    public async Task ONEWAY1ae_the_workflow_part_alone_keeps_its_own_digest_and_a_planted_part_joins_the_plan()
    {
        SeedWorkflows();
        var alone = await ConversionApplies.PlanAsync(Doors(), new ConversionChoices());
        var settings = Plant();

        var both = await ConversionApplies.PlanAsync(Doors(settings), new ConversionChoices());

        Assert.Equal(alone.Of(ConversionParts.Workflows)!.Digest, alone.Digest);
        Assert.Equal(["settings", "workflows"], both.Parts.Select(each => each.Part));
        Assert.NotEqual(alone.Digest, both.Digest);
        Assert.Equal(alone.Of(ConversionParts.Workflows)!.Digest, both.Of(ConversionParts.Workflows)!.Digest);
        Assert.Equal(both.Parts.Sum(each => each.Census.Count), both.Count);
        Assert.False(both.Ended);
        Assert.Contains(ConvertCommand.Lines(both), line => line.Contains("settings 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ONEWAY1ae_a_planted_part_is_done_beside_the_workflows_in_one_receipt_stamped_with_its_build()
    {
        SeedWorkflows();
        var settings = Plant();
        var plan = await ConversionApplies.PlanAsync(Doors(settings), new ConversionChoices());

        var result = await ConversionApplies.ApplyAsync(Doors(settings), new ConversionChoices(), plan.Digest);

        Assert.True(result.Done, result.Message);
        Assert.Equal("new", settings.Value);
        Assert.Equal("merge-after-accept", DriverConfig.Load(ConfigPath).DefaultWorkflow);
        var receipt = new HomeReceipts(_home).Read(plan.Digest)!;
        Assert.Equal("0.42.0", receipt.Build);
        Assert.Equal([("settings", ConversionOutcomes.Done), ("workflows", ConversionOutcomes.Done)], receipt.Parts.Select(each => (each.Part, each.Outcome)));
        Assert.True(ConversionReceipts.Stands(_home));
        Assert.Equal(plan.Of("settings")!.Digest, receipt.Of("settings")!.Digest);
    }

    [Fact]
    public async Task ONEWAY1ae_a_part_whose_effect_fails_is_failed_holds_every_clear_and_runs_again_when_pressed_again()
    {
        Directory.CreateDirectory(_home);
        var settings = Plant();
        settings.FailEffect = true;
        var notes = Plant("notes", clears: true);
        var plan = await ConversionApplies.PlanAsync(Doors(settings, notes), new ConversionChoices());

        var failed = await ConversionApplies.ApplyAsync(Doors(settings, notes), new ConversionChoices(), plan.Digest);

        Assert.False(failed.Done);
        Assert.Equal(ConversionOutcomes.Failed, failed.Parts.Single(each => each.Part == "settings").Outcome);
        Assert.Equal("new", notes.Value);
        Assert.Equal(0, notes.Clears);
        Assert.Contains("the file is held", failed.Message, StringComparison.Ordinal);

        settings.FailEffect = false;
        var again = await ConversionApplies.ApplyAsync(Doors(settings, notes), new ConversionChoices(), plan.Digest);

        Assert.True(again.Done, again.Message);
        Assert.Equal("new", settings.Value);
        Assert.Equal(1, notes.Clears);
        Assert.True(new HomeReceipts(_home).Read(plan.Digest)!.Settled);
    }

    [Fact]
    public async Task ONEWAY1ae_a_part_that_moved_since_the_plan_refuses_the_whole_press_with_nothing_written()
    {
        SeedWorkflows();
        var settings = Plant();
        var plan = await ConversionApplies.PlanAsync(Doors(settings), new ConversionChoices());
        settings.Value = "old, edited";

        var result = await ConversionApplies.ApplyAsync(Doors(settings), new ConversionChoices(), plan.Digest);

        Assert.Equal(ConversionRefusals.Moved, result.Refused);
        Assert.NotNull(result.Plan);
        Assert.Empty(new HomeReceipts(_home).All());
        Assert.Null(DriverConfig.Load(ConfigPath).DefaultWorkflow);
    }

    [Fact]
    public async Task ONEWAY1ae_a_part_that_does_not_answer_at_apply_is_written_failed_with_its_pressed_digest_and_retried_in_its_receipt()
    {
        SeedWorkflows();
        var quests = Plant("quests");
        var plan = await ConversionApplies.PlanAsync(Doors(quests), new ConversionChoices());
        quests.Unread = true;

        var first = await ConversionApplies.ApplyAsync(Doors(quests), new ConversionChoices(), plan.Digest);

        Assert.False(first.Done);
        var entry = new HomeReceipts(_home).Read(plan.Digest)!.Of("quests")!;
        Assert.Equal((ConversionOutcomes.Failed, plan.Of("quests")!.Digest, 0), (entry.Outcome, entry.Digest, entry.Operations.Count));
        Assert.Contains("not read: the service did not answer", entry.Said);
        Assert.Equal("merge-after-accept", DriverConfig.Load(ConfigPath).DefaultWorkflow);
        Assert.True(ConversionReceipts.Stands(_home));
        Assert.Contains(ConvertCommand.Lines(first), line => line.Contains("--leave quests", StringComparison.Ordinal));

        quests.Unread = false;
        var again = await ConversionApplies.ApplyAsync(Doors(quests), new ConversionChoices(), plan.Digest);

        Assert.True(again.Done, again.Message);
        Assert.Equal("new", quests.Value);
        Assert.Single(new HomeReceipts(_home).All());
    }

    [Fact]
    public async Task ONEWAY1ae_an_unreachable_part_that_moved_before_its_retry_is_closed_moved_and_offered_again()
    {
        SeedWorkflows();
        var quests = Plant("quests");
        var plan = await ConversionApplies.PlanAsync(Doors(quests), new ConversionChoices());
        quests.Unread = true;
        await ConversionApplies.ApplyAsync(Doors(quests), new ConversionChoices(), plan.Digest);
        quests.Unread = false;
        quests.Value = "old, written again";

        var retried = await ConversionApplies.ApplyAsync(Doors(quests), new ConversionChoices(), plan.Digest);

        Assert.Equal(ConversionOutcomes.Moved, retried.Parts.Single(each => each.Part == "quests").Outcome);
        Assert.Equal("old, written again", quests.Value);
        Assert.True(new HomeReceipts(_home).Read(plan.Digest)!.Settled);
        var next = await ConversionApplies.PlanAsync(Doors(quests), new ConversionChoices());
        Assert.False(next.Of("quests")!.Empty);
        Assert.NotEqual(plan.Digest, next.Digest);
    }

    [Fact]
    public async Task ONEWAY1ae_a_press_of_a_plan_this_Daoris_did_not_answer_with_a_part_unread_is_refused_moved()
    {
        SeedWorkflows();
        var quests = Plant("quests");
        var plan = await ConversionApplies.PlanAsync(Doors(quests) with { Answered = null }, new ConversionChoices());
        quests.Unread = true;

        var result = await ConversionApplies.ApplyAsync(Doors(quests) with { Answered = new AnsweredPlans() }, new ConversionChoices(), plan.Digest);

        Assert.Equal(ConversionRefusals.Moved, result.Refused);
        Assert.Contains("cannot be compared", result.Message, StringComparison.Ordinal);
        Assert.Empty(new HomeReceipts(_home).All());
    }

    [Fact]
    public async Task ONEWAY1ae_leave_it_closes_a_failed_part_as_left_the_receipt_settles_and_the_next_plan_offers_it_again()
    {
        SeedWorkflows();
        var quests = Plant("quests", clears: true);
        var notes = Plant("notes", clears: true);
        var plan = await ConversionApplies.PlanAsync(Doors(quests, notes), new ConversionChoices());
        quests.Unread = true;
        await ConversionApplies.ApplyAsync(Doors(quests, notes), new ConversionChoices(), plan.Digest);
        Assert.Equal(0, notes.Clears);

        var left = ConversionApplies.Leave(Doors(quests, notes), plan.Digest, "quests");

        var receipt = new HomeReceipts(_home).Read(plan.Digest)!;
        Assert.Equal(ConversionOutcomes.Left, receipt.Of("quests")!.Outcome);
        Assert.True(receipt.Settled);
        Assert.Equal(1, notes.Clears);
        Assert.False(left.Done);
        Assert.Equal(ConversionRefusals.Part, ConversionApplies.Leave(Doors(quests, notes), plan.Digest, "notes").Refused);
        quests.Unread = false;
        Assert.False((await ConversionApplies.PlanAsync(Doors(quests, notes), new ConversionChoices())).Of("quests")!.Empty);
    }

    [Fact]
    public async Task ONEWAY1ae_a_lost_answer_leaves_a_part_uncertain_which_stands_and_a_restart_settles_it_from_its_postcondition()
    {
        Directory.CreateDirectory(_home);
        var accounts = Plant("accounts");
        accounts.LoseAnswer = true;
        var plan = await ConversionApplies.PlanAsync(Doors(accounts), new ConversionChoices());

        var lost = await ConversionApplies.ApplyAsync(Doors(accounts), new ConversionChoices(), plan.Digest);

        Assert.Equal(ConversionOutcomes.Uncertain, lost.Parts.Single().Outcome);
        Assert.True(ConversionReceipts.Stands(_home));
        accounts.LoseAnswer = false;

        var settled = ConversionApplies.SettleAll(Doors(accounts));

        Assert.Equal(ConversionOutcomes.Done, settled.Single().Of("accounts")!.Outcome);
        Assert.Equal(ConversionOutcomes.Done, new HomeReceipts(_home).Read(plan.Digest)!.Of("accounts")!.Outcome);
    }

    [Fact]
    public async Task ONEWAY1ae_a_restart_mid_apply_goes_on_from_the_receipt_written_before_the_first_effect()
    {
        SeedWorkflows();
        var settings = Plant();
        settings.Crash = true;
        var plan = await ConversionApplies.PlanAsync(Doors(settings), new ConversionChoices());

        await Assert.ThrowsAsync<IOException>(() => ConversionApplies.ApplyAsync(Doors(settings), new ConversionChoices(), plan.Digest));

        var stopped = new HomeReceipts(_home).Read(plan.Digest)!;
        Assert.Equal(ConversionOutcomes.Pending, stopped.Of("settings")!.Outcome);
        Assert.Null(DriverConfig.Load(ConfigPath).DefaultWorkflow);
        settings.Crash = false;

        var resumed = await ConversionApplies.ApplyAsync(Doors(settings), new ConversionChoices(), plan.Digest);

        Assert.True(resumed.Done, resumed.Message);
        Assert.Equal(("new", "merge-after-accept"), (settings.Value, DriverConfig.Load(ConfigPath).DefaultWorkflow));
    }

    [Fact]
    public void ONEWAY1ae_a_receipt_written_before_parts_reads_as_one_workflow_part_with_no_build()
    {
        Directory.CreateDirectory(_home);
        var digest = new string('a', 64);
        var old = new ConversionReceipt(digest, DateTimeOffset.Parse("2026-11-02T10:00:00Z"), new ConversionChoices(), [], [], "merge-after-accept", [], [], [],
            [new("configuration", "configuration", "the choices, in one change", ConversionOutcomes.Done)]) { Before = new(null, [], [], [], []) };
        var path = new HomeReceipts(_home).PathOf(digest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(old, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var read = new HomeReceipts(_home).Read(digest)!;

        Assert.Null(read.Build);
        Assert.Equal([(ConversionParts.Workflows, ConversionOutcomes.Done)], read.Parts.Select(each => (each.Part, each.Outcome)));
        Assert.Equal("merge-after-accept", new ConversionReceipts(_home).Read(digest)!.DefaultWorkflow);
        Assert.True(ConversionReceipts.Stands(_home));
        Assert.True(HomeReceipts.AnyBeforeParts(_home));
        var entry = TransitionalRegister.Entries.Single(each => each.Id == "conversion-receipt-before-parts");
        Assert.False(entry.Ended(_home));
        new HomeReceipts(_home).Write(read);
        Assert.True(entry.Ended(_home));
    }

    [Fact]
    public async Task ONEWAY1ae_a_part_that_is_not_the_workflows_alone_stands_and_holds_the_rollback()
    {
        Directory.CreateDirectory(_home);
        var git = Plant("git");
        var plan = await ConversionApplies.PlanAsync(Doors(git), new ConversionChoices());

        await ConversionApplies.ApplyAsync(Doors(git), new ConversionChoices(), plan.Digest);

        Assert.Null(new HomeReceipts(_home).Read(plan.Digest)!.Of(ConversionParts.Workflows));
        Assert.True(ConversionReceipts.Stands(_home));
        Assert.Equal(InstallRollback.Workflows, InstallRollback.Refused(_home, "0.41.0", "0.42.0", ConversionReceipts.Stands)!.Value.Code);
    }

    [Fact]
    public async Task ONEWAY1ae_undo_of_a_two_part_receipt_puts_both_back_and_the_rollback_lifts()
    {
        SeedWorkflows();
        var settings = Plant();
        var plan = await ConversionApplies.PlanAsync(Doors(settings), new ConversionChoices());
        await ConversionApplies.ApplyAsync(Doors(settings), new ConversionChoices(), plan.Digest);
        var undo = ConversionApplies.UndoPlan(Doors(settings));
        Assert.Null(undo.Refusal);
        Assert.Contains("`settings` goes back to old", undo.Lines);

        var result = ConversionApplies.Undo(Doors(settings), undo.Digest!);

        Assert.True(result.Done, result.Message);
        Assert.Equal("old", settings.Value);
        var config = DriverConfig.Load(ConfigPath);
        Assert.Null(config.DefaultWorkflow);
        Assert.True(config.Landings.ContainsKey("plain"));
        Assert.False(ConversionReceipts.Stands(_home));
        Assert.Contains("`settings` is old again", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ONEWAY1ae_undo_is_refused_when_a_parts_write_moved_and_once_the_floor_is_needed()
    {
        SeedWorkflows();
        var settings = Plant();
        var plan = await ConversionApplies.PlanAsync(Doors(settings), new ConversionChoices());
        await ConversionApplies.ApplyAsync(Doors(settings), new ConversionChoices(), plan.Digest);
        settings.Value = "edited since";

        var moved = ConversionApplies.UndoPlan(Doors(settings));

        Assert.StartsWith("moved:", moved.Refusal);
        Assert.Contains("settings", moved.Refusal);
        settings.Value = "new";
        var floored = ConversionApplies.UndoPlan(Doors(settings) with { FloorNeeded = "0.50.0" });
        Assert.Contains("0.50.0", floored.Refusal);
        Assert.Null(floored.Digest);
        Assert.Null(ConversionApplies.UndoPlan(Doors(settings)).Refusal);
    }

    [Fact]
    public async Task ONEWAY1ae_undo_waits_for_the_latest_receipt_to_settle_and_never_reaches_back_past_it()
    {
        Directory.CreateDirectory(_home);
        var settings = Plant();
        var first = await ConversionApplies.PlanAsync(Doors(settings), new ConversionChoices());
        await ConversionApplies.ApplyAsync(Doors(settings), new ConversionChoices(), first.Digest);
        var git = Plant("git");
        git.FailEffect = true;
        var second = await ConversionApplies.PlanAsync(Doors(settings, git), new ConversionChoices());
        await ConversionApplies.ApplyAsync(Doors(settings, git), new ConversionChoices(), second.Digest);

        var undo = ConversionApplies.UndoPlan(Doors(settings, git));

        Assert.Contains("not settled", undo.Refusal);
        Assert.Equal("new", settings.Value);
    }

    [Fact]
    public void ONEWAY1ae_the_terminal_reads_convert_and_its_leave()
    {
        var digest = new string('b', 64);

        var leave = ConvertCommand.Read(["--apply", digest, "--leave", "quests"], out var problem)!;

        Assert.Null(problem);
        Assert.Equal(("apply", digest, "quests"), (leave.Act, leave.Digest, leave.Leave));
        Assert.Null(ConvertCommand.Read(["--plan", "--leave", "quests"], out var refused));
        Assert.Contains("--leave", refused);
        Assert.StartsWith("usage: daoris convert", ConvertCommand.Usage, StringComparison.Ordinal);
    }
}
