namespace Daoris.Knowledge;

/// <summary>The quest an ask becomes: its words, its carry and the exchange's judgement of it.</summary>
internal sealed class AskQuests(QuestExchange exchange, QuestFiles? askFiles)
{
    /// <summary>How long a quest's title may be when an ask's first line becomes one.</summary>
    private const int TitleLength = 100;

    /// <summary>
    /// One quest from the ask, asked BY the ask, in its circle, carrying its links and the files it
    /// kept — and, from an intake, its own words and chain beside them. The exchange judges it — who
    /// may be asked is the exchange's answer, never this desk's.
    /// </summary>
    public async Task<QuestPublishOutcome> PublishAsync(
        Ask ask, string to, DateTimeOffset now, CancellationToken ct, AskDraft? draft = null, string? session = null, bool agent = false)
    {
        var uploads = new List<QuestUpload>();
        foreach (var attachment in ask.Attachments)
        {
            if (askFiles is not null && askFiles.Has(ask.Id, attachment))
            {
                uploads.Add(new QuestUpload(
                    attachment.Name,
                    await File.ReadAllBytesAsync(askFiles.PathOf(ask.Id, attachment), ct).ConfigureAwait(false)));
            }
        }

        // The ask's own carry always travels: the person gave those, and whatever the intake read
        // beside them adds to it rather than replacing it.
        return await exchange.PublishAsync(
            new QuestAsk(
                AskDesk.SenderOf(ask.Id), to,
                draft?.Title is { } title && !string.IsNullOrWhiteSpace(title) ? title.Trim() : TitleOf(ask.Sentence),
                string.IsNullOrWhiteSpace(draft?.Body) ? BodyOf(ask) : DraftBodyOf(ask, draft.Body))
            {
                Links = [.. ask.Links.Concat(draft?.Links ?? []).Distinct(StringComparer.Ordinal)],
                Uploads = [.. uploads, .. draft?.Uploads ?? []],
                Then = draft?.Then ?? [],
                // The person's words, each with its check: the exchange judges them against this ask's.
                Requirements = draft?.Requirements ?? [],
                // The intake's short title; where it gives none, the ask's own, so the quest and the ask
                // it came from share one name. Neither, and the quest is named from the ask's words.
                Short = string.IsNullOrWhiteSpace(draft?.Short) ? ask.Short : draft.Short,
                Weight = draft?.Weight,
                // The ask's declared paths always travel, the intake's added beside them.
                Paths = [.. ask.Paths, .. draft?.Paths ?? []],
                // The chain's review choice: the exchange checks a session's quote against this ask's words.
                Review = draft?.Review,
                // The intake's kind proposal: the exchange checks its reason quotes this ask's words.
                WorkflowProposal = draft?.WorkflowProposal,
                Workspace = ask.Workspace,
                // The session publishing, as its connector names it: the intake that read the ask.
                PublishedBy = session,
                // Whether an agent publishes, apart from which session it credits.
                ByAgent = agent,
            },
            now, ct).ConfigureAwait(false);
    }

    /// <summary>The ask's first line, cut at a word if it is long — a quest's title is one line.</summary>
    private static string TitleOf(string sentence)
    {
        var line = sentence.Split('\n', 2)[0].Trim();
        if (line.Length <= TitleLength) return line;
        var cut = line[..TitleLength];
        var space = cut.LastIndexOf(' ');
        return (space > TitleLength / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }

    /// <summary>The ask's words whole, and where they came from.</summary>
    private static string BodyOf(Ask ask) =>
        $"{ask.Sentence}\n\n— asked at workspace `{ask.Workspace}` (ask `#{ask.Id}`).";

    /// <summary>
    /// An intake's words first — it read the ticket — and the person's own beneath them, verbatim: a
    /// receiver weighing a paraphrase deserves the words behind it.
    /// </summary>
    private static string DraftBodyOf(Ask ask, string words)
    {
        var quoted = string.Join("\n", ask.Sentence.Split('\n').Select(line => $"> {line.TrimEnd('\r')}"));
        return $"{words.Trim()}\n\n— asked at workspace `{ask.Workspace}` (ask `#{ask.Id}`), in the asker's words:\n\n{quoted}";
    }
}
