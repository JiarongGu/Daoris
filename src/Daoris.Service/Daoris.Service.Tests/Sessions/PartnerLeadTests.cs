using Microsoft.Data.Sqlite;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A partner's desk judges who may lead a piece as the helper's desk judges who may ask: Ask Daoris's conversation is neither.</summary>
public sealed class PartnerLeadTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly string _home = Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", "partner-lead", Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Partners));
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (IOException)
        {
            // A file the scanner holds for a moment: the next prune of _fixtures takes it.
        }
    }

    [Theory]
    [InlineData("Owner", HelperRefusal.None)]
    [InlineData(SessionLedger.HelpRepository, HelperRefusal.BadShape)]
    public async Task Ask_Daoriss_conversation_cannot_lead_a_piece(string repository, HelperRefusal refusal)
    {
        var lead = await _sessions.CreateAsync(null, repository, "claude-code", Now, kind: SessionKind.Chat, tree: Path.Combine(_home, "lead"), ownEntry: true);
        File.WriteAllText(
            PartnerBox.PathOf(_home, "p1a2b3c4", "ask"),
            $"{{\"id\":\"p1a2b3c4\",\"asker\":\"{lead.Id}\",\"piece\":\"Make the window seven days.\",\"base\":\"abc123\"}}");

        var opened = await new PartnerDesk(_sessions, new PartnerBox(_home)).OpenAsync(
            new PartnerOpen(lead.Id, "p1a2b3c4", "codex-acp", Path.Combine(_home, "clone")), Now);

        Assert.Equal(refusal, opened.Refusal);
        if (refusal != HelperRefusal.None) Assert.Contains("Ask Daoris", opened.Message);
    }
}
