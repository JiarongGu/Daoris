using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// READSHARE1: each reader of a record AtomicFile moves into place reads it while the move still holds it (MoveHold).
/// </summary>
public sealed class ReadShareConversationTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 23, 9, 12, 30, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-readshare-conversations-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>What <paramref name="read"/> answers while <paramref name="path"/> is held as its move holds it, which refuses a plain read.</summary>
    private static T Held<T>(string path, Func<T> read)
    {
        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            return read();
        }
    }

    /// <summary>What <paramref name="read"/> answers while <paramref name="path"/> is held, as <see cref="Held{T}"/> holds it.</summary>
    private static async Task<T> HeldAsync<T>(string path, Func<Task<T>> read)
    {
        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            return await read();
        }
    }

    /// <summary>How long a hold lasts under a reader that writes the file back: far past its read, well within its rename's retries.</summary>
    private const int HoldMs = 500;

    /// <summary>
    /// <paramref name="edit"/> run while <paramref name="path"/> is held, the hold let go <see cref="HoldMs"/> after it starts: its read
    /// meets the hold, which a plain read is refused by at once, and its write back waits it out in AtomicFile's rename, as it does a
    /// move's moment.
    /// </summary>
    private static async Task<T> HeldWhileReadAsync<T>(string path, Func<Task<T>> edit)
    {
        var hold = MoveHold.Take(path);
        try
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            using var release = new Timer(_ => hold.Dispose(), null, HoldMs, Timeout.Infinite);
            return await edit();
        }
        finally
        {
            hold.Dispose();
        }
    }

    /// <summary><see cref="HeldWhileReadAsync{T}"/> for an edit that answers nothing.</summary>
    private static Task HeldWhileRead(string path, Action edit) => HeldWhileReadAsync(path, () =>
    {
        edit();
        return Task.FromResult(true);
    });

    // ——— The go-on files beside a session's transcript (GoOnFiles).

    /// <summary>READSHARE1: <see cref="GoOnMarks.Read"/> (GoOnFiles.ReadMark) reads a mark while its move into place still holds it.</summary>
    [Fact]
    public void A_go_on_mark_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var marks = new GoOnMarks(_home);
        marks.Mark("s1", ["w1", "w2"], GoOnMarks.FailedTwice, At);

        var mark = Held(marks.PathOf("s1"), () => marks.Read("s1"));

        Assert.Equal(["w1", "w2"], mark!.Said);
        Assert.Equal(GoOnMarks.FailedTwice, mark.Why);
    }

    /// <summary>READSHARE1: <see cref="NewSessionChoices.Read"/> (GoOnFiles.ReadChoice) reads a choice while its move into place still holds it.</summary>
    [Fact]
    public void A_new_session_choice_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var choices = new NewSessionChoices(_home);
        choices.Choose("s1", ["w1"], At);

        var choice = Held(choices.PathOf("s1"), () => choices.Read("s1"));

        Assert.Equal(["w1"], choice!.Said);
    }

    /// <summary>READSHARE1: <see cref="ReopenMarks.Told"/> (GoOnFiles.ReadTold) reads the reopens told while their move into place still holds them.</summary>
    [Fact]
    public void A_told_reopen_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var reopens = new ReopenMarks(_home);
        var reopen = new QuestReopenView("desk", 48, new QuestSubmissionRef("desk", 47), null, At, "s1");
        reopens.Mark("s1", "q1", reopen, At);

        var told = Held(reopens.PathOf("s1"), () => reopens.Told("s1", "q1", reopen));

        Assert.True(told);
    }

    /// <summary>READSHARE1: <see cref="HarnessConversations.Read"/> (GoOnFiles.ReadConversation) reads a conversation id while its move into place still holds it.</summary>
    [Fact]
    public void A_harness_conversation_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var conversations = new HarnessConversations(_home);
        conversations.Keep("s1", "claude-code", "conv-0001");

        var conversation = Held(conversations.PathOf("s1"), () => conversations.Read("s1"));

        Assert.Equal(new HarnessConversation("claude-code", "conv-0001"), conversation);
    }

    // ——— A hand-over's files (HandOverFiles, HandOverAskFiles).

    /// <summary>READSHARE1: <see cref="HandOverBox.Read"/> (HandOverFiles.ReadBox) reads a box while its move into place still holds it.</summary>
    [Fact]
    public void A_hand_over_box_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var box = new HandOverBox(_home);
        box.Write("s1", new HandOverRequest("codex-acp", "work", HandOverWhy.Press, HandOverBy.Agent, At));

        var request = Held(box.PathOf("s1"), () => box.Read("s1"));

        Assert.Equal(("codex-acp", "work", HandOverWhy.Press, HandOverBy.Agent), (request!.Agent, request.Account, request.Why, request.AskedBy));
    }

    /// <summary>READSHARE1: <see cref="HandedQuests.Read"/> (HandOverFiles.ReadHanded) reads a handed quest while its move into place still holds it.</summary>
    [Fact]
    public void A_handed_quest_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var handed = new HandedQuests(_home);
        handed.Write("q1", new HandedQuest("codex-acp", "work", "s1", HandOverWhy.Near, HandOverBy.Person, At) { Note = "The parser is done." });

        var quest = Held(handed.PathOf("q1")!, () => handed.Read("q1"));

        Assert.Equal(("codex-acp", "s1", "The parser is done."), (quest!.Agent, quest.From, quest.Note));
    }

    /// <summary>READSHARE1: <see cref="HandOverAskFiles.ReadState"/> (HandOverAskFiles.Parse) reads an ask's state while its move into place still holds it.</summary>
    [Fact]
    public void A_hand_over_asks_state_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var files = new HandOverAskFiles(_home);
        files.WriteState("s1", new HandOverAskState("a1b2c3d4", HandOverAskStates.Waiting, null, "codex-acp", At));

        var state = Held(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + HandOverAskFiles.StateSuffix), () => files.ReadState("s1"));

        Assert.Equal(("a1b2c3d4", HandOverAskStates.Waiting, "codex-acp"), (state!.Id, state.State, state.Agent));
    }

    // ——— A helper's and a partner's files.

    /// <summary>READSHARE1: <see cref="HelperFiles.ReadState"/> (HelperFiles.Parse) reads an ask's state while its move into place still holds it.</summary>
    [Fact]
    public void A_helpers_state_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var files = new HelperFiles(_home);
        files.WriteState(new HelperState("a1b2c3d4", HelperStates.Started, null, "h9helper", At) { Agent = "codex-acp" });

        var state = Held(files.PathOf("a1b2c3d4", "state"), () => files.ReadState("a1b2c3d4"));

        Assert.Equal((HelperStates.Started, "h9helper", "codex-acp"), (state!.State, state.Helper, state.Agent));
    }

    /// <summary>READSHARE1: <see cref="PartnerFiles.ReadState"/> (PartnerFiles.Parse) reads a piece's state while its move into place still holds it.</summary>
    [Fact]
    public void A_partners_state_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var files = new PartnerFiles(_home);
        files.WriteState(new PartnerState("p1b2c3d4", PartnerStates.Asked, null, At) { Agent = "codex-acp", Job = "j1" });

        var state = Held(files.PathOf("p1b2c3d4", "state"), () => files.ReadState("p1b2c3d4"));

        Assert.Equal((PartnerStates.Asked, "codex-acp", "j1"), (state!.State, state.Agent, state.Job));
    }

    // ——— A chat's start.

    /// <summary>READSHARE1: <see cref="StartRequests.RunAsync"/> (StartRequests.Read) answers a kept start while its move into place still holds it.</summary>
    [Fact]
    public async Task A_kept_start_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var starts = new StartRequests(_home);
        await starts.RunAsync("r1", () => Task.FromResult(new StartedFrom("c9", true, null, "Conversation `c9` opened.")));

        var kept = await HeldAsync(DaorisHome.Entry(_home, HomeEntry.Starts, "r1.json"),
            () => starts.RunAsync("r1", () => Task.FromException<StartedFrom>(new InvalidOperationException("the kept start was not read"))));

        Assert.Equal(("c9", true, "Conversation `c9` opened."), (kept.SessionId, kept.Sent, kept.Message));
    }

    /// <summary>
    /// READSHARE1: <see cref="StartFrom.RunAsync"/> (StartFrom.Files) hands on a file said with the words while the move that kept it
    /// still holds it.
    /// </summary>
    [Fact]
    public async Task A_kept_upload_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(root);
        var ledger = new ChatLedger().Register("engine", root);
        ledger.Chat("c1", "completed", "talk", root).Say("w1", "See the trace.", "trace.txt");
        var upload = Assert.Single(ChatFiles.Keep(_home, "c1", [new ChatUpload("trace.txt", Encoding.UTF8.GetBytes("exit 3"))]));
        var handed = new List<ChatUpload>();

        var started = await HeldAsync(upload.Path, () => StartFrom.RunAsync(
            ledger.Client(), _home, new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), output: null, "c1",
            (_, _) => Task.FromResult(new ChatStart("c9", "Conversation `c9` opened in `engine`.")),
            (_, _, files, _) =>
            {
                handed.AddRange(files);
                return true;
            }));

        Assert.True(started.Sent);
        var file = Assert.Single(handed);
        Assert.Equal(("trace.txt", "exit 3"), (file.Name, Encoding.UTF8.GetString(file.Content)));
    }

    // ——— Ask Daoris's files.

    /// <summary>READSHARE1: <see cref="HelpConversations.Read"/> reads what the person kept of a conversation while its move into place still holds it.</summary>
    [Fact]
    public void A_help_conversations_name_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var conversations = new HelpConversations(_home);
        conversations.Rename("h1", "Port check");

        var kept = Held(conversations.PathOf("h1"), () => conversations.Read("h1"));

        Assert.Equal("Port check", kept.Name);
    }

    /// <summary>READSHARE1: <see cref="HelpPublished.Read"/> (HelpPublished.Oldest) reads what an Apply published while its move into place still holds it.</summary>
    [Fact]
    public void A_help_conversations_published_list_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var published = new HelpPublished(_home);
        published.Keep("h1", new HelpPublishedEntry(At, "p1", "ask", "work") { Ask = "a1b2c3d4" });

        var read = Held(published.PathOf("h1"), () => published.Read("h1"));

        var entry = Assert.Single(read);
        Assert.Equal(("p1", "ask", "work", "a1b2c3d4"), (entry.Proposal, entry.Kind, entry.Workspace, entry.Ask));
    }

    /// <summary>READSHARE1: <see cref="HelpWired.Read"/> (HelpWired.Oldest) reads a start's receipts while their move into place still holds them.</summary>
    [Fact]
    public void A_help_conversations_receipts_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var wired = new HelpWired(_home);
        wired.Keep("h1", new HelpReceipt { At = At, Harness = "claude-code", Folder = "room", Account = "work" });

        var read = Held(wired.PathOf("h1"), () => wired.Read("h1"));

        var receipt = Assert.Single(read);
        Assert.Equal(("claude-code", "room", "work"), (receipt.Harness, receipt.Folder, receipt.Account));
    }

    /// <summary>READSHARE1: <see cref="ConfiguredAccess.Read"/> reads the person's configured access while its move into place still holds it.</summary>
    [Fact]
    public void Configured_access_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        ConfiguredAccess.Choose(_home, ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.Inspect, chosen: true);

        var access = Held(ConfiguredAccess.PathOf(_home), () => ConfiguredAccess.Read(_home));

        Assert.Equal([ConfiguredAccess.Inspect], access.Housekeeping);
    }

    /// <summary>
    /// READSHARE1: <see cref="ConfiguredAccess.Choose"/> (ConfiguredAccess.Load) edits the file as it reads while its move into place
    /// still holds it, so the grant chosen before is kept beside the one chosen now.
    /// </summary>
    [Fact]
    public async Task A_configured_access_choice_loads_the_file_its_move_into_place_still_holds()
    {
        if (!OperatingSystem.IsWindows()) return;
        ConfiguredAccess.Choose(_home, ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.Inspect, chosen: true);

        var access = await HeldWhileReadAsync(ConfiguredAccess.PathOf(_home),
            () => Task.FromResult(ConfiguredAccess.Choose(_home, ConfiguredAccess.SettingsGrant, ConfiguredAccess.Cap, chosen: true)));

        Assert.Equal([ConfiguredAccess.Inspect], access.Housekeeping);
        Assert.Equal([ConfiguredAccess.Cap], access.Settings);
    }

    // ——— Ask Daoris's proposals, each file as the service's box writes it.

    private static readonly HelpMachineFacts Facts = new(Repositories: ["engine"], Workspaces: ["default", "work"], Agents: ["claude-code"]);

    /// <summary>A proposal's file as the service's <c>HelpProposalBox</c> writes it, moved into place.</summary>
    private string Proposal(string id, string kind, string door, string? target = null, string? workspace = null)
    {
        var path = Path.Combine(HelpProposals.FolderOf(_home), $"{id}.json");
        Directory.CreateDirectory(HelpProposals.FolderOf(_home));
        AtomicFile.WriteText(path, new JsonObject
        {
            ["id"] = id, ["proposed"] = "2026-10-23T09:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = kind, ["door"] = door, ["target"] = target, ["workspace"] = workspace, ["value"] = null,
            ["sentence"] = null, ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        }.ToJsonString());
        return path;
    }

    /// <summary>READSHARE1: <see cref="HelpProposals.Pending"/> reads a conversation's proposals while their move into place still holds them.</summary>
    [Fact]
    public void A_pending_proposal_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Proposal("p1", "sync", "sync", "engine");

        var pending = Held(path, () => HelpProposals.Pending(_home, "h1"));

        var proposal = Assert.Single(pending);
        Assert.Equal(("p1", "sync", "engine", "the person asked"), (proposal.Id, proposal.Kind, proposal.Target, proposal.Why));
    }

    /// <summary>READSHARE1: <see cref="HelpProposals.Find"/> reads a proposal while its move into place still holds it.</summary>
    [Fact]
    public void A_proposal_is_found_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Proposal("p1", "sync", "sync", "engine");

        var proposal = Held(path, () => HelpProposals.Find(_home, "p1"));

        Assert.Equal(("sync", "engine", "h1", "proposed"), (proposal!.Kind, proposal.Target, proposal.Session, proposal.State));
    }

    /// <summary>READSHARE1: <see cref="HelpProposals.Settle"/> reads the proposal it settles while its move into place still holds it, every other field kept.</summary>
    [Fact]
    public async Task A_proposal_is_settled_from_the_file_its_move_into_place_still_holds()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Proposal("p1", "sync", "sync", "engine");

        await HeldWhileRead(path, () => HelpProposals.Settle(_home, "p1", "dismissed", "not now"));

        var settled = HelpProposals.Find(_home, "p1")!;
        Assert.Equal(("dismissed", "engine", "the person asked"), (settled.State, settled.Target, settled.Why));
    }

    /// <summary>
    /// READSHARE1: <see cref="HelpProposals.Drawn"/> (HelpClearProposals.Drawn) holds a clear card's units in the proposal's file it
    /// reads while its move into place still holds it.
    /// </summary>
    [Fact]
    public async Task A_clear_cards_units_are_held_in_the_file_its_move_into_place_still_holds()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Proposal("p7", "clear", "quest", "q1done00");
        var proposal = HelpProposals.Find(_home, "p7")!;
        var unit = new HistoryUnitPlan(HistoryKinds.Quest, "q1done00", "work")
        {
            Quests = ["q1done00"], Sessions = ["s1ended0"], Bytes = new HistoryBytes(1024, 0, 0, 0, 0),
        };
        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Facts with { History = [new HistoryPlan(HistoryScope.Quest, "q1done00", [unit])] });
        Assert.Null(plan.Refusal);

        await HeldWhileRead(path, () => HelpProposals.Drawn(_home, proposal, plan));

        var drawn = HelpProposals.Find(_home, "p7")!;
        Assert.Equal([new HistoryUnitName(HistoryKinds.Quest, "q1done00")], drawn.Units);
        Assert.Equal(("proposed", "the person asked"), (drawn.State, drawn.Why));
    }

    /// <summary>
    /// READSHARE1: a sync card's look (HelpSyncProposals.Keep) keeps the rows it listed in the proposal's file it reads while its move
    /// into place still holds it.
    /// </summary>
    [Fact]
    public async Task A_sync_looks_rows_are_kept_in_the_file_its_move_into_place_still_holds()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Proposal("p11", "sync", "sync", "engine");
        var proposal = HelpProposals.Find(_home, "p11")!;
        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Facts);
        var doors = new HelpStandInDoors
        {
            Listed = new SyncPlan([new LinePull("engine", "work", "main", PullKind.FastForward, "aaaaaaaa", "bbbbbbbb", 1, null, null)], [], []),
        };

        var looked = await HeldWhileReadAsync(path, () => HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None));

        Assert.True(looked.Stands);
        var kept = HelpProposals.Find(_home, "p11")!;
        Assert.Equal([("engine:main", "line", true)], kept.Listed!.Select(row => (row.Key, row.Step, row.Moves)));
        Assert.Equal(("proposed", "the person asked"), (kept.State, kept.Why));
    }

    // ——— A brainstorm's files.

    /// <summary>READSHARE1: <see cref="BrainstormFiles.PacketBytes"/> (BrainstormFiles.ReadBytes) reads a packet while its move into place still holds it.</summary>
    [Fact]
    public void A_brainstorms_packet_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var files = new BrainstormFiles(_home);
        files.WritePacket("b1", [1, 2, 3]);

        var packet = Held(files.PathOf("b1", BrainstormInstruction.PacketFile), () => files.PacketBytes("b1"));

        Assert.Equal([1, 2, 3], packet!);
    }

    /// <summary>READSHARE1: <see cref="BrainstormFiles.ReadState"/> (BrainstormFiles.Parse) reads a brainstorm's state while its move into place still holds it.</summary>
    [Fact]
    public void A_brainstorms_state_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var files = new BrainstormFiles(_home);
        files.WriteState(new BrainstormState("b1", BrainstormStates.Running, "digest1", null, At));

        var state = Held(files.PathOf("b1", "state.json"), () => files.ReadState("b1"));

        Assert.Equal((BrainstormStates.Running, "digest1"), (state!.State, state.Packet));
    }
}
