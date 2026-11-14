using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The turns each record's session ran, read across its line (<see cref="SessionTurns.OfLine"/>): one read for every line a list names,
/// since an ended part a list leaves out still holds its turns.
/// </summary>
public static class SessionTurnLines
{
    /// <summary>Each of this machine's <paramref name="records"/> by id, with its line's turns; a teammate's record answers none.</summary>
    public static Task<IReadOnlyDictionary<string, IReadOnlyList<SessionTurn>>> ReadAsync(
        SessionStore store, IReadOnlyList<Session> records, CancellationToken ct = default) => store.Db.RunAsync(async () =>
    {
        var lines = records.Where(record => record.Origin is null && record.Line is not null).Select(record => record.Line!)
            .Distinct(StringComparer.Ordinal).ToList();
        var members = new Dictionary<string, List<Session>>(StringComparer.Ordinal);
        if (lines.Count > 0)
        {
            await using var command = store.Db.Command();
            command.CommandText = "SELECT * FROM sessions WHERE origin IS NULL AND line IN (SELECT value FROM json_each($lines))";
            command.Parameters.AddWithValue("$lines", JsonSerializer.Serialize(lines));
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var member = SessionRowCodec.Read(reader);
                if (!members.TryGetValue(member.Line!, out var kept)) members[member.Line!] = kept = [];
                kept.Add(member);
            }
        }

        var turns = new Dictionary<string, IReadOnlyList<SessionTurn>>(StringComparer.Ordinal);
        var ofLine = members.ToDictionary(pair => pair.Key, pair => SessionTurns.OfLine(pair.Value), StringComparer.Ordinal);
        foreach (var record in records.Where(record => record.Origin is null))
        {
            turns[record.Id] = record.Line is { } line && ofLine.TryGetValue(line, out var read) ? read : SessionTurns.OfLine([record]);
        }

        return (IReadOnlyDictionary<string, IReadOnlyList<SessionTurn>>)turns;
    }, ct);
}
