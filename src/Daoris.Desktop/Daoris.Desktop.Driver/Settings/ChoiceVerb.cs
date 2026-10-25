namespace Daoris.Driver;

/// <summary>
/// One of <c>daoris-driver driver</c>'s verbs that a subdomain above the settings owns (FOUNDATION1 mod-iii, the design §14.2 and §14.3:
/// a verb file belongs to the subdomain whose acts it drives): the settings declare this port, the subdomain implements it beside the
/// rule it edits, and Loop wires the set into the terminal's world (<see cref="DriverChoicesWorld.Verbs"/>), so the settings' command
/// names no type of theirs. Its words are the verb's own: what it says, the press it plans and the lines it adds to <c>list</c>.
/// </summary>
public abstract class ChoiceVerb
{
    /// <summary>The verb's word, as <c>daoris driver &lt;word&gt;</c> says it.</summary>
    public abstract string Word { get; }

    /// <summary>
    /// The press <paramref name="args"/> ask for, read from the words and judged against <paramref name="before"/>, the choices as this
    /// terminal read them; a refusal of the words is <see cref="ChoiceWords"/>, nothing done.
    /// </summary>
    public abstract ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world);

    /// <summary>
    /// Where the words only read, what they say and the exit code they end with; null where they change the home, which is
    /// <see cref="Plan"/>'s. Asked only where <see cref="DriverChoicesCommand.Changes"/> says the words change nothing.
    /// </summary>
    public virtual int? Said(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world) => null;

    /// <summary>
    /// Where the words run whole, never planned (a workflow's writes act on the terminal's word, since no route takes one): the run and
    /// its exit code; null where <see cref="Plan"/> is the verb's.
    /// </summary>
    public virtual Task<int>? RunAsync(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world, CancellationToken ct) => null;

    /// <summary>The lines this verb's keys add to <c>list</c>, said where its section stands in it.</summary>
    public virtual void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
    }
}
