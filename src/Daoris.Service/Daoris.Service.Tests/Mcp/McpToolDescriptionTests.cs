using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using Daoris.Knowledge.Mcp;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// What a session reads when it chooses a tool. The connector is handed to repositories that know
/// nothing of this one, so its words are the canon's, never this repository's decision numbers — the
/// rule the driven instruction already keeps.
/// </summary>
public sealed partial class McpToolDescriptionTests
{
    /// <summary>Every method of every tool class the host registers (PART10a), tool or not.</summary>
    private static IEnumerable<MethodInfo> Methods() => KnowledgeServer.ToolTypes.SelectMany(type => type.GetMethods());

    /// <summary>The name of every tool the host offers.</summary>
    private static List<string> ToolNames() =>
        Methods().Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name).OfType<string>().ToList();

    private static IEnumerable<(string Tool, string Text)> Descriptions() =>
        Methods()
            .Select(method => (Tool: method.GetCustomAttribute<McpServerToolAttribute>()?.Name,
                Method: method))
            .Where(tool => tool.Tool is not null)
            .SelectMany(tool => new[] { tool.Method.GetCustomAttribute<DescriptionAttribute>()?.Description }
                .Concat(tool.Method.GetParameters().Select(p => p.GetCustomAttribute<DescriptionAttribute>()?.Description))
                .OfType<string>()
                .Select(text => (tool.Tool!, text)));

    [Fact]
    public void No_tool_description_names_a_decision_number()
    {
        var numbered = Descriptions().Where(d => DecisionNumber().IsMatch(d.Text)).ToList();

        Assert.True(numbered.Count == 0, string.Join("\n", numbered.Select(d => $"{d.Tool}: {d.Text}")));
    }

    /// <summary>
    /// Ask and wait: a session that needs another repository is steered to ask it and wait, from each
    /// tool it might otherwise reach for — publishing, answering its own quest, and widening its rules.
    /// </summary>
    [Theory]
    [InlineData("quest_publish")]
    [InlineData("quest_respond")]
    [InlineData("permission_propose")]
    public void The_tools_a_session_reaches_for_say_to_ask_and_wait(string tool)
    {
        var text = string.Join(" ", Descriptions().Where(d => d.Tool == tool).Select(d => d.Text));

        Assert.Contains("wait", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quest_publish", tool == "quest_publish" ? "quest_publish " + text : text);
    }

    /// <summary>
    /// LAYOUT8: the set-up tool says it only proposes the press, which publishes one quest to the repository's own session and
    /// adds the rule its session needs, and that Daoris never writes into the repository; a repository is named, never a folder.
    /// </summary>
    [Fact]
    public void The_setup_tool_says_it_proposes_the_press_and_never_writes_into_the_repository()
    {
        var text = string.Join(" ", Descriptions().Where(d => d.Tool == "setup_propose").Select(d => d.Text));

        Assert.Contains("Ask Daoris only", text);
        Assert.Contains("one quest", text);
        Assert.Contains("its own session", text);
        Assert.Contains("rule", text);
        Assert.Contains("never writes into the repository", text);
        Assert.Contains("never its folder", text);
    }

    /// <summary>WORKFLOW1h-a: the workflow tool says it only proposes, wants whole steps, and names the parts not proposed yet.</summary>
    [Fact]
    public void The_workflow_tool_says_it_only_proposes_whole_steps()
    {
        var text = string.Join(" ", Descriptions().Where(d => d.Tool == "workflow_propose").Select(d => d.Text));

        Assert.Contains("Nothing changes until the person presses Apply", text);
        Assert.Contains("whole", text);
        Assert.Contains("Not proposed yet", text);
    }

    /// <summary>FABRIC-PIPELINE1i: the workflow tool says a verification pipeline takes a graph, and a route alone chooses and saves nothing.</summary>
    [Fact]
    public void The_workflow_tool_says_a_graph_and_a_route_alone()
    {
        var text = string.Join(" ", Descriptions().Where(d => d.Tool == "workflow_propose").Select(d => d.Text));

        Assert.Contains("verification pipeline", text);
        Assert.Contains("graph", text);
        Assert.Contains("`kind:<id>`", text);
        Assert.Contains("saving nothing", text);
    }

    /// <summary>WORKFLOW1h-name: the workflow tool says a new workflow is named by the person's words, and an edit keeps its name.</summary>
    [Fact]
    public void The_workflow_tool_says_a_new_workflow_takes_the_persons_name()
    {
        var text = string.Join(" ", Descriptions().Where(d => d.Tool == "workflow_propose").Select(d => d.Text));

        Assert.Contains("name", text);
        Assert.Contains("at most 60", text);
        Assert.Contains("keeps its name", text);
    }

    /// <summary>
    /// WORKFLOW1i-a: quest_publish's kind proposal says it quotes the person's own words, is at most 300 characters, is a kind and
    /// not a workflow, and that only the person's press applies it.
    /// </summary>
    [Fact]
    public void The_publish_tool_describes_the_kind_proposal_as_a_quote_only_the_person_applies()
    {
        var text = string.Join(" ", Descriptions().Where(d => d.Tool == "quest_publish").Select(d => d.Text));

        Assert.Contains(typeof(QuestTools).GetMethod("PublishQuestAsync")!.GetParameters(), p => p.Name == "workflowProposal");
        Assert.Contains("only their press applies it", text);
        Assert.Contains("the person's own words", text);
        Assert.Contains("at most 300", text);
    }

    /// <summary>
    /// A server over one checkout says what it reads and that it matches words (ORIENT1c), in the words a
    /// session reads before it searches the files: the checkout's name, the folders it was given, the tier.
    /// </summary>
    [Fact]
    public void A_server_over_one_checkout_says_what_it_reads_and_which_tier_answers()
    {
        var checkout = Path.Combine(Path.GetTempPath(), "atlas");
        var options = new Daoris.Knowledge.ServiceOptions("root", "db", Repository: checkout, Documents: "docs", Index: "docs/index");

        var lexical = KnowledgeServer.Instructions(options, semantic: false);
        var semantic = KnowledgeServer.Instructions(options, semantic: true);

        Assert.NotNull(lexical);
        Assert.Contains("`atlas`", lexical);
        Assert.Contains("`docs/`", lexical);
        Assert.Contains("`docs/index/`", lexical);
        Assert.Contains("knowledge_search", lexical);
        Assert.Contains("words only", lexical);
        Assert.DoesNotContain("words only", semantic!);
        Assert.DoesNotMatch(DecisionNumber(), lexical);
    }

    /// <summary>
    /// HIST1b (D153, D37): removing a record is a person's act, so no connector tool clears history, and none is handed the
    /// desk that would. Ask Daoris may only propose one (HIST1f), as it proposes a delete.
    /// </summary>
    [Fact]
    public void No_tool_clears_history_or_is_handed_what_would()
    {
        var tools = ToolNames();

        Assert.Contains("quest_publish", tools);
        // HIST1f-a: `clear_propose` only writes a proposal the person applies, as `delete_propose` does.
        Assert.DoesNotContain(
            tools.Where(name => name != "clear_propose"),
            name => name.Contains("clear") || name.Contains("history") || name.Contains("forget"));
        Assert.Contains("clear_propose", tools);
        Assert.DoesNotContain(
            KnowledgeServer.ToolTypes.SelectMany(type => type.GetConstructors()).SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(Daoris.Knowledge.HistoryDesk)
                         || parameter.ParameterType == typeof(Daoris.Knowledge.ComposedService));
    }

    /// <summary>
    /// XAGENT1c (D155 points 5, 8 and 9): an agent is never the person. A second opinion has two tools, the reviewer's and the
    /// working session's, and no tool asks for one, hands one on, or answers a dispute (*Go on anyway…*, *I looked myself…*);
    /// each says the findings are claims, not facts, and that the person sees them.
    /// </summary>
    [Fact]
    public void No_tool_settles_a_second_opinion_and_both_say_its_findings_are_claims()
    {
        var tools = ToolNames();

        Assert.Equal(["opinion_answer", "opinion_give"], tools.Where(name => name.Contains("opinion")).Order());
        Assert.DoesNotContain(tools, name => name.Contains("anyway") || name.Contains("myself") || name.Contains("settle")
                                             || name.Contains("dispute") || name.Contains("reviewer"));
        foreach (var tool in new[] { "opinion_give", "opinion_answer" })
        {
            var text = string.Join(" ", Descriptions().Where(d => d.Tool == tool).Select(d => d.Text));
            Assert.Contains("claims", text);
            Assert.Contains("person sees", text);
        }
    }

    /// <summary>
    /// EVID1a (D144 §1, §3): the session a requirement will judge never writes its verdict — the driver reads the commit and
    /// posts it to a local door no connector tool reaches. What the session is told is to commit what its evidence names,
    /// since Daoris reads its last commit when it ends; and the publish says a requirement may name its evidence.
    /// </summary>
    [Fact]
    public void No_tool_records_evidence_and_the_tools_say_what_is_read()
    {
        var tools = ToolNames();
        var respond = string.Join(" ", Descriptions().Where(d => d.Tool == "quest_respond").Select(d => d.Text));
        var publish = string.Join(" ", Descriptions().Where(d => d.Tool == "quest_publish").Select(d => d.Text));

        Assert.DoesNotContain(tools, name => name.Contains("evidence") || name.Contains("verdict"));
        Assert.Contains("evidence", respond);
        Assert.Contains("last commit", respond);
        Assert.Contains("evidence", publish);
    }

    /// <summary>
    /// ORIENT2e (the orientation design §3.1–§3.2): the search says it finds where things are and its kinds name
    /// the index, and the reader takes the lines a hit names, so a session asks for the range and not the file.
    /// </summary>
    [Fact]
    public void The_search_names_the_index_and_the_reader_takes_the_lines_a_hit_names()
    {
        var search = Descriptions().Where(d => d.Tool == "knowledge_search").Select(d => d.Text).ToList();
        var get = Descriptions().Where(d => d.Tool == "knowledge_get").Select(d => d.Text).ToList();

        Assert.Contains(search, text => text.Contains("where things are"));
        Assert.Contains(search, text => text.StartsWith("Restrict to kinds:") && text.Contains("index"));
        Assert.Contains(search, text => text.Contains("lines"));
        Assert.Contains(get, text => text.Contains("lines") && text.Contains("12-30"));
        Assert.Contains(
            typeof(KnowledgeSearchTools).GetMethod(nameof(KnowledgeSearchTools.GetAsync))!.GetParameters(),
            parameter => parameter.Name == "lines" && parameter.HasDefaultValue);
    }

    /// <summary>A server over one checkout says a reading may be a hit's lines, not only an entry whole.</summary>
    [Fact]
    public void A_server_over_one_checkout_says_the_reader_takes_a_hit_s_lines()
    {
        var options = new Daoris.Knowledge.ServiceOptions("root", "db", Repository: Path.Combine(Path.GetTempPath(), "atlas"));

        Assert.Contains("the lines a hit names", KnowledgeServer.Instructions(options, semantic: false));
    }

    /// <summary>A server over a family of repositories says nothing more than its tools do, as before.</summary>
    [Fact]
    public void A_server_over_a_family_gives_no_instructions()
    {
        Assert.Null(KnowledgeServer.Instructions(new Daoris.Knowledge.ServiceOptions("root", "db"), semantic: false));
    }

    /// <summary>MIXAGENT2a-i: what a session reads of the helper tools says what each does and what it leaves to the person's rule.</summary>
    [Fact]
    public void The_helper_tools_say_the_answer_is_another_agents_claim_and_the_rule_is_the_persons()
    {
        var ask = string.Join(" ", Descriptions().Where(d => d.Tool == "helper_ask").Select(d => d.Text));
        var answer = string.Join(" ", Descriptions().Where(d => d.Tool == "helper_answer").Select(d => d.Text));

        Assert.Contains("another agent's claim", ask);
        Assert.Contains("person's rule", ask);
        Assert.Contains("changes nothing", ask);
        Assert.Contains("claim", answer);
        Assert.Contains("once", answer);
    }

    /// <summary>
    /// COAGENT1b: what a partner reads of `partner_answer` says it answers once, that its files are its piece and Daoris reads them, that
    /// its words are a claim, and that nobody asks it anything after; it names no decision.
    /// </summary>
    [Fact]
    public void The_partner_tool_says_its_files_are_its_piece_and_its_answer_a_claim_said_once()
    {
        var answer = string.Join(" ", Descriptions().Where(d => d.Tool == "partner_answer").Select(d => d.Text));

        Assert.Contains("once", answer);
        Assert.Contains("files you leave", answer);
        Assert.Contains("claim", answer);
        Assert.Contains("Nobody can ask you anything after", answer);
        Assert.DoesNotContain("D199", answer);
    }

    /// <summary>
    /// MIXAGENT1c: what a session reads of `handover_propose` says its note is its own claim, that the person's rule settles it, and
    /// that it does not end the turn; and Ask Daoris's `hand_propose`, a different tool, says nothing of it.
    /// </summary>
    [Fact]
    public void The_hand_over_tool_says_its_note_is_a_claim_and_the_rule_is_the_persons()
    {
        var propose = string.Join(" ", Descriptions().Where(d => d.Tool == "handover_propose").Select(d => d.Text));
        var hand = string.Join(" ", Descriptions().Where(d => d.Tool == "hand_propose").Select(d => d.Text));

        Assert.Contains("claim", propose);
        Assert.Contains("person's rule", propose);
        Assert.Contains("turn", propose);
        Assert.DoesNotContain("handover_propose", hand);
    }

    /// <summary>
    /// PARKVIEW1: what a session reads of `question_ask` says the person answers with a press at the conversation's end, sends a
    /// go-ahead to `go_ahead_ask`, and says the turn ends after it; and `go_ahead_ask` itself names no question tool.
    /// </summary>
    [Fact]
    public void The_question_tool_says_its_answers_are_presses_and_a_go_ahead_is_asked_elsewhere()
    {
        var question = string.Join(" ", Descriptions().Where(d => d.Tool == "question_ask").Select(d => d.Text));

        Assert.Contains("press", question);
        Assert.Contains("go_ahead_ask", question);
        Assert.Contains("end your turn", question);
        Assert.Contains("recommend", question);
    }

    /// <summary>
    /// SESSCTX1f (the session-context design §3.5): what a session reads of `line_read` says it changes nothing, reads this machine's
    /// record and never over the network, reads its own session's line and refuses any other record, and never shows the agent's
    /// thoughts; it is the one tool of its kind.
    /// </summary>
    [Fact]
    public void The_line_tool_says_it_reads_its_own_line_on_this_machine_and_changes_nothing()
    {
        var read = string.Join(" ", Descriptions().Where(d => d.Tool == "line_read").Select(d => d.Text));

        Assert.Contains("Read-only", read);
        Assert.Contains("on this machine", read);
        Assert.Contains("never over the network", read);
        Assert.Contains("this session's line", read);
        Assert.Contains("refuses any other session's record", read);
        Assert.Contains("never the agent's thoughts", read);
        Assert.Contains("before", read);
        Assert.DoesNotContain(ToolNames(), name => name.StartsWith("line_", StringComparison.Ordinal) && name != "line_read");
    }

    /// <summary>
    /// SESSONE2a (D167 point 7; the co-working design §2.2): the channel's two tools say what it is for and that work stays a quest, that
    /// another repository's message is that agent's claim, and that a read moves the mark; no tool starts a session through it.
    /// </summary>
    [Fact]
    public void The_channel_tools_say_work_stays_a_quest_and_a_message_is_a_claim()
    {
        var post = string.Join(" ", Descriptions().Where(d => d.Tool == "channel_post").Select(d => d.Text));
        var read = string.Join(" ", Descriptions().Where(d => d.Tool == "channel_read").Select(d => d.Text));

        Assert.Contains("quest_publish", post);
        Assert.Contains("never starts", post);
        Assert.Contains("4000", post);
        Assert.Contains("claim", read);
        Assert.Contains("the person's own", read);
        Assert.Contains("moves", read);
        Assert.DoesNotContain(ToolNames(), name => name.StartsWith("channel_") && name is not ("channel_post" or "channel_read"));
    }

    /// <summary>
    /// HELPMODE1c2a (the Ask Daoris modes design §4.1): the housekeeping tools are Ask Daoris's alone and act only as the person
    /// granted them; the two that remove take a repository and branch names, never a path, and say each target is judged again
    /// and nothing is forced. No tool drops a recovery ref.
    /// </summary>
    [Fact]
    public void The_housekeeping_tools_say_whose_they_are_and_what_holds_them()
    {
        var tools = ToolNames().Where(name => name.StartsWith("housekeeping_", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
        string Said(string tool) => string.Join(" ", Descriptions().Where(d => d.Tool == tool).Select(d => d.Text));

        Assert.Equal(["housekeeping_clean_up", "housekeeping_discard_branch", "housekeeping_inspect"], tools);
        Assert.All(tools, tool => Assert.Contains("Ask Daoris only", Said(tool)));
        Assert.All(tools, tool => Assert.Contains("granted", Said(tool)));
        foreach (var act in new[] { "housekeeping_clean_up", "housekeeping_discard_branch" })
        {
            Assert.Contains("never a path", Said(act));
            Assert.Contains("judged again", Said(act));
            Assert.Contains("never forced", Said(act));
        }

        Assert.DoesNotContain(ToolNames(), name => name.Contains("recovery") || name.Contains("force"));
    }

    /// <summary>
    /// HELPMODE1c3 (the Ask Daoris modes design §4.1): the chosen settings' tools are Ask Daoris's alone and act only as the person
    /// granted each setting; each changes it with no Apply, through the checks and writer Settings uses, and the change is recorded.
    /// The five ordinary settings and nothing else: never the mode, a grant, an account or a safeguard.
    /// </summary>
    [Fact]
    public void The_chosen_settings_tools_say_whose_they_are_and_that_each_change_is_recorded()
    {
        var tools = ToolNames().Where(name => name.StartsWith("settings_", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
        string Said(string tool) => string.Join(" ", Descriptions().Where(d => d.Tool == tool).Select(d => d.Text));

        Assert.Equal(["settings_cap", "settings_cooloff", "settings_language", "settings_notify", "settings_timeout"], tools);
        Assert.All(tools, tool => Assert.Contains("Ask Daoris only", Said(tool)));
        Assert.All(tools, tool => Assert.Contains("granted", Said(tool)));
        Assert.All(tools, tool => Assert.Contains("no Apply", Said(tool)));
        Assert.All(tools, tool => Assert.Contains("recorded", Said(tool)));
        Assert.Contains("repository or a workspace", Said("settings_language"));
        Assert.DoesNotContain(ToolNames(), name => name.StartsWith("settings_", StringComparison.Ordinal)
            && (name.Contains("mode") || name.Contains("grant") || name.Contains("account")));
    }

    /// <summary>
    /// HELPMODE1c4 (the Ask Daoris modes design §4.1): the checkouts' tool is Ask Daoris's alone and reads only what the person granted;
    /// it takes a repository and a path relative to its checkout, says Daoris judges each read, that git's own folder and what git
    /// ignores are refused, and that it changes nothing; it answers <c>git status</c> and <c>git branch --list</c>, and never a git
    /// that writes files through its output options.
    /// </summary>
    [Fact]
    public void The_checkout_tool_says_whose_it_is_and_what_holds_each_read()
    {
        var tools = ToolNames().Where(name => name.StartsWith("checkout_", StringComparison.Ordinal)).ToList();
        var said = string.Join(" ", Descriptions().Where(d => d.Tool == "checkout_read").Select(d => d.Text));

        Assert.Equal(["checkout_read"], tools);
        Assert.Contains("Ask Daoris only", said);
        Assert.Contains("granted", said);
        Assert.Contains("relative to its checkout", said);
        Assert.Contains("judges each read", said);
        Assert.Contains("git ignores", said);
        Assert.Contains("changes nothing", said);
        Assert.Contains("`git status`", said);
        Assert.Contains("`git branch --list`", said);
        Assert.DoesNotContain(ToolNames(), name => name.StartsWith("checkout_", StringComparison.Ordinal)
            && (name.Contains("log") || name.Contains("diff") || name.Contains("show") || name.Contains("write")));
    }

    // Not after `#`: an entry id's example (`…/DECISIONS.md#D12`) is an anchor in the reader's own
    // repository, not a citation of this one's.
    [GeneratedRegex(@"(?<![#\w])D\d{1,3}\b")]
    private static partial Regex DecisionNumber();
}
