using System.Globalization;

namespace Daoris.Driver;

/// <summary>The words of the second opinion's gate (design §8.5's table): what it says in the driver's English, and the terminal's doors it names.</summary>
public static class OpinionGateWords
{
    /// <summary>The terminal's verb for a door, naming the session where the state knows it.</summary>
    private static string Verb(OpinionGateState gate, string verb, string? words = null) =>
        $"`daoris session opinion {verb} {gate.Session ?? "<session>"}{(words is null ? "" : $" {words}")}`";

    /// <summary>
    /// What the gate says (design §8.5's table): the state, why, and the terminal's doors that move it, where it holds; what let
    /// it go, where it does.
    /// </summary>
    public static string Says(OpinionGateState gate)
    {
        var who = gate.Opinion is { } opinion ? opinion.Who : null;
        var anyway = Verb(gate, "anyway", "\"…\"");
        return gate.State switch
        {
            OpinionGateStates.None => gate.Rule is null
                ? "Nothing waits for a second opinion here."
                : $"No second opinion waits for this work: {NoneWhy(gate)}.",
            OpinionGateStates.WaitsChain =>
                $"Waits for a second opinion: it is asked once this chain's last step in `{gate.Repository}` is done, and quest `#{gate.Later}` "
                + $"still works there, so one opinion reads the chain's whole work. {Verb(gate, "ask")} asks one for this work now, or {anyway} "
                + "lets it go on without one.",
            // CHATFLOW1d (the chat-flow design §5.2): a chat run's opinion is asked by the person's press, never at a look.
            OpinionGateStates.NotAsked when gate.Run is not null => gate.Asked
                ? $"Waits for a second opinion: it is asked, and {Reviewer(gate)} reads it from the driver's next look; press Accept again once it settles."
                : $"Waits for a second opinion: one from {Reviewer(gate)} is asked when you press Accept.",
            OpinionGateStates.NotAsked => gate.Held
                ? "Waits for a second opinion: it is asked once the hold on its quest is lifted, since that comes first."
                : $"Waits for a second opinion: it is asked at the driver's next look. {Verb(gate, "ask")} asks it now.",
            OpinionGateStates.Reading =>
                $"Waits for a second opinion: being read by {who}, {LabelSaid(gate.Opinion!.Label)}"
                + (gate.Opinion.Minutes is { } minutes ? string.Create(CultureInfo.InvariantCulture, $", for at most {minutes} minutes") : "")
                + $". `daoris session opinion stop {gate.Opinion.Id}` stops it.",
            OpinionGateStates.WithSession =>
                $"Waits for a second opinion: the working session is answering {Plural(gate.Opinion!.Findings!.Count, "finding")} by {who}.",
            OpinionGateStates.ReadAgain => $"Waits for a second opinion: read again by {who}, the commits made in answer.",
            OpinionGateStates.Disputed =>
                $"{Plural(gate.Disputes!.Count, "finding")} by {who} {(gate.Disputes.Count == 1 ? "is" : "are")} disputed: a `must` the working "
                + "session did not fix and no recheck withdrew" + SinceSaid(gate) + $". {Verb(gate, "show")} lists them with their answers; your "
                + $"Accept, or your Reviewed where a look is required, answers them; {anyway} lands it over them, or "
                + $"`daoris session say {gate.Opinion!.Working} \"…\"` sends it back.",
            OpinionGateStates.CommitsSince =>
                $"{Plural(gate.Since ?? 0, "commit")} since {(gate.Since == 1 ? "was" : "were")} not read by another agent: {who} read it at "
                + $"`{Short(gate.Recheck is { Given: true } recheck ? recheck.Tip : gate.Opinion!.Tip)}`, and one pass and one recheck are "
                + $"spent for this work. Your Accept, or your Reviewed where a look is required, answers them; {anyway} lands them, or "
                + $"{Verb(gate, "ask")} asks again.",
            OpinionGateStates.Unavailable => $"No second opinion: {UnavailableWhy(gate)}. " + (gate.Required
                ? $"The rule requires one, so the work waits for you: {Verb(gate, "ask")} tries again, with `--same-agent` asks the same "
                  + $"agent in a fresh conversation, {Verb(gate, "myself", "\"…\"")} records your own reading, or {anyway} lets it land without one."
                : "The rule does not require one, so nothing waits for it."),
            OpinionGateStates.Settled => Settled(gate),
            OpinionGateStates.Anyway => $"You went on without a settled second opinion{Answering(gate)}{WordsSaid(gate.Person)}.",
            OpinionGateStates.Myself => $"You looked at it yourself in place of another agent's reading{WordsSaid(gate.Person)}.",
            OpinionGateStates.Answered => gate.Person?.Said == ReviewVerdicts.Reviewed
                ? $"Your Reviewed answered the second opinion{Answering(gate)}{WordsSaid(gate.Person)}."
                : $"Your Accept answered the second opinion{Answering(gate)}{WordsSaid(gate.Person)}.",
            // WORKFLOW1f (the workflow design §3.7): the named workflow's step sits, saying what declares it; the person's own answers
            // are the floor, as they are where no opinion can be had.
            OpinionGateStates.CannotStart => $"Waits for a second opinion, and it cannot start: {gate.Problem} {anyway} lets it land "
                + $"without one, or {Verb(gate, "myself", "\"…\"")} records your own reading.",
            _ => $"Whether this work waits for a second opinion could not be read: {gate.Problem}. Nothing lands until it can be.",
        };
    }

    /// <summary>
    /// The reviewer a chat run's press asks (CHATFLOW1d): the rule's first, in backticks, as the look walks them from it; the look may
    /// choose a later one where the first cannot read.
    /// </summary>
    internal static string Reviewer(OpinionGateState gate) =>
        gate.Rule?.Rule.Reviewers.FirstOrDefault() is { } first ? $"`{first}`" : "another agent";

    /// <summary>
    /// What a chat run's press says where it asked the opinion (CHATFLOW1d, the chat-flow design §5.2 point 3): the refusal on every door,
    /// whose words ask the person to press again once it settles.
    /// </summary>
    public static string AskedAtPress(OpinionGateState gate) =>
        $"A second opinion is asked: {Reviewer(gate)} reads it from the driver's next look; press Accept again once it settles.";

    /// <summary>What the person's answer answered: the disputes, or the commits nobody read, as the gate stood beneath it.</summary>
    private static string Answering(OpinionGateState gate) => gate.Answered switch
    {
        OpinionGateStates.Disputed => $" over {Plural(gate.Disputes?.Count ?? 0, "disputed finding")}",
        OpinionGateStates.CommitsSince => $" with {Plural(gate.Since ?? 0, "commit")} no other agent read",
        OpinionGateStates.Unavailable => " when none could be had",
        OpinionGateStates.WaitsChain or OpinionGateStates.NotAsked => " before one was asked",
        OpinionGateStates.CannotStart => " when its workflow's step could not start",
        OpinionGateStates.Reading or OpinionGateStates.WithSession or OpinionGateStates.ReadAgain => " while it was still being read",
        _ => "",
    };

    private static string Settled(OpinionGateState gate)
    {
        var opinion = gate.Opinion!;
        var read = Short(gate.Recheck is { Given: true } recheck ? recheck.Tip : opinion.Tip);
        if (opinion.Findings is { Count: 0 }) return $"Second opinion settled: {opinion.Who} raised nothing in what it read at `{read}`: {opinion.Read}";
        return $"Second opinion settled: {opinion.Who} read it at `{read}`, and its {Plural(opinion.Findings!.Count, "finding")} "
            + $"{(gate.ToPerson is null ? "were answered by the working session" : "went to you")} with no dispute open.";
    }

    private static string NoneWhy(OpinionGateState gate) =>
        gate.Rule!.Rule.IsNone
            ? $"`{gate.Repository}` is set to none"
            : $"{(gate.Rule.Source == OpinionSource.Repository ? $"`{gate.Repository}`'s rule" : "the workspace's rule")} reads before each next "
              + "step, not before landing";

    private static string UnavailableWhy(OpinionGateState gate) => gate.Code switch
    {
        ReviewerUnavailable.NoReviewer => "no listed reviewer of another maker is installed",
        ReviewerUnavailable.Cooling => "every listed reviewer of another maker is cooling"
            + (gate.Until is { } until ? $" until {until.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC, when the driver asks again" : ""),
        ReviewerUnavailable.SignedOut => "no listed reviewer of another maker has an account signed in",
        ReviewerUnavailable.NotIndependent => "no listed reviewer is another maker's agent",
        "ended" => $"{gate.Opinion?.Who ?? "its reviewer"} ended without saying one",
        "out-of-time" => $"{gate.Opinion?.Who ?? "its reviewer"} ran out of its minutes before it said one",
        _ => "no listed reviewer of another maker could read it",
    };

    private static string LabelSaid(string label) => label switch
    {
        ReviewerLabels.AnotherMaker => "another maker's agent",
        ReviewerLabels.SameAgent => "the same agent in a fresh conversation, which is not an independent reading",
        _ => "an agent whose maker is not declared, whose reading is not counted as another maker's",
    };

    private static string SinceSaid(OpinionGateState gate) =>
        gate.Since is > 0 ? $", and {Plural(gate.Since.Value, "commit")} since {(gate.Since == 1 ? "was" : "were")} not read by another agent" : "";

    private static string WordsSaid(OpinionPersonWord? word) => word?.Words is { Length: > 0 } words ? $": \"{words}\"" : "";

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string Short(string commit) => TextBounds.ShortCommit(commit, 8);
}
