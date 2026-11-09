using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one registration list holds the stand-ins, Claude Code, Codex and
/// dsh written on the contract. The ports a provider written on the contract alone needs are declared; each provider states the ones it uses.
/// </summary>
public sealed class ProvidersBuiltTests
{
    private static Provider Listed(string id) => Providers.Built().Single(provider => provider.Id.Value == id);

    public static TheoryData<string> Ids => ["claude-code", "codex", "dsh", "stub"];

    public static TheoryData<string> RealDoors => ["claude-code", "claude-code-acp", "codex-acp", "dsh"];

    // What only the contract carries: a class never stated these, so the old registry holds each absent where a registration states it.
    private static readonly Type[] Ports =
    [
        typeof(ISpawn), typeof(IDialect), typeof(IChildVocabulary), typeof(IConversationHome),
        typeof(IDoorOffers), typeof(ISettingsFile), typeof(IRuleLanguage), typeof(ISkillLayout),
    ];

    /// <summary>
    /// Whether two values state the same facts, collecting where they do not: a record by its members, a list by its items, a table
    /// by its entries, a known fact by its being known and its value (a port, which only the contract carries, and the evidence's
    /// wording are left out unless asked).
    /// </summary>
    internal static void Differences(object? left, object? right, string path, List<string> found, bool withEvidence = false)
    {
        if (ReferenceEquals(left, right)) return;
        if (left is null || right is null) { found.Add($"{path}: {left ?? "null"} against {right ?? "null"}"); return; }
        var type = left.GetType();
        if (type != right.GetType() && !(left is IEnumerable && right is IEnumerable && left is not string)) { found.Add($"{path}: {type.Name} against {right.GetType().Name}"); return; }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Known<>))
        {
            if (Ports.Any(port => port.IsAssignableFrom(type.GetGenericArguments()[0]))) return;
            var known = (bool)type.GetProperty("IsKnown")!.GetValue(left)!;
            if (known != (bool)type.GetProperty("IsKnown")!.GetValue(right)!) { found.Add($"{path}: known against absent"); return; }
            if (!known) return;
            Differences(type.GetProperty("By")!.GetValue(left), type.GetProperty("By")!.GetValue(right), path + ".By", found);
            Differences(type.GetProperty("Value")!.GetValue(left), type.GetProperty("Value")!.GetValue(right), path + ".Value", found, withEvidence);
            if (withEvidence)
            {
                Differences(type.GetProperty("Evidence")!.GetValue(left), type.GetProperty("Evidence")!.GetValue(right), path + ".Evidence", found);
                Differences(type.GetProperty("Applies")!.GetValue(left), type.GetProperty("Applies")!.GetValue(right), path + ".Applies", found);
            }

            return;
        }

        if (type.IsPrimitive || type.IsEnum || left is string || left is TimeSpan)
        {
            if (!left.Equals(right)) found.Add($"{path}: {left} against {right}");
            return;
        }

        if (left is Regex regex)
        {
            var other = (Regex)right;
            if (regex.ToString() != other.ToString() || regex.Options != other.Options) found.Add($"{path}: {regex} against {other}");
            return;
        }

        if (left is Delegate) { found.Add($"{path}: two different delegates"); return; }
        if (left is IDictionary pairs)
        {
            var theirs = (IDictionary)right;
            if (pairs.Count != theirs.Count) found.Add($"{path}: {pairs.Count} entries against {theirs.Count}");
            foreach (var key in pairs.Keys)
            {
                if (!theirs.Contains(key)) found.Add($"{path}[{key}]: missing");
                else Differences(pairs[key], theirs[key], $"{path}[{key}]", found, withEvidence);
            }

            return;
        }

        if (left is IEnumerable items)
        {
            var mine = items.Cast<object?>().ToList();
            var yours = ((IEnumerable)right).Cast<object?>().ToList();
            if (mine.Count != yours.Count) { found.Add($"{path}: {mine.Count} items against {yours.Count}"); return; }
            for (var i = 0; i < mine.Count; i++) Differences(mine[i], yours[i], $"{path}[{i}]", found, withEvidence);
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(each => each.Name != "EqualityContract" && each.GetIndexParameters().Length == 0))
        {
            if (Ports.Any(port => port.IsAssignableFrom(property.PropertyType)) || property.Name == nameof(AbilitiesPart.ServersInherit)) continue;
            Differences(property.GetValue(left), property.GetValue(right), $"{path}.{property.Name}", found, withEvidence);
        }
    }

    private static List<string> Compared(object? left, object? right, bool withEvidence = false)
    {
        var found = new List<string>();
        Differences(left, right, "", found, withEvidence);
        return found;
    }

    [Fact]
    public void The_registration_list_holds_each_provider_with_its_doors_on_their_transports()
    {
        Assert.Equal(["claude-code", "codex", "dsh", "stub", "alt-stub"], Providers.Built().Select(provider => provider.Id.Value));
        string Doors(string id) => string.Join(' ', Listed(id).Doors.Select(door => $"{door.Id}:{door.Transport}"));

        Assert.Equal("claude-code:Native claude-code-acp:Protocol", Doors("claude-code"));
        Assert.Equal("codex-acp:Protocol", Doors("codex"));
        Assert.Equal("dsh:Protocol", Doors("dsh"));
        Assert.Equal("acp-stub:Protocol stub:Native", Doors("stub"));
        Assert.Equal("alt-acp-stub:Protocol alt-stub:Native", Doors("alt-stub"));
        Assert.Equal(["stub", "alt-stub"], Providers.Built().Where(provider => provider.Identity.StandIn).Select(provider => provider.Id.Value));
        Assert.All(Providers.Built(), provider => Assert.Equal(Reach.Everywhere, provider.Reach));
        Assert.All(Providers.Built(), provider => Assert.False(provider.Unavailable));
    }

    /// <summary>The registry over the built set, whose doors all arrive through the shim, equals the one over the registration list.</summary>
    [Fact]
    public void The_registry_over_the_built_set_equals_the_one_over_the_registration_list()
    {
        var built = ProviderRegistry.Built();
        var listed = new ProviderRegistry(Providers.Built(), []);

        foreach (var provider in listed.Providers) Assert.Empty(Compared(built.Provider(provider.Id), provider));
        foreach (var door in listed.Doors)
        {
            Assert.Empty(Compared(built.Door(door), listed.Door(door)));
            Assert.Equal(listed.OwnerOf(door), built.OwnerOf(door));
            Assert.Equal(listed.ProviderOf(door), built.ProviderOf(door));
        }
    }

    /// <summary>
    /// Codex's limit words and usage question are provider <c>codex</c>'s and its built door reads them through it (row d), on the list
    /// and on the registry over the built set; a harness naming <c>codex</c> is still lent neither, one naming <c>codex-acp</c> both.
    /// </summary>
    [Fact]
    public void Codex_s_limit_words_and_usage_question_are_the_agent_s_and_its_door_reads_them_through_it()
    {
        var door = new DoorId("codex-acp");
        foreach (var registry in new[] { new ProviderRegistry(Providers.Built(), []), ProviderRegistry.Built() })
        {
            var codex = registry.Provider("codex");
            Assert.Same(CodexLimits.Words, codex.Tables.Required.Limits);
            Assert.Same(UsageReads.Of(CodexUsage.Question), codex.Usage.Required.Read);
            Assert.Same(CodexLimits.Words, registry.LimitsOf(door));
            Assert.Same(UsageReads.Of(CodexUsage.Question), registry.UsageOf(door));
            Assert.Null(codex.Lent.Required.Tables.Limits);
            Assert.Null(codex.Lent.Required.Usage.Read);
            Assert.Same(CodexLimits.Words, registry.Door("codex-acp").Lent.Tables.Limits);
            Assert.Same(UsageReads.Of(CodexUsage.Question), registry.Door("codex-acp").Lent.Usage.Read);
            // The door's own words stand beside the provider's: the door says its limits as Codex does, and the lent words are the same.
            Assert.Same(CodexLimits.Words, registry.Door("codex-acp").Tables.Limits);
            Assert.Same(registry.Door("codex-acp").Tables.Limits, registry.Door("codex-acp").Lent.Tables.Limits);
        }
    }

    /// <summary>
    /// A door keyed by its provider's name declares no words of its own (the provider holds them), and a door of Claude Code's
    /// reads its provider's by its key, so neither carries a table beside the provider's.
    /// </summary>
    [Fact]
    public void A_door_keyed_by_its_provider_or_by_its_key_declares_no_account_words_of_its_own()
    {
        var registry = ProviderRegistry.Built();
        foreach (var name in new[] { "claude-code", "claude-code-acp", "dsh" })
        {
            var own = registry.Door(name).Tables.Own;
            Assert.Null(own.Limits);
            Assert.Null(own.Windows);
            Assert.Null(own.SignIn);
        }
    }

    /// <summary>A stand-in's door answers the readers as a door with no model in it: a plain capture, no resume, no dialect, no carriers.</summary>
    [Theory]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    public void A_stand_in_door_answers_the_readers_with_no_model_behind_it(string name)
    {
        var door = ProviderRegistry.Built().Door(name);
        var target = new SessionTarget("q-1", "title", "body", "asker", "repo", TestHomes.Root, "http://svc");
        var chat = SpawnRequests.Chat("repo", TestHomes.Root, "http://svc", null);
        IReadOnlyList<string> command = ["node", "agent.mjs", "--flag"];

        Assert.Equal(name, door.Id.Value);
        Assert.Equal(name == "stub" ? Transport.Native : Transport.Protocol, door.Transport);
        Assert.True(door.Can.Interactive);
        Assert.Equal(["node", "agent.mjs", "--flag"], [ProviderRegistry.Built().Start(door, target, command).FileName, .. ProviderRegistry.Built().Start(door, target, command).ArgumentList]);
        Assert.Equal("node", ProviderRegistry.Built().Chat(door, chat, command).FileName);
        Assert.Null(ProviderRegistry.Built().Resume(door, target, command, "conversation"));
        Assert.Null(door.StructuredOutput());
        Assert.Null(door.FrameInterrupt());
        Assert.Equal("a message", door.FrameMessage("a message"));
        Assert.Empty(door.Spawned().ToolsUpFront);
    }

    [Fact]
    public void Each_real_door_states_its_spawn_and_Claude_Code_s_doors_their_dialects()
    {
        var doors = Providers.Built().Where(provider => !provider.Identity.StandIn).SelectMany(provider => provider.Doors).ToList();

        Assert.All(doors, door => Assert.True(door.Spawn.IsKnown));
        Assert.Equal(
            ["claude-code:INativeDialect", "claude-code-acp:IProtocolDialect"],
            doors.Where(door => door.Dialect.IsKnown)
                .Select(door => $"{door.Id}:{(door.Dialect.Required is INativeDialect ? nameof(INativeDialect) : nameof(IProtocolDialect))}"));
    }

    /// <summary>A built-in door states no identity of its own: through the shim it reads its provider's, the protocol door's too.</summary>
    [Fact]
    public void A_built_in_door_reads_its_provider_s_identity()
    {
        var set = ProviderRegistry.Built();

        Assert.Equal(("Claude Code", "Anthropic", "CC"), Identity(set.AgentNamed("claude-code-acp")!));
        Assert.Equal(("Codex", "OpenAI", "Cx"), Identity(set.AgentNamed("codex-acp")!));
        Assert.Equal(("DeepSeek Harness", "DeepSeek", "DH"), Identity(set.AgentNamed("dsh")!));
        static (string?, string?, string?) Identity(AccountAgent toolchain) => (toolchain.Product, toolchain.Maker, toolchain.Mark);
    }

    [Fact]
    public void The_pipe_stand_in_is_read_by_the_plain_capture_and_the_protocol_one_keeps_its_stdin_open()
    {
        var target = new SessionTarget("q-1", "title", "body", "asker", "repo", TestHomes.Root, "http://svc");
        IReadOnlyList<string> command = ["node", "agent.mjs"];

        Assert.False(ProviderRegistry.Built().Start("stub", target, command).RedirectStandardInput);
        Assert.True(ProviderRegistry.Built().Start("acp-stub", target, command).RedirectStandardInput);
    }

    [Fact]
    public void The_stand_ins_state_their_spawn_and_stub_states_no_native_dialect()
    {
        var provider = Listed("stub");

        Assert.All(provider.Doors, door => Assert.True(door.Spawn.IsKnown));
        // stub maps no stream-json and is read by the plain capture; acp-stub's protocol dialects are row i's.
        Assert.All(provider.Doors, door => Assert.False(door.Dialect.IsKnown));
    }

    /// <summary>
    /// A protocol door's resume is eligibility to negotiate, never a resume known (the design §2.6, X): stated as the transport's
    /// default and negotiated at the open; the pipe stand-in resumes nothing.
    /// </summary>
    [Fact]
    public void A_protocol_door_s_resume_is_eligibility_and_the_pipe_stand_in_resumes_nothing()
    {
        var provider = Listed("stub");
        var protocol = provider.Doors.Single(door => door.Transport == Transport.Protocol).Abilities.Resumes;
        var native = provider.Doors.Single(door => door.Transport == Transport.Native).Abilities.Resumes;

        Assert.True(protocol.IsKnown);
        Assert.True(protocol.Required);
        Assert.Equal(Eligibility.Applies, protocol.Applies);
        Assert.True(native.IsKnown);
        Assert.False(native.Required);
        Assert.Equal(Known<bool>.Anywhere, native.Applies);
    }

    [Fact]
    public void Both_missing_command_messages_name_the_door_and_read_alike()
    {
        var shimmed = ProviderRegistry.Built();
        var target = new SessionTarget("q-1", "t", "b", "a", "repo", "/root", "http://x");
        var chat = SpawnRequests.Chat("repo", "/root", "http://x", null);

        foreach (var name in new[] { "stub", "acp-stub" })
        {
            var door = shimmed.Door(name);
            var expected = Assert.Throws<DriverException>(() => ProviderRegistry.Built().Start(door, target, null)).Message;

            Assert.Contains($"the {name} adapter needs a command", expected);
            Assert.Contains($"[\"node\", \"path/to/agent.mjs\", \"--door\", \"{(name == "stub" ? "native" : "protocol")}\"]", expected);
            Assert.Equal(expected, Assert.Throws<DriverException>(() => ProviderRegistry.Built().Chat(door, chat, null)).Message);
            Assert.Equal(expected, Assert.Throws<DriverException>(() => ProviderRegistry.Built().Start(door, target, [])).Message);
        }
    }

    [Fact]
    public void The_stand_in_flag_is_the_registration_s_and_names_every_door_of_a_stand_in()
    {
        Assert.True(Providers.IsStandIn("stub"));
        Assert.True(Providers.IsStandIn("acp-stub"));
        Assert.True(Providers.IsStandIn("ACP-STUB"));
        Assert.True(Providers.IsStandIn("alt-stub"));
        Assert.True(Providers.IsStandIn("alt-acp-stub"));
        Assert.False(Providers.IsStandIn("claude-code"));
        Assert.False(Providers.IsStandIn("codex"));
        Assert.False(Providers.IsStandIn("acme"));
        Assert.False(Providers.IsStandIn(""));
        // The registry over the built set reads the same flag for every provider it makes.
        var registry = ProviderRegistry.Built();
        Assert.Equal(["alt-stub", "stub"], registry.Providers.Where(provider => provider.Identity.StandIn).Select(provider => provider.Id.Value));
    }

    /// <summary>The ports a provider written on the contract alone needs are declared, with the members row b names.</summary>
    [Fact]
    public void The_ports_are_declared()
    {
        Assert.True(typeof(ISpawn).IsInterface);
        Assert.True(typeof(INativeDialect).IsInterface && typeof(IDialect).IsAssignableFrom(typeof(INativeDialect)));
        Assert.True(typeof(IProtocolDialect).IsInterface && typeof(IDialect).IsAssignableFrom(typeof(IProtocolDialect)));
        Assert.True(typeof(IUsageRead).IsInterface && typeof(IUsageQuestion).IsAssignableFrom(typeof(IUsageRead))
                    && typeof(IUsageRead).IsAssignableFrom(typeof(UsageQuestion)));
        Assert.True(typeof(IChildVocabulary).IsInterface);
        Assert.True(typeof(IConversationHome).IsInterface);
        Assert.True(typeof(AgentRun).IsAbstract);
        Assert.Equal(typeof(Known<ISpawn>), typeof(AgentDoor).GetProperty("Spawn")!.PropertyType);
        Assert.Equal(typeof(Known<IDialect>), typeof(AgentDoor).GetProperty("Dialect")!.PropertyType);
        Assert.Equal(typeof(Known<IChildVocabulary>), typeof(Provider).GetProperty("Children")!.PropertyType);
        Assert.Equal(typeof(Known<IConversationHome>), typeof(Provider).GetProperty("Conversations")!.PropertyType);
        Assert.Equal(typeof(IUsageRead), typeof(UsagePart).GetProperty("Read")!.PropertyType);
    }

    /// <summary>Each provider that has children says them: Claude Code its hook, stream and calls with an end, Codex its thread calls and no end.</summary>
    [Fact]
    public void The_providers_that_have_children_declare_their_vocabulary()
    {
        var claude = Listed("claude-code").Children.Required;
        Assert.True(claude.SaysEnd);
        Assert.Contains("stream", claude.StreamSignals);
        Assert.Contains("hook", claude.HookLines);

        var codex = Listed("codex").Children.Required;
        Assert.False(codex.SaysEnd);
        Assert.Empty(codex.HookLines);
        Assert.Contains("thread", codex.CallKeys);

        Assert.All(["dsh", "stub", "alt-stub"], id => Assert.False(Listed(id).Children.IsKnown));
    }

    /// <summary>Process starts compared by what a spawn would do: program, arguments, folder, redirections and variables.</summary>
    internal static string Shape(ProcessStartInfo info) =>
        $"{info.FileName} [{string.Join('|', info.ArgumentList)}] in {info.WorkingDirectory} in={info.RedirectStandardInput} out={info.RedirectStandardOutput} "
        + string.Join(';', info.Environment.Where(pair => pair.Key.StartsWith("DAORIS_", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
}
