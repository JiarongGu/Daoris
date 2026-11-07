using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Daoris.Driver;

// The ports a provider written on the contract alone needs. A part states its port or is stated absent; each port's default is the
// behaviour for an agent that says nothing.

/// <summary>
/// A door's spawn: a start's, a conversation's and a native resume's <see cref="ProcessRecipe"/>, which the
/// transports' one shell (<see cref="Shell"/>) completes, and the carriers it hands its own process once the shell has.
/// </summary>
public interface ISpawn
{
    /// <summary>The recipe of a driven session: preparation only, the driver owns its lifetime.</summary>
    ProcessRecipe Start(SpawnRequest request);

    /// <summary>The recipe of a conversation, a new one or the one <see cref="SpawnRequest.Resume"/> names; the shell opens its stdin.</summary>
    ProcessRecipe Chat(SpawnRequest request);

    /// <summary>
    /// The native door's resume of the conversation <see cref="SpawnRequest.Resume"/> names, its next turn written on the input by the
    /// driver; null where the door knows none.
    /// </summary>
    ProcessRecipe? Resume(SpawnRequest request) => null;

    /// <summary>The variables that load a session's tools with its first request; empty where there is no such switch.</summary>
    IReadOnlyDictionary<string, string> ToolsUpFront => ReadOnlyDictionary<string, string>.Empty;

    /// <summary>
    /// Whether its program runs Claude Code's, so its spawn is pointed at the <c>claude</c> Claude Code's pin chose and takes that pin's
    /// switch (<see cref="ClaudeAcp"/>). False by default: Codex's and dsh's doors read neither variable.
    /// </summary>
    bool ClaudeSeam => false;

    /// <summary>
    /// Points this spawn at the managed <c>claude</c> a start on a door that takes the seam is handed (<see cref="ClaudeSeam"/>), for the
    /// shell to apply with the rest of the account's half; nothing by default.
    /// </summary>
    void PointAtClaude(ProcessStartInfo info, string managedClaude) { }

    /// <summary>
    /// The variables its program reads for the model and the effort it runs with, which a start's environment may carry beside the command's
    /// flags; none by default. What a start's asked choice reads from its spawn is the command's flags and these.
    /// </summary>
    SpawnChoiceVariables ChoiceVariables => SpawnChoiceVariables.None;

    /// <summary>Hands a pipe-door spawn the servers Daoris offers, as the agent's own flag; nothing by default.</summary>
    void HandServers(ProcessStartInfo info, string configFile) { }

    /// <summary>Hands a pipe-door spawn its rules file; nothing by default.</summary>
    void HandSettings(ProcessStartInfo info, string settingsFile) { }

    /// <summary>Hands a pipe-door spawn the harness plugins composed for it; nothing by default.</summary>
    void HandPlugins(ProcessStartInfo info, IReadOnlyList<string> plugins) { }
}

/// <summary>What a door reads of its program's wire beyond the transport's: a native door's lines, or a protocol door's frames.</summary>
public interface IDialect;

/// <summary>
/// A native door's dialect: a reader of its program's structured stdout, new per session, and how a person's message
/// and an interrupt are framed on its stdin. Absent, a door is read as plain lines and an end, nothing more, which is every pipe door
/// until its wire is checked.
/// </summary>
public interface INativeDialect : IDialect
{
    /// <summary>A reader of this program's own structured stdout, new per session (today's <see cref="IStreamMapper"/>).</summary>
    IStreamMapper Mapper();

    /// <summary>A person's message as the program reads it on stdin.</summary>
    string FrameMessage(string text) => text;

    /// <summary>The line that stops a turn and keeps the session; null where the program has none.</summary>
    string? FrameInterrupt() => null;
}

/// <summary>
/// A protocol door's dialect: what the shared transport hands the agent's own readings.
/// </summary>
public interface IProtocolDialect : IDialect
{
    /// <summary>What <c>session/new</c> carries to take the rules file and the plugins composed for the session; null sends no <c>_meta</c>.</summary>
    object? SessionMeta(string settingsFile, IReadOnlyList<string> plugins) => null;
}

/// <summary>How an account's windows are asked between turns: the program, the command, the answers recorded and the freshness.</summary>
public interface IUsageQuestion
{
    /// <summary>The harness whose binary the command names.</summary>
    string Harness { get; }

    /// <summary>The binary and arguments that start the question.</summary>
    IReadOnlyList<string> Command { get; }

    /// <summary>The answers the reader is written against.</summary>
    IReadOnlyList<RecordedFrame> Recorded { get; }

    /// <summary>The question's name as the usage history writes a reading's source.</summary>
    string Source { get; }

    /// <summary>How long a reading stands before an account is asked again.</summary>
    TimeSpan Fresh { get; }
}

