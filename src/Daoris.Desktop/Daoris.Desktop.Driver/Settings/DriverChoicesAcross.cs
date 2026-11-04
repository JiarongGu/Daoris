using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>The standing choices' <c>across</c> verb (READ1, D107): whether a checkout is read by agents outside it, and the writes declared into another.</summary>
internal static class DriverChoicesAcross
{
    public static ChoicePress Across(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var workspace = FlagValue(args, "--workspace");
        var clear = args.Contains("--clear");
        var words = Operands(args, ["--workspace"]).Skip(1).ToList();
        string? name = workspace;
        if (name is null && words.Count > 0)
        {
            name = words[0];
            words.RemoveAt(0);
        }

        var what = words.ElementAtOrDefault(0);
        var value = words.ElementAtOrDefault(1);
        if (string.IsNullOrEmpty(name))
        {
            throw new ChoiceWords("`driver across` needs <repository>|--workspace <name>, then read on|off|--clear or write-to <other> "
                + "[--clear] — e.g. `daoris place across engine read off`.");
        }

        if (what == "read")
        {
            if (!clear && value is not ("on" or "off"))
            {
                throw new ChoiceWords("`driver across … read` needs on|off|--clear — e.g. `daoris place across engine read off`.");
            }

            bool? set = clear ? null : value == "on";
            var payload = Scoped(workspace, name);
            payload["read"] = set;
            return new("across", ChoiceRoutes.ReadAcross, payload, _ =>
            {
                StandingChoices.Write(path, config => workspace is not null ? config.WithWorkspaceReadAcross(workspace, set) : config.WithReadAcross(name, set));
                return Task.FromResult<object?>(null);
            }, _ => null, _ =>
            {
                var whose = workspace is not null ? $"each checkout in the workspace `{workspace}` that sets none of its own is" : $"`{name}`'s checkout is";
                if (set is null)
                {
                    Say(workspace is not null
                        ? $"daoris: checkouts in the workspace `{workspace}` are read across again, unless one says otherwise."
                        : $"daoris: `{name}` takes its workspace's reading again, else on.");
                }
                else if (set == true)
                {
                    Say($"daoris: {whose} read by sessions in its workspace's other repositories and by Ask Daoris —");
                    Say("  its files and `git status` and the branch list, never a write.");
                }
                else
                {
                    Say($"daoris: {whose} read by no agent outside it: no session in another repository, and not Ask Daoris.");
                }

                if (set is not null && workspace is not null)
                {
                    Say("  A repository with a setting of its own keeps it — `daoris place across <repository> read --clear` hands it back.");
                }

                Say("  A session already running keeps what it started with.");
                Say($"  Written to {path} — the driver reads it at every start, so nothing restarts.");
                return 0;
            });
        }

        if (what == "write-to")
        {
            if (workspace is not null)
            {
                throw new ChoiceWords("a relationship is declared from one repository — `daoris place across <repository> write-to <other>`.");
            }

            if (AcrossDeclarations.Problem(name, value ?? "") is { } problem) throw new ChoiceWords(problem);
            return new("across", ChoiceRoutes.WriteAcross, new JsonObject { ["repository"] = name, ["to"] = value, ["allow"] = !clear }, _ =>
            {
                StandingChoices.Write(path, config => config.WithWriteAcross(name, value!, !clear));
                return Task.FromResult<object?>(null);
            }, _ => null, _ =>
            {
                if (clear)
                {
                    Say($"daoris: sessions in `{name}` no longer write into `{value}`; a change needed there is a quest again.");
                }
                else
                {
                    Say($"daoris: sessions in `{name}` may also write into `{value}` — its files, and a commit there —");
                    Say("  where both are in one workspace with a checkout here. This is your standing say-so for writing");
                    Say($"  across; it has one direction, so `{value}` writes nothing into `{name}` unless you declare that too.");
                    Say($"  `daoris place across {name} write-to {value} --clear` takes it back.");
                }

                Say($"  Written to {path} — the driver reads it at every start, so nothing restarts.");
                return 0;
            });
        }

        throw new ChoiceWords($"`driver across` sets read or write-to, not `{what ?? ""}` — e.g. `daoris place across engine read off`, "
            + "`daoris place across plugins write-to engine`.");
    }
}
