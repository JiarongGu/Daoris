namespace Daoris.Driver;

/// <summary>
/// <c>daoris notices --set-aside &lt;kind&gt;</c>: every row of one kind in <i>What needs you</i> set aside in one press, the person's, as
/// the window's <i>Set all aside</i> presses it. Judged where Daoris runs, since what needs the person is judged there; alone it acts on nothing.
/// </summary>
public static class NoticesAside
{
    /// <summary>The verb's usage, in <see cref="NoticesRead.Usage"/>.</summary>
    public const string Form = "--set-aside <kind>";

    /// <summary>The press, sent to the running Daoris; where none answers the verb says so and changes nothing.</summary>
    public static Task<int> RunAsync(string kind, InstanceWorld? instance, TextWriter output, TextWriter error, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(
            instance, PersonPresses.NoticeAside, InstanceDoor.Said(("rowKind", kind), ("all", true)),
            () =>
            {
                error.WriteLine("notices: what needs you is judged by the running Daoris, so nothing was set aside; start Daoris and run this again.");
                return Task.FromResult(2);
            },
            output, ct);

    /// <summary>The verb's lines for a set-aside of <paramref name="kind"/> that put <paramref name="count"/> rows aside; null where its write is refused.</summary>
    public static TreesSaid Said(string kind, int? count) => count switch
    {
        null => new([], ["notices: the notices' record could not be written this moment; run this again."], 2),
        0 => new([$"notices: no row of kind `{kind}` is waiting for you."], [], 0),
        _ => new([$"notices: set aside {count} row{(count == 1 ? "" : "s")} of kind `{kind}`."], [], 0),
    };
}
