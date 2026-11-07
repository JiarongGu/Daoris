using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Data.Sqlite;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A home of a working machine's size, generated under the repository's scratch: the service's own host over a store holding
/// thousands of ended session records, a registered repository with a few dozen session trees on unlanded branches, and one tree holding
/// a large ignored build. Every name is invented. The records past the few opened through the client are copies of one ended record
/// written into the store's table, each with its own id and revision, so the generator spends seconds where thousands of opens would
/// spend minutes; the service reads them as it reads its own.
/// </summary>
internal sealed class BenchHome : IAsyncDisposable
{
    /// <summary>The repository the home registers.</summary>
    public const string Repository = "engine";

    /// <summary>
    /// The folder the repository ignores, which the large tree's build fills: an engine's intermediate files, a name no build tool's list
    /// holds, so a read that looks into ignored folders looks into it.
    /// </summary>
    public const string Ignored = "Intermediate";

    private BenchHome(string scratch, RealServiceHost host, ServiceClient client, string root, string home)
    {
        Scratch = scratch;
        Host = host;
        Client = client;
        Root = root;
        Home = home;
    }

    public string Scratch { get; }

    public RealServiceHost Host { get; }

    public ServiceClient Client { get; }

    /// <summary>The client's doors, which count what each read asks.</summary>
    public required BenchDoors Doors { get; init; }

    /// <summary>The registered checkout.</summary>
    public string Root { get; }

    /// <summary>The driver's home, whose trees are under it.</summary>
    public string Home { get; }

    /// <summary>Each tree with the session that ran in it and the commit it opened at.</summary>
    public List<(string Session, string Tree, string BaseCommit)> Trees { get; } = [];

    /// <summary>The tree whose ignored build holds <see cref="Built"/> files.</summary>
    public (string Session, string Tree, string BaseCommit) Large { get; private set; }

    /// <summary>How many files the large tree's ignored build holds.</summary>
    public int Built { get; private set; }

