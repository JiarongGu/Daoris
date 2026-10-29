using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.ConformanceLiveCases;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The suite's cases (the agent-provider design §10), every one §10 names: the mandatory cases every provider passes, each stated ability's
/// cases and a stated absence's fallback, and a stand-in's live cases (<c>ProviderConformanceLive.cs</c>). A case today's code cannot answer is
/// written all the same, its reader asked for and missing, and held pending in each provider's file with the row that writes the reader.
/// </summary>
internal static class ConformanceCases
{
    public static IReadOnlyList<ConformanceCase> All { get; } =
    [
        // ---- The mandatory cases: every provider ----
        OfProvider("identity.rules", "its identity's rules (§2.2): unique ids, a door never another provider's id, at least one door, D23's error, the reserved names", IdentityRules),
        OfProvider("parts.stated", "every part stated, an absent one a statement, and Reach never absent (O1-F12)", PartsStated),
        OfProvider("ids.names-resolve", "its ids and aliases: every name it goes by, its account key and each door's accountOf among them, resolving to it", NamesResolve),
        OfDoor("posture.unmapped-refused", "a full or asking start with none mapped refused naming it (D183, D184)", PostureUnmapped),
        OfDoor("recipe.no-daoris-variable", "a recipe naming no variable of Spawning.Daoris (§4.6)", RecipeNamesNoDaorisVariable),
        OfDoor("recipe.own-variables-pass", "its own declared variables passing whatever their spelling, the stub's DAORIS_STUB_CONFIG_DIR among them", RecipeOwnVariablesPass),
        OfProvider("recipe.daoris-name-refused", "a provider declaring a name of Daoris's list refused at registration with D23's error naming it", RecipeDaorisNameRefused),
        OfProvider("recipe.session-token-key-kept", "a provider declaring a session token Daoris only clears (GH_TOKEN) registered, its key set after the clear (O2-F6)", RecipeSessionTokenKeyKept),
        OfDoor("spawn.key-only-own", "its key variables only in its own spawn, and no other provider's credential or home in it (s-vi)", SpawnKeyOnlyOwn,
            (subject, provider, _) => Others(subject.Registry, provider).Count > 0, ConformanceHalf.Environment),
        OfProvider("spawn.shared-variable-kept", "two providers declaring one variable each keep it in its own spawn, a third's spawn stripped of it (O2-F7)", SpawnSharedVariableKept,
            (subject, provider) => Declared(subject.Registry, provider).Count > 0, ConformanceHalf.Environment),
        OfDoor("spawn.registering-keeps-declared", "registering a provider keeps in every other provider's spawn each variable that provider declares (row b, O3-F7)", SpawnRegisteringKeepsDeclared,
            half: ConformanceHalf.Environment),
        OfProvider("spawn.registering-strips-undeclared", "registering a provider whose key no other declares removes that key's inherited sentinel from another's spawn (C4-4.1)", SpawnRegisteringStripsUndeclared,
            (subject, provider) => Declared(subject.Registry, provider).Count > 0, ConformanceHalf.Environment),
        OfProvider("accounts.pool-agrees", "every provider holding one account key answering every account fact equally: its pool's agreement (O4-F1)", PoolAgrees),
        OfProvider("accounts.lent-exact", "each id's Lent part giving a joined or made door exactly the facts it states, one level (O4-F1)", LentExact),
        OfDoor("interactive.declared", "interactive a declaration of the door's, never the transport's (C2-1.1)", InteractiveDeclared),
        OfDoorAsync("interactive.absent-refused", "a door stating interactive absent refused a conversation before any record opens, with today's sentence (C2-1.1)", InteractiveAbsentRefused,
            (_, _, door) => door.Abilities.Interactive is { IsKnown: true, Value: false }),
        OfProvider("tables.recorded-sentences", "each word table's patterns standing on a recorded sentence and reading it (AgentErrorsTests' rule, generalized)", TablesRecorded),
        OfDoor("tables.d210-precedence", "D210's precedence over its tables: a limit's sentence first, the source before any kind, access before transient, the unread fatal", D210Precedence),
        OfDoor("run.unsupported-refused", "an unsupported operation refused before a write (O1-F16)",
            _ => NotYet("AgentRun held by the executors, refusing an operation the run's own evidence does not admit before it writes")),
        OfDoor("frames.unknown-unread", "unknown and malformed frames kept unread, never guessed", FramesUnknownUnread),
        OfProvider("admission.not-widened", "a provider unable to widen admission",
            _ => NotYet("the plan's walk reading each choice's provider's Reach before any context or credential is sent")),
        OfProvider("containment.not-widened", "a provider unable to widen containment",
            _ => NotYet("Platform's launcher starting and containing a completed ProcessRecipe, the door's spawn no longer its own process")),
        OfProvider("applicability.one-fact-differs", "a measurement equal to the run's effective configuration in all but one of its eight facts refused, for each one (C1-3.2, O1-F11)",
            _ => NotYet("the effective configuration whole, the eight facts a measurement's Applies names and the sheet that admits by them")),
        OfProvider("applicability.other-opening", "a negotiation from another opening with the same versions and posture admitting nothing",
            _ => NotYet("a negotiated reading kept per door, version and account, which admits nothing by itself")),
        OfProvider("applicability.other-copy", "a measurement on another session's copy with every other fact equal admitting, the roots compared by their kind (O2-F13)",
            _ => NotYet("the writable roots compared by their kind, the copy alone, in a measurement's Applies")),
        OfProvider("records.ids-resolve", "its old records' ids resolving exactly, by the kind of id each field holds (§5)", RecordIdsResolve),

        // ---- Each ability it states runs its applicable cases; an ability stated absent passes its fallback's ----
        OfDoor("ability.resumes", "a resume: on a protocol door eligibility negotiated at the open, never a resume known; a native door's its own; none the unsupported resume's fallback (§2.6, O2-F12)", AbilityResumes),
        OfDoor("ability.takes-settings", "takes rules, by flag or _meta, and stated absent handed none", AbilityTakesSettings),
        OfDoor("ability.structured", "structured output, stated or its fallback", Ability("Structured")),
        OfDoor("ability.words-at-next-step", "words taken at the next step, stated or its fallback", Ability("WordsAtNextStep")),
        OfDoor("ability.steering", "steering, stated or its fallback", Ability("Steering")),
        OfDoor("ability.stop-keeps-conversation", "a stop that keeps the conversation, on a protocol door session/cancel's (§2.6)", Ability("StopKeepsConversation")),
        OfDoor("ability.one-task-stopped", "one task stopped, stated or its fallback", Ability("OneTaskStopped")),
        OfDoor("ability.resume-fails-closed", "a resume that fails closed, stated or its fallback", Ability("ResumeFailsClosed")),
        OfDoor("ability.requests-held", "requests held for the person, stated or its fallback", Ability("RequestsHeld")),
        OfDoor("ability.hands-servers", "hands servers: on the wire, by a file or not at all, a protocol door's on the wire (§2.6)", Ability("HandsServers")),
        OfDoor("ability.servers-inherit-environment", "its servers inherit its environment, stated or its fallback", Ability("ServersInheritEnvironment")),
        OfDoor("ability.leaves-own-servers-out", "leaves its own servers out, stated or its fallback", Ability("LeavesOwnServersOut")),
        OfDoor("dialect.recorded-frames", "the dialect over its recorded frames giving the readings expected and emitting nothing it was not told; stated absent, plain lines and an end", DialectRecordedFrames),
        OfDoor("dialect.cross-fed", "each protocol dialect over the others' frames, reading only what its own dialect allows (C2-4.2)",
            _ => NotYet("each protocol door's own dialect, fed the other doors' extensions"), (_, _, door) => Protocol(door)),

        // ---- A stand-in's program run live: the Process half ----
        .. LiveCases,
    ];

