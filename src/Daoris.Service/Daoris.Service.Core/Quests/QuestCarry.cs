namespace Daoris.Knowledge;

/// <summary>What a quest carries beside its words: links and files, judged once for every door, and the bytes kept after the record exists.</summary>
internal sealed class QuestCarry(QuestFiles? files)
{
    /// <summary>
    /// The judgement over what a quest carries — one place, because an HTTP door, the MCP door and a
    /// relay from another machine all arrive here, and a limit two of them enforced would be a limit
    /// the third quietly did not.
    /// </summary>
    public QuestCarried Judge(QuestAsk ask)
    {
        var links = new List<string>();
        foreach (var given in ask.Links)
        {
            var link = given.Trim();
            if (link.Length == 0 || links.Contains(link, StringComparer.Ordinal)) continue;

            // Shown as a link, so it must BE an address: a `javascript:` "link" in a drawer is a
            // script, and a `file:` one names somebody's disk.
            if (link.Length > QuestExchange.MaxLinkLength
                || !Uri.TryCreate(link, UriKind.Absolute, out var address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                return Refuse(
                    QuestPublishRefusal.BadLink,
                    $"`{Clip(link)}` is not a link a quest can carry — a link is an absolute http or https "
                    + $"address of at most {QuestExchange.MaxLinkLength} characters. Put anything else in the body.");
            }

            links.Add(link);
        }

        if (links.Count > QuestExchange.MaxLinks)
        {
            return Refuse(
                QuestPublishRefusal.BadLink,
                $"A quest carries at most {QuestExchange.MaxLinks} links — this one was given {links.Count}.");
        }

        if (ask.Uploads.Count > 0 && files is null)
        {
            // No home, no files: writing them somewhere nobody pointed this host is refused.
            // A shared deployment never reaches here — its door takes names only.
            return Refuse(QuestPublishRefusal.NoHome, DaorisHome.Sentence);
        }

        var attachments = new List<QuestAttachment>();
        foreach (var named in ask.Named)
        {
            // A name that arrives already described is made safe by the machine that kept the file;
            // one that is not safe is never kept by Daoris, and a record naming it would lie.
            if (named.Name != QuestFiles.SafeName(named.Name)
                || named.Sha256.Length != 64 || !named.Sha256.All(Uri.IsHexDigit) || named.Sha256 != named.Sha256.ToLowerInvariant()
                || named.Bytes < 0)
            {
                return Refuse(
                    QuestPublishRefusal.BadAttachment,
                    $"`{Clip(named.Name)}` is not a file a record can name — an attachment by name carries a "
                    + "safe file name, its lowercase sha256 and its size.");
            }

            attachments.Add(named);
        }

        attachments.AddRange(ask.Uploads.Select(QuestFiles.Describe));

        // The same content twice is one file — its hash is its identity; the first name given wins.
        var distinct = attachments
            .GroupBy(a => a.Sha256, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (distinct.Count > QuestExchange.MaxAttachments)
        {
            return Refuse(
                QuestPublishRefusal.BadAttachment,
                $"A quest carries at most {QuestExchange.MaxAttachments} files — this one was given {distinct.Count}.");
        }

        var total = distinct.Sum(a => a.Bytes);
        if (total > QuestExchange.MaxAttachmentBytes)
        {
            return Refuse(
                QuestPublishRefusal.BadAttachment,
                $"A quest's files come to at most {Megabytes(QuestExchange.MaxAttachmentBytes)} together — these come to "
                + $"{Megabytes(total)}. Carry a link to the large ones instead.");
        }

        return new(null, "", links, distinct);

        static QuestCarried Refuse(QuestPublishRefusal refusal, string message) => new(refusal, message, [], []);
    }

    /// <summary>
    /// The same judgement over what an ASK carries — an ask keeps its links and files until
    /// it becomes quests, and a limit it did not share with them would be refused only later, at a
    /// publish nobody is watching.
    /// </summary>
    public (QuestPublishRefusal? Refusal, string Message, IReadOnlyList<string> Links) JudgeCarry(
        IReadOnlyList<string> links, IReadOnlyList<QuestUpload> uploads)
    {
        var carried = Judge(new QuestAsk("", "", "", "") { Links = links, Uploads = uploads });
        return (carried.Refusal, carried.Message, carried.Links);
    }

    /// <summary>
    /// Keep the bytes the record names, under THIS machine's home — after the record exists, so a
    /// refused publish kept nothing. Answers what the asker should hear about it, or nothing.
    /// </summary>
    /// <remarks>
    /// Only bytes the record actually names are kept: a quest published before, under the same title,
    /// is answered as it stands, and what this call carried beyond it is SAID rather than
    /// quietly dropped. A file the disk refuses leaves the quest published and says which — the
    /// record's name for it is honest either way, because every reader asks whether it is here.
    /// </remarks>
    public async Task<string> KeepAsync(Quest quest, QuestAsk ask, QuestCarried carried, CancellationToken ct)
    {
        var onRecord = quest.Attachments.Select(a => a.Sha256).ToHashSet(StringComparer.Ordinal);
        var notKept = new List<string>();
        foreach (var upload in ask.Uploads)
        {
            if (!onRecord.Contains(QuestFiles.Describe(upload).Sha256)) continue;
            try
            {
                await files!.KeepAsync(quest.Id, upload, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                notKept.Add($"`{QuestFiles.SafeName(upload.Name)}` ({error.Message})");
            }
        }

        var said = "";
        var unadded = carried.Attachments.Where(a => !onRecord.Contains(a.Sha256)).Select(a => $"`{a.Name}`")
            .Concat(carried.Links.Where(l => !quest.Links.Contains(l, StringComparer.Ordinal)).Select(l => $"<{l}>"))
            .ToList();
        if (unadded.Count > 0)
        {
            said += $"\n\n⚠ `#{quest.Id}` was already published, and a quest carries what it was first published "
                    + $"with — not added: {string.Join(", ", unadded)}. A new ask is a new title.";
        }

        if (notKept.Count > 0)
        {
            said += $"\n\n⚠ Published, but this machine could not keep: {string.Join(", ", notKept)}. The record "
                    + "names them; a session will be told they are not here.";
        }

        return said;
    }

    private static string Megabytes(long bytes) => $"{bytes / (1024.0 * 1024.0):0.#} MB";
}
