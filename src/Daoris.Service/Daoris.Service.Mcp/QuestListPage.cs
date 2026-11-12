using System.Text;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// One page of <c>quest_list</c>'s answer: the quests in their order, each page filled to a budget of characters well inside an agent's
/// tool limit, never fewer than one quest, under each repository's heading with its whole count. A list that fits is one page, said as
/// it always was; a longer one says which page it is and which holds the next, since every closed quest of a workspace answered at once
/// overflowed what the agent can read.
/// </summary>
public static class QuestListPage
{
    /// <summary>The characters a page's quests fill before the next goes to the page after.</summary>
    public const int Budget = 40_000;

    /// <summary>One quest's lines, under the repository it is addressed to.</summary>
    public sealed record Block(string To, string Text);

    /// <summary>Page <paramref name="page"/> (from 1) of <paramref name="blocks"/>, said in <paramref name="scoped"/>'s words.</summary>
    public static string Of(IReadOnlyList<Block> blocks, int page, string scoped, int budget = Budget)
    {
        var pages = Split(blocks, budget);
        if (page < 1 || page > pages.Count)
        {
            return $"{blocks.Count} quest(s){scoped} fill {pages.Count} page(s); there is no page {page}.";
        }

        var (first, count) = pages[page - 1];
        var text = new StringBuilder(pages.Count == 1
            ? $"{blocks.Count} quest(s){scoped}:\n\n"
            : $"{blocks.Count} quest(s){scoped}, page {page} of {pages.Count}: quests {first + 1} to {first + count}.\n\n");
        var totals = blocks.GroupBy(block => block.To).ToDictionary(group => group.Key, group => group.Count());
        string? heading = null;
        foreach (var block in blocks.Skip(first).Take(count))
        {
            if (block.To != heading)
            {
                if (heading is not null) text.AppendLine();
                heading = block.To;
                text.AppendLine($"## `{block.To}` — {totals[block.To]}");
            }

            text.Append(block.Text);
        }

        text.AppendLine();
        if (page < pages.Count)
        {
            text.AppendLine($"Page {page + 1} holds the next: call `quest_list` again with `page: {page + 1}`; `handback_read` reads one quest's note whole.");
        }

        return text.ToString();
    }

    /// <summary>Each page's first block and how many it holds.</summary>
    private static List<(int First, int Count)> Split(IReadOnlyList<Block> blocks, int budget)
    {
        List<(int, int)> pages = [];
        var (first, filled) = (0, 0);
        for (var at = 0; at < blocks.Count; at++)
        {
            if (at > first && filled + blocks[at].Text.Length > budget)
            {
                pages.Add((first, at - first));
                (first, filled) = (at, 0);
            }

            filled += blocks[at].Text.Length;
        }

        if (blocks.Count > first || pages.Count == 0) pages.Add((first, blocks.Count - first));
        return pages;
    }
}
