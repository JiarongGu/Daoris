namespace Daoris.Driver;

/// <summary>
/// This machine's remotes map, read and written (D63, WSP3): named on <see cref="RemoteTarget"/>, where every caller named
/// them, as static members of an extension block (STRUCT3a's pattern, D166's STRUCT3d note); the map's rules stay the
/// Domain's, and the file and the environment are read through <see cref="ServiceMachine"/>.
/// </summary>
public static class RemoteTargetReads
{
    extension(RemoteTarget)
    {
        /// <summary>
        /// The map's conventional home, beside the driver's own config under the Daoris home (D63) — and
        /// null where there is none: a machine with no home has no remotes, the documented default anyway.
        /// </summary>
        public static string? DefaultPath => DaorisHome.File(ServiceMachine.Variable, RemoteTarget.FileName);

        /// <summary>The file every door reads and writes — the override, or the conventional home, as the driver's and the harnesses' are.</summary>
        public static string? ResolvePath() => ServiceMachine.Variable(RemoteTarget.PathVariable) ?? RemoteTarget.DefaultPath;

        /// <summary>This machine's remotes, by workspace. Absence is the default and it is silent (D21).</summary>
        public static IReadOnlyDictionary<string, RemoteTarget> Load() => RemoteTarget.Load(ServiceMachine.Variable, RemoteTarget.ResolvePath());

        /// <summary>
        /// The testable shape: the same judgement over injected surroundings. The environment's answer, where it gives one
        /// (<see cref="RemoteTarget.FromEnvironment"/>), else the file's (<see cref="RemoteTarget.Parse"/>).
        /// </summary>
        public static IReadOnlyDictionary<string, RemoteTarget> Load(Func<string, string?> environment, string? path) =>
            RemoteTarget.FromEnvironment(environment) ?? RemoteTarget.Parse(ServiceMachine.TextIfThere(path));

        /// <summary>
        /// The map as it sits on disk, whatever the environment currently says — what an EDITOR reads.
        /// </summary>
        /// <remarks>
        /// An edit must land in the file even when the environment outranks it (a machine with the env
        /// pair set could otherwise never wire a second workspace); which one is LIVE is a separate
        /// question, and the surface answers it separately. Same judgement as the CLI's `remote` verb.
        /// </remarks>
        public static IReadOnlyDictionary<string, RemoteTarget> LoadFile(string path) =>
            RemoteTarget.Load(_ => null, path);

        /// <summary>
        /// Write the map back — atomically, beside-then-rename, like every write in this family: a driver
        /// tick may read this file at any moment, and a torn read must never be what it finds.
        /// </summary>
        public static void Save(string path, IReadOnlyDictionary<string, RemoteTarget> remotes) =>
            ServiceMachine.WriteText(path, RemoteTarget.Json(remotes));
    }
}