/// <summary>How an agent's lineage names a child: its hook's lines, its calls' <c>_meta</c> keys, its stream's signals.</summary>
public interface IChildVocabulary
{
    IReadOnlyList<string> HookLines { get; }

    IReadOnlyList<string> CallKeys { get; }

    IReadOnlyList<string> StreamSignals { get; }

    /// <summary>Whether the agent says a child's end; where it does not, an end stays unknown.</summary>
    bool SaysEnd { get; }

    /// <summary>A reader of one record's lines in this agent's words, new per reading.</summary>
    IChildLines Lines();

    /// <summary>
    /// What a line of this agent's appended after a reading names for whether it moves it: the child a call came from, a call from an id
    /// named with no parent (<see cref="ChildCallKind.Root"/>), or none that can; null where the line is no call, and any line moves it.
    /// </summary>
    ChildCall? Wake(System.Text.Json.Nodes.JsonObject line);
}

/// <summary>
/// An agent's settings file for an account's default model and effort: the model names its program takes as aliases
/// and the efforts a default in the file may hold, which are not what a session may carry (<see cref="DoorOffers"/>' two scopes).
/// </summary>
public interface ISettingsFile
{
    /// <summary>The aliases the program accepts, offered before a free field for a full model id.</summary>
    IReadOnlyList<string> Models { get; }

    /// <summary>The efforts a default may hold; one a session alone may carry is not among them.</summary>
    IReadOnlyList<string> Efforts { get; }

    /// <summary>The efforts only a session on the program's native door may carry, never a file's default (<c>max</c> for Claude Code).</summary>
    IReadOnlyList<string> SessionOnly { get; }
}

/// <summary>
/// What a session on one door may carry for a model and an effort, the session scope of <see cref="DoorOffers"/>: the
/// provider's settings file where it states one, extended by the efforts only a native session carries on a door that says so, and a door that
/// states nothing carries what its open last listed on any account.
/// </summary>
public interface IDoorOffers
{
    /// <summary>The choices a session on this door offers, or null where nothing states or has listed any.</summary>
    ChoicesOffered? Of(string home, string door, ISettingsFile? provider);
}

/// <summary>What a conversation keeps under an account's home, by the agent's version.</summary>
public interface IConversationHome
{
    /// <summary>The paths under an account's home a conversation keeps, for this version of the agent (null: any).</summary>
    IReadOnlyList<string> Keeps(string? version);

    /// <summary>
    /// Where the agent files a session by the working directory it ran in, under the person's own home for it (a folder named by the
    /// directory, as <see cref="ProjectFolders"/> says); null for an agent that files its sessions another way (Codex, by date).
    /// </summary>
    ProjectFolders? Projects => null;
}

/// <summary>
/// An agent's own home laid out by project folders: the variable naming it, its place where none is set, the folder the projects are in, and
/// how a working directory becomes a folder's name. What the workspace's wiring page lists as Daoris's leftovers is read through it.
/// </summary>
/// <param name="Variable">The environment variable naming the person's own home for the agent.</param>
/// <param name="DefaultFolder">The home's folder under the user profile where no variable names one.</param>
/// <param name="Folder">The folder of the home the projects are filed in.</param>
/// <param name="Flatten">A working directory's name, as the agent names the folder it files that directory's sessions in.</param>
public sealed record ProjectFolders(string Variable, string DefaultFolder, string Folder, Func<string, string> Flatten);

/// <summary>
/// How a provider takes the product's rules for one session: its own settings file, its hooks among them, and the
/// hard denials its classifier is told. The rules stay the product's one grammar; a provider that takes none states this absent, and the
/// person's denies reach none of its sessions.
/// </summary>
public interface IRuleLanguage
{
    /// <summary>Write the session's file under the home and answer its path, which the door hands by its own way.</summary>
    string Write(string home, string sessionId, HandedRules rules);
}

/// <summary>
/// How a door is handed a start's connectors at its spawn: by a file in its provider's own layout. A door whose
/// servers ride its wire, or that takes none at its spawn, states this absent, and no file is written for it.
/// </summary>
public interface IServerHanding
{
    /// <summary>Hand <paramref name="plan"/>'s servers to the process; the file written, which goes with the session, or null for none.</summary>
    string? Hand(ProcessStartInfo info, string home, ConnectorPlan plan);
}

/// <summary>How a provider lays out the skills a start is handed, from plain values Loop composes.</summary>
public interface ISkillLayout
{
    /// <summary>The session's folder under the home and each plugin in it, in the order given; a null folder and none where nothing is handed.</summary>
    (string? Folder, IReadOnlyList<string> Plugins) Write(string home, string sessionId, IReadOnlyList<SkillSource> sources);
}
