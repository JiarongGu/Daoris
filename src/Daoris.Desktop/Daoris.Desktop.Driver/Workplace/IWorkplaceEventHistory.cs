namespace Daoris.Driver;

/// <summary>The supplied conversation record's removal, including its retained event numbering.</summary>
public interface ISessionEventRemoval
{
    bool Forget(string sessionId);
}

/// <summary>The supplied record's existing writes, complete reads, existence and opening words.</summary>
public interface IWorkplaceEventHistory : ISessionEventHistory, ISessionEventRemoval
{
    bool Exists(string sessionId);
    IReadOnlyDictionary<string, string> Openings(IEnumerable<string> sessionIds);
}
