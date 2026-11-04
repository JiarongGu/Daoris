using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a plugin answered at a landing (WSR4, D100): whether it pushed the branch, the pull request it
/// opened where it opened one, and its own sentence.
/// </summary>
/// <param name="Failed">
/// The plugin's step did not complete — it could not start, answered late or wrongly, or refused the
/// call — and <paramref name="Message"/> is Daoris's sentence saying which. A plugin that answered it
/// did not push is not a failure of the wire: its own sentence says why.
/// </param>
public sealed record PluginLanding(string Plugin, bool Pushed, string? PullRequest, string Message, bool Failed = false);
