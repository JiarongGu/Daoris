using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>The words of <c>daoris quest list</c>, read: which quests, and how they are printed.</summary>
public sealed record QuestListRead(string? Repository, string? Workspace, bool Open, bool Json);

/// <summary>
/// <c>daoris quest list [--repository &lt;name&gt;] [--workspace &lt;name&gt;] [--open] [--json]</c>: a read of the quests this machine
/// holds, closed ones too unless <c>--open</c>. It changes nothing.
/// </summary>
public static class QuestListCommand
{
    public const string Usage = "daoris quest list [--repository <name>] [--workspace <name>] [--open] [--json]";

    /// <summary>Whether <paramref name="args"/> are this verb's.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["list", ..];

    /// <summary>The words read, or null with the problem; a problem sends nothing.</summary>
    public static QuestListRead? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        string? repository = null, workspace = null;
        bool open = false, json = false;
        for (var i = 1; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--open": open = true; break;
                case "--json": json = true; break;
                case "--repository" or "--workspace":
                    if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        problem = $"{args[i]} needs a value.";
                        return null;
                    }

                    if (args[i] == "--repository") repository = args[++i];
                    else workspace = args[++i];
                    break;
                default:
                    problem = $"`{args[i]}` is not a flag of `quest list`.";
                    return null;
            }
        }

        return new QuestListRead(repository, workspace, open, json);
    }

    /// <summary>One quest as a line: its id, status, receiver and title.</summary>
    public static string Line(string id, string status, string to, string title) =>
        $"#{id.TrimStart('#')}  {status}  {to}  {title.ReplaceLineEndings(" ").Trim()}";

    /// <summary>Print the quests, or their JSON as the service wrote it; 0 read.</summary>
    public static async Task<int> RunAsync(QuestListRead read, ServiceClient service, TextWriter output)
    {
        var sent = await service.SendAsync(new ListQuests(read.Repository, !read.Open, read.Workspace)).ConfigureAwait(false);
        var quests = sent.Answered();
        if (read.Json)
        {
            output.WriteLine(sent.Reply.Payload);
            return 0;
        }

        if (quests.Length == 0) output.WriteLine(read.Open ? "no open quests." : "no quests.");
        foreach (var quest in quests) output.WriteLine(Line(quest.Id, quest.Status, quest.To, quest.Title));
        return 0;
    }
}
