using System.Diagnostics;
using System.Text;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests.Benchmark;

/// <summary>
/// A home at the size an install reaches after months of work: about three thousand ended sessions with their words and
/// their folders, a few open ones, a few hundred quests and asks, and a registry of twenty-five checkouts, every name invented. It
/// is written under a test's scratch through the stores' own doors, so the records are the ones the service writes, and the host
/// is then started over it as the shell starts one.
/// </summary>
/// <remarks>
/// The records are spread over ninety days, as a home that kept them would hold them: older records carry no entry key, as records
/// from before the key do, and the newer ones carry the one the ledger writes. Chats continue one another in short lines and a
/// quest's attempts follow each other, so a line's walk has hops to take.
/// </remarks>
public static class BenchmarkHome
{
    public const string Workspace = "default";

    public const int Repositories = 25;

    public const int EndedSessions = 3000;

    public const int Quests = 300;

    public const int Asks = 150;

    /// <summary>The open records: three working, two waiting on the person with words, one queued, each in its own repository.</summary>
    public const int OpenSessions = 6;

    private static readonly string[] Names =
    [
        "alder", "basalt", "cinder", "dunmore", "elmsway", "fernhold", "gristle", "harrow", "ivel", "juniper",
        "kestrel", "larch", "mossgate", "nettle", "oriel", "pellow", "quarry", "rowan", "sedge", "thistle",
        "umber", "vetch", "wold", "yarrow", "zephyr",
    ];

    private static readonly DateTimeOffset Start = new(2026, 7, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The repositories' names, in the order they were registered.</summary>
    public static IReadOnlyList<string> RepositoryNames => Names;

    /// <summary>Write the home, its index and its checkouts under <paramref name="scratch"/>, laid out as <see cref="RealHost"/> starts over.</summary>
    public static async Task WriteAsync(string scratch, CancellationToken ct = default)
    {
        var home = Path.Combine(scratch, "home");
        var repositories = Path.Combine(scratch, "repositories");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(repositories);
        foreach (var name in Names) Checkout(repositories, name);

        using var environment = new ScopedEnvironment(new Dictionary<string, string?>
        {
            [DaorisHome.Variable] = home,
            [ServiceOptions.DatabaseVariable] = Path.Combine(scratch, "index", "knowledge.db"),
            [ServiceOptions.RootVariable] = repositories,
            [ServiceOptions.ModelVariable] = null,
            [Access.ModeVariable] = null,
            [Access.WorkspaceVariable] = null,
            [RemoteConfig.UrlVariable] = null,
        });
        var (options, error) = ServiceOptions.FromEnvironment(repositories, Path.Combine(scratch, "index", "knowledge.db"));
        if (error is not null) throw new InvalidOperationException(error);
        await using var service = await ServiceFactory.CreateAsync(options, ct: ct);
        // A fixture is written once and read after: nothing is lost if the machine stops while it is written, so no write waits on the disk.
        await using (var unsynced = service.Store!.Connection.CreateCommand())
        {
            unsynced.CommandText = "PRAGMA synchronous = OFF; PRAGMA journal_mode = MEMORY;";
            await unsynced.ExecuteNonQueryAsync(ct);
        }

        await service.Service.ImportAsync(repositories, Start, Workspace, ct);

        var asks = new List<string>();
        for (var i = 0; i < Asks; i++)
        {
            var asked = await service.Asks.AskAsync(new AskRequest(Workspace, $"Make the {Topic(i)} report read the {Topic(i + 7)} ledger."), At(i * 600), ct);
            asks.Add(asked.Ask!.Id);
        }

        var quests = new List<Quest>();
        for (var i = 0; i < Quests; i++)
        {
            var from = i % 3 == 0 ? $"ask #{asks[i % asks.Count]}" : Names[(i + 3) % Names.Length];
            quests.Add(await service.Quests.PublishAsync(
                from, Names[i % Names.Length], $"The {Topic(i)} step for {Names[i % Names.Length]}",
                $"Carry the {Topic(i)} change through the {Topic(i + 3)} module and say what moved.", At(i * 300), Workspace, ct: ct));
        }

        string Root(string repository) => Path.Combine(repositories, repository);
        await EndedAsync(service, home, Root, quests, asks, ct);
        await OpenAsync(service, home, Root, quests, ct);
    }

    private static async Task EndedAsync(
        ComposedService service, string home, Func<string, string> Root, IReadOnlyList<Quest> quests, IReadOnlyList<string> asks, CancellationToken ct)
    {
        var lastChat = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < EndedSessions; i++)
        {
            var at = At(i * 2600);
            // Records from before the entry key hold none; the newer half hold the one the ledger writes at the open.
            var keyed = i >= EndedSessions / 2;
            Session session;
            if (i % 20 == 0)
            {
                var ask = asks[(i / 20) % asks.Count];
                session = await service.Sessions.CreateAsync(
                    null, "intake", "claude-code", at, Workspace, SessionKind.Chat, ct: ct, ask: ask,
                    entryOrigin: keyed ? SessionEntries.AskOrigin(ask) : null, entryPart: keyed ? "intake" : null);
            }
            else if (i % 5 < 2)
            {
                var quest = quests[i % quests.Count];
                session = await service.Sessions.CreateAsync(
                    quest.Id, quest.To, "claude-code", at, Workspace, SessionKind.Driven, tree: Root(quest.To), baseCommit: "0000000",
                    ct: ct, entryOrigin: keyed ? $"quest:{quest.Id}" : null);
            }
            else
            {
                var repository = Names[i % Names.Length];
                // One chat in four continues the last one in its repository, so lines of a few parts form.
                var from = i % 4 == 0 && lastChat.TryGetValue(repository, out var previous) ? previous : null;
                session = await service.Sessions.CreateAsync(
                    null, repository, "claude-code", at, Workspace, SessionKind.Chat, tree: Root(repository), ct: ct, from: from,
                    fromWhy: from is null ? null : "handed over", ownEntry: keyed && from is null);
                lastChat[repository] = session.Id;
            }

            await service.Sessions.SetStateAsync(session.Id, SessionState.Working, null, null, null, at.AddMinutes(1), ct);
            if (i % 3 == 0)
            {
                await service.Sessions.KeepSaidAsync(session.Id, Word($"Look at the {Topic(i)} case too.", at.AddMinutes(5)), ct: ct);
                await service.Sessions.KeepSaidAsync(session.Id, Word("And say what you changed.", at.AddMinutes(9)), ct: ct);
            }

            var ended = i % 11 == 0 ? SessionState.Failed : i % 7 == 0 ? SessionState.Stopped : SessionState.Completed;
            await service.Sessions.SetStateAsync(
                session.Id, ended, $"Ended after the {Topic(i)} step.", null, "transcript.jsonl", at.AddMinutes(40), ct);
            Folder(home, session.Id, i);
        }
    }

