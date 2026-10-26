namespace Daoris.Driver;

/// <summary>The resolved start facts Agents consumes; Participants retains the judgement and its refusal wording.</summary>
public interface IAgentAccess
{
    string Permissions { get; }
    string Repository { get; }
    string? Workspace { get; }
    string? PermissionMode { get; }
    string? Posture { get; }
    string Unoffered(string named, string offers);
}
