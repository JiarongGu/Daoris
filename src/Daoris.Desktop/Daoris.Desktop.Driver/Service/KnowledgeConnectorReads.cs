namespace Daoris.Driver;

/// <summary>
/// The knowledge connector as this machine has it (ACP4, CONNECTOR1): named on <see cref="KnowledgeConnector"/>, where every
/// caller named them, as static members of an extension block (STRUCT3a's pattern, D166's STRUCT3d note); the order of the
/// places and the server's surroundings stay the Domain's, and the files and the environment are read through
/// <see cref="ServiceMachine"/>.
/// </summary>
public static class KnowledgeConnectorReads
{
    extension(KnowledgeConnector)
    {
        /// <summary>
        /// The server to offer, or <c>null</c> when this machine has no host to offer.
        /// </summary>
        /// <remarks>
        /// Null is an answer, not a failure: the session drives exactly as it did before ACP4, which is
        /// without a connector. Refusing to start would make a missing optional binary fatal to a loop
        /// that has other work to do.
        /// </remarks>
        /// <param name="environment">The surroundings to pass through; null reads this process's.</param>
        /// <param name="scope">What this session's connector carries beyond the store (<see cref="KnowledgeConnector.Server"/>).</param>
        public static AcpMcpServer? Offer(
            string? explicitPath, string? home, string baseDirectory,
            IReadOnlyDictionary<string, string?>? environment = null,
            IReadOnlyDictionary<string, string>? scope = null)
        {
            var located = KnowledgeConnector.Locate(explicitPath, home, baseDirectory);
            if (located is null) return null;

            Func<string, string?> variable = environment is not null
                ? name => environment.TryGetValue(name, out var held) ? held : null
                : ServiceMachine.Variable;
            return KnowledgeConnector.Server(located, variable, home, scope);
        }

        /// <summary>The first place this machine actually has the host, or null.</summary>
        public static string? Locate(string? explicitPath, string? home, string baseDirectory) =>
            KnowledgeConnector.Candidates(explicitPath, home, baseDirectory).FirstOrDefault(ServiceMachine.Exists);

        /// <summary>
        /// Where to look, in the order they deserve trust (<see cref="KnowledgeConnector.Ranked"/>), a development build's
        /// workspace found by walking up from the running application to the folder that holds its manifest.
        /// </summary>
        public static IReadOnlyList<string> Candidates(string? explicitPath, string? home, string baseDirectory) =>
            KnowledgeConnector.Ranked(
                explicitPath, home, baseDirectory, ServiceMachine.FolderWith(baseDirectory, KnowledgeConnector.WorkspaceMarker));
    }
}
