namespace Daoris.Driver;

/// <summary>The room's sources answer their sections and workspace projection without exposing their owners' stores.</summary>
internal interface IHelpMachineSources
{
    HelpMachine Describe(IHelpRoomSection section, HelpMachine machine);
    HelpMachine Describe(string? workspace);
}

public interface IKnowledgeParkedQuest
{
    string Quest { get; }
    string Repository { get; }
    int? Failures { get; }
}

public interface IKnowledgeHeldQuest
{
    string Quest { get; }
    string Repository { get; }
    string Session { get; }
}

internal interface IKnowledgeHelp
{
    string? ScopeOf(string? workspace);
    string? GoesOnIn(string home, string? workspace, string? tree);
    bool SaidNotReadYet(string home);
    IReadOnlyList<string?> Unread(string home);
    string Prepare(string home, HelpMachine machine);
    IReadOnlyList<HelpRead> HandedCheckouts(string home, string session);
    IReadOnlyList<(string Id, string Name)> Views { get; }
    IReadOnlyList<(string Id, string Name)> Domains { get; }
    IReadOnlyList<(string Within, string Id, string Name)> Parts { get; }
    string AskItem { get; }
    string OfferItem { get; }
}

internal static class KnowledgeHelp
{
    private static IKnowledgeHelp? _current;

    internal static IKnowledgeHelp Current
    {
        get => _current ?? throw new InvalidOperationException("no reading of Help is installed");
        set => _current = value;
    }
}
