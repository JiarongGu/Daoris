namespace Daoris.Driver;

/// <summary>
/// <c>across</c> (HELP9, D107), as <c>daoris place across</c> reads its words: <c>read on|off|--clear</c> for a
/// repository or a whole workspace, or <c>write-to &lt;other&gt; [--clear]</c> from a repository, <c>--clear</c>
/// wherever it stands — said as the terminal says it, or refused in its words.
/// </summary>
/// <remarks>
/// A repository naming itself is the route's own refusal, which its edit throws. The other repository of a
/// write-to is a name a helper can invent, so it is checked here as the target is.
/// </remarks>
internal static class HelpAcrossSetting
{
    public static (string? Refusal, (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) Planned) Plan(
        string? target, string? workspace, string value, HelpMachineFacts facts)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var clear = words.Remove("--clear");
        var named = target is { Length: > 0 };
        var circle = workspace is { Length: > 0 };
        switch (words.FirstOrDefault())
        {
            case "read":
            {
                if (named == circle) return ("reading across is set for a repository or a workspace — name exactly one.", default);
                bool? read = words.Count == 2 && !clear ? words[1] switch { "on" => true, "off" => false, _ => null } : null;
                if (clear ? words.Count != 1 : read is null)
                {
                    return ("`across … read` is `read on|off|--clear` — e.g. `daoris place across engine read off`.", default);
                }

                var scope = circle ? $"--workspace {ShellWord.Of(workspace!, ShellWord.Workspace)}" : target;
                var subject = circle ? $"Each checkout in workspace `{workspace}` that sets none of its own" : $"`{target}`'s checkout";
                var describe = read switch
                {
                    true => $"{subject} is read by sessions in its workspace's other repositories and by Ask Daoris: its files, "
                        + "`git status` and the branch list, never a write.",
                    false => $"{subject} is read by no agent outside it: no session in another repository, and not Ask Daoris.",
                    null => circle
                        ? $"Checkouts in workspace `{workspace}` are read across again, unless one says otherwise."
                        : $"`{target}` takes its workspace's reading again, else on.",
                };
                if (circle && read is not null) describe += " A repository with a setting of its own keeps it.";
                return (null, (describe, $"daoris place across {scope} read {(clear ? "--clear" : words[1])}",
                    c => circle ? c.WithWorkspaceReadAcross(workspace!, read) : c.WithReadAcross(target!, read)));
            }
            case "write-to":
            {
                if (circle || !named)
                {
                    return ("a relationship is declared from one repository — `daoris place across <repository> write-to <other>`.", default);
                }

                if (words.Count > 2)
                {
                    return ("`across … write-to` names one repository — `write-to <other> [--clear]`.", default);
                }

                var other = words.Count == 2 ? words[1] : "";
                if (other.Length > 0 && !string.Equals(other, target, StringComparison.OrdinalIgnoreCase)
                    && !facts.Repositories.Contains(other, StringComparer.OrdinalIgnoreCase))
                {
                    return ($"`{other}` is not registered on this machine — use a repository's name as Repositories lists it.", default);
                }

                var describe = clear
                    ? $"Sessions in `{target}` no longer write into `{other}`; a change needed there is a quest again."
                    : $"Sessions in `{target}` may also write into `{other}` — its files, and a commit there — where both are in one "
                      + "workspace with a checkout here. Applying it is your standing say-so for writing across, one way: "
                      + $"`{other}` writes nothing into `{target}` unless you declare that too.";
                return (null, (describe, $"daoris place across {target} write-to {other}{(clear ? " --clear" : "")}",
                    c => c.WithWriteAcross(target!, other, !clear)));
            }
            default:
                return ("`across` sets `read on|off|--clear` or `write-to <other> [--clear]` — e.g. `daoris place across engine read off`, "
                    + "`daoris place across plugins write-to engine`.", default);
        }
    }
}
