using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The role a step names: the position that takes it, never who fills it. A step names one by its
/// declaration's <see cref="NodeDeclaration.Role"/>; one that names none reads its kind's default (<see cref="INodeKind.Role"/>), which is read and
/// never written, so a version saved before the field keeps its digest. A role is one of <see cref="All"/>, or a role of the home's catalogue
/// (a working role that staffs an agent step), which the save reads where a step names one (<see cref="WorkflowOperations.RoleProblems"/>).
/// </summary>
public static class StepRoles
{
    /// <summary>The working role that does the work, the work step's (a seeded catalogue role: build and design work).</summary>
    public const string Design = "design";

    /// <summary>The second opinion, another family's read of the work (the seeded catalogue role).</summary>
    public const string Opinion = "opinion";

    /// <summary>Who tries the work hands-on before it lands: the look.</summary>
    public const string Tester = "tester";

    /// <summary>Who decides on the work at a revision: the Accept.</summary>
    public const string Acceptor = "acceptor";

    /// <summary>Who reads an unchecked done at <c>quest.holds</c>, cast like any role; never the repository a quest comes from.</summary>
    public const string AskReader = "ask-reader";

    /// <summary>Who says yes to one act outside a repository: a go-ahead.</summary>
    public const string Approver = "approver";

    /// <summary>Who names a release's target and moves it through its stages.</summary>
    public const string ReleaseManager = "release-manager";

    /// <summary>Who carries out the run's own acts and observations: git's, a plugin's, Daoris's.</summary>
    public const string Operations = "operations";

    /// <summary>Who changes a live configuration after a release.</summary>
    public const string ConfigChanger = "config-changer";

    /// <summary>Who keeps the keys and sign-ins a plugin acts with.</summary>
    public const string CredentialsKeeper = "credentials-keeper";

    /// <summary>The role at the top of every reporting line; a person's task names it until the task names its own.</summary>
    public const string Owner = "owner";

    /// <summary>Every role a step may name without the catalogue, in the editor's order: the census the window words, in both catalogues.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Design, Opinion, Tester, Acceptor, AskReader, Approver, ReleaseManager, Operations, ConfigChanger, CredentialsKeeper, Owner];

    /// <summary>A role's id: lowercase words of letters and digits joined by dashes, at most 40, as a catalogue role's id is.</summary>
    public static readonly System.Text.RegularExpressions.Regex Shape =
        new(@"^(?=.{1,40}\z)[a-z0-9]+(?:-[a-z0-9]+)*\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Words that name a holder, never a role, a person or an agent alike, so the refusal says the same of either; <c>human</c> stays
    /// though no kind has it, so a role named for the old kind is refused. The agents' ids are their providers' and the model families'.</summary>
    public static IReadOnlyList<string> Holders { get; } =
    [
        "you", "me", "i", "person", "human", "user", "someone", "anyone", "nobody", "agent", "daoris",
        "claude", "claude-code", "codex", "dsh", "deepseek", "opus", "sonnet", "haiku", "gpt", "gemini",
    ];

    /// <summary>A step that names a holder where its role goes.</summary>
    public const string HolderRefused = "role-holder";

    /// <summary>A step whose role is no role's id: not one of <see cref="All"/>, nor a role of the home's catalogue.</summary>
    public const string Unknown = "role-unknown";

    /// <summary>The role <paramref name="node"/> names, or its kind's default where it names none; an older version's agent step read by its option.</summary>
    public static string Of(NodeDeclaration node) => Named(node) ?? Default(node.Kind);

    /// <summary>The role a step of <paramref name="kind"/> that names none reads: its kind's, and a plugin's step <see cref="Operations"/>.</summary>
    public static string Default(string kind) => NodeKit.Of(kind)?.Role ?? Operations;

    /// <summary>
    /// <paramref name="graph"/> as a save writes it: no step names its kind's default role where naming none reads the same, so a version
    /// holding only defaults is the version a build before the field reads, and closes no going back (<see cref="WorkflowRolesSince"/>). An
    /// agent step's role is kept as named, since there naming none takes the role the place's catalogue gives its work, not the default.
    /// Changed in place, and returned.
    /// </summary>
    public static JsonNode? Unwritten(JsonNode? graph)
    {
        if (graph?["nodes"] is not JsonArray nodes) return graph;
        foreach (var node in nodes.OfType<JsonObject>())
        {
            if (node["kind"] is JsonValue kind && kind.TryGetValue<string>(out var named) && node["role"] is JsonValue role
                && role.TryGetValue<string>(out var written) && written == Default(named) && NodeKit.Of(named)?.RoleTakes is not [_, ..]
                && node["options"]?[NodeOptionFields.Role] is null)
            {
                node.Remove("role");
            }
        }

        return graph;
    }

    /// <summary>
    /// The role <paramref name="node"/> names itself, or null: its <see cref="NodeDeclaration.Role"/>, else the <c>role</c> option an agent step
    /// named it by before the field (<see cref="NodeOptionFields.Role"/>). The option is read, never written, until no kept version names it.
    /// </summary>
    public static string? Named(NodeDeclaration node) =>
        node.Role ?? (node.Options.TryGetValue(NodeOptionFields.Role, out var written) && NodeOptions.Value(written) is string option && option.Length > 0 ? option : null);

    /// <summary>
    /// The role as an option's JSON text, as the role's readers of an agent step take it: the field's where it names one, else the option as
    /// written, or null.
    /// </summary>
    public static string? Written(NodeDeclaration node) =>
        node.Role is { } role ? JsonSerializer.Serialize(role) : node.Options.GetValueOrDefault(NodeOptionFields.Role);

    /// <summary>What <paramref name="node"/>'s own role breaks, or null: a holder named where a role goes, or no role's id.</summary>
    public static WireProblem? Problem(NodeDeclaration node)
    {
        if (node.Role is not { } role) return null;
        if (Holders.Contains(role.Trim().ToLowerInvariant(), StringComparer.Ordinal))
        {
            return new(HolderRefused, null, node.Id,
                $"the step `{node.Id}` names `{role}`, who would fill a role, where its role goes: name the role that takes it (`{string.Join("`, `", All)}`, or a role of the catalogue), and the place says who fills it.");
        }

        return Shape.IsMatch(role) ? null : new(Unknown, null, node.Id, $"the step `{node.Id}` names `{role}`, which is no role's id: lowercase words joined by dashes.");
    }
}
