using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a reader asks of a registered door beyond its record: the abilities it can state, the spawn and the
/// dialect its parts hold, and, through the registry, what its provider adds: the variables a start strips, the rule language, the
/// skill layout and the maker. A door with no spawn declared is refused by name, never run.
/// </summary>
public static class DoorPorts
{
    extension(AgentDoor door)
    {
        /// <summary>What the door can do (<see cref="DoorAbilities"/>), a stated ability or its transport's default.</summary>
        public DoorAbilities Can => DoorAbilities.Of(door);

        /// <summary>The door's spawn, refused by name where it declares none.</summary>
        public ISpawn Spawned() =>
            door.Spawn.IsKnown ? door.Spawn.Required : throw new DriverException($"the `{door.Id.Value}` door declares no spawn.");

        /// <summary>The reader of the door's structured stdout on the native door, or null where it states no native dialect.</summary>
        public IStreamMapper? StructuredOutput() => Native(door)?.Mapper();

        /// <summary>A person's message as the door's harness reads it on a conversation's stdin.</summary>
        public string FrameMessage(string text) => Native(door)?.FrameMessage(text) ?? text;

        /// <summary>The line that stops a conversation's turn on the door's stdin, null where it has none.</summary>
        public string? FrameInterrupt() => Native(door)?.FrameInterrupt();

        public string? FrameSteer(string text, string uuid) => Native(door)?.FrameSteer(text, uuid);

        /// <summary>What a protocol-door session carries on <c>session/new</c> to take its rules file, plugins and own tool home, null for none.</summary>
        public object? AcpSessionMeta(string settingsFile, IReadOnlyList<string>? plugins = null, OwnTools? own = null) =>
            door.Dialect.IsKnown ? (door.Dialect.Required as IProtocolDialect)?.SessionMeta(settingsFile, plugins ?? [], OwnFor(door, own)) : null;

        /// <summary><see cref="AcpSessionMeta"/> for a session held to the person's tier, its handed file and Daoris's servers alone.</summary>
        public object? AcpSourcesAloneMeta(string settingsFile, IReadOnlyList<string>? plugins = null, OwnTools? own = null) =>
            door.Dialect.IsKnown ? (door.Dialect.Required as IProtocolDialect)?.SourcesAloneMeta(settingsFile, plugins ?? [], OwnFor(door, own)) : null;

        /// <summary>How the door takes a start's servers at its spawn, or null where they ride its wire or it takes none there.</summary>
        public IServerHanding? ServerHanding() => door.Spawn.TryGet(out var spawn) ? spawn as IServerHanding : null;

        /// <summary>A door a plugin declared: its identity, home and program its own, and none of its provider's.</summary>
        public bool Declared => door.Identity.DeclaredBy is not null;
    }

    extension(ProviderRegistry registry)
    {
        /// <summary>The provider <paramref name="door"/> belongs to.</summary>
        public Provider ProviderOfDoor(AgentDoor door) => registry.Provider(registry.ProviderOf(door.Id));

        /// <summary>A driven start's process, stripped of every other registered provider's variable but for a declared door's.</summary>
        public ProcessStartInfo Start(AgentDoor door, SpawnRequest request) => DoorStarts.Started(door.Spawned(), request, registry.Stripped(door));

        /// <summary>A conversation's process, refused where the door does not hold one.</summary>
        public ProcessStartInfo Chat(AgentDoor door, SpawnRequest request) =>
            door.Can.Interactive
                ? DoorStarts.Chatted(door.Spawned(), request, registry.Stripped(door))
                : throw new DriverException(
                    $"the `{door.Id.Value}` adapter cannot hold a conversation — it spawns an agent that takes its "
                    + "target once and runs to completion. Chat with an adapter that declares `interactive`.");

        /// <summary>The native door's resume, null where the door knows none.</summary>
        public ProcessStartInfo? Resume(AgentDoor door, SpawnRequest request) => DoorStarts.Resumed(door.Spawned(), request, registry.Stripped(door));

        /// <summary>The maker a door's registration states: a plugin's door its own, any other its provider's (<see cref="CommitTrailers"/>).</summary>
        public string? MakerOf(AgentDoor door) => door.Declared ? door.Identity.Maker : registry.ProviderOfDoor(door).Identity.Maker;

        /// <summary>
        /// The language the door's rules are written in, or null where the person's rules reach none of its sessions: a plugin's door,
        /// which takes no settings file, none, whichever provider it joined.
        /// </summary>
        public IRuleLanguage? RuleLanguage(AgentDoor door) =>
            !door.Declared && registry.ProviderOfDoor(door).Rules.TryGet(out var language) ? language : null;

        /// <summary>The layout the door's skills are copied in, or null where it is handed none: a plugin's door takes Claude Code's.</summary>
        public ISkillLayout? SkillLayout(AgentDoor door) =>
            door.Declared ? ClaudeSkills.Layout : registry.ProviderOfDoor(door).Skills.TryGet(out var layout) ? layout : null;

        /// <summary>The abilities of the door named <paramref name="name"/>, null where none is.</summary>
        public DoorAbilities? AbilitiesOf(string name) => registry.Doors.Any(id => Ids.Same(id.Value, name)) ? registry.Door(name).Can : null;

        private IReadOnlyCollection<string>? Stripped(AgentDoor door) =>
            door.Declared ? null : RegisteredVariables.Others(registry.ProviderOfDoor(door));
    }

    private static INativeDialect? Native(AgentDoor door) => door.Dialect.IsKnown ? door.Dialect.Required as INativeDialect : null;

    private static OwnTools? OwnFor(AgentDoor door, OwnTools? own) => door.Declared ? null : own;
}
