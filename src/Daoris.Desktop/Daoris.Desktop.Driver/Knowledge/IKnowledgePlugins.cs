namespace Daoris.Driver;

/// <summary>The catalogue's answers to the room: eligible landing plugins and an installed plugin's source kind.</summary>
internal interface IKnowledgePlugins
{
    IReadOnlyList<string> LandingPlugins();
    string? SourceKind(string folder);
}
