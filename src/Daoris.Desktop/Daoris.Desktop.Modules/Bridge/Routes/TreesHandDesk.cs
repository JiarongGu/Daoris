using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A session's landed branch handed to a landing plugin after its landing, behind the hand-off doors: the plan before the press, or the
/// press. The branch is the one this session's landing made and recorded; a session whose landing made none answers no branch, which is
/// information, not a failure. A refusal is an answer, as a landing's is.
/// </summary>
internal sealed class TreesHandDesk(DriverLoop loop)
{
    /// <summary>What the hand-off's plan or its press asked: the plan with <c>Said</c> for the verb's words.</summary>
    internal sealed record HandAsk(string? Id, string? Branch, string? Repository, string? Plugin, bool Press, bool Said = false);

    private readonly DriverLoop _loop = loop;

    public async Task<object?> HandOffAsync(HandAsk hand, CancellationToken cancellationToken)
    {
        // `trees hand <branch>` names a landed branch (and its repository where two hold one) where the page names a session.
        // The page's `id` reads as it always did; a `branch` is found as the verb finds it, by the record's session or branch.
        var branch = hand.Branch;
        var id = branch is null ? hand.Id ?? throw new DriverException("a hand-off names a session's `id`, or a landed `branch`.") : hand.Id;
        var named = hand.Plugin;
        var service = _loop.Service ?? throw NotReady();
        var asksPlan = !hand.Press;
        // `trees hand --plan` asks for the verb's words (`said`) where the page asks for the plan's fields.
        var terminal = asksPlan && hand.Said;
        // The plugin's lines go into the answer too (`said`), so a terminal that sent the press prints them as the alone path does.
        var pluginLines = new List<string>();
        var trees = new SessionTrees(_loop.Home, new LandingPlugins(
            _loop.Home, say: TreesLandDesk.SayTo(_loop, pluginLines), log: _loop.Log, health: _loop.Health));

        // The session's newest landing: a session lands once, and a second press is refused while its branch stands.
        LandedBranch? entry;
        if (id is not null && branch is null)
        {
            entry = trees.Recorded.Find(id).FirstOrDefault(each => string.Equals(each.Session, id, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var found = trees.Recorded.Find(branch ?? id!, hand.Repository);
            var repositories = found.Select(each => each.Repository).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (repositories.Count > 1)
            {
                // A branch name in two repositories means the person says which, as the verb says (`--repository`).
                var several = TreesWords.NamesSeveral(branch ?? id!, repositories);
                if (terminal) return new { Session = id, Said = TreesWords.HandRefused(several).ToWire() };
                return asksPlan
                    ? new { Session = id, Branch = (string?)null }
                    : (object)new { Session = id, Done = false, Message = several, Branch = (string?)null, Said = TreesWords.HandRefused(several).ToWire() };
            }

            entry = found.FirstOrDefault();
        }

        if (entry is null)
        {
            var asked = branch ?? id!;
            if (terminal) return new { Session = id, Said = TreesWords.HandRefused(TreeHand.NotLanded(asked)).ToWire() };
            return asksPlan
                ? new { Session = id, Branch = (string?)null }
                : (object)new { Session = id, Done = false, Message = TreeHand.NotLanded(asked), Branch = (string?)null, Said = TreesWords.HandRefused(TreeHand.NotLanded(asked)).ToWire() };
        }

        id ??= entry.Session;
        var root = await TreesRoutes.CheckoutOfAsync(service, entry.Repository, cancellationToken);
        if (root is null)
        {
            var none = TreesWords.HandNoCheckout(entry.Repository, entry.Branch);
            if (terminal) return new { Session = id, Said = TreesWords.HandRefused(none).ToWire() };
            return asksPlan
                ? new { Session = id, Branch = (string?)entry.Branch, entry.Repository, Plugin = (string?)null, Problem = (string?)none, entry.PullRequest, Commits = 0 }
                : (object)new { Session = id, Done = false, Message = none, Branch = (string?)entry.Branch, Said = TreesWords.HandRefused(none).ToWire() };
        }

        if (asksPlan)
        {
            var plan = await trees.HandPlanAsync(root, entry, named, cancellationToken);
            if (terminal) return new { Session = id, Said = TreesPlanWords.HandPlan(plan, pluginLines).ToWire() };
            return new { Session = id, Branch = (string?)plan.Branch, plan.Repository, plan.Plugin, plan.Problem, plan.PullRequest, plan.Commits };
        }

        PressSteps.Reach(PressSteps.HandOff);
        using var waiting = PressSteps.WhileWaiting(PressSteps.HandOff);
        var handed = await TreesRoutes.HandAsync(_loop, trees, root, entry, named, cancellationToken);
        return new
        {
            Session = id, Done = handed.Handed, handed.Message, Branch = (string?)handed.Branch,
            Plugin = handed.Plugin is { } said
                ? new { Id = said.Plugin, said.Pushed, said.PullRequest, said.Message, said.Failed }
                : null,
            // what `trees hand` prints, the plugin's lines first, and its exit code (§7.3).
            Said = TreesWords.Hand(handed, pluginLines).ToWire(),
        };
    }
}
