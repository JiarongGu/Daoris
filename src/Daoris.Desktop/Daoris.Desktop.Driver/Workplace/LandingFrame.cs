using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a plugin is told at a landing (D100) — the one frame on <see cref="HookPoints.Land"/>, sent once
/// the branch exists.
/// </summary>
/// <param name="Root">The repository's own checkout on this machine, where the plugin runs its push.</param>
/// <param name="Branch">The branch Daoris just made, holding the session's work.</param>
/// <param name="Base">The line the work grew from, which a pull request goes into — null where git can name none.</param>
/// <param name="Title">What the work is called: the quest's title, else what the person first said, else null.</param>
/// <param name="Quest">The chain's first quest (WSR5), or null for a conversation.</param>
/// <param name="PullRequest">
/// The pull request the landing record holds for this branch (LAND2c, D149 point 4): one a plugin opened at the chain's first
/// landing, which this push grows, so the plugin pushes and opens no second one. Null where none is open from it yet.
/// </param>
/// <param name="AcceptedBy">
/// Who accepted the work (LAND2c, D145 point 3), one of <see cref="Daoris.Driver.AcceptedBy"/>: the description a plugin
/// writes says it, and it is not always the person who reviewed it.
/// </param>
/// <param name="Tip">
/// The commit the landing judged and put the branch at (LANDPIN2, D173's FABRIC-END7 note), which the plugin pushes to the branch
/// (<c>git push origin &lt;tip&gt;:refs/heads/&lt;branch&gt;</c>) rather than the branch as it stands at the push: a tree on a branch of
/// its own (TREEFOLLOW1) has no ref made for it, so a commit made there after the judgement would otherwise ride the push unseen.
/// Null in a frame from before it, where a plugin pushes the branch by name.
/// </param>
/// <param name="Origin">
/// <c>origin</c>'s address as the one reader gives it (GITAUTH1b, the git-auth design §4.4 and §6.7; <see cref="GitRemotes"/>): Daoris's
/// own <c>daoris::</c> prefix taken off. A plugin's tool that reads the checkout's remotes (<c>gh</c>, <c>az</c>) cannot parse the address
/// git shows under the doors, so the plugin names its repository from this. Filled by the landing before the plugin starts; null where
/// the checkout has no <c>origin</c>.
/// </param>
/// <param name="Key">
/// The delivery key (LANDNODE1e, the land node design §3), told only to a plugin that declares it keeps one: a plugin keyed by it makes no
/// second effect for a key it has answered, and <c>work/state</c> is asked with it. Null for a landing no delivery performs, and for a
/// plugin that declares no key.
/// </param>
/// <param name="Mode">
/// What the plugin is asked to do, one of <see cref="LandingModes"/>: <c>pull-request</c> pushes and opens (or grows) the pull request,
/// as every plugin did before; <c>push</c> only pushes, and is told only to a plugin that declares it.
/// </param>
public sealed record LandingFrame(
    string Repository, string Workspace, string Root, string Branch, string? Base, string? Title,
    string? Quest, string Session, IReadOnlyList<LandingCommit> Commits, string? PullRequest = null,
    string AcceptedBy = Daoris.Driver.AcceptedBy.Person, string? Tip = null, string? Origin = null,
    string? Key = null, string Mode = LandingModes.PullRequest);
