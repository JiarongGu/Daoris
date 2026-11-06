using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>The handshake's <c>events</c> for a plugin on <c>work/event</c> (the event hook design §4.1): the grammar it speaks, the last <c>seq</c> minted, and the build.</summary>
public sealed record WorkEventHandshake(int Grammar, long Seq, string Build);

/// <summary>
/// The hook set's half of <c>work/event</c> (EVENTHOOK1b, the event hook design §4.1, §4.4): which plugins it starts for the point, the
/// handshake each is started with, and the lease's answer kept across a busy read (P6), so a plugin on the point is wanted only by the
/// loop that holds the home and a busy read neither stops it nor counts an exit.
/// </summary>
public static class WorkEventPlugins
{
    // The install's driver bundle, hashed once a process for each home: a build changes only with an update, which starts every process again.
    private static readonly ConcurrentDictionary<string, string> Built = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Lazy<IReadOnlySet<string>> Named = new(() => ProviderRegistry.Built().Names.ToHashSet(StringComparer.Ordinal));

    /// <summary>
    /// The build the handshake says: <see cref="RelayBuild.Of"/> of the <c>app/daoris-driver/</c> of the install <paramref name="home"/> is
    /// the data of, or <see cref="RelayBuild.None"/> for a home of no install.
    /// </summary>
    public static string Build(string home) => Built.GetOrAdd(Path.GetFullPath(home), full =>
        InstallRoot.InstallOf(full) is { } install ? RelayBuild.Of(Path.Combine(install, "app", "daoris-driver")) : RelayBuild.None);

    /// <summary>This build's adapter names, the only ones an ending may carry.</summary>
    public static IReadOnlySet<string> Adapters => Named.Value;

    /// <summary>Whether the plugin's manifest names the point.</summary>
    public static bool Declares(PluginEntry plugin) => plugin.Manifest.Hooks?.Points.Contains(HookPoints.Event, StringComparer.Ordinal) == true;

    /// <summary>The plugin's queue attached before its process starts, and the handshake that says where it begins.</summary>
    public static WorkEventHandshake Join(WorkEventPoint point, string plugin, string home) =>
        new(WorkEventGrammar.Version, point.Join(plugin), Build(home));

    /// <summary>
    /// Whether this process holds <paramref name="home"/>, read once a look: a read the lease file's lock made busy keeps the answer before
    /// it, so a moment's lock neither stops a plugin nor counts its exit (P6).
    /// </summary>
    public static Func<bool> Held(string home)
    {
        var last = false;
        return () => last = HomeLease.HeldHereRead(home) ?? last;
    }
}
