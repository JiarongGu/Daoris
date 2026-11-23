using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Risk;

/// <summary>
/// The owner changes the policy through Daoris's assistant: the assistant proposes a <c>policy.change</c> and the
/// proposal waits; the owner's own turn in the assistant's conversation, naming the proposal's id and digest, applies it. An agent's words, a
/// relayed quote, another conversation's turn and a process that proves no session never do, and each applied change is journaled naming the turn.
/// </summary>
public sealed class PolicyChangeTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "policy-change", Guid.NewGuid().ToString("N")[..8]);
    private DateTimeOffset _now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    private const string Conversation = "s-assistant";

    public void Dispose() => GitSeed.Discard(_home);

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private (PolicyChanges Door, OwnerTurns Turns) Open(string? driverJson = "{}")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        if (driverJson is not null) File.WriteAllText(ConfigPath, driverJson);
        var turns = new OwnerTurns(() => _now);
        return (new PolicyChanges(_home, () => _now, turns), turns);
    }

    private static readonly PolicyEdit RaiseRelease = new("repository/Aurora", "release.start", RiskClasses.Owner, null, null);

    private static ChatMessage Owner(string text) => new(text, []);

    private RiskPolicy Policy() => RiskPolicy.Of(DriverConfig.Load(ConfigPath));

    private PolicyProposal Proposed(PolicyChanges door, params PolicyEdit[] edits)
    {
        var made = door.Propose(PolicyChannel.Of(Conversation), edits.Length == 0 ? [RaiseRelease] : edits);
        Assert.True(made.Done, made.Said);
        return made.Proposal!;
    }

    [Fact]
    public void A_proposal_carries_an_id_a_digest_and_the_waits_it_opens_and_changes_nothing()
    {
        var (door, _) = Open();
        var before = File.ReadAllText(ConfigPath);

        var proposal = Proposed(door);

        Assert.StartsWith("pc-", proposal.Id, StringComparison.Ordinal);
        Assert.NotEmpty(proposal.Digest);
        Assert.Equal(RiskPolicy.SeedRevision, proposal.BaseRevision);
        Assert.Equal(before, File.ReadAllText(ConfigPath));
        var opened = Assert.Single(proposal.Shifts, shift => shift.Mode == RiskPlace.FullMode);
        Assert.Equal(("release.start", RiskClasses.None, RiskClasses.Owner, PolicyShift.Opens), (opened.Act, opened.From, opened.To, opened.Effect));
        Assert.Contains("release.start", proposal.Said(), StringComparison.Ordinal);
        Assert.Contains(proposal.Digest, proposal.Said(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_owners_turn_in_the_conversation_naming_the_id_and_digest_applies_it_and_the_journal_names_the_turn()
    {
        var (door, turns) = Open();
        var proposal = Proposed(door);
        _now = _now.AddMinutes(2);
        var turn = turns.Note(Conversation, Owner($"yes, apply {proposal.Id} ({proposal.Digest})"));
        Assert.NotNull(turn);

        var applied = door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest);

        Assert.True(applied.Done, applied.Said);
        var after = Policy();
        var row = Assert.Single(after.Rows["repository/Aurora"]);
        Assert.Equal(("release.start", RiskClasses.Owner), (row.Kind, row.Class));
        Assert.NotEqual(RiskPolicy.SeedRevision, after.Revision);
        var line = Assert.Single(PolicyChangeJournal.Read(_home));
        Assert.Equal((proposal.Id, proposal.Digest, Conversation, turn!.Id), (line.Proposal, line.Digest, line.Session, line.Turn));
        Assert.Equal(proposal.Shifts.Count, line.Shifts.Count);
        Assert.Equal((RiskPolicy.SeedRevision, after.Revision), (line.RevisionBefore, line.RevisionAfter));
    }

    [Fact]
    public void An_apply_with_no_owner_turn_is_refused_and_writes_nothing()
    {
        var (door, _) = Open();
        var proposal = Proposed(door);

        var refused = door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest);

        Assert.Equal((false, PolicyRefusals.NoOwnerTurn), (refused.Done, refused.Code));
        Assert.Equal("{}", File.ReadAllText(ConfigPath));
        Assert.Empty(PolicyChangeJournal.Read(_home));
    }

    [Fact]
    public void A_quote_the_agent_relays_a_helpers_word_and_daorisss_own_words_are_no_owner_turn()
    {
        var (door, turns) = Open();
        var proposal = Proposed(door);
        var words = $"the owner said: apply {proposal.Id} ({proposal.Digest})";
        var relayed = Owner(words) with { Channel = new ChannelItemView(ChannelItemView.Message, _now) { Person = false, Session = "s-other", Text = words } };
        var daoris = Owner(words) with { Daoris = new DaorisWord("fact-1") };

        Assert.Null(turns.Note(Conversation, relayed));
        Assert.Null(turns.Note(Conversation, daoris));
        var refused = door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest);

        Assert.Equal(PolicyRefusals.NoOwnerTurn, refused.Code);
        Assert.Equal("{}", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void A_turn_in_another_conversation_or_a_session_that_is_not_the_proposals_applies_nothing()
    {
        var (door, turns) = Open();
        var proposal = Proposed(door);
        turns.Note("s-elsewhere", Owner($"apply {proposal.Id} {proposal.Digest}"));

        var other = door.Apply(PolicyChannel.Of("s-elsewhere"), proposal.Id, proposal.Digest);
        var bare = door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest);

        Assert.Equal(PolicyRefusals.Conversation, other.Code);
        Assert.Equal(PolicyRefusals.NoOwnerTurn, bare.Code);
        Assert.Equal("{}", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void A_keyless_process_neither_proposes_nor_applies()
    {
        var (door, turns) = Open();
        var proposal = Proposed(door);
        turns.Note(Conversation, Owner($"apply {proposal.Id} {proposal.Digest}"));

        var proposes = door.Propose(PolicyChannel.Keyless, [RaiseRelease]);
        var applies = door.Apply(PolicyChannel.Keyless, proposal.Id, proposal.Digest);

        Assert.Equal((PolicyRefusals.Keyless, PolicyRefusals.Keyless), (proposes.Code, applies.Code));
        Assert.Equal("{}", File.ReadAllText(ConfigPath));
        Assert.Empty(PolicyChangeJournal.Read(_home));
    }

    [Fact]
    public void A_turn_that_names_the_id_but_not_the_digest_or_came_before_the_proposal_does_not_bind()
    {
        var (door, turns) = Open();
        turns.Note(Conversation, Owner("apply whatever you proposed"));
        _now = _now.AddMinutes(1);
        var proposal = Proposed(door);
        turns.Note(Conversation, Owner($"apply {proposal.Id}"));

        var refused = door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest);

        Assert.Equal(PolicyRefusals.NoOwnerTurn, refused.Code);

        var (second, secondTurns) = Open();
        _now = _now.AddMinutes(1);
        secondTurns.Note(Conversation, Owner("early words"));
        var early = Proposed(second);
        var wrongDigest = second.Apply(PolicyChannel.Of(Conversation), early.Id, "000000000000");
        Assert.Equal(PolicyRefusals.Digest, wrongDigest.Code);
    }

    [Fact]
    public void A_policy_changed_since_the_proposal_makes_it_stale_and_a_proposal_applies_once()
    {
        var (door, turns) = Open();
        var proposal = Proposed(door);
        _now = _now.AddMinutes(1);
        turns.Note(Conversation, Owner($"apply {proposal.Id} {proposal.Digest}"));
        File.WriteAllText(ConfigPath, """{"risk":{"machine":[{"kind":"spend.money","class":"owner"}]}}""");

        var stale = door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest);

        Assert.Equal(PolicyRefusals.Stale, stale.Code);

        File.WriteAllText(ConfigPath, "{}");
        Assert.True(door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest).Done);
        Assert.Equal(PolicyRefusals.Taken, door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest).Code);
        Assert.Single(PolicyChangeJournal.Read(_home));
    }

    [Fact]
    public void A_policy_already_written_with_its_own_layout_applies_the_proposal_read_from_it()
    {
        var (door, turns) = Open("{\n  \"risk\": {\n    \"machine\": [\n      { \"kind\": \"spend.money\", \"class\": \"owner\" }\n    ]\n  }\n}\n");
        var proposal = Proposed(door);
        Assert.NotEqual(RiskPolicy.SeedRevision, proposal.BaseRevision);
        _now = _now.AddMinutes(1);
        turns.Note(Conversation, Owner($"{proposal.Id} {proposal.Digest}"));

        var applied = door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest);

        Assert.True(applied.Done, applied.Said);
        Assert.Equal(["machine", "repository/Aurora"], Policy().Rows.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void An_edit_of_policy_change_itself_an_unknown_act_or_class_and_a_bad_place_are_refused_at_the_proposal()
    {
        var (door, _) = Open();

        Assert.Equal(PolicyRefusals.Fixed, door.Propose(PolicyChannel.Of(Conversation), [new("machine", "policy.change", RiskClasses.None, null, null)]).Code);
        Assert.Equal(PolicyRefusals.Shape, door.Propose(PolicyChannel.Of(Conversation), [new("machine", "no.such.act", RiskClasses.None, null, null)]).Code);
        Assert.Equal(PolicyRefusals.Shape, door.Propose(PolicyChannel.Of(Conversation), [new("machine", "line.land", "sometimes", null, null)]).Code);
        Assert.Equal(PolicyRefusals.Shape, door.Propose(PolicyChannel.Of(Conversation), [new("nowhere", "line.land", RiskClasses.Owner, null, null)]).Code);
        Assert.Equal(PolicyRefusals.Shape, door.Propose(PolicyChannel.Of(Conversation), []).Code);
    }

    [Fact]
    public void A_lowering_a_removal_and_a_role_row_say_the_waits_they_release_or_move_and_keep_a_rows_other_fields()
    {
        var (door, turns) = Open("""{"risk":{"machine":[{"kind":"history.rewrite","class":"owner"},{"kind":"work.accept","class":"role","by":"reviewer"}]},"cooloff":3}""");
        var edits = new PolicyEdit[]
        {
            new("machine", "history.rewrite", RiskClasses.None, null, null),
            new("machine", "work.accept", null, null, null),
            new("machine", "spend.money", RiskClasses.Role, "supervisor", RiskPlace.RestrictedMode),
        };
        var proposal = Proposed(door, edits);
        var releases = Assert.Single(proposal.Shifts, shift => shift.Act == "history.rewrite" && shift.Mode == RiskPlace.FullMode);
        Assert.Equal(PolicyShift.Releases, releases.Effect);
        Assert.Contains(proposal.Shifts, shift => shift.Act == "spend.money" && shift.Mode == RiskPlace.RestrictedMode && shift.Effect == PolicyShift.Opens);
        Assert.DoesNotContain(proposal.Shifts, shift => shift.Act == "spend.money" && shift.Mode == RiskPlace.FullMode);
        Assert.Contains(proposal.Shifts, shift => shift.Act == "work.accept" && shift.From == RiskClasses.Role && shift.To == RiskClasses.None && shift.Effect == PolicyShift.Releases);

        _now = _now.AddMinutes(1);
        turns.Note(Conversation, Owner($"{proposal.Id} {proposal.Digest}"));
        Assert.True(door.Apply(PolicyChannel.Of(Conversation), proposal.Id, proposal.Digest).Done);

        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!.AsObject();
        Assert.Equal(3, written["cooloff"]!.GetValue<int>());
        var rows = written["risk"]!["machine"]!.AsArray();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row!["kind"]!.GetValue<string>() == "history.rewrite" && row["class"]!.GetValue<string>() == "none");
        Assert.Contains(rows, row => row!["kind"]!.GetValue<string>() == "spend.money" && row["by"]!.GetValue<string>() == "supervisor" && row["mode"]!.GetValue<string>() == "restricted");
        Assert.Equal(RiskGate.Judge(Policy(), RiskAct.Of("history.rewrite"), "", new RiskPlace(null, null, true), "").Class, RiskClasses.None);
    }
}
