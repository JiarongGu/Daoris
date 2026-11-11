namespace Daoris.Driver;

/// <summary>
/// A creation's plan as the window reads it (<c>REGISTRY_CREATE_PLAN</c>): the ask as it resolved, the folder, the line and the identity, the steps
/// in order, every refusal at once, the digest a Confirm pins and the first lines of each quest the press publishes. The plan's values in its own
/// words; nothing is judged here.
/// </summary>
public static class CreatePlanWire
{
    public static object Of(CreatePlanned plan, DateOnly day) => new
    {
        Asked = new
        {
            plan.Asked.Name, plan.Asked.Purpose, plan.Asked.Kind, plan.Asked.Workspace, plan.Asked.Under, plan.Asked.Line, plan.Asked.Work,
        },
        plan.Workspace,
        plan.Folder,
        plan.Line,
        Author = plan.Author is { } author ? new { author.Name, author.Email } : null,
        plan.RemoteStep,
        plan.Resumes,
        Steps = plan.Steps.Select(step => new { step.Id, step.Said }).ToArray(),
        Refusals = plan.Refusals.Select(refusal => new { refusal.Code, refusal.Sentence }).ToArray(),
        Applies = plan.Applies,
        plan.Digest,
        Quests = CreateQuests.Of(plan.Asked, day).Select(quest => new { quest.Role, quest.Title, quest.Head }).ToArray(),
    };
}
