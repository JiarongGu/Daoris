using System.Text;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// A provider's seeding through the layer: the session's events as the history, Daoris's record as the context's one host entry, rendered in
/// the Seed form by Lyntai's rendering with the provider's own writer, and its files written under the account's home, each beside its place
/// first and then moved over it. What the provider states for each door is the rendering's: the writer's word and the door's resume.
/// </summary>
/// <param name="provider">The provider's doors as the layer reads them.</param>
/// <param name="writer">The provider's writer for a conversation run in a tree, written at a moment.</param>
internal sealed class LayerSeeding(AgentProviderDescriptor provider, Func<string, DateTimeOffset, IConversationSeedWriter> writer) : IConversationSeeding
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
        var context = string.IsNullOrWhiteSpace(ask.Record)
            ? SessionContext.Empty
            : SessionContext.Empty.Append(new SessionContextNote(SessionContextKind.Standing, ask.Record), SessionContextAuthor.Host);
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
                var target = Path.Combine(ask.Home, file.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target + ".seeding", file.Text, Utf8);
                File.Move(target + ".seeding", target, overwrite: true);
                placed.Add(file.Name);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Remove(ask.Home, new SeedPlacement(ask.Conversation, placed, null));
            return SeedPlacement.Not(ask.Conversation, SeedPlacement.Failed);
        }

        return new SeedPlacement(rendered.ResumeToken ?? ask.Conversation, placed, null);
    }

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
