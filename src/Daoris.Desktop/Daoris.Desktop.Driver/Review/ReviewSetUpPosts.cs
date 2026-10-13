using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A set-up step's start held where its tree lacks the procedure, and its set-ups posted with the commit Daoris read
/// (REVIEWENV1c, D154 points 4, 5 and 9; the review environment design §2.1, §2.6).
/// </summary>
/// <remarks>
/// <b>Named where its callers always named it</b> (STRUCT3c, D166's note): <c>ReviewSetUps.ProcedureMissing</c> and
/// <c>PostAsync</c> are static members of a C# 14 extension block on the Domain's <see cref="ReviewSetUps"/>, which keeps the
/// environment's choice and the planner's sit; looking in the tree and reading its <c>HEAD</c> are Review's Infrastructure's, and
/// the service's doors are Service's.
/// </remarks>
public static class ReviewSetUpPosts
{
    extension(ReviewSetUps)
    {
        /// <summary>
        /// Why a set-up step's start is held at its spawn (design §1.1, §2.1), or null where it may start: its tree holds no file at
        /// the procedure's path, which Daoris checks exists and never reads for meaning.
        /// </summary>
        public static string? ProcedureMissing(ReviewEnvironment environment, QuestView quest, string tree) =>
            ReviewRules.Holds(tree, environment.Procedure)
                ? null
                : $"set-up step `#{quest.Id}` follows `{environment.Procedure}` to reach `{environment.Name}`, and this tree holds no "
                  + $"`{environment.Procedure}`: a procedure is `{quest.To}`'s own document, and writing it is its own work.";

        /// <summary>
        /// The set-ups a set-up step's session said, posted with the commit Daoris read from its tree (design §2.6): at the end of its
        /// run, and of every later run words went on with. The tree's <c>HEAD</c> is read here, never reported; each said set-up is
        /// kept once by the service, so posting again is no move. The conversation is told what was posted, and the machine log gets
        /// <c>review.shown</c>. Never throws: the record has concluded, and the next end posts what this one could not.
        /// </summary>
        /// <param name="quest">The quest as the conclusion read it; nothing is posted for one that is no set-up step.</param>
        /// <param name="started">When the run opened, for the log's seconds until shown.</param>
        /// <param name="showing">
        /// Daoris's browser's review half, where the loop carries one (REVIEWENV1d): once a set-up is posted, the folder its session
        /// named is served to the step's tab from then until the person's verdict, and the conversation is told so. Null serves nothing.
        /// </param>
        /// <returns>What was said, or null where nothing was posted.</returns>
        public static async Task<string?> PostAsync(
            ServiceClient service, DriverConfig config, SessionEvents events, QuestView? quest, string session, string tree, string? workspace,
            DateTimeOffset started, CancellationToken ct, ReviewDesk? showing = null)
        {
            if (quest?.SetUpIn is not { } named) return null;
            Noted said;
            try
            {
                if (ReviewSetUps.Environment(config, quest, workspace) is not { } environment)
                {
                    said = ReviewNotes.NoRule(named, quest.To);
                }
                else if (await ReviewGit.TreeHeadAsync(tree, ct).ConfigureAwait(false) is not { } commit || !EvidenceCodes.IsObjectId(commit))
                {
                    said = ReviewNotes.NoCommit();
                }
                else
                {
                    var (ok, message) = await service.PostSetUpAsync(quest.Id, session, commit, environment.Kind, ct).ConfigureAwait(false);
                    said = ok ? ReviewNotes.Posted(commit, named, message, served: null) : ReviewNotes.NotPosted(message, next: false);
                    if (ok)
                    {
                        var after = await service.FindQuestAsync(quest.Id, ct).ConfigureAwait(false);
                        var newest = after?.SetUps.LastOrDefault(setUp => string.Equals(setUp.Session, session, StringComparison.OrdinalIgnoreCase));
                        service.LandingSaid(ReviewLines.Shown(
                            quest.Id, session, environment.Kind, served: newest?.Served is not null, run: newest?.Run is not null,
                            seconds: newest?.At is { } at ? (long)Math.Max(0, (at - started).TotalSeconds) : null));
                        if (showing is not null && await showing.ServeSetUpAsync(after, session, tree, environment, ct).ConfigureAwait(false) is { } served)
                        {
                            said = ReviewNotes.Posted(commit, named, message, served);
                        }
                    }
                }
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                said = ReviewNotes.NotPosted(error.Message.TrimEnd().TrimEnd('.'), next: true);
            }

            events.Keep(
                session,
                new SessionEvent { Kind = SessionEventKind.Note, Text = $"— {said.Note}", Parts = said.Parts.Count > 0 ? said.Parts : null },
                say: null);
            return said.Note;
        }
    }
}
