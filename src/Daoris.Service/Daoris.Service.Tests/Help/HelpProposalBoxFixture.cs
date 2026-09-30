using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>What every kind's writer tests stand on (MOD6): a home of the test's own, the box over it, and the file a proposal left.</summary>
public abstract class HelpProposalBoxFixture : IDisposable
{
    protected readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-proposals-" + Guid.NewGuid().ToString("N")[..8]);
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-29T10:00:00Z");

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
        GC.SuppressFinalize(this);
    }

    protected HelpProposalBox Box() => new(_home);

    protected JsonElement Written(string id)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_home, "help", "proposals", $"{id}.json")));
        return document.RootElement.Clone();
    }

    /// <summary>An empty field of a theory's row, read as the tool's parameter left out.</summary>
    protected static string? Named(string field) => field.Length == 0 ? null : field;
}
