using System.Text.Json;

namespace Daoris.Desktop;

// The payloads of GitRoutes' acts, each named for its route. Every one names the checkout it acts on by its <c>Repository</c>, and its
// <c>Workspace</c> where the repository sits in more than one.

/// <summary>The branches list: <c>All</c> lifts the scope to every repository, <c>Remotes</c> adds a named repository's remotes.</summary>
public sealed record GitBranchesRead(string? Repository, string? Workspace, bool All = false, bool Remotes = false);

/// <summary>One hunk picked to stage or unstage; <c>Lines</c> are the numbers of the lines picked, null for the whole hunk.</summary>
public sealed record GitHunkPicked(string? Id, JsonElement? Lines);

/// <summary>A file, hunks or lines of it staged or unstaged against the HEAD, index and source the page showed.</summary>
public sealed record GitStageAct(string? Repository, string? Workspace, string? Path, string? Head, string? Index, string? Source, IReadOnlyList<GitHunkPicked>? Picks);

/// <summary>An act the journal holds, named by its id: the act Undo reverses or a stopped rename Finish completes.</summary>
public sealed record GitActTarget(string? Repository, string? Workspace, string? Act);

/// <summary>One conflict's side taken.</summary>
public sealed record GitSidePicked(string? Id, string? Take);

/// <summary>A file's conflicts resolved, or the file marked resolved, against the stages the page showed.</summary>
public sealed record GitResolveAct(
    string? Repository, string? Workspace, string? Path, string? Head, string? Base, string? Ours, string? Theirs, string? Worktree, IReadOnlyList<GitSidePicked>? Picks);

/// <summary>A commit or an amend of the staged changes, against the HEAD and tree the page showed.</summary>
public sealed record GitCommitAct(string? Repository, string? Workspace, string? Message, string? Head, string? Tree);

/// <summary>A switch to <c>Branch</c>, undone by switching back to <c>From</c>.</summary>
public sealed record GitSwitchAct(string? Repository, string? Workspace, string? Branch, string? From, string? Head);

/// <summary>A stash saved, against the HEAD and the shared stash's tip the plan showed.</summary>
public sealed record GitStashAct(string? Repository, string? Workspace, bool Untracked, string? Message, string? Head, string? Stash);

/// <summary>A stash applied by its commit's id.</summary>
public sealed record GitStashApplyAct(string? Repository, string? Workspace, string? Id, string? Head);

/// <summary>A branch or a tag made or deleted at <c>At</c>; <c>Said</c> asks for a terminal's words.</summary>
public sealed record GitRefAct(string? Repository, string? Workspace, string? Kind, string? Name, string? At, bool Said = false);

/// <summary>A branch or a tag renamed to <c>To</c>.</summary>
public sealed record GitRefRenameAct(string? Repository, string? Workspace, string? Kind, string? Name, string? To, string? At, bool Said = false);

/// <summary>Origin's branches fetched.</summary>
public sealed record GitFetchAct(string? Repository, string? Workspace, bool Said = false);

/// <summary>
/// A branch's history edited from objects: <c>Plan</c> asks only what the edit would take, and <c>Pins</c> are the ids a plan pinned
/// (<c>tip</c>, <c>onto</c> and <c>commits</c>), free JSON the edit reads.
/// </summary>
public sealed record GitHistoryEditAct(
    string? Repository, string? Workspace, string? Edit, string? Branch, string? Target, JsonElement? Pins, bool Plan = false, bool Said = false);