    // ---- Building a case ----

    private static ConformanceCase OfProvider(
        string name, string says, Action<ConformanceRun> body, Func<ConformanceSubject, Provider, bool>? applies = null,
        ConformanceHalf half = ConformanceHalf.InProcess) =>
        new(name, half, ConformanceScope.Provider, says, (subject, provider, _) => applies?.Invoke(subject, provider) ?? true, Sync(body));

    private static ConformanceCase OfDoor(
        string name, string says, Action<ConformanceRun> body, Func<ConformanceSubject, Provider, AgentDoor, bool>? applies = null,
        ConformanceHalf half = ConformanceHalf.InProcess) =>
        new(name, half, ConformanceScope.Door, says,
            (subject, provider, door) => door is not null && (applies?.Invoke(subject, provider, door) ?? true), Sync(body));

    private static ConformanceCase OfDoorAsync(
        string name, string says, Func<ConformanceRun, Task> body, Func<ConformanceSubject, Provider, AgentDoor, bool>? applies = null) =>
        new(name, ConformanceHalf.InProcess, ConformanceScope.Door, says,
            (subject, provider, door) => door is not null && (applies?.Invoke(subject, provider, door) ?? true), body);

    private static Func<ConformanceRun, Task> Sync(Action<ConformanceRun> body) => run =>
    {
        body(run);
        return Task.CompletedTask;
    };

    /// <summary>A case whose reader is not written yet: it fails, naming the reader, so its line holds it pending.</summary>
    private static void NotYet(string reader) => throw new NotAnsweredYet(reader);

    internal static bool Protocol(AgentDoor door) => door.Transport == Transport.Protocol;

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void NoDifferences(object? left, object? right, string path)
    {
        var found = new List<string>();
        ProvidersBuiltTests.Differences(left, right, path, found);
        Assert.True(found.Count == 0, string.Join('\n', found));
    }

    // ---- The mandatory cases ----

