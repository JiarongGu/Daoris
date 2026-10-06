using System.Text;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// Search backed by SQLite's FTS5 index, ranked by BM25.
/// </summary>
/// <remarks>
/// Replaces the hand-rolled scorer with a real ranking function, and filters in SQL rather than in
/// memory — which is what lets the index outgrow what fits in a process.
///
/// <see cref="LexicalKnowledgeSearch"/> stays: it works against any
/// <see cref="IKnowledgeStore"/>, which the tests want and a store-agnostic default needs.
/// </remarks>
public sealed class SqliteKnowledgeSearch(SqliteKnowledgeStore store) : IKnowledgeSearch
{
    /// <summary>
    /// Title matches count for more than body matches: a title is what the author chose to call the
    /// thing, and an entry titled for the query is almost always the one wanted.
    /// </summary>
    /// <remarks>
    /// These are BM25 <em>column</em> weights, and they are positional across <b>every</b> column of
    /// the FTS table — including the <c>UNINDEXED</c> <c>id</c>. Passing two weights therefore
    /// assigned them to <c>id</c> and <c>title</c>, leaving body and title equal and the title
    /// weighting silently doing nothing. The leading <c>0.0</c> for <c>id</c> is what keeps the rest
    /// aligned; it is not decoration.
    /// </remarks>
    private const double TitleWeight = 10.0;
    private const double BodyWeight = 1.0;

    /// <summary>Invariant culture: a comma decimal separator would be a SQL syntax error.</summary>
    private static string Weight(double value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Where bm25's rank lands: immediately after the entry's own columns.</summary>
    private static int RankOrdinal => SqliteKnowledgeStore.ColumnCount;

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        var (filter, parameters) = BuildFilter(query);
        var match = BuildMatchExpression(query.Text);

        await using var command = store.Connection.CreateCommand();

        if (match is null)
        {
            // No usable terms is a browse, not a failed search.
            command.CommandText =
                $"SELECT {SqliteKnowledgeStore.Columns} FROM entries e WHERE 1=1 {filter} " +
                "ORDER BY repository, title LIMIT $limit;";
        }
        else
        {
            // bm25() returns a NEGATIVE score where more negative is a better match, so ascending
            // order is best-first and the sign is flipped for the caller.
            command.CommandText =
                $"""
                 SELECT {SqliteKnowledgeStore.QualifiedColumns},
                        bm25(entries_fts, 0.0, {Weight(TitleWeight)}, {Weight(BodyWeight)}) AS rank
                 FROM entries_fts
                 JOIN entries e ON e.id = entries_fts.id
                 WHERE entries_fts MATCH $match {filter}
                 ORDER BY rank
                 LIMIT $limit;
                 """;
            command.Parameters.AddWithValue("$match", match);
        }

        command.Parameters.AddWithValue("$limit", query.Limit);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);

        var hits = new List<KnowledgeHit>();
        var asked = Text.QueryTerms(query.Text).Select(term => term.Term).ToList();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var entry = SqliteKnowledgeStore.Read(reader);
            // The rank rides one past the entry's own columns — so it moves whenever they do. It moved
            // once already, when entries gained their workspace.
            var score = match is null ? 0 : -reader.GetDouble(RankOrdinal);
            hits.Add(new KnowledgeHit(entry, score, Text.Excerpt(entry.Body, asked)));
        }

        return hits;
    }

    /// <summary>
    /// Turn free text into an FTS5 MATCH expression.
    /// </summary>
    /// <remarks>
    /// Every term is quoted, because FTS5's query language treats <c>" * : ( ) NOT OR</c> and others
    /// as syntax — an unquoted apostrophe or colon from an ordinary question is a syntax error, and a
    /// search box that throws on a normal sentence is worse than one that finds nothing.
    ///
    /// Terms are joined with OR so a partial match still returns something; BM25 then ranks the
    /// entries matching more of the query above those matching less, which is the behaviour wanted
    /// without making a missing word fatal.
    ///
    /// The terms are the question's words and its adjacent words joined (<see cref="Text.QueryTerms"/>,
    /// ORIENT1f): the row holds an identifier whole beside its words, so <i>probe lock</i> reaches
    /// <c>ProbeLock</c> by both. A join is named once for each word it joins, which is how it counts that
    /// many times: FTS5's bm25 adds every phrase of the expression, a phrase named twice twice, and takes
    /// no weight per phrase.
    /// </remarks>
    internal static string? BuildMatchExpression(string text)
    {
        var terms = Text.QueryTerms(text);

        if (terms.Count == 0) return null;

        var builder = new StringBuilder();
        foreach (var term in terms)
        {
            for (var named = 0; named < term.Words; named++)
            {
                if (builder.Length > 0) builder.Append(" OR ");
                builder.Append('"').Append(term.Term.Replace("\"", "\"\"")).Append('"');
            }
        }

        return builder.ToString();
    }

    private static (string Filter, List<(string Name, object Value)> Parameters) BuildFilter(KnowledgeQuery query)
    {
        var filter = new StringBuilder();
        var parameters = new List<(string, object)>();

        if (query.Provenance is { } provenance)
        {
            filter.Append(" AND e.provenance = $prov");
            parameters.Add(("$prov", (int)provenance));
        }

        if (query.Kinds is { Count: > 0 } kinds)
        {
            var names = kinds.Select((k, i) => (Name: $"$kind{i}", Value: (object)(int)k)).ToList();
            filter.Append($" AND e.kind IN ({string.Join(", ", names.Select(n => n.Name))})");
            parameters.AddRange(names);
        }

        if (query.Repositories is { Count: > 0 } repositories)
        {
            var names = repositories.Select((r, i) => (Name: $"$repo{i}", Value: (object)r)).ToList();
            // NOCASE, as the parsed set is and as every other door compares a repository name (REV3).
            filter.Append($" AND e.repository COLLATE NOCASE IN ({string.Join(", ", names.Select(n => n.Name))})");
            parameters.AddRange(names);
        }

        if (query.Workspace is { } workspace)
        {
            // NOCASE, because a workspace name is a person's name for a circle and they will type it
            // the way it reads — the same comparison Workspaces.Same makes in memory.
            filter.Append(" AND e.workspace = $workspace COLLATE NOCASE");
            parameters.Add(("$workspace", Workspaces.Normalize(workspace)));
        }

        return (filter.ToString(), parameters);
    }
}
