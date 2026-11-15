using System.Text;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// A provider's seeding through the layer: the session's events as the history, its context entries as Lyntai's context, rendered in
/// the Seed form by Lyntai's rendering with the provider's own writer, and its files written under the account's home, each beside its place
/// first and then moved over it. What the provider states for each door is the rendering's: the writer's word and the door's resume.
/// </summary>
/// <param name="provider">The provider's doors as the layer reads them.</param>
/// <param name="writer">The provider's writer for a conversation run in a tree, written at a moment.</param>
/// <param name="physical">Where the agent keeps its files in another form than the text a writer returns (compressed, say): the file as it is placed.</param>
internal sealed class LayerSeeding(
    AgentProviderDescriptor provider, Func<string, DateTimeOffset, IConversationSeedWriter> writer, Func<ContextFile, PhysicalSeedFile>? physical = null)
    : IConversationSeeding
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>A door that resumes a conversation placed in an account's home, as <paramref name="evidence"/> measured.</summary>
    public static Lyntai.Agents.AgentDoor Door(string id, string kind, string evidence) => new()
    {
        Id = id, Kind = kind,
        Abilities = [new AgentAbility(AgentAbilities.Resume, AgentSupport.Supported, AgentAbilitySource.Measured) { Evidence = evidence }],
    };

    /// <inheritdoc/>
    public bool Seeds(string door) =>
        new StandardContextRendering(provider, seedWriter: writer(".", DateTimeOffset.UnixEpoch)).SupportFor(ContextForm.Seed, door) == AgentSupport.Supported;

    /// <inheritdoc/>
    public SeedPlacement Place(SeedAsk ask)
    {
        var context = Context(ask.Context);
        var rendering = new StandardContextRendering(provider, seedWriter: writer(Path.GetFullPath(ask.Tree), ask.At));
        var result = rendering.Render(context, new ContextRenderRequest(ContextForm.Seed, ask.Door)
        {
            History = SeedHistory.Of(ask.History), SeedSessionId = ask.Conversation,
        });
        if (result.Rendered is not { } rendered)
        {
            return SeedPlacement.Not(ask.Conversation, result.Refusal!.Kind == ContextRefusalKind.NothingToSeed ? SeedPlacement.Empty : SeedPlacement.Unseeded);
        }

        var placed = new List<string>();
        try
        {
            foreach (var file in rendered.Files)
            {
                var (name, bytes) = physical?.Invoke(file) is { } form ? (form.Name, form.Bytes) : (file.Name, Utf8.GetBytes(file.Text));
                var target = Path.Combine(ask.Home, name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target + ".seeding", bytes);
                File.Move(target + ".seeding", target, overwrite: true);
                placed.Add(name);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Remove(ask.Home, new SeedPlacement(ask.Conversation, placed, null));
            return SeedPlacement.Not(ask.Conversation, SeedPlacement.Failed);
        }

        return new SeedPlacement(rendered.ResumeToken ?? ask.Conversation, placed, null);
    }

    /// <summary>The session's entries as Lyntai's context, an agent's as its claim; a blank entry, or an agent's of the work, is none the log takes.</summary>
    internal static SessionContext Context(IReadOnlyList<ContextEntry> entries) =>
        entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Text) && !(entry.Claim && entry.Kind == ContextKind.Work)).Aggregate(
            SessionContext.Empty,
            (context, entry) => context.Append(
                new SessionContextNote(Kind(entry.Kind), entry.Text) { Subject = string.IsNullOrWhiteSpace(entry.Subject) ? null : entry.Subject },
                entry.Claim ? SessionContextAuthor.Agent : SessionContextAuthor.Host));

    private static SessionContextKind Kind(ContextKind kind) => kind switch
    {
        ContextKind.Work => SessionContextKind.Work,
        ContextKind.Decided => SessionContextKind.Decided,
        ContextKind.Found => SessionContextKind.Found,
        ContextKind.Tree => SessionContextKind.Tree,
        _ => SessionContextKind.Standing,
    };

    /// <inheritdoc/>
    public void Remove(string home, SeedPlacement placed)
    {
        foreach (var name in placed.Files)
        {
            try
            {
                File.Delete(Path.Combine(home, name));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A file nothing resumes is left; the next seed of the same id writes over it.
            }
        }
    }
}
