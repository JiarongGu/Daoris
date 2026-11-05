using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 rows b and d (the agent-provider design §11): the one registration list holds the stand-ins, Claude Code, Codex and
/// dsh written on the contract, and what each states is held against the classes it stands beside until row t, field by field (old
/// against new, X). The ports a provider written on the contract alone needs are declared; each provider states the ones it uses.
/// </summary>
public sealed class ProvidersBuiltTests
{
    /// <summary>Today's adapter classes and Codex's holder as a set, the old side of the comparison.</summary>
    internal static AdapterSet Old() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        ["stub"] = new StubAdapter(),
        ["acp-stub"] = new AcpStubAdapter(),
        ["claude-code"] = new ClaudeCodeAdapter(),
        ["claude-code-acp"] = new ClaudeAcpAdapter(),
        ["codex-acp"] = new CodexAcpAdapter(),
        ["dsh"] = new DshAdapter(),
    }, new Dictionary<string, HarnessToolchain>(StringComparer.OrdinalIgnoreCase) { [CodexAccounts.Agent] = CodexAccounts.Toolchain });

    private static Provider Listed(string id) => Providers.Built().Single(provider => provider.Id.Value == id);

    public static TheoryData<string> Ids => ["claude-code", "codex", "dsh", "stub"];

    public static TheoryData<string> RealDoors => ["claude-code", "claude-code-acp", "codex-acp", "dsh"];

    private static readonly Type[] Ports = [typeof(ISpawn), typeof(IDialect), typeof(IChildVocabulary), typeof(IConversationHome)];

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
            if (Ports.Any(port => port.IsAssignableFrom(property.PropertyType))) continue;
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

    /// <summary>Old against new: what the registry reads from the classes is what each provider on the contract states, every part.</summary>
    [Theory]
    [MemberData(nameof(Ids))]
    public void Each_provider_on_the_contract_states_what_the_classes_state(string id)
    {
        var old = ProviderRegistry.Of(Old()).Provider(id);

        Assert.Empty(Compared(old, Listed(id)));
    }

    /// <summary>The registry over the built set, whose doors all arrive through the shim, equals the one over the registration list.</summary>
    [Fact]
    public void The_registry_over_the_built_set_equals_the_one_over_the_registration_list()
    {
        var built = ProviderRegistry.Of(AdapterSet.Built());
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
        foreach (var registry in new[] { new ProviderRegistry(Providers.Built(), []), ProviderRegistry.Of(AdapterSet.Built()) })
        {
            var codex = registry.Provider("codex");
            Assert.Same(CodexLimits.Words, codex.Tables.Required.Limits);
            Assert.Same(CodexUsage.Question, codex.Usage.Required.Question);
            Assert.Same(CodexLimits.Words, registry.LimitsOf(door));
            Assert.Same(CodexUsage.Question, registry.UsageOf(door));
            Assert.Null(codex.Lent.Required.Tables.Limits);
            Assert.Null(codex.Lent.Required.Usage.Question);
            Assert.Same(CodexLimits.Words, registry.Door("codex-acp").Lent.Tables.Limits);
            Assert.Same(CodexUsage.Question, registry.Door("codex-acp").Lent.Usage.Question);
        }
    }

    /// <summary>Old against new through the shim: every member the readers ask of an adapter answers as the class answered it.</summary>
    [Theory]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    public void The_shim_presents_a_stand_in_as_the_class_does(string name)
    {
        ISessionAdapter old = Old().Resolve(name);
        var door = AdapterSet.Built().Resolve(name);
        var target = new SessionTarget("q-1", "title", "body", "asker", "repo", Path.GetTempPath(), "http://svc");
        var chat = new ChatTarget("repo", Path.GetTempPath(), "http://svc");
        IReadOnlyList<string> command = ["node", "agent.mjs", "--flag"];

        Assert.IsType<LegacyDoor>(door);
        Assert.Equal(old.Name, door.Name);
        Assert.Equal(old.Wire, door.Wire);
        Assert.Equal(old.Interactive, door.Interactive);
        Assert.Equal(old.Resumes, door.Resumes);
        Assert.Equal(old.TakesSettings, door.TakesSettings);
        Assert.Equal((old.AcpPosture, old.AskingPosture, old.FullPosture), (door.AcpPosture, door.AskingPosture, door.FullPosture));
        Assert.Empty(Compared(old.Toolchain, door.Toolchain));
        Assert.Equal(Shape(old.Prepare(target, command)), Shape(door.Prepare(target, command)));
        Assert.Equal(Shape(old.PrepareChat(chat, command)), Shape(door.PrepareChat(chat, command)));
        Assert.Null(door.PrepareResume(target, command, "conversation"));
        Assert.Null(door.StructuredOutput());
        Assert.Null(door.FrameInterrupt());
        Assert.Equal("a message", door.FrameMessage("a message"));
        Assert.Empty(door.ToolsUpFront);
    }

    /// <summary>
    /// Old against new through the shim, for every real door: each member the readers ask of an adapter, the spawn with its binary and
    /// with a configured command, the resume, the carriers, the stream reader, the frames, the session's <c>_meta</c> and what the
    /// program reads of a tree, answers as the class answered it.
    /// </summary>
    [Theory]
    [MemberData(nameof(RealDoors))]
    public void The_shim_presents_a_real_door_as_its_class_does(string name)
    {
        ISessionAdapter old = Old().Resolve(name);
        var door = AdapterSet.Built().Resolve(name);
        var target = new SessionTarget("q-1", "title", "body", "asker", "repo", Path.GetTempPath(), "http://svc");
        var chat = new ChatTarget("repo", Path.GetTempPath(), "http://svc");
        IReadOnlyList<string> command = ["agent", "--flag"];
        IReadOnlyList<string> plugins = ["plugins/one"];

        Assert.IsType<LegacyDoor>(door);
        Assert.Equal(old.Name, door.Name);
        Assert.Equal(old.Wire, door.Wire);
        Assert.Equal(old.Interactive, door.Interactive);
        Assert.Equal(old.Resumes, door.Resumes);
        Assert.Equal(old.TakesSettings, door.TakesSettings);
        Assert.Equal(
            (old.AcpPosture, old.AskingPosture, old.FullPosture, old.ReadingPosture),
            (door.AcpPosture, door.AskingPosture, door.FullPosture, door.ReadingPosture));
        Assert.Equal((old.LoadsTreeSkills, old.InstructionFileLimit), (door.LoadsTreeSkills, door.InstructionFileLimit));
        Assert.Empty(Compared(old.Toolchain, door.Toolchain));
        Assert.Equal(Shape(old.Prepare(target, null)), Shape(door.Prepare(target, null)));
        Assert.Equal(Shape(old.Prepare(target, command)), Shape(door.Prepare(target, command)));
        Assert.Equal(old.PrepareResume(target, command, "c-1") is { } resumed ? Shape(resumed) : null,
            door.PrepareResume(target, command, "c-1") is { } shimmed ? Shape(shimmed) : null);
        if (old.Interactive) Assert.Equal(Shape(old.PrepareChat(chat, command)), Shape(door.PrepareChat(chat, command)));
        else Assert.Equal(Assert.Throws<DriverException>(() => old.PrepareChat(chat, command)).Message, Assert.Throws<DriverException>(() => door.PrepareChat(chat, command)).Message);
        Assert.Equal(old.ToolsUpFront, door.ToolsUpFront);
        Assert.Equal(Carried(old, plugins), Carried(door, plugins));
        Assert.Equal(old.StructuredOutput()?.GetType(), door.StructuredOutput()?.GetType());
        Assert.Equal(old.FrameMessage("a message"), door.FrameMessage("a message"));
        Assert.Equal(old.FrameInterrupt() is null, door.FrameInterrupt() is null);
        Assert.Equal(JsonSerializer.Serialize(old.AcpSessionMeta("settings.json")), JsonSerializer.Serialize(door.AcpSessionMeta("settings.json")));
        Assert.Equal(
            JsonSerializer.Serialize(old.AcpSessionMeta("settings.json", plugins)), JsonSerializer.Serialize(door.AcpSessionMeta("settings.json", plugins)));
    }

    /// <summary>What a door's carriers add to a spawn: its servers, its rules file and its plugins, in order.</summary>
    private static string Carried(ISessionAdapter adapter, IReadOnlyList<string> plugins)
    {
        var info = new ProcessStartInfo("agent");
        adapter.HandServers(info, "servers.json");
        adapter.HandSettings(info, "settings.json");
        adapter.HandPlugins(info, plugins);
        return string.Join('|', info.ArgumentList);
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
        var set = AdapterSet.Built();

        Assert.Equal(("Claude Code", "Anthropic", "CC"), Identity(set.Resolve("claude-code-acp").Toolchain!));
        Assert.Equal(("Codex", "OpenAI", "Cx"), Identity(set.Resolve("codex-acp").Toolchain!));
        Assert.Equal(("DeepSeek Harness", "DeepSeek", "DH"), Identity(set.Resolve("dsh").Toolchain!));
        static (string?, string?, string?) Identity(HarnessToolchain toolchain) => (toolchain.Product, toolchain.Maker, toolchain.Mark);
    }

    [Fact]
    public void The_pipe_stand_in_is_read_by_the_plain_capture_and_the_protocol_one_keeps_its_stdin_open()
    {
        var target = new SessionTarget("q-1", "title", "body", "asker", "repo", Path.GetTempPath(), "http://svc");
        IReadOnlyList<string> command = ["node", "agent.mjs"];

        Assert.False(AdapterSet.Built().Resolve("stub").Prepare(target, command).RedirectStandardInput);
        Assert.True(AdapterSet.Built().Resolve("acp-stub").Prepare(target, command).RedirectStandardInput);
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
    public void Both_missing_command_messages_read_as_the_classes_read()
    {
        var shimmed = AdapterSet.Built();
        var target = new SessionTarget("q-1", "t", "b", "a", "repo", "/root", "http://x");
        var chat = new ChatTarget("repo", "/root", "http://x");

        foreach (var name in new[] { "stub", "acp-stub" })
        {
            ISessionAdapter old = Old().Resolve(name);
            var door = shimmed.Resolve(name);
            var expected = Assert.Throws<DriverException>(() => old.Prepare(target, null)).Message;

            Assert.Equal(expected, Assert.Throws<DriverException>(() => door.Prepare(target, null)).Message);
            Assert.Equal(expected, Assert.Throws<DriverException>(() => door.PrepareChat(chat, null)).Message);
            Assert.Equal(expected, Assert.Throws<DriverException>(() => door.Prepare(target, [])).Message);
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
        var registry = ProviderRegistry.Of(AdapterSet.Built());
        Assert.Equal(["stub", "alt-stub"], registry.Providers.Where(provider => provider.Identity.StandIn).Select(provider => provider.Id.Value));
    }

    /// <summary>The ports a provider written on the contract alone needs are declared, with the members row b names.</summary>
    [Fact]
    public void The_ports_are_declared()
    {
        Assert.True(typeof(ISpawn).IsInterface);
        Assert.True(typeof(INativeDialect).IsInterface && typeof(IDialect).IsAssignableFrom(typeof(INativeDialect)));
        Assert.True(typeof(IProtocolDialect).IsInterface && typeof(IDialect).IsAssignableFrom(typeof(IProtocolDialect)));
        Assert.True(typeof(IUsageQuestion).IsInterface && typeof(IUsageQuestion).IsAssignableFrom(typeof(UsageQuestion)));
        Assert.True(typeof(IChildVocabulary).IsInterface);
        Assert.True(typeof(IConversationHome).IsInterface);
        Assert.True(typeof(AgentRun).IsAbstract);
        Assert.Equal(typeof(Known<ISpawn>), typeof(AgentDoor).GetProperty("Spawn")!.PropertyType);
        Assert.Equal(typeof(Known<IDialect>), typeof(AgentDoor).GetProperty("Dialect")!.PropertyType);
        Assert.Equal(typeof(Known<IChildVocabulary>), typeof(Provider).GetProperty("Children")!.PropertyType);
        Assert.Equal(typeof(Known<IConversationHome>), typeof(Provider).GetProperty("Conversations")!.PropertyType);
        Assert.Equal(typeof(IUsageQuestion), typeof(UsagePart).GetProperty("Question")!.PropertyType);
    }

    /// <summary>Process starts compared by what a spawn would do: program, arguments, folder, redirections and variables.</summary>
    internal static string Shape(ProcessStartInfo info) =>
        $"{info.FileName} [{string.Join('|', info.ArgumentList)}] in {info.WorkingDirectory} in={info.RedirectStandardInput} out={info.RedirectStandardOutput} "
        + string.Join(';', info.Environment.Where(pair => pair.Key.StartsWith("DAORIS_", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
}