    /// <summary>
    /// The home: <paramref name="records"/> ended records in all, <paramref name="trees"/> of them each with a tree and a commit of its own,
    /// one of those with <paramref name="built"/> ignored files.
    /// </summary>
    public static async Task<BenchHome> MakeAsync(int records, int trees, int built)
    {
        var scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "home-size", Guid.NewGuid().ToString("N")[..8]);
        var host = await RealServiceHost.StartAsync(Path.Combine(scratch, "service"));
        var doors = new BenchDoors(new ServiceHttp(host.Url, key: null));
        var client = new ServiceClient(doors);
        var root = await GitSeed.PlainAsync(Path.Combine(scratch, "checkouts", Repository), Repository);
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), $"/{Ignored}/\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "--quiet", "-m", "ignore the build");
        var home = Path.Combine(scratch, "home");
        Directory.CreateDirectory(home);
        var made = new BenchHome(scratch, host, client, root, home) { Doors = doors };
        try
        {
            await made.FillAsync(records, trees, built);
            return made;
        }
        catch
        {
            // The host holds its store open and outlives the test unless it is stopped here: no disposal follows a failed start.
            await made.DisposeAsync();
            throw;
        }
    }

    private async Task FillAsync(int records, int trees, int built)
    {
        var (root, home, client, made) = (Root, Home, Client, this);
        var (ok, message) = await client.RegisterAsync(new JsonObject
        {
            ["repository"] = Repository,
            ["root"] = root,
            ["packs"] = new JsonArray(),
            ["domain"] = new JsonObject { ["summary"] = "An engine.", ["owns"] = new JsonArray("its own tree"), ["accepts"] = new JsonArray("a quest") },
        });
        if (!ok) throw new InvalidOperationException(message);

        var sessionTrees = new SessionTrees(home);
        for (var at = 0; at < trees; at++)
        {
            var tree = await sessionTrees.OpenAsync(root, Repository, "default");
            var baseCommit = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
            await File.WriteAllTextAsync(Path.Combine(tree.Path, $"part-{at}.txt"), $"part {at}\n");
            await GitAsync(tree.Path, "add", "-A");
            await GitAsync(tree.Path, "commit", "--quiet", "-m", $"part {at}");
            made.Trees.Add((await made.EndedAsync(tree.Path, baseCommit), tree.Path, baseCommit));
        }

        // The large tree's work is landed by a squash, so the line holds it by content and every read asks what else the tree holds; the
        // checkout and the tree each built the engine, so the ignored build is in both.
        made.Large = made.Trees[0];
        made.Built = built;
        await GitAsync(root, "cherry-pick", "--allow-empty", (await GitAsync(made.Large.Tree, "rev-parse", "HEAD")).Trim());
        Build(Path.Combine(root, Ignored), built);
        Build(Path.Combine(made.Large.Tree, Ignored), built);
        made.Copy(made.Trees[^1].Session, records - trees);
    }

    /// <summary>An ended conversation in <paramref name="tree"/>, opened and ended as the driver opens and ends one.</summary>
    private async Task<string> EndedAsync(string tree, string baseCommit)
    {
        var (id, message) = await Client.OpenChatAsync(Repository, "stub", tree: tree, baseCommit: baseCommit);
        if (id is null) throw new InvalidOperationException(message);
        foreach (var state in new[] { "starting", "working", "completed" }) await Client.AdvanceAsync(id, state);
        return id;
    }

    /// <summary>A build's output: folders of small files, as an engine's intermediate files are.</summary>
    private static void Build(string folder, int files)
    {
        const int PerFolder = 500;
        for (var at = 0; at < files; at++)
        {
            var into = Path.Combine(folder, $"module-{at / PerFolder:D3}", "obj-free");
            if (at % PerFolder == 0) Directory.CreateDirectory(into);
            File.WriteAllText(Path.Combine(into, $"unit-{at:D5}.o"), "built\n");
        }
    }

    /// <summary>
    /// <paramref name="count"/> copies of <paramref name="template"/>'s row, each a record of its own with no tree, last moved days ago: the
    /// ended records a machine keeps.
    /// </summary>
    private void Copy(string template, int count)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Scratch, "service", "index", "knowledge.db")};Mode=ReadWrite;Pooling=False");
        connection.Open();
        var columns = new List<string>();
        using (var info = connection.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(sessions)";
            using var read = info.ExecuteReader();
            while (read.Read()) columns.Add(read.GetString(1));
        }

        string Value(string column) => column switch
        {
            "id" => "$id",
            "revision" => "(SELECT MAX(revision) FROM sessions) + 1",
            "tree" or "base_commit" => "NULL",
            "created" or "updated" => "$at",
            _ => column,
        };

        using var transaction = connection.BeginTransaction();
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = $"INSERT INTO sessions ({string.Join(", ", columns)}) SELECT {string.Join(", ", columns.Select(Value))} FROM sessions WHERE id = $template";
        var id = insert.Parameters.Add("$id", SqliteType.Text);
        var at = insert.Parameters.Add("$at", SqliteType.Text);
        insert.Parameters.AddWithValue("$template", template);
        var now = DateTimeOffset.UtcNow;
        for (var each = 0; each < count; each++)
        {
            id.Value = $"{each:x8}";
            at.Value = now.AddDays(-3).AddMinutes(-each).ToString("O");
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private RealServiceHost? _again;

    /// <summary>
    /// The service's host stopped and started again over this home's store, until it answers: what the desktop's start waits on the host
    /// for at this size. The client is left on the stopped host.
    /// </summary>
    public async Task<long> HostAgainAsync()
    {
        Host.Dispose();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _again = await RealServiceHost.StartAsync(Path.Combine(Scratch, "service"));
        return watch.ElapsedMilliseconds;
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Host.Dispose();
        _again?.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(Scratch);
        return ValueTask.CompletedTask;
    }
}
