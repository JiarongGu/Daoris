using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// `daoris update`, the terminal's door to an install's update: what is staged, what holds for it and how the last swap ended; and
/// <c>--when-idle</c>, <c>--now</c> and <c>--cancel</c>, written to <c>$DAORIS_HOME/update.json</c> as the banner's buttons write it.
/// </summary>
/// <remarks>
/// <para><b>The install is the home's</b>: the folder above a home that is its <c>data</c>. A home named for itself — a
/// gate's, a one-off — has none, and <c>--install &lt;folder&gt;</c> names it; a verb with none still writes its word, for
/// whatever is staged when the application next looks.</para>
///
/// <para>The printing lives in the library so a test runs the whole door in-process. Exit codes: 0 said or written · 1 refused: a
/// verb from a session's terminal, nothing staged, or <c>--now</c> for a build the application refuses (<see cref="UpdateRefusal"/>),
/// said with the word still written · 2 a word it does not answer, or a status with no install to read.</para>
/// </remarks>
[CommandGroup("update", Whole = true)]
public static class UpdateCommand
{
    public const string Usage =
        "usage: daoris update [--install <folder>]                 what is staged, what holds for it, the last swap\n"
        + "       daoris update --when-idle|--now|--cancel [--install <folder>]";

    /// <param name="home">The Daoris home, where the request is written.</param>
    /// <param name="log">This host's machine log: a word said here is a line, as the screen's is.</param>
    /// <param name="session">The terminal's <c>DAORIS_SESSION_ID</c>: a session's reads, and its verb is refused as the person's.</param>
    /// <param name="said">Everything the door said, for the caller to print.</param>
    public static int Run(string[] args, string home, Func<DateTimeOffset> clock, MachineLog? log, string? session, out string said)
    {
        var output = new StringBuilder();
        var code = Run(args, home, clock, log, session, output);
        said = output.ToString();
        return code;
    }

    private static int Run(string[] args, string home, Func<DateTimeOffset> clock, MachineLog? log, string? session, StringBuilder output)
    {
        string? mode = null;
        string? install = null;
        for (var at = 0; at < args.Length; at++)
        {
            var word = args[at];
            var verb = word switch
            {
                "--when-idle" => UpdateMode.WhenIdle,
                "--now" => UpdateMode.Now,
                "--cancel" => UpdateMode.NotNow,
                _ => null,
            };
            if (verb is not null && mode is null)
            {
                mode = verb;
                continue;
            }

            if (word == "--install" && at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal) && install is null)
            {
                install = InstallUpdate.InstallAt(args[++at]);
                continue;
            }

            output.AppendLine(verb is not null
                ? "update: one of --when-idle, --now or --cancel, not two."
                : $"update: `{word}` is not a word this door answers.");
            output.AppendLine(Usage);
            return 2;
        }

        install ??= InstallUpdate.InstallOf(home);
        var manifest = install is null ? null : StagedBuild.Read(install, out _);

        if (mode is null)
        {
            if (install is null)
            {
                output.AppendLine($"update: {home} is not an install's data folder, so there is no install beside it to read; "
                    + "name one with --install <folder>.");
                return 2;
            }

            Status(install, manifest, InstallUpdate.Read(home), output);
            return 0;
        }

        if (session is { Length: > 0 })
        {
            output.AppendLine($"update: {DriverChoicesCommand.SessionsTerminal}");
            return 1;
        }

        if (install is not null && !StagedBuild.IsStaged(install))
        {
            output.AppendLine($"update: nothing is staged beside {install}, so there is nothing to install — stage a build "
                + $"with `npm run publish:desktop -- --to <install> --service --stage`.");
            return 1;
        }

        var build = manifest?.Id;
        InstallUpdate.Write(home, new UpdateRequest(mode, build, clock()));
        log?.Info("update.requested", ("mode", mode), ("door", "terminal"), ("build", build));

        var what = build is null ? "whatever is staged" : $"build {build}";
        // The application's own refusal, checked here as it checks it, so the word is never answered as taken when nothing will close.
        if (mode == UpdateMode.Now && install is not null && UpdateRefusal.Of(install, home, manifest?.Version, UpdateRefusal.Running) is var (_, why))
        {
            output.AppendLine($"update: {what} is not installed now: {why}");
            output.AppendLine("  Nothing closes. `daoris update --now` tries again once that has moved.");
            return 1;
        }

        output.AppendLine($"update: {Meaning(mode)} — for {what}.");
        if (install is null)
        {
            output.AppendLine("  No install beside this home to read: the desktop running on it takes the word for whatever is staged there.");
        }

        return 0;
    }

    private static void Status(string install, StagedManifest? manifest, UpdateRequest? request, StringBuilder output)
    {
        if (!StagedBuild.IsStaged(install))
        {
            output.AppendLine($"update: nothing is staged beside {install}.");
        }
        else if (manifest is null)
        {
            StagedBuild.Read(install, out var problem);
            output.AppendLine($"update: a build is staged beside {install}, and it will not be installed: {problem?.Sentence}");
        }
        else
        {
            var commit = manifest.Commit is null ? "" : $" ({manifest.Commit})";
            var at = manifest.At is { } staged ? $", staged {Moment(staged)}" : "";
            output.AppendLine($"update: Daoris {manifest.Version}{commit} is staged beside {install}, build {manifest.Id}{at}.");
            output.AppendLine($"  {Meaning(InstallUpdate.ModeFor(request, manifest.Id))}.");
        }

        if (StagedBuild.ReadJournal(install) is { } last)
        {
            var build = last.Id is null ? "" : $" build {last.Id}{(last.Version is null ? "" : $" ({last.Version})")}";
            var when = last.At is { } moment ? $" at {Moment(moment)}" : "";
            var outcome = last.Phase switch
            {
                SwapPhase.Installed => $"installed{build}{when}{(last.Confirmed == false ? ", which never said it came up" : "")}.",
                SwapPhase.RolledBack => $"rolled back{build}{when}: {last.Detail ?? last.Reason}",
                SwapPhase.Refused => $"refused{build}{when}: {last.Detail ?? last.Reason}",
                _ => $"{last.Phase}{build}{when}, still under way.",
            };
            output.AppendLine($"  The last swap: {outcome}");

            // What the swap found held, and for how long: the launcher writes no log, so this is where a person reads it.
            if (last.Holds is { Count: > 0 } holds) output.AppendLine($"  Held as it swapped: {string.Join("; ", holds.Select(hold => hold.Said))}.");
        }
    }

    /// <summary>What each mode means for a staged build, in the words both doors use.</summary>
    private static string Meaning(string mode) => mode switch
    {
        UpdateMode.Now => "installed now — the desktop closes, ending what runs as a close does, and starts again on the new build",
        UpdateMode.NotNow => "not now — it waits, and work starts as before; `daoris update --when-idle` installs it when idle, `--now` at once",
        _ => "installed when idle — the desktop starts nothing new, lets what runs end or park, then installs it and starts again",
    };

    private static string Moment(DateTimeOffset moment) =>
        moment.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
