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
    /// <param name="declared">
    /// Whether to run the repository's own declared gates after the universal ones. False runs the
    /// universal half alone.
    /// </param>
    /// <remarks>
    /// 🔴 <b>A repository cannot declare this binary as one of its own gates.</b> The run would reach
    /// that row and start itself, which reaches the row again — and no entry in the declaration can
    /// say "except this one", nor should it have to. The universal half is by definition the half the
    /// declaration does not contain, so running it alone is well defined where the whole is not.
    ///
    /// The other case is a matrix: declared gates that are per-platform cannot all pass in one process
    /// on one machine, so they run as separate jobs and the universal gates still need a home.
    /// </remarks>
    public RunReport Run(Action<string> write, bool declared = true)
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

        // Named, never silent — the same rule a disabled gate follows. A run that checked half of what
        // the declaration lists and said nothing reads as coverage it is not, and the drift from there
        // to "we have gates for that" takes about a week.
        if (!declared)
        {
            if (declaration.Gates.Count > 0)
            {
                write($"  not run   {string.Join(", ", declaration.Gates.Select(g => g.Name))}  "
                    + $"(declared in {GateDeclaration.FileName}; this run is the universal gates only)");
            }

            return new RunReport(results, true);
        }

        foreach (var gate in declaration.Gates)
        {
            write($"  running   {gate.Name}  ({gate.Run})");
            var directory = gate.WorkingDirectory is null
                ? context.RepositoryRoot
                : context.Path(gate.WorkingDirectory);

            var code = Process.RunShell(gate.Run, directory);
            var result = code == 0
                ? GateResult.Pass(gate.Name)
                : GateResult.Fail(gate.Name, $"exited {code}");

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
