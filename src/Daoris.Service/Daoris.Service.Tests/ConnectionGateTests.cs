namespace Daoris.Service.Tests;

/// <summary>
/// A host holds one connection for all its stores and answers requests at once, and SQLite does not
/// nest transactions — so every transaction on it goes through <c>ConnectionGate</c>, or a second one
/// arriving mid-way fails (SYNC1 found it with a take arriving while the page published). A source scan
/// rather than a unit test, because the property is one every transaction site must carry and the next
/// one written will not know to.
/// </summary>
public sealed class ConnectionGateTests
{
    [Fact]
    public void Every_file_that_opens_a_transaction_takes_the_connections_gate()
    {
        var core = SourceRoot();
        var sites = 0;
        var ungated = new List<string>();

        foreach (var file in Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var source = File.ReadAllText(file);
            if (!source.Contains("BeginTransaction", StringComparison.Ordinal)) continue;

            sites++;
            if (!source.Contains("ConnectionGate.For(", StringComparison.Ordinal))
            {
                ungated.Add(Path.GetRelativePath(core, file));
            }
        }

        Assert.True(sites >= 2, $"expected to find the quest store's and the index's transactions, found {sites} file(s)");
        Assert.True(
            ungated.Count == 0,
            "a transaction outside the connection's gate fails whenever another request holds one:\n"
            + string.Join('\n', ungated));
    }

    private static string SourceRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Service.Core")))
        {
            folder = folder.Parent;
        }

        return folder is null
            ? throw new InvalidOperationException("the service source tree was not found above the test binary")
            : Path.Combine(folder.FullName, "Daoris.Service.Core");
    }
}
