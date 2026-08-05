namespace Daoris.Devkit;

/// <summary>
/// Doctrine drift — delegated to `daoris check`, never reimplemented.
/// </summary>
/// <remarks>
/// <para>This gate is four lines of real work because it must be. `daoris check` already answers "has
/// this repository's doctrine drifted", against a lock whose semantics took four corrections to get
/// right (D19). A second implementation here would be a second answer to a question that already has
/// one — which is the pathology both artefacts exist to remove, committed by the tool that removes
/// it.</para>
///
/// <para>So the devkit runs the CLI and passes on its exit code. The two artefacts stay separate: the
/// CLI knows about doctrine, the devkit knows about gates, and the only thing crossing between them is
/// a process boundary and an integer.</para>
///
/// <para>A repository with no manifest skips rather than fails. Not every repository has adopted
/// Daoris, and the devkit is useful to one that has not.</para>
/// </remarks>
public sealed class DoctrineGate : IGate
{
    public string Name => "doctrine";

    public GateResult Run(GateContext context)
    {
        if (!File.Exists(Path.Combine(context.RepositoryRoot, "daoris.json")))
        {
            return GateResult.Skip(Name, "no daoris.json — this repository has not adopted the doctrine tool");
        }

        var doctrine = context.Declaration.Doctrine;
        var arguments = new List<string>(doctrine.Arguments ?? []) { "check" };

        ProcessOutput result;
        try
        {
            result = Process.Run(doctrine.Command, arguments, context.RepositoryRoot);
        }
        catch (Exception error) when (error is DevkitException or System.ComponentModel.Win32Exception)
        {
            return GateResult.Fail(Name,
                $"'{doctrine.Command}' is declared by daoris.json but could not be run — install it with "
                + "'npm i -g daoris', name it under 'doctrine.command', or remove the manifest if this "
                + "repository no longer adopts it.");
        }

        // Exit 2 is a tool error, not a policy failure — the difference matters, because one means the
        // doctrine drifted and the other means the check never ran.
        return result.ExitCode switch
        {
            0 => GateResult.Pass(Name, result.Output.Trim()),
            1 => GateResult.Fail(Name, $"{result.Output.Trim()}\n{result.Error.Trim()}".Trim()),
            _ => GateResult.Fail(Name, $"daoris check could not run: {result.Error.Trim()}"),
        };
    }
}
