namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver sessions partner</c> (COAGENT1b, D199 point 2; the co-agent design §6.4): the person's terminal door to a piece.
/// <c>partner &lt;session&gt; "&lt;piece&gt;" --agent &lt;name&gt; [--paths &lt;path&gt;…]</c> asks one, by the judge and writer the page's press
/// shares (<see cref="PartnerPersonAsk"/>), reaching the running Daoris as the sessions family's presses do. <c>partner &lt;session&gt;
/// --list</c> shows the session's pieces and where each stands, read off the pieces' box. <c>partner &lt;piece&gt; --stop</c> is its
/// partner's record's stop, the same press as <c>sessions stop</c>: the driver reads it as the person's and keeps the work.
/// </summary>
/// <remarks>
/// <para>The design's <c>--discard</c> and <c>--hand-over</c> wait for the acts they name (a piece's ref removed in one verified
/// transaction, an uncertain piece's work handed), which no sub-feature builds yet; the words refuse them as unknown.</para>
/// <para>Exit codes are the family's: 0 asked, listed or stopped · 1 refused, a policy answer · 2 could not, the usage among them.</para>
/// </remarks>
public static class SessionsPartnerCommand
{
    private const string UsageProblem =
        "`partner` takes one session's id, the piece in quotes and `--agent <name>`, with `--paths <path>…` where the piece is about some; "
        + "or a session's id and `--list`; or a piece's id and `--stop`.";

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static SessionsAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        switch (args)
        {
            case ["partner", var id, "--list"] when Word(id):
                return new SessionsAsk("partner-list") { Ids = [id] };
            case ["partner", var piece, "--stop"] when Word(piece):
                return new SessionsAsk("partner-stop") { Ids = [piece.TrimStart('#')] };
            case ["partner", var id, var words, ..] when Word(id) && Word(words):
            {
                var rest = args.Skip(3).ToList();
                string? agent = null;
                var paths = new List<string>();
                for (var at = 0; at < rest.Count; at++)
                {
                    switch (rest[at])
                    {
                        case "--agent" when at + 1 < rest.Count && Word(rest[at + 1]) && agent is null:
                            agent = rest[++at].Trim();
                            break;
                        case "--paths" when at + 1 < rest.Count && Word(rest[at + 1]):
                            while (at + 1 < rest.Count && Word(rest[at + 1])) paths.Add(rest[++at].Trim());
                            break;
                        default:
                            problem = UsageProblem;
                            return null;
                    }
                }

                if (agent is null)
                {
                    problem = UsageProblem;
                    return null;
                }

                return new SessionsAsk("partner") { Ids = [id], Text = words.Trim(), Agent = agent, Paths = paths };
            }
        }

        problem = UsageProblem;
        return null;
    }

    /// <summary>Write the ask for the driver's look, or say why not.</summary>
    public static async Task<int> RunAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct = default)
    {
        PartnerAsked asked;
        try
        {
            asked = await PartnerPersonAsk.AskAsync(
                world.Home, world.Service, ask.Ids[0], ask.Text ?? "", ask.Paths, ask.Agent, world.PieceBase, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException)
        {
            output.WriteLine($"sessions: the service did not answer ({error.Message}), so nothing was asked.");
            return 2;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"sessions: the ask could not be written under the home ({error.Message}), so nothing was asked.");
            return 2;
        }

        output.WriteLine($"sessions: {asked.Message}");
        return asked.Asked is null ? 1 : 0;
    }

    /// <summary>
    /// <c>partner &lt;session&gt; --list</c>: each piece the session asked, oldest first, with where it stands, the agent it went to, its
    /// partner's record and its frozen ref once imported. Read off the box, so it says the same with or without a running Daoris.
    /// </summary>
    public static Task<int> ListAsync(SessionsWorld world, string session, TextWriter output)
    {
        var files = new PartnerFiles(world.Home);
        var pieces = files.Asks().Where(ask => string.Equals(ask.Asker, session, StringComparison.Ordinal)).ToList();
        if (pieces.Count == 0)
        {
            output.WriteLine($"sessions: session {session} has asked no piece.");
            return Task.FromResult(0);
        }

        output.WriteLine($"sessions: session {session}'s pieces, oldest first:");
        foreach (var ask in pieces)
        {
            var state = files.ReadState(ask.Id);
            var stands = state?.State ?? (files.Unreadable(ask.Id, "state") ? PartnerStates.Uncertain : PartnerStates.Asked);
            var line = $"  #{ask.Id}  {stands}{(state?.Code is { } code ? $" ({code})" : "")}  {state?.Agent ?? ask.Agent ?? "no agent named"}";
            if (state?.Partner is { } partner) line += $"  partner {partner}";
            if (state?.Commit is { Length: > 0 } commit) line += $"  {TextBounds.ShortCommit(commit, 7)} at {PieceRefs.Of(ask.Id)}";
            output.WriteLine(line);
            output.WriteLine($"      {TextBounds.Clip(TextBounds.OneLine(ask.Piece), 100, wholePairs: true)}");
        }

        return Task.FromResult(0);
    }

    /// <summary>
    /// The partner's record a piece's stop ends: only a piece started and not yet ended has one running. Null, said, where there is
    /// none to stop.
    /// </summary>
    public static string? Working(SessionsWorld world, string piece, TextWriter output)
    {
        var files = new PartnerFiles(world.Home);
        if (files.ReadAsk(piece) is null)
        {
            output.WriteLine($"sessions: there is no piece `#{piece}` on this machine. Nothing was stopped.");
            return null;
        }

        if (files.ReadState(piece) is not { State: PartnerStates.Started, Partner: { } partner })
        {
            var stands = files.ReadState(piece)?.State ?? PartnerStates.Asked;
            output.WriteLine($"sessions: piece `#{piece}` is {stands}, and only a piece whose partner works can be stopped. Nothing was stopped.");
            return null;
        }

        return partner;
    }

    private static bool Word(string word) => word.Trim().Length > 0 && !word.StartsWith('-');
}
