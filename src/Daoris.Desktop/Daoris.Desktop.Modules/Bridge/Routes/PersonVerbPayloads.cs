namespace Daoris.Desktop;

/// <summary>
/// A quest's end or departure as the person presses it: <c>Quest</c> by its id, with or without the <c>#</c>, and the <c>Note</c> a done
/// or a reopen keeps with their words. <c>Said</c> asks for the terminal verb's own lines.
/// </summary>
public sealed record QuestEndPress(string Quest, string? Note = null, bool Said = false);

/// <summary>
/// The person's verdict on a quest's review, <c>Verdict</c> as the service spells it (<c>reviewed</c>, <c>not-yet</c>, <c>skipped</c>), or the
/// chain's choice at the gate: an <c>Environment</c> to set the work up in, <c>on</c> being the repository's default.
/// </summary>
public sealed record QuestReviewPress(string Quest, string? Verdict = null, string? Environment = null, string? Words = null, bool Said = false);

/// <summary>A quest's own kind and workflow: neither <c>Kind</c> nor <c>Workflow</c> clears the choice.</summary>
public sealed record QuestWorkflowPress(string Quest, string? Kind = null, string? Workflow = null, string? Words = null, bool Said = false);

/// <summary>An ask's kind and workflow: neither <c>Kind</c> nor <c>Workflow</c> clears the choice.</summary>
public sealed record AskWorkflowPress(string Ask, string? Kind = null, string? Workflow = null, string? Words = null, bool Said = false);

/// <summary>An ask's review choice: <c>on</c>, <c>off</c> or an environment's name.</summary>
public sealed record AskReviewPress(string Ask, string Choice, string? Words = null, bool Said = false);

/// <summary>A page of Daoris's browser kept (<c>add</c>, with its own <c>Title</c> where given) or dropped (<c>remove</c>) on its bookmarks bar.</summary>
public sealed record BrowserFavoritePress(string Action, string Address, string? Title = null, bool Said = false);

/// <summary>One of Daoris's browser's settings, <c>use</c>, <c>links</c> or <c>extensions</c>, and what it is set to.</summary>
public sealed record BrowserSettingPress(string Setting, string Value, bool Said = false);

/// <summary>A repository taken off the registry.</summary>
public sealed record RegistryRetirePress(string Repository, bool Said = false);

/// <summary>A repository wired to the workspace named.</summary>
public sealed record RegistryRewirePress(string Repository, string Workspace, bool Said = false);

/// <summary>What an absolute <c>Folder</c>'s subdirectories propose registered, or the service's own knowledge root where none is given.</summary>
public sealed record RegistryImportPress(string? Folder = null, string? Workspace = null, bool Said = false);

/// <summary>An installed plugin replaced from <c>Folder</c>; <c>Offer</c> names the install's own offer that folder is, which is recorded as its source.</summary>
public sealed record PluginReplacePress(string Folder, string? Offer = null);
