using System.Text.RegularExpressions;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The census of the sessions' journal. Its reference is the trigger-written <c>event_journal</c>: every write of a session's stored
/// state mints an <c>opened</c> or <c>state</c> row there whoever wrote it, and each must name the fact that witnessed it
/// (<see cref="SessionJournal.UnwitnessedAsync"/>), here and in the baseline of an installed store. And every door that moves or opens a
/// record names its witness, so a fact's door is the caller's, never the store's own word.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class SessionFactCensusTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-28T08:00:00Z");
    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Every_state_row_event_journal_mints_names_its_fact_and_a_write_past_the_journal_is_found()
    {
        foreach (var state in Enum.GetValues<SessionState>().Where(state => state != SessionState.Queued))
        {
            var opened = await _sessions.CreateAsync("q", "Owner", "stub", Now, witness: WitnessDoor.QuestOpen);
            await _sessions.SetStateAsync(opened.Id, SessionState.Working, null, null, null, Now, witness: WitnessDoor.Advance);
            await _sessions.SetStateAsync(opened.Id, state, "moved", null, null, Now, witness: WitnessDoor.Advance);
            // A record ended and taken up again: the move out of the end is witnessed too.
            if (SessionFold.OfStored(state).State == FoldedState.Ended)
            {
                await _sessions.SetStateAsync(opened.Id, SessionState.Working, "again", null, null, Now, witness: WitnessDoor.GoOn, forgive: true);
            }
        }

        var own = await _sessions.CreateAsync("q", "Owner", "stub", Now, witness: WitnessDoor.QuestOpen);
        await _sessions.MirrorAsync((await _sessions.FindAsync(own.Id))! with { Id = "other/x", Origin = "other", State = SessionState.Working });

        Assert.True(await RowsAsync() >= 30, "the census found too few state rows: the trigger no longer mints them");
        Assert.Empty(await _sessions.Journal.UnwitnessedAsync());

        // A write that moves the state outside the journal's door: the trigger still mints its row, and nothing witnesses it.
        await using (var past = _connection.CreateCommand())
        {
            past.CommandText = "UPDATE sessions SET state = 'Stopped' WHERE id = $id";
            past.Parameters.AddWithValue("$id", own.Id);
            await past.ExecuteNonQueryAsync();
        }

        var unwitnessed = Assert.Single(await _sessions.Journal.UnwitnessedAsync());
        Assert.Equal((own.Id, "state", "Stopped"), (unwitnessed.Session, unwitnessed.Change, unwitnessed.State));
    }

    private async Task<long> RowsAsync()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM event_journal WHERE family = 'sessions' AND change IN ('opened', 'state')";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>The service's production sources: its core, its hosts and its connector, the tests and the build's output aside.</summary>
    private static IEnumerable<string> Sources()
    {
        var service = Path.Combine(SourceFiles.WorkspaceFolder(), "src", "Daoris.Service");
        return Directory.EnumerateFiles(service, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(service, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) is var parts
                           && !parts[0].Contains("Tests", StringComparison.Ordinal) && !parts[0].Contains("Rehearsals", StringComparison.Ordinal)
                           && !parts.Contains("bin") && !parts.Contains("obj"));
    }

    [GeneratedRegex(@"\b(SetStateAsync|AdvanceAsync|CreateAsync)\(")]
    private static partial Regex Door();

    [Fact]
    public void Every_door_that_moves_or_opens_a_session_names_its_witness()
    {
        var unnamed = new List<string>();
        var calls = 0;
        foreach (var path in Sources())
        {
            var text = File.ReadAllText(path);
            foreach (Match door in Door().Matches(text))
            {
                var line = text[(text.LastIndexOf('\n', door.Index) + 1)..door.Index];
                // A declaration of a door is no call of one: its parameters are the census's next door's to name.
                if (Regex.IsMatch(line, @"\b(private|public|internal|protected)\b.*\bTask\b")) continue;
                var span = Arguments(text, door.Index + door.Length - 1);
                // A create is a session's where it names a kind of session: every door that opens one does, and no other store's create.
                if (door.Groups[1].Value == "CreateAsync" && !span.Contains("SessionKind", StringComparison.Ordinal)) continue;
                calls++;
                if (!Regex.IsMatch(span, @"\bwitness\b")) unnamed.Add($"{Path.GetFileName(path)}:{text[..door.Index].Count(c => c == '\n') + 1}");
            }
        }

        Assert.True(calls >= 15, $"the census found {calls} doors: it no longer reads them");
        Assert.True(unnamed.Count == 0, $"moves or opens a session with no witness named: {string.Join(", ", unnamed)}");
    }

    /// <summary>The text between the parenthesis at <paramref name="open"/> and the one that closes it.</summary>
    private static string Arguments(string text, int open)
    {
        var depth = 0;
        for (var at = open; at < text.Length; at++)
        {
            if (text[at] == '(') depth++;
            else if (text[at] == ')' && --depth == 0) return text[open..(at + 1)];
        }

        return text[open..];
    }
}