    private static async Task OpenAsync(ComposedService service, string home, Func<string, string> Root, IReadOnlyList<Quest> quests, CancellationToken ct)
    {
        var at = At(EndedSessions * 2600 + 600);
        for (var i = 0; i < OpenSessions; i++)
        {
            var quest = quests[quests.Count - 1 - i];
            var session = await service.Sessions.CreateAsync(
                quest.Id, quest.To, "claude-code", at, Workspace, SessionKind.Driven, tree: Root(quest.To), baseCommit: "0000000", ct: ct,
                entryOrigin: $"quest:{quest.Id}");
            if (i == OpenSessions - 1) continue;
            await service.Sessions.SetStateAsync(session.Id, SessionState.Working, null, null, null, at.AddMinutes(1), ct);
            if (i >= 3)
            {
                await service.Sessions.SetStateAsync(session.Id, SessionState.AwaitingPerson, "Which ledger did you mean?", null, null, at.AddMinutes(4), ct);
                await service.Sessions.KeepSaidAsync(session.Id, Word("The monthly one.", at.AddMinutes(6)), ct: ct);
            }

            Folder(home, session.Id, i);
        }
    }

    private static SaidWord Word(string text, DateTimeOffset at) => new(Guid.NewGuid().ToString("N")[..8], text, at, []);

    private static DateTimeOffset At(int seconds) => Start.AddSeconds(seconds);

    private static string Topic(int i) => Topics[i % Topics.Length];

    private static readonly string[] Topics = ["bridge", "ledger", "harbour", "lantern", "orchard", "quarry", "meadow", "furnace", "beacon", "mill", "spindle"];

    // What a session keeps in its folder: its transcript, its prompt and the events of its run, a few kilobytes each.
    private static void Folder(string home, string id, int i)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Sessions, id);
        Directory.CreateDirectory(folder);
        var transcript = new StringBuilder();
        for (var line = 0; line < 40; line++)
        {
            transcript.Append($$"""{"type":"assistant","line":{{line}},"text":"Read the {{Topic(i + line)}} module and changed its step {{line}}."}""").Append('\n');
        }

        File.WriteAllText(Path.Combine(folder, "transcript.jsonl"), transcript.ToString());
        File.WriteAllText(Path.Combine(folder, "prompt.md"), $"# The {Topic(i)} step\n\nCarry it through and say what moved.\n");
        File.WriteAllText(Path.Combine(folder, "run.events.jsonl"), """{"event":"started"}""" + "\n" + """{"event":"ended"}""" + "\n");
    }

    // A checkout of its own: a declaration, knowledge and decisions to index, and one commit, so its signature has a head to read.
    private static void Checkout(string repositories, string name)
    {
        var root = Path.Combine(repositories, name);
        Directory.CreateDirectory(Path.Combine(root, ".claude", "knowledge"));
        Directory.CreateDirectory(Path.Combine(root, "docs", "decisions"));
        File.WriteAllText(Path.Combine(root, "daoris.json"), $$"""
            {
              "source": "s", "packs": [],
              "domain": { "summary": "Keeps the {{name}} tools.", "owns": ["the {{name}} tree"], "accepts": ["a quest"] }
            }
            """);
        for (var note = 0; note < 12; note++)
        {
            File.WriteAllText(
                Path.Combine(root, ".claude", "knowledge", $"note-{note:D2}.md"),
                $"# {name} note {note}\n\n## What it says\n\nWhat {name} learned about the {Topic(note)} and step {note}.\n");
        }

        for (var decision = 0; decision < 120; decision++)
        {
            File.WriteAllText(
                Path.Combine(root, "docs", "decisions", $"D{decision + 1}.md"),
                $"# D{decision + 1} — the {Topic(decision)} rule\n\nWhy the {Topic(decision + 2)} goes first.\n");
        }

        Git(root, "init", "-q", "-b", "main");
        Git(root, "add", "-A");
        Git(root, "-c", "user.name=Bench", "-c", "user.email=bench@example.invalid", "commit", "-q", "-m", "init");
    }

    private static void Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var git = Process.Start(start)!;
        var said = git.StandardError.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed in {root}: {said}");
    }
}
