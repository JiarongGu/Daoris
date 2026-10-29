using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row b, part 2 (the agent-provider design §10; D213's row b part 2 note): the conformance suite, the contract's meter. A
/// theory over every provider and door the registry holds (<c>ProviderRegistry.Of(AdapterSet.Built())</c>), every provider the registration
/// list holds (<c>Providers.Built()</c>) and a plugin's provider made in a test home, run per provider, so a provider that answers the
/// contract wrongly fails here rather than in a caller. Each case a provider cannot pass yet is held in its own <c>pending.json</c>, named
/// with the row of §11 that makes it pass; a pending case that passes fails the suite until its line goes, and a line naming no case the
/// suite runs fails it too.
/// </summary>
/// <remarks>
/// The fast half: no process is started and nothing is set in the process's environment. The spawn cases that plant variables in the
/// environment are <see cref="ProviderConformanceEnvironmentTests"/>' (run alone), and the stand-ins' live cases
/// <see cref="ProviderConformanceProcessTests"/>' (the Process half).
/// </remarks>
public sealed class ProviderConformanceTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("in-process");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    public static TheoryData<string, string, string, string> InProcess => Conformance.Rows(ConformanceHalf.InProcess);

    /// <summary>Every mandatory case and each stated ability's, per provider and door: passing, or failing and held pending by name.</summary>
    [Theory]
    [MemberData(nameof(InProcess))]
    public async Task Each_provider_answers_the_contract_or_holds_the_case_pending(string source, string provider, string door, string @case)
    {
        if (await Conformance.RunAsync(source, provider, door, @case, _scratch) is { } held) output.WriteLine(held);
    }

    /// <summary>The suite's providers are the registry's, the registration list's and a test home's plugin's, every door of each among its rows.</summary>
    [Fact]
    public void The_suite_runs_over_every_provider_and_door_the_registry_holds()
    {
        var registry = ProviderRegistry.Of(AdapterSet.Built());
        var rows = Conformance.Runs().ToList();

        Assert.Equal(
            registry.Providers.Select(provider => provider.Id.Value).Order(StringComparer.Ordinal),
            rows.Where(row => row.Subject.Source == ConformanceSubject.RegistrySource).Select(row => row.Provider.Id.Value)
                .Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            registry.Doors.Select(door => door.Value).Order(StringComparer.Ordinal),
            rows.Where(row => row.Subject.Source == ConformanceSubject.RegistrySource && row.Door != Conformance.EveryDoor)
                .Select(row => row.Door).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            Providers.Built().Select(provider => provider.Id.Value).Order(StringComparer.Ordinal),
            rows.Where(row => row.Subject.Source == ConformanceSubject.ListSource).Select(row => row.Provider.Id.Value)
                .Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            [ConformanceSubjects.PluginDoor, ConformanceSubjects.PluginOff],
            rows.Where(row => row.Subject.Source == ConformanceSubject.PluginSource).Select(row => row.Provider.Id.Value)
                .Distinct().Order(StringComparer.Ordinal));
        // The stand-ins are the registry's StandIn providers, never a list of names.
        Assert.Equal(
            registry.Providers.Where(provider => provider.Identity.StandIn).Select(provider => provider.Id.Value),
            rows.Where(row => row.Case.Half == ConformanceHalf.Live && row.Subject.Source == ConformanceSubject.RegistrySource)
                .Select(row => row.Provider.Id.Value).Distinct());
    }

    /// <summary>The cases are §10's whole: each it names is written, run or held pending, and none is left out for lack of a reader.</summary>
    [Fact]
    public void The_cases_are_section_10_s_whole()
    {
        Assert.Equal(Section10.Order(StringComparer.Ordinal), Conformance.Cases.Select(each => each.Name).Order(StringComparer.Ordinal));
        Assert.All(Conformance.Cases, each => Assert.False(string.IsNullOrWhiteSpace(each.Says), $"{each.Name} says nothing"));
    }

    /// <summary>
    /// The case names, by §10's clauses, written down once more on purpose: a case dropped from the suite fails here, so the list stays §10's
    /// whole however many are pending.
    /// </summary>
    private static readonly string[] Section10 =
    [
        // The mandatory cases.
        "identity.rules", "parts.stated", "ids.names-resolve", "posture.unmapped-refused",
        "recipe.no-daoris-variable", "recipe.own-variables-pass", "recipe.daoris-name-refused", "recipe.session-token-key-kept",
        "spawn.key-only-own", "spawn.shared-variable-kept", "spawn.registering-keeps-declared", "spawn.registering-strips-undeclared",
        "accounts.pool-agrees", "accounts.lent-exact", "interactive.declared", "interactive.absent-refused",
        "tables.recorded-sentences", "tables.d210-precedence", "run.unsupported-refused", "frames.unknown-unread",
        "admission.not-widened", "containment.not-widened",
        "applicability.one-fact-differs", "applicability.other-opening", "applicability.other-copy", "records.ids-resolve",
        // Each ability's cases, and a stated absence's fallback.
        "ability.resumes", "ability.takes-settings", "ability.structured", "ability.words-at-next-step", "ability.steering",
        "ability.stop-keeps-conversation", "ability.one-task-stopped", "ability.resume-fails-closed", "ability.requests-held",
        "ability.hands-servers", "ability.servers-inherit-environment", "ability.leaves-own-servers-out",
        "dialect.recorded-frames", "dialect.cross-fed",
        // A stand-in's program run live.
        "live.start", "live.turn", "live.resume-own", "live.missing-conversation", "live.other-writer", "live.word-before-wire",
        "live.limit-cools", "live.transient-retried", "live.access-and-fatal", "live.child-seen-ended", "live.unknown-parent-end",
        "live.usage-null-computed", "live.reported-model", "live.refused-model", "live.reask-later-choice", "live.reask-none",
        "live.held-request", "live.sign-in", "live.pin-controlled", "live.listing-configured",
    ];

    /// <summary>
    /// The layout (D213's row b part 2 note): every provider the suite runs brings its own file, a stand-in's beside its <c>provider.json</c>
    /// and a real provider's under <c>fixtures/providers/&lt;id&gt;/</c>, each in the one shape, and no file stands for a provider nobody runs.
    /// </summary>
    [Fact]
    public void Each_provider_brings_its_own_pending_file_by_one_layout()
    {
        var providers = Conformance.Runs().Select(row => row.Provider.Id.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Empty(ConformancePending.Problems(providers));
        Assert.Equal(
            Path.Combine(SourceFiles.Project(SourceFiles.DriverProject), "Agents", "Providers", "Stub", ConformancePending.FileName),
            ConformancePending.FileOf("stub"));
        Assert.Equal(
            Path.Combine(SourceFiles.Project(SourceFiles.DriverTestsProject), "fixtures", "providers", "claude-code", ConformancePending.FileName),
            ConformancePending.FileOf("claude-code"));
    }

    /// <summary>Every line of every provider's file names a case the suite runs for that provider, on a door it runs it on (or every door).</summary>
    [Fact]
    public void Every_pending_line_names_a_case_the_suite_runs()
    {
        var unmatched = Conformance.Runs()
            .GroupBy(row => row.Provider.Id.Value, StringComparer.OrdinalIgnoreCase)
            .SelectMany(rows => ConformancePending.Unmatched(
                ConformancePending.Of(rows.Key), [.. rows.Select(row => (row.Case.Name, row.Door))]))
            .ToList();

        Assert.True(unmatched.Count == 0, string.Join('\n', unmatched));
    }

    /// <summary>
    /// The pending reader's first rule, planted (never by leaving the suite red): a case held pending that passes fails the suite, naming the
    /// file its line is to go from. The stand-in's identity rules pass, so a line holding them pending fails its row.
    /// </summary>
    [Fact]
    public async Task A_planted_pending_case_that_passes_fails_the_suite()
    {
        var planted = ConformancePending.Parse(
            """[ { "case": "identity.rules", "door": "*", "row": "c", "why": "Planted: a case the stand-in passes, held pending." } ]""",
            "planted.json");
        Assert.Empty(planted.Problems);

        var failed = await Assert.ThrowsAsync<PendingCasePasses>(() =>
            Conformance.RunAsync(ConformanceSubject.RegistrySource, "stub", Conformance.EveryDoor, "identity.rules", _scratch, planted.Lines));

        Assert.Contains("pending case 'identity.rules' on '*' passes", failed.Message);
        Assert.Contains("planted.json", failed.Message);
        // Unplanted, the same row passes, and a held case that fails is still held, with the reason it is.
        Assert.Null(await Conformance.RunAsync(ConformanceSubject.RegistrySource, "stub", Conformance.EveryDoor, "identity.rules", _scratch, []));
        var held = await Conformance.RunAsync(ConformanceSubject.RegistrySource, "stub", Conformance.EveryDoor, "admission.not-widened", _scratch);
        Assert.StartsWith("held pending (row u)", held);
    }

    /// <summary>The second rule, planted: a line naming a case the suite does not run, or a door it does not run that case on, fails.</summary>
    [Fact]
    public void The_reader_fails_a_line_naming_no_case_it_runs()
    {
        var planted = ConformancePending.Parse(
            """
            [
              { "case": "identity.rules", "door": "*", "row": "c", "why": "A provider's case, run on every door at once." },
              { "case": "no.such-case", "door": "*", "row": "c", "why": "A case nobody wrote." },
              { "case": "posture.unmapped-refused", "door": "claude-code", "row": "o", "why": "A door this provider has not." },
              { "case": "parts.stated", "door": "stub", "row": "c", "why": "A provider's case named on one door." }
            ]
            """,
            "planted.json");
        IReadOnlyCollection<(string, string)> runs =
            [("identity.rules", Conformance.EveryDoor), ("parts.stated", Conformance.EveryDoor), ("posture.unmapped-refused", "stub")];
        Assert.Empty(planted.Problems);

        var unmatched = ConformancePending.Unmatched(planted.Lines, runs);

        Assert.Equal(3, unmatched.Count);
        Assert.Contains(unmatched, line => line.Contains("'no.such-case' on '*'", StringComparison.Ordinal));
        Assert.Contains(unmatched, line => line.Contains("'posture.unmapped-refused' on 'claude-code'", StringComparison.Ordinal));
        Assert.Contains(unmatched, line => line.Contains("'parts.stated' on 'stub'", StringComparison.Ordinal));
    }

    /// <summary>The one layout, refused where it is broken: a missing field, a row §11 does not name, a line said twice, a why of two sentences.</summary>
    [Fact]
    public void The_reader_refuses_a_line_out_of_the_layout()
    {
        var read = ConformancePending.Parse(
            """
            [
              { "case": "identity.rules", "door": "*", "row": "z", "why": "No such row." },
              { "case": "parts.stated", "door": "*", "why": "No row at all." },
              { "case": "ids.names-resolve", "door": "*", "row": "c", "why": "Said once." },
              { "case": "ids.names-resolve", "door": "*", "row": "c", "why": "Said twice." },
              { "case": "records.ids-resolve", "door": "*", "row": "c", "why": "Two sentences. Not one." },
              { "case": "accounts.pool-agrees", "door": "*", "row": "c", "why": "A field the layout has no place for.", "more": 1 }
            ]
            """,
            "broken.json");

        Assert.Equal(5, read.Problems.Count);
        Assert.Contains(read.Problems, problem => problem.Contains("row 'z'", StringComparison.Ordinal));
        Assert.Contains(read.Problems, problem => problem.Contains("'row'", StringComparison.Ordinal));
        Assert.Contains(read.Problems, problem => problem.Contains("twice", StringComparison.Ordinal));
        Assert.Contains(read.Problems, problem => problem.Contains("one sentence", StringComparison.Ordinal));
        Assert.Contains(read.Problems, problem => problem.Contains("'more'", StringComparison.Ordinal));
        Assert.NotEmpty(ConformancePending.Parse("{}", "object.json").Problems);
    }
}
