using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class KnowledgeHelpReads : IKnowledgeHelp
{
    [ModuleInitializer]
    internal static void Install() => KnowledgeHelp.Current = new KnowledgeHelpReads();

    public string? ScopeOf(string? workspace) => HelpRoom.ScopeOf(workspace);
    public string? GoesOnIn(string home, string? workspace, string? tree) => HelpRoom.GoesOnIn(home, workspace, tree);
    public bool SaidNotReadYet(string home) => HelpRoom.SaidNotReadYet(home);
    public IReadOnlyList<string?> Unread(string home) => HelpRoom.Unread(home);
    public string Prepare(string home, HelpMachine machine) => HelpRoom.Prepare(home, machine);
    public IReadOnlyList<HelpRead> HandedCheckouts(string home, string session) => new HelpWired(home).Read(session).FirstOrDefault()?.Checkouts ?? [];
    public IReadOnlyList<(string Id, string Name)> Views => HelpPlaces.Views;
    public IReadOnlyList<(string Id, string Name)> Domains => HelpPlaces.Domains;
    public IReadOnlyList<(string Within, string Id, string Name)> Parts => HelpPlaces.Parts;
    public string AskItem => HelpPlaces.AskItem;
    public string OfferItem => HelpPlaces.OfferItem;
}
