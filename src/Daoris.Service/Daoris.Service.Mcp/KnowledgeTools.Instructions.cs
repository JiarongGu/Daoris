using System.Text;

namespace Daoris.Knowledge.Mcp;

public sealed partial class KnowledgeTools
{
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
