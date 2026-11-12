using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The growth switch the person sets (<c>docs/2026-11-11-grow-department-design.md</c> §4a), the page's Settings rows and `daoris settings growth`
/// their terminal twin: whether an agent's press that opens a department or a room runs as the access mode says, is asked even in full access,
/// or is refused. The person's alone: the pipe refuses it to any session's proof in every mode (<see cref="PipeOutsideOnly"/>), and the room never
/// lists it among the verbs an agent may press.
/// </summary>
public sealed class GrowthRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// One change to the switch, as the page and the verb send it: <c>repository</c> and/or <c>workspace</c> and its <c>value</c>
    /// (<c>mode</c>, <c>asked</c> or <c>off</c>), judged by <see cref="GrowthRules.Apply"/> before anything is written. It reads nothing
    /// else, so it waits for nothing; each press after it reads the value afresh, so nothing restarts.
    /// </summary>
    [DriverRoute("SET_GROWTH", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_GROWTH", Repository = "repository", Workspace = "workspace")]
    [GroundScoped("SET_GROWTH", "growth", "workspaceGrowth", Sets = "value")]
    [Publishes("SET_GROWTH", StateScopes.Driver)]
    private object? SetGrowth(SetGrowth press)
    {
        var edit = GrowthRules.EditOf(RuleEdit.Shape(press));
        // The rule's refusals come first, in its words, before anything is written.
        GrowthRules.Apply(DriverConfig.Load(_loop.ConfigPath), edit);
        _context.Change(config => GrowthRules.Apply(config, edit));
        return DriverRoutes.State(_loop);
    }
}
