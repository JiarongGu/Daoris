using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row b, part 1 (D213's row b note; the agent-provider design §11 row b): the one registration list holds the two
/// stand-ins written on the contract, and what they state is held against the classes they stand beside until row t, field by field
/// (old against new, X). The ports a provider written on the contract alone needs are declared; the stand-ins state the ones they use.
/// </summary>
public sealed class ProvidersBuiltTests
{
    /// <summary>Today's two stand-in classes as a set, the old side of the comparison.</summary>
    internal static AdapterSet Old() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        ["stub"] = new StubAdapter(),
        ["acp-stub"] = new AcpStubAdapter(),
    });

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
    public void The_registration_list_holds_the_stand_ins_as_one_provider_with_a_door_on_each_transport()
    {
        var provider = Assert.Single(Providers.Built());

        Assert.Equal(new ProviderId("stub"), provider.Id);
        Assert.True(provider.Identity.StandIn);
        Assert.Equal([new DoorId("acp-stub"), new DoorId("stub")], provider.Doors.Select(door => door.Id));
        Assert.Equal(Transport.Native, provider.Doors.Single(door => door.Id.Value == "stub").Transport);
        Assert.Equal(Transport.Protocol, provider.Doors.Single(door => door.Id.Value == "acp-stub").Transport);
        Assert.Equal(Reach.Everywhere, provider.Reach);
        Assert.False(provider.Unavailable);
    }

    /// <summary>Old against new: what the registry reads from the two classes is what the contract's stand-in states, every part.</summary>
    [Fact]
    public void The_stand_ins_on_the_contract_state_what_the_classes_state()
    {
        var old = ProviderRegistry.Of(Old()).Provider("stub");
        var contract = Providers.Built().Single();

        Assert.Empty(Compared(old, contract));
    }

    /// <summary>The registry over the built set, whose stand-ins arrive through the shim, equals the one over the registration list.</summary>
    [Fact]
    public void The_registry_over_the_built_set_equals_the_one_over_the_registration_list_for_the_stand_ins()
    {
        var built = ProviderRegistry.Of(AdapterSet.Built());
        var listed = new ProviderRegistry(Providers.Built(), []);

        Assert.Empty(Compared(built.Provider("stub"), listed.Provider("stub")));
        foreach (var door in listed.Doors)
        {
            Assert.Empty(Compared(built.Door(door), listed.Door(door)));
            Assert.Equal(listed.OwnerOf(door), built.OwnerOf(door));
            Assert.Equal(listed.ProviderOf(door), built.ProviderOf(door));
        }
    }

    [Fact]
    public void The_stand_ins_state_their_spawn_and_stub_states_no_native_dialect()
    {
        var provider = Providers.Built().Single();

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
        var provider = Providers.Built().Single();
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
        Assert.False(Providers.IsStandIn("claude-code"));
        Assert.False(Providers.IsStandIn("codex"));
        Assert.False(Providers.IsStandIn("acme"));
        Assert.False(Providers.IsStandIn(""));
        // The registry over the built set reads the same flag for every provider it makes.
        var registry = ProviderRegistry.Of(AdapterSet.Built());
        Assert.Equal(["stub"], registry.Providers.Where(provider => provider.Identity.StandIn).Select(provider => provider.Id.Value));
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