    private static void IdentityRules(ConformanceRun run)
    {
        var (registry, provider) = (run.Registry, run.Provider);
        var others = registry.Providers.Where(other => !Same(other.Id.Value, provider.Id.Value)).ToList();
        var doors = registry.Providers.SelectMany(each => each.Doors).ToList();

        Assert.Single(registry.Providers, other => Same(other.Id.Value, provider.Id.Value));
        foreach (var door in provider.Doors)
        {
            Assert.Single(doors, other => Same(other.Id.Value, door.Id.Value));
            Assert.DoesNotContain(others, other => Same(other.Id.Value, door.Id.Value));
            Assert.Equal(provider.Id, registry.ProviderOf(door.Id));
            // A door's plugin is the source's word, one of the plugins its provider was declared by; a built-in door names none.
            if (door.Identity.DeclaredBy is { } plugin) Assert.Contains(plugin, provider.Identity.DeclaredBy);
        }

        if (provider.Unavailable) Assert.Empty(provider.Doors);
        else Assert.True(provider.Doors.Count > 0 || provider.Program.IsKnown, $"'{provider.Id}' has no door and names no program: no provider and no holder");

        // A provider's id where a door is asked is D23's error naming what exists, never its first door; an unknown id is that error too.
        if (!provider.Doors.Any(door => Same(door.Id.Value, provider.Id.Value)))
        {
            Assert.Contains("one of:", Assert.Throws<DriverException>(() => registry.Door(provider.Id.Value)).Message);
        }

        var unknown = provider.Id.Value + "-unregistered";
        Assert.Contains(provider.Id.Value, Assert.Throws<DriverException>(() => registry.Provider(unknown)).Message);
        Assert.Contains("one of:", Assert.Throws<DriverException>(() => registry.Door(unknown)).Message);

        // A plugin declares no id the built set holds; a built provider's ids are reserved by registering (D64's rule 3, O4-F9).
        var names = provider.Doors.Select(door => door.Id.Value).Append(provider.Id.Value).ToList();
        if (provider.Identity.DeclaredBy.Count > 0 || provider.Unavailable)
        {
            Assert.All(names, name => Assert.DoesNotContain(name, registry.Reserved, StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            Assert.All(names, name => Assert.Contains(name, registry.Reserved, StringComparer.OrdinalIgnoreCase));
        }
    }

    private static void PartsStated(ConformanceRun run)
    {
        var problems = new List<string>();
        Stated(run.Provider, run.Provider.Id.Value, problems);
        foreach (var door in run.Provider.Doors)
        {
            Stated(door, $"{run.Provider.Id}/{door.Id}", problems);
            Stated(door.Abilities, $"{run.Provider.Id}/{door.Id}.Abilities", problems);
        }

        Assert.True(Enum.IsDefined(run.Provider.Reach), $"Reach is {run.Provider.Reach}: required, never absent");
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>Each part of a record stated: a known fact with its evidence, where it applies and its value; an absent one carrying none; a required part present.</summary>
    private static void Stated(object record, string path, List<string> problems)
    {
        var nullability = new NullabilityInfoContext();
        foreach (var property in record.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(each => each.GetIndexParameters().Length == 0))
        {
            var value = property.GetValue(record);
            var type = property.PropertyType;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Known<>))
            {
                var known = (bool)type.GetProperty("IsKnown")!.GetValue(value)!;
                var evidence = (string?)type.GetProperty("Evidence")!.GetValue(value);
                var applies = (string?)type.GetProperty("Applies")!.GetValue(value);
                var held = type.GetProperty("Value")!.GetValue(value);
                if (known && (string.IsNullOrWhiteSpace(evidence) || string.IsNullOrWhiteSpace(applies) || held is null))
                {
                    problems.Add($"{path}.{property.Name}: known with no evidence, no place it applies, or no value");
                }

                if (!known && (evidence is not null || applies is not null)) problems.Add($"{path}.{property.Name}: absent, yet carrying evidence");
            }
            else if (value is null && nullability.Create(property).ReadState != NullabilityState.Nullable)
            {
                problems.Add($"{path}.{property.Name}: a required part is null");
            }
        }
    }

    private static void NamesResolve(ConformanceRun run)
    {
        var (registry, provider) = (run.Registry, run.Provider);

        Assert.Same(provider, registry.Provider(provider.Id.Value));
        Assert.Same(provider, registry.Provider(provider.Id.Value.ToUpperInvariant()));
        foreach (var door in provider.Doors)
        {
            Assert.Same(door, registry.Door(door.Id));
            Assert.Same(door, registry.Door(door.Id.Value.ToUpperInvariant()));
            Assert.Equal(door.AccountOf ?? new AccountKey(door.Id.Value), registry.OwnerOf(door.Id));
            Assert.Equal(registry.OwnerOf(door.Id), registry.OwnerOfAgent(door.Id.Value));
        }

        var key = provider.KeyOf();
        Assert.Contains(provider.Id, registry.Pool(key).Providers);
        if (!provider.Doors.Any(door => Same(door.Id.Value, provider.Id.Value))) Assert.Equal(key, registry.OwnerOfAgent(provider.Id.Value));
        // Every name it goes by, its account key and each door's accountOf among them, is an agent whose accounts are kept.
        Assert.All(ProviderRegistration.Names(provider), name => Assert.Contains(name, registry.Known, StringComparer.OrdinalIgnoreCase));
    }

    private static void RecordIdsResolve(ConformanceRun run)
    {
        var (registry, provider) = (run.Registry, run.Provider);

        Assert.Equal(Enum.GetValues<IdKind>().Order(), RecordIds.Fields.Select(field => field.Kind).Distinct().Order());
        foreach (var door in provider.Doors) Assert.Same(door, registry.Resolve(door.Id.Value, IdKind.Door).Door);
        Assert.Same(provider, registry.Resolve(provider.Id.Value, IdKind.Provider).Provider);
        Assert.Contains(provider.Id, registry.Resolve(provider.KeyOf().Value, IdKind.AccountKey).Pool!.Providers);
        Assert.Same(provider, registry.Resolve(provider.Id.Value, IdKind.Program).Provider);
        foreach (var door in provider.Doors.Where(door => !registry.Providers.Any(each => Same(each.Id.Value, door.Id.Value))))
        {
            Assert.Same(door, registry.Resolve(door.Id.Value, IdKind.Program).Door);
        }

        // A provider's id is refused as a door's, never read as its first door (codex).
        if (!provider.Doors.Any(door => Same(door.Id.Value, provider.Id.Value)))
        {
            Assert.Contains("one of:", Assert.Throws<DriverException>(() => registry.Resolve(provider.Id.Value, IdKind.Door)).Message);
        }
    }

    private static void PostureUnmapped(ConformanceRun run)
    {
        var (door, adapter) = (run.TheDoor, run.Adapter);
        var name = $"`{door.Id}`";

        Assert.Equal((door.Postures.Ordinary, door.Postures.Asking, door.Postures.Full), (adapter.AcpPosture, adapter.AskingPosture, adapter.FullPosture));
        var help = HelpAccess.For(HelpModes.Full, adapter).Refusal;
        var unrestricted = DrivenAccess.For(Permissions(AccessRules.Unrestricted), "engine", adapter).Refusal;
        var asking = DrivenAccess.For(Permissions(AccessRules.Asking), "engine", adapter).Refusal;
        if (door.Postures.Full is { Length: > 0 })
        {
            Assert.Null(help);
            Assert.Null(unrestricted);
        }
        else
        {
            Assert.Contains(name, help);
            Assert.Contains(name, unrestricted);
        }

        if (door.Postures.Asking is { Length: > 0 }) Assert.Null(asking);
        else Assert.Contains(name, asking);
    }

    private static ResolvedAccess Permissions(string value) => AccessRules.ResolveLevels(
        new AccessSetting { Pauses = new Dictionary<string, string> { [AccessRules.Permissions] = value } }, null, null);

    private static void RecipeNamesNoDaorisVariable(ConformanceRun run)
    {
        var info = run.Adapter.Prepare(run.Target(), run.Command());
        var named = Spawning.Roles.Where(name => info.Environment.TryGetValue(name, out var value) && value is not null).ToList();

        Assert.True(named.Count == 0, $"the door's own spawn sets Daoris's {string.Join(", ", named)}: the transports' shell sets them after the recipe (§4.6)");
    }

    private static void RecipeOwnVariablesPass(ConformanceRun run)
    {
        // Registered, whatever its spelling: no prefix rule refuses a provider's own variable.
        _ = new ProviderRegistry([run.Provider], []);
        if (run.TheDoor.Home.Variable is not { Length: > 0 } variable || run.Adapter.Toolchain is not { } toolchain) return;

        var info = run.Adapter.Prepare(run.Target(), run.Command());
        var profile = Path.Combine(run.Home(), "profile");
        HarnessProbe.Apply(info, toolchain, profile);

        Assert.Equal(profile, info.Environment[variable]);
    }

    private static void RecipeDaorisNameRefused(ConformanceRun run)
    {
        const string daoris = "DAORIS_TARGET";
        Assert.Contains(daoris, Spawning.Roles);

        var refused = Assert.Throws<DriverException>(() => new ProviderRegistry([Keyed(run.Provider, daoris)], []));
        Assert.Contains(daoris, refused.Message);
    }

    private static void RecipeSessionTokenKeyKept(ConformanceRun run)
    {
        const string token = "GH_TOKEN";

        // Registered: a session token Daoris only clears is a provider's to declare as its key.
        _ = new ProviderRegistry([Keyed(run.Provider, token)], []);
        foreach (var door in run.Provider.Doors)
        {
            var adapter = run.Subject.Adapters.Resolve(door.Id.Value);
            if (adapter.Toolchain is not { } toolchain) continue;
            var info = adapter.Prepare(run.Target(), run.Command());
            HarnessProbe.Apply(info, toolchain, profileHome: null, account: new Dictionary<string, string> { [token] = "conformance-key" });
            Assert.Equal("conformance-key", info.Environment[token]);
        }
    }

    /// <summary>The provider, its pool declaring <paramref name="variable"/> as its key.</summary>
    private static Provider Keyed(Provider provider, string variable)
    {
        var accounts = provider.Accounts.IsKnown ? provider.Accounts.Required : new AccountsPart(provider.KeyOf(), null, PoolFacts.None);
        return provider with
        {
            Accounts = Known<AccountsPart>.Declared(accounts with { Facts = accounts.Facts with { KeyVariable = variable } }, "a conformance case's key"),
        };
    }

    private static void PoolAgrees(ConformanceRun run)
    {
        var registry = run.Registry;
        var key = run.Provider.KeyOf();
        var pool = registry.Pool(key);

        Assert.Equal(key, pool.Key);
        Assert.Contains(run.Provider.Id, pool.Providers);
        Assert.All(pool.Providers, id => Assert.Equal(key, registry.Provider(id).KeyOf()));
        var answers = pool.Doors.Select(door => (Door: door, Facts: AccountFacts(registry, door))).ToList();
        foreach (var (door, facts) in answers.Skip(1)) NoDifferences(answers[0].Facts, facts, $"{answers[0].Door} against {door}");
    }

    /// <summary>Every account fact a door reads: its words, its windows, its usage question and the agent that answers for its accounts.</summary>
    private sealed record AccountRead(LimitWords? Limits, SignInWords? SignIn, WindowWords? Windows, IUsageQuestion? Usage, string? Agent, PoolFacts? Accounts);

    private static AccountRead AccountFacts(ProviderRegistry registry, DoorId door)
    {
        var agent = registry.AccountAgentOf(door);
        return new(registry.LimitsOf(door), registry.SignInOf(door), registry.WindowsOf(door), registry.UsageOf(door), agent?.Name, agent?.Accounts);
    }

    private static void LentExact(ConformanceRun run)
    {
        var (registry, provider) = (run.Registry, run.Provider);
        foreach (var door in provider.Doors)
        {
            var key = registry.OwnerOf(door.Id);
            // Keyed by its own name, a door reads its own declarations alone.
            if (Same(key.Value, door.Id.Value)) continue;

            var lender = Same(key.Value, provider.Id.Value)
                ? provider.Lent.IsKnown ? provider.Lent.Required : LentPart.None
                : registry.Doors.Any(each => Same(each.Value, key.Value)) ? registry.Door(key.Value).Lent : LentPart.None;
            var own = door.Lent;

            // Its own declaration, else what the id its key names lends, and never what that one reads in turn.
            Assert.Same(own.Tables.Limits ?? lender.Tables.Limits, registry.LimitsOf(door.Id));
            Assert.Same(own.Tables.SignIn ?? lender.Tables.SignIn, registry.SignInOf(door.Id));
            Assert.Same(own.Tables.Windows ?? lender.Tables.Windows, registry.WindowsOf(door.Id));
            Assert.Same(own.Usage.Question ?? lender.Usage.Question, registry.UsageOf(door.Id));
            if (!ReferenceEquals(lender, LentPart.None) && registry.AccountAgentOf(door.Id) is { } agent)
            {
                NoDifferences(lender.Accounts, agent.Accounts, $"{door.Id}'s account facts against what '{key}' lends");
            }
        }
    }

    private static void InteractiveDeclared(ConformanceRun run)
    {
        var interactive = run.TheDoor.Abilities.Interactive;

        Assert.True(interactive.IsKnown, "interactive is each door's declaration, stated on every door");
        Assert.Equal(KnownBy.Declared, interactive.By);
        Assert.DoesNotContain("transport", interactive.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(interactive.Value, run.Adapter.Interactive);
    }

    private static async Task InteractiveAbsentRefused(ConformanceRun run)
    {
        var door = run.TheDoor;
        var home = run.Home();
        var ledger = new ChatLedger().Register("engine", run.Root());
        using var client = ledger.Client();
        using var runner = new ChatRunner(client, run.Subject.Adapters, home, new SessionProcesses(DaorisHome.Entry(home, HomeEntry.Sessions)));

        var started = await runner.StartAsync("engine", door.Id.Value, DriverConfig.Empty);

        Assert.Null(started.SessionId);
        Assert.Equal(
            $"the `{door.Id}` adapter does not hold conversations — it spawns an agent that takes its target once and runs to completion.",
            started.Message);
        Assert.Equal(0, ledger.Count);
        // And the door's own spawn refuses the conversation, naming it, before anything starts.
        Assert.Contains($"`{door.Id}`", Assert.Throws<DriverException>(() => run.Adapter.PrepareChat(run.Chat(), run.Command())).Message);
    }

    private static void TablesRecorded(ConformanceRun run)
    {
        var provider = run.Provider;
        var tables = new List<(string Where, TablesPart Tables)>();
        if (provider.Tables.IsKnown) tables.Add(("its tables", provider.Tables.Required));
        if (provider.Lent.IsKnown) tables.Add(("what it lends", provider.Lent.Required.Tables));
        tables.AddRange(provider.Doors.Select(door => ($"{door.Id}'s own", door.Lent.Tables)));
        var errors = tables.Select(each => (each.Where, Words: each.Tables.ErrorWords))
            .Concat(provider.Doors.Select(door => ($"{door.Id}'s door tables", Words: door.Tables.ErrorWords)));

        var problems = new List<string>();
        foreach (var (where, part) in tables)
        {
            if (part.Limits is { } limits) problems.AddRange(Unproven(limits).Select(problem => $"{where}, limits: {problem}"));
            if (part.SignIn is { } signIn) problems.AddRange(Unproven(signIn).Select(problem => $"{where}, sign-in: {problem}"));
            if (part.Windows is { } windows) problems.AddRange(Unproven(windows).Select(problem => $"{where}, windows: {problem}"));
        }

        foreach (var (where, words) in errors)
        {
            if (words is not { } said) continue;
            var sentences = said.Recorded.Where(recorded => !recorded.SourceDerived).Select(recorded => recorded.Sentence).ToList();
            problems.AddRange(said.Transient.Where(pattern => !sentences.Any(sentence => AgentErrors.Matches(pattern, sentence)))
                .Select(pattern => $"{where}, error words: transient `{pattern}` reads no recorded sentence"));
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    private static string InZone(string sentence) => sentence.Replace(AccountLimits.ZonePlaceholder, "UTC", StringComparison.Ordinal);

    private static IEnumerable<string> Unproven(LimitWords words)
    {
        var sentences = words.Recorded.Select(recorded => InZone(recorded.Sentence)).ToList();
        if (sentences.Count == 0) yield return "no recorded sentence";
        foreach (var pattern in words.Markers.Concat(words.Resets).Where(pattern => !sentences.Any(sentence => AccountLimits.Matches(pattern, sentence))))
        {
            yield return $"`{pattern}` reads no recorded sentence";
        }

        foreach (var sentence in sentences.Where(sentence => !words.Markers.Any(marker => AccountLimits.Matches(marker, sentence))))
        {
            yield return $"recorded `{sentence}` is read by no marker";
        }
    }

    private static IEnumerable<string> Unproven(SignInWords words)
    {
        var sentences = words.Recorded.Select(recorded => recorded.Sentence).ToList();
        if (sentences.Count == 0) yield return "no recorded sentence";
        foreach (var marker in words.Markers.Where(marker => !sentences.Any(sentence => SignInRefusals.Matches(marker, sentence))))
        {
            yield return $"`{marker}` reads no recorded sentence";
        }

        foreach (var sentence in sentences.Where(sentence => !words.Markers.Any(marker => SignInRefusals.Matches(marker, sentence))))
        {
            yield return $"recorded `{sentence}` is read by no marker";
        }
    }

    private static IEnumerable<string> Unproven(WindowWords words)
    {
        var frames = words.Recorded.Select(recorded => recorded.Frame).ToList();
        if (frames.Count == 0) yield return "no recorded frame";
        foreach (var word in words.Windows.Keys.Concat(words.Clear).Concat(words.Warned).Concat(words.Refused).Append(words.Credits))
        {
            if (!frames.Any(frame => frame.Contains(JsonSerializer.Serialize(word), StringComparison.Ordinal))) yield return $"`{word}` stands in no recorded frame";
        }
    }

    private static void D210Precedence(ConformanceRun run)
    {
        var (registry, door, adapters, adapter) = (run.Registry, run.TheDoor, run.Subject.Adapters, run.Adapter);
        var wire = Protocol(door) ? "protocol" : "pipe";
        AgentError Read(string words, int? status, string? kind, string where, DateTimeOffset seen) =>
            AgentErrors.Read(adapters, adapter, new DoorFailure(wire, null, kind, status, words, where), seen, TimeZoneInfo.Utc);
        static DateTimeOffset Seen(string day) => DateTimeOffset.Parse(day + "T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        foreach (var recorded in registry.LimitsOf(door.Id)?.Recorded ?? [])
        {
            var words = InZone(recorded.Sentence);
            // A limit's sentence first, whatever transient kind or status is beside it (D210 §3.2 step 1).
            Assert.Equal(AgentErrorKind.Limit, Read(words, 529, "overloaded", DoorWhere.Prompt, Seen(recorded.Seen)).Kind);
            // The source before any kind: the harness's own lines are never a limit.
            Assert.NotEqual(AgentErrorKind.Limit, Read(words, null, null, DoorWhere.Harness, Seen(recorded.Seen)).Kind);
        }

        // A sign-in's refusal is access, before a transient status beside it.
        foreach (var recorded in registry.SignInOf(door.Id)?.Recorded ?? [])
        {
            Assert.Equal(AgentErrorKind.Access, Read(recorded.Sentence, 529, null, DoorWhere.Prompt, Seen(recorded.Seen)).Kind);
        }

        // What no table reads is fatal, as today.
        var unread = Read("a failure no agent's table says", null, null, DoorWhere.Prompt, DateTimeOffset.UnixEpoch);
        Assert.Equal((AgentErrorKind.Fatal, AgentErrors.Unread), (unread.Kind, unread.By));
    }

    private static void FramesUnknownUnread(ConformanceRun run)
    {
        var (door, adapter) = (run.TheDoor, run.Adapter);
        if (Protocol(door)) NotYet("the door's IProtocolDialect, handed only what the protocol transport does not read itself");
        if (door.Dialect.IsKnown || adapter.StructuredOutput() is not null) NotYet("the door's INativeDialect over its program's stream, the door stating it");

        // No dialect stated: plain lines and an end, so no line is read as a frame at all (§2.6).
        Assert.Null(adapter.StructuredOutput());
    }

    // ---- Each ability ----

    private static void AbilityResumes(ConformanceRun run)
    {
        var (door, adapter) = (run.TheDoor, run.Adapter);
        var resumes = door.Abilities.Resumes;

        Assert.True(resumes.IsKnown, "a resume is stated on every door");
        Assert.Equal(resumes.Value, adapter.Resumes);
        var resumed = adapter.PrepareResume(run.Target(), run.Command(), "c-1", "go on");
        if (Protocol(door))
        {
            // The transport's default: eligibility to negotiate at the open, never a resume known (X; §2.6).
            Assert.True(resumes.Value);
            Assert.Equal(Eligibility.Applies, resumes.Applies);
            Assert.Null(resumed);
        }
        else if (resumes.Value)
        {
            Assert.NotNull(resumed);
            Assert.Contains("c-1", resumed.ArgumentList);
        }
        else
        {
            // The unsupported resume's fallback, answered as today: no process to resume with.
            Assert.Null(resumed);
        }
    }

    private static void AbilityTakesSettings(ConformanceRun run)
    {
        var (door, adapter) = (run.TheDoor, run.Adapter);
        var takes = door.Abilities.TakesSettings;
        var settings = Path.Combine(run.Home(), "settings.json");

        Assert.True(takes.IsKnown, "taking rules is stated on every door");
        Assert.Equal(takes.Value, adapter.TakesSettings);
        var info = adapter.Prepare(run.Target(), run.Command());
        List<string> before = [.. info.ArgumentList];
        adapter.HandSettings(info, settings);
        var meta = adapter.AcpSessionMeta(settings, []);
        if (!takes.Value)
        {
            // Stated absent: nothing composed, nothing handed, on either way in.
            Assert.Equal(before, info.ArgumentList);
            Assert.Null(meta);
        }
        else if (Protocol(door))
        {
            Assert.NotNull(meta);
            Assert.Contains(JsonSerializer.Serialize(settings).Trim('"'), JsonSerializer.Serialize(meta), StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(settings, info.ArgumentList);
        }
    }

    /// <summary>
    /// An ability the contract does not hold yet as a door's <c>Known</c> part: the case asks the part for it, so it passes once the row that
    /// states the ability lands, and the provider's line then goes.
    /// </summary>
    private static Action<ConformanceRun> Ability(string part) => run =>
    {
        if (typeof(AbilitiesPart).GetProperty(part) is not { } property) NotYet($"AbilitiesPart.{part}, the door's Known ability");
        else Assert.NotNull(property.GetValue(run.TheDoor.Abilities));
    };

    private static void DialectRecordedFrames(ConformanceRun run)
    {
        var (door, adapter) = (run.TheDoor, run.Adapter);
        if (Protocol(door)) NotYet("the door's IProtocolDialect over its recorded frames under fixtures/providers/<id>/");
        if (door.Dialect.IsKnown || adapter.StructuredOutput() is not null) NotYet("the door's INativeDialect over its recorded frames under fixtures/providers/<id>/");

        // Stated absent, the fallback: plain lines and an end, nothing more.
        Assert.Null(adapter.StructuredOutput());
        Assert.Equal("a word", adapter.FrameMessage("a word"));
        Assert.Null(adapter.FrameInterrupt());
    }

    // ---- The spawn cases: sentinels planted in the process's environment, read off a prepared spawn ----

    /// <summary>The variables a provider declares of its own: each door's home variable, its pool's and its doors' account agents' key, its programs' pinned ones.</summary>
    internal static IReadOnlyList<string> Declared(ProviderRegistry registry, Provider provider) =>
    [
        .. provider.Doors.Select(door => door.Home.Variable)
            .Append(provider.Accounts.IsKnown ? provider.Accounts.Required.Facts.KeyVariable : null)
            .Concat(provider.Doors.Select(door => registry.AccountAgentOf(door.Id)?.Accounts.KeyVariable))
            .Concat(Pinned(provider.Program))
            .Concat(provider.Doors.SelectMany(door => Pinned(door.Program)))
            .OfType<string>()
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal),
    ];

    private static IEnumerable<string> Pinned(Known<ProgramFacts> program) =>
        program is { IsKnown: true, Value.PinnedEnvironment: { } pinned } ? pinned.Keys : [];

    /// <summary>What the other providers this view registers declare and this one does not.</summary>
    private static IReadOnlyList<string> Others(ProviderRegistry registry, Provider provider)
    {
        var mine = Declared(registry, provider);
        return
        [
            .. registry.Providers.Where(other => !Same(other.Id.Value, provider.Id.Value))
                .SelectMany(other => Declared(registry, other))
                .Except(mine, StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static string Sentinel(string name) => "conformance-sentinel-" + name.ToLowerInvariant();

    /// <summary>
    /// Runs <paramref name="body"/> with each variable planted in the process's environment, the case's own home as Daoris's, and puts
    /// back what was there.
    /// </summary>
    private static void Planted(ConformanceRun run, IEnumerable<string> variables, Action body)
    {
        var planted = variables.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(name => name, Sentinel, StringComparer.OrdinalIgnoreCase);
        planted[DaorisHome.Variable] = run.Home();
        var was = planted.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var (name, value) in planted) Environment.SetEnvironmentVariable(name, value);
            body();
        }
        finally
        {
            foreach (var (name, value) in was) Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <summary>Each spawn a door prepares: a driven start's, and a conversation's where it holds one.</summary>
    private static IEnumerable<ProcessStartInfo> Spawns(ConformanceRun run, ISessionAdapter adapter)
    {
        yield return adapter.Prepare(run.Target(), run.Command());
        if (adapter.Interactive) yield return adapter.PrepareChat(run.Chat(), run.Command());
    }

    private static bool Inherits(ProcessStartInfo info, string name) =>
        info.Environment.TryGetValue(name, out var value) && value == Sentinel(name);

    private static void SpawnRegisteringKeepsDeclared(ConformanceRun run)
    {
        var registry = run.Registry;
        var everyone = registry.Providers.SelectMany(provider => Declared(registry, provider));
        var mine = Declared(registry, run.Provider);
        Planted(run, everyone, () =>
        {
            foreach (var info in Spawns(run, run.Adapter))
            {
                var lost = mine.Where(name => !info.Environment.TryGetValue(name, out var value) || string.IsNullOrEmpty(value)).ToList();
                Assert.True(lost.Count == 0, $"{run.TheDoor.Id}'s spawn lost {string.Join(", ", lost)}, which its provider declares, beside every provider registered");
            }
        });
    }

    private static void SpawnKeyOnlyOwn(ConformanceRun run)
    {
        var others = Others(run.Registry, run.Provider);
        Planted(run, others, () =>
        {
            foreach (var info in Spawns(run, run.Adapter))
            {
                var inherited = others.Where(name => Inherits(info, name)).ToList();
                Assert.True(inherited.Count == 0, $"{run.TheDoor.Id}'s spawn inherits {string.Join(", ", inherited)}, another provider's");
            }
        });
    }

    private static void SpawnSharedVariableKept(ConformanceRun run)
    {
        var variable = Declared(run.Registry, run.Provider)[0];
        var twin = Bare("conformance-twin", variable);
        var third = Bare("conformance-third", null);
        _ = new ProviderRegistry([run.Provider, twin, third], []);
        Planted(run, [variable], () =>
        {
            foreach (var door in run.Provider.Doors)
            {
                Assert.True(run.Subject.Adapters.Resolve(door.Id.Value).Prepare(run.Target(), run.Command()).Environment.ContainsKey(variable));
            }

            Assert.True(Inherits(Prepared(run, twin), variable), $"the twin declaring {variable} lost it");
            Assert.False(Inherits(Prepared(run, third), variable), $"a third provider's spawn inherits {variable}, which two others declare");
        });
    }

    private static void SpawnRegisteringStripsUndeclared(ConformanceRun run)
    {
        var declared = Declared(run.Registry, run.Provider);
        var key = run.Provider.Accounts is { IsKnown: true, Value.Facts.KeyVariable: { Length: > 0 } own } ? own : declared[0];
        var third = Bare("conformance-third", null);
        _ = new ProviderRegistry([run.Provider, third], []);
        Planted(run, [key], () => Assert.False(Inherits(Prepared(run, third), key), $"another provider's spawn inherits {key}, which only '{run.Provider.Id}' declares"));
    }

    private static ProcessStartInfo Prepared(ConformanceRun run, Provider provider) =>
        new LegacyDoor(provider, provider.Doors[0]).Prepare(run.Target(), run.Command());

    /// <summary>A provider of one native door, declaring at most a home variable: the cases' second and third providers.</summary>
    private static Provider Bare(string id, string? variable)
    {
        var door = new AgentDoor(
            new DoorId(id), Transport.Native, new DoorIdentity(null, null, null), Known<ProgramFacts>.Absent,
            new DoorHome(variable, OwnHome: true, TrustFile: null, MustExist: false), LentPart.None, Known<PortRef>.Absent,
            new PosturesPart(null, null, null), Known<ISpawn>.Declared(new BareSpawn(), "a conformance case's provider"), Known<IDialect>.Absent,
            new AbilitiesPart(
                Known<bool>.Declared(false, "a conformance case's provider"),
                Known<bool>.Declared(false, "a conformance case's provider"),
                Known<bool>.Declared(false, "a conformance case's provider")),
            new DoorTablesPart(null, null), null);
        return new Provider(
            new ProviderId(id), new ProviderIdentity(null, null, null, false, []), [door], Known<ProgramFacts>.Absent,
            Known<AccountsPart>.Absent, Known<TablesPart>.Absent, Known<UsagePart>.Absent, Known<LentPart>.Absent, Known<PortRef>.Absent,
            Known<IChildVocabulary>.Absent, Known<PortRef>.Absent, Known<PortRef>.Absent, Known<IConversationHome>.Absent, Reach.Everywhere,
            Known<VocabularyPart>.Absent);
    }

    private sealed class BareSpawn : ISpawn
    {
        public ProcessStartInfo Start(SessionTarget target, IReadOnlyList<string>? command) =>
            Spawning.InRoot(target, command![0], command.Skip(1));

        public ProcessStartInfo Chat(ChatTarget target, IReadOnlyList<string>? command) =>
            Spawning.ChatInRoot(target, command![0], command.Skip(1));
    }
}
