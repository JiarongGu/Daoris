namespace Daoris.Driver;

/// <summary>Which rung of the resolution answered: the order a start asks in.</summary>
public enum ChoiceFrom
{
    /// <summary>The person picked it for this one session.</summary>
    Picked,

    /// <summary>This workspace's default.</summary>
    Workspace,

    /// <summary>This machine's default.</summary>
    Machine,

    /// <summary>Nothing set: the agent's own sign-in for an account, <c>PATH</c> for a version.</summary>
    Unset,
}
