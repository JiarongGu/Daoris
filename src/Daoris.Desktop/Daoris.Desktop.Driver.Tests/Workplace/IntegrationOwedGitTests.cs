using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's read of the declarations owed acceptance, against a real checkout. A line's
/// committed <c>daoris.gates.json</c> that declares an integration is owed while nothing is accepted (<c>unaccepted</c>) or another blob is
/// (<c>changed</c>), owes nothing once its blob is accepted, and a line that holds none, or one that declares no <c>integration</c>, owes
/// nothing. Git is asked once for a tip that moved, and not for one that did not.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationOwedGitTests : IAsyncLifetime
{
    private const string Declares = """{ "gates": [], "integration": { "by": "daoris", "scratch": ["bin/**"] } }""";
    private const string Declaring = """{ "gates": [], "integration": { "by": "daoris", "scratch": ["obj/**"] } }""";
    private const string Without = """{ "gates": [] }""";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-integration-owed-" + Guid.NewGuid().ToString("N")[..8]);

    private string Root => Path.Combine(_home, "engine");

    private readonly DateTimeOffset _now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Root);
        Assert.Equal(0, (await GitFixture.RunAsync(Root, "init", "-q", "-b", "main")).ExitCode);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return Task.CompletedTask;
    }

    private async Task<string> CommitAsync(string? declaration)
    {
        var file = Path.Combine(Root, "daoris.gates.json");
        if (declaration is null) File.Delete(file);
        else await File.WriteAllTextAsync(file, declaration);
        await File.WriteAllTextAsync(Path.Combine(Root, "moved.txt"), Guid.NewGuid().ToString("N"));
        await GitFixture.RunAsync(Root, "add", "-A");
        Assert.Equal(0, (await GitFixture.RunAsync(Root, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "next")).ExitCode);
        return (await GitFixture.GitAsync(Root, "rev-parse", "HEAD:daoris.gates.json")).Trim() is { Length: 40 } blob && declaration is not null ? blob : "none";
    }

    private RepoView[] Registry => [new RepoView("engine", true, Root, "aurora")];

    private static DriverConfig Choices(string? accepted = null) =>
        accepted is null ? DriverConfig.Empty.WithLine("engine", "main") : DriverConfig.Empty.WithLine("engine", "main").WithIntegrationGates("engine", accepted);

    /// <summary>A declaration committed on the line and accepted by no one is owed as <c>unaccepted</c>, with the repository, workspace, line and blob.</summary>
    [Fact]
    public async Task A_declared_integration_nobody_accepted_is_owed_as_unaccepted()
    {
        var blob = await CommitAsync(Declares);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);

        var each = Assert.Single(owed.Owed);
        Assert.Equal(("engine", "aurora", "main", blob, IntegrationAcceptance.Unaccepted, _now), (each.Repository, each.Workspace, each.Line, each.Blob, each.State, each.Since));
    }

    /// <summary>The tip's blob accepted, the repository owes nothing; and the next look, with no git, ends the row.</summary>
    [Fact]
    public async Task An_accepted_blob_owes_nothing_and_the_acceptance_ends_the_row_at_the_next_look_with_no_git()
    {
        var blob = await CommitAsync(Declares);
        var owed = new IntegrationOwed();
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Single(owed.Owed);

        await owed.LookAsync(Choices(blob), Registry, _now.AddMinutes(1), CancellationToken.None);

        Assert.Empty(owed.Owed);
        Assert.Equal(0, owed.GitReads);
    }

    /// <summary>A blob that is not the accepted one is <c>changed</c>, since when it is first read, and a tip that moved to the accepted blob owes nothing.</summary>
    [Fact]
    public async Task A_declaration_changed_since_the_acceptance_is_owed_as_changed_and_keeps_its_since_across_looks()
    {
        var accepted = await CommitAsync(Declares);
        var changed = await CommitAsync(Declaring);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(accepted), Registry, _now, CancellationToken.None);
        await owed.LookAsync(Choices(accepted), Registry, _now.AddMinutes(5), CancellationToken.None);

        var each = Assert.Single(owed.Owed);
        Assert.Equal((IntegrationAcceptance.Changed, changed, _now), (each.State, each.Blob, each.Since));
    }

    /// <summary>
    /// In full access the repository's line is the person's own: the declaration it commits is accepted as it lands, never owed, said
    /// in one sentence of the checks that changed; in restricted mode the same declaration waits.
    /// </summary>
    [Fact]
    public async Task A_declaration_on_a_full_access_line_is_accepted_as_it_lands_and_said_as_checks_changed()
    {
        const string Checks = """{ "gates": [ { "name": "build", "run": "dotnet build" } ], "integration": { "by": "daoris" } }""";
        const string More = """{ "gates": [ { "name": "build", "run": "dotnet build" }, { "name": "comments", "run": "node gate.mjs" } ], "integration": { "by": "daoris" } }""";
        var first = await CommitAsync(Checks);
        var second = await CommitAsync(More);
        var full = Choices(first).WithAccess(new AccessEdit { Repository = "engine", Full = true });
        var configPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        full.Save(configPath);
        var owed = new IntegrationOwed();

        await owed.LookAsync(full, Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Under(full));
        var byMode = Assert.Single(owed.Passed(full));
        Assert.Equal(second, byMode.Blob);

        var said = await DeclarationAcceptance.AcceptPassedAsync(_home, configPath, byMode, CancellationToken.None);

        Assert.Equal(
            "engine's checks changed on main: added `comments`; they run from now on (passed by the risk policy: where its harness's reach is full the seed classes it `none` (rule seed at seed)).",
            said);
        Assert.Equal(second, IntegrationAcceptance.Of(DriverConfig.Load(configPath), "engine"));
        var restricted = Choices(first);
        Assert.Single(owed.Under(restricted));
        Assert.Empty(owed.Passed(restricted));
    }

    [Fact]
    public async Task A_landed_command_change_under_unrestricted_permissions_is_accepted_without_a_row_and_under_asking_waits()
    {
        const string Command = """{ "gates": [ { "name": "build", "run": "dotnet build -c Release" } ], "integration": { "by": "daoris" } }""";
        var first = await CommitAsync(Build);
        var second = await CommitAsync(Command);
        var unrestricted = Choices(first).WithAccess(new AccessEdit { Repository = "engine", Set = [new(AccessRules.Permissions, AccessRules.Unrestricted)] });
        var asking = Choices(first).WithAccess(new AccessEdit { Repository = "engine", Set = [new(AccessRules.Permissions, AccessRules.Asking)] });
        var configPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        var owed = new IntegrationOwed();
        await owed.LookAsync(unrestricted, Registry, _now, CancellationToken.None);

        Assert.Equal(IntegrationAcceptance.Changed, Assert.Single(owed.Under(asking)).State);
        Assert.Empty(owed.Passed(asking));
        Assert.Empty(owed.Under(unrestricted));
        var taken = Assert.Single(owed.Passed(unrestricted));

        asking.Save(configPath);
        Assert.Null(await DeclarationAcceptance.AcceptPassedAsync(_home, configPath, taken, CancellationToken.None));
        Assert.Equal(first, IntegrationAcceptance.Of(DriverConfig.Load(configPath), "engine"));

        unrestricted.Save(configPath);
        var said = await DeclarationAcceptance.AcceptPassedAsync(_home, configPath, taken, CancellationToken.None);
        Assert.Equal(
            "engine's checks changed on main: changed `build`; they run from now on (passed by the risk policy: where its harness's reach is full the seed classes it `none` (rule seed at seed)).",
            said);
        Assert.Equal(second, IntegrationAcceptance.Of(DriverConfig.Load(configPath), "engine"));
    }

    private static DriverConfig WithRisk(DriverConfig config, string risk)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(config.ToJson())!.AsObject();
        node["risk"] = System.Text.Json.Nodes.JsonNode.Parse(risk);
        return DriverConfig.Parse(node.ToJsonString());
    }

    /// <summary>A changed declaration waits only where the policy classes <c>command.change</c>, whatever the access mode.</summary>
    [Fact]
    public async Task A_changed_declaration_waits_only_where_the_policy_classes_command_change_whatever_the_access_mode()
    {
        const string Command = """{ "gates": [ { "name": "build", "run": "dotnet build -c Release" } ], "integration": { "by": "daoris" } }""";
        var first = await CommitAsync(Build);
        var second = await CommitAsync(Command);
        var configPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        var restricted = Choices(first);
        var passes = WithRisk(restricted, """{ "repository/engine": [ { "kind": "command.change", "class": "none" } ] }""");
        var full = Choices(first).WithAccess(new AccessEdit { Repository = "engine", Full = true });
        var asks = WithRisk(full, """{ "repository/engine": [ { "kind": "command.change", "class": "owner" } ] }""");
        var owed = new IntegrationOwed();
        await owed.LookAsync(restricted, Registry, _now, CancellationToken.None);

        Assert.Empty(owed.Under(passes));
        var taken = Assert.Single(owed.Passed(passes));
        var verdict = Assert.Single(taken.Verdicts);
        Assert.Equal(("gate:build", RiskVerdict.Pass, "repository/engine"), (verdict.Unit, verdict.Verdict.Outcome, verdict.Verdict.Source));

        var row = Assert.Single(owed.Under(asks));
        Assert.Empty(owed.Passed(asks));
        Assert.Equal(RiskVerdict.OwnerOutcome, Assert.Single(row.Verdicts).Verdict.Outcome);

        asks.Save(configPath);
        Assert.Null(await DeclarationAcceptance.AcceptPassedAsync(_home, configPath, taken, CancellationToken.None));
        Assert.Equal(first, IntegrationAcceptance.Of(DriverConfig.Load(configPath), "engine"));

        passes.Save(configPath);
        var said = await DeclarationAcceptance.AcceptPassedAsync(_home, configPath, taken, CancellationToken.None);
        Assert.Equal(
            "engine's checks changed on main: changed `build`; they run from now on (passed by the risk policy: `repository/engine` classes `command.change` `none` (rule repository/engine at "
            + RiskPolicy.Of(passes).Revision + ")).",
            said);
        Assert.Equal(second, IntegrationAcceptance.Of(DriverConfig.Load(configPath), "engine"));
    }

    /// <summary>A change to one gate of two is judged on that unit alone, with its digest.</summary>
    [Fact]
    public async Task A_change_to_one_gate_is_judged_on_that_unit_alone_with_its_digest()
    {
        const string Two = """{ "gates": [ { "name": "build", "run": "dotnet build" }, { "name": "comments", "run": "node gate.mjs" } ], "integration": { "by": "daoris" } }""";
        const string Moved = """{ "gates": [ { "name": "build", "run": "dotnet build -c Release" }, { "name": "comments", "run": "node gate.mjs" } ], "integration": { "by": "daoris" } }""";
        var first = await CommitAsync(Two);
        await CommitAsync(Moved);
        var restricted = Choices(first);
        var owed = new IntegrationOwed();

        await owed.LookAsync(restricted, Registry, _now, CancellationToken.None);

        var row = Assert.Single(owed.Under(restricted));
        var one = Assert.Single(row.Verdicts);
        Assert.Equal(["gate:build"], row.ChangedUnits);
        Assert.Equal(("gate:build", RiskVerdict.OwnerOutcome), (one.Unit, one.Verdict.Outcome));
        Assert.Equal(DeclarationUnits.Of(Moved)!["gate:build"], one.Digest);
    }

    /// <summary>The journal holds the verdict of a taken declaration and of a held one, each naming its unit and digest.</summary>
    [Fact]
    public async Task The_journal_names_the_verdict_and_the_unit_digest_of_a_taken_and_of_a_held_declaration()
    {
        const string Command = """{ "gates": [ { "name": "build", "run": "dotnet build -c Release" } ], "integration": { "by": "daoris" } }""";
        var first = await CommitAsync(Build);
        await CommitAsync(Command);
        var digest = DeclarationUnits.Of(Command)!["gate:build"][..12];
        var owed = new IntegrationOwed();
        await owed.LookAsync(Choices(first), Registry, _now, CancellationToken.None);
        var shadow = new RiskShadow(_home, () => _now);

        RiskDeclarationHolds.Shadow(shadow, owed, Choices(first));
        var held = Assert.Single(RiskJournal.Read(_home));
        Assert.Equal((RiskVerdict.OwnerOutcome, true), (held.Outcome, held.TodayHeld));
        Assert.Contains($"`build` {digest}", held.Subject, StringComparison.Ordinal);

        var elsewhere = Path.Combine(_home, "taken");
        var passing = WithRisk(Choices(first), """{ "repository/engine": [ { "kind": "command.change", "class": "none" } ] }""");
        RiskDeclarationHolds.Shadow(new RiskShadow(elsewhere, () => _now), owed, passing);
        var taken = Assert.Single(RiskJournal.Read(elsewhere));
        Assert.Equal((RiskVerdict.Pass, false, "repository/engine"), (taken.Outcome, taken.TodayHeld, taken.Source));
        Assert.Contains($"`build` {digest}", taken.Subject, StringComparison.Ordinal);
    }

    /// <summary>A path-only change on a full-access line is kept as it lands, in the keep's sentence, never taken or owed.</summary>
    [Fact]
    public async Task A_change_of_no_command_on_a_full_access_line_is_kept_and_not_taken()
    {
        var first = await CommitAsync(Build);
        await CommitAsync("""{ "gates": [ { "name": "build", "run": "dotnet build", "paths": ["src/"] } ], "integration": { "by": "daoris" } }""");
        var full = Choices(first).WithAccess(new AccessEdit { Repository = "engine", Full = true });
        var owed = new IntegrationOwed();

        await owed.LookAsync(full, Registry, _now, CancellationToken.None);

        Assert.Empty(owed.Under(full));
        Assert.Empty(owed.Passed(full));
        Assert.Single(owed.Kept(full));
    }

    [Fact]
    public async Task A_landed_change_of_no_command_keeps_the_acceptance_said_and_a_command_change_is_owed_with_its_difference()
    {
        const string Checks = """{ "gates": [ { "name": "build", "run": "dotnet build" } ], "integration": { "by": "daoris" } }""";
        const string Pattern = """{ "gates": [ { "name": "build", "run": "dotnet build", "paths": ["src/"] } ], "integration": { "by": "daoris" } }""";
        const string Command = """{ "gates": [ { "name": "build", "run": "dotnet build -c Release", "paths": ["src/"] } ], "integration": { "by": "daoris" } }""";
        var first = await CommitAsync(Checks);
        var second = await CommitAsync(Pattern);
        var configPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Choices(first).Save(configPath);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(first), Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Under(Choices(first)));
        var kept = Assert.Single(owed.Kept(Choices(first)));
        var said = await DeclarationAcceptance.AcceptKeptAsync(_home, configPath, kept, CancellationToken.None);

        Assert.Equal("engine's daoris.gates.json on main changed no command, only the paths of `build`, so it stays accepted as it landed.", said);
        Assert.Equal(second, IntegrationAcceptance.Of(DriverConfig.Load(configPath), "engine"));
        Assert.Null(await DeclarationAcceptance.AcceptKeptAsync(_home, configPath, kept, CancellationToken.None));

        var third = await CommitAsync(Command);
        await owed.LookAsync(Choices(second), Registry, _now.AddMinutes(1), CancellationToken.None);

        Assert.Empty(owed.Kept(Choices(second)));
        var row = Assert.Single(owed.Under(Choices(second)));
        Assert.Equal((IntegrationAcceptance.Changed, third), (row.State, row.Blob));
        Assert.Equal([new DeclaredCheck("build", "dotnet build -c Release", "dotnet build")], row.Change!.Changed);
    }

    private const string Build = """{ "gates": [ { "name": "build", "run": "dotnet build" } ], "integration": { "by": "daoris" } }""";

    /// <summary>
    /// A line's declaration with a member written twice, or a <c>run</c> that is not a string, never ends the look: it waits for the
    /// person as a change, and a full-access line accepts it as it lands, said, with nothing thrown.
    /// </summary>
    [Theory]
    [InlineData("""{ "gates": [ { "name": "build", "run": "dotnet build", "run": "dotnet build -c Release" } ], "integration": { "by": "daoris" } }""")]
    [InlineData("""{ "gates": [ { "name": "build", "run": 2 } ], "integration": { "by": "daoris" } }""")]
    public async Task A_declaration_with_a_member_written_twice_or_a_run_not_a_string_waits_for_the_person_and_the_look_goes_on(string landed)
    {
        var first = await CommitAsync(Build);
        var second = await CommitAsync(landed);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(first), Registry, _now, CancellationToken.None);

        Assert.Empty(owed.Kept(Choices(first)));
        var row = Assert.Single(owed.Under(Choices(first)));
        Assert.Equal((IntegrationAcceptance.Changed, second), (row.State, row.Blob));

        var full = Choices(first).WithAccess(new AccessEdit { Repository = "engine", Full = true });
        var configPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        full.Save(configPath);
        var said = await DeclarationAcceptance.AcceptPassedAsync(_home, configPath, Assert.Single(owed.Passed(full)), CancellationToken.None);

        Assert.StartsWith("engine's ", said);
        Assert.Equal(second, IntegrationAcceptance.Of(DriverConfig.Load(configPath), "engine"));
    }

    /// <summary>
    /// A keep judged against an acceptance another writer withdraws before the keep takes the file's lock writes nothing: the check
    /// is made on the file as the writer reads it under its lock, so a withdrawn acceptance never comes back.
    /// </summary>
    [Fact]
    public async Task A_keep_never_brings_back_an_acceptance_withdrawn_while_it_waited_for_the_lock()
    {
        var first = await CommitAsync(Build);
        await CommitAsync("""{ "gates": [ { "name": "build", "run": "dotnet build", "paths": ["src/"] } ], "integration": { "by": "daoris" } }""");
        var configPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Choices(first).Save(configPath);
        var owed = new IntegrationOwed();
        await owed.LookAsync(Choices(first), Registry, _now, CancellationToken.None);
        var kept = Assert.Single(owed.Kept(Choices(first)));

        Task<string?> keeping;
        using (KeepLock.Take(Path.GetFullPath(configPath)))
        {
            keeping = Task.Run(() => DeclarationAcceptance.AcceptKeptAsync(_home, configPath, kept, CancellationToken.None));
            await Task.Delay(TimeSpan.FromMilliseconds(400));
            AtomicFile.WriteText(configPath, DeclarationAcceptance.Withdraw(_home, DriverConfig.Load(configPath), "engine", "aurora").ToJson());
        }

        Assert.Null(await keeping.WaitAsync(Poll.Patience));
        Assert.Null(IntegrationAcceptance.Of(DriverConfig.Load(configPath), "engine"));
    }

    /// <summary>The acceptance ends the row at once, from the choices as they stand, with no look and no git.</summary>
    [Fact]
    public async Task An_acceptance_ends_the_row_without_another_look()
    {
        var blob = await CommitAsync(Declares);
        var owed = new IntegrationOwed();
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Single(owed.Under(Choices()));

        var reads = owed.GitReads;
        Assert.Empty(owed.Under(Choices(blob)));
        Assert.Equal(reads, owed.GitReads);
    }

    /// <summary>A line that holds no declaration, or one that declares no integration, owes nothing whether or not something is accepted.</summary>
    [Fact]
    public async Task A_line_with_no_declaration_or_no_integration_owes_nothing()
    {
        await CommitAsync(null);
        var owed = new IntegrationOwed();
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Owed);

        await CommitAsync(Without);
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Owed);

        await CommitAsync(Declares);
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Single(owed.Owed);

        // The declaration withdrawn: nothing accepted before changes that, only the line's tip.
        await CommitAsync(Without);
        await owed.LookAsync(Choices("0123456789abcdef0123456789abcdef01234567"), Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Owed);
    }

    /// <summary>Git is asked when the line's tip moved and not when it did not; a repository with no checkout here is not read.</summary>
    [Fact]
    public async Task Git_is_asked_only_where_the_tip_moved_and_never_for_a_repository_with_no_checkout()
    {
        await CommitAsync(Declares);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.True(owed.GitReads > 0);
        await owed.LookAsync(Choices(), Registry, _now.AddMinutes(1), CancellationToken.None);
        Assert.Equal(0, owed.GitReads);

        await CommitAsync(Declares);
        await owed.LookAsync(Choices(), Registry, _now.AddMinutes(2), CancellationToken.None);
        // The moved tip carries a blob seen before: the blob is asked for, its text is not.
        Assert.Equal(1, owed.GitReads);

        await owed.LookAsync(Choices(), [new RepoView("engine", true, null, "aurora"), new RepoView("web", true, Path.Combine(_home, "absent"), "aurora")],
            _now.AddMinutes(3), CancellationToken.None);
        Assert.Empty(owed.Owed);
        Assert.Equal(0, owed.GitReads);
    }

    /// <summary>Every git start the look's read asks, as arguments.</summary>
    private async Task<List<string>> StartedAsync(IntegrationOwed owed, DriverConfig config)
    {
        var started = new List<string>();
        GitFlow.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        try
        {
            await owed.LookAsync(config, Registry, _now.AddMinutes(1), CancellationToken.None);
        }
        finally
        {
            GitFlow.Started.Value = null;
        }

        return started;
    }

    /// <summary>A tree that stood since the last look starts no git at all, whatever the line, acceptance or unjudged pair.</summary>
    [Theory]
    [InlineData("line set, nothing accepted", "set", null)]
    [InlineData("line guessed, nothing accepted", "guess", null)]
    [InlineData("an accepted blob this checkout holds not", "set", "unheld")]
    [InlineData("a changed declaration judged", "set", "changed")]
    [InlineData("a line the checkout does not hold", "absent", null)]
    public async Task A_standing_tree_starts_no_git_at_the_next_look(string why, string line, string? accepted)
    {
        Assert.NotEmpty(why);
        await CommitAsync(Declares);
        var blob = accepted switch
        {
            null => null,
            "unheld" => new string('a', 40),
            _ => await CommitAsync(Declaring) is var other && other != "none" ? other : null,
        };
        if (accepted == "changed") await CommitAsync(Declares);
        var config = line == "set" ? Choices(blob)
            : line == "absent" ? DriverConfig.Empty.WithLine("engine", "gone")
            : blob is null ? DriverConfig.Empty : DriverConfig.Empty.WithIntegrationGates("engine", blob);
        var owed = new IntegrationOwed();
        await owed.LookAsync(config, Registry, _now, CancellationToken.None);

        var started = await StartedAsync(owed, config);

        Assert.Empty(started);
    }

    /// <summary>A line that appears, and a pair whose wait has passed, are asked again; a git that failed is no answer to hold.</summary>
    [Fact]
    public async Task A_line_that_appears_after_a_miss_is_read_at_the_next_look()
    {
        await CommitAsync(Declares);
        var config = DriverConfig.Empty.WithLine("engine", "gone");
        var owed = new IntegrationOwed();
        await owed.LookAsync(config, Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Owed);

        Assert.Equal(0, (await GitFixture.RunAsync(Root, "branch", "gone")).ExitCode);
        var started = await StartedAsync(owed, config);

        Assert.Contains(started, arguments => arguments.StartsWith("ls-tree", StringComparison.Ordinal));
        Assert.Single(owed.Owed);
    }

    [Fact]
    public async Task A_pair_git_gave_no_text_for_is_asked_again_once_its_wait_has_passed()
    {
        await CommitAsync(Declares);
        var config = Choices(new string('a', 40));
        var owed = new IntegrationOwed();
        await owed.LookAsync(config, Registry, _now, CancellationToken.None);

        var started = new List<string>();
        GitFlow.Started.Value = arguments =>
        {
            lock (started) started.Add(string.Join(' ', arguments));
        };
        try
        {
            await owed.LookAsync(config, Registry, _now.Add(IntegrationOwed.MissStands).AddMinutes(1), CancellationToken.None);
        }
        finally
        {
            GitFlow.Started.Value = null;
        }

        Assert.Contains(started, arguments => arguments.StartsWith("cat-file", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_tip_read_that_git_failed_to_answer_is_asked_again_at_the_next_look()
    {
        await CommitAsync(Declares);
        var owed = new IntegrationOwed();
        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        var moved = await CommitAsync(Declaring);

        using (GitFlow.Failing(ask => ask.Arguments.Count > 0 && ask.Arguments[0] == "ls-tree"))
        {
            await owed.LookAsync(Choices(), Registry, _now.AddMinutes(1), CancellationToken.None);
        }

        await owed.LookAsync(Choices(), Registry, _now.AddMinutes(2), CancellationToken.None);

        Assert.Equal(moved, Assert.Single(owed.Owed).Blob);
    }

    private const string TwoGates = """{ "gates": [ { "name": "build", "run": "dotnet build", "paths": ["src/"] }, { "name": "tools", "run": "npm test" } ], "integration": { "by": "daoris" } }""";

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    /// <summary>An acceptance by blob is converted at the first look, owing no press before or after, and not converted again.</summary>
    [Fact]
    public async Task An_acceptance_by_blob_is_converted_to_its_units_at_the_first_look_and_owes_no_press()
    {
        var blob = await CommitAsync(TwoGates);
        Choices(blob).Save(ConfigPath);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(blob), Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Under(Choices(blob)));
        var unconverted = Assert.Single(owed.Unconverted(Choices(blob)));
        Assert.Equal(("engine", blob), (unconverted.Repository, unconverted.Blob));

        Assert.True(await DeclarationAcceptance.ConvertAsync(ConfigPath, unconverted, CancellationToken.None));

        var converted = DriverConfig.Load(ConfigPath);
        Assert.Equal(blob, converted.IntegrationUnits["engine"].Blob);
        Assert.Equal(["gate:build", "gate:tools", "order", "part:integration"], converted.IntegrationUnits["engine"].Units.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(blob, IntegrationAcceptance.Of(converted, "engine"));
        Assert.Empty(owed.Unconverted(converted));
        Assert.Empty(owed.Under(converted));
        Assert.False(await DeclarationAcceptance.ConvertAsync(ConfigPath, unconverted, CancellationToken.None));
    }

    /// <summary>Nothing accepted, or an acceptance of none, converts nothing.</summary>
    [Fact]
    public async Task Nothing_accepted_or_an_acceptance_of_none_converts_nothing()
    {
        await CommitAsync(TwoGates);
        var owed = new IntegrationOwed();

        await owed.LookAsync(Choices(), Registry, _now, CancellationToken.None);
        Assert.Empty(owed.Unconverted(Choices()));
        await owed.LookAsync(Choices("none"), Registry, _now.AddMinutes(1), CancellationToken.None);
        Assert.Empty(owed.Unconverted(Choices("none")));
    }

    /// <summary>A change to one gate is owed for that unit alone, a path edit keeps the acceptance, and the home's units are what is judged against.</summary>
    [Fact]
    public async Task A_change_to_one_gate_is_owed_for_that_unit_alone_judged_against_the_units_the_home_holds()
    {
        var first = await CommitAsync(TwoGates);
        var config = Choices(first).WithIntegrationUnits("engine", new AcceptedUnits(first, DeclarationUnits.Of(TwoGates)!));
        var commandText = TwoGates.Replace("npm test", "npm run test", StringComparison.Ordinal);
        var second = await CommitAsync(commandText);
        var owed = new IntegrationOwed();

        await owed.LookAsync(config, Registry, _now, CancellationToken.None);

        var row = Assert.Single(owed.Under(config));
        Assert.Equal((IntegrationAcceptance.Changed, second), (row.State, row.Blob));
        Assert.Equal(["gate:tools"], row.ChangedUnits);
        Assert.Equal([new DeclaredCheck("tools", "npm run test", "npm test")], row.Change!.Changed);

        var accepting = config.WithIntegrationUnits("engine", new AcceptedUnits(first, DeclarationUnits.Of(commandText)!));
        var holding = new IntegrationOwed();
        await holding.LookAsync(accepting, Registry, _now, CancellationToken.None);
        Assert.Empty(holding.Under(accepting));
        Assert.Empty(Assert.Single(holding.Kept(accepting)).ChangedUnits);

        var third = await CommitAsync(TwoGates.Replace("[\"src/\"]", "[\"src/\", \"lib/\"]", StringComparison.Ordinal));
        await owed.LookAsync(config, Registry, _now.AddMinutes(1), CancellationToken.None);
        var pathEdit = Assert.Single(owed.Kept(config));
        Assert.Equal((third, 0), (pathEdit.Blob, pathEdit.ChangedUnits.Count));
        Assert.Empty(owed.Under(config));
    }

    /// <summary>A dropped path keeps the acceptance and is said in the keep's one line.</summary>
    [Fact]
    public async Task A_dropped_path_keeps_the_acceptance_and_is_said_in_one_line()
    {
        var first = await CommitAsync(TwoGates);
        var second = await CommitAsync(TwoGates.Replace("[\"src/\"]", "[\"lib/\"]", StringComparison.Ordinal));
        Choices(first).Save(ConfigPath);
        var owed = new IntegrationOwed();
        await owed.LookAsync(Choices(first), Registry, _now, CancellationToken.None);

        var kept = Assert.Single(owed.Kept(Choices(first)));
        var said = await DeclarationAcceptance.AcceptKeptAsync(_home, ConfigPath, kept, CancellationToken.None);

        Assert.Equal([new DeclaredNarrowing("the paths of `build`", ["src/"])], kept.Narrowed);
        Assert.Equal(
            "engine's daoris.gates.json on main changed no command, only the paths of `build` (narrowed: the paths of `build` lost `src/`), so it stays accepted as it landed.",
            said);
        Assert.Equal(second, IntegrationAcceptance.Of(DriverConfig.Load(ConfigPath), "engine"));
    }
}
