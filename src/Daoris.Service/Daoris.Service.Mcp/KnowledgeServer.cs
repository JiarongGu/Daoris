using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// The connector's server as a session meets it: its tool classes, as the one list the host registers and the tests read,
/// and what it says before the first search (PART10a).
/// </summary>
/// <remarks>
/// <para>Each tool class takes only the services its tools use, and the SDK builds it from the host's services for each
/// call. A new class of tools is one new file and one line in <see cref="ToolTypes"/>; nothing finds one by scanning. The
/// tool names are the wire and belong to each method, whichever class holds it.</para>
///
/// <para>Descriptions are written for the moment of choosing, not the moment of reading documentation — they are what a
/// model matches against when it decides whether a tool applies, so they say what the tool answers rather than what it
/// does internally.</para>
///
/// <para>Results are markdown rather than JSON on purpose: the caller is a language model, and a table it can read beats a
/// structure it has to re-serialise into prose.</para>
/// </remarks>
public static class KnowledgeServer
{
    /// <summary>Every class of tools the server offers.</summary>
    public static IReadOnlyList<Type> ToolTypes { get; } =
    [
        typeof(KnowledgeSearchTools),
        typeof(QuestTools),
        typeof(RuleProposalTools),
        typeof(ReviewTools),
        typeof(GoAheadTools),
        typeof(HelperTools),
        typeof(ChannelTools),
        typeof(OpinionTools),
        // Ask Daoris's proposals, a class for each kind of proposal the box writes (PART10c).
        typeof(HelpAcceptTool),
        typeof(HelpAccountTool),
        typeof(HelpAgentTool),
        typeof(HelpAskTool),
        typeof(HelpBrowserTool),
        typeof(HelpClearTool),
        typeof(HelpDeleteTool),
        typeof(HelpGoTool),
        typeof(HelpHandTool),
        typeof(HelpPauseTool),
        typeof(HelpPluginTool),
        typeof(HelpRepositoryTool),
        typeof(HelpSessionTool),
        typeof(HelpSettingTool),
        typeof(HelpSetupTool),
        typeof(HelpSyncTool),
        typeof(HelpWorkflowTool),
    ];

    /// <summary>
    /// Registers every class in <see cref="ToolTypes"/>, and the workspace scope the search and quest tools share, which is
    /// the tools' own and not a service the host composes.
    /// </summary>
    public static IMcpServerBuilder WithKnowledgeTools(this IMcpServerBuilder builder)
    {
        builder.Services.TryAddSingleton<WorkspaceScope>();
        // Named, because the SDK's generic WithTools<T>(T target) binds the list itself as one tool object, whose methods
        // are no tools, and the host then advertised nothing (PART10a's pin caught it).
        return builder.WithTools(toolTypes: ToolTypes);
    }

    /// <summary>
    /// What a server over one checkout tells a session before its first search (ORIENT1c), or null for a
    /// server over a family, whose tools already say what they answer.
    /// </summary>
    /// <remarks>
    /// <para>A workspace's own server is asked where something is and what was decided, by agents who would
    /// otherwise search the files. They read a server's instructions with its tools, so this says what it
    /// reads, where it reads from, and which tier answers: by words only when the deployment named no model
    /// (D24's report, given before the first answer as well as on every one).</para>
    ///
    /// <para>Project-agnostic, as the tools' own descriptions are: the folders are the deployment's, named
    /// back to it, and no decision of this repository is cited.</para>
    /// </remarks>
    /// <param name="options">The deployment's options.</param>
    /// <param name="semantic">Whether a model answers beside the words.</param>
    public static string? Instructions(ServiceOptions options, bool semantic)
    {
        if (options.Repository is not { } checkout) return null;

        var name = Path.GetFileName(checkout.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var text = new StringBuilder();
        text.Append($"This server reads one checkout, `{name}`: its rules, knowledge, skills, decisions, fixes and finished tasks");
        if (options.Documents is { } documents) text.Append($"; the documents under `{documents}/`, a section each");
        if (options.Index is { } index)
        {
            text.Append($"; and its index under `{index}/`, a row each, so asking where something is (a route, a command, ")
                .Append("a key, a fixture, a method) lands on the row that names its file and line");
        }
        text.Append(". Ask `knowledge_search` before searching the files, and `knowledge_get` reads an entry whole, ")
            .Append("or only the lines a hit names. A reading older than a minute is read again at the next search.");
        text.Append(semantic
            ? " Answers match by words and by meaning."
            : " Answers match by words only: no model is named here, so use the words the repository would use.");
        return text.ToString();
    }
}
