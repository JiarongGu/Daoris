namespace Daoris.Devkit;

/// <param name="Results">Every gate that ran, in order.</param>
/// <param name="Passed">False if any gate failed.</param>
public sealed record RunReport(IReadOnlyList<GateResult> Results, bool Passed);

/// <summary>
/// Runs the universal gates and then the repository's declared ones.
/// </summary>
/// <remarks>
/// <para><b>Universal first, declared second, and stop at the first failure.</b> The universal gates are
/// cheap and catch the things that are expensive to undo — a leaked credential, a hand-stamped version.
/// Running a twelve-minute build before discovering that a secret is staged wastes the twelve minutes
/// and, worse, trains people to start the build and walk away.</para>
///
/// <para><b>A skipped gate is reported as skipped.</b> Not as a pass. A run that says "8 passed" when
/// three of them had nothing to check is a report that reads as coverage and is not, and the drift from
/// there to "we have gates for that" takes about a week.</para>
///
/// <para><b>Disabled gates are printed on every run.</b> Turning one off is allowed — it is written down
/// in the repository, which makes it a decision. Printing it keeps the decision visible, so nobody
/// discovers a year later that the scan they were relying on was off the whole time.</para>
/// </remarks>
public sealed class GateRunner(GateContext context, IReadOnlyList<IGate> universal)
{
    public RunReport Run(Action<string> write)
    {
        var results = new List<GateResult>();
        var declaration = context.Declaration;

        foreach (var name in declaration.Disabled.OrderBy(n => n, StringComparer.Ordinal))
        {
            write($"  disabled  {name}  (declared in {GateDeclaration.FileName})");
        }

        foreach (var gate in universal)
        {
            if (declaration.Disabled.Contains(gate.Name)) continue;

            var result = Execute(gate);
            results.Add(result);
            write(Format(result));
            if (!result.Passed) return new RunReport(results, false);
        }

        foreach (var declared in declaration.Gates)
        {
            write($"  running   {declared.Name}  ({declared.Run})");
            var directory = declared.WorkingDirectory is null
                ? context.RepositoryRoot
                : context.Path(declared.WorkingDirectory);

            var code = Process.RunShell(declared.Run, directory);
            var result = code == 0
                ? GateResult.Pass(declared.Name)
                : GateResult.Fail(declared.Name, $"exited {code}");

            results.Add(result);
            write(Format(result));
            if (!result.Passed) return new RunReport(results, false);
        }

        return new RunReport(results, true);
    }

    /// <summary>
    /// A gate that throws is a failed gate, not a crashed devkit.
    /// </summary>
    /// <remarks>
    /// One gate blowing up must not take the run's report with it — the results already collected are
    /// the useful part, and an unhandled exception discards them to print a stack trace instead.
    /// </remarks>
    private GateResult Execute(IGate gate)
    {
        try
        {
            return gate.Run(context);
        }
        catch (DevkitException error)
        {
            return GateResult.Fail(gate.Name, error.Message);
        }
        catch (Exception error)
        {
            return GateResult.Fail(gate.Name, $"{error.GetType().Name}: {error.Message}");
        }
    }

    private static string Format(GateResult result)
    {
        var status = result.Skipped ? "skipped " : result.Passed ? "ok      " : "FAILED  ";
        var detail = result.Detail.Length == 0 ? "" : $"  {Indent(result.Detail)}";
        return $"  {status}  {result.Name}{detail}";
    }

    /// <summary>Continuation lines line up under the first, so a multi-line finding stays readable.</summary>
    private static string Indent(string detail) => detail.Replace("\n", "\n            ");
}
